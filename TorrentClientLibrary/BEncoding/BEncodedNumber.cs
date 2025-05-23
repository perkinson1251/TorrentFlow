using System;
using System.Globalization;
using DefensiveProgrammingFramework;
using TorrentFlow.TorrentClientLibrary.Exceptions;
using TorrentFlow.TorrentClientLibrary.Extensions;

namespace TorrentFlow.TorrentClientLibrary.BEncoding
{
    public class BEncodedNumber : BEncodedValue, IComparable<BEncodedNumber>
    {
        private long number;
        public BEncodedNumber()
            : this(0)
        {
        }
        public BEncodedNumber(long value)
        {
            this.number = value;
        }
        public long Number
        {
            get
            {
                return this.number;
            }

            set
            {
                this.number = value;
            }
        }
        public static BEncodedNumber ToBEncodedNumber(long value)
        {
            return new BEncodedNumber(value);
        }
        public static BEncodedNumber ToBEncodedNumber(int value)
        {
            return new BEncodedNumber(value);
        }
        public int CompareTo(object other)
        {
            if (other is BEncodedNumber ||
                other is long ||
                other is int)
            {
                return this.CompareTo((BEncodedNumber)other);
            }
            else
            {
                return -1;
            }
        }
        public int CompareTo(BEncodedNumber other)
        {
            other.CannotBeNull();

            return this.number.CompareTo(other.number);
        }
        public int CompareTo(long other)
        {
            return this.number.CompareTo(other);
        }
        public int CompareTo(int other)
        {
            return this.number.CompareTo(other);
        }
        public override int Encode(byte[] buffer, int offset)
        {
            buffer.CannotBeNullOrEmpty();
            offset.MustBeGreaterThanOrEqualTo(0);

            long number = this.number;
            int written = offset;
            long reversed;

            buffer[written++] = (byte)'i';

            if (number < 0)
            {
                buffer[written++] = (byte)'-';
                number = -number;
            }

            // Reverse the number '12345' to get '54321'
            reversed = 0;

            for (long i = number; i != 0; i /= 10)
            {
                reversed = (reversed * 10) + (i % 10);
            }

            // Write each digit of the reversed number to the array. We write '1'
            // first, then '2', etc
            for (long i = reversed; i != 0; i /= 10)
            {
                buffer[written++] = (byte)((i % 10) + '0');
            }

            if (number == 0)
            {
                buffer[written++] = (byte)'0';
            }

            // If the original number ends in one or more zeros, they are lost
            // when we reverse the number. We add them back in here.
            for (long i = number; i % 10 == 0 && number != 0; i /= 10)
            {
                buffer[written++] = (byte)'0';
            }

            buffer[written++] = (byte)'e';

            return written - offset;
        }
        public override bool Equals(object obj)
        {
            BEncodedNumber obj2 = obj as BEncodedNumber;

            if (obj2 == null)
            {
                return false;
            }
            else
            {
                return this.number == obj2.number;
            }
        }
        public override int GetHashCode()
        {
            return this.number.GetHashCode();
        }
        public override int LengthInBytes()
        {
            long number = this.number;
            int count = 2; // account for the 'i' and 'e'

            if (number == 0)
            {
                return count + 1;
            }

            if (number < 0)
            {
                number = -number;
                count++;
            }

            for (long i = number; i != 0; i /= 10)
            {
                count++;
            }

            return count;
        }
        public override string ToString()
        {
            return this.number.ToString(CultureInfo.InvariantCulture);
        }
        internal override void DecodeInternal(RawReader reader)
        {
            reader.CannotBeNull();

            int sign = 1;
            int letter;

            if (reader.ReadByte() != 'i')
            {
                throw new BEncodingException("Invalid data found. Aborting.");
            }

            if (reader.PeekByte() == '-')
            {
                sign = -1;
                reader.ReadByte();
            }

            while ((letter = reader.PeekByte()) != -1 &&
                    letter != 'e')
            {
                if (letter < '0' ||
                    letter > '9')
                {
                    throw new BEncodingException("Invalid number found.");
                }

                this.number = (this.number * 10) + (letter - '0');

                reader.ReadByte();
            }

            if (reader.ReadByte() != 'e')
            {
                throw new BEncodingException("Invalid data found. Aborting.");
            }

            this.number *= sign;
        }
    }
}
