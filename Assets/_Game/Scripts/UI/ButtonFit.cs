using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace MissionTrainer
{
    /// <summary>
    /// Keeps a button at least as wide as its longest word, so a label never breaks inside a word
    /// ("Sen / d"). Long sentences still wrap between words. Re-checks only when the text changes.
    /// </summary>
    public class ButtonFit : MonoBehaviour
    {
        public Text label;
        public float padding = 22f;
        LayoutElement le;
        string last;
        static readonly Regex Tags = new Regex("<[^>]+>");

        void OnEnable() => last = null;

        void LateUpdate()
        {
            if (!label || label.text == last) return;
            last = label.text;
            if (!le) le = GetComponent<LayoutElement>();
            if (!le) le = gameObject.AddComponent<LayoutElement>();   // (no ?? on Unity objects)
            var settings = label.GetGenerationSettings(Vector2.zero);
            settings.horizontalOverflow = HorizontalWrapMode.Overflow;
            var gen = label.cachedTextGeneratorForLayout;
            float widest = 0;
            foreach (var word in Tags.Replace(last, "").Split(' ', '\n'))
                if (word.Length > 0) widest = Mathf.Max(widest, gen.GetPreferredWidth(word, settings) / label.pixelsPerUnit);
            le.minWidth = widest + padding;
        }
    }
}
