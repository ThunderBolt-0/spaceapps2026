using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MissionTrainer
{
    /// <summary>One popup per scene that explains a science word in kid words.</summary>
    public class GlossaryPopup : MonoBehaviour
    {
        static GlossaryPopup instance;

        public GameObject panel;
        public TMP_Text wordText;
        public TMP_Text meaningText;
        public Button closeButton;

        void Awake()
        {
            instance = this;
            if (panel) panel.SetActive(false);
            if (closeButton) closeButton.onClick.AddListener(() => panel.SetActive(false));
        }

        public static void Show(string key)
        {
            if (instance == null) { Debug.LogWarning("No GlossaryPopup in scene"); return; }
            var e = ContentDb.Glossary(key);
            instance.wordText.text = e != null ? e.word : key;
            instance.meaningText.text = e != null ? e.meaning : "(No meaning written yet.)";
            instance.panel.SetActive(true);
        }
    }
}
