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
        public const string DEFAULT_IP = "0.0.0.0";
        public const ushort DEFAULT_PORT = 7457;

        private static ushort GetAvailableTcpPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return (ushort)port;
        }

        static void ConfigureNetworkManager(NetworkManager networkManager, ushort port, string ip, string? listenEndpoint = null)
        {
            if (port == 0)
            {
                port = GetAvailableTcpPort();
            }

            UnityEngine.Object.Destroy(networkManager.NetworkConfig.NetworkTransport);
            UnityTransport unityTransport = networkManager.gameObject.AddComponent<UnityTransport>();
            networkManager.NetworkConfig.NetworkTransport = unityTransport;
            unityTransport.SetConnectionData(ip, port, listenEndpoint);

            Debug.Log($"[Local/LAN Multiplayer] Port = {port}, IP = {ip}{(listenEndpoint != null ? $" Listen Endpoint: {listenEndpoint}" : "")}");
        }

        [ConsoleCommand]
        public static void StartServer()
        {
            HasteNetworking.SetState(HasteNetworking.State.Host, (networkManager) => ConfigureNetworkManager(networkManager, DEFAULT_PORT, DEFAULT_IP));
        }
        [ConsoleCommand]
        public static void StartServer(ushort port)
        {
            HasteNetworking.SetState(HasteNetworking.State.Host, (networkManager) => ConfigureNetworkManager(networkManager, port, DEFAULT_IP));
        }
        [ConsoleCommand]
        public static void StartServer(ushort port, string ip)
        {
            HasteNetworking.SetState(HasteNetworking.State.Host, (networkManager) => ConfigureNetworkManager(networkManager, port, ip));
        }
        [ConsoleCommand]
        public static void StartServer(ushort port, string ip, string listenEndpoint)
        {
            HasteNetworking.SetState(HasteNetworking.State.Host, (networkManager) => ConfigureNetworkManager(networkManager, port, ip, listenEndpoint));
        }

        [ConsoleCommand]
        public static void Connect()
        {
            HasteNetworking.SetState(HasteNetworking.State.Client, (networkManager) => ConfigureNetworkManager(networkManager, DEFAULT_PORT, DEFAULT_IP));
        }
        [ConsoleCommand]
        public static void Connect(ushort port)
        {
            HasteNetworking.SetState(HasteNetworking.State.Client, (networkManager) => ConfigureNetworkManager(networkManager, port, DEFAULT_IP));
        }
        [ConsoleCommand]
        public static void Connect(ushort port, string ip)
        {
            HasteNetworking.SetState(HasteNetworking.State.Client, (networkManager) => ConfigureNetworkManager(networkManager, port, ip));
        }
        [ConsoleCommand]
        public static void Connect(ushort port, string ip, string? listenEndpoint = null)
        {
            HasteNetworking.SetState(HasteNetworking.State.Client, (networkManager) => ConfigureNetworkManager(networkManager, port, ip));
        }
    }
}
