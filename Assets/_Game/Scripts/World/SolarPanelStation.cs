using UnityEngine;

namespace MissionTrainer
{
    /// <summary>
    /// A solar panel outside the habitat. Dust builds up every sol (faster in dusty skies) and cuts power.
    /// Walk out on an EVA and press E to clean it: costs oxygen, restores full panel power.
    /// Added automatically to every panel in the "SolarField" by GameFlow.
    /// </summary>
    public class SolarPanelStation : MonoBehaviour, IInteractable
    {
        public MissionRunner runner;
        void Awake() { if (!runner) runner = FindAnyObjectByType<MissionRunner>(); }

        public string Prompt
        {
            get
            {
                if (!runner || runner.State == null || runner.IsOver || runner.ActiveCard != null) return null;
                float dust = runner.State.panelDust * 100;
                if (dust < 5) return "Solar panels are clean";
                return $"Clean the solar panels ({dust:0}% dusty · -{Mathf.Max(1, runner.Config.balance.cleanOxygenCost):0}% oxygen)";
            }
        }

        public void Interact() => runner.CleanPanels();
    }
}
