using UnityEngine;

namespace MissionTrainer
{
    /// <summary>
    /// A buried ice deposit at the "pinged location" far from the base (Mission Control's map).
    /// Daily missions use deposit 0; scripted missions (First Sols, The Storm) use three (index 0-2).
    /// Walking there and drilling costs air (high risk), but gives research and becomes oxygen (high reward).
    /// Created at runtime by GameFlow (copied from a rock sample site), so the scene does not need a rebuild.
    /// </summary>
    public class IceSite : MonoBehaviour, IInteractable
    {
        public MissionRunner runner;
        public GameObject visuals;
        public int index;

        void Awake() { if (!runner) runner = FindAnyObjectByType<MissionRunner>(); }

        void Update()
        {
            bool show = runner && runner.IceAvailable(index);
            if (visuals && visuals.activeSelf != show) visuals.SetActive(show);

            // Arriving at the pinged location (scripted missions)
            if (index == 0 && runner && runner.State != null && runner.State.iceAccepted && !runner.State.atPing)
            {
                var cam = Camera.main;
                if (cam && (cam.transform.position - transform.position).sqrMagnitude < 14f * 14f) runner.ArrivedAtPing();
            }
        }

        public bool Active => visuals && visuals.activeSelf;

        public string Prompt
        {
            get
            {
                if (!Active || runner.ActiveCard != null) return null;
                return $"Drill for ice  (Air -{runner.IceCost:0}%  ·  +{runner.IceResearch} research)";
            }
        }

        public void Interact()
        {
            if (Active) runner.DrillIce(index);
        }
    }
}
