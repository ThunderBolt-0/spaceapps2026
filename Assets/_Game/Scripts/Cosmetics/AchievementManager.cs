using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MissionTrainer
{
    public class Cosmetic
    {
        public string id, label, slot, howToUnlock;   // slot: trim | visor | patch | stripes
        public Color color;
        public float metallic, smoothness;           // visors
        public bool unlockedByDefault;
        public string rarity = "Common";             // Common | Rare | Epic | Legendary (how special it is)
        public string icon = "star";                 // for generated patches
    }

    /// <summary>
    /// Suit cosmetics and the achievements that unlock them. Looks only: nothing here changes gameplay.
    /// Unlocks and what you're wearing are saved with PlayerPrefs (SaveSystem).
    /// </summary>
    public static class AchievementManager
    {
        public static readonly string[] Slots = { "trim", "visor", "patch", "stripes" };

        public static readonly Cosmetic[] Catalog =
        {
            // Suit colors (the whole suit takes the color; it keeps its folds and shading)
            new Cosmetic { id = "trim_white", label = "Classic white suit", slot = "trim", color = new Color(0.97f, 0.97f, 0.97f), unlockedByDefault = true },
            new Cosmetic { id = "trim_blue", label = "Sky blue suit", slot = "trim", color = new Color(0.20f, 0.50f, 1f), unlockedByDefault = true },
            new Cosmetic { id = "trim_orange", label = "Mars orange suit", slot = "trim", color = new Color(0.95f, 0.50f, 0.15f), howToUnlock = "Finish any mission." },
            new Cosmetic { id = "trim_green", label = "Garden green suit", slot = "trim", color = new Color(0.25f, 0.70f, 0.35f), howToUnlock = "Complete Training." },
            new Cosmetic { id = "trim_silver", label = "Silver suit", slot = "trim", rarity = "Rare", color = new Color(0.78f, 0.80f, 0.84f), howToUnlock = "Earn 9 stars in Story." },
            new Cosmetic { id = "trim_sand", label = "Desert tan suit", slot = "trim", color = new Color(0.80f, 0.64f, 0.42f), howToUnlock = "Bring home 5 rock samples." },
            new Cosmetic { id = "trim_hematite", label = "Hematite grey suit", slot = "trim", rarity = "Rare", color = new Color(0.42f, 0.43f, 0.48f), howToUnlock = "Find hematite \"blueberries\" for your rock collection." },
            new Cosmetic { id = "trim_jarosite", label = "Jarosite gold suit", slot = "trim", rarity = "Rare", color = new Color(0.86f, 0.68f, 0.20f), howToUnlock = "Find jarosite for your rock collection." },
            new Cosmetic { id = "trim_ice", label = "Ice blue suit", slot = "trim", rarity = "Rare", color = new Color(0.50f, 0.85f, 1f), howToUnlock = "Drill 5 ice deposits." },
            new Cosmetic { id = "trim_meteor", label = "Meteorite iron suit", slot = "trim", rarity = "Epic", color = new Color(0.55f, 0.57f, 0.63f), howToUnlock = "Find an iron meteorite (Epic rock)." },
            new Cosmetic { id = "trim_leopard", label = "Leopard-spot red suit", slot = "trim", rarity = "Legendary", color = new Color(0.78f, 0.24f, 0.14f), howToUnlock = "Find the Legendary leopard-spot rock." },

            // Visors
            new Cosmetic { id = "visor_clear", label = "Smoke visor", slot = "visor", color = new Color(0.06f, 0.06f, 0.08f), metallic = 0.6f, smoothness = 0.97f, unlockedByDefault = true },
            new Cosmetic { id = "visor_gold", label = "Gold sun visor", slot = "visor", rarity = "Epic", color = new Color(0.85f, 0.64f, 0.22f), metallic = 1f, smoothness = 0.93f, howToUnlock = "Complete the last stage: Memory Glitch." },
            new Cosmetic { id = "visor_purple", label = "Aurora visor", slot = "visor", rarity = "Rare", color = new Color(0.42f, 0.22f, 0.68f), metallic = 0.75f, smoothness = 0.95f, howToUnlock = "Survive 5 sols in Freeplay." },
            new Cosmetic { id = "visor_chrome", label = "Mirror chrome visor", slot = "visor", rarity = "Epic", color = new Color(0.82f, 0.84f, 0.88f), metallic = 1f, smoothness = 0.99f, howToUnlock = "Find 6 kinds of rocks." },
            new Cosmetic { id = "visor_frost", label = "Frost visor", slot = "visor", rarity = "Rare", color = new Color(0.55f, 0.82f, 1f), metallic = 0.5f, smoothness = 0.97f, howToUnlock = "Survive the night in Stage 1: The Storm." },

            // Mission patches (decals on chest and shoulder)
            new Cosmetic { id = "patch_trainee", label = "Trainee patch", slot = "patch", color = new Color(0.3f, 0.45f, 0.9f), unlockedByDefault = true },
            new Cosmetic { id = "patch_ch1", label = "The Storm patch", slot = "patch", color = new Color(0.9f, 0.55f, 0.25f), howToUnlock = "Complete Chapter 1: The Storm." },
            new Cosmetic { id = "patch_ch2", label = "Cold Night patch", slot = "patch", color = new Color(0.6f, 0.8f, 1f), howToUnlock = "Complete Chapter 2: Cold Night." },
            new Cosmetic { id = "patch_ch3", label = "Stuck patch", slot = "patch", color = new Color(0.8f, 0.65f, 0.4f), howToUnlock = "Complete Chapter 3: Stuck." },
            new Cosmetic { id = "patch_ch4", label = "Silence patch", slot = "patch", color = new Color(0.3f, 0.8f, 0.8f), howToUnlock = "Complete Chapter 4: Silence." },
            new Cosmetic { id = "patch_ch5", label = "Final Stage patch", slot = "patch", color = new Color(0.95f, 0.75f, 0.2f), howToUnlock = "Complete the last stage: Memory Glitch." },
            new Cosmetic { id = "patch_sol10", label = "10-sol patch", slot = "patch", color = new Color(0.75f, 0.5f, 0.3f), howToUnlock = "Survive 10 sols in Freeplay." },
            new Cosmetic { id = "patch_sol25", label = "25-sol patch", slot = "patch", color = new Color(0.8f, 0.82f, 0.86f), howToUnlock = "Survive 25 sols in Freeplay." },
            new Cosmetic { id = "patch_sol50", label = "50-sol patch", slot = "patch", rarity = "Epic", color = new Color(0.98f, 0.8f, 0.3f), howToUnlock = "Survive 50 sols in Freeplay." },
            new Cosmetic { id = "patch_geologist", label = "Geologist patch", slot = "patch", rarity = "Rare", icon = "rock", color = new Color(0.72f, 0.5f, 0.32f), howToUnlock = "Find 5 kinds of rocks." },
            new Cosmetic { id = "patch_icehunter", label = "Ice Hunter patch", slot = "patch", rarity = "Rare", icon = "snow", color = new Color(0.35f, 0.7f, 0.95f), howToUnlock = "Drill 3 ice deposits." },
            new Cosmetic { id = "patch_research", label = "Top Scientist patch", slot = "patch", rarity = "Epic", icon = "lab", color = new Color(0.55f, 0.35f, 0.9f), howToUnlock = "Earn 1,000 research points in total." },
            new Cosmetic { id = "patch_rockmaster", label = "Rock Master patch", slot = "patch", rarity = "Legendary", icon = "star", color = new Color(0.95f, 0.7f, 0.15f), howToUnlock = "Find every kind of rock in the collection." },

            // Extras
            new Cosmetic { id = "stripes_commander", label = "Commander red stripes", slot = "stripes", rarity = "Legendary", color = new Color(0.85f, 0.1f, 0.1f), howToUnlock = "Get 3 stars on every stage." },
        };

        /// <summary>Try-on: the suit screen can show a locked item on the suit before you earn it.</summary>
        public static readonly Dictionary<string, string> Preview = new Dictionary<string, string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetPreview() => Preview.Clear();

        public static Color RarityColor(string r) => r switch
        {
            "Rare" => new Color(0.35f, 0.65f, 1.00f),
            "Epic" => new Color(0.75f, 0.45f, 1.00f),
            "Legendary" => new Color(1.00f, 0.72f, 0.20f),
            _ => new Color(0.78f, 0.78f, 0.80f),
        };

        public static int TotalIce => PlayerPrefs.GetInt("totalIce", 0);
        public static void AddIce() { PlayerPrefs.SetInt("totalIce", TotalIce + 1); PlayerPrefs.Save(); }

        /// <summary>Progress toward an unlock (for the progress bars). max = 0 means "no bar".</summary>
        public static (int cur, int max) Progress(Cosmetic c)
        {
            switch (c.id)
            {
                case "trim_sand": return (SaveSystem.TotalSamples, 5);
                case "trim_ice": return (TotalIce, 5);
                case "patch_icehunter": return (TotalIce, 3);
                case "visor_chrome": return (RockCollection.UniqueFound, 6);
                case "patch_geologist": return (RockCollection.UniqueFound, 5);
                case "patch_rockmaster": return (RockCollection.UniqueFound, RockCollection.All.Length);
                case "patch_research": return (SaveSystem.TotalResearch, 1000);
                case "patch_sol10": return (SaveSystem.FreeplaySols, 10);
                case "patch_sol25": return (SaveSystem.FreeplaySols, 25);
                case "patch_sol50": return (SaveSystem.FreeplaySols, 50);
                default: return (0, 0);
            }
        }

        /// <summary>Unlocks that come from collecting (rocks, ice, research). Call any time; returns newly unlocked labels.</summary>
        public static List<string> CheckCollection()
        {
            var fresh = new List<string>();
            void Give(string id) { if (!IsUnlocked(id) && SaveSystem.Unlock(id)) fresh.Add(Find(id)?.label ?? id); }
            if (RockCollection.Has("blueberries")) Give("trim_hematite");
            if (RockCollection.Has("jarosite")) Give("trim_jarosite");
            if (RockCollection.Has("heatshield") || RockCollection.Has("blockisland")) Give("trim_meteor");
            if (RockCollection.Has("cheyava")) Give("trim_leopard");
            if (TotalIce >= 5) Give("trim_ice");
            if (TotalIce >= 3) Give("patch_icehunter");
            if (RockCollection.UniqueFound >= 6) Give("visor_chrome");
            if (RockCollection.UniqueFound >= 5) Give("patch_geologist");
            if (RockCollection.UniqueFound >= RockCollection.All.Length && RockCollection.All.Length > 0) Give("patch_rockmaster");
            if (SaveSystem.TotalResearch >= 1000) Give("patch_research");
            return fresh;
        }

        static readonly Dictionary<string, string> Defaults = new Dictionary<string, string>
        {
            { "trim", "trim_white" }, { "visor", "visor_clear" }, { "patch", "patch_trainee" }, { "stripes", "" },
        };

        public static Cosmetic Find(string id)
        {
            foreach (var c in Catalog) if (c.id == id) return c;
            return null;
        }

        public static IEnumerable<Cosmetic> InSlot(string slot) => Catalog.Where(c => c.slot == slot);

        public static bool IsUnlocked(string id) => (Find(id)?.unlockedByDefault ?? false) || SaveSystem.HasUnlock(id);

        public static int UnlockedCount => Catalog.Count(c => IsUnlocked(c.id));

        /// <summary>What is worn in a slot (falls back to the default if nothing/locked is saved). Null = nothing (stripes off).</summary>
        public static Cosmetic Equipped(string slot)
        {
            if (Preview.TryGetValue(slot, out var pv)) return string.IsNullOrEmpty(pv) ? null : Find(pv);
            Defaults.TryGetValue(slot, out var def);
            var c = Find(SaveSystem.GetEquipped(slot, def ?? ""));
            if (c != null && IsUnlocked(c.id)) return c;
            return string.IsNullOrEmpty(def) ? null : Find(def);
        }

        public static bool IsEquipped(Cosmetic c) => Equipped(c.slot)?.id == c.id;

        /// <summary>Wear an unlocked item. Stripes toggle on/off.</summary>
        public static bool Equip(Cosmetic c)
        {
            if (!IsUnlocked(c.id)) return false;
            if (c.slot == "stripes") SaveSystem.SetEquipped("stripes", IsEquipped(c) ? "" : c.id);
            else SaveSystem.SetEquipped(c.slot, c.id);
            return true;
        }

        /// <summary>Check achievements after a mission. Returns labels of newly unlocked items for the debrief.</summary>
        public static List<string> OnMissionEnded(MissionData m, MissionResult r)
        {
            var fresh = new List<string>();
            void Give(string id) { if (!IsUnlocked(id) && SaveSystem.Unlock(id)) fresh.Add(Find(id)?.label ?? id); }

            Give("trim_orange");   // finishing counts, even a safe abort: never punish curiosity

            if (m.mode == "story" && r.success)
            {
                if (m.chapter == 0) Give("trim_green");
                if (m.chapter >= 1 && m.chapter <= 5) Give("patch_ch" + m.chapter);
                if (m.chapter == 9) { Give("visor_gold"); Give("patch_ch5"); }   // the last stage
                if (m.chapter == 1) Give("visor_frost");
            }
            foreach (var f in CheckCollection()) if (!fresh.Contains(f)) fresh.Add(f);

            int totalStars = 0;
            bool allThree = true;
            for (int ch = 1; ch < MissionLoader.StoryChapters.Length; ch++)
            {
                int s = SaveSystem.GetStars(MissionLoader.StoryChapters[ch]);
                totalStars += s;
                if (s < 3) allThree = false;
            }
            totalStars += SaveSystem.GetStars(MissionLoader.StoryChapters[0]);
            if (totalStars >= 9) Give("trim_silver");
            if (allThree) Give("stripes_commander");
            if (SaveSystem.TotalSamples >= 5) Give("trim_sand");

            if (m.mode == "freeplay")
            {
                if (SaveSystem.FreeplaySols >= 5) Give("visor_purple");
                int sols = SaveSystem.FreeplaySols;
                if (sols >= 10) Give("patch_sol10");
                if (sols >= 25) Give("patch_sol25");
                if (sols >= 50) Give("patch_sol50");
            }
            return fresh;
        }

        static bool HadSolarStorm(MissionData m)
        {
            if (m.sols != null) foreach (var s in m.sols) if (s.radiation >= 1.5f) return true;
            return false;
        }
    }
}
