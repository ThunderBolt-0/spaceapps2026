using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MissionTrainer
{
    /// <summary>Small chip: "Real · Live", "Real · Archived 2018-06-10" or "Simulated".</summary>
    public class DataBadge : MonoBehaviour
    {
        public TMP_Text label;
        public Image background;
        public Color realColor = new Color(0.20f, 0.65f, 0.35f);
        public Color simulatedColor = new Color(0.45f, 0.45f, 0.55f);

        public void Set(Source s)
        {
            if (s == null) s = Source.Simulated("");
            if (label) label.text = s.BadgeText();
            if (background) background.color = s.kind == "simulated" ? simulatedColor : realColor;
        }
    }
}
