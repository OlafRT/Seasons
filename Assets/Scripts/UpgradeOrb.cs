using System.Collections;
using UnityEngine;

namespace TreeJam
{
    [RequireComponent(typeof(Collider2D))]
    public class UpgradeOrb : MonoBehaviour
    {
        public UpgradeDefinition definition;
        public TreeGameState gameState;
        [Tooltip("The two glowing sprites on this orb")]
        public GameObject orbGraphics;
        [Tooltip("Branch/root artwork to show when this upgrade is purchased")]
        public GameObject growthVisual;

        private Collider2D clickCollider;
        private bool isVisible;
        private Vector3 fullGrowthScale;
        private Vector3 originalGraphicsScale;

        public bool IsVisible => isVisible;

        private void Awake()
        {
            clickCollider = GetComponent<Collider2D>();
            if (gameState == null) gameState = FindFirstObjectByType<TreeGameState>();
            if (growthVisual != null)
            {
                fullGrowthScale = growthVisual.transform.localScale;
                growthVisual.SetActive(false);
            }
            if (orbGraphics != null) originalGraphicsScale = orbGraphics.transform.localScale;
        }

        private void OnEnable()
        {
            if (gameState != null) gameState.UpgradePurchased += OnUpgradePurchased;
        }

        private void Start() => Refresh();

        private void OnDisable()
        {
            if (gameState != null) gameState.UpgradePurchased -= OnUpgradePurchased;
        }

        private void OnUpgradePurchased(UpgradeDefinition purchased)
        {
            Refresh();
        }

        private void Refresh()
        {
            isVisible = gameState != null && gameState.IsUnlocked(definition);
            if (orbGraphics != null) orbGraphics.SetActive(isVisible);
            if (clickCollider != null) clickCollider.enabled = isVisible;
        }

        private void Update()
        {
            if (!isVisible || orbGraphics == null) return;
            float pulse = 1f + 0.07f * Mathf.Sin(Time.time * 3.2f + transform.position.x);
            orbGraphics.transform.localScale = originalGraphicsScale * pulse;
        }

        public void Purchase()
        {
            if (!isVisible || gameState == null) return;
            if (!gameState.TryPurchase(definition)) return;
            if (growthVisual != null) StartCoroutine(AnimateGrowth());
        }

        private IEnumerator AnimateGrowth()
        {
            growthVisual.SetActive(true);
            const float duration = 0.45f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                growthVisual.transform.localScale = fullGrowthScale * Mathf.Max(0.001f, t);
                yield return null;
            }
            growthVisual.transform.localScale = fullGrowthScale;
        }
    }
}
