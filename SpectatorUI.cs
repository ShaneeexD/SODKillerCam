using UnityEngine;
using UnityEngine.UI;
using TMPro; // Use TextMeshPro for better text rendering if available, otherwise use UnityEngine.UI.Text

namespace KillerCam
{
    public static class SpectatorUI
    {
        private static GameObject spectatorTextObject;
        private static TextMeshProUGUI spectatorText; // Or use Text spectatorText if TMP isn't available/preferred
        private static bool isCreated = false;

        public static void CreateSpectatorText()
        {
            if (isCreated) return;

            try
            {
                // Find the main Canvas
                Canvas canvas = GameObject.FindObjectOfType<Canvas>();
                if (canvas == null)
                {
                    KillerCam.Logger.LogError("SpectatorUI: Canvas not found!");
                    return;
                }

                // Create the GameObject for the text
                spectatorTextObject = new GameObject("SpectatorInfoText");
                spectatorTextObject.transform.SetParent(canvas.transform, false); // Set 'worldPositionStays' to false

                // Add and configure the RectTransform
                RectTransform rectTransform = spectatorTextObject.AddComponent<RectTransform>();
                rectTransform.anchorMin = new Vector2(0.5f, 0); // Bottom center anchor
                rectTransform.anchorMax = new Vector2(0.5f, 0);
                rectTransform.pivot = new Vector2(0.5f, 0); // Pivot at the bottom center
                rectTransform.anchoredPosition = new Vector2(0, 30); // Position 30 units up from the bottom edge
                rectTransform.sizeDelta = new Vector2(500, 50); // Set a reasonable size

                // Add the TextMeshProUGUI component (or UnityEngine.UI.Text)
                spectatorText = spectatorTextObject.AddComponent<TextMeshProUGUI>();
                if (spectatorText == null)
                {
                    KillerCam.Logger.LogError("SpectatorUI: Failed to add TextMeshProUGUI component.");
                    GameObject.Destroy(spectatorTextObject);
                    return;
                }

                // Configure the text properties
                spectatorText.fontSize = 24;
                spectatorText.color = Color.white;
                spectatorText.alignment = TextAlignmentOptions.Center; // Center alignment
                spectatorText.fontStyle = FontStyles.Bold; // Make it bold
                // Optional: Add an outline or shadow for better readability
                // You might need to add Outline or Shadow component if using UnityEngine.UI.Text
                // For TextMeshPro, you might adjust material properties or use its built-in outline features.

                spectatorText.text = ""; // Start with empty text
                spectatorTextObject.SetActive(false); // Start hidden

                isCreated = true;
                KillerCam.Logger.LogInfo("SpectatorUI created successfully.");
            }
            catch (System.Exception ex)
            {
                KillerCam.Logger.LogError($"SpectatorUI: Error creating UI - {ex.Message}\n{ex.StackTrace}");
                if (spectatorTextObject != null) GameObject.Destroy(spectatorTextObject);
                isCreated = false;
            }
        }

        public static void UpdateText(string message)
        {
            if (!isCreated) CreateSpectatorText();
            if (!isCreated || spectatorText == null) return;

            spectatorTextObject.SetActive(true); // Ensure the object is active before setting text
            spectatorText.text = message;
        }

        public static void ShowText()
        {
             if (!isCreated || spectatorTextObject == null) return;
             spectatorTextObject.SetActive(true);
        }

        public static void HideText()
        {
            if (!isCreated || spectatorTextObject == null) return;
            spectatorTextObject.SetActive(false);
        }

        public static void DestroySpectatorText()
        {
            if (spectatorTextObject != null)
            {
                HideText(); // Ensure text is hidden before destroying
                GameObject.Destroy(spectatorTextObject);
                spectatorTextObject = null;
                spectatorText = null;
                isCreated = false;
                KillerCam.Logger.LogInfo("SpectatorUI destroyed.");
            }
        }
    }
}
