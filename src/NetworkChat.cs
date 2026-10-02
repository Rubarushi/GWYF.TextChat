using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HarmonyLib;
using Mirror;
using Steamworks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GWYF.TextChat
{
    public struct ChatWireV2 : NetworkMessage
    {
        public byte Kind;
        public string Name;
        public byte[] Body;
    }

    internal sealed class NetworkChat
    {
        private const string CapabilityKey = "gwyf.textchat.protocol";
        private const string Version = "2";
        private readonly Action<string, string> receive;
        private readonly Dictionary<int, NetworkConnectionToClient> peers = new Dictionary<int, NetworkConnectionToClient>();
        private readonly Dictionary<int, float> lastSend = new Dictionary<int, float>();
        private NetworkConnectionToServer clientConnection;
        private bool serverWasActive;
        private float nextHello, nextMetadata;
        private CSteamID advertisedLobby;
        private LobbySettings lobby;
        internal bool HostReady { get; private set; }

        internal NetworkChat(Action<string, string> receive)
        {
            this.receive = receive;
            Writer<ChatWireV2>.write = Write;
            Reader<ChatWireV2>.read = Read;
            RegisterServer(); RegisterClient();
        }

        internal static bool IsPlayScene(string scene) => scene == "HomeScene" || scene == "CasinoScene" ||
            scene == "LoseStateScene" || scene == "WinStateScene" || scene == "SummaryScene" || scene == "GameTest";

        internal static bool InSession => NetworkClient.isConnected && NetworkClient.ready &&
            NetworkClient.localPlayer != null && IsPlayScene(SceneManager.GetActiveScene().name);

        internal void RegisterServer() => NetworkServer.ReplaceHandler<ChatWireV2>(OnServerMessage);
        internal void RegisterClient() => NetworkClient.ReplaceHandler<ChatWireV2>(OnClientMessage);
        internal void ResetServer() { peers.Clear(); lastSend.Clear(); }
        internal void ResetClient() { clientConnection = null; HostReady = false; nextHello = 0; }

        internal void Tick()
        {
            if (!NetworkServer.active && serverWasActive) ResetServer();
            serverWasActive = NetworkServer.active;
            if (!ReferenceEquals(clientConnection, NetworkClient.connection))
            {
                ResetClient(); clientConnection = NetworkClient.connection;
            }
            if (!NetworkClient.isConnected) HostReady = false;
            if (SteamManager.Initialized && Time.unscaledTime >= nextMetadata)
            {
                nextMetadata = Time.unscaledTime + 1;
                if (lobby == null) lobby = Resources.Load<LobbySettings>("LobbySettings");
                CSteamID current = lobby != null ? lobby.steamLobbyID : CSteamID.Nil;
                if (current != advertisedLobby)
                {
                    if (advertisedLobby != CSteamID.Nil) SteamMatchmaking.SetLobbyMemberData(advertisedLobby, CapabilityKey, "");
                    advertisedLobby = current;
                }
                if (current != CSteamID.Nil) SteamMatchmaking.SetLobbyMemberData(current, CapabilityKey, Version);
            }
            // Discovery uses only lobby metadata. Every chat message and handshake uses Mirror.
            // Never send an unknown Mirror message to an unmodded server.
            bool supported = NetworkClient.activeHost;
            if (!supported && SteamManager.Initialized && advertisedLobby != CSteamID.Nil)
                supported = SteamMatchmaking.GetLobbyMemberData(advertisedLobby,
                    SteamMatchmaking.GetLobbyOwner(advertisedLobby), CapabilityKey) == Version;
            if (!HostReady && NetworkClient.isConnected && NetworkClient.ready && supported && Time.unscaledTime >= nextHello)
            {
                nextHello = Time.unscaledTime + 2;
                NetworkClient.Send(new ChatWireV2 { Kind = 1, Name = Version }, Channels.Reliable);
            }
        }

        internal bool Send(string text)
        {
            if (!InSession || !HostReady) return false;
            NetworkClient.Send(new ChatWireV2 { Kind = 3, Body = ChatProtocol.Encode(text) }, Channels.Reliable);
            return true;
        }

        private void OnServerMessage(NetworkConnectionToClient connection, ChatWireV2 message)
        {
            if (!connection.isAuthenticated || !connection.isReady) return;
            if (message.Kind == 1 && message.Name == Version)
            {
                peers[connection.connectionId] = connection;
                connection.Send(new ChatWireV2 { Kind = 2, Name = Version }, Channels.Reliable);
                return;
            }
            if (message.Kind != 3 || !IsPlayScene(SceneManager.GetActiveScene().name) ||
                !peers.TryGetValue(connection.connectionId, out var peer) || !ReferenceEquals(peer, connection)) return;
            byte[] body = message.Body;
            if (!ChatProtocol.TryDecode(body, body?.Length ?? 0, out string text)) return;
            if (lastSend.TryGetValue(connection.connectionId, out float previous) && Time.unscaledTime - previous < 0.65f) return;
            lastSend[connection.connectionId] = Time.unscaledTime;
            var outgoing = new ChatWireV2 { Kind = 4, Name = SenderName(connection), Body = ChatProtocol.Encode(text) };
            foreach (var item in new List<KeyValuePair<int, NetworkConnectionToClient>>(peers))
            {
                if (!NetworkServer.connections.TryGetValue(item.Key, out var current) || !ReferenceEquals(current, item.Value))
                { peers.Remove(item.Key); lastSend.Remove(item.Key); continue; }
                if (current.isAuthenticated && current.isReady) current.Send(outgoing, Channels.Reliable);
            }
        }

        private void OnClientMessage(ChatWireV2 message)
        {
            if (message.Kind == 2 && message.Name == Version) { HostReady = true; return; }
            byte[] body = message.Body;
            if (message.Kind == 4 && HostReady && InSession && ChatProtocol.TryDecode(body, body?.Length ?? 0, out string text))
                receive(ChatProtocol.Clean(message.Name, 48), text);
        }

        private static string SenderName(NetworkConnectionToClient connection)
        {
            // Identity is derived by the host, rather than accepting a client-supplied name.
            ulong id = 0;
            if (connection is LocalConnectionToClient && SteamManager.Initialized) id = SteamUser.GetSteamID().m_SteamID;
            if (id == 0) ulong.TryParse(connection.address, out id);
            if (id == 0 && connection.identity != null)
            {
                var steam = connection.identity.GetComponentInChildren<SteamIdComponent>();
                if (steam != null) id = steam.SteamId;
            }
            if (id != 0 && SteamManager.Initialized)
            {
                string name = ChatProtocol.Clean(SteamFriends.GetFriendPersonaName(new CSteamID(id)), 48);
                if (name.Length > 0) return name;
            }
            return "Player " + connection.connectionId;
        }

        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private static void Write(NetworkWriter writer, ChatWireV2 value)
        {
            writer.WriteByte(value.Kind);
            byte[] name = Utf8.GetBytes(ChatProtocol.Clean(value.Name, 48));
            WriteBlock(writer, name, 192); WriteBlock(writer, value.Body ?? Array.Empty<byte>(), ChatProtocol.MaxPacketBytes);
        }
        private static void WriteBlock(NetworkWriter writer, byte[] bytes, int maximum)
        {
            if (bytes.Length > maximum) throw new InvalidDataException("Chat packet exceeds limit.");
            writer.WriteUShort((ushort)bytes.Length); writer.WriteBytes(bytes, 0, bytes.Length);
        }
        private static byte[] ReadBlock(NetworkReader reader, int maximum)
        {
            int length = reader.ReadUShort();
            if (length > maximum) throw new InvalidDataException("Chat packet exceeds limit.");
            return reader.ReadBytes(new byte[length], length);
        }
        private static ChatWireV2 Read(NetworkReader reader) => new ChatWireV2
        {
            Kind = reader.ReadByte(), Name = Utf8.GetString(ReadBlock(reader, 192)),
            Body = ReadBlock(reader, ChatProtocol.MaxPacketBytes)
        };

        internal void Dispose()
        {
            NetworkServer.UnregisterHandler<ChatWireV2>(); NetworkClient.UnregisterHandler<ChatWireV2>();
            if (SteamManager.Initialized && advertisedLobby != CSteamID.Nil)
                SteamMatchmaking.SetLobbyMemberData(advertisedLobby, CapabilityKey, "");
            ResetServer(); ResetClient();
        }
    }

    [HarmonyPatch(typeof(CustomNetworkManager), nameof(CustomNetworkManager.OnStartServer))]
    internal static class RegisterChatServer { private static void Prefix() => Plugin.Instance?.Chat?.RegisterServer(); }
    [HarmonyPatch(typeof(CustomNetworkManager), nameof(CustomNetworkManager.OnClientConnect))]
    internal static class RegisterChatClient { private static void Prefix() => Plugin.Instance?.Chat?.RegisterClient(); }
    [HarmonyPatch(typeof(CustomNetworkManager), nameof(CustomNetworkManager.OnStopServer))]
    internal static class StopChatServer { private static void Postfix() => Plugin.Instance?.Chat?.ResetServer(); }
    [HarmonyPatch(typeof(CustomNetworkManager), nameof(CustomNetworkManager.OnStopClient))]
    internal static class StopChatClient { private static void Postfix() => Plugin.Instance?.Chat?.ResetClient(); }
}
