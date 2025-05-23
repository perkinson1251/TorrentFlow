using System;
using System.Globalization;
using System.Text;
using DefensiveProgrammingFramework;
using TorrentFlow.TorrentClientLibrary.Exceptions;
using TorrentFlow.TorrentClientLibrary.Extensions;

namespace TorrentFlow.TorrentClientLibrary.BEncoding
{
    public class BEncodedString : BEncodedValue, IComparable<BEncodedString>
    {
        private byte[] textBytes;
        public BEncodedString()
            : this(Array.Empty<byte>())
        {
        }
        public BEncodedString(char[] value)
            : this(Encoding.ASCII.GetBytes(value))
        {
        }
        public BEncodedString(string value)
            : this(Encoding.ASCII.GetBytes(value))
        {
        }
        public BEncodedString(byte[] value)
        {
            value.CannotBeNull();

            this.textBytes = value;
        }
        public string Hex
        {
            get
            {
                return BitConverter.ToString(this.TextBytes);
            }
        }
        public string Text
        {
            get
            {
                return Encoding.ASCII.GetString(this.textBytes);
            }

            set
            {
                this.textBytes = Encoding.ASCII.GetBytes(value);
            }
        }
        public byte[] TextBytes
        {
            get
            {
                return this.textBytes;
            }
        }
        public static bool ByteMatch(byte[] array1, byte[] array2)
        {
            array1.CannotBeNull();
            array2.CannotBeNull();

            if (array1.Length != array2.Length)
            {
                return false;
            }
            else
            {
                return ByteMatch(array1, 0, array2, 0, array1.Length);
            }
        }
        public static bool ByteMatch(byte[] array1, int offset1, byte[] array2, int offset2, int count)
        {
            array1.CannotBeNull();
            array2.CannotBeNull();

            // If either of the arrays is too small, they're not equal
            if ((array1.Length - offset1) < count ||
                (array2.Length - offset2) < count)
            {
                return false;
            }

            // Check if any elements are unequal
            for (int i = 0; i < count; i++)
            {
                if (array1[offset1 + i] != array2[offset2 + i])
                {
                    return false;
                }
            }

            return true;
        }
        public static BEncodedString ToBEncodedString(string value)
        {
            value.CannotBeNullOrEmpty();

            return new BEncodedString(value);
        }
        public static BEncodedString ToBEncodedString(char[] value)
        {
            value.CannotBeNullOrEmpty();

            return new BEncodedString(value);
        }
        public static BEncodedString ToBEncodedString(byte[] value)
        {
            value.CannotBeNullOrEmpty();

            return new BEncodedString(value);
        }
        public int CompareTo(object other)
        {
            return this.CompareTo(other as BEncodedString);
        }
        public int CompareTo(BEncodedString other)
        {
            int difference;
            int length;

            if (other == null)
            {
                return 1;
            }
            else
            {
                difference = 0;
                length = this.textBytes.Length > other.textBytes.Length ? other.textBytes.Length : this.textBytes.Length;

                for (int i = 0; i < length; i++)
                {
                    if ((difference = this.textBytes[i].CompareTo(other.textBytes[i])) != 0)
                    {
                        return difference;
                    }
                }

                if (this.textBytes.Length == other.textBytes.Length)
                {
                    return 0;
                }
                else
                {
                    return this.textBytes.Length > other.textBytes.Length ? 1 : -1;
                }
            }
        }
        public override int Encode(byte[] buffer, int offset)
        {
            buffer.CannotBeNullOrEmpty();
            offset.MustBeGreaterThanOrEqualTo(0);

            int written;

            written = offset;
            written += this.WriteAscii(buffer, written, this.textBytes.Length.ToString(CultureInfo.InvariantCulture));
            written += this.WriteAscii(buffer, written, ":");
            written += this.Write(buffer, written, this.textBytes);

            return written - offset;
        }
        public override bool Equals(object obj)
        {
            BEncodedString other;

            if (obj == null)
            {
                return false;
            }
            else
            {
                if (obj is string)
                {
                    other = new BEncodedString((string)obj);
                }
                else if (obj is BEncodedString)
                {
                    other = (BEncodedString)obj;
                }
                else
                {
                    return false;
                }

                return ByteMatch(this.textBytes, other.textBytes);
            }
        }
        public override int GetHashCode()
        {
            int hash = 0;

            for (int i = 0; i < this.textBytes.Length; i++)
            {
                hash += this.textBytes[i];
            }

            return hash;
        }
        public override int LengthInBytes()
        {
            // The length is equal to the length-prefix + ':' + length of data
            int prefix = 1; // Account for ':'

            // Count the number of characters needed for the length prefix
            for (int i = this.textBytes.Length; i != 0; i = i / 10)
            {
                prefix += 1;
            }

            if (this.textBytes.Length == 0)
            {
                prefix++;
            }

            return prefix + this.textBytes.Length;
        }
        public override string ToString()
        {
            return Encoding.ASCII.GetString(this.textBytes);
        }
        public int Write(byte[] destination, int destinationOffset, byte[] source, int sourceOffset, int count)
        {
            destination.CannotBeNullOrEmpty();
            destinationOffset.MustBeGreaterThanOrEqualTo(0);
            source.CannotBeNull();
            sourceOffset.MustBeGreaterThanOrEqualTo(0);
            count.MustBeGreaterThanOrEqualTo(0);

            Buffer.BlockCopy(source, sourceOffset, destination, destinationOffset, count);

            return count;
        }
        public int Write(byte[] buffer, int offset, byte[] value)
        {
            buffer.CannotBeNullOrEmpty();
            offset.MustBeGreaterThanOrEqualTo(0);
            value.CannotBeNull();

            return this.Write(buffer, offset, value, 0, value.Length);
        }
        public int Write(byte[] buffer, int offset, byte value)
        {
            buffer.CannotBeNullOrEmpty();
            offset.MustBeGreaterThanOrEqualTo(0);

            buffer[offset] = value;

            return 1;
        }
        public int WriteAscii(byte[] buffer, int offset, string text)
        {
            buffer.CannotBeNullOrEmpty();
            offset.MustBeGreaterThanOrEqualTo(0);
            text.CannotBeNullOrEmpty();

            for (int i = 0; i < text.Length; i++)
            {
                this.Write(buffer, offset + i, (byte)text[i]);
            }

            return text.Length;
        }
        internal override void DecodeInternal(RawReader reader)
        {
            reader.CannotBeNull();

            int letterCount;
            string length = string.Empty;

            // read in how many characters the string is long
            while (reader.PeekByte() != -1 &&
                   reader.PeekByte() != ':')
            {
                length += (char)reader.ReadByte();
            }

            if (reader.ReadByte() != ':')
            {
                throw new BEncodingException("Invalid data found. Aborting");
            }

            if (!int.TryParse(length, out letterCount))
            {
                throw new BEncodingException("Invalid BEncodedString. Could not read the string length.");
            }

            this.textBytes = new byte[letterCount];

            if (reader.Read(this.textBytes, 0, letterCount) != letterCount)
            {
                throw new BEncodingException("Invalid BEncodedString. The string does not match the specified length.");
            }
        }
    }
}
