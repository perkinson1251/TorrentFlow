using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using DefensiveProgrammingFramework;

namespace TorrentFlow.TorrentClientLibrary.Extensions
{
    public static class CryptoExtensions
    {
        public static byte[] CalculateSha1Hash(this byte[] data, int offset, int length)
        {
            return CalculateHashSha(data, offset, length, 128);
        }
        public static byte[] CalculateSha1Hash(this byte[] data)
        {
            return CalculateHashSha(data, 0, data.Length, 128);
        }
        private static byte[] CalculateHash(this byte[] data, int offset, int count, HashAlgorithm hashAlgorithm)
        {
            byte[] hashRaw = hashAlgorithm.ComputeHash(data, offset, count);
            hashAlgorithm.Clear();

            return hashRaw;
        }
        private static byte[] CalculateHashSha(this byte[] data, int offset, int length, int hashSize)
        {
            data.CannotBeNullOrEmpty();
            offset.MustBeGreaterThanOrEqualTo(0);
            length.MustBeGreaterThanOrEqualTo(0);
            hashSize.MustBeOneOf(128, 256, 384, 512);

            byte[] hashRaw = null;

            if (data != null)
            {
                if (hashSize == 128)
                {
                    using HashAlgorithm sha = SHA1.Create();

                    hashRaw = CalculateHash(data, offset, length, sha);
                }
                else if (hashSize == 256)
                {
                    using HashAlgorithm sha = SHA256.Create();

                    hashRaw = CalculateHash(data, offset, length, sha);
                }
                else if (hashSize == 384)
                {
                    using HashAlgorithm sha = SHA384.Create();

                    hashRaw = CalculateHash(data, offset, length, sha);
                }
                else if (hashSize == 512)
                {
                    using HashAlgorithm sha = SHA512.Create();

                    hashRaw = CalculateHash(data, offset, length, sha);
                }
            }

            return hashRaw;
        }
    }
}
