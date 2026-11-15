using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MissionTrainer
{
    /// <summary>Main menu: chapters with stars, Freeplay, difficulty, and panel buttons.</summary>
    public class MenuView : MonoBehaviour
    {
        [Serializable]
        public class ChapterButton
        {
            public string missionId;  // e.g. "ch1_storm"
            public Button button;
            public TMP_Text starsText;
        }

        [Serializable]
        public class DifficultyButton
        {
            public string difficultyId; // cadet | pilot | commander
            public Button button;
        }

        public ChapterButton[] chapters;
        public DifficultyButton[] difficulties;
        public TMP_Text difficultyText;
        public Button freeplayButton;
        public TMP_Text freeplayStatsText;

        void Start()
        {
            foreach (var c in chapters)
            {
                var ch = c;
                ch.button.onClick.AddListener(() => GameManager.Instance.StartStory(ch.missionId));
                if (ch.starsText)
                {
                    int s = SaveSystem.GetStars(ch.missionId);
                    ch.starsText.text = new string('★', s) + new string('☆', 3 - s);
                }
            }

            foreach (var d in difficulties)
            {
                var diff = d;
                diff.button.onClick.AddListener(() => { GameManager.Instance.DifficultyId = diff.difficultyId; ShowDifficulty(); });
            }
            ShowDifficulty();

            if (freeplayButton) freeplayButton.onClick.AddListener(() => GameManager.Instance.StartFreeplay());
            if (freeplayStatsText)
                freeplayStatsText.text = $"Sols survived: {SaveSystem.FreeplaySols}   Best: {SaveSystem.HighScore}   Credits: {SaveSystem.Credits}";
        }

        void ShowDifficulty()
        {
            if (difficultyText) difficultyText.text = "Difficulty: " + GameManager.Instance.Level.label;
        }
    }
}
