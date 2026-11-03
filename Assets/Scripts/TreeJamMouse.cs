using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TreeJam
{
    public class TreeJamMouse : MonoBehaviour
    {
        public static UpgradeOrb HoveredOrb { get; private set; }
        private Camera worldCamera;

        private void Awake() => worldCamera = Camera.main;

        private void Update()
        {
            Vector2 screenPosition;
            bool clicked;
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current == null) return;
            screenPosition = Mouse.current.position.ReadValue();
            clicked = Mouse.current.leftButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            screenPosition = Input.mousePosition;
            clicked = Input.GetMouseButtonDown(0);
#else
            return;
#endif
            if (worldCamera == null) worldCamera = Camera.main;
            if (worldCamera == null) return;

            Vector3 worldPosition = worldCamera.ScreenToWorldPoint(screenPosition);
            Collider2D hit = Physics2D.OverlapPoint(new Vector2(worldPosition.x, worldPosition.y));
            UpgradeOrb next = hit != null ? hit.GetComponent<UpgradeOrb>() : null;
            if (next != null && !next.IsVisible) next = null;

            HoveredOrb = next;
            if (clicked && next != null) next.Purchase();
        }

        private void OnDisable() => HoveredOrb = null;
    }
}
