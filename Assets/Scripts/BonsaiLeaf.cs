using System;
using System.Collections;
using UnityEngine;

namespace TreeJam
{
    /// <summary>
    /// One real, independently-rendered leaf. A leaf can detach, flutter to the soil,
    /// remain as litter and later decompose. No season logic is assumed here.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class BonsaiLeaf : MonoBehaviour
    {
        public enum LeafState { Attached, Falling, Landed, Decomposing }

        public LeafState State { get; private set; } = LeafState.Attached;
        public float GrowthThreshold { get; private set; }
        public event Action<BonsaiLeaf> Landed;
        public event Action<BonsaiLeaf> Decomposed;

        private SpriteRenderer visual;
        private Vector3 fullScale;
        private Color normalColor;
        private Coroutine motion;

        public void Initialize(Vector3 scale, float threshold, Color tint)
        {
            visual = GetComponent<SpriteRenderer>();
            fullScale = scale;
            GrowthThreshold = threshold;
            normalColor = tint;
            visual.color = tint;
            State = LeafState.Attached;
            SetGrowth(1f, 1f);
        }

        public void SetTint(Color tint)
        {
            if (visual == null) visual = GetComponent<SpriteRenderer>();
            normalColor = tint;
            if (State == LeafState.Attached) visual.color = tint;
        }

        public void SetGrowth(float limbProgress, float canopyAmount)
        {
            if (State != LeafState.Attached) return;
            float bud = Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01((limbProgress - GrowthThreshold) / .18f));
            float size = bud * Mathf.Clamp01(canopyAmount);
            transform.localScale = fullScale * Mathf.Max(.0001f, size);
            if (visual == null) visual = GetComponent<SpriteRenderer>();
            visual.enabled = size > .003f;
        }

        /// <summary>
        /// Detaches the actual leaf object from its growing branch. The leaf remains
        /// in the world after landing. The tree creates a NEW leaf next spring.
        /// </summary>
        public void Fall(Transform leafLitterParent, float soilSurfaceWorldY, float fallSeconds)
        {
            if (State != LeafState.Attached) return;
            if (motion != null) StopCoroutine(motion);
            State = LeafState.Falling;
            transform.SetParent(leafLitterParent, true);
            if (visual == null) visual = GetComponent<SpriteRenderer>();
            visual.enabled = true;
            visual.sortingOrder = 9;
            motion = StartCoroutine(FlutterToGround(soilSurfaceWorldY, fallSeconds));
        }

        private IEnumerator FlutterToGround(float soilSurfaceWorldY, float fallSeconds)
        {
            Vector3 start = transform.position;
            float startAngle = transform.eulerAngles.z;
            Vector3 startScale = transform.localScale;
            float drift = UnityEngine.Random.Range(-.28f, .28f);
            float sway = UnityEngine.Random.Range(.09f, .22f);
            float phase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float spin = UnityEngine.Random.Range(-210f, 210f);
            float duration = Mathf.Max(.3f, fallSeconds);
            float endY = Mathf.Min(start.y - .05f, soilSurfaceWorldY + UnityEngine.Random.Range(.01f, .065f));

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float flutter = Mathf.Sin(t * Mathf.PI * 7f + phase) * sway * Mathf.Sin(Mathf.PI * t);
                float x = start.x + drift * t + flutter;
                // Slight vertical hesitation for a floating, parachuting leaf.
                float y = Mathf.Lerp(start.y, endY, t) +
                          .065f * (1f - t) * Mathf.Sin(t * Mathf.PI * 4f);
                transform.position = new Vector3(x, y, start.z);
                transform.rotation = Quaternion.Euler(0f, 0f,
                    startAngle + spin * t + 15f * Mathf.Sin(t * 16f + phase));
                transform.localScale = startScale;
                yield return null;
            }

            State = LeafState.Landed;
            motion = null;
            Landed?.Invoke(this);
        }

        /// <summary>
        /// Ready for the future nutrient system: call when leaf litter decomposes.
        /// The Decomposed event is fired once, allowing nutrients to be awarded.
        /// </summary>
        public void BeginDecomposition(float seconds)
        {
            if (State != LeafState.Landed) return;
            State = LeafState.Decomposing;
            if (motion != null) StopCoroutine(motion);
            motion = StartCoroutine(DecomposeRoutine(Mathf.Max(.1f, seconds)));
        }

        private IEnumerator DecomposeRoutine(float duration)
        {
            float elapsed = 0f;
            Vector3 startScale = transform.localScale;
            Color startColor = visual.color;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                transform.localScale = startScale * (1f - .65f * t);
                Color tint = startColor;
                tint.a = startColor.a * (1f - t);
                visual.color = tint;
                yield return null;
            }
            Decomposed?.Invoke(this);
            Destroy(gameObject);
        }
    }
}
