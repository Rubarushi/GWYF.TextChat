using System;
using System.Text;

namespace GWYF.TextChat
{
    // Versioned marker keeps other mods' lobby messages out of this chat.
    internal static class ChatProtocol
    {
        internal const int MaxCharacters = 256;
        internal const int MaxPacketBytes = 2048;
        private static readonly byte[] Marker = Encoding.ASCII.GetBytes("GWYF.TextChat/1\n");
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        internal static string Clean(string value, int limit = MaxCharacters)
        {
            var text = new StringBuilder();
            if (value == null) return "";
            foreach (char c in value)
            {
                if (text.Length >= limit) break;
                if (!char.IsControl(c) && c != '\u2028' && c != '\u2029' &&
                    !(c >= '\u202a' && c <= '\u202e') && !(c >= '\u2066' && c <= '\u2069')) text.Append(c);
            }
            if (text.Length > 0 && char.IsHighSurrogate(text[text.Length - 1])) text.Length--;
            return text.ToString().Trim();
        }

        internal static byte[] Encode(string text)
        {
            byte[] body = Utf8.GetBytes(Clean(text));
            byte[] packet = new byte[Marker.Length + body.Length];
            Buffer.BlockCopy(Marker, 0, packet, 0, Marker.Length);
            Buffer.BlockCopy(body, 0, packet, Marker.Length, body.Length);
            return packet;
        }

        internal static bool TryDecode(byte[] packet, int length, out string text)
        {
            text = "";
            if (packet == null || length <= Marker.Length || length > MaxPacketBytes || length > packet.Length) return false;
            for (int i = 0; i < Marker.Length; i++) if (packet[i] != Marker[i]) return false;
            try { text = Clean(Utf8.GetString(packet, Marker.Length, length - Marker.Length)); }
            catch (DecoderFallbackException) { return false; }
            return text.Length > 0;
        }
    }
}
