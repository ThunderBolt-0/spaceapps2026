using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MissionTrainer
{
    /// <summary>Shows a decision card. Every option shows its cost before the player picks.</summary>
    public class DecisionCardView : MonoBehaviour
    {
        public MissionRunner runner;
        public GameObject panel;
        public TMP_Text promptText;
        public Transform optionParent;
        [Tooltip("Button prefab with a TMP_Text child.")]
        public Button optionPrefab;

        readonly List<Button> spawned = new List<Button>();

        void Awake() { if (panel) panel.SetActive(false); }
        void OnEnable() { if (runner) runner.CardShown += Show; }
        void OnDisable() { if (runner) runner.CardShown -= Show; }

        void Show(Card card)
        {
            foreach (var b in spawned) Destroy(b.gameObject);
            spawned.Clear();

            panel.SetActive(true);
            promptText.text = GlossaryText.Format(card.prompt);
            bool hints = runner.Level.hintsOn;

            for (int i = 0; i < card.options.Length; i++)
            {
                var opt = card.options[i];
                var btn = Instantiate(optionPrefab, optionParent);
                spawned.Add(btn);

                string hint = hints && opt.HasTag("priority") ? "  <color=#FFD54F>★ HAB tip</color>" : "";
                string locked = opt.needsRadio && !runner.State.IsOn(SystemIds.Radio) ? "\n<size=70%>Needs the radio on</size>" : "";
                btn.GetComponentInChildren<TMP_Text>().text =
                    $"{opt.label}{hint}\n<size=70%>{opt.Describe(runner.Config)}</size>{locked}";
                btn.interactable = runner.CanChoose(opt);

                int index = i;
                btn.onClick.AddListener(() =>
                {
                    panel.SetActive(false);   // hide first: Choose may show the next card
                    runner.Choose(index);
                });
            }
        }
    }
}
