using System;
using UnityEngine;

namespace TreeJam
{
    /// <summary>
    /// Controls daylight, photosynthesis, a movable sun, and a screen-sized night tint.
    /// Does not change the player's tree, upgrades, or environment art assets.
    /// A future SeasonManager can call SetDaylightHours to shorten winter days.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TreeJamDayNightCycle : MonoBehaviour
    {
        [Header("References — assigned by the installer")]
        public TreeGameState gameState;
        public Camera worldCamera;
        [Tooltip("Use your movable sun sprite (ideally cropped around the actual sun).")]
        public SpriteRenderer sunSprite;
        [Tooltip("A full-screen unlit tint: works with lit AND unlit artwork. Edit its darkness below.")]
        public SpriteRenderer nightOverlay;

        [Header("Clock")]
        [Min(10f)] public float secondsPerDay = 90f;
        [Range(0f, 24f)] public float startHour = 7.5f;
        [Tooltip("Summer default: sunrise at 5 AM. A later season system can change this.")]
        [Range(0f, 23f)] public float sunriseHour = 5f;
        [Tooltip("Summer default: sunset at 9 PM.")]
        [Range(1f, 24f)] public float sunsetHour = 21f;

        [Header("Sun arc (world space)")]
        [Tooltip("Y position of the sun at sunrise and sunset, measured in world units.")]
        public float sunHorizonY = -0.7f;
        [Tooltip("Highest Y position of the sun, measured in world units.")]
        public float sunNoonY = 4.8f;
        [Tooltip("Fraction of the camera's half-width reached by the sunrise/sunset position.")]
        [Range(0.25f, 1.5f)] public float sunHorizontalReach = 1.0f;
        [Tooltip("If you exported the Sun PNG on a full-sized canvas, use this to correct its visual offset. A tightly cropped Sun PNG is best.")]
        public Vector2 sunArtOffset = Vector2.zero;

        [Header("Atmosphere")]
        [Tooltip("At night, tint this much dark blue over world sprites. HUD remains unaffected.")]
        public Color nightTint = new Color(0.025f, 0.065f, 0.16f, 0.78f);
        [Tooltip("Higher values create a longer, more gradual twilight.")]
        [Range(0.15f, 1f)] public float twilightRange = 0.75f;
        [Tooltip("Shapes how strongly light rises at dawn and falls at dusk.")]
        [Range(0.35f, 2f)] public float sunlightCurvePower = 0.9f;

        public float CurrentHour { get; private set; }
        public int DayNumber { get; private set; } = 1;
        public float Sunlight01 { get; private set; } = 1f;
        public bool IsDaytime { get; private set; } = true;
        public event Action<int> NewDay;

        private float elapsed;
        private Color originalSunColor = Color.white;
        private bool originalSunEnabled;
        private bool hasOriginalSun;

        private void Awake()
        {
            if (gameState == null) gameState = FindFirstObjectByType<TreeGameState>();
            if (worldCamera == null) worldCamera = Camera.main;
            if (sunSprite != null)
            {
                originalSunColor = sunSprite.color;
                originalSunEnabled = sunSprite.enabled;
                hasOriginalSun = true;
            }
            Evaluate();
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            int previousDay = DayNumber;
            Evaluate();
            if (DayNumber > previousDay) NewDay?.Invoke(DayNumber);
        }

        public void SetDaylightHours(float sunrise, float sunset)
        {
            sunriseHour = Mathf.Clamp(sunrise, 0f, 23f);
            sunsetHour = Mathf.Clamp(sunset, sunriseHour + 0.1f, 24f);
        }

        private void Evaluate()
        {
            float daysElapsed = startHour / 24f + elapsed / Mathf.Max(10f, secondsPerDay);
            float dayFraction = Mathf.Repeat(daysElapsed, 1f);
            DayNumber = 1 + Mathf.FloorToInt(daysElapsed);
            CurrentHour = dayFraction * 24f;

            float dawn = sunriseHour / 24f;
            float dusk = Mathf.Max(dawn + 0.001f, sunsetHour / 24f);
            IsDaytime = dayFraction >= dawn && dayFraction <= dusk;
            float dayProgress = Mathf.Clamp01((dayFraction - dawn) / (dusk - dawn));
            float solarHeight = IsDaytime ? Mathf.Max(0f, Mathf.Sin(Mathf.PI * dayProgress)) : 0f;

            Sunlight01 = Mathf.Pow(solarHeight, sunlightCurvePower);
            if (gameState != null) gameState.SetSunlightFactor(Sunlight01);

            // Fade the entire scene rather than only adjusting Global Light 2D:
            // the player's Photoshop environment and bonsai leaves contain unlit sprites.
            float twilight = Mathf.Clamp01(solarHeight / Mathf.Max(0.01f, twilightRange));
            float darkness = 1f - twilight * twilight * (3f - 2f * twilight);
            if (nightOverlay != null)
            {
                Color tint = nightTint;
                tint.a *= darkness;
                nightOverlay.color = tint;
                FitOverlayToCamera();
            }
            if (sunSprite != null && hasOriginalSun)
            {
                // At night the sun goes below the horizon and fades out.
                sunSprite.enabled = IsDaytime && originalSunEnabled;
                float visibility = Mathf.Clamp01(solarHeight * 6f);
                Color tint = originalSunColor;
                tint.a *= visibility;
                sunSprite.color = tint;
                if (IsDaytime && worldCamera != null)
                {
                    float halfWidth = worldCamera.orthographicSize * worldCamera.aspect;
                    float horizontal = Mathf.Lerp(-halfWidth * sunHorizontalReach,
                        halfWidth * sunHorizontalReach, dayProgress);
                    float y = Mathf.Lerp(sunHorizonY, sunNoonY, solarHeight);
                    Vector3 p = worldCamera.transform.position;
                    sunSprite.transform.position = new Vector3(
                        p.x + horizontal + sunArtOffset.x,
                        y + sunArtOffset.y,
                        sunSprite.transform.position.z);
                }
            }
        }

        private void FitOverlayToCamera()
        {
            if (nightOverlay == null || nightOverlay.sprite == null || worldCamera == null ||
                !worldCamera.orthographic) return;
            float height = worldCamera.orthographicSize * 2f + 1f;
            float width = worldCamera.orthographicSize * worldCamera.aspect * 2f + 1f;
            Vector2 spriteSize = nightOverlay.sprite.bounds.size;
            if (spriteSize.x < 0.001f || spriteSize.y < 0.001f) return;
            nightOverlay.transform.position = new Vector3(
                worldCamera.transform.position.x, worldCamera.transform.position.y, 0f);
            nightOverlay.transform.localScale = new Vector3(
                width / spriteSize.x, height / spriteSize.y, 1f);
        }

        private void OnDisable()
        {
            if (gameState != null) gameState.SetSunlightFactor(1f);
            if (nightOverlay != null)
            {
                Color tint = nightOverlay.color;
                tint.a = 0f;
                nightOverlay.color = tint;
            }
            if (sunSprite != null && hasOriginalSun)
            {
                sunSprite.enabled = originalSunEnabled;
                sunSprite.color = originalSunColor;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Camera cam = worldCamera != null ? worldCamera : Camera.main;
            if (cam == null || !cam.orthographic) return;
            Gizmos.color = new Color(1f, .78f, .16f, .85f);
            Vector3 previous = Vector3.zero;
            for (int i = 0; i <= 30; i++)
            {
                float t = i / 30f;
                float halfWidth = cam.orthographicSize * cam.aspect;
                Vector3 point = new Vector3(
                    cam.transform.position.x + Mathf.Lerp(-halfWidth * sunHorizontalReach,
                        halfWidth * sunHorizontalReach, t) + sunArtOffset.x,
                    Mathf.Lerp(sunHorizonY, sunNoonY, Mathf.Sin(t * Mathf.PI)) + sunArtOffset.y,
                    0f);
                if (i > 0) Gizmos.DrawLine(previous, point);
                previous = point;
            }
        }
    }
}
