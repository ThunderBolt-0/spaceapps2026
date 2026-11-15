using UnityEngine;

namespace MissionTrainer
{
    /// <summary>One round vault door of the airlock. Click it to open it (it closes by itself when you walk away).</summary>
    public class AirlockDoor : MonoBehaviour, IInteractable
    {
        public Airlock airlock;
        public bool inner;

        public string Prompt => null;   // the doors open by themselves (nothing to click)

        public void Interact()
        {
            if (airlock) airlock.Toggle(inner);
        }
    }
}
