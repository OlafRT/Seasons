using UnityEngine;
using UnityEngine.UI;

namespace TreeJam
{
    /// <summary>Updates a normal editable Unity UI Text element with the simulated clock.</summary>
    public sealed class TreeJamDayNightClockUI : MonoBehaviour
    {
        public TreeJamDayNightCycle cycle;
        public Text label;
        private void Awake()
        {
            if (cycle == null) cycle = FindFirstObjectByType<TreeJamDayNightCycle>();
            if (label == null) label = GetComponent<Text>();
        }
        private void LateUpdate()
        {
            if (label == null || cycle == null) return;
            int minutes = Mathf.FloorToInt(cycle.CurrentHour * 60f) % (24 * 60);
            int hh = minutes / 60;
            int mm = minutes % 60;
            label.text = $"DAY {cycle.DayNumber:00}  |  {hh:00}:{mm:00}\n" +
                (cycle.IsDaytime ? $"SUNLIGHT  {cycle.Sunlight01 * 100f:0}%" : "NIGHT  •  RESTING");
        }
    }
}
