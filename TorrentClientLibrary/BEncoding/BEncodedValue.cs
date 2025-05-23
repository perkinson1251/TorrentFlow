using System.IO;
using DefensiveProgrammingFramework;
using TorrentFlow.TorrentClientLibrary.Exceptions;
using TorrentFlow.TorrentClientLibrary.Extensions;

namespace TorrentFlow.TorrentClientLibrary.BEncoding
{
    public abstract class BEncodedValue
    {
        public static T Clone<T>(T value) where T : BEncodedValue
        {
            value.CannotBeNull();

            return (T)BEncodedValue.Decode(value.Encode());
        }
        public static BEncodedValue Decode(byte[] data)
        {
            data.CannotBeNull();

            BEncodedValue value = null;

            using (MemoryStream ms = new MemoryStream(data))
            {
                using (RawReader stream = new RawReader(ms))
                {
                    value = Decode(stream);
                }
            }

            return value;
        }
        public static BEncodedValue Decode(byte[] buffer, int offset, int length)
        {
            buffer.CannotBeNull();
            offset.MustBeGreaterThanOrEqualTo(0);
            length.MustBeGreaterThanOrEqualTo(0);

            return Decode(buffer, offset, length, true);
        }
        public static BEncodedValue Decode(byte[] buffer, int offset, int length, bool strictDecoding)
        {
            buffer.CannotBeNull();
            offset.MustBeGreaterThanOrEqualTo(0);
            offset.MustBeLessThanOrEqualTo(buffer.Length - length);
            length.MustBeGreaterThanOrEqualTo(0);

            BEncodedValue value = null;

            using (RawReader reader = new RawReader(new MemoryStream(buffer, offset, length), strictDecoding))
            {
                value = BEncodedValue.Decode(reader);
            }

            return value;
        }
        public static BEncodedValue Decode(Stream stream)
        {
            stream.CannotBeNull();

            return Decode(new RawReader(stream));
        }
        public static BEncodedValue Decode(RawReader reader)
        {
            reader.CannotBeNull();

            BEncodedValue data;
            int peekByte = reader.PeekByte();

            if (peekByte == 'i')
            {
                // integer
                data = new BEncodedNumber();
                data.DecodeInternal(reader);

                return data;
            }
            else if (peekByte == 'd')
            {
                // dictionary
                data = new BEncodedDictionary();
                data.DecodeInternal(reader);

                return data;
            }
            else if (peekByte == 'l')
            {
                // list
                data = new BEncodedList();
                data.DecodeInternal(reader);

                return data;
            }
            else if (peekByte == '0' ||
                     peekByte == '1' ||
                     peekByte == '2' ||
                     peekByte == '3' ||
                     peekByte == '4' ||
                     peekByte == '5' ||
                     peekByte == '6' ||
                     peekByte == '7' ||
                     peekByte == '8' ||
                     peekByte == '9')
            {
                // string
                data = new BEncodedString();
                data.DecodeInternal(reader);

                return data;
            }
            else
            {
                throw new BEncodingException("Could not find what value to decode.");
            }
        }
        public static T Decode<T>(byte[] data) where T : BEncodedValue
        {
            data.CannotBeNull();

            return (T)BEncodedValue.Decode(data);
        }
        public static T Decode<T>(byte[] buffer, int offset, int length) where T : BEncodedValue
        {
            buffer.CannotBeNull();
            offset.MustBeGreaterThanOrEqualTo(0);
            length.MustBeGreaterThanOrEqualTo(0);

            return BEncodedValue.Decode<T>(buffer, offset, length, true);
        }
        public static T Decode<T>(byte[] buffer, int offset, int length, bool strictDecoding) where T : BEncodedValue
        {
            buffer.CannotBeNull();
            offset.MustBeGreaterThanOrEqualTo(0);
            length.MustBeGreaterThanOrEqualTo(0);

            return (T)BEncodedValue.Decode(buffer, offset, length, strictDecoding);
        }
        public static T Decode<T>(Stream stream) where T : BEncodedValue
        {
            stream.CannotBeNull();

            return (T)BEncodedValue.Decode(stream);
        }
        public static T Decode<T>(RawReader reader) where T : BEncodedValue
        {
            reader.CannotBeNull();

            return (T)BEncodedValue.Decode(reader);
        }
        public byte[] Encode()
        {
            byte[] buffer = new byte[this.LengthInBytes()];

            if (this.Encode(buffer, 0) != buffer.Length)
            {
                throw new BEncodingException("Error encoding the data");
            }

            return buffer;
        }
        public abstract int Encode(byte[] buffer, int offset);
        public abstract int LengthInBytes();
        internal static BEncodedValue Decode(byte[] buffer, bool strictDecoding)
        {
            buffer.CannotBeNull();

            return Decode(buffer, 0, buffer.Length, strictDecoding);
        }
        internal abstract void DecodeInternal(RawReader reader);
    }
}
