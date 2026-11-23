using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// The Echo Battle's link between two headsets on the same Wi-Fi (owner 2026-10-30: local Wi-Fi only; no cloud, no
    /// accounts, nothing leaves the local network). Plain UDP: the host announces itself by broadcast once a second; a
    /// guest that hears it asks to join; then the two send each other small text messages (a phrase of notes, a score).
    /// Messages that matter are numbered and resent until the other side acknowledges them, so a dropped packet never
    /// loses a turn. Only notes, scores and a random session id are sent: no names, no room data.
    /// Receiving happens on a background thread; <see cref="Poll"/> hands messages to the main thread.
    /// </summary>
    public sealed class EchoBattleLink : IDisposable
    {
        public const string Protocol = "LE2";
        public const int DefaultPort = 47777;
        public const float AnnounceSeconds = 1f, ResendSeconds = 0.3f;
        public const int MaxResends = 40;

        public enum Role { Host, Guest }

        public readonly struct Message
        {
            public readonly string Type, Payload;
            public Message(string type, string payload) { Type = type; Payload = payload; }
            public override string ToString() => $"{Type} {Payload}";
        }

        struct Outgoing
        {
            public int Seq;
            public string Text;
            public float NextSend;
            public int Sends;
        }

        readonly int m_LocalPort, m_PeerPort;
        readonly IPAddress m_Broadcast;
        readonly string m_Id = Guid.NewGuid().ToString("N").Substring(0, 8);
        readonly ConcurrentQueue<(IPEndPoint from, string text)> m_Inbox = new();
        readonly List<Outgoing> m_Unacked = new();
        readonly HashSet<int> m_Seen = new();
        UdpClient m_Socket;
        Thread m_Receiver;
        volatile bool m_Running;
        int m_NextSeq = 1;
        float m_NextAnnounce;
        IPEndPoint m_Peer;
        string m_PeerId;

        public Role Mode { get; }
        public bool Connected => m_Peer != null;
        public string Id => m_Id;
        public string Status { get; private set; } = "";

        /// <summary>Two headsets use the same port; tests on one machine give each end its own and point them at each other.</summary>
        public EchoBattleLink(Role role, int localPort = DefaultPort, int peerPort = DefaultPort, IPAddress broadcast = null)
        {
            Mode = role;
            m_LocalPort = localPort;
            m_PeerPort = peerPort;
            m_Broadcast = broadcast ?? IPAddress.Broadcast;
        }

        public void Open()
        {
            m_Socket = new UdpClient();
            m_Socket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            m_Socket.EnableBroadcast = true;
            m_Socket.Client.Bind(new IPEndPoint(IPAddress.Any, m_LocalPort));
            m_Running = true;
            m_Receiver = new Thread(Receive) { IsBackground = true, Name = "EchoBattleLink" };
            m_Receiver.Start();
            Status = Mode == Role.Host ? "Waiting for a player to join" : "Looking for a host";
        }

        void Receive()
        {
            var from = new IPEndPoint(IPAddress.Any, 0);
            while (m_Running)
            {
                try
                {
                    var bytes = m_Socket.Receive(ref from);
                    m_Inbox.Enqueue((new IPEndPoint(from.Address, from.Port), Encoding.UTF8.GetString(bytes)));
                }
                catch (SocketException) { if (!m_Running) return; }
                catch (ObjectDisposedException) { return; }
            }
        }

        /// <summary>Sends a message the other side must get (resent until acknowledged).</summary>
        public void Send(string type, string payload = "")
        {
            int seq = m_NextSeq++;
            m_Unacked.Add(new Outgoing { Seq = seq, Text = Format(type, seq, payload), NextSend = 0f });
        }

        string Format(string type, int seq, string payload) => $"{Protocol}|{type}|{seq}|{m_Id}|{payload}";

        void SendRaw(string text, IPEndPoint to)
        {
            if (m_Socket == null || to == null) return;
            var bytes = Encoding.UTF8.GetBytes(text);
            try { m_Socket.Send(bytes, bytes.Length, to); }
            catch (SocketException e) { Status = $"Network: {e.SocketErrorCode}"; }
        }

        /// <summary>Delivers what arrived since the last call (main thread), announces, resends; <paramref name="now"/> in seconds.</summary>
        public List<Message> Poll(float now)
        {
            var delivered = new List<Message>();
            while (m_Inbox.TryDequeue(out var packet)) Handle(packet.from, packet.text, delivered);

            if (Mode == Role.Host && !Connected && now >= m_NextAnnounce)
            {
                m_NextAnnounce = now + AnnounceSeconds;
                SendRaw(Format("HELLO", 0, ""), new IPEndPoint(m_Broadcast, m_PeerPort));
            }
            if (Connected)
                for (int i = 0; i < m_Unacked.Count; i++)
                {
                    var o = m_Unacked[i];
                    if (now < o.NextSend) continue;
                    if (o.Sends >= MaxResends)
                    {
                        Status = "The other headset stopped answering";
                        m_Unacked.RemoveAt(i--);
                        continue;
                    }
                    SendRaw(o.Text, m_Peer);
                    o.Sends++;
                    o.NextSend = now + ResendSeconds;
                    m_Unacked[i] = o;
                }
            return delivered;
        }

        void Handle(IPEndPoint from, string text, List<Message> delivered)
        {
            var parts = text.Split(new[] { '|' }, 5);
            if (parts.Length < 5 || parts[0] != Protocol || parts[3] == m_Id) return; // not ours, or our own broadcast
            string type = parts[1], sender = parts[3], payload = parts[4];
            if (!int.TryParse(parts[2], out int seq)) return;

            if (type == "HELLO")
            {
                if (Mode == Role.Guest && !Connected)
                {
                    m_Peer = new IPEndPoint(from.Address, m_PeerPort);
                    m_PeerId = sender;
                    Status = "Found a host; joining";
                    Send("JOIN");
                }
                return;
            }
            if (type == "ACK")
            {
                if (int.TryParse(payload, out int acked)) m_Unacked.RemoveAll(o => o.Seq == acked);
                return;
            }
            if (Mode == Role.Host && !Connected && type == "JOIN")
            {
                m_Peer = new IPEndPoint(from.Address, m_PeerPort);
                m_PeerId = sender;
                Status = "A player joined";
            }
            if (!Connected || sender != m_PeerId) return; // a third headset, or a stray packet
            SendRaw(Format("ACK", 0, seq.ToString()), m_Peer);
            if (!m_Seen.Add(seq)) return; // a resend we already handled
            delivered.Add(new Message(type, payload));
        }

        public void Dispose()
        {
            m_Running = false;
            try { m_Socket?.Close(); } catch (Exception) { }
            m_Socket = null;
        }
    }
}
