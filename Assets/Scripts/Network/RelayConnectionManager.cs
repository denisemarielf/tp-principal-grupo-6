using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

public static class RelayConnectionManager
{
    private const string ConnectionType = "dtls";

    public static async Task EnsureSignedInAsync()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
        {
            // Un perfil distinto por instancia: con Multiplayer Play Mode o dos builds en la misma PC,
            // sin esto todas comparten la misma cuenta anonima y el host y el cliente serian el mismo jugador.
            var options = new InitializationOptions().SetProfile("p" + Guid.NewGuid().ToString("N").Substring(0, 20));
            await UnityServices.InitializeAsync(options);
        }

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
    }

    public static async Task<string> StartHostAsync(int maxConnections)
    {
        await EnsureSignedInAsync();

        Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
        string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, ConnectionType));

        NetworkManager.Singleton.StartHost();

        return joinCode;
    }

    public static async Task StartClientAsync(string joinCode)
    {
        await EnsureSignedInAsync();

        JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetRelayServerData(AllocationUtils.ToRelayServerData(joinAllocation, ConnectionType));

        NetworkManager.Singleton.StartClient();
    }
}
