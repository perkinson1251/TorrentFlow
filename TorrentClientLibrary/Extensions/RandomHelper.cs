using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DefensiveProgrammingFramework;

namespace TorrentFlow.TorrentClientLibrary.Extensions
{
    public static class RandomHelper
    {
        private static Random random = new Random(DateTime.UtcNow.Millisecond);
        public static Random Random
        {
            get
            {
                return RandomHelper.random;
            }
        }
        public static string RandomString(int size, StringCasing stringCasing, params char[] characters)
        {
            if (characters == null ||
                characters.Length == 0)
            {
                return string.Empty;
            }
            else
            {
                size.MustBeGreaterThan(0);
                characters.CannotBeNullOrEmpty();

                StringBuilder builder = new StringBuilder();

                for (int i = 0; i < size; i++)
                {
                    builder.Append(characters[random.Next(0, characters.Length - 1)]);
                }

                return builder.ToString().ToString(stringCasing);
            }
        }
        public static string RandomString(int size, string characters)
        {
            if (string.IsNullOrEmpty(characters))
            {
                return string.Empty;
            }
            else
            {
                return RandomString(size, StringCasing.None, characters.ToCharArray());
            }
        }
    }
}
