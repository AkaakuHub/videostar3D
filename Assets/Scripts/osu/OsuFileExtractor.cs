using System;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace OsuTools
{
    /// <summary>
    /// Extracts .osu files from .osz beatmap archives.
    /// Works on all platforms including iOS and Android.
    /// </summary>
    public static class OsuFileExtractor
    {
        /// <summary>
        /// Extract all .osu files from an .osz archive to the specified directory.
        /// </summary>
        /// <param name="oszData">The .osz file data (byte array)</param>
        /// <param name="outputDir">Directory to extract files to (must be writable)</param>
        /// <returns>Array of extracted .osu file paths</returns>
        public static string[] ExtractAllOsuFiles(byte[] oszData, string outputDir)
        {
            // Ensure output directory exists
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // Create a temporary file for the .osz data
            var tempOszPath = Path.Combine(outputDir, "temp.osz");
            File.WriteAllBytes(tempOszPath, oszData);

            // Extract using ZipArchive (works on all platforms)
            var extractedFiles = new System.Collections.Generic.List<string>();

            using (var archive = ZipFile.OpenRead(tempOszPath))
            {
                foreach (var entry in archive.Entries)
                {
                    if (entry.FullName.EndsWith(".osu", StringComparison.OrdinalIgnoreCase))
                    {
                        var destinationPath = Path.Combine(outputDir, Path.GetFileName(entry.FullName));
                        entry.ExtractToFile(destinationPath, overwrite: true);
                        extractedFiles.Add(destinationPath);
                    }
                }
            }

            // Clean up temporary .osz file
            File.Delete(tempOszPath);

            return extractedFiles.ToArray();
        }

        /// <summary>
        /// Extract a specific .osu file from an .osz archive by difficulty name.
        /// Returns the first .osu file whose version matches the specified difficulty.
        /// </summary>
        /// <param name="oszData">The .osz file data (byte array)</param>
        /// <param name="difficultyVersion">The difficulty version name to search for</param>
        /// <param name="outputDir">Directory to extract files to</param>
        /// <returns>Path to the extracted .osu file, or null if not found</returns>
        public static string ExtractOsuFileByDifficulty(byte[] oszData, string difficultyVersion, string outputDir)
        {
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            var tempOszPath = Path.Combine(outputDir, "temp.osz");
            File.WriteAllBytes(tempOszPath, oszData);

            using (var archive = ZipFile.OpenRead(tempOszPath))
            {
                foreach (var entry in archive.Entries)
                {
                    if (entry.FullName.EndsWith(".osu", StringComparison.OrdinalIgnoreCase))
                    {
                        // Check if this file contains the difficulty version
                        using (var stream = entry.Open())
                        using (var reader = new StreamReader(stream))
                        {
                            var content = reader.ReadToEnd();
                            if (content.Contains($"Version:{difficultyVersion}"))
                            {
                                var destinationPath = Path.Combine(outputDir, Path.GetFileName(entry.FullName));
                                entry.ExtractToFile(destinationPath, overwrite: true);
                                File.Delete(tempOszPath);
                                return destinationPath;
                            }
                        }
                    }
                }
            }

            File.Delete(tempOszPath);
            return null;
        }

        /// <summary>
        /// Get all .osu file entries from an .osz archive without extracting them.
        /// Useful for listing available difficulties before downloading.
        /// </summary>
        /// <param name="oszData">The .osz file data (byte array)</param>
        /// <returns>Array of .osu entry names</returns>
        public static string[] ListOsuFiles(byte[] oszData)
        {
            var tempPath = Path.Combine(Application.temporaryCachePath, "temp_list.osz");
            File.WriteAllBytes(tempPath, oszData);

            var osuFiles = new System.Collections.Generic.List<string>();

            using (var archive = ZipFile.OpenRead(tempPath))
            {
                foreach (var entry in archive.Entries)
                {
                    if (entry.FullName.EndsWith(".osu", StringComparison.OrdinalIgnoreCase))
                    {
                        osuFiles.Add(entry.FullName);
                    }
                }
            }

            File.Delete(tempPath);
            return osuFiles.ToArray();
        }

        /// <summary>
        /// Extract the .osu file content as text without writing to disk.
        /// </summary>
        /// <param name="oszData">The .osz file data (byte array)</param>
        /// <param name="difficultyVersion">The difficulty version to extract</param>
        /// <returns>The .osu file content as text, or null if not found</returns>
        public static string ExtractOsuFileContent(byte[] oszData, string difficultyVersion)
        {
            var tempPath = Path.Combine(Application.temporaryCachePath, "temp_content.osz");
            File.WriteAllBytes(tempPath, oszData);

            using (var archive = ZipFile.OpenRead(tempPath))
            {
                foreach (var entry in archive.Entries)
                {
                    if (entry.FullName.EndsWith(".osu", StringComparison.OrdinalIgnoreCase))
                    {
                        using (var stream = entry.Open())
                        using (var reader = new StreamReader(stream))
                        {
                            var content = reader.ReadToEnd();
                            if (content.Contains($"Version:{difficultyVersion}"))
                            {
                                File.Delete(tempPath);
                                return content;
                            }
                        }
                    }
                }
            }

            File.Delete(tempPath);
            return null;
        }

        /// <summary>
        /// Get the writable persistent data path for the current platform.
        /// Use this location for storing extracted beatmap files.
        /// </summary>
        public static string PersistentDataPath => Application.persistentDataPath;

        /// <summary>
        /// Get a dedicated beatmaps directory in persistent data.
        /// Creates the directory if it doesn't exist.
        /// </summary>
        public static string BeatmapsDirectory
        {
            get
            {
                var path = Path.Combine(Application.persistentDataPath, "Beatmaps");
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                return path;
            }
        }
    }
}
