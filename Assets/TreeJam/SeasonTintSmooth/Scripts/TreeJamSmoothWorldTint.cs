using UnityEngine;

namespace TreeJam
{
    /// <summary>
    /// Visual-only smoothing layer for the existing seasons, day/night overlay and
    /// Nordic day sky. Runs AFTER their Update/LateUpdate methods. Does not change
    /// the clock, sunlight factor, growth, or season progression.
    /// </summary>
    [DefaultExecutionOrder(30000)]
    [DisallowMultipleComponent]
    public sealed class TreeJamSmoothWorldTint : MonoBehaviour
    {
        [Header("Existing scene components")]
        public TreeJamSeasonCycle seasons;
        public TreeJamDayNightCycle dayNight;
        public TreeJamNordicSky nordicSky;
        public SpriteRenderer seasonOverlay;
        public SpriteRenderer nightOverlay;
        public SpriteRenderer daySky;

        [Header("Transition times (real seconds)")]
        [Tooltip("How long the world takes to ease between seasonal colors.")]
        [Min(0.1f)] public float seasonTransitionSeconds = 20f;
        [Tooltip("Time needed to complete about 90% of the night tint fade at dawn/dusk.")]
        [Min(0.1f)] public float twilightFadeSeconds = 4.5f;
        [Tooltip("Time needed to complete about 90% of the day sky fade at dawn/dusk.")]
        [Min(0.1f)] public float skyFadeSeconds = 4.5f;

        private bool initialized;
        private TreeSeason observedSeason;
        private float seasonTransitionElapsed;
        private Color seasonFrom, seasonTo, seasonShown;
        private Color nightTintFrom, nightTintTo;
        private Color nightOverlayShown, daySkyShown;

        private void Start()
        {
            if (seasons == null) seasons = FindFirstObjectByType<TreeJamSeasonCycle>();
            if (dayNight == null) dayNight = FindFirstObjectByType<TreeJamDayNightCycle>();
            if (nordicSky == null) nordicSky = FindFirstObjectByType<TreeJamNordicSky>();
            if (seasonOverlay == null && seasons != null) seasonOverlay = seasons.seasonTintOverlay;
            if (nightOverlay == null && dayNight != null) nightOverlay = dayNight.nightOverlay;
            if (daySky == null && nordicSky != null) daySky = nordicSky.daySky;

            observedSeason = seasons != null ? seasons.CurrentSeason : TreeSeason.Spring;
            seasonFrom = seasonTo = seasonShown = seasonOverlay != null ? seasonOverlay.color : Color.clear;
            nightTintFrom = nightTintTo = dayNight != null ? dayNight.nightTint : Color.clear;
            currentNightTint = nightTintTo;
            nightOverlayShown = nightOverlay != null ? nightOverlay.color : Color.clear;
            daySkyShown = daySky != null ? daySky.color : Color.white;
            initialized = true;
        }

        private void LateUpdate()
        {
            if (!initialized) return;
            float dt = Time.deltaTime;

            // SeasonCycle sets these targets INSTANTLY at season changes. Capture
            // the new targets, then show an eased interpolation instead.
            if (seasons != null && seasons.CurrentSeason != observedSeason)
            {
                observedSeason = seasons.CurrentSeason;
                seasonTransitionElapsed = 0f;
                seasonFrom = seasonShown;
                seasonTo = seasonOverlay != null ? seasonOverlay.color : seasonFrom;
                if (dayNight != null)
                {
                    // Use the previously smoothed tint, not the new abrupt target.
                    nightTintFrom = currentNightTint;
                    nightTintTo = dayNight.nightTint;
                }
            }

            seasonTransitionElapsed += dt;
            float t = Mathf.Clamp01(seasonTransitionElapsed / Mathf.Max(0.1f, seasonTransitionSeconds));
            t = t * t * (3f - 2f * t); // ease-in/out, no hard color step

            if (seasonOverlay != null)
            {
                seasonShown = Color.LerpUnclamped(seasonFrom, seasonTo, t);
                seasonOverlay.color = seasonShown;
            }
            if (dayNight != null)
            {
                currentNightTint = Color.LerpUnclamped(nightTintFrom, nightTintTo, t);
                dayNight.nightTint = currentNightTint;
            }

            // DayNight and NordicSky write their real-time target color every frame.
            // Soften their *visible* tint, preserving the clock/solar calculations.
            if (nightOverlay != null)
            {
                float a = ResponseAmount(dt, twilightFadeSeconds);
                nightOverlayShown = Color.LerpUnclamped(nightOverlayShown, nightOverlay.color, a);
                nightOverlay.color = nightOverlayShown;
            }
            if (daySky != null)
            {
                float a = ResponseAmount(dt, skyFadeSeconds);
                daySkyShown = Color.LerpUnclamped(daySkyShown, daySky.color, a);
                daySky.color = daySkyShown;
            }
        }

        private Color currentNightTint;

        private static float ResponseAmount(float deltaTime, float secondsForNinetyPercent)
        {
            // Frame-rate independent exponential easing (90% within specified time).
            return 1f - Mathf.Exp(-2.302585093f * deltaTime / Mathf.Max(0.1f, secondsForNinetyPercent));
        }
    }
}
