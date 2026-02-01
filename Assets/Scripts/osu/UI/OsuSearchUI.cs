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
    /// Supports pagination and proper list display.
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

        [Header("Pagination Buttons")]
        [SerializeField]
        private Button previousButton;

        [SerializeField]
        private Button nextButton;

        [SerializeField]
        private TextMeshProUGUI pageInfoText;

        [Header("Manager")]
        [SerializeField]
        private OsuSearchManager searchManager;

        [Header("Card Style")]
        [SerializeField]
        private Color cardBackgroundColor = new Color(0.15f, 0.15f, 0.2f);

        [SerializeField]
        private float cardHeight = 130f;

        [Header("Text")]
        [SerializeField]
        private TMP_FontAsset resultsFont;

        [Header("Prefabs")]
        [SerializeField]
        private GameObject downloadButtonPrefab;

        [Header("Pagination Settings")]
        [SerializeField]
        private int resultsPerPage = 20;

        private readonly List<GameObject> currentCards = new List<GameObject>();
        private OsuSearchResult[] currentResults;
        private int currentPage = 0;
        private int totalResults = 0;
        private string currentQuery = string.Empty;

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

            if (previousButton != null)
            {
                previousButton.onClick.AddListener(OnPreviousButtonClicked);
            }

            if (nextButton != null)
            {
                nextButton.onClick.AddListener(OnNextButtonClicked);
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
                ApplyFont(statusText);
            }

            if (resultsFont == null)
            {
                resultsFont = statusText?.font
                              ?? pageInfoText?.font
                              ?? searchInputField?.textComponent?.font;
            }

            UpdatePaginationUI();
        }

        private void OnDestroy()
        {
            if (searchButton != null)
            {
                searchButton.onClick.RemoveListener(OnSearchButtonClicked);
            }

            if (previousButton != null)
            {
                previousButton.onClick.RemoveListener(OnPreviousButtonClicked);
            }

            if (nextButton != null)
            {
                nextButton.onClick.RemoveListener(OnNextButtonClicked);
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

            currentQuery = searchInputField.text.Trim();
            currentPage = 0;
            SetStatus($"Searching for: {currentQuery}...");
            ClearResults();

            if (searchButton != null)
            {
                searchButton.interactable = false;
            }

            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(true);
            }

            Debug.Log($"[OsuSearchUI] Searching for: {currentQuery} (page {currentPage})");
            searchManager.SearchBeatmaps(currentQuery, amount: resultsPerPage, offset: currentPage * resultsPerPage);
        }

        private void OnPreviousButtonClicked()
        {
            if (currentPage > 0)
            {
                currentPage--;
                LoadCurrentPage();
            }
        }

        private void OnNextButtonClicked()
        {
            // Check if there are more results
            if ((currentPage + 1) * resultsPerPage < totalResults || currentResults?.Length == resultsPerPage)
            {
                currentPage++;
                LoadCurrentPage();
            }
        }

        private void LoadCurrentPage()
        {
            SetStatus($"Loading page {currentPage + 1}...");
            ClearResults();

            if (searchButton != null)
            {
                searchButton.interactable = false;
            }

            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(true);
            }

            Debug.Log($"[OsuSearchUI] Loading page {currentPage} for: {currentQuery}");
            searchManager.SearchBeatmaps(currentQuery, amount: resultsPerPage, offset: currentPage * resultsPerPage);
        }

        private void HandleSearchComplete(OsuSearchResult[] results)
        {
            Debug.Log($"[OsuSearchUI] Search complete! Found {results?.Length ?? 0} results on page {currentPage}");

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
                currentResults = new OsuSearchResult[0];
                UpdatePaginationUI();
                return;
            }

            currentResults = results;
            totalResults += results.Length; // Approximate total
            SetStatus($"Found {results.Length} beatmap(s) on page {currentPage + 1}.");
            DisplayResults(results);
            UpdatePaginationUI();
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

        private void UpdatePaginationUI()
        {
            if (previousButton != null)
            {
                previousButton.interactable = currentPage > 0;
            }

            if (nextButton != null)
            {
                // Enable next button if we got a full page of results
                bool hasNextPage = currentResults != null && currentResults.Length == resultsPerPage;
                nextButton.interactable = hasNextPage;
            }

            if (pageInfoText != null)
            {
                pageInfoText.text = currentPage > 0 ? $"Page {currentPage + 1}" : "Page 1";
                ApplyFont(pageInfoText);
            }
        }

        private void DisplayResults(OsuSearchResult[] results)
        {
            Debug.Log($"[OsuSearchUI] Displaying {results.Length} results");
            ClearResults();

            foreach (var result in results)
            {
                var card = CreateResultCard(result, resultsContainer);
                currentCards.Add(card);
                // Force layout rebuild after each card
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(card.GetComponent<RectTransform>());
            }

            // Force final layout rebuild
            if (resultsContainer != null)
            {
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(resultsContainer as RectTransform);
            }

            Debug.Log($"[OsuSearchUI] Created {currentCards.Count} cards");

            // Reset scroll position to top
            if (resultsScrollRect != null)
            {
                resultsScrollRect.verticalNormalizedPosition = 1f;
            }
        }

        /// <summary>
        /// Creates a result card with proper layout for list display.
        /// </summary>
        private GameObject CreateResultCard(OsuSearchResult result, Transform parent)
        {
            // Card container
            var cardObj = new GameObject($"ResultCard_{result.id}");
            var cardRect = cardObj.AddComponent<RectTransform>();
            cardObj.transform.SetParent(parent, worldPositionStays: false);

            // Set card to full width with fixed height
            // VerticalLayoutGroup will override anchoredPosition
            cardRect.anchorMin = new Vector2(0, 1);
            cardRect.anchorMax = new Vector2(1, 1);
            cardRect.pivot = new Vector2(0.5f, 1);
            cardRect.anchoredPosition = new Vector2(0, 0); // Start at 0, VerticalLayoutGroup will adjust
            cardRect.sizeDelta = new Vector2(0, cardHeight);

            // Background
            var cardImage = cardObj.AddComponent<Image>();
            cardImage.color = cardBackgroundColor;

            // LayoutElement for VerticalLayoutGroup
            var layoutElement = cardObj.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = cardHeight;
            layoutElement.minHeight = cardHeight;
            layoutElement.flexibleWidth = 1f;

            // Content container with padding
            var contentObj = new GameObject("CardContent");
            var contentRect = contentObj.AddComponent<RectTransform>();
            contentObj.transform.SetParent(cardObj.transform, worldPositionStays: false);

            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.offsetMin = new Vector2(10, 10);
            contentRect.offsetMax = new Vector2(-10, -10);

            // Horizontal layout for content (Cover | Info + Button)
            var hLayout = contentObj.AddComponent<HorizontalLayoutGroup>();
            hLayout.padding = new RectOffset(0, 0, 0, 0);
            hLayout.spacing = 10f;
            hLayout.childAlignment = TextAnchor.MiddleLeft;
            hLayout.childControlWidth = true;
            hLayout.childControlHeight = true;
            hLayout.childForceExpandWidth = true;
            hLayout.childForceExpandHeight = false;

            // Cover Image (left side)
            var coverObj = CreateCoverImage(contentObj.transform, result);
            var coverLayout = coverObj.AddComponent<LayoutElement>();
            coverLayout.preferredWidth = 90f;
            coverLayout.preferredHeight = 90f;
            coverLayout.minWidth = 90f;
            coverLayout.minHeight = 90f;
            coverLayout.flexibleWidth = 0f;

            // Right side container (Info + Button)
            var rightContainerObj = new GameObject("RightContainer");
            var rightContainerRect = rightContainerObj.AddComponent<RectTransform>();
            rightContainerObj.transform.SetParent(contentObj.transform, worldPositionStays: false);
            rightContainerRect.anchorMin = Vector2.zero;
            rightContainerRect.anchorMax = Vector2.one;
            rightContainerRect.sizeDelta = Vector2.zero;

            // Vertical layout for right side
            var rightVLayout = rightContainerObj.AddComponent<VerticalLayoutGroup>();
            rightVLayout.padding = new RectOffset(0, 0, 0, 0);
            rightVLayout.spacing = 6f;
            rightVLayout.childAlignment = TextAnchor.UpperLeft;
            rightVLayout.childControlWidth = true;
            rightVLayout.childControlHeight = true;
            rightVLayout.childForceExpandWidth = true;
            rightVLayout.childForceExpandHeight = false;

            var rightLayout = rightContainerObj.AddComponent<LayoutElement>();
            rightLayout.flexibleWidth = 1f;
            rightLayout.preferredHeight = 90f;

            // Info text container
            var infoObj = CreateInfoText(rightContainerObj.transform, result);
            var infoLayout = infoObj.AddComponent<LayoutElement>();
            infoLayout.flexibleWidth = 1f;
            infoLayout.preferredHeight = 75f;

            // Download button
            var buttonObj = CreateDownloadButton(rightContainerObj.transform, result);
            if (buttonObj != null)
            {
                var buttonLayout = buttonObj.GetComponent<LayoutElement>() ?? buttonObj.AddComponent<LayoutElement>();
                buttonLayout.preferredHeight = 32f;
                buttonLayout.minHeight = 32f;
            }

            return cardObj;
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

        private GameObject CreateInfoText(Transform parent, OsuSearchResult result)
        {
            var infoObj = new GameObject("InfoText");
            infoObj.transform.SetParent(parent, worldPositionStays: false);

            var rect = infoObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;

            // Vertical layout for text lines
            var vLayout = infoObj.AddComponent<VerticalLayoutGroup>();
            vLayout.padding = new RectOffset(0, 0, 0, 0);
            vLayout.spacing = 2f;
            vLayout.childAlignment = TextAnchor.UpperLeft;
            vLayout.childControlWidth = true;
            vLayout.childControlHeight = true;
            vLayout.childForceExpandWidth = true;
            vLayout.childForceExpandHeight = false;

            // Create text elements
            var titleText = string.IsNullOrWhiteSpace(result.title_unicode) ? result.title : result.title_unicode;
            var artistText = string.IsNullOrWhiteSpace(result.artist_unicode) ? result.artist : result.artist_unicode;
            CreateTextElement(infoObj.transform, titleText, 15, FontStyles.Bold, Color.white);
            CreateTextElement(infoObj.transform, artistText, 13, FontStyles.Normal, new Color(0.75f, 0.75f, 0.75f));
            CreateTextElement(infoObj.transform, $"Mapped by {result.creator}", 11, FontStyles.Normal, new Color(0.5f, 0.5f, 0.5f));
            CreateTextElement(infoObj.transform, $"BPM: {result.bpm:F1}", 11, FontStyles.Normal, new Color(0.5f, 0.8f, 1f));

            return infoObj;
        }

        private void CreateTextElement(Transform parent, string text, int fontSize, FontStyles fontStyle, Color color)
        {
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(parent, worldPositionStays: false);

            var rect = textObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0, fontSize + 4f);

            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontSizeMax = fontSize;
            tmp.color = color;
            tmp.fontStyle = fontStyle;
            ApplyFont(tmp);
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.verticalAlignment = VerticalAlignmentOptions.Top;
            tmp.enableAutoSizing = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;

            var layoutElement = textObj.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = fontSize + 4f;
            layoutElement.minHeight = fontSize + 4f;
            layoutElement.flexibleWidth = 1f;
        }

        private GameObject CreateDownloadButton(Transform parent, OsuSearchResult result)
        {
            // Instantiate from prefab
            if (downloadButtonPrefab == null)
            {
                Debug.LogError("[OsuSearchUI] Download button prefab is not assigned!");
                return null;
            }

            var buttonObj = Instantiate(downloadButtonPrefab, parent);
            buttonObj.name = $"DownloadButton_{result.id}";

            var buttonRect = buttonObj.GetComponent<RectTransform>();
            if (buttonRect != null)
            {
                buttonRect.anchorMin = new Vector2(0, 1);
                buttonRect.anchorMax = new Vector2(1, 1);
                buttonRect.pivot = new Vector2(0.5f, 1);
                buttonRect.anchoredPosition = Vector2.zero;
                buttonRect.sizeDelta = new Vector2(0, 32f);
            }

            var button = buttonObj.GetComponent<Button>();
            if (button == null)
            {
                Debug.LogError("[OsuSearchUI] Download button prefab missing Button component!");
                return buttonObj;
            }

            ApplyFontToChildren(buttonObj);

            // Clear existing listeners and add new click handler
            button.onClick = new Button.ButtonClickedEvent();
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

        private void ApplyFont(TextMeshProUGUI text)
        {
            if (text != null && resultsFont != null)
            {
                text.font = resultsFont;
            }
        }

        private void ApplyFontToChildren(GameObject root)
        {
            if (root == null || resultsFont == null)
            {
                return;
            }

            foreach (var tmp in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                tmp.font = resultsFont;
            }
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
