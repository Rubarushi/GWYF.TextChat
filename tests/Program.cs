using System;
using System.Text;
using GWYF.TextChat;

int checks = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks++; }
foreach (string input in new[] { "안녕하세요", "Hello 한글 😀", "<size=99>literal markup</size>", new string('한', 256) })
{
    byte[] packet = ChatProtocol.Encode(input);
    Check(ChatProtocol.TryDecode(packet, packet.Length, out string result) && result == input, "Unicode round trip");
}
Check(ChatProtocol.Clean("a\n\r\t\u202eb") == "ab", "Control and bidi filtering");
Check(ChatProtocol.Clean(new string('x', 255) + "😀").Length == 255, "No split surrogate at length boundary");
byte[] otherMod = Encoding.UTF8.GetBytes("AnotherMod/1\nhello");
Check(!ChatProtocol.TryDecode(otherMod, otherMod.Length, out _), "Ignore other mod messages");
byte[] valid = ChatProtocol.Encode("hello");
Check(!ChatProtocol.TryDecode(valid, valid.Length + 1, out _), "Reject invalid byte count");
Check(!ChatProtocol.TryDecode(new byte[4096], 4096, out _), "Reject oversized packet");
byte[] malformed = ChatProtocol.Encode("x"); malformed[malformed.Length - 1] = 0xff;
Check(!ChatProtocol.TryDecode(malformed, malformed.Length, out _), "Reject malformed UTF8");
byte[] empty = ChatProtocol.Encode(" \n");
Check(!ChatProtocol.TryDecode(empty, empty.Length, out _), "Reject empty message");
Console.WriteLine($"PASS: {checks} protocol checks");
