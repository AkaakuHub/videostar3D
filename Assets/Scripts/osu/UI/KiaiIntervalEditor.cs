using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OsuTools.UI
{
    public class KiaiIntervalEditor : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private OsuSearchManager searchManager;

        [SerializeField]
        private RawImage kiaiTimelineImage;

        [SerializeField]
        private TMP_FontAsset uiFont;

        [Header("Generated UI")]
        [SerializeField]
        private GameObject selectionPanel;

        [SerializeField]
        private TextMeshProUGUI selectionLabel;

        [SerializeField]
        private Button prevButton;

        [SerializeField]
        private Button nextButton;

        [SerializeField]
        private Button useButton;

        [SerializeField]
        private GameObject editorPanel;

        [SerializeField]
        private Button playPauseButton;

        [SerializeField]
        private TextMeshProUGUI playPauseLabel;

        [SerializeField]
        private Button jumpStartButton;

        [SerializeField]
        private Button jumpEndButton;

        [SerializeField]
        private TextMeshProUGUI currentTimeText;

        [SerializeField]
        private TextMeshProUGUI startTimeText;

        [SerializeField]
        private TextMeshProUGUI endTimeText;

        [Header("Timeline Handles")]
        [SerializeField]
        private RectTransform selectionRange;

        [SerializeField]
        private RectTransform startHandle;

        [SerializeField]
        private RectTransform endHandle;

        [SerializeField]
        private RectTransform playhead;

        [Header("Colors")]
        [SerializeField]
        private Color selectionColor = new Color(1f, 0.4f, 0.6f, 0.35f);

        [SerializeField]
        private Color handleColor = new Color(1f, 1f, 1f, 0.9f);

        [SerializeField]
        private Color playheadColor = new Color(1f, 1f, 1f, 0.9f);

        private readonly List<OsuAnalyzer.Interval> intervals = new List<OsuAnalyzer.Interval>();
        private OsuAnalyzer.AnalysisResult currentResult;
        private AudioSource audioSource;
        private int selectedIndex;
        private int startMs;
        private int endMs;
        private int totalMs;
        private bool editorActive;
        private bool uiBuilt;
        private bool hasIntervalsFromFile;
        private const float RestartThresholdSeconds = 0.15f;

        private void Awake()
        {
            if (searchManager == null)
            {
                searchManager = FindObjectOfType<OsuSearchManager>();
            }

            if (kiaiTimelineImage == null)
            {
                var timeline = transform.Find("KiaiTimeline");
                if (timeline != null)
                {
                    kiaiTimelineImage = timeline.GetComponent<RawImage>();
                }
            }

            if (uiFont == null)
            {
                uiFont = GetComponentInChildren<TextMeshProUGUI>()?.font;
            }

            EnsureAudioSource();
            EnsureUi();

            if (searchManager != null)
            {
                searchManager.OnAnalysisComplete += HandleAnalysisComplete;
                searchManager.OnAudioClipReady += HandleAudioClipReady;
            }

            SetEditorActive(false);
            SetSelectionActive(false);
            SetHandlesVisible(false);
        }

        private void OnDestroy()
        {
            if (searchManager != null)
            {
                searchManager.OnAnalysisComplete -= HandleAnalysisComplete;
                searchManager.OnAudioClipReady -= HandleAudioClipReady;
            }
        }

        private void Update()
        {
            if (!editorActive || audioSource == null || audioSource.clip == null || totalMs <= 0)
            {
                return;
            }

            var currentMs = (int)(audioSource.time * 1000f);
            if (audioSource.isPlaying && endMs > 0 && currentMs >= endMs)
            {
                audioSource.time = endMs / 1000f;
                audioSource.Pause();
                UpdatePlayPauseLabel();
                currentMs = endMs;
            }

            UpdatePlayheadFromMs(currentMs);
            UpdateCurrentTimeText(currentMs);
        }

        private void EnsureAudioSource()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }
        }

        private void EnsureUi()
        {
            if (uiBuilt)
            {
                return;
            }

            if (kiaiTimelineImage != null)
            {
                var input = kiaiTimelineImage.GetComponent<KiaiTimelineInput>();
                if (input == null)
                {
                    input = kiaiTimelineImage.gameObject.AddComponent<KiaiTimelineInput>();
                }
                input.SetEditor(this);
                kiaiTimelineImage.raycastTarget = true;
            }

            if (selectionPanel == null)
            {
                selectionPanel = CreatePanel("KiaiSelectionPanel", 60f);
                var layout = selectionPanel.AddComponent<HorizontalLayoutGroup>();
                layout.padding = new RectOffset(6, 6, 6, 6);
                layout.spacing = 8f;
                layout.childAlignment = TextAnchor.MiddleLeft;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = true;

                prevButton = CreateButton(selectionPanel.transform, "Prev", "<");
                selectionLabel = CreateLabel(selectionPanel.transform, "Kiai 1/1");
                nextButton = CreateButton(selectionPanel.transform, "Next", ">");
                useButton = CreateButton(selectionPanel.transform, "Use", "Edit");

                var labelLayout = selectionLabel.gameObject.AddComponent<LayoutElement>();
                labelLayout.flexibleWidth = 1f;
                labelLayout.minWidth = 120f;

                SetButtonWidth(prevButton, 40f);
                SetButtonWidth(nextButton, 40f);
                SetButtonWidth(useButton, 70f);

                prevButton.onClick.AddListener(SelectPrevious);
                nextButton.onClick.AddListener(SelectNext);
                useButton.onClick.AddListener(ApplySelectionAndEdit);
            }

            if (editorPanel == null)
            {
                editorPanel = CreatePanel("KiaiEditorPanel", 70f);
                playPauseButton = CreateButton(editorPanel.transform, "PlayPause", "Play");
                playPauseLabel = playPauseButton.GetComponentInChildren<TextMeshProUGUI>();
                playPauseButton.onClick.AddListener(TogglePlayPause);

                jumpStartButton = CreateButton(editorPanel.transform, "JumpStart", "Start");
                jumpStartButton.onClick.AddListener(JumpToStart);

                jumpEndButton = CreateButton(editorPanel.transform, "JumpEnd", "End");
                jumpEndButton.onClick.AddListener(JumpToEnd);

                currentTimeText = CreateLabel(editorPanel.transform, "0:00.000");
                startTimeText = CreateLabel(editorPanel.transform, "Start: 0:00.000");
                endTimeText = CreateLabel(editorPanel.transform, "End: 0:00.000");

                var playRect = playPauseButton.GetComponent<RectTransform>();
                playRect.anchorMin = new Vector2(0f, 1f);
                playRect.anchorMax = new Vector2(0f, 1f);
                playRect.pivot = new Vector2(0f, 1f);
                playRect.anchoredPosition = new Vector2(0f, 0f);
                playRect.sizeDelta = new Vector2(70f, 28f);

                var jumpStartRect = jumpStartButton.GetComponent<RectTransform>();
                jumpStartRect.anchorMin = new Vector2(0f, 1f);
                jumpStartRect.anchorMax = new Vector2(0f, 1f);
                jumpStartRect.pivot = new Vector2(0f, 1f);
                jumpStartRect.anchoredPosition = new Vector2(78f, 0f);
                jumpStartRect.sizeDelta = new Vector2(60f, 28f);

                var jumpEndRect = jumpEndButton.GetComponent<RectTransform>();
                jumpEndRect.anchorMin = new Vector2(0f, 1f);
                jumpEndRect.anchorMax = new Vector2(0f, 1f);
                jumpEndRect.pivot = new Vector2(0f, 1f);
                jumpEndRect.anchoredPosition = new Vector2(144f, 0f);
                jumpEndRect.sizeDelta = new Vector2(60f, 28f);

                var timeRect = currentTimeText.GetComponent<RectTransform>();
                timeRect.anchorMin = new Vector2(0f, 1f);
                timeRect.anchorMax = new Vector2(0f, 1f);
                timeRect.pivot = new Vector2(0f, 1f);
                timeRect.anchoredPosition = new Vector2(212f, -2f);
                timeRect.sizeDelta = new Vector2(160f, 24f);

                var startRect = startTimeText.GetComponent<RectTransform>();
                startRect.anchorMin = new Vector2(0f, 0f);
                startRect.anchorMax = new Vector2(0f, 0f);
                startRect.pivot = new Vector2(0f, 0f);
                startRect.anchoredPosition = new Vector2(0f, 6f);
                startRect.sizeDelta = new Vector2(240f, 22f);

                var endRect = endTimeText.GetComponent<RectTransform>();
                endRect.anchorMin = new Vector2(1f, 0f);
                endRect.anchorMax = new Vector2(1f, 0f);
                endRect.pivot = new Vector2(1f, 0f);
                endRect.anchoredPosition = new Vector2(0f, 6f);
                endRect.sizeDelta = new Vector2(240f, 22f);
            }

            EnsureTimelineHandles();
            uiBuilt = true;
        }

        private GameObject CreatePanel(string name, float height)
        {
            var panel = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );
            panel.transform.SetParent(transform, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 20f);
            rect.sizeDelta = new Vector2(-40f, height);

            var image = panel.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.2f);

            return panel;
        }

        private Button CreateButton(Transform parent, string name, string label)
        {
            var buttonObj = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button)
            );
            buttonObj.transform.SetParent(parent, false);
            var image = buttonObj.GetComponent<Image>();
            image.color = new Color(0.2f, 0.6f, 1f, 0.85f);

            var textObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObj.transform.SetParent(buttonObj.transform, false);
            var text = textObj.GetComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 16f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            if (uiFont != null)
            {
                text.font = uiFont;
            }

            var textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            return buttonObj.GetComponent<Button>();
        }

        private TextMeshProUGUI CreateLabel(Transform parent, string textValue)
        {
            var labelObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObj.transform.SetParent(parent, false);
            var text = labelObj.GetComponent<TextMeshProUGUI>();
            text.text = textValue;
            text.fontSize = 14f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            if (uiFont != null)
            {
                text.font = uiFont;
            }

            return text;
        }

        private void SetButtonWidth(Button button, float width)
        {
            if (button == null)
            {
                return;
            }

            var layout = button.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.minWidth = width;
        }

        private void EnsureTimelineHandles()
        {
            if (kiaiTimelineImage == null)
            {
                return;
            }

            if (selectionRange == null)
            {
                selectionRange = CreateTimelineImage("KiaiSelectionRange", selectionColor);
                selectionRange.SetAsFirstSibling();
            }

            if (startHandle == null)
            {
                startHandle = CreateHandle("KiaiStartHandle");
                var drag = startHandle.gameObject.AddComponent<KiaiHandleDrag>();
                drag.SetEditor(this, KiaiHandleDrag.HandleType.Start);
            }

            if (endHandle == null)
            {
                endHandle = CreateHandle("KiaiEndHandle");
                var drag = endHandle.gameObject.AddComponent<KiaiHandleDrag>();
                drag.SetEditor(this, KiaiHandleDrag.HandleType.End);
            }

            if (playhead == null)
            {
                playhead = CreateHandle("KiaiPlayhead", 2f, playheadColor);
                var drag = playhead.gameObject.AddComponent<KiaiHandleDrag>();
                drag.SetEditor(this, KiaiHandleDrag.HandleType.Playhead);
            }
        }

        private RectTransform CreateTimelineImage(string name, Color color)
        {
            var obj = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );
            obj.transform.SetParent(kiaiTimelineImage.transform, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = obj.GetComponent<Image>();
            image.color = color;

            return rect;
        }

        private RectTransform CreateHandle(
            string name,
            float width = 6f,
            Color? colorOverride = null
        )
        {
            var obj = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );
            obj.transform.SetParent(kiaiTimelineImage.transform, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            var height = kiaiTimelineImage.rectTransform.rect.height;
            if (height <= 0f)
            {
                height = 60f;
            }
            rect.sizeDelta = new Vector2(width, height + 12f);
            rect.anchoredPosition = Vector2.zero;

            var image = obj.GetComponent<Image>();
            image.color = colorOverride ?? handleColor;

            return rect;
        }

        private void HandleAnalysisComplete(OsuAnalyzer.AnalysisResult result)
        {
            ResetForNewTrack();
            currentResult = result;
            intervals.Clear();
            if (result != null && result.KiaiIntervals != null)
            {
                intervals.AddRange(result.KiaiIntervals);
            }

            hasIntervalsFromFile = intervals.Count > 0;

            totalMs = result?.LastHitObjectTimeMs ?? 0;
            if (audioSource != null && audioSource.clip != null)
            {
                totalMs = Mathf.Max(totalMs, (int)(audioSource.clip.length * 1000f));
            }
            if (totalMs <= 0 && intervals.Count > 0)
            {
                var maxEnd = 0;
                foreach (var iv in intervals)
                {
                    var end = iv.EndMs ?? iv.StartMs;
                    if (end > maxEnd)
                    {
                        maxEnd = end;
                    }
                }
                totalMs = maxEnd;
            }

            if (intervals.Count == 0)
            {
                startMs = 0;
                endMs = totalMs > 0 ? totalMs : 0;
                SetSelectionActive(false);
                SetEditorActive(true);
                return;
            }

            selectedIndex = 0;
            UpdateSelectionLabel();

            if (intervals.Count == 1)
            {
                ApplySelectedInterval();
                SetSelectionActive(false);
                SetEditorActive(true);
            }
            else
            {
                SetSelectionActive(true);
                SetEditorActive(false);
            }
        }

        private void HandleAudioClipReady(AudioClip clip)
        {
            if (clip == null)
            {
                return;
            }

            if (audioSource == null)
            {
                EnsureAudioSource();
            }

            audioSource.clip = clip;
            totalMs = Mathf.Max(totalMs, (int)(clip.length * 1000f));
            if (!hasIntervalsFromFile && endMs <= 0)
            {
                startMs = 0;
                endMs = totalMs;
            }
            UpdateTimeLabels();
            UpdateHandlesFromTimes();
        }

        private void ResetForNewTrack()
        {
            selectedIndex = 0;
            hasIntervalsFromFile = false;
            startMs = 0;
            endMs = 0;
            totalMs = 0;

            if (audioSource != null)
            {
                audioSource.Stop();
                audioSource.time = 0f;
                audioSource.clip = null;
            }

            SetSelectionActive(false);
            SetEditorActive(false);
            UpdatePlayPauseLabel();
            UpdateTimeLabels();
        }

        private void SelectPrevious()
        {
            if (intervals.Count == 0)
            {
                return;
            }

            selectedIndex = Mathf.Max(0, selectedIndex - 1);
            UpdateSelectionLabel();
        }

        private void SelectNext()
        {
            if (intervals.Count == 0)
            {
                return;
            }

            selectedIndex = Mathf.Min(intervals.Count - 1, selectedIndex + 1);
            UpdateSelectionLabel();
        }

        private void ApplySelectionAndEdit()
        {
            if (intervals.Count == 0)
            {
                return;
            }

            ApplySelectedInterval();
            SetSelectionActive(false);
            SetEditorActive(true);
        }

        private void ApplySelectedInterval()
        {
            if (intervals.Count == 0)
            {
                return;
            }

            var interval = intervals[selectedIndex];
            startMs = interval.StartMs;
            endMs = interval.EndMs ?? totalMs;
            if (endMs <= 0)
            {
                endMs = totalMs;
            }

            if (endMs < startMs)
            {
                endMs = startMs;
            }

            UpdateHandlesFromTimes();
            UpdateTimeLabels();
        }

        private void UpdateSelectionLabel()
        {
            if (selectionLabel == null || intervals.Count == 0)
            {
                return;
            }

            var interval = intervals[selectedIndex];
            var end = interval.EndMs ?? totalMs;
            if (end <= 0)
            {
                end = interval.StartMs;
            }

            selectionLabel.text =
                $"Kiai {selectedIndex + 1}/{intervals.Count}: {OsuAnalyzer.MsToTimeString(interval.StartMs)} - {OsuAnalyzer.MsToTimeString(end)}";

            if (prevButton != null)
            {
                prevButton.interactable = selectedIndex > 0;
            }
            if (nextButton != null)
            {
                nextButton.interactable = selectedIndex < intervals.Count - 1;
            }
        }

        private void SetSelectionActive(bool active)
        {
            if (selectionPanel != null)
            {
                selectionPanel.SetActive(active);
            }
        }

        private void SetEditorActive(bool active)
        {
            editorActive = active;
            if (editorPanel != null)
            {
                editorPanel.SetActive(active);
            }

            if (active)
            {
                UpdatePlayPauseLabel();
                UpdateHandlesFromTimes();
                UpdateTimeLabels();
                SetHandlesVisible(true);
            }
            else
            {
                SetHandlesVisible(false);
            }
        }

        private void SetHandlesVisible(bool visible)
        {
            if (selectionRange != null)
            {
                selectionRange.gameObject.SetActive(visible);
            }
            if (startHandle != null)
            {
                startHandle.gameObject.SetActive(visible);
            }
            if (endHandle != null)
            {
                endHandle.gameObject.SetActive(visible);
            }
            if (playhead != null)
            {
                playhead.gameObject.SetActive(visible);
            }
        }

        private void TogglePlayPause()
        {
            if (audioSource == null || audioSource.clip == null)
            {
                return;
            }

            if (audioSource.isPlaying)
            {
                audioSource.Pause();
            }
            else
            {
                var startSeconds = startMs / 1000f;
                var endSeconds = endMs / 1000f;
                if (
                    audioSource.time < startSeconds
                    || audioSource.time >= endSeconds - RestartThresholdSeconds
                )
                {
                    audioSource.time = startSeconds;
                }
                audioSource.Play();
            }

            UpdatePlayPauseLabel();
        }

        private void UpdatePlayPauseLabel()
        {
            if (playPauseLabel == null)
            {
                return;
            }

            playPauseLabel.text = audioSource != null && audioSource.isPlaying ? "Pause" : "Play";
        }

        private void UpdateTimeLabels()
        {
            UpdateCurrentTimeText((int)(audioSource != null ? audioSource.time * 1000f : 0f));
            if (startTimeText != null)
            {
                startTimeText.text = $"Start: {OsuAnalyzer.MsToTimeString(startMs)}";
            }
            if (endTimeText != null)
            {
                endTimeText.text = $"End: {OsuAnalyzer.MsToTimeString(endMs)}";
            }
        }

        private void JumpToStart()
        {
            if (audioSource == null || audioSource.clip == null)
            {
                return;
            }

            audioSource.time = startMs / 1000f;
            UpdateHandlesFromTimes();
            UpdateTimeLabels();
        }

        private void JumpToEnd()
        {
            if (audioSource == null || audioSource.clip == null)
            {
                return;
            }

            audioSource.time = endMs / 1000f;
            UpdateHandlesFromTimes();
            UpdateTimeLabels();
        }

        private void UpdateCurrentTimeText(int currentMs)
        {
            if (currentTimeText != null)
            {
                currentTimeText.text = OsuAnalyzer.MsToTimeString(currentMs);
            }
        }

        private void UpdateHandlesFromTimes()
        {
            if (totalMs <= 0)
            {
                return;
            }

            var startNorm = Mathf.Clamp01(startMs / (float)totalMs);
            var endNorm = Mathf.Clamp01(endMs / (float)totalMs);

            if (endNorm < startNorm)
            {
                var temp = startNorm;
                startNorm = endNorm;
                endNorm = temp;
            }

            if (selectionRange != null)
            {
                selectionRange.anchorMin = new Vector2(startNorm, 0f);
                selectionRange.anchorMax = new Vector2(endNorm, 1f);
                selectionRange.offsetMin = Vector2.zero;
                selectionRange.offsetMax = Vector2.zero;
            }

            SetHandleNormalized(startHandle, startNorm);
            SetHandleNormalized(endHandle, endNorm);

            var currentMs = (int)(audioSource != null ? audioSource.time * 1000f : 0f);
            UpdatePlayheadFromMs(currentMs);
        }

        private void UpdatePlayheadFromMs(int currentMs)
        {
            if (playhead == null || totalMs <= 0)
            {
                return;
            }

            var norm = Mathf.Clamp01(currentMs / (float)totalMs);
            SetHandleNormalized(playhead, norm);
        }

        private void SetHandleNormalized(RectTransform handle, float norm)
        {
            if (handle == null)
            {
                return;
            }

            handle.anchorMin = new Vector2(norm, 0.5f);
            handle.anchorMax = new Vector2(norm, 0.5f);
            handle.anchoredPosition = Vector2.zero;
        }

        public void HandleDrag(KiaiHandleDrag.HandleType type, PointerEventData eventData)
        {
            if (!editorActive || kiaiTimelineImage == null || totalMs <= 0)
            {
                return;
            }

            if (
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    kiaiTimelineImage.rectTransform,
                    eventData.position,
                    eventData.pressEventCamera,
                    out var localPoint
                )
            )
            {
                return;
            }

            var rect = kiaiTimelineImage.rectTransform.rect;
            var norm = Mathf.Clamp01((localPoint.x - rect.xMin) / rect.width);
            var newMs = Mathf.RoundToInt(norm * totalMs);

            switch (type)
            {
                case KiaiHandleDrag.HandleType.Start:
                    startMs = Mathf.Clamp(newMs, 0, endMs);
                    if (audioSource != null)
                    {
                        audioSource.time = startMs / 1000f;
                    }
                    break;
                case KiaiHandleDrag.HandleType.End:
                    endMs = Mathf.Clamp(newMs, startMs, totalMs);
                    if (audioSource != null)
                    {
                        audioSource.time = endMs / 1000f;
                    }
                    break;
                case KiaiHandleDrag.HandleType.Playhead:
                    SeekTo(norm);
                    break;
            }

            UpdateHandlesFromTimes();
            UpdateTimeLabels();
        }

        public void HandleSeek(PointerEventData eventData)
        {
            if (!editorActive || kiaiTimelineImage == null || totalMs <= 0)
            {
                return;
            }

            if (
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    kiaiTimelineImage.rectTransform,
                    eventData.position,
                    eventData.pressEventCamera,
                    out var localPoint
                )
            )
            {
                return;
            }

            var rect = kiaiTimelineImage.rectTransform.rect;
            var norm = Mathf.Clamp01((localPoint.x - rect.xMin) / rect.width);
            SeekTo(norm);
            UpdateHandlesFromTimes();
            UpdateTimeLabels();
        }

        private void SeekTo(float normalized)
        {
            if (audioSource != null && audioSource.clip != null && totalMs > 0)
            {
                var targetMs = Mathf.RoundToInt(normalized * totalMs);
                audioSource.time = targetMs / 1000f;
            }
        }
    }

    public class KiaiHandleDrag : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        public enum HandleType
        {
            Start,
            End,
            Playhead,
        }

        private KiaiIntervalEditor editor;
        private HandleType handleType;

        public void SetEditor(KiaiIntervalEditor owner, HandleType type)
        {
            editor = owner;
            handleType = type;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            editor?.HandleDrag(handleType, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            editor?.HandleDrag(handleType, eventData);
        }
    }

    public class KiaiTimelineInput : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        private KiaiIntervalEditor editor;

        public void SetEditor(KiaiIntervalEditor owner)
        {
            editor = owner;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            editor?.HandleSeek(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            editor?.HandleSeek(eventData);
        }
    }
}
