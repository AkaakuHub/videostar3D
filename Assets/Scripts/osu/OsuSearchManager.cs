using System;
using System.Collections;
using OsuTools.Api;
using UnityEngine;
using Path = System.IO.Path;

namespace OsuTools
{
    /// <summary>
    /// Main controller for osu! beatmap search, download, and analysis workflow.
    /// </summary>
    public class OsuSearchManager : MonoBehaviour
    {
        [Header("Events")]
        public Action<OsuSearchResult[]> OnSearchComplete;
        public Action<string> OnSearchError;
        public Action<float> OnDownloadProgress;
        public Action<OsuAnalyzer.AnalysisResult> OnAnalysisComplete;
        public Action<string> OnAnalysisError;

        /// <summary>
        /// Search for beatmaps with the given query.
        /// </summary>
        public void SearchBeatmaps(string query, int amount = 20, int offset = 0)
        {
            StartCoroutine(SearchCoroutine(query, amount, offset));
        }

        private IEnumerator SearchCoroutine(string query, int amount, int offset)
        {
            var request = new SearchRequest(query, amount, offset);
            yield return OsuDirectApiClient.Search(
                request,
                results => OnSearchComplete?.Invoke(results),
                error => OnSearchError?.Invoke(error)
            );
        }

        /// <summary>
        /// Download a beatmap set and analyze it.
        /// </summary>
        /// <param name="setId">Beatmap set ID</param>
        /// <param name="difficultyVersion">Specific difficulty to analyze (null = first found)</param>
        public void DownloadAndAnalyze(int setId, string difficultyVersion = null)
        {
            StartCoroutine(DownloadAndAnalyzeCoroutine(setId, difficultyVersion));
        }

        private IEnumerator DownloadAndAnalyzeCoroutine(int setId, string difficultyVersion)
        {
            Debug.Log($"[OsuSearchManager] DownloadAndAnalyzeCoroutine started for set ID: {setId}");

            // Download the .osz file
            byte[] oszData = null;
            bool downloadSuccess = false;
            string downloadError = null;

            yield return OsuDirectApiClient.DownloadBeatmapSet(
                setId,
                data =>
                {
                    oszData = data;
                    downloadSuccess = true;
                    Debug.Log($"[OsuSearchManager] Download complete! Size: {data.Length} bytes");
                },
                progress =>
                {
                    Debug.Log($"[OsuSearchManager] Download progress: {progress * 100:F1}%");
                    OnDownloadProgress?.Invoke(progress);
                },
                error =>
                {
                    downloadError = error;
                    Debug.LogError($"[OsuSearchManager] Download error: {error}");
                }
            );

            if (!downloadSuccess)
            {
                Debug.LogError($"[OsuSearchManager] Download failed: {downloadError}");
                OnAnalysisError?.Invoke(downloadError);
                yield break;
            }

            Debug.Log($"[OsuSearchManager] Extracting .osu files from archive...");

            // Determine output directory (always use persistentDataPath for mobile compatibility)
            string outputDir = OsuFileExtractor.BeatmapsDirectory;
            Debug.Log($"[OsuSearchManager] Output directory: {outputDir}");

            // Extract .osu file
            string osuFilePath = null;
            if (string.IsNullOrEmpty(difficultyVersion))
            {
                // Extract all files and use the first one
                var extractedFiles = OsuFileExtractor.ExtractAllOsuFiles(oszData, outputDir);
                Debug.Log($"[OsuSearchManager] Extracted {extractedFiles.Length} .osu files");
                if (extractedFiles.Length > 0)
                {
                    osuFilePath = extractedFiles[0];
                    Debug.Log($"[OsuSearchManager] Using first file: {osuFilePath}");
                }
            }
            else
            {
                // Extract specific difficulty
                osuFilePath = OsuFileExtractor.ExtractOsuFileByDifficulty(
                    oszData,
                    difficultyVersion,
                    outputDir
                );
                Debug.Log($"[OsuSearchManager] Extracted specific difficulty: {osuFilePath}");
            }

            if (string.IsNullOrEmpty(osuFilePath))
            {
                Debug.LogError("[OsuSearchManager] Failed to extract .osu file!");
                OnAnalysisError?.Invoke("Failed to extract .osu file from .osz archive.");
                yield break;
            }

            Debug.Log($"[OsuSearchManager] Analyzing {osuFilePath}...");

            // Analyze the .osu file
            var result = OsuAnalyzer.AnalyzeFromFile(osuFilePath);
            Debug.Log($"[OsuSearchManager] Analysis complete! BPM sections: {result.BpmSections.Count}, Kiai intervals: {result.KiaiIntervals.Count}");
            OnAnalysisComplete?.Invoke(result);
        }

        /// <summary>
        /// Download and analyze directly from content without writing to disk.
        /// </summary>
        public IEnumerator DownloadAndAnalyzeInMemory(int setId, string difficultyVersion = null)
        {
            byte[] oszData = null;
            bool downloadSuccess = false;
            string downloadError = null;

            yield return OsuDirectApiClient.DownloadBeatmapSet(
                setId,
                data =>
                {
                    oszData = data;
                    downloadSuccess = true;
                },
                progress => OnDownloadProgress?.Invoke(progress),
                error => downloadError = error
            );

            if (!downloadSuccess)
            {
                OnAnalysisError?.Invoke(downloadError);
                yield break;
            }

            // Extract content to memory
            string osuContent;
            if (string.IsNullOrEmpty(difficultyVersion))
            {
                // Get first .osu file content
                var fileList = OsuFileExtractor.ListOsuFiles(oszData);
                if (fileList.Length == 0)
                {
                    OnAnalysisError?.Invoke("No .osu files found in archive.");
                    yield break;
                }
                // Extract first file to temp, read it, then analyze
                var tempDir = Path.Combine(Application.temporaryCachePath, "temp_osu");
                var extractedFiles = OsuFileExtractor.ExtractAllOsuFiles(oszData, tempDir);
                if (extractedFiles.Length > 0)
                {
                    osuContent = System.IO.File.ReadAllText(extractedFiles[0]);
                    System.IO.Directory.Delete(tempDir, recursive: true);
                }
                else
                {
                    OnAnalysisError?.Invoke("Failed to extract .osu files.");
                    yield break;
                }
            }
            else
            {
                osuContent = OsuFileExtractor.ExtractOsuFileContent(oszData, difficultyVersion);
                if (string.IsNullOrEmpty(osuContent))
                {
                    OnAnalysisError?.Invoke($"Difficulty '{difficultyVersion}' not found.");
                    yield break;
                }
            }

            // Analyze the content
            var result = OsuAnalyzer.AnalyzeFromText(osuContent);
            OnAnalysisComplete?.Invoke(result);
        }
    }
}
