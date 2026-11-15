using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MissionTrainer
{
    /// <summary>
    /// End screen: stars + breakdown, Your choices / What the real team did / What to try next time,
    /// one Real science card, and sources.
    /// </summary>
    public class DebriefView : MonoBehaviour
    {
        public MissionRunner runner;
        public GameObject panel;
        public TMP_Text titleText, reasonText, starsText, breakdownText;
        public TMP_Text choicesText, realTeamText, tryNextText;
        public TMP_Text realScienceTitle, realScienceText;
        public TMP_Text sourcesText, rewardsText;
        public Button retryButton, menuButton;

        void Awake() { if (panel) panel.SetActive(false); }
        void OnEnable() { if (runner) runner.MissionEnded += Show; }
        void OnDisable() { if (runner) runner.MissionEnded -= Show; }

        void Start()
        {
            if (retryButton) retryButton.onClick.AddListener(() => GameManager.Instance.Retry());
            if (menuButton) menuButton.onClick.AddListener(() => GameManager.Instance.BackToMenu());
        }

        void Show(MissionResult r)
        {
            panel.SetActive(true);
            var m = runner.Mission;
            var b = r.breakdown;

            Set(titleText, r.success ? "Mission complete!" : "Safe abort");
            Set(reasonText, r.reason);
            Set(starsText, new string('★', r.stars) + new string('☆', 3 - r.stars));
            Set(breakdownText,
                $"Safety margin {b.safety}/40\nSystems priority {b.priority}/25\nProcedure {b.procedure}/15\n" +
                $"Safe trips {b.shielding}/10\nScience {b.science}/10");

            Set(choicesText, r.finalState.choiceLog.Count == 0 ? "You didn't face any big choices." : string.Join("\n", r.finalState.choiceLog));

            var d = m.debrief;
            Set(realTeamText, d?.realTeam);
            Set(tryNextText, d?.tryNext);
            Set(realScienceTitle, d?.realScience?.title);
            Set(realScienceText, d?.realScience?.text);

            if (sourcesText && d?.sources != null)
                sourcesText.text = "Sources: " + string.Join(", ",
                    d.sources.Select(k => ContentDb.Source(k)?.name ?? k));

            string rewards = "";
            if (r.creditsEarned > 0) rewards += $"+{r.creditsEarned} credits\n";
            foreach (var u in r.newUnlocks) rewards += "New: " + u + "\n";
            Set(rewardsText, rewards);
        }

        static void Set(TMP_Text t, string s) { if (t) t.text = GlossaryText.Format(s ?? ""); }
    }
}
