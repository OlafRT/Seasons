using UnityEngine;

namespace TreeJam
{
    /// <summary>
    /// Snowy mountain sprite is ALREADY stacked above the normal mountains.
    /// Only its alpha changes. No crossfade/repositioning/reparenting of artwork.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TreeJamWinterMountainOverlay : MonoBehaviour
    {
        public TreeJamSeasonCycle seasons;
        public SpriteRenderer winterMountains;
        [Range(0f,1f)] public float winterOpacity = 1f;
        [Min(.1f)] public float fadeInSeconds = 22f;
        [Min(.1f)] public float fadeOutSeconds = 24f;
        [Tooltip("Optional early hint of snowfall during autumn, normally leave at zero.")]
        [Range(0f, .4f)] public float autumnOpacity = 0f;
        private Color baseColor;
        private float amount;

        private void Awake()
        {
            if (seasons == null) seasons = FindFirstObjectByType<TreeJamSeasonCycle>();
            if (winterMountains != null) baseColor = winterMountains.color;
        }
        private void OnEnable()
        {
            if (winterMountains != null) baseColor = winterMountains.color;
        }
        private void LateUpdate()
        {
            if (winterMountains == null) return;
            TreeSeason season = seasons != null ? seasons.CurrentSeason : TreeSeason.Summer;
            float target = season == TreeSeason.Winter ? winterOpacity :
                           season == TreeSeason.Autumn ? autumnOpacity : 0f;
            float seconds = target > amount ? fadeInSeconds : fadeOutSeconds;
            amount = Mathf.MoveTowards(amount, target, Time.deltaTime / Mathf.Max(.1f, seconds));
            Color color = baseColor;
            color.a = amount;
            winterMountains.color = color;
        }
    }
}
