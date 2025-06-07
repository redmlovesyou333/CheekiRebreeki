using TMPro;
using UnityEngine;

namespace CheekiRebreeki.Core.UI
{
    public static class RevivePromptManager
    {
        private static GameObject _promptObject;
        private static TextMeshProUGUI _promptText;

        public static void Show(string key, string playerName)
        {
            string message = $"HOLD <color=#7FFF00>{key}</color> TO REVIVE <color=red>{playerName.ToUpper()}</color>";

            if (_promptObject == null)
            {
                _promptObject = new GameObject("CheekiRebreeki_RevivePromptUI");
                Object.DontDestroyOnLoad(_promptObject);

                var canvas = _promptObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 998;

                _promptText = _promptObject.AddComponent<TextMeshProUGUI>();
                _promptText.fontSize = 24;
                _promptText.alignment = TextAlignmentOptions.Center;

                var rectTransform = _promptText.rectTransform;
                // Positioned in the upper-middle of the screen
                rectTransform.anchorMin = new Vector2(0.5f, 0.6f);
                rectTransform.anchorMax = new Vector2(0.5f, 0.6f);
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
                rectTransform.sizeDelta = new Vector2(800, 50);
                rectTransform.anchoredPosition = Vector2.zero;
            }
            
            _promptText.text = message;
            _promptObject.SetActive(true);
        }

        public static void Hide()
        {
            if (_promptObject != null)
            {
                Object.Destroy(_promptObject);
                _promptObject = null;
                _promptText = null;
            }
        }
    }
}