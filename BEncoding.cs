using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace TorrentFlow
{
    public static class BEncoding
    {
        private static readonly byte DictionaryStart = (byte)'d';
        private static readonly byte ListStart = (byte)'l';
        private static readonly byte NumberStart = (byte)'i';
        private static readonly byte EndMarker = (byte)'e';
        private static readonly byte ByteArrayDivider = (byte)':';

        public static object Decode(byte[] bytes)
        {
            using var ms = new MemoryStream(bytes);
            using var reader = new BinaryReader(ms, Encoding.UTF8, false); // Важливо вказати Encoding.UTF8
            return DecodeNextObject(reader);
        }
        
        public static async Task<object> DecodeFileAsync(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("Файл не знайдено: " + path, path);

            byte[] bytes = await File.ReadAllBytesAsync(path);
            return Decode(bytes);
        }


        private static object DecodeNextObject(BinaryReader reader)
        {
            byte currentByte = reader.ReadByte();
            reader.BaseStream.Position--; // Повертаємося на один байт, щоб наступні методи могли прочитати маркер

            if (currentByte == DictionaryStart)
                return DecodeDictionary(reader);
            if (currentByte == ListStart)
                return DecodeList(reader);
            if (currentByte == NumberStart)
                return DecodeNumber(reader);
            // Інакше це має бути рядок (byte array)
            return DecodeByteArray(reader);
        }

        private static Dictionary<string, object> DecodeDictionary(BinaryReader reader)
        {
            reader.ReadByte(); // Прочитати 'd'
            var dict = new Dictionary<string, object>();
            var keys = new List<byte[]>(); // Для перевірки сортування

            while (reader.PeekChar() != EndMarker)
            {
                byte[] keyBytes = (byte[])DecodeByteArray(reader); // Ключі завжди є byte[] (рядки)
                string key = Encoding.UTF8.GetString(keyBytes); // Конвертуємо в string для Dictionary
                
                keys.Add(keyBytes);
                object value = DecodeNextObject(reader);
                dict.Add(key, value);
            }
            reader.ReadByte(); // Прочитати 'e'

            // Перевірка сортування ключів (важливо для info_hash)
            // Порівнюємо байти ключів, а не рядки
            for (int i = 0; i < keys.Count - 1; i++)
            {
                if (CompareByteArrays(keys[i], keys[i + 1]) > 0)
                {
                     System.Diagnostics.Debug.WriteLine("Увага: Ключі в словнику можуть бути не відсортовані лексикографічно (по байтах). Це може вплинути на info_hash.");
                }
            }
            return dict;
        }
        
        // Допоміжний метод для порівняння масивів байтів
        private static int CompareByteArrays(byte[] a, byte[] b)
        {
            int minLength = Math.Min(a.Length, b.Length);
            for (int i = 0; i < minLength; i++)
            {
                if (a[i] < b[i]) return -1;
                if (a[i] > b[i]) return 1;
            }
            return a.Length.CompareTo(b.Length);
        }


        private static List<object> DecodeList(BinaryReader reader)
        {
            reader.ReadByte(); // Прочитати 'l'
            var list = new List<object>();
            while (reader.PeekChar() != EndMarker)
            {
                list.Add(DecodeNextObject(reader));
            }
            reader.ReadByte(); // Прочитати 'e'
            return list;
        }

        private static long DecodeNumber(BinaryReader reader)
        {
            reader.ReadByte(); // Прочитати 'i'
            var numStr = new StringBuilder();
            char c;
            while ((c = (char)reader.ReadByte()) != 'e')
            {
                numStr.Append(c);
            }
            return long.Parse(numStr.ToString());
        }

        private static byte[] DecodeByteArray(BinaryReader reader)
        {
            var lenStr = new StringBuilder();
            char c;
            while ((c = (char)reader.ReadByte()) != ':')
            {
                lenStr.Append(c);
            }
            int length = int.Parse(lenStr.ToString());
            return reader.ReadBytes(length);
        }

        // Методи Encode поки що можна пропустити, вони знадобляться для створення торентів
        // public static byte[] Encode(object obj) { ... }
    }
}