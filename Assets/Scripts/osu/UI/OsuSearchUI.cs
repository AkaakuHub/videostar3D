using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;
using OsuTools.Api;

namespace OsuTools.UI
{
    /// <summary>
    /// UI component for searching and displaying osu! beatmap results.
    /// Completely rewritten with proper VerticalLayoutGroup approach.
    /// </summary>
    public class OsuSearchUI : MonoBehaviour
    {
        [Header("UI References - Assign in Inspector")]
        [SerializeField]
        private TMP_InputField searchInputField;

        [SerializeField]
        private Button searchButton;

        [SerializeField]
        private ScrollRect resultsScrollRect;

        [SerializeField]
        private Transform resultsContainer;

        [SerializeField]
        private TextMeshProUGUI statusText;

        [SerializeField]
        private GameObject loadingIndicator;

        [Header("Manager")]
        [SerializeField]
        private OsuSearchManager searchManager;

        [Header("Card Style")]
        [SerializeField]
        private Color cardBackgroundColor = new Color(0.15f, 0.15f, 0.2f);

        [SerializeField]
        private float cardHeight = 130f;

        [SerializeField]
        private float cardSpacing = 10f;

        private readonly List<GameObject> currentCards = new List<GameObject>();

        private void Awake()
        {
            if (searchManager == null)
            {
                searchManager = FindObjectOfType<OsuSearchManager>();
                if (searchManager == null)
                {
                    var managerObj = new GameObject("OsuSearchManager");
                    searchManager = managerObj.AddComponent<OsuSearchManager>();
                }
            }

            if (searchButton != null)
            {
                searchButton.onClick.AddListener(OnSearchButtonClicked);
            }

            if (searchInputField != null)
            {
                searchInputField.lineType = TMP_InputField.LineType.SingleLine;
                searchInputField.onSubmit.AddListener(_ => OnSearchButtonClicked());
            }

            searchManager.OnSearchComplete += HandleSearchComplete;
            searchManager.OnSearchError += HandleSearchError;

            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(false);
            }

            if (statusText != null)
            {
                statusText.text = "Enter a search term to find beatmaps";
            }
        }

        private void OnDestroy()
        {
            if (searchButton != null)
            {
                searchButton.onClick.RemoveListener(OnSearchButtonClicked);
            }

            if (searchManager != null)
            {
                searchManager.OnSearchComplete -= HandleSearchComplete;
                searchManager.OnSearchError -= HandleSearchError;
            }
        }

        private void OnSearchButtonClicked()
        {
            if (searchInputField == null || string.IsNullOrWhiteSpace(searchInputField.text))
            {
                SetStatus("Please enter a search term.");
                return;
            }

            var query = searchInputField.text.Trim();
            SetStatus($"Searching for: {query}...");
            ClearResults();

            if (searchButton != null)
            {
                searchButton.interactable = false;
            }

            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(true);
            }

