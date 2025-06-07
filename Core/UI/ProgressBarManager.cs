using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CheekiRebreeki.Core.UI
{
    public static class ProgressBarManager
    {
        private static GameObject _progressBarObject;
        private static RectTransform _fillImageRect;
        private static RectTransform _backgroundRect;
        private static TextMeshProUGUI _titleText;

        public static void Show(string title)
        {
            if (_progressBarObject != null)
            {
                _titleText.text = title;
                UpdateProgress(0);
                _progressBarObject.SetActive(true);
                return;
            }

            // Create container GameObject
            _progressBarObject = new GameObject("CheekiRebreeki_ProgressBarUI");
            Object.DontDestroyOnLoad(_progressBarObject);

            // Create Canvas
            var canvas = _progressBarObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;

            // Create Background Image
            var bgObject = new GameObject("Background");
            bgObject.transform.SetParent(_progressBarObject.transform);
            var bgImage = bgObject.AddComponent<Image>();
            bgImage.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
            
            _backgroundRect = bgObject.GetComponent<RectTransform>();
            _backgroundRect.anchorMin = new Vector2(0.5f, 0.55f);
            _backgroundRect.anchorMax = new Vector2(0.5f, 0.55f);
            _backgroundRect.pivot = new Vector2(0.5f, 0.5f);
            _backgroundRect.sizeDelta = new Vector2(400, 35); // Thicker bar
            _backgroundRect.anchoredPosition = Vector2.zero;

            // Create Fill Image using a manual RectTransform approach for reliability
            var fillObject = new GameObject("Fill");
            fillObject.transform.SetParent(bgObject.transform);
            var fillImage = fillObject.AddComponent<Image>();
            fillImage.color = new Color(0.2f, 0.8f, 0.2f, 0.9f); // Green color
            
            _fillImageRect = fillObject.GetComponent<RectTransform>();
            _fillImageRect.anchorMin = new Vector2(0, 0.5f); // Anchor to the left-center
            _fillImageRect.anchorMax = new Vector2(0, 0.5f);
            _fillImageRect.pivot = new Vector2(0, 0.5f); // Pivot from the left
            _fillImageRect.anchoredPosition = Vector2.zero;

            // Create Title Text
            var textObject = new GameObject("Title");
            textObject.transform.SetParent(bgObject.transform);
            _titleText = textObject.AddComponent<TextMeshProUGUI>();
            _titleText.text = title;
            _titleText.fontSize = 18;
            _titleText.color = Color.white;
            _titleText.alignment = TextAlignmentOptions.Center;
            
            var textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            UpdateProgress(0); // Set initial progress
            _progressBarObject.SetActive(true);
        }

        public static void UpdateProgress(float progress)
        {
            if (_fillImageRect != null && _backgroundRect != null)
            {
                progress = Mathf.Clamp01(progress);
                // Manually set the width of the fill rectangle based on progress
                _fillImageRect.sizeDelta = new Vector2(_backgroundRect.sizeDelta.x * progress, _backgroundRect.sizeDelta.y);
            }
        }

        public static void Hide()
        {
            if (_progressBarObject != null)
            {
                Object.Destroy(_progressBarObject);
                _progressBarObject = null;
                _fillImageRect = null;
                _backgroundRect = null;
                _titleText = null;
            }
        }
    }
}