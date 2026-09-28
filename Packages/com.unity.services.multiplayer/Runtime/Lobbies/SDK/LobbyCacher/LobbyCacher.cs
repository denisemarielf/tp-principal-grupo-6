using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;

namespace Lobbies.SDK.LobbyCacher
{
    class LobbyCacher
    {
        readonly string m_PlayerID;
        readonly Dictionary<string, LobbyEntry> m_LobbyCacher;

        class LobbyEntry : ILobbyEvents
        {
            public bool IsSubscribed { get; private set; }
            public Lobby Lobby { get; set; }
            public LobbyEventCallbacks Callbacks { get; set; }

            public Task SubscribeAsync()
            {
                if (Callbacks == null)
                    throw new InvalidOperationException(
                        "No callbacks set for lobby events. Set callbacks using WithEventSubscription before subscribing.");

                IsSubscribed = true;
                return Task.CompletedTask;
            }

            public Task UnsubscribeAsync()
            {
                IsSubscribed = false;
                return Task.CompletedTask;
            }
        }

        public LobbyCacher(string playerID = "")
        {
            m_PlayerID = playerID;
            m_LobbyCacher = new Dictionary<string, LobbyEntry>();
        }

        public ILobbyEvents WithEventSubscription(string lobbyId, LobbyEventCallbacks lobbyCallbacks)
        {
            if (!m_LobbyCacher.TryGetValue(lobbyId, out var lobbyEntry))
            {
                return null;
            }

            lobbyEntry.Callbacks = lobbyCallbacks;
            lobbyEntry.SubscribeAsync();

            return lobbyEntry;
        }

        /// <summary>
        /// Attempts to get the cached <see
        /// cref="Lobby"/> for the specified lobby.
        /// </summary>
        /// <param name="lobbyId">
        /// The identifier of the lobby to look up.
        /// </param>
        /// <param name="cachedLobby">
        /// When this method returns, contains the cached <see
        /// cref="Lobby"/> for <paramref name="lobbyId"/> if
        /// one is present; otherwise, <see langword="null"/>.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if a non-<see langword="null"/>
        /// <see cref="Lobby"/> is cached for <paramref
        /// name="lobbyId"/>; otherwise, <see langword="false"/>.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="lobbyId"/> is <see langword="null"/>.
        /// </exception>
        public bool TryGetLobbyCache(string lobbyId, out Lobby cachedLobby)
        {
            cachedLobby = m_LobbyCacher.TryGetValue(lobbyId, out var lobbyEntry) ? lobbyEntry.Lobby : null;
            return cachedLobby != null;
        }

        public string GetIfMatchTag(string lobbyId, bool applyIfMatch = true)
        {
            return applyIfMatch ? GetLobbyCacheVersion(lobbyId) : default;
        }

        string GetLobbyCacheVersion(string lobbyId)
        {
            return TryGetLobbyCache(lobbyId, out var cachedLobby) ? cachedLobby.Version.ToString() : null;
        }

        public bool RemoveLobbyCache(string lobbyId)
        {
            return m_LobbyCacher.Remove(lobbyId);
        }

        public void UpdateLobbyCache(string lobbyId, Lobby newLobby)
        {
            if (!TryGetLobbyCache(lobbyId, out var cachedLobby))
            {
                return;
            }

            UpdateLobbyCache(lobbyId, LobbyPatcher.GetLobbyDiff(cachedLobby, newLobby));
        }

        public void UpdateLobbyCache(string lobbyId, ILobbyChanges changes)
        {
            if (!TryGetLobbyCache(lobbyId, out var cachedLobby))
            {
                return;
            }

            LobbyEntry cachedEntry;
            // if the lobby was deleted, remove it from the cache and invoke the callback
            if (changes.LobbyDeleted)
            {
                if (m_LobbyCacher.TryGetValue(lobbyId, out cachedEntry))
                {
                    var removed = RemoveLobbyCache(lobbyId);
                    if (removed && cachedEntry is { IsSubscribed : true })
                    {
                        cachedEntry.Callbacks?.InvokeLobbyChanged(changes);
                        cachedEntry.UnsubscribeAsync();
                    }
                }

                return;
            }

            // if the cached lobby version is older than the changed lobby version, update the cached lobby
            // and invoke the callback (or InvokeKickedFromLobby if the player was removed)
            if (cachedLobby.Version < changes.Version.Value)
            {
                if (WasRemovedFromLobby(changes, cachedLobby))
                {
                    if (m_LobbyCacher.TryGetValue(cachedLobby.Id, out cachedEntry) && cachedEntry.IsSubscribed)
                    {
                        cachedEntry.Callbacks?.InvokeKickedFromLobby();
                    }
                    return;
                }

                changes.ApplyToLobby(cachedLobby);
                if (m_LobbyCacher.TryGetValue(cachedLobby.Id, out cachedEntry) && cachedEntry.IsSubscribed)
                {
                    cachedEntry.Callbacks?.InvokeLobbyChanged(changes);
                }
            }
        }

        public bool AddLobbyCache(string lobbyId, Lobby lobby)
        {
            if (m_LobbyCacher.TryGetValue(lobbyId, out var cachedLobby))
            {
                if (cachedLobby.Lobby != null)
                {
                    return false;
                }

                cachedLobby.Lobby = lobby;
                return true;
            }

            m_LobbyCacher[lobbyId] = new LobbyEntry { Lobby = lobby };
            return true;
        }

        bool WasRemovedFromLobby(ILobbyChanges changes, Lobby cachedLobby)
        {
            if (cachedLobby == null || !changes.PlayerLeft.Changed)
                return false;

            // If the current player left and has not joined again, it has been removed from the lobby (kicked, or left)
            if (changes.PlayerLeft.Value.Exists(playerIdx => cachedLobby.Players[playerIdx].Id == m_PlayerID)
                && (changes.PlayerJoined.Value == null ||
                    !changes.PlayerJoined.Value.Exists(joinEvt => joinEvt.Player.Id == m_PlayerID)))
            {
                RemoveLobbyCache(cachedLobby.Id);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Invalidates the cache for a Lobby.
        /// </summary>
        /// <param name="lobbyId">The Lobby id.</param>
        public void InvalidateCacheForLobby(string lobbyId)
        {
            if (m_LobbyCacher.TryGetValue(lobbyId, out var lobbyEntry))
            {
                lobbyEntry.Lobby = null;
            }
        }
    }
}
