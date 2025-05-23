using System;
using System.Net;
using System.Text;
using DefensiveProgrammingFramework;
using TorrentFlow.TorrentClientLibrary.Exceptions;
using TorrentFlow.TorrentClientLibrary.Extensions;

namespace TorrentFlow.TorrentClientLibrary.PeerWireProtocol.Messages
{
    public abstract class Message
    {
        public const int ByteLength = 1;
        public const int IntLength = 4;
        public const int LongLength = 8;
        public const int ShortLength = 2;
        public abstract int Length
        {
            get;
        }
        public static void Copy(byte[] source, int sourceOffset, byte[] destination, ref int destinationOffset, int count)
        {
            destination.CannotBeNullOrEmpty();
            destinationOffset.MustBeGreaterThanOrEqualTo(0);
            destinationOffset.MustBeLessThan(destination.Length);
            source.CannotBeNullOrEmpty();
            sourceOffset.MustBeGreaterThanOrEqualTo(0);
            sourceOffset.MustBeLessThan(source.Length);
            count.MustBeLessThanOrEqualTo(destination.Length - destinationOffset);
            count.MustBeLessThanOrEqualTo(source.Length - sourceOffset);

            Buffer.BlockCopy(source, sourceOffset, destination, destinationOffset, count);

            destinationOffset += count;
        }
        public static byte[] FromPeerId(string peerId)
        {
            peerId.MustBe(x => x.Contains("-", StringComparison.InvariantCulture));

            int delimiterIndex;

            delimiterIndex = peerId.LastIndexOf('-');

            peerId = Encoding.ASCII.GetBytes(peerId.Substring(0, delimiterIndex + 1)).ToHexaDecimalString() + // client id
                     peerId.Substring(delimiterIndex + 1); // random number

            peerId.Length.MustBeEqualTo(40);

            return peerId.ToByteArray();
        }
        public static byte ReadByte(byte[] buffer, ref int offset)
        {
            buffer.CannotBeNullOrEmpty();
            offset.MustBeGreaterThanOrEqualTo(0);
            offset.MustBeLessThanOrEqualTo(buffer.Length - ByteLength);

            byte value;

            value = Buffer.GetByte(buffer, offset);

            offset++;

            return value;
        }
        public static byte[] ReadBytes(byte[] buffer, ref int offset, int count)
        {
            buffer.CannotBeNullOrEmpty();
            offset.MustBeGreaterThanOrEqualTo(0);
            offset.MustBeLessThanOrEqualTo(buffer.Length - count);
            count.MustBeGreaterThanOrEqualTo(0);
            count.MustBeLessThanOrEqualTo(buffer.Length - offset);

            byte[] result = new byte[count];

            Buffer.BlockCopy(buffer, offset, result, 0, count);

            offset += count;

            return result;
        }
        public static IPEndPoint ReadEndpoint(byte[] buffer, ref int offset)
        {
            buffer.CannotBeNullOrEmpty();
            offset.MustBeGreaterThanOrEqualTo(0);
            offset.MustBeLessThanOrEqualTo(buffer.Length - IntLength - ShortLength);

            byte[] ipaddress = new byte[4];
            int port;

            ipaddress = ReadBytes(buffer, ref offset, 4);
            port = (ushort)ReadShort(buffer, ref offset);

            return new IPEndPoint(new IPAddress(ipaddress), port);
        }
        public static int ReadInt(byte[] buffer, ref int offset)
        {
            buffer.CannotBeNullOrEmpty();
            offset.MustBeGreaterThanOrEqualTo(0);
            offset.MustBeLessThanOrEqualTo(buffer.Length - IntLength);

            int value;

            value = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buffer, offset));

            offset += IntLength;

