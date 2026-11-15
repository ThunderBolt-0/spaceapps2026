using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MissionTrainer
{
    /// <summary>The 3 meters + food + sol + sky info + Next sol button. Display only.</summary>
    public class HudView : MonoBehaviour
    {
        public MissionRunner runner;

        [Header("Meters (Image type = Filled)")]
        public Image powerFill;
        public Image oxygenFill;
        public Image doseFill;
        public TMP_Text powerText, oxygenText, doseText;

        [Header("Counters")]
        public TMP_Text foodText, solText, titleText, skyText, powerTrendText;
        public DataBadge skyBadge;
        public Button nextSolButton;

        void OnEnable() { if (runner) runner.StateChanged += Refresh; }
        void OnDisable() { if (runner) runner.StateChanged -= Refresh; }

        void Start()
        {
            if (nextSolButton) nextSolButton.onClick.AddListener(runner.NextSol);
        }

        void Refresh(HabitatState s)
        {
            SetMeter(powerFill, powerText, s.power, "Power");
            SetMeter(oxygenFill, oxygenText, s.oxygen, "Oxygen");
            SetMeter(doseFill, doseText, s.dose, "Radiation");

            if (foodText) foodText.text = "Food: " + s.food;
            if (solText) solText.text = "Sol " + s.sol;
            if (titleText) titleText.text = runner.Mission.title;

            var env = runner.Mission.EnvForSol(Mathf.Max(1, s.sol));
            if (skyText) skyText.text = GlossaryText.Format($"[[tau|Dust]]: {env.tau:0.0}   Sun: {env.sunlight * 100:0}%");
            if (skyBadge) skyBadge.Set(env.source);

            if (powerTrendText)
            {
                var next = runner.Mission.EnvForSol(s.sol + 1);
                float d = SimEngine.NetPowerPreview(s, next, runner.Config, runner.Level);
                powerTrendText.text = (d >= 0 ? "+" : "") + d.ToString("0") + "% next sol";
            }

            if (nextSolButton) nextSolButton.interactable = !runner.IsOver && runner.ActiveCard == null;
        }

        static void SetMeter(Image fill, TMP_Text text, float value, string label)
        {
            if (fill) fill.fillAmount = value / 100f;
            if (text) text.text = $"{label} {value:0}%";
        }
    }
}