            Debug.Log($"[OsuSearchUI] Searching for: {query}");
            searchManager.SearchBeatmaps(query, amount: 20);
        }

        private void HandleSearchComplete(OsuSearchResult[] results)
        {
            Debug.Log($"[OsuSearchUI] Search complete! Found {results?.Length ?? 0} results");

            if (searchButton != null)
            {
                searchButton.interactable = true;
            }

            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(false);
            }

            if (results == null || results.Length == 0)
            {
                SetStatus("No results found.");
                return;
            }

            SetStatus($"Found {results.Length} beatmap(s).");
            DisplayResults(results);
        }

        private void HandleSearchError(string error)
        {
            Debug.LogError($"[OsuSearchUI] Search error: {error}");

            if (searchButton != null)
            {
                searchButton.interactable = true;
            }

            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(false);
            }

            SetStatus($"Error: {error}");
        }

        private void DisplayResults(OsuSearchResult[] results)
        {
            Debug.Log($"[OsuSearchUI] Displaying {results.Length} results");
            ClearResults();

            foreach (var result in results)
            {
                var card = CreateResultCard(result, resultsContainer);
                currentCards.Add(card);
            }

            Debug.Log($"[OsuSearchUI] Created {currentCards.Count} cards");
        }

        /// <summary>
        /// Creates a result card with a completely new layout approach.
        /// Uses simple HorizontalLayoutGroup with proper LayoutElements.
        /// </summary>
        private GameObject CreateResultCard(OsuSearchResult result, Transform parent)
        {
            // 1. Card container
            var cardObj = new GameObject($"ResultCard_{result.id}");
            var cardRect = cardObj.AddComponent<RectTransform>();
            cardObj.transform.SetParent(parent, worldPositionStays: false);

            // Set card to full width with fixed height
            cardRect.anchorMin = new Vector2(0, 1);
            cardRect.anchorMax = new Vector2(1, 1);
            cardRect.pivot = new Vector2(0.5f, 1);
            cardRect.anchoredPosition = Vector2.zero;
            cardRect.sizeDelta = new Vector2(0, cardHeight);

            // Background
            var cardImage = cardObj.AddComponent<Image>();
            cardImage.color = cardBackgroundColor;

            // LayoutElement for parent VerticalLayoutGroup
            var cardLayout = cardObj.AddComponent<LayoutElement>();
            cardLayout.preferredHeight = cardHeight;
            cardLayout.minHeight = cardHeight;
            cardLayout.flexibleWidth = 1f;

            // 2. Main Horizontal Layout Container (stretched to fill card)
            var hLayoutObj = new GameObject("HorizontalLayout");
            var hLayoutRect = hLayoutObj.AddComponent<RectTransform>();
            hLayoutObj.transform.SetParent(cardObj.transform, worldPositionStays: false);

            // Stretch to fill card with padding
            hLayoutRect.anchorMin = Vector2.zero;
            hLayoutRect.anchorMax = Vector2.one;
            hLayoutRect.offsetMin = new Vector2(8f, 8f); // left, bottom padding
            hLayoutRect.offsetMax = new Vector2(-8f, -8f); // right, top padding

            // Horizontal Layout Group
            var hLayout = hLayoutObj.AddComponent<HorizontalLayoutGroup>();
            hLayout.padding = new RectOffset(0, 0, 0, 0);
            hLayout.spacing = 10f;
            hLayout.childAlignment = TextAnchor.MiddleLeft;
            hLayout.childControlWidth = false;
            hLayout.childControlHeight = false;
            hLayout.childForceExpandWidth = false;
            hLayout.childForceExpandHeight = false;

            // 3. Cover Image (fixed size)
            var coverObj = CreateCoverImage(hLayoutObj.transform, result);
            var coverLayout = coverObj.GetComponent<LayoutElement>();
            if (coverLayout == null)
            {
                coverLayout = coverObj.AddComponent<LayoutElement>();
            }
            coverLayout.preferredWidth = 90f;
            coverLayout.preferredHeight = 90f;
            coverLayout.minWidth = 90f;
            coverLayout.minHeight = 90f;

            // 4. Right Content Container (flexible width, contains info text and button)
            var rightContentObj = new GameObject("RightContent");
            var rightContentRect = rightContentObj.AddComponent<RectTransform>();
            rightContentObj.transform.SetParent(hLayoutObj.transform, worldPositionStays: false);

            // Right content uses VerticalLayoutGroup
            var rightVLayout = rightContentObj.AddComponent<VerticalLayoutGroup>();
            rightVLayout.padding = new RectOffset(0, 0, 0, 0);
            rightVLayout.spacing = 6f;
            rightVLayout.childAlignment = TextAnchor.UpperLeft;
            rightVLayout.childControlWidth = true;
            rightVLayout.childControlHeight = false;
            rightVLayout.childForceExpandWidth = true;
            rightVLayout.childForceExpandHeight = false;

            var rightLayoutElement = rightContentObj.AddComponent<LayoutElement>();
            rightLayoutElement.flexibleWidth = 1f;
            rightLayoutElement.preferredHeight = 90f;

            // 5. Info Text Area (with VerticalLayoutGroup for proper stacking)
            var infoAreaObj = new GameObject("InfoArea");
            var infoAreaRect = infoAreaObj.AddComponent<RectTransform>();
            infoAreaObj.transform.SetParent(rightContentObj.transform, worldPositionStays: false);

            // Vertical layout for text lines
            var infoVLayout = infoAreaObj.AddComponent<VerticalLayoutGroup>();
            infoVLayout.padding = new RectOffset(0, 0, 0, 0);
            infoVLayout.spacing = 2f;
            infoVLayout.childAlignment = TextAnchor.UpperLeft;
            infoVLayout.childControlWidth = true;
            infoVLayout.childControlHeight = false;
            infoVLayout.childForceExpandWidth = true;
            infoVLayout.childForceExpandHeight = false;

            var infoLayoutElement = infoAreaObj.AddComponent<LayoutElement>();
            infoLayoutElement.flexibleWidth = 1f;
            infoLayoutElement.preferredHeight = 75f;

            // Create each text line as a separate child
            CreateTextLine(infoAreaObj.transform, result.title, 15, FontStyles.Bold, Color.white, 20f);
            CreateTextLine(infoAreaObj.transform, result.artist, 13, FontStyles.Normal, new Color(0.75f, 0.75f, 0.75f), 18f);
            CreateTextLine(infoAreaObj.transform, $"Mapped by {result.creator}", 11, FontStyles.Normal, new Color(0.5f, 0.5f, 0.5f), 16f);
            CreateTextLine(infoAreaObj.transform, $"BPM: {result.bpm:F1}", 11, FontStyles.Normal, new Color(0.5f, 0.8f, 1f), 16f);

            // 6. Download Button
            var buttonObj = CreateDownloadButton(rightContentObj.transform, result);
            var buttonLayout = buttonObj.GetComponent<LayoutElement>();
            if (buttonLayout == null)
            {
                buttonLayout = buttonObj.AddComponent<LayoutElement>();
            }
            buttonLayout.preferredHeight = 32f;
            buttonLayout.minHeight = 32f;

            return cardObj;
        }

        /// <summary>
        /// Creates a single text line with proper layout.
        /// </summary>
        private void CreateTextLine(Transform parent, string text, int fontSize, FontStyles fontStyle, Color color, float preferredHeight)
        {
            var textObj = new GameObject("TextLine");
            textObj.transform.SetParent(parent, worldPositionStays: false);

            var rect = textObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0, preferredHeight);

            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontSizeMax = fontSize;
            tmp.fontSizeMin = 8;
            tmp.color = color;
            tmp.fontStyle = fontStyle;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.verticalAlignment = VerticalAlignmentOptions.Top;
            tmp.enableAutoSizing = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;

            var layoutElement = textObj.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = preferredHeight;
            layoutElement.minHeight = preferredHeight;
            layoutElement.flexibleWidth = 1f;
        }

        private GameObject CreateCoverImage(Transform parent, OsuSearchResult result)
        {
            var coverObj = new GameObject("CoverImage");
            coverObj.transform.SetParent(parent, worldPositionStays: false);

            var rect = coverObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(90, 90);

            var rawImage = coverObj.AddComponent<RawImage>();
            rawImage.color = Color.white;

            // Load cover asynchronously
            var coverUrl = result.covers?.cover ?? result.covers?.card ?? result.covers?.list;
            if (!string.IsNullOrEmpty(coverUrl))
            {
                StartCoroutine(LoadCoverImage(rawImage, coverUrl));
            }

            return coverObj;
        }

        private GameObject CreateDownloadButton(Transform parent, OsuSearchResult result)
        {
            var buttonObj = new GameObject("DownloadButton");
            buttonObj.transform.SetParent(parent, worldPositionStays: false);

            var rect = buttonObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0, 32);

            var button = buttonObj.AddComponent<Button>();

            // Button background
            var bgImage = buttonObj.AddComponent<Image>();
            bgImage.color = new Color(0.2f, 0.6f, 1f);
            button.targetGraphic = bgImage;

            // Button text
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform, worldPositionStays: false);

            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = "Download";
            tmp.fontSize = 13;
            tmp.fontSizeMax = 13;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.verticalAlignment = VerticalAlignmentOptions.Middle;

            // Button click handler
            button.onClick.AddListener(() =>
            {
                Debug.Log($"[OsuSearchUI] Download button clicked: {result.title} (ID: {result.id})");
                SetStatus($"Downloading {result.title}...");

                if (searchManager != null)
                {
                    searchManager.DownloadAndAnalyze(result.id, null);
                }
                else
                {
                    Debug.LogError("[OsuSearchUI] searchManager is null!");
                    SetStatus("Error: Manager not found");
                }
            });

            return buttonObj;
        }

        private IEnumerator LoadCoverImage(RawImage targetImage, string url)
        {
            using (var webRequest = UnityWebRequestTexture.GetTexture(url))
            {
                yield return webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.Success && targetImage != null)
                {
                    var texture = DownloadHandlerTexture.GetContent(webRequest);
                    targetImage.texture = texture;
                }
                else
                {
                    Debug.LogWarning($"Failed to load cover image: {url}");
                    if (targetImage != null)
                    {
                        targetImage.color = new Color(0.3f, 0.3f, 0.3f);
                    }
                }
            }
        }

        private void ClearResults()
        {
            foreach (var card in currentCards)
            {
                if (card != null)
                {
                    Destroy(card);
                }
            }
            currentCards.Clear();
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
                Debug.Log($"[OsuSearchUI] Status: {message}");
            }
        }
    }
}
