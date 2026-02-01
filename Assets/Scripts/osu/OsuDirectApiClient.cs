using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace OsuTools.Api
{
    /// <summary>
    /// Client for communicating with osu.direct API.
    /// Handles beatmap search and download operations.
    /// </summary>
    public static class OsuDirectApiClient
    {
        private const string SearchBaseUrl = "https://osu.direct/api/v2/search";
        private const string DownloadBaseUrl = "https://osu.direct/api/d";

        /// <summary>
        /// Search for beatmaps using the osu.direct API.
        /// </summary>
        /// <param name="request">Search request parameters</param>
        /// <param name="onSuccess">Callback with search results</param>
        /// <param name="onError">Callback with error message</param>
        public static IEnumerator Search(
            SearchRequest request,
            Action<OsuSearchResult[]> onSuccess,
            Action<string> onError = null
        )
        {
            var query = UnityWebRequest.EscapeURL(request.query ?? "");
            var url = $"{SearchBaseUrl}?query={query}&amount={request.amount}&offset={request.offset}";

            using (var webRequest = UnityWebRequest.Get(url))
            {
                webRequest.SetRequestHeader("Accept", "application/json");

                yield return webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var json = webRequest.downloadHandler.text;
                        // Preprocess JSON: replace @2x with _2x for C# compatibility
                        var processedJson = json.Replace("@2x", "_2x");
                        // osu.direct returns a JSON array directly, so wrap it for JsonUtility
                        var wrappedJson = $"{{\"results\":{processedJson}}}";
                        var results = JsonUtility.FromJson<OsuSearchResultWrapper>(wrappedJson);
                        onSuccess?.Invoke(results != null && results.results != null ? results.results : new OsuSearchResult[0]);
                    }
                    catch (Exception ex)
                    {
                        onError?.Invoke($"Failed to parse search results: {ex.Message}");
                    }
                }
                else
                {
                    onError?.Invoke($"Search failed: {webRequest.error} (HTTP {webRequest.responseCode})");
                }
            }
        }

        /// <summary>
        /// Download a beatmap set (.osz file) by its ID.
        /// </summary>
        /// <param name="setId">Beatmap set ID</param>
        /// <param name="onSuccess">Callback with downloaded .osz data</param>
        /// <param name="onProgress">Callback with download progress (0.0 to 1.0)</param>
        /// <param name="onError">Callback with error message</param>
        public static IEnumerator DownloadBeatmapSet(
            int setId,
            Action<byte[]> onSuccess,
            Action<float> onProgress = null,
            Action<string> onError = null
        )
        {
            var url = $"{DownloadBaseUrl}/{setId}";
            Debug.Log($"[OsuDirectApiClient] Starting download: {url}");

            using (var webRequest = UnityWebRequest.Get(url))
            {
                // Set timeout to 60 seconds
                webRequest.timeout = 60;

                // Follow redirects (302) to get the actual file
                webRequest.redirectLimit = 10;

                // Send request and report progress
                var operation = webRequest.SendWebRequest();

                float lastProgress = -1f;
                while (!operation.isDone)
                {
                    float currentProgress = webRequest.downloadProgress;
                    // Log progress every 10%
                    if (currentProgress - lastProgress >= 0.1f || currentProgress >= 1.0f)
                    {
                        Debug.Log($"[OsuDirectApiClient] Download progress: {currentProgress * 100:F1}%");
                        lastProgress = currentProgress;
                    }
                    onProgress?.Invoke(currentProgress);
                    yield return null;
                }

                // Final progress update
                onProgress?.Invoke(1.0f);

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    var data = webRequest.downloadHandler.data;
                    onSuccess?.Invoke(data);
                    Debug.Log($"[OsuDirectApiClient] Successfully downloaded beatmap set {setId}, size: {data?.Length ?? 0} bytes");
                }
                else
                {
                    var errorMsg = $"Download failed: {webRequest.error} (HTTP {webRequest.responseCode})";
                    Debug.LogError($"[OsuDirectApiClient] {errorMsg}");
                    onError?.Invoke(errorMsg);
                }
            }
        }

        /// <summary>
        /// Download and save a beatmap set to a local file.
        /// </summary>
        /// <param name="setId">Beatmap set ID</param>
        /// <param name="outputPath">Local file path to save the .osz file</param>
        /// <param name="onSuccess">Callback when download completes</param>
        /// <param name="onProgress">Callback with download progress (0.0 to 1.0)</param>
        /// <param name="onError">Callback with error message</param>
        public static IEnumerator DownloadBeatmapSetToFile(
            int setId,
            string outputPath,
            Action<string> onSuccess,
            Action<float> onProgress = null,
            Action<string> onError = null
        )
        {
            var url = $"{DownloadBaseUrl}/{setId}";

            using (var webRequest = UnityWebRequest.Get(url))
            {
                webRequest.redirectLimit = 10;
                var downloadHandler = new DownloadHandlerFile(outputPath);
                downloadHandler.removeFileOnAbort = true;
                webRequest.downloadHandler = downloadHandler;

                yield return webRequest.SendWebRequest();

                while (!webRequest.isDone)
                {
                    onProgress?.Invoke(webRequest.downloadProgress);
                    yield return null;
                }

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    onSuccess?.Invoke(outputPath);
                }
                else
                {
                    onError?.Invoke($"Download failed: {webRequest.error} (HTTP {webRequest.responseCode})");
                }
            }
        }

        /// <summary>
        /// Wrapper class for JSON array parsing since JsonUtility requires a wrapper object.
        /// </summary>
        [System.Serializable]
        private class OsuSearchResultWrapper
        {
            public OsuSearchResult[] results;
        }
    }
}
