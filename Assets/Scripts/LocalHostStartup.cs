using System;
using System.Net;
using System.Net.Sockets;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace RPGKarawara
{
    /// <summary>Chooses a free local listen port without changing saved transport configuration.</summary>
    public static class LocalHostStartup
    {
        public static bool TryStartHost(NetworkManager network, out ushort activePort)
        {
            activePort = 0;
            if (network == null) return false;
            var transport = network.NetworkConfig.NetworkTransport as UnityTransport;
            if (network.IsListening)
            {
                if (transport != null && transport.GetLocalEndpoint().IsValid) activePort = transport.GetLocalEndpoint().Port;
                return network.IsHost;
            }
            if (transport == null || transport.Protocol != UnityTransport.ProtocolType.UnityTransport)
                return network.StartHost();

            var original = transport.ConnectionData;
            var listenAddress = string.IsNullOrEmpty(original.ServerListenAddress)
                ? (original.IsIpv6 ? IPAddress.IPv6Loopback : IPAddress.Loopback)
                : IPAddress.Parse(original.ServerListenAddress);
            ushort port = SelectAvailablePort(listenAddress, original.Port, transport.UseWebSockets);
            transport.SetConnectionData(original.Address, port, original.ServerListenAddress);
            if (!network.StartHost())
            {
                transport.ConnectionData = original;
                return false;
            }
            activePort = transport.GetLocalEndpoint().IsValid ? transport.GetLocalEndpoint().Port : port;
            if (activePort == 0) activePort = port;
            if (port == 0)
            {
                // Publish the OS-assigned port for a subsequent local client connection.
                var selected = transport.ConnectionData;
                selected.Port = activePort;
                transport.ConnectionData = selected;
            }
            if (original.Port != 0 && activePort != original.Port)
                Debug.LogWarning($"Host port {original.Port} was occupied; listening on {activePort}. Clients must use this port.");
            return true;
        }

        public static ushort SelectAvailablePort(IPAddress address, ushort preferred, bool webSockets = false)
        {
            if (preferred == 0) return 0;
            // Try the configured port and nineteen nearby fallbacks, then let the OS choose.
            for (int offset = 0; offset < 20; offset++)
            {
                int candidate = preferred + offset;
                if (candidate > ushort.MaxValue) break;
                using (var socket = new Socket(address.AddressFamily,
                    webSockets ? SocketType.Stream : SocketType.Dgram,
                    webSockets ? ProtocolType.Tcp : ProtocolType.Udp))
                {
                    socket.ExclusiveAddressUse = true;
                    try
                    {
                        socket.Bind(new IPEndPoint(address, candidate));
                        return (ushort)candidate;
                    }
                    catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse ||
                        ex.SocketErrorCode == SocketError.AccessDenied)
                    {
                        // Occupied or reserved by the OS: keep searching.
                    }
                }
            }
            return 0;
        }
    }
}
