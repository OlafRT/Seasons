using System;

namespace TreeJam
{
    // Compatibility bridge for TreeGameState.TryPurchase.
    // UI is now drawn by a normal, editable Canvas instead of OnGUI.
    public class TreeJamHUD : UnityEngine.MonoBehaviour
    {
        public TreeGameState gameState; // Retained for scenes made by the first starter.
        public static event Action<string> MessageRaised;

        public static void Notify(string text)
        {
            MessageRaised?.Invoke(text);
        }
    }
}
