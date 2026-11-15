using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MissionTrainer
{
    /// <summary>
    /// All saves go through PlayerPrefs (IndexedDB on WebGL). Every write calls Save()
    /// so progress survives closing the browser tab.
    /// </summary>
    public static class SaveSystem
    {
        const string Stars = "stars_";
        const string CreditsKey = "credits";
        const string HighScoreKey = "highscore";
        const string FreeplaySolsKey = "freeplaySols";
        const string UnlocksKey = "unlocks";
        const string UpgradesKey = "upgrades";
        const string EquippedKey = "equipped_";
        const string DifficultyKey = "difficulty";

        public static int GetStars(string missionId) => PlayerPrefs.GetInt(Stars + missionId, 0);

        public static void SetBestStars(string missionId, int stars)
        {
            if (stars > GetStars(missionId)) SetInt(Stars + missionId, stars);
        }

        public static int Credits { get => PlayerPrefs.GetInt(CreditsKey, 0); set => SetInt(CreditsKey, Mathf.Max(0, value)); }
        public static int HighScore { get => PlayerPrefs.GetInt(HighScoreKey, 0); set => SetInt(HighScoreKey, value); }
        public static int FreeplaySols { get => PlayerPrefs.GetInt(FreeplaySolsKey, 0); set => SetInt(FreeplaySolsKey, value); }
        public static string Difficulty { get => PlayerPrefs.GetString(DifficultyKey, "cadet"); set => SetString(DifficultyKey, value); }
        // Story progress: a stage is "done" when its mission ends in success (its lesson goes in the Crew Handbook).
        public static bool IsDone(string missionId) => PlayerPrefs.GetInt("done_" + missionId, 0) == 1;
        public static void MarkDone(string missionId) => SetInt("done_" + missionId, 1);

        public static int TotalSamples { get => PlayerPrefs.GetInt("totalSamples", 0); set => SetInt("totalSamples", value); }
        public static int TotalResearch { get => PlayerPrefs.GetInt("totalResearch", 0); set => SetInt("totalResearch", value); }
        public static bool SeenIntro { get => PlayerPrefs.GetInt("seenIntro", 0) == 1; set => SetInt("seenIntro", value ? 1 : 0); }

        // Cosmetic unlocks (never affect gameplay)
        public static List<string> Unlocks => ReadList(UnlocksKey);
        public static bool HasUnlock(string id) => Unlocks.Contains(id);
        public static bool Unlock(string id) => AddToList(UnlocksKey, id);

        // Freeplay shop upgrades
        public static List<string> Upgrades => ReadList(UpgradesKey);
        public static bool HasUpgrade(string id) => Upgrades.Contains(id);
        public static bool AddUpgrade(string id) => AddToList(UpgradesKey, id);

        public static string GetEquipped(string slot, string fallback) => PlayerPrefs.GetString(EquippedKey + slot, fallback);
        public static void SetEquipped(string slot, string id) => SetString(EquippedKey + slot, id);

        static List<string> ReadList(string key) =>
            PlayerPrefs.GetString(key, "").Split(',').Where(s => s.Length > 0).ToList();

        static bool AddToList(string key, string id)
        {
            var list = ReadList(key);
            if (list.Contains(id)) return false;
            list.Add(id);
            SetString(key, string.Join(",", list));
            return true;
        }

        static void SetInt(string key, int v) { PlayerPrefs.SetInt(key, v); PlayerPrefs.Save(); }
        static void SetString(string key, string v) { PlayerPrefs.SetString(key, v); PlayerPrefs.Save(); }
    }
}
