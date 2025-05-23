using System;
using System.IO;
using DefensiveProgrammingFramework;
using TorrentFlow.TorrentClientLibrary.Extensions;

namespace TorrentFlow.TorrentClientLibrary.BEncoding
{
    public class RawReader : Stream
    {
        private bool hasPeek;
        private Stream input;
        private byte[] peeked;
        private bool strictDecoding;
        public RawReader(Stream input)
            : this(input, true)
        {
        }
        public RawReader(Stream input, bool strictDecoding)
        {
            input.CannotBeNull();

            this.input = input;
            this.peeked = new byte[1];
            this.strictDecoding = strictDecoding;
        }
        public override bool CanRead
        {
            get
            {
                return this.input.CanRead;
            }
        }
        public override bool CanSeek
        {
            get
            {
                return this.input.CanSeek;
            }
        }
        public override bool CanWrite
        {
            get
            {
                return false;
            }
        }
        public override long Length
        {
            get
            {
                return this.input.Length;
            }
        }
        public override long Position
        {
            get
            {
                if (this.hasPeek)
                {
                    return this.input.Position - 1;
                }

                return this.input.Position;
            }

            set
            {
                if (value != this.Position)
                {
                    this.hasPeek = false;
                    this.input.Position = value;
                }
            }
        }
        public bool StrictDecoding
        {
            get
            {
                return this.strictDecoding;
            }
        }
        public override void Flush()
        {
            throw new NotSupportedException();
        }
        public int PeekByte()
        {
            if (!this.hasPeek)
            {
                this.hasPeek = this.Read(this.peeked, 0, 1) == 1;
            }

            return this.hasPeek ? this.peeked[0] : -1;
        }
        public override int Read(byte[] buffer, int offset, int count)
        {
            buffer.CannotBeNull();
            offset.MustBeGreaterThanOrEqualTo(0);
            count.MustBeGreaterThanOrEqualTo(0);

            int read = 0;

            if (this.hasPeek &&
                count > 0)
            {
                this.hasPeek = false;
                buffer[offset] = this.peeked[0];
                offset++;
                count--;
                read++;
            }

            read += this.input.Read(buffer, offset, count);

            return read;
        }
        public override int ReadByte()
        {
            if (this.hasPeek)
            {
                this.hasPeek = false;

                return this.peeked[0];
            }
            else
            {
                return base.ReadByte();
            }
        }
        public override long Seek(long offset, SeekOrigin origin)
        {
            offset.MustBeGreaterThanOrEqualTo(0);

            long val;

            if (this.hasPeek &&
                origin == SeekOrigin.Current)
            {
                val = this.input.Seek(offset - 1, origin);
            }
            else
            {
                val = this.input.Seek(offset, origin);
            }

            this.hasPeek = false;

            return val;
        }
        public override void SetLength(long value)
        {
            value.MustBeGreaterThanOrEqualTo(0);

            throw new NotSupportedException();
        }
        public override void Write(byte[] buffer, int offset, int count)
        {
            buffer.CannotBeNullOrEmpty();
            offset.MustBeGreaterThanOrEqualTo(0);
            count.MustBeLessThanOrEqualTo(0);

            throw new NotSupportedException();
        }
    }
}
