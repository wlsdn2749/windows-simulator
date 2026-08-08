using System;

namespace MikaNetwork
{
    public static class MikaPacketBuilder
    {
        public const int HeaderSize = sizeof(ushort) + sizeof(ushort);
        public const ushort MaxPacketSize = ushort.MaxValue;

        static MikaPacketBuilder()
        {

        }

        public static ushort ReadId(ReadOnlySpan<byte> packet) => BitConverter.ToUInt16(packet);
        public static ushort ReadSize(ReadOnlySpan<byte> packet) => BitConverter.ToUInt16(packet.Slice(sizeof(ushort)));
        public static ReadOnlyMemory<byte> ReadBody(ReadOnlyMemory<byte> packet) => packet.Slice(HeaderSize);
        public static ReadOnlyMemory<byte> ReadPacket(ReadOnlySpan<byte> packet, int size)
            => packet.Slice(0, size).ToArray().AsMemory();

        public static byte[] MakePacket(ushort packetId, byte[] body)
        {
            byte[] packet = new byte[HeaderSize + body.Length];
            ushort size = (ushort)(HeaderSize + body.Length);

            body.CopyTo(packet, HeaderSize); // Header만큼 이후에 복사
            BitConverter.TryWriteBytes(packet.AsSpan(0, sizeof(ushort)), packetId);
            BitConverter.TryWriteBytes(packet.AsSpan(sizeof(ushort), sizeof(ushort)), size);

            return packet;
        }
    }
}
