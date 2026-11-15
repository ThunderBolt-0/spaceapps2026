using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MissionTrainer
{
    /// <summary>Lists unlocked cosmetics; tap one to wear it. Looks only, never gameplay.</summary>
    public class SuitView : MonoBehaviour
    {
        public Transform entryParent;
        [Tooltip("Button prefab with a TMP_Text child.")]
        public Button entryPrefab;
        public AstronautSuit preview;

        readonly List<Button> spawned = new List<Button>();

        void OnEnable() => Build();

        void Build()
        {
            foreach (var b in spawned) Destroy(b.gameObject);
            spawned.Clear();

            foreach (var c in AchievementManager.Catalog)
            {
                var cos = c;
                bool unlocked = AchievementManager.IsUnlocked(cos.id);
                bool worn = SaveSystem.GetEquipped(cos.slot, "") == cos.id;
                var btn = Instantiate(entryPrefab, entryParent);
                spawned.Add(btn);
                btn.GetComponentInChildren<TMP_Text>().text =
                    unlocked ? cos.label + (worn ? "  (wearing)" : "") : $"Locked\n<size=70%>{cos.howToUnlock}</size>";
                btn.interactable = unlocked;
                btn.onClick.AddListener(() =>
                {
                    SaveSystem.SetEquipped(cos.slot, cos.id);
                    if (preview) preview.Apply();
                    Build();
                });
            }
        }
    }
}
