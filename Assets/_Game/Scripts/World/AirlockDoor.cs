using UnityEngine;

namespace MissionTrainer
{
    /// <summary>One round vault door of the airlock. Click it to open it (it closes by itself when you walk away).</summary>
    public class AirlockDoor : MonoBehaviour, IInteractable
    {
        public Airlock airlock;
        public bool inner;

        public string Prompt => !airlock ? null : airlock.IsOpen(inner) ? "Close the airlock door" : "Open the airlock door";

        public void Interact()
        {
            if (airlock) airlock.Toggle(inner);
        }
    }
}
