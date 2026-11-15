using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MissionTrainer
{
    /// <summary>Small chip: "Real · Live" or "Real · Archived 2018-06-10". Shown only for real data.</summary>
    public class DataBadge : MonoBehaviour
    {
        public TMP_Text label;
        public Image background;
        public Color realColor = new Color(0.20f, 0.65f, 0.35f);

        public void Set(Source s)
        {
            bool real = s != null && s.kind != "simulated";
            gameObject.SetActive(real);
            if (!real) return;
            if (label) label.text = s.BadgeText();
            if (background) background.color = realColor;
        }
    }
}