            return value;
        }
        public static long ReadLong(byte[] buffer, ref int offset)
        {
            buffer.CannotBeNullOrEmpty();
            offset.MustBeGreaterThanOrEqualTo(0);
            offset.MustBeLessThanOrEqualTo(buffer.Length - LongLength);

            long result;

            result = IPAddress.NetworkToHostOrder(BitConverter.ToInt64(buffer, offset));

            offset += LongLength;

            return result;
        }
        public static short ReadShort(byte[] buffer, ref int offset)
        {
            buffer.CannotBeNullOrEmpty();
            offset.MustBeGreaterThanOrEqualTo(0);
            offset.MustBeLessThan(buffer.Length);

            short value;

            value = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(buffer, offset));

            offset += ShortLength;

            return value;
        }
        public static string ReadString(byte[] buffer, ref int offset, int count)
        {
            buffer.CannotBeNullOrEmpty();
            offset.MustBeGreaterThanOrEqualTo(0);
            offset.MustBeLessThan(buffer.Length);
            count.MustBeGreaterThanOrEqualTo(0);
            count.MustBeLessThanOrEqualTo(buffer.Length - offset);

            string value;

            value = Encoding.ASCII.GetString(buffer, offset, count);

            offset += count;

            return value;
        }
        public static string ToPeerId(byte[] value)
        {
            value.CannotBeNullOrEmpty();
            value.Length.MustBeEqualTo(20);

            int delimiterIndex = -1;
            int offset = 0;
            string peerId = null;

            for (int i = 0; i < value.Length; i++)
            {
                if ((char)value[i] == '-')
                {
                    delimiterIndex = i;
                }
            }

            if (delimiterIndex > 0)
            {
                peerId = ReadString(value, ref offset, delimiterIndex + 1) + // client id
                         ReadBytes(value, ref offset, value.Length - delimiterIndex - 1).ToHexaDecimalString(); // random number
            }
            else
            {
                peerId = ReadBytes(value, ref offset, value.Length).ToHexaDecimalString(); // could not interpret peer id -> read as binary string
            }

            return peerId;
        }
        public static void Write(byte[] buffer, ref int offset, byte value)
        {
            buffer.CannotBeNullOrEmpty();
            offset.MustBeGreaterThanOrEqualTo(0);
            offset.MustBeLessThan(buffer.Length);

            Buffer.SetByte(buffer, offset, value);

            offset += ByteLength;
        }
        public static void Write(byte[] buffer, ref int offset, ushort value)
        {
            buffer.CannotBeNullOrEmpty();
            offset.MustBeGreaterThanOrEqualTo(0);
            offset.MustBeLessThanOrEqualTo(buffer.Length - ShortLength);

            Write(buffer, ref offset, (short)value);
        }
        public static void Write(byte[] buffer, ref int offset, short value)
        {
            buffer.CannotBeNullOrEmpty();
            offset.MustBeGreaterThanOrEqualTo(0);
            offset.MustBeLessThanOrEqualTo(buffer.Length - ShortLength);

            Copy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(value)), 0, buffer, ref offset, ShortLength);
        }
        public static void Write(byte[] buffer, ref int offset, IPAddress value)
        {
            buffer.CannotBeNullOrEmpty();
            offset.MustBeGreaterThanOrEqualTo(0);
            offset.MustBeLessThan(buffer.Length);
            value.CannotBeNull();

            Write(buffer, ref offset, value.GetAddressBytes());
        }
        public static void Write(byte[] buffer, ref int offset, IPEndPoint value)
        {
            buffer.CannotBeNullOrEmpty();
            offset.MustBeGreaterThanOrEqualTo(0);
            offset.MustBeLessThan(buffer.Length);
            value.CannotBeNull();

            Write(buffer, ref offset, value.Address);
            Write(buffer, ref offset, (ushort)value.Port);
        }
        public static void Write(byte[] buffer, ref int offset, int value)
        {
            buffer.CannotBeNullOrEmpty();
            offset.MustBeGreaterThanOrEqualTo(0);
            offset.MustBeLessThanOrEqualTo(buffer.Length - IntLength);

            Copy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(value)), 0, buffer, ref offset, IntLength);
        }
        public static void Write(byte[] buffer, ref int offset, uint value)
        {
            buffer.CannotBeNullOrEmpty();
            offset.MustBeGreaterThanOrEqualTo(0);
            offset.MustBeLessThanOrEqualTo(buffer.Length - IntLength);

            Write(buffer, ref offset, (int)value);
        }
        public static void Write(byte[] buffer, ref int offset, long value)
        {
            buffer.CannotBeNullOrEmpty();
            offset.MustBeGreaterThanOrEqualTo(0);
            offset.MustBeLessThanOrEqualTo(buffer.Length - LongLength);

            Copy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(value)), 0, buffer, ref offset, LongLength);
        }
        public static void Write(byte[] buffer, ref int offset, ulong value)
        {
            buffer.CannotBeNullOrEmpty();
            offset.MustBeGreaterThanOrEqualTo(0);
            offset.MustBeLessThanOrEqualTo(buffer.Length - LongLength);

            Write(buffer, ref offset, (long)value);
        }
        public static void Write(byte[] buffer, ref int offset, byte[] value)
        {
            value.CannotBeNull();
            buffer.CannotBeNullOrEmpty();
            offset.MustBeGreaterThanOrEqualTo(0);
            offset.MustBeLessThanOrEqualTo(buffer.Length - value.Length);

            Copy(value, 0, buffer, ref offset, value.Length);
        }
        public static void Write(byte[] buffer, ref int offset, string value)
        {
            buffer.CannotBeNullOrEmpty();
            offset.MustBeGreaterThanOrEqualTo(0);
            offset.MustBeLessThan(buffer.Length);

            byte[] data;

            data = Encoding.ASCII.GetBytes(value);

            Copy(data, 0, buffer, ref offset, data.Length);
        }
        public byte[] Encode()
        {
            byte[] buffer = new byte[this.Length];

            this.Encode(buffer, 0);

            return buffer;
        }
        public abstract int Encode(byte[] buffer, int offset);
        protected int CheckWritten(int written)
        {
            if (written != this.Length)
            {
                throw new MessageException("Message encoded incorrectly. Incorrect number of bytes written");
            }
            else
            {
                return written;
            }
        }
    }
}
