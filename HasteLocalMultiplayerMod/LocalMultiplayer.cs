using Landfall.Haste;
using Landfall.Modding;
using System.Net;
using System.Net.Sockets;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using Zorro.Core.CLI;

namespace HasteLocalMultiplayerMod
{
    [LandfallPlugin]
    public class LocalMultiplayer
    {
        public const string MOD_PREFIX = "Local/LAN Multiplayer";
        public const string DEFAULT_HOST_IP = "0.0.0.0";
        public const ushort DEFAULT_PORT = 7457;

        private static ushort GetAvailableTcpPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return (ushort)port;
        }

        static void ConfigureNetworkManager(NetworkManager networkManager, string ip, ushort port, string? listenEndpoint = null)
        {
            UnityEngine.Object.Destroy(networkManager.NetworkConfig.NetworkTransport);
            UnityTransport unityTransport = networkManager.gameObject.AddComponent<UnityTransport>();
            networkManager.NetworkConfig.NetworkTransport = unityTransport;
            unityTransport.SetConnectionData(ip, port, listenEndpoint);

            Debug.Log($"[Local/LAN Multiplayer] Port = {port}, IP = {ip}{(listenEndpoint != null ? $" Listen Endpoint: {listenEndpoint}" : "")}");
        }

        [ConsoleCommand]
        public static void StartServer()
        {
            StartServerAtPort(DEFAULT_PORT);
        }
        [ConsoleCommand]
        public static void StartServerAtPort(ushort port)
        {
            StartServerAtIpPort(DEFAULT_HOST_IP, port);
        }
        [ConsoleCommand]
        public static void StartServerAtIpPort(string ip, ushort port)
        {
            StartServerAtIpPortListenEndpoint(ip, port, null);
        }
        [ConsoleCommand]
        public static void StartServerAtIpPortListenEndpoint(string ip, ushort port, string? listenEndpoint = null)
        {
            if (port == 0)
            {
                port = GetAvailableTcpPort();
            }

            Debug.Log($"[{MOD_PREFIX}] Starting Server on {ip}:{port}");
            if (listenEndpoint != null)
            {
                Debug.Log($"[{MOD_PREFIX}] Listen Endpoint: {listenEndpoint}");
            }

            HasteNetworking.SetState(HasteNetworking.State.Host, (networkManager) => ConfigureNetworkManager(networkManager, ip, port, listenEndpoint));
        }

        [ConsoleCommand]
        public static void ConnectToLoopback()
        {
            ConnectToLoopbackPort(DEFAULT_PORT);
        }
        [ConsoleCommand]
        public static void ConnectToLoopbackPort(ushort port)
        {
            ConnectTo("127.0.0.1", port);
        }

        [ConsoleCommand]
        public static void ConnectTo(string ip, ushort port)
        {
            if (port == 0)
        {
                Debug.LogError($"[{MOD_PREFIX}] Cannot use port 0! Consider using the default port {DEFAULT_PORT}");
                return;
            }

            Debug.Log($"[{MOD_PREFIX}] Connecting to {ip}:{port}...");

            HasteNetworking.SetState(HasteNetworking.State.Client, (networkManager) => ConfigureNetworkManager(networkManager, ip, port));
        }
    }
}
