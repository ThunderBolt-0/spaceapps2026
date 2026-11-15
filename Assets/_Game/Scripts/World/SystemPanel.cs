using UnityEngine;
using UnityEngine.UI;

namespace MissionTrainer
{
    /// <summary>A wall panel in the habitat. Walk up, look at it, press E to switch the system.</summary>
    public class SystemPanel : MonoBehaviour, IInteractable
    {
        public MissionRunner runner;
        public string systemId;           // SystemIds value
        public Renderer indicator;        // glows when on
        public Text label;                // world-space label on the panel
        public GameObject[] showWhenOn;   // e.g. grow lights, glowing heater coil
        public Color onColor = new Color(0.2f, 1f, 0.4f);
        public Color offColor = new Color(0.25f, 0.25f, 0.3f);

        Material indicatorMat;

        void Awake()
        {
            if (!runner) runner = FindAnyObjectByType<MissionRunner>();
            if (indicator) indicatorMat = indicator.material; // instance, so panels glow separately
        }

        void Start()
        {
            // Proper name before any mission starts (otherwise the label shows the raw id).
            var def = GameManager.Instance.Config.System(systemId);
            if (label && def != null) label.text = def.label.ToUpper();
        }

        void OnEnable() { if (runner) runner.StateChanged += Refresh; }
        void OnDisable() { if (runner) runner.StateChanged -= Refresh; }

        SystemDef Def => runner && runner.Config != null ? runner.Config.System(systemId) : null;

        public string Prompt
        {
            get
            {
                if (!runner || runner.State == null || runner.IsOver || runner.ActiveCard != null) return null;
                var d = Def;
                if (d == null) return null;
                float cost = d.powerPerSol * runner.Level.drainMultiplier;
                return runner.State.IsOn(systemId)
                    ? $"Turn OFF {d.label}  (saves {cost:0}% power per sol)"
                    : $"Turn ON {d.label}  (costs {cost:0}% power per sol)";
            }
        }

        public void Interact() => runner.SetSystem(systemId, !runner.State.IsOn(systemId));

        void Refresh(HabitatState s)
        {
            bool on = s.IsOn(systemId);
            if (indicatorMat)
            {
                indicatorMat.color = on ? onColor : offColor;
                indicatorMat.SetColor("_EmissionColor", on ? onColor * 2f : Color.black);
            }
            if (showWhenOn != null) foreach (var go in showWhenOn) if (go) go.SetActive(on);
            var d = Def;
            if (label && d != null) label.text = $"{d.label.ToUpper()}\n<color={(on ? "#6CFF8F" : "#9A9AA8")}>{(on ? "ON" : "OFF")}</color>";
        }
    }
}
