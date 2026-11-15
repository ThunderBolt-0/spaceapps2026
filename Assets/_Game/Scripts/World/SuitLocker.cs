using UnityEngine;

namespace MissionTrainer
{
    /// <summary>
    /// Suit stand in the habitat. Click it to put on your spacesuit (needed to go out through the airlock),
    /// or to take it off. The suit on the stand disappears while you wear it.
    /// Customizing the suit is on the home screen ("My suit").
    /// </summary>
    public class SuitLocker : MonoBehaviour, IInteractable
    {
        public MissionRunner runner;
        public AstronautSuit display;
        void Awake() { if (!runner) runner = FindAnyObjectByType<MissionRunner>(); }

        void Update()
        {
            if (!display) return;
            bool show = runner == null || runner.State == null || runner.IsOver || !runner.State.suitOn;
            if (display.gameObject.activeSelf != show) display.gameObject.SetActive(show);
        }

        public string Prompt
        {
            get
            {
                if (!runner || runner.State == null || runner.IsOver || runner.ActiveCard != null) return null;
                return runner.State.suitOn ? "Take off your spacesuit" : "Put on your spacesuit";
            }
        }

        public void Interact()
        {
            if (runner.State == null || runner.IsOver) return;
            var ui = PlaytestUI.Current;
            if (ui) ui.PlaySuitScene(!runner.State.suitOn, null);
            else runner.SetSuit(!runner.State.suitOn);
        }
    }
}
