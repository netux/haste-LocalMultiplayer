using Landfall.Haste;
using Landfall.Modding;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using Zorro.Core;
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

        [ConsoleCommand]
        public static async Task StartServer()
        {
            await StartServerAtPort(DEFAULT_PORT);
        }

        [ConsoleCommand]
        public static async Task StartServerAtPort(ushort port)
        {
            await StartServerAtIpPortAndListenEndpoint(DEFAULT_HOST_IP, port);
        }

        [ConsoleCommand]
        public static async Task StartServerAtAddress(string address)
        {
            await StartServerAtAddressAndListenEndpoint(address, null);
        }

        [ConsoleCommand]
        public static async Task StartServerAtAddressAndListenEndpoint(string address, string? listenEndpoint = null)
        {
            if (!TryParseIPEndPoint(address, out IPEndPoint ipEndpoint))
            {
                Debug.LogError($"[{MOD_PREFIX}] Invalid address '{address}'");
                return;
            }

            string ipAddress = ipEndpoint.Address.ToString();
            ushort port = ipEndpoint.Port <= 0 ? DEFAULT_PORT : (ushort)ipEndpoint.Port;
            await StartServerAtIpPortAndListenEndpoint(ipAddress, port, listenEndpoint);
        }

        public static async Task StartServerAtIpPortAndListenEndpoint(string ip, ushort port, string? listenEndpoint = null)
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

            await SetLocalNetworkingState(HasteNetworking.State.SteamHost, ip, port, listenEndpoint);

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

        private static async Task SetLocalNetworkingState(
            HasteNetworking.State state,
            string ip,
            ushort port,
            string? listenEndpoint
        )
        {
            static void ConfigureNetworkManager(NetworkManager networkManager, string ip, ushort port, string? listenEndpoint = null)
            {
                UnityEngine.Object.Destroy(networkManager.NetworkConfig.NetworkTransport);
                UnityTransport unityTransport = networkManager.gameObject.AddComponent<UnityTransport>();
                networkManager.NetworkConfig.NetworkTransport = unityTransport;
                unityTransport.SetConnectionData(ip, port, listenEndpoint);
            }

            if (NetworkManager.Singleton)
            {
                NetworkManager.Singleton.Shutdown(false);
                while (NetworkManager.Singleton.ShutdownInProgress)
                {
                    await Awaitable.NextFrameAsync();
                }
                UnityEngine.Object.Destroy(NetworkManager.Singleton.gameObject);
                await Awaitable.NextFrameAsync();
            }

            if (state == HasteNetworking.State.Off)
            {
                return;
            }


            UnityEngine.Object.Instantiate(SingletonAsset<StaticReferences>.Instance.NetworkManagerPrefab);
            await Awaitable.NextFrameAsync();

            if (NetworkManager.Singleton == null)
            {
                Debug.LogError($"[{MOD_PREFIX}] NetworkManager null a frame after instantiation? Aborting setting network state.");
                return;
            }

            NetworkManager.Singleton.OnConnectionEvent += HasteNetworking.OnConnectionEvent;
            NetworkManager.Singleton.OnPreShutdown += delegate
            {
                HasteNetworking._currentConfig = HasteNetworking.Config.Off;
            };

            bool isHost = state == HasteNetworking.State.SteamHost;
            if (isHost)
            {
                ConfigureNetworkManager(NetworkManager.Singleton, ip, port);
            }
            else
            {
                ConfigureNetworkManager(NetworkManager.Singleton, ip, port, listenEndpoint);
            }

            bool wasSuccessful = isHost
                ? NetworkManager.Singleton.StartClient()
                : NetworkManager.Singleton.StartHost();
            if (!wasSuccessful)
            {
                Debug.LogError($"[{MOD_PREFIX}] Connection unsuccessful :(");
                return;
            }

            HasteNetworking._currentConfig = state == HasteNetworking.State.SteamHost
                ? HasteNetworking.Config.SteamHost
                : HasteNetworking.Config.SteamClient(new Steamworks.CSteamID()); // this *just* works, surprisingly
        }

        [ConsoleCommand]
        public static async Task ConnectTo(string address)
        {
            if (!TryParseIPEndPoint(address, out IPEndPoint ipEndpoint))
            {
                Debug.LogError($"[{MOD_PREFIX}] Invalid address '{address}'");
                return;
        }

            string ipAddress = ipEndpoint.Address.ToString();
            ushort port = ipEndpoint.Port <= 0 ? DEFAULT_PORT : (ushort)ipEndpoint.Port;
            await ConnectToIpPort(ipAddress, port);
        }

        public static async Task ConnectToIpPort(string ip, ushort port)
        {
            if (port == 0)
        {
                Debug.LogError($"[{MOD_PREFIX}] Its not possible to connect to port 0!");
                return;
            }

            Debug.Log($"[{MOD_PREFIX}] Connecting to {ip}:{port}...");

            await SetLocalNetworkingState(HasteNetworking.State.SteamClient, ip, port, null);
        }

        [ConsoleCommand]
        public static async Task ConnectToLoopback()
        {
            await ConnectToLoopbackAtPort(DEFAULT_PORT);
        }

        [ConsoleCommand]
        public static async Task ConnectToLoopbackAtPort(ushort port)
        {
            await ConnectToIpPort("127.0.0.1", port);
        }

        [ConsoleCommand]
        public static void Disconnect()
        {
            Debug.Log($"[{MOD_PREFIX}] Disconnecting");

            HasteNetworking.SetState(HasteNetworking.Config.Off);
        }
    }
}
