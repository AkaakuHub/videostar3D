using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace OsuTools
{
    public static class OsuAnalyzer
    {
        public sealed class TimingPoint
        {
            public int TimeMs;
            public double BeatLength;
            public int Meter;
            public int SampleSet;
            public int SampleIndex;
            public int Volume;
            public int Uninherited;
            public int Effects;
        }

        public sealed class BpmSection
        {
            public int StartMs;
            public int? EndMs;
            public double Bpm;
        }

        public sealed class Interval
        {
            public int StartMs;
            public int? EndMs;
        }

        public sealed class AnalysisResult
        {
            public List<BpmSection> BpmSections = new List<BpmSection>();
            public List<Interval> KiaiIntervals = new List<Interval>();
            public int? LastHitObjectTimeMs;
        }

        public static AnalysisResult AnalyzeFromText(string osuText, int mergeThresholdMs = 500)
        {
            if (osuText == null)
            {
                throw new ArgumentNullException(nameof(osuText));
            }

            var tps = ParseTimingPoints(osuText);
            var bpmSections = ExtractBpmSections(tps);
            var kiaiIntervals = ExtractKiaiIntervals(tps);
            kiaiIntervals = MergeKiaiIntervals(kiaiIntervals, mergeThresholdMs);

            var lastTime = GetLastHitObjectTime(osuText);

            for (int i = 0; i < kiaiIntervals.Count; i++)
            {
                if (kiaiIntervals[i].EndMs == null && lastTime != null)
                {
                    kiaiIntervals[i].EndMs = lastTime.Value;
                }
            }

            return new AnalysisResult
            {
                BpmSections = bpmSections,
                KiaiIntervals = kiaiIntervals,
                LastHitObjectTimeMs = lastTime,
            };
        }

        public static AnalysisResult AnalyzeFromFile(string filePath, int mergeThresholdMs = 500)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException("filePath is null or empty.", nameof(filePath));
            }

            var text = File.ReadAllText(filePath);
            return AnalyzeFromText(text, mergeThresholdMs);
        }

        public static string MsToTimeString(int ms)
        {
            var minutes = ms / 60000;
            var seconds = (ms % 60000) / 1000;
            var millis = ms % 1000;
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}:{1:00}.{2:000}",
                minutes,
                seconds,
                millis
            );
        }

        static List<string> ExtractSectionLines(string text, string section)
        {
            var pattern = @"^\[" + Regex.Escape(section) + @"\](.*?)(?=^\[|\Z)";
            var match = Regex.Match(
                text,
                pattern,
                RegexOptions.Multiline | RegexOptions.Singleline
            );
            if (!match.Success)
            {
                return new List<string>();
            }

            var content = match.Groups[1].Value;
            var rawLines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            var lines = new List<string>();
            foreach (var raw in rawLines)
            {
                var line = raw.Trim();
                if (line.Length == 0)
                {
                    continue;
                }
                if (line.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }
                lines.Add(line);
            }

            return lines;
        }

        static List<TimingPoint> ParseTimingPoints(string osuText)
        {
            var lines = ExtractSectionLines(osuText, "TimingPoints");
            var tps = new List<TimingPoint>();

            foreach (var line in lines)
            {
                var fields = line.Split(',');
                if (fields.Length < 8)
                {
                    continue;
                }

                if (!TryParseInt(fields[0], out var timeMs))
                {
                    continue;
                }

                if (!TryParseDouble(fields[1], out var beatLength))
                {
                    continue;
                }

                TryParseInt(fields[2], out var meter);
                TryParseInt(fields[3], out var sampleSet);
                TryParseInt(fields[4], out var sampleIndex);
                TryParseInt(fields[5], out var volume);
                TryParseInt(fields[6], out var uninherited);
                TryParseInt(fields[7], out var effects);

                tps.Add(
                    new TimingPoint
                    {
                        TimeMs = timeMs,
                        BeatLength = beatLength,
                        Meter = meter,
                        SampleSet = sampleSet,
                        SampleIndex = sampleIndex,
                        Volume = volume,
                        Uninherited = uninherited,
                        Effects = effects,
                    }
                );
            }

            tps.Sort((a, b) => a.TimeMs.CompareTo(b.TimeMs));
            return tps;
        }

        static List<BpmSection> ExtractBpmSections(List<TimingPoint> tps)
        {
            var reds = new List<TimingPoint>();
            foreach (var tp in tps)
            {
                if (tp.Uninherited == 1)
                {
                    reds.Add(tp);
                }
            }

            var segs = new List<BpmSection>();
            for (int i = 0; i < reds.Count; i++)
            {
                var tp = reds[i];
                if (tp.BeatLength == 0.0)
                {
                    continue;
                }

                var bpm = 60000.0 / tp.BeatLength;
                var start = tp.TimeMs;
                int? end = null;
                if (i + 1 < reds.Count)
                {
                    end = reds[i + 1].TimeMs;
                }

                segs.Add(
                    new BpmSection
                    {
                        StartMs = start,
                        EndMs = end,
                        Bpm = bpm,
                    }
                );
            }

            return segs;
        }

        static List<Interval> ExtractKiaiIntervals(List<TimingPoint> tps)
        {
            var intervals = new List<Interval>();
            bool? prev = null;
            int? start = null;

            foreach (var tp in tps)
            {
                var kiai = ((tp.Effects & 1) == 1);
                if (prev == null)
                {
                    prev = kiai;
                    if (kiai)
                    {
                        start = tp.TimeMs;
                    }
                    continue;
                }

                if (kiai != prev.Value)
                {
                    if (kiai)
                    {
                        start = tp.TimeMs;
                    }
                    else
                    {
                        if (start != null)
                        {
                            intervals.Add(
                                new Interval { StartMs = start.Value, EndMs = tp.TimeMs }
                            );
                        }
                        start = null;
                    }
                    prev = kiai;
                }
            }

            if (prev == true && start != null)
            {
                intervals.Add(new Interval { StartMs = start.Value, EndMs = null });
            }

            return intervals;
        }

        static List<Interval> MergeKiaiIntervals(List<Interval> intervals, int thresholdMs)
        {
            if (intervals == null || intervals.Count == 0)
            {
                return new List<Interval>();
            }

            var merged = new List<Interval>
            {
                new Interval { StartMs = intervals[0].StartMs, EndMs = intervals[0].EndMs },
            };

            for (int i = 1; i < intervals.Count; i++)
            {
                var current = intervals[i];
                var last = merged[merged.Count - 1];

                if (last.EndMs == null)
                {
                    continue;
                }

                var gap = current.StartMs - last.EndMs.Value;
                if (gap <= thresholdMs)
                {
                    last.EndMs = current.EndMs;
                }
                else
                {
                    merged.Add(new Interval { StartMs = current.StartMs, EndMs = current.EndMs });
                }
            }

            return merged;
        }

        static int? GetLastHitObjectTime(string osuText)
        {
            var lines = ExtractSectionLines(osuText, "HitObjects");
            if (lines.Count == 0)
            {
                return null;
            }

            var last = 0;
            foreach (var line in lines)
            {
                var fields = line.Split(',');
                if (fields.Length < 3)
                {
                    continue;
                }

                if (TryParseInt(fields[2], out var timeMs))
                {
                    if (timeMs > last)
                    {
                        last = timeMs;
                    }
                }
            }

            return last;
        }

        static bool TryParseInt(string s, out int value)
        {
            value = 0;
            if (s == null)
            {
                return false;
            }

            s = s.Trim();
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            {
                value = (int)d;
                return true;
            }

            return false;
        }

        static bool TryParseDouble(string s, out double value)
        {
            value = 0.0;
            if (s == null)
            {
                return false;
            }

            s = s.Trim();
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }
}
