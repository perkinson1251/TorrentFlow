using System.Collections.Generic;
using System.Linq;

namespace TorrentFlow
{
    public class TorrentFileMainInfo // Представляє словник "info"
    {
        public long PieceLength { get; set; }
        public byte[] Pieces { get; set; } = System.Array.Empty<byte>(); // Хеші частин, з'єднані разом
        public bool Private { get; set; }
        public string? Name { get; set; } // Для одного файлу або назва директорії для багатьох

        // Для одного файлу
        public long? Length { get; set; }

        // Для декількох файлів
        public List<TorrentFileInfo>? Files { get; set; }

        public byte[] RawInfoBytes { get; set; } = System.Array.Empty<byte>(); // Сирі байти словника info для info_hash
    }

    public class TorrentFileInfo // Представляє файл у списку "files"
    {
        public long Length { get; set; }
        public List<string> Path { get; set; } = new List<string>(); // Шлях як список компонентів
        public string FullPath => string.Join(System.IO.Path.DirectorySeparatorChar.ToString(), Path);
    }
    
    public class TorrentFileContent
    {
        public string? Announce { get; set; } // Головний трекер
        public List<List<string>>? AnnounceList { get; set; } // Список трекерів (альтернативний формат)
        public long? CreationDate { get; set; }
        public string? Comment { get; set; }
        public string? CreatedBy { get; set; }
        public string? Encoding { get; set; }
        public TorrentFileMainInfo Info { get; set; } = new TorrentFileMainInfo();

        // Додаткові властивості, які ми можемо захотіти витягти
        public string? Name => Info.Name; // Зручний доступ до імені
        public long TotalSize // Обчислення загального розміру
        {
            get
            {
                if (Info.Length.HasValue) // Один файл
                    return Info.Length.Value;
                if (Info.Files != null) // Декілька файлів
                    return Info.Files.Sum(f => f.Length);
                return 0;
            }
        }
        public byte[]? InfoHash { get; set; } // Буде обчислено пізніше
    }
}