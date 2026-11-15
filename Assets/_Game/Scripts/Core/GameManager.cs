using UnityEngine;
using UnityEngine.SceneManagement;

namespace MissionTrainer
{
    public enum GameMode { Story, Freeplay }

    /// <summary>
    /// Lives across scenes. Holds mode, difficulty and the current mission.
    /// The game runs in one scene (Mars): GameFlow switches between menus and missions there.
    /// Auto-creates itself, so you can press Play in any scene.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public const string MainScene = "Mars";

        static GameManager _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatic() => _instance = null;

        public static GameManager Instance
        {
            get
            {
                if (_instance == null) new GameObject("GameManager").AddComponent<GameManager>();
                return _instance;
            }
        }

        public GameMode Mode { get; private set; } = GameMode.Story;
        public MissionData CurrentMission { get; set; }
        public DifficultyConfig Config { get; private set; }

        /// <summary>Set before loading the Mars scene to jump straight into CurrentMission.</summary>
        public bool PlayOnLoad { get; set; }

        public string DifficultyId
        {
            get => SaveSystem.Difficulty;
            set => SaveSystem.Difficulty = value;
        }

        public DifficultyLevel Level => Config.Level(DifficultyId);

        void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            Config = DifficultyConfig.Load();
        }

        public void SetMission(MissionData mission, GameMode mode)
        {
            CurrentMission = mission;
            Mode = mode;
        }

        public void StartStory(string missionId)
        {
            if (GameFlow.Current) { GameFlow.Current.PlayStory(missionId); return; }
            var m = MissionLoader.Load(missionId);
            if (m == null) return;
            SetMission(m, GameMode.Story);
            PlayOnLoad = true;
            SceneManager.LoadScene(MainScene);
        }

        public void StartFreeplay()
        {
            if (GameFlow.Current) { GameFlow.Current.PlayFreeplay(); return; }
            SetMission(FreeplayGenerator.Generate(System.Environment.TickCount), GameMode.Freeplay);
            PlayOnLoad = true;
            SceneManager.LoadScene(MainScene);
        }

        public void Retry()
        {
            var runner = FindAnyObjectByType<MissionRunner>();
            if (runner && runner.Mission != null)
            {
                if (PlaytestUI.Current) PlaytestUI.Current.ClearForNewMission();
                runner.Restart();
            }
        }

        public void BackToMenu()
        {
            if (GameFlow.Current) GameFlow.Current.ShowTitle();
            else SceneManager.LoadScene(MainScene);
        }
    }
}
