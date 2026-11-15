using UnityEngine;

namespace MissionTrainer
{
    /// <summary>A rock sample on the surface. Click to pick it up (costs oxygen). A fresh rock is found here every morning.</summary>
    public class SampleSite : MonoBehaviour, IInteractable
    {
        public MissionRunner runner;
        public GameObject rock;
        public GameObject marker;
        bool taken;

        void Awake() { if (!runner) runner = FindAnyObjectByType<MissionRunner>(); }
        void OnEnable() { if (runner) { runner.MissionStarted += ResetSite; runner.DayStarted += NewDay; } }
        void OnDisable() { if (runner) { runner.MissionStarted -= ResetSite; runner.DayStarted -= NewDay; } }

        void NewDay(int day) => ResetSite();

        public bool Available => !taken;

        public string Prompt
        {
            get
            {
                if (taken || !runner || runner.State == null || runner.IsOver || runner.ActiveCard != null) return null;
                return $"Collect rock sample  (-{runner.Config.balance.sampleOxygenCost:0}% oxygen)";
            }
        }

        public void Interact()
        {
            if (!runner.TryCollectSample()) return;
            taken = true;
            if (rock) rock.SetActive(false);
            if (marker) marker.SetActive(false);
        }

        void ResetSite()
        {
            taken = false;
            if (rock) rock.SetActive(true);
            if (marker) marker.SetActive(true);
        }
    }
}
