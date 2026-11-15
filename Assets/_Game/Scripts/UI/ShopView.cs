using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MissionTrainer
{
    /// <summary>Freeplay upgrade shop. Each upgrade is bought once and helps every later Freeplay mission.</summary>
    public class ShopView : MonoBehaviour
    {
        public TMP_Text creditsText;
        public Transform itemParent;
        [Tooltip("Button prefab with a TMP_Text child.")]
        public Button itemPrefab;

        readonly List<Button> spawned = new List<Button>();

        void OnEnable() => Build();

        void Build()
        {
            foreach (var b in spawned) Destroy(b.gameObject);
            spawned.Clear();
            if (creditsText) creditsText.text = "Credits: " + SaveSystem.Credits;

            foreach (var item in ContentDb.ShopItems)
            {
                var it = item;
                bool owned = SaveSystem.HasUpgrade(it.id);
                var btn = Instantiate(itemPrefab, itemParent);
                spawned.Add(btn);
                btn.GetComponentInChildren<TMP_Text>().text =
                    $"{it.label}\n<size=70%>{it.description}</size>\n" + (owned ? "Owned" : $"{it.cost} credits");
                btn.interactable = !owned && SaveSystem.Credits >= it.cost;
                btn.onClick.AddListener(() => Buy(it));
            }
        }

        void Buy(ShopItem item)
        {
            if (SaveSystem.Credits < item.cost || SaveSystem.HasUpgrade(item.id)) return;
            SaveSystem.Credits -= item.cost;
            SaveSystem.AddUpgrade(item.id);
            Build();
        }
    }
}
