using OsuTools;
using UnityEngine;

public sealed class OsuAnalyzeExample : MonoBehaviour
{
    [SerializeField]
    TextAsset osuFile;

    void Start()
    {
        if (osuFile == null)
        {
            Debug.LogError("osuFile is null.");
            return;
        }

        var result = OsuAnalyzer.AnalyzeFromText(osuFile.text, mergeThresholdMs: 500);

        Debug.Log("=== BPM Sections ===");
        for (int i = 0; i < result.BpmSections.Count; i++)
        {
            var sec = result.BpmSections[i];
            var endStr = sec.EndMs.HasValue ? OsuAnalyzer.MsToTimeString(sec.EndMs.Value) : "end";
            Debug.Log(
                string.Format(
                    "Section {0}: {1} ~ {2}  BPM={3:F2}",
                    i + 1,
                    OsuAnalyzer.MsToTimeString(sec.StartMs),
                    endStr,
                    sec.Bpm
                )
            );
        }

        Debug.Log("=== Kiai Intervals ===");
        for (int i = 0; i < result.KiaiIntervals.Count; i++)
        {
            var iv = result.KiaiIntervals[i];
            var end = iv.EndMs.HasValue ? iv.EndMs.Value : -1;
            Debug.Log(
                string.Format(
                    "Kiai {0}: {1} ~ {2}",
                    i + 1,
                    OsuAnalyzer.MsToTimeString(iv.StartMs),
                    end >= 0 ? OsuAnalyzer.MsToTimeString(end) : "end"
                )
            );
        }

        Debug.Log(
            string.Format(
                "LastHitObjectTime: {0}",
                result.LastHitObjectTimeMs.HasValue
                    ? OsuAnalyzer.MsToTimeString(result.LastHitObjectTimeMs.Value)
                    : "none"
            )
        );
    }
}
