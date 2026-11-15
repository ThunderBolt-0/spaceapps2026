using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MissionTrainer
{
    /// <summary>Five toggles: life support, heater, radio, greenhouse, science lab. Each shows its power cost.</summary>
    public class SystemTogglePanel : MonoBehaviour
    {
        [Serializable]
        public class Entry
        {
            public string systemId;   // use SystemIds values, e.g. "heater"
            public Toggle toggle;
            public TMP_Text label;
        }

        public MissionRunner runner;
        public Entry[] entries;

        void OnEnable() { if (runner) runner.StateChanged += Refresh; }
        void OnDisable() { if (runner) runner.StateChanged -= Refresh; }

        void Start()
        {
            var cfg = GameManager.Instance.Config;
            foreach (var e in entries)
            {
                var entry = e;
                var def = cfg.System(entry.systemId);
                if (entry.label && def != null) entry.label.text = $"{def.label}\n<size=70%>-{def.powerPerSol:0}% power / sol</size>";
                entry.toggle.onValueChanged.AddListener(on => runner.SetSystem(entry.systemId, on));
            }
        }

        void Refresh(HabitatState s)
        {
            foreach (var e in entries)
            {
                e.toggle.SetIsOnWithoutNotify(s.IsOn(e.systemId));
                e.toggle.interactable = !runner.IsOver && runner.ActiveCard == null;
            }
        }
    }
}
