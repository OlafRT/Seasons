using UnityEngine;
using UnityEngine.UI;

namespace TreeJam
{
    public enum TreeSeason { Spring, Summer, Autumn, Winter }

    /// <summary>
    /// Bridges the existing TreeJamDayNightCycle and OrganicBonsaiTree.
    /// A new season begins at sunrise after exactly NightsPerSeason complete nights.
    /// No existing gameplay or renderer script needs to be replaced.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TreeJamSeasonCycle : MonoBehaviour
    {
        [Header("Existing components")]
        public TreeJamDayNightCycle dayNight;
        public TreeGameState resources;
        public OrganicBonsaiTree bonsai;
        public SpriteRenderer seasonTintOverlay;
        public Text seasonLabel;

        [Header("Time and pacing")]
        public TreeSeason startingSeason = TreeSeason.Spring;
        [Min(1)] public int nightsPerSeason = 2;
        [Tooltip("Seconds for a full day, including its night. A 45 second day gives a roughly 6 minute year.")]
        [Min(10)] public float secondsPerDay = 45f;
        [Tooltip("Keep the bonsai's very slow passive growth in sync with simulated years.")]
        public bool syncBonsaiAge = true;
        [Min(0f)] public float autumnLeafFallSeconds = 28f;
        [Min(0f)] public float springRegrowthSeconds = 12f;

        [Header("Daylight length by season (simulated clock hours)")]
        public Vector2 springDaylight = new Vector2(6f, 19.5f);
        public Vector2 summerDaylight = new Vector2(4f, 22f);
        public Vector2 autumnDaylight = new Vector2(8f, 17f);
        public Vector2 winterDaylight = new Vector2(10f, 14f);
        [Tooltip("How quickly daylight hours interpolate when the season changes, in simulated hours per real second.")]
        [Min(0.01f)] public float daylightTransitionSpeed = .09f;

        public TreeSeason CurrentSeason { get; private set; }
        public int Year { get; private set; } = 1;
        public int NightsThisSeason { get; private set; }
        public int NightsSinceStart { get; private set; }

        private bool wasDaytime;
        private float totalElapsed;
        private float elapsedInSeason;
        private Color originalNightTint;
        private float targetSunrise, targetSunset;

        private void Awake()
        {
            if (dayNight == null) dayNight = FindFirstObjectByType<TreeJamDayNightCycle>();
            if (resources == null) resources = FindFirstObjectByType<TreeGameState>();
            if (bonsai == null) bonsai = FindFirstObjectByType<OrganicBonsaiTree>();
            CurrentSeason = startingSeason;
        }

        private void Start()
        {
            if (dayNight == null)
            {
                Debug.LogError("Tree Jam Seasons: Missing TreeJamDayNightCycle. Install the day/night patch first.", this);
                enabled = false;
                return;
            }
            dayNight.secondsPerDay = secondsPerDay;
            originalNightTint = dayNight.nightTint;
            wasDaytime = dayNight.IsDaytime;
            SetSeasonVisualsAndHours(initial: true);
            RefreshLabel();
        }

        private void Update()
        {
            if (dayNight == null) return;
            totalElapsed += Time.deltaTime;
            elapsedInSeason += Time.deltaTime;
            dayNight.secondsPerDay = secondsPerDay;

            // Count complete nights only when the following sunrise is reached.
            bool isDay = dayNight.IsDaytime;
            if (isDay && !wasDaytime)
            {
                NightsSinceStart++;
                NightsThisSeason++;
                if (NightsThisSeason >= Mathf.Max(1, nightsPerSeason))
                    AdvanceSeason();
            }
            wasDaytime = isDay;

            // Avoid a sudden sun jump as the season changes.
            dayNight.sunriseHour = Mathf.MoveTowards(dayNight.sunriseHour,
                targetSunrise, daylightTransitionSpeed * Time.deltaTime);
            dayNight.sunsetHour = Mathf.MoveTowards(dayNight.sunsetHour,
                targetSunset, daylightTransitionSpeed * Time.deltaTime);

            if (syncBonsaiAge && bonsai != null)
            {
                float daysPerYear = 4f * Mathf.Max(1, nightsPerSeason);
                // We start at Year 1, age zero. Growth is gradual across each simulated day.
                bonsai.SetAgeInYears(totalElapsed / Mathf.Max(10f, secondsPerDay * daysPerYear));
            }
            RefreshLabel();
        }

        private void LateUpdate()
        {
            if (dayNight == null) return;

            // DayNight sets the sunlight first; seasonal dormancy then modifies the
            // effective photosynthesis rate without changing TreeGameState.cs.
            if (resources != null)
            {
                float rate = 1f;
                if (CurrentSeason == TreeSeason.Spring)
                    rate = Mathf.Lerp(.45f, 1f, Mathf.Clamp01(elapsedInSeason / Mathf.Max(1f, secondsPerDay * nightsPerSeason)));
                else if (CurrentSeason == TreeSeason.Autumn)
                    rate = Mathf.Lerp(.8f, 0f, Mathf.Clamp01(elapsedInSeason / Mathf.Max(1f, secondsPerDay * nightsPerSeason)));
                else if (CurrentSeason == TreeSeason.Winter)
                    rate = 0f;
                resources.SetSunlightFactor(dayNight.Sunlight01 * rate);
            }

            if (seasonTintOverlay != null && dayNight.nightOverlay != null)
            {
                seasonTintOverlay.transform.position = dayNight.nightOverlay.transform.position;
                seasonTintOverlay.transform.localScale = dayNight.nightOverlay.transform.localScale;
            }
        }

        private void AdvanceSeason()
        {
            NightsThisSeason = 0;
            elapsedInSeason = 0f;
            if (CurrentSeason == TreeSeason.Winter)
            {
                CurrentSeason = TreeSeason.Spring;
                Year++;
            }
            else CurrentSeason++;
            SetSeasonVisualsAndHours(initial: false);
        }

        private void SetSeasonVisualsAndHours(bool initial)
        {
            Vector2 times = springDaylight;
            switch (CurrentSeason)
            {
                case TreeSeason.Summer: times = summerDaylight; break;
                case TreeSeason.Autumn: times = autumnDaylight; break;
                case TreeSeason.Winter: times = winterDaylight; break;
            }
            targetSunrise = times.x;
            targetSunset = times.y;
            if (dayNight != null)
            {
                if (initial) dayNight.SetDaylightHours(targetSunrise, targetSunset);
                Color night = originalNightTint;
                if (CurrentSeason == TreeSeason.Winter)
                    night = new Color(.012f,.025f,.10f, .90f);
                else if (CurrentSeason == TreeSeason.Autumn)
                    night = new Color(.045f,.037f,.09f, .83f);
                dayNight.nightTint = night;
            }

            if (seasonTintOverlay != null)
            {
                switch (CurrentSeason)
                {
                    case TreeSeason.Spring: seasonTintOverlay.color = new Color(.5f,.76f,.49f,.03f); break;
                    case TreeSeason.Summer: seasonTintOverlay.color = new Color(1f,1f,1f,0f); break;
                    case TreeSeason.Autumn: seasonTintOverlay.color = new Color(.42f,.22f,.12f,.10f); break;
                    case TreeSeason.Winter: seasonTintOverlay.color = new Color(.15f,.25f,.42f,.29f); break;
                }
            }

            if (!initial && bonsai != null)
            {
                if (CurrentSeason == TreeSeason.Autumn)
                    bonsai.BeginAutumnLeafFall(Mathf.Max(1f, autumnLeafFallSeconds));
                else if (CurrentSeason == TreeSeason.Winter)
                    bonsai.DecomposeLandedLeaves(Mathf.Max(5f, secondsPerDay));
                else if (CurrentSeason == TreeSeason.Spring)
                    bonsai.BeginSpringLeafRegrowth(Mathf.Max(1f, springRegrowthSeconds));
            }
            Debug.Log($"Tree Jam: {CurrentSeason}, Year {Year} (after {NightsSinceStart} nights).", this);
        }

        private void RefreshLabel()
        {
            if (seasonLabel == null) return;
            seasonLabel.text = $"{CurrentSeason.ToString().ToUpperInvariant()}  /  YEAR {Year}    |    NIGHT {NightsThisSeason}/{Mathf.Max(1,nightsPerSeason)}";
        }

        private void OnDisable()
        {
            if (dayNight != null) dayNight.nightTint = originalNightTint;
            if (seasonTintOverlay != null)
            {
                Color c = seasonTintOverlay.color;
                c.a = 0f;
                seasonTintOverlay.color = c;
            }
            if (resources != null) resources.SetSunlightFactor(1f);
        }
    }
}
