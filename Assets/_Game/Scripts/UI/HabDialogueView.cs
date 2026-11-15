using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MissionTrainer
{
    /// <summary>HAB's speech box. Lines queue up; tap Next to read the next one.</summary>
    public class HabDialogueView : MonoBehaviour
    {
        public MissionRunner runner;
        public GameObject box;
        public TMP_Text lineText;
        public Button nextButton;

        readonly Queue<string> lines = new Queue<string>();

        void Awake() { if (box) box.SetActive(false); }
        void OnEnable() { if (runner) runner.HabSays += Enqueue; }
        void OnDisable() { if (runner) runner.HabSays -= Enqueue; }
        void Start() { if (nextButton) nextButton.onClick.AddListener(ShowNext); }

        void Enqueue(string line)
        {
            lines.Enqueue(line);
            if (!box.activeSelf) ShowNext();
        }

        void ShowNext()
        {
            if (lines.Count == 0) { box.SetActive(false); return; }
            box.SetActive(true);
            lineText.text = GlossaryText.Format(lines.Dequeue());
        }
    }
}
