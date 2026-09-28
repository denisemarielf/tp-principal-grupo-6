using System;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.Services.DistributedAuthority.Apis.DistributedAuthority;
using Unity.Services.DistributedAuthority.DistributedAuthority;
using Unity.Services.DistributedAuthority.ErrorMitigation;
using Unity.Services.DistributedAuthority.Exceptions;
using Unity.Services.DistributedAuthority.Http;
using Unity.Services.DistributedAuthority.Internal;
using Unity.Services.DistributedAuthority.Models;
using Unity.Services.DistributedAuthority.SDK;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Multiplayer;
using Unity.Services.Qos.Internal;
using Unity.Services.Relay;
using Session = Unity.Services.DistributedAuthority.Models.Session;

namespace Unity.Services.DistributedAuthority
{
    /// <summary>
    /// This class allows a game client to interact
    /// with the Unity Distributed Authority Service.
    /// </summary>
    class WrappedDistributedAuthorityService : IDistributedAuthoritySDKConfiguration, IDistributedAuthorityService
    {
        private const string QosRelayServiceName = "relay";

        Configuration Configuration { get; }

        readonly IRetryPolicyProvider m_RetryPolicyProvider;
        readonly IClock m_Clock;
        readonly IInternalDaLobbyService m_LobbyService;
        readonly IRelayService m_RelayService;
        readonly IQosResults m_QosResults;

        readonly IDistributedAuthorityApiClient m_ApiClient;

        internal WrappedDistributedAuthorityService(
            IDistributedAuthorityApiClient apiClient,
            IRetryPolicyProvider retryPolicyProvider,
            IClock clock,
            Configuration configuration,
            IInternalDaLobbyService internalLobbyService,
            IRelayService relayService,
            IQosResults qosResults)
        {
            m_ApiClient = apiClient;
            m_RetryPolicyProvider = retryPolicyProvider;
            m_Clock = clock;
            Configuration = configuration;
            m_LobbyService = internalLobbyService;
            m_RelayService = relayService;
            m_QosResults = qosResults;
        }

