using UnityEngine;

namespace MissionTrainer
{
    /// <summary>
    /// The antenna dish is the Radio you switch at its console: it slowly tracks Earth while the Radio is on,
    /// and stops when it's off (or when the Sun blocks contact). Added automatically by GameFlow.
    /// </summary>
    public class RadioDish : MonoBehaviour, IInteractable
    {
        public MissionRunner runner;
        public Transform dish;   // the part that turns (the mast stays put)
        void Awake() { if (!runner) runner = FindAnyObjectByType<MissionRunner>(); }

        bool Talking => runner && runner.State != null && runner.State.IsOn(SystemIds.Radio) && !runner.NoContact;

        void Update()
        {
            if (Talking && dish) dish.Rotate(0, 6f * Time.deltaTime, 0, Space.World);
        }

        public string Prompt =>
            runner && runner.State != null ? (Talking ? "Radio dish: talking to Earth" : "Radio dish: not connected") : null;

        public void Interact()
        {
            if (!runner || runner.State == null) return;
            runner.Say(runner.NoContact
                ? "HAB: The Sun is between Mars and Earth, so the dish can't reach Earth right now."
                : runner.State.IsOn(SystemIds.Radio)
                    ? "HAB: The dish is pointed at Earth. Messages take between 4 and 24 minutes to travel each way!"
                    : "HAB: The Radio is off, so the dish is resting. Turn the Radio on inside to hear Mission Control.");
        }
    }
}
