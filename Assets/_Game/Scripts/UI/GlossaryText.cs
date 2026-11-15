using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MissionTrainer
{
    /// <summary>
    /// Add to any TMP text that may contain glossary words (turn on Raycast Target).
    /// Write [[tau]] or [[tau|dusty sky]] in JSON; it becomes a tappable underlined word.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class GlossaryText : MonoBehaviour, IPointerClickHandler
    {
        static readonly Regex Tag = new Regex(@"\[\[(\w+)(?:\|([^\]]+))?\]\]");

        public static string Format(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            return Tag.Replace(raw, m =>
            {
                string key = m.Groups[1].Value;
                string shown = m.Groups[2].Success ? m.Groups[2].Value : (ContentDb.Glossary(key)?.word ?? key);
                return $"<link=\"{key}\"><color=#4FC3F7><u>{shown}</u></color></link>";
            });
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            var text = GetComponent<TMP_Text>();
            int i = TMP_TextUtilities.FindIntersectingLink(text, eventData.position, eventData.pressEventCamera);
            if (i >= 0) GlossaryPopup.Show(text.textInfo.linkInfo[i].GetLinkID());
        }
    }
}
