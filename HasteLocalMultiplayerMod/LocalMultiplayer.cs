using Landfall.Haste;
using Landfall.Modding;
using System.Net;
using System.Net.NetworkInformation;
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

        private static bool TryParseIPEndPoint(string address, out IPEndPoint ipEndpoint)
        {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
            ipEndpoint = null;
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.

            var addressSplit = address.Split(":", 2);
            if (addressSplit.Length == 0)
            {
                return false;
            }

            IPAddress ipAddress;
            int port = 0;

            if (!IPAddress.TryParse(addressSplit[0], out ipAddress))
            {
                return false;
            }

            if (addressSplit.Length == 2)
            {
                if (!int.TryParse(addressSplit[1], out port))
                {
                    return false;
                }
            }

            ipEndpoint = new IPEndPoint(ipAddress, port);
            return true;
        }

        private static ushort GetAvailableTcpPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return (ushort)port;
        }

        private static string[] GetInterNetworkIpAddresses()
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where((networkInterface) => networkInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || networkInterface.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                .SelectMany((networkInterface) => networkInterface.GetIPProperties().UnicastAddresses)
                .Select((addressInfo) => addressInfo.Address)
                .Where((address) => address.AddressFamily == AddressFamily.InterNetwork)
                .Select((address) => address.ToString())
                .ToArray();
        }

        static void ConfigureNetworkManager(NetworkManager networkManager, string ip, ushort port, string? listenEndpoint = null)
        {
            UnityEngine.Object.Destroy(networkManager.NetworkConfig.NetworkTransport);
            UnityTransport unityTransport = networkManager.gameObject.AddComponent<UnityTransport>();
            networkManager.NetworkConfig.NetworkTransport = unityTransport;
            unityTransport.SetConnectionData(ip, port, listenEndpoint);
        }

        [ConsoleCommand]
        public static void StartServer()
        {
            StartServerAtPort(DEFAULT_PORT);
        }

        [ConsoleCommand]
        public static void StartServerAtPort(ushort port)
        {
            StartServerAtIpPortAndListenEndpoint(DEFAULT_HOST_IP, port);
        }

        [ConsoleCommand]
        public static void StartServerAtAddress(string address)
        {
            StartServerAtAddressAndListenEndpoint(address, null);
        }

        [ConsoleCommand]
        public static void StartServerAtAddressAndListenEndpoint(string address, string? listenEndpoint = null)
        {
            if (!TryParseIPEndPoint(address, out IPEndPoint ipEndpoint))
            {
                Debug.LogError($"[{MOD_PREFIX}] Invalid address '{address}'");
                return;
            }

            string ipAddress = ipEndpoint.Address.ToString();
            ushort port = ipEndpoint.Port <= 0 ? DEFAULT_PORT : (ushort)ipEndpoint.Port;
            StartServerAtIpPortAndListenEndpoint(ipAddress, port, listenEndpoint);
        }

        public static void StartServerAtIpPortAndListenEndpoint(string ip, ushort port, string? listenEndpoint = null)
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

            var availableLanIps = GetInterNetworkIpAddresses();
            if (availableLanIps.Length > 0)
            {
                Debug.Log($"[{MOD_PREFIX}] Connect via LAN with one of: {string.Join(", ", availableLanIps.Select((lanIp) => $"{lanIp}:{port}"))}");
            }
            else
            {
                Debug.LogWarning($"[{MOD_PREFIX}] No available intranet IPs available for LAN connection!");
            }
        }

        [ConsoleCommand]
        public static void ConnectTo(string address)
        {
            if (!TryParseIPEndPoint(address, out IPEndPoint ipEndpoint))
            {
                Debug.LogError($"[{MOD_PREFIX}] Invalid address '{address}'");
                return;
        }

            string ipAddress = ipEndpoint.Address.ToString();
            ushort port = ipEndpoint.Port <= 0 ? DEFAULT_PORT : (ushort)ipEndpoint.Port;
            ConnectToIpPort(ipAddress, port);
        }

        public static void ConnectToIpPort(string ip, ushort port)
        {
            if (port == 0)
        {
                Debug.LogError($"[{MOD_PREFIX}] Its not possible to connect to port 0!");
                return;
            }

            Debug.Log($"[{MOD_PREFIX}] Connecting to {ip}:{port}...");

            HasteNetworking.SetState(HasteNetworking.State.Client, (networkManager) => ConfigureNetworkManager(networkManager, ip, port));
        }

        [ConsoleCommand]
        public static void ConnectToLoopback()
        {
            ConnectToLoopbackAtPort(DEFAULT_PORT);
        }

        [ConsoleCommand]
        public static void ConnectToLoopbackAtPort(ushort port)
        {
            ConnectToIpPort("127.0.0.1", port);
        }

        [ConsoleCommand]
        public static void Disconnect()
        {
            Debug.Log($"[{MOD_PREFIX}] Disconnecting");

            HasteNetworking.SetState(HasteNetworking.State.Off, (networkManager) => { /* no-op */ });
        }
    }
}
