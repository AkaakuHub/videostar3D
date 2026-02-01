using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OsuTools.UI
{
    /// <summary>
    /// Displays osu! beatmap metadata analysis results on a Canvas.
    /// Shows BPM sections and Kiai time intervals as visual timelines.
    /// </summary>
    public class OsuMetadataDisplay : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField]
        private RawImage bpmTimelineImage;

        [SerializeField]
        private RawImage kiaiTimelineImage;

        [SerializeField]
        private TextMeshProUGUI songInfoText;

        [SerializeField]
        private TextMeshProUGUI statsText;

        [SerializeField]
        private TextMeshProUGUI bpmSectionsText;

        [SerializeField]
        private TextMeshProUGUI kiaiIntervalsText;

        [Header("Timeline Settings")]
        [SerializeField]
        [Tooltip("Width of the timeline texture in pixels")]
        private int timelineWidth = 1000;

        [SerializeField]
        [Tooltip("Height of the timeline texture in pixels")]
        private int timelineHeight = 60;

        [Header("Colors")]
        [SerializeField]
        private Color bpmSectionColor = new Color(0.2f, 0.6f, 1f);

        [SerializeField]
        private Color kiaiColor = new Color(1f, 0.4f, 0.6f);

        [SerializeField]
        private Color backgroundColor = new Color(0.1f, 0.1f, 0.15f);

        [SerializeField]
        private Color separatorColor = new Color(0.3f, 0.3f, 0.35f);

        [Header("Manager")]
        [SerializeField]
        private OsuSearchManager searchManager;

        private OsuAnalyzer.AnalysisResult currentResult;
        private string currentSongTitle;

        private void Awake()
        {
            if (searchManager != null)
            {
                searchManager.OnAnalysisComplete += HandleAnalysisComplete;
                searchManager.OnAnalysisError += HandleAnalysisError;
            }

            // Initialize with empty textures
            ClearTimelines();
        }

        private void OnDestroy()
        {
            if (searchManager != null)
            {
                searchManager.OnAnalysisComplete -= HandleAnalysisComplete;
                searchManager.OnAnalysisError -= HandleAnalysisError;
            }
        }

        /// <summary>
        /// Display analysis results for a specific song.
        /// </summary>
        public void Display(string songTitle, OsuAnalyzer.AnalysisResult result)
        {
            currentSongTitle = songTitle;
            currentResult = result;
            UpdateDisplay();
        }

        private void HandleAnalysisComplete(OsuAnalyzer.AnalysisResult result)
        {
            currentResult = result;
            UpdateDisplay();
        }

        private void HandleAnalysisError(string error)
        {
            Debug.LogError($"Analysis error: {error}");
            if (statsText != null)
            {
                statsText.text = $"Analysis Error: {error}";
            }
        }

        private void UpdateDisplay()
        {
            if (currentResult == null)
            {
                return;
            }

            UpdateSongInfo();
            UpdateStats();
            UpdateBpmSectionsText();
            UpdateKiaiIntervalsText();
            DrawBpmTimeline();
            DrawKiaiTimeline();
        }

        private void UpdateSongInfo()
        {
            if (songInfoText != null)
            {
                songInfoText.text = !string.IsNullOrEmpty(currentSongTitle)
                    ? currentSongTitle
                    : "Beatmap Analysis";
            }
        }

        private void UpdateStats()
        {
            if (statsText == null || currentResult == null)
            {
                return;
            }

            var length = currentResult.LastHitObjectTimeMs.HasValue
                ? OsuAnalyzer.MsToTimeString(currentResult.LastHitObjectTimeMs.Value)
                : "Unknown";

            statsText.text =
                $"Length: {length} | BPM Sections: {currentResult.BpmSections.Count} | Kiai Intervals: {currentResult.KiaiIntervals.Count}";
        }

        private void UpdateBpmSectionsText()
        {
            if (bpmSectionsText == null || currentResult == null)
            {
                return;
            }

            var lines = new List<string> { "BPM Sections:" };
            for (int i = 0; i < currentResult.BpmSections.Count; i++)
            {
                var sec = currentResult.BpmSections[i];
                var endStr = sec.EndMs.HasValue
                    ? OsuAnalyzer.MsToTimeString(sec.EndMs.Value)
                    : "end";
                lines.Add(
                    $"{i + 1}. {OsuAnalyzer.MsToTimeString(sec.StartMs)} - {endStr} | {sec.Bpm:F2} BPM"
                );
            }
            bpmSectionsText.text = string.Join("\n", lines);
        }

        private void UpdateKiaiIntervalsText()
        {
            if (kiaiIntervalsText == null || currentResult == null)
            {
                return;
            }

            var lines = new List<string> { "Kiai Time Intervals:" };
            for (int i = 0; i < currentResult.KiaiIntervals.Count; i++)
            {
                var iv = currentResult.KiaiIntervals[i];
                var end = iv.EndMs.HasValue ? iv.EndMs.Value : -1;
                lines.Add(
                    $"{i + 1}. {OsuAnalyzer.MsToTimeString(iv.StartMs)} - {(end >= 0 ? OsuAnalyzer.MsToTimeString(end) : "end")}"
                );
            }
            kiaiIntervalsText.text = string.Join("\n", lines);
        }

        private void ClearTimelines()
        {
            if (bpmTimelineImage != null)
            {
                var emptyTexture = new Texture2D(1, 1);
                emptyTexture.SetPixel(0, 0, backgroundColor);
                emptyTexture.Apply();
                bpmTimelineImage.texture = emptyTexture;
            }

            if (kiaiTimelineImage != null)
            {
                var emptyTexture = new Texture2D(1, 1);
                emptyTexture.SetPixel(0, 0, backgroundColor);
                emptyTexture.Apply();
                kiaiTimelineImage.texture = emptyTexture;
            }
        }

        private void DrawBpmTimeline()
        {
            if (
                bpmTimelineImage == null
                || currentResult == null
                || currentResult.BpmSections.Count == 0
            )
            {
                return;
            }

            var texture = new Texture2D(timelineWidth, timelineHeight);
            var colors = new Color[timelineWidth * timelineHeight];

            // Fill background
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = backgroundColor;
            }

            // Calculate total length
            int totalMs = currentResult.BpmSections.Max(sec =>
                sec.EndMs ?? currentResult.LastHitObjectTimeMs ?? 0
            );
            if (totalMs <= 0)
            {
                totalMs = currentResult.LastHitObjectTimeMs ?? 0;
            }

            if (totalMs <= 0)
            {
                bpmTimelineImage.texture = texture;
                return;
            }

            // Draw BPM sections
            foreach (var section in currentResult.BpmSections)
            {
                int startX = (section.StartMs * timelineWidth) / totalMs;
                int endX = section.EndMs.HasValue
                    ? (section.EndMs.Value * timelineWidth) / totalMs
                    : timelineWidth;

                // Clamp coordinates
                startX = Mathf.Clamp(startX, 0, timelineWidth - 1);
                endX = Mathf.Clamp(endX, 0, timelineWidth);

                // Fill section
                for (int x = startX; x < endX; x++)
                {
                    for (int y = 0; y < timelineHeight; y++)
                    {
                        colors[y * timelineWidth + x] = bpmSectionColor;
                    }
                }
            }

            // Draw section separators
            foreach (var section in currentResult.BpmSections)
            {
                int x = (section.StartMs * timelineWidth) / totalMs;
                x = Mathf.Clamp(x, 0, timelineWidth - 1);

                for (int y = 0; y < timelineHeight; y++)
                {
                    colors[y * timelineWidth + x] = separatorColor;
                }
            }

            texture.SetPixels(colors);
            texture.Apply();
            bpmTimelineImage.texture = texture;
        }

        private void DrawKiaiTimeline()
        {
            if (
                kiaiTimelineImage == null
                || currentResult == null
                || currentResult.KiaiIntervals.Count == 0
            )
            {
                return;
            }

            var texture = new Texture2D(timelineWidth, timelineHeight);
            var colors = new Color[timelineWidth * timelineHeight];

            // Fill background
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = backgroundColor;
            }

            // Calculate total length
            int totalMs = currentResult.LastHitObjectTimeMs ?? 0;
            if (totalMs <= 0 && currentResult.KiaiIntervals.Count > 0)
            {
                totalMs = currentResult.KiaiIntervals.Max(iv => iv.EndMs ?? 0);
            }

            if (totalMs <= 0)
            {
                kiaiTimelineImage.texture = texture;
                return;
            }

            // Draw Kiai intervals
            foreach (var interval in currentResult.KiaiIntervals)
            {
                int startX = (interval.StartMs * timelineWidth) / totalMs;
                int endX = interval.EndMs.HasValue
                    ? (interval.EndMs.Value * timelineWidth) / totalMs
                    : timelineWidth;

                // Clamp coordinates
                startX = Mathf.Clamp(startX, 0, timelineWidth - 1);
                endX = Mathf.Clamp(endX, 0, timelineWidth);

                // Fill interval
                for (int x = startX; x < endX; x++)
                {
                    for (int y = 0; y < timelineHeight; y++)
                    {
                        colors[y * timelineWidth + x] = kiaiColor;
                    }
                }
            }

            // Draw interval boundaries
            foreach (var interval in currentResult.KiaiIntervals)
            {
                int startX = (interval.StartMs * timelineWidth) / totalMs;
                int endX = interval.EndMs.HasValue
                    ? (interval.EndMs.Value * timelineWidth) / totalMs
                    : timelineWidth;

                startX = Mathf.Clamp(startX, 0, timelineWidth - 1);
                endX = Mathf.Clamp(endX, 0, timelineWidth);

                for (int y = 0; y < timelineHeight; y++)
                {
                    if (startX < timelineWidth)
                    {
                        colors[y * timelineWidth + startX] = separatorColor;
                    }
                    if (endX > 0 && endX < timelineWidth)
                    {
                        colors[y * timelineWidth + endX - 1] = separatorColor;
                    }
                }
            }

            texture.SetPixels(colors);
            texture.Apply();
            kiaiTimelineImage.texture = texture;
        }
    }
}
