using UnityEngine;

namespace MissionTrainer
{
    /// <summary>
    /// The base's weather mast (like MEDA on Perseverance). Click it on an EVA to read temperature, wind, pressure
    /// and dust for today, from the same data as the Mars Report. One daily mission; earns a little research.
    /// </summary>
    public class WeatherStation : MonoBehaviour, IInteractable
    {
        public MissionRunner runner;
        public Transform spinner;   // the wind cups, turned by today's wind

        void Awake() { if (!runner) runner = FindAnyObjectByType<MissionRunner>(); }

        void Update()
        {
            if (!spinner || runner == null || runner.State == null) return;
            var e = runner.Mission.EnvForSol(Mathf.Max(1, runner.State.sol + 1));
            spinner.Rotate(0, (90f + e.tau * 120f) * Time.deltaTime, 0, Space.Self);
        }

        public string Prompt
        {
            get
            {
                if (!runner || runner.State == null || runner.IsOver || runner.ActiveCard != null) return null;
                return runner.State.weatherRead ? "Read the weather station again" : $"Read the weather station  (+{HabitatState.ResearchPerWeather} research)";
            }
        }

        public void Interact()
        {
            if (!runner.State.outside) { runner.Say("HAB: The weather station is outside. Go out through the airlock."); return; }
            if (runner.ReadWeather() && PlaytestUI.Current) PlaytestUI.Current.ShowWeatherReport();
        }
    }
}
