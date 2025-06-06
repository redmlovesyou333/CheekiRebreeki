using UnityEngine;
using TMPro;

namespace CheekiRebreeki.Core
{
    /// <summary>
    /// Manages the "YOU ARE DOWNED" on-screen message.
    /// It creates its own Canvas to ensure it displays correctly over all other UI elements.
    /// </summary>
    public class OnScreenTextManager : MonoBehaviour
    {
        private static GameObject _downedTextObject;
        private static TextMeshProUGUI _downedTextMesh;

        public static void ShowDownedMessage(string message)
        {
            // If the UI object already exists, just update the text.
            if (_downedTextObject != null)
            {
                if (_downedTextMesh != null)
                {
                    _downedTextMesh.text = message;
                }
                return;
            }

            // Create a new GameObject to hold the UI elements.
            _downedTextObject = new GameObject("DownedTextMessageUI");
            DontDestroyOnLoad(_downedTextObject);

            // Create a Canvas to render the text on the screen.
            // Using a separate canvas ensures it's not affected by other game UI state changes.
            var canvas = _downedTextObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000; // High sorting order to render on top.

            // Create the TextMeshPro component.
            _downedTextMesh = _downedTextObject.AddComponent<TextMeshProUGUI>();
            _downedTextMesh.text = message;
            _downedTextMesh.fontSize = 48;
            _downedTextMesh.color = Color.red;
            _downedTextMesh.alignment = TextAlignmentOptions.Center;

            // Stretch the text object to fill the entire screen for easy centering.
            var rectTransform = _downedTextMesh.rectTransform;
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            _downedTextObject.SetActive(true);
        }

        public static void HideDownedMessage()
        {
            if (_downedTextObject != null)
            {
                Destroy(_downedTextObject);
                _downedTextObject = null;
                _downedTextMesh = null;
            }
        }
    }
}