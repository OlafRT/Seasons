using UnityEngine;

namespace TreeJam
{
    /// <summary>
    /// Reveals the user's Night Sky sprite, already behind the Day Sky, by fading
    /// the top sprite's alpha. Leaves the existing sun motion and night tint intact.
    /// Also supplies a configurable VERY small summer midnight light floor.
    /// </summary>
    [DefaultExecutionOrder(1600)]
    [DisallowMultipleComponent]
    public sealed class TreeJamNordicSky : MonoBehaviour
    {
        [Header("Assign your EXISTING Photoshop layers")]
        public SpriteRenderer daySky;
        public SpriteRenderer nightSky;
        public TreeJamDayNightCycle dayNight;
        public TreeJamSeasonCycle seasons;
        public TreeGameState resources;

        [Header("Day sky visibility at full night")]
        [Range(0f, 1f)] public float springNightDaySkyAlpha = .18f;
        [Range(0f, 1f)] public float summerNightDaySkyAlpha = .48f;
        [Range(0f, 1f)] public float autumnNightDaySkyAlpha = .12f;
        [Range(0f, 1f)] public float winterNightDaySkyAlpha = .02f;
        [Range(.02f, .8f)] public float twilightSmoothing = .45f;
        [Header("Nordic light: 0 at night outside summer")]
        [Range(0f, .25f)] public float summerNightPhotosynthesis = .10f;
        public bool changeNightOverlayIntensity = true;
        [Range(.2f, 1f)] public float summerOverlayMultiplier = .62f;
        [Range(.2f, 1f)] public float winterOverlayMultiplier = .97f;

        private Color initialDayColor;
        private Color initialNightColor;
        private bool initialized;

        private void Start()
        {
            if (dayNight == null) dayNight = FindFirstObjectByType<TreeJamDayNightCycle>();
            if (seasons == null) seasons = FindFirstObjectByType<TreeJamSeasonCycle>();
            if (resources == null) resources = FindFirstObjectByType<TreeGameState>();
            if (daySky != null) initialDayColor = daySky.color;
            if (nightSky != null) initialNightColor = nightSky.color;
            initialized = true;
            if (daySky == null || nightSky == null)
                Debug.LogWarning("Tree Jam Nordic Sky: assign BOTH Day Sky and Night Sky SpriteRenderers in the Inspector. The second must render behind the first.", this);
        }

        private void LateUpdate()
        {
            if (!initialized || dayNight == null) return;
            TreeSeason season = seasons != null ? seasons.CurrentSeason : TreeSeason.Summer;
            float nightAlpha = springNightDaySkyAlpha;
            switch (season)
            {
                case TreeSeason.Summer: nightAlpha = summerNightDaySkyAlpha; break;
                case TreeSeason.Autumn: nightAlpha = autumnNightDaySkyAlpha; break;
                case TreeSeason.Winter: nightAlpha = winterNightDaySkyAlpha; break;
            }

            // 0 at night -> 1 in bright daylight; smooth to avoid visible popping.
            float dayVisibility = Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01(dayNight.Sunlight01 / Mathf.Max(.02f, twilightSmoothing)));
            if (daySky != null)
            {
                Color tint = initialDayColor;
                Color seasonTint = Color.white;
                if (season == TreeSeason.Autumn) seasonTint = new Color(1f, .95f, .88f);
                if (season == TreeSeason.Winter) seasonTint = new Color(.90f, .94f, 1f);
                tint.r *= seasonTint.r;
                tint.g *= seasonTint.g;
                tint.b *= seasonTint.b;
                tint.a = initialDayColor.a * Mathf.Lerp(nightAlpha, 1f, dayVisibility);
                daySky.color = tint;
            }
            if (nightSky != null)
            {
                // Keep it opaque while the day layer covers it. No separate crossfade needed.
                Color tint = initialNightColor;
                tint.a = initialNightColor.a;
                nightSky.color = tint;
            }

            if (changeNightOverlayIntensity && dayNight.nightOverlay != null)
            {
                Color tint = dayNight.nightOverlay.color;
                float m = season == TreeSeason.Summer ? summerOverlayMultiplier :
                          season == TreeSeason.Winter ? winterOverlayMultiplier : .84f;
                tint.a *= m;
                dayNight.nightOverlay.color = tint;
            }
            // SeasonCycle writes its sunlight factor in LateUpdate; this runs after it.
            // Midnight twilight is only an optional summer mechanic, not true full sunlight.
            if (resources != null && season == TreeSeason.Summer && !dayNight.IsDaytime)
                resources.SetSunlightFactor(Mathf.Max(resources.SunlightFactor, summerNightPhotosynthesis));
        }

        private void OnDisable()
        {
            if (!initialized) return;
            if (daySky != null) daySky.color = initialDayColor;
            if (nightSky != null) nightSky.color = initialNightColor;
        }
    }
}
