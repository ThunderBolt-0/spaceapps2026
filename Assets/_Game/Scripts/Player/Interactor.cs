using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace MissionTrainer
{
    /// <summary>Anything the astronaut can use. Return null/empty Prompt when it can't be used now.</summary>
    public interface IInteractable
    {
        string Prompt { get; }
        void Interact();
    }

    /// <summary>
    /// Kid-friendly interaction: point at a thing with the mouse and CLICK it (or press E).
    /// While looking around (right button held) it uses the screen center instead.
    /// Things up to 7 m away can be used, so younger players don't need precise walking.
    /// </summary>
    public class Interactor : MonoBehaviour
    {
        public float range = 7f;
        Camera cam;
        FirstPersonController fpc;

        void Awake()
        {
            cam = GetComponent<Camera>();
            if (!cam) cam = Camera.main;
            fpc = GetComponentInParent<FirstPersonController>();
        }

        void Update()
        {
            var hud = PlaytestUI.Current;
            var mouse = Mouse.current;
            IInteractable target = null;
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

            if ((hud == null || !hud.WantsCursor) && !overUI)
            {
                bool looking = fpc && fpc.Looking;
                Ray ray = looking || mouse == null
                    ? new Ray(cam.transform.position, cam.transform.forward)
                    : cam.ScreenPointToRay(mouse.position.ReadValue());
                if (Physics.Raycast(ray, out var hit, range, ~0, QueryTriggerInteraction.Ignore))
                    target = hit.collider.GetComponentInParent<IInteractable>();
            }

            string prompt = target != null ? target.Prompt : null;
            bool usable = !string.IsNullOrEmpty(prompt);
            if (hud) hud.SetPrompt(usable ? "Click:  " + prompt : "");

            var kb = Keyboard.current;
            bool click = mouse != null && mouse.leftButton.wasPressedThisFrame && !overUI;
            if (usable && (click || (kb != null && kb.eKey.wasPressedThisFrame))) target.Interact();
        }
    }
}
