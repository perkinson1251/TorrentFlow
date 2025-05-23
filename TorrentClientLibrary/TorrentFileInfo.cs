using DefensiveProgrammingFramework;
using TorrentFlow.TorrentClientLibrary.Extensions;

namespace TorrentFlow.TorrentClientLibrary
{
    public sealed class TorrentFileInfo
    {
        public TorrentFileInfo(string filePath, string md5hash, long length)
        {
            filePath.MustBeValidFilePath();
            md5hash.IsNotNull().Then(() => md5hash.Length.MustBeEqualTo(32));
            length.MustBeGreaterThan(0);

            this.FilePath = filePath;
            this.Md5Hash = md5hash;
            this.Length = length;
            this.Download = true;
        }
        private TorrentFileInfo()
        {
        }
        public bool Download
        {
            get;
            set;
        }
        public string FilePath
        {
            get;
            private set;
        }
        public long Length
        {
            get;
            private set;
        }
        public string Md5Hash
        {
            get;
            private set;
        }
    }
}
