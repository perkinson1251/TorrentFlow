using System.Collections;
using System.Collections.Generic;
using System.Text;
using DefensiveProgrammingFramework;
using TorrentFlow.TorrentClientLibrary.Exceptions;
using TorrentFlow.TorrentClientLibrary.Extensions;

namespace TorrentFlow.TorrentClientLibrary.BEncoding
{
    public class BEncodedList : BEncodedValue, IList<BEncodedValue>, IEnumerable
    {
        private List<BEncodedValue> list;
        public BEncodedList()
            : this(new List<BEncodedValue>())
        {
        }
        public BEncodedList(int capacity)
            : this(new List<BEncodedValue>(capacity))
        {
        }
        public BEncodedList(IEnumerable<BEncodedValue> list)
        {
            list.CannotContainOnlyNull();

            this.list = new List<BEncodedValue>(list);
        }
        public BEncodedList(List<BEncodedValue> value)
        {
            value.CannotBeNull();

            this.list = value;
        }
        public int Count
        {
            get
            {
                return this.list.Count;
            }
        }
        public bool IsReadOnly
        {
            get
            {
                return false;
            }
        }
        public BEncodedValue this[int index]
        {
            get
            {
                index.MustBeGreaterThanOrEqualTo(0);

                return this.list[index];
            }

            set
            {
                this.list[index] = value;
            }
        }
        public void Add(BEncodedValue item)
        {
            item.CannotBeNull();

            this.list.Add(item);
        }
        public void AddRange(IEnumerable<BEncodedValue> collection)
        {
            collection.CannotContainOnlyNull();

            this.list.AddRange(collection);
        }
        public void Clear()
        {
            this.list.Clear();
        }
        public bool Contains(BEncodedValue item)
        {
            item.CannotBeNull();

            return this.list.Contains(item);
        }
        public void CopyTo(BEncodedValue[] array, int arrayIndex)
        {
            array.CannotBeNull();
            arrayIndex.MustBeGreaterThanOrEqualTo(0);

            this.list.CopyTo(array, arrayIndex);
        }
        public override int Encode(byte[] buffer, int offset)
        {
            buffer.CannotBeNullOrEmpty();
            offset.MustBeGreaterThanOrEqualTo(0);

            int written = 0;

            buffer[offset] = (byte)'l'; // lists start with l

            written++;

            for (int i = 0; i < this.list.Count; i++)
            {
                written += this.list[i].Encode(buffer, offset + written);
            }

            buffer[offset + written] = (byte)'e'; // lists end with e

            written++;

            return written;
        }
        public override bool Equals(object obj)
        {
            BEncodedList other = obj as BEncodedList;

            if (other == null)
            {
                return false;
            }
            else
            {
                for (int i = 0; i < this.list.Count; i++)
                {
                    if (!this.list[i].Equals(other.list[i]))
                    {
                        return false;
                    }
                }

                return true;
            }
        }
        public IEnumerator<BEncodedValue> GetEnumerator()
        {
            return this.list.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
        public override int GetHashCode()
        {
            int result = 0;

            for (int i = 0; i < this.list.Count; i++)
            {
                result ^= this.list[i].GetHashCode();
            }

            return result;
        }
        public int IndexOf(BEncodedValue item)
        {
            item.CannotBeNull();

            return this.list.IndexOf(item);
        }
        public void Insert(int index, BEncodedValue item)
        {
            index.MustBeGreaterThanOrEqualTo(0);
            item.CannotBeNull();

            this.list.Insert(index, item);
        }
        public override int LengthInBytes()
        {
            int length = 0;

            length += 1;   // Lists start with 'l'

            for (int i = 0; i < this.list.Count; i++)
            {
                length += this.list[i].LengthInBytes();
            }

            length += 1;   // Lists end with 'e'

            return length;
        }
        public bool Remove(BEncodedValue item)
        {
            item.CannotBeNull();

            return this.list.Remove(item);
        }
        public void RemoveAt(int index)
        {
            this.list.RemoveAt(index);
        }
        public override string ToString()
        {
            return Encoding.UTF8.GetString(this.Encode());
        }
        internal override void DecodeInternal(RawReader reader)
        {
            reader.CannotBeNull();

            if (reader.ReadByte() != 'l')
            {
                throw new BEncodingException("Invalid data found. Aborting");
            }

            while (reader.PeekByte() != -1 &&
                   reader.PeekByte() != 'e')
            {
                this.list.Add(BEncodedValue.Decode(reader));
            }

            if (reader.ReadByte() != 'e')
            {
                throw new BEncodingException("Invalid data found. Aborting");
            }
        }
    }
}