        public void SetBasePath(string basePath)
        {
            Configuration.BasePath = basePath;
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentException">Returned when the lobby ID is not valid.</exception>
        public async Task<Session> CreateSessionForLobbyIdAsync(string lobbyId, string region = null)
        {
            if (string.IsNullOrEmpty(lobbyId))
            {
                throw new ArgumentException("Lobby ID cannot be null or empty.", nameof(lobbyId));
            }

            if (string.IsNullOrEmpty(region) && m_QosResults != null && m_RelayService != null)
            {
                try
                {
                    var regions = (await m_RelayService.ListRegionsAsync()).Select(r => r.Id).ToList();
                    var qosResults =
                        await m_QosResults.GetSortedQosResultsAsync(QosRelayServiceName, regions);
                    // pick first region in the sorted list (best latency + packet loss)
                    if (qosResults.Any())
                    {
                        region = qosResults[0].Region;
                        Logger.LogVerbose($"best region is {region}");
                    }
                    else
                    {
                        Logger.LogWarning($"No Qos region selected. Will use default.");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Could not do Qos region selection. Will use default.{Environment.NewLine}" +
                        $"QoS failed due to [{ex.GetType().Name}]. Reason: {ex.Message}");
                }
            }

            var request = new CreateSessionRequest(new Session(lobbyId, region));

            try
            {
                var response = await m_ApiClient.CreateSessionAsync(request);
                return response.Result;
            }
            catch (HttpException<ErrorResponseBody> e)
            {
                throw new DistributedAuthorityServiceException(e.ActualError.GetExceptionReason(), e.ActualError.GetExceptionMessage(), e);
            }
            catch (HttpException e)
            {
                if (e.Response.IsHttpError)
                {
                    throw new DistributedAuthorityServiceException(e.Response.GetExceptionReason(), e.Response.ErrorMessage, e);
                }

                if (e.Response.IsNetworkError)
                {
                    throw new DistributedAuthorityServiceException(DistributedAuthorityExceptionReason.NetworkError, e.Response.ErrorMessage);
                }

                throw new RequestFailedException((int)DistributedAuthorityExceptionReason.Unknown, "Something went wrong.", e);
            }
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentException">Returned when the lobby ID is not valid and when the timeout is not a positive number.</exception>
        public Task<string> JoinSessionForLobbyIdAsync(string lobbyId, TimeSpan timeout)
        {
            if (string.IsNullOrEmpty(lobbyId))
            {
                throw new ArgumentException("Lobby Id cannot be null or empty.", nameof(lobbyId));
            }

            if (timeout <= TimeSpan.Zero)
            {
                throw new ArgumentException("Timeout must be greater than 0.", nameof(timeout));
            }

            return DoAsync();

            async Task<string> DoAsync()
            {
                try
                {
                    return await PollLobbyForJoinCodeAsync(lobbyId, timeout);
                }
                catch (HttpException<ErrorResponseBody> e)
                {
                    throw new DistributedAuthorityServiceException(e.ActualError.GetExceptionReason(), e.ActualError.GetExceptionMessage(), e);
                }
                catch (HttpException e)
                {
                    if (e.Response.IsHttpError)
                    {
                        throw new DistributedAuthorityServiceException(e.Response.GetExceptionReason(), e.Response.ErrorMessage, e);
                    }

                    if (e.Response.IsNetworkError)
                    {
                        throw new DistributedAuthorityServiceException(DistributedAuthorityExceptionReason.NetworkError, e.Response.ErrorMessage);
                    }

                    throw new RequestFailedException((int)DistributedAuthorityExceptionReason.Unknown, "Something went wrong.", e);
                }
            }
        }

        public Task<string> JoinSessionForLobbyIdAsync(string lobbyId)
        {
            return JoinSessionForLobbyIdAsync(lobbyId, IDistributedAuthorityService.DefaultTimeout);
        }

        async Task<string> PollLobbyForJoinCodeAsync(string lobbyId, TimeSpan timeout)
        {
            const float MaxDelayInSeconds = 2f;
            const float JitterMagnitudeInSeconds = 0.5f;

            var lobby = await m_LobbyService.JoinLobbyByIdAsync(lobbyId);
            if (TryGetJoinCode(lobby, out var joinCode))
            {
                return joinCode;
            }

            var start = m_Clock.UtcNow();
            var maxRetries = (uint)Math.Floor(timeout.TotalSeconds / MaxDelayInSeconds);
            lobby = await m_RetryPolicyProvider
                .ForOperation(async() => await m_LobbyService.GetLobbyAsync(lobbyId))
                    .WithRetryCondition(l => Task.FromResult(ShouldRetry(l, start, timeout)))
                    .WithJitterMagnitude(JitterMagnitudeInSeconds)
                    .WithMaxDelayTime(MaxDelayInSeconds)
                    .HandleException<LobbyServiceException>(ex => ex.Reason == LobbyExceptionReason.RateLimited)
                    .UptoMaximumRetries(maxRetries)
                    .RunAsync();

            if (TryGetJoinCode(lobby, out joinCode))
            {
                return joinCode;
            }

            throw new DistributedAuthorityServiceException(DistributedAuthorityExceptionReason.RequestTimeOut, $"Timed out after {(m_Clock.UtcNow() - start).TotalSeconds:F0}s waiting for the relay join code.");
        }

        /// <summary>
        /// Determines whether polling for the Relay join code should continue.
        /// </summary>
        /// <param name="lobby">
        /// The lobby to check for a join code. Can be <see langword="null"/>.
        /// </param>
        /// <param name="start">
        /// The time at which polling began.
        /// </param>
        /// <param name="timeout">
        /// The maximum duration to poll for before giving up.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if <paramref name="lobby"/> does not yet
        /// carry a join code (as determined by <see cref="TryGetJoinCode"/>)
        /// and the elapsed time since <paramref name="start"/> has not reached
        /// <paramref name="timeout"/>; otherwise, <see langword="false"/>.
        /// </returns>
        bool ShouldRetry(Lobby lobby, DateTimeOffset start, TimeSpan timeout)
        {
            if (TryGetJoinCode(lobby, out _))
            {
                return false;
            }

            return m_Clock.UtcNow().Subtract(start) < timeout;
        }

        /// <summary>
        /// Tries to get the Relay join code from the
        /// session metadata stored on the specified lobby.
        /// </summary>
        /// <param name="lobby">
        /// The lobby to read the session metadata from.
        /// Can be <see langword="null"/>, in which case
        /// the method returns <see langword="false"/>.
        /// </param>
        /// <param name="joinCode">
        /// When this method returns <see langword="true"/>, contains the
        /// non-null, non-empty Relay join code read from <paramref name="lobby"/>.
        /// When this method returns <see langword="false"/>, is set to
        /// <see cref="System.String.Empty"/>. This value is never
        /// <see langword="null"/> on return.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if and only if <paramref name="joinCode"/> is
        /// a non-null, non-empty string; otherwise, <see langword="false"/>.
        /// This is the case exactly when <paramref name="lobby"/> carries a
        /// non-null, non-empty <see cref="NetworkMetadata.RelayJoinCode"/>.
        /// </returns>
        /// <remarks>
        /// A session property value that cannot be deserialized into a <see
        /// cref="NetworkMetadata"/> instance is logged and treated as a missing
        /// join code, causing the method to return <see langword="false"/>.
        /// </remarks>
        static bool TryGetJoinCode(Lobby lobby, out string joinCode)
        {
            joinCode = string.Empty;

            if (lobby == null)
            {
                Logger.LogVerbose($"{nameof(lobby)} is null.");
                return false;
            }

            if (lobby.Data == null)
            {
                Logger.LogVerbose($"{nameof(lobby.Data)} is null. Lobby id: {lobby.Id}");
                return false;
            }

            var hasSessionProperty =
                lobby.Data.TryGetValue(NetworkModule.SessionPropertyKey, out var sessionPropertyJson);
            if (!hasSessionProperty)
            {
                Logger.LogVerbose($"{nameof(lobby.Data)} does not contain key \"{NetworkModule.SessionPropertyKey}\". Lobby id: {lobby.Id}");
                return false;
            }

            if (sessionPropertyJson == null)
            {
                Logger.LogVerbose($"{nameof(sessionPropertyJson)} is null. Lobby id: {lobby.Id}");
                return false;
            }

            if (string.IsNullOrEmpty(sessionPropertyJson.Value))
            {
                Logger.LogVerbose($"{nameof(sessionPropertyJson.Value)} is null or empty. Lobby id: {lobby.Id}");
                return false;
            }

            try
            {
                var networkMetadata = JsonConvert.DeserializeObject<NetworkMetadata>(sessionPropertyJson.Value);

                if (networkMetadata == null)
                {
                    Logger.LogVerbose($"{nameof(networkMetadata)} is null. Lobby id: {lobby.Id}");
                    return false;
                }

                if (string.IsNullOrEmpty(networkMetadata.RelayJoinCode))
                {
                    Logger.LogVerbose($"{nameof(networkMetadata.RelayJoinCode)} is null or empty. Lobby id: {lobby.Id}");
                    return false;
                }

                joinCode = networkMetadata.RelayJoinCode;
                return true;
            }
            catch (Exception e)
            {
                Logger.LogException(e);
                return false;
            }
        }
    }
}
