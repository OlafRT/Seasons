using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TreeJam
{
    /// <summary>Keeps the screen black across a scene reload, then reveals new buds.</summary>
    public sealed class TreeJamEndgameTransition : MonoBehaviour
    {
        private Image curtain;
        private static TreeJamEndgameTransition active;

        public static TreeJamEndgameTransition Create()
        {
            if (active != null) return active;
            GameObject root = new GameObject("TREE JAM - BLACKOUT (persistent)",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            DontDestroyOnLoad(root);
            active = root.AddComponent<TreeJamEndgameTransition>();
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            GameObject panel = new GameObject("Blackout");
            panel.transform.SetParent(root.transform, false);
            RectTransform rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            active.curtain = panel.AddComponent<Image>();
            active.curtain.color = new Color(0f,0f,0f,0f);
            active.curtain.raycastTarget = true;
            return active;
        }

        public IEnumerator FadeToBlack(float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / Mathf.Max(.01f, duration)));
                curtain.color = new Color(0f,0f,0f,p);
                yield return null;
            }
            curtain.color = Color.black;
        }

        public void BeginRebirth(string scenePath, float hold, float fade, float sproutSeconds)
        {
            StartCoroutine(Rebirth(scenePath,hold,fade,sproutSeconds));
        }

        private IEnumerator Rebirth(string scenePath, float hold, float fade, float sproutSeconds)
        {
            // Remain fully black long enough for the loss to register.
            curtain.color = Color.black;
            yield return new WaitForSecondsRealtime(Mathf.Max(0f,hold));
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("Endgame: scene has not been saved. Save the game scene and add it to Build Profiles.", this);
                yield break;
            }
            AsyncOperation load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (load == null)
            {
                Debug.LogError("Endgame: scene cannot be reloaded. Add it to the active Build Profile scenes.", this);
                yield break;
            }
            while (!load.isDone) yield return null;
            yield return null; // Allow OrganicBonsaiTree.Start to create its new leaf slots.
            OrganicBonsaiTree sprout = FindFirstObjectByType<OrganicBonsaiTree>();
            Transform newWood = sprout != null ? sprout.transform.Find("PROCEDURAL BONSAI (runtime)") : null;
            Vector3 fullSize = newWood != null ? newWood.localScale : Vector3.one;
            BonsaiLeaf[] buds = sprout != null ? sprout.GetComponentsInChildren<BonsaiLeaf>(true) : null;
            if (sprout != null) sprout.enabled = false; // Prevent the bonsai Update from overriding rebirth growth.
            if (newWood != null) newWood.localScale = fullSize * .14f;
            if (buds != null) foreach (BonsaiLeaf bud in buds) if (bud != null) bud.SetGrowth(1f,0f);
            float elapsed = 0f;
            float duration = Mathf.Max(fade, sproutSeconds);
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float fadeProgress = Mathf.SmoothStep(0f,1f,Mathf.Clamp01(elapsed / Mathf.Max(.01f,fade)));
                curtain.color = new Color(0f,0f,0f,1f-fadeProgress);
                float bloom = Mathf.SmoothStep(0f,1f,Mathf.Clamp01(elapsed / Mathf.Max(.01f,sproutSeconds)));
                if (newWood != null) newWood.localScale = fullSize * Mathf.Lerp(.14f, 1f, bloom);
                if (buds != null)
                {
                    // Twigs stretch from the old stump first; leaves emerge near the end.
                    float leafReveal = Mathf.SmoothStep(0f,1f,Mathf.Clamp01((bloom - .62f) / .38f));
                    foreach (BonsaiLeaf bud in buds) if (bud != null) bud.SetGrowth(1f,leafReveal);
                }
                yield return null;
            }
            if (newWood != null) newWood.localScale = fullSize;
            if (sprout != null) { sprout.SetLeafAmount(1f); sprout.enabled = true; }
            curtain.color = new Color(0f,0f,0f,0f);
            active = null;
            Destroy(gameObject);
        }
    }
}
