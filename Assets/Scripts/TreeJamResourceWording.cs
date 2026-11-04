using UnityEngine;
using UnityEngine.UI;

namespace TreeJam
{
    /// <summary>
    /// Dynamic compatibility labels. Existing market code still calls its stored
    /// resource "sunlight"; this only changes player-facing copy to "energy".
    /// </summary>
    [DefaultExecutionOrder(5200)]
    [DisallowMultipleComponent]
    public sealed class TreeJamResourceWording : MonoBehaviour
    {
        public TreeJamFungiMarket market;
        public TreeJamUI ui;
        private void Start()
        {
            if(market == null) market=FindFirstObjectByType<TreeJamFungiMarket>();
            if(ui == null) ui=FindFirstObjectByType<TreeJamUI>();
        }
        private void LateUpdate()
        {
            if(market != null && market.statusText != null && market.resources != null &&
               market.statusText.gameObject.activeInHierarchy)
            {
                market.statusText.text = $"Stored: {market.resources.Energy:0} energy / " +
                                         $"{market.resources.Nutrients:0} nutrients";
            }
            if(ui != null && ui.toastText != null && ui.toastText.text.Contains("Fungi exchanged"))
                ui.toastText.text = ui.toastText.text.Replace("sunlight", "energy").Replace("Sunlight", "Energy");
        }
    }
}
