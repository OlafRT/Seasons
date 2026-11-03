using UnityEngine;
using UnityEngine.UI;

namespace TreeJam
{
    /// <summary>
    /// Keeps the existing editable bottom-left UI subtitle synchronized with
    /// the actual season controller. No changes to TreeJamUI or SeasonCycle needed.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10000)]
    public sealed class TreeJamSeasonHeaderSync : MonoBehaviour
    {
        public TreeJamSeasonCycle seasons;
        public Text label;

        private void Awake()
        {
            if (seasons == null) seasons = FindFirstObjectByType<TreeJamSeasonCycle>();
            if (label == null) label = GetComponent<Text>();
        }

        private void LateUpdate()
        {
            if (label == null || seasons == null) return;
            string season = seasons.CurrentSeason.ToString().ToUpperInvariant();
            string year = YearAsWords(seasons.Year);
            string caption = season + " / YEAR " + year;
            if (label.text != caption) label.text = caption;
        }

        private static string YearAsWords(int year)
        {
            switch (year)
            {
                case 1: return "ONE";
                case 2: return "TWO";
                case 3: return "THREE";
                case 4: return "FOUR";
                case 5: return "FIVE";
                case 6: return "SIX";
                case 7: return "SEVEN";
                case 8: return "EIGHT";
                case 9: return "NINE";
                case 10: return "TEN";
                default: return year.ToString();
            }
        }
    }
}
