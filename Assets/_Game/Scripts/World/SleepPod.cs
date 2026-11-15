using UnityEngine;

namespace MissionTrainer
{
    /// <summary>Bed in the habitat: sleep until the next sol (same as End the day). Plays the night cutscene.</summary>
    public class SleepPod : MonoBehaviour, IInteractable
    {
        public MissionRunner runner;
        void Awake() { if (!runner) runner = FindAnyObjectByType<MissionRunner>(); }

        public string Prompt =>
            runner && runner.State != null && !runner.IsOver && runner.ActiveCard == null ? "Go to sleep (End the day)" : null;

        public void Interact()
        {
            if (PlaytestUI.Current) PlaytestUI.Current.Sleep();
            else runner.NextSol();
        }
    }
}
