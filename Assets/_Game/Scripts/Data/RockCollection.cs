using System;
using System.Linq;
using UnityEngine;

namespace MissionTrainer
{
    [Serializable]
    public class RockInfo
    {
        public string id, name, rarity, color, foundBy, when, where, fact, why;
        public Color Tint => ColorUtility.TryParseHtmlString(color, out var c) ? c : Color.gray;
    }

    [Serializable] public class RockList { public RockInfo[] rocks; }

    /// <summary>
    /// The rock collection: every rock sample you pick up rolls a rarity, then a real Mars rock of that rarity.
    /// New kinds are added to your collection (saved with PlayerPrefs) and shown on the Rock Collection page.
    /// All rocks are real finds by Mars rovers (Resources/Data/rocks.json).
    /// </summary>
    public static class RockCollection
    {
        public static readonly string[] Rarities = { "Common", "Uncommon", "Rare", "Epic", "Legendary" };
        static readonly int[] Weights = { 55, 25, 12, 6, 2 };
        public static readonly int[] ResearchBonus = { 0, 5, 10, 20, 40 };

        static RockInfo[] all;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() => all = null;

        public static RockInfo[] All => all ??= ContentDb.LoadJson<RockList>("Data/rocks")?.rocks ?? new RockInfo[0];

        public static int RarityIndex(string r) => Mathf.Max(0, Array.IndexOf(Rarities, r));

        public static Color RarityColor(string r) => RarityIndex(r) switch
        {
            0 => new Color(0.78f, 0.78f, 0.80f),
            1 => new Color(0.40f, 0.85f, 0.45f),
            2 => new Color(0.35f, 0.65f, 1.00f),
            3 => new Color(0.75f, 0.45f, 1.00f),
            _ => new Color(1.00f, 0.72f, 0.20f),
        };

        public static int Count(string id) => PlayerPrefs.GetInt("rock_" + id, 0);
        public static bool Has(string id) => Count(id) > 0;
        public static int UniqueFound => All.Count(r => Has(r.id));

        /// <summary>Roll a rock for one collected sample, add it to the collection. isNew = first time ever.</summary>
        public static RockInfo Roll(out bool isNew)
        {
            isNew = false;
            if (All.Length == 0) return null;
            int total = Weights.Sum(), pick = UnityEngine.Random.Range(0, total), tier = 0;
            for (; tier < Weights.Length; tier++) { if (pick < Weights[tier]) break; pick -= Weights[tier]; }
            tier = Mathf.Min(tier, Rarities.Length - 1);
            var pool = All.Where(r => RarityIndex(r.rarity) == tier).ToArray();
            if (pool.Length == 0) pool = All;
            var rock = pool[UnityEngine.Random.Range(0, pool.Length)];
            isNew = !Has(rock.id);
            PlayerPrefs.SetInt("rock_" + rock.id, Count(rock.id) + 1);
            PlayerPrefs.Save();
            return rock;
        }
    }
}
