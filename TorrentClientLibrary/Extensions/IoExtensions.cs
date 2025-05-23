using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web;
using DefensiveProgrammingFramework;

namespace TorrentFlow.TorrentClientLibrary.Extensions
{
    public static class IoExtensions
    {
        public static void DeleteDirectoryRecursively(this string directoryPath, bool deleteOnlyDirectoryContents = false)
        {
            directoryPath.MustBeValidDirectoryPath();

            if (Directory.Exists(directoryPath))
            {
                // delete files
                foreach (string file in Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                    File.Delete(file);
                }

                // delete subdirectories
                foreach (string subDirectory in Directory.GetDirectories(directoryPath))
                {
                    DeleteDirectoryRecursively(subDirectory);
                }

                if (!deleteOnlyDirectoryContents)
                {
                    // delete root directory
                    Directory.Delete(directoryPath, true);
                }
            }
        }
    }
}
