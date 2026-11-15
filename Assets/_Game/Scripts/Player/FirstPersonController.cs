using UnityEngine;
using UnityEngine.InputSystem;

namespace MissionTrainer
{
    /// <summary>
    /// Small first-person controller: WASD/arrows walk, Shift run, Space jump, mouse look.
    /// Click the game to capture the mouse (browsers require a click). Uses Mars gravity.
    /// Stops looking/moving while a UI overlay (card, debrief, Tab mouse) wants the cursor.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        public Transform cameraPivot;
        public MissionRunner runner;
        public float walkSpeed = 3.5f;
        public float runSpeed = 6f;
        public float lookSensitivity = 0.12f;
        public float jumpSpeed = 2.6f;
        [Tooltip("Mars gravity is about 38% of Earth's (3.71 m/s²), so jumps are floaty.")]
        public float gravity = 3.71f;

        /// <summary>True while the player holds the right mouse button to look around.</summary>
        public bool Looking { get; private set; }

        CharacterController cc;
        float pitch, verticalSpeed;
        Vector3 spawnPos;
        Quaternion spawnRot;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            if (!cameraPivot) cameraPivot = GetComponentInChildren<Camera>().transform;
            if (!runner) runner = FindAnyObjectByType<MissionRunner>();
            spawnPos = transform.position;
            spawnRot = transform.rotation;
        }

        void OnEnable() { if (runner) runner.MissionStarted += Respawn; }
        void OnDisable() { if (runner) runner.MissionStarted -= Respawn; }

        /// <summary>Move the astronaut instantly (one-click "Go to the shelter" / "Go back inside").</summary>
        public void TeleportTo(Vector3 position, Vector3 lookAt)
        {
            cc.enabled = false;
            var flat = lookAt - position; flat.y = 0;
            transform.SetPositionAndRotation(position, flat.sqrMagnitude > 0.01f ? Quaternion.LookRotation(flat) : transform.rotation);
            cc.enabled = true;
            pitch = 0;
            verticalSpeed = 0;
            if (cameraPivot) cameraPivot.localEulerAngles = Vector3.zero;
        }

        public void ReturnHome() => TeleportTo(spawnPos, spawnPos + spawnRot * Vector3.forward);

        void Respawn()
        {
            cc.enabled = false;   // CharacterController ignores direct position changes while enabled
            transform.SetPositionAndRotation(spawnPos, spawnRot);
            cc.enabled = true;
            pitch = 0;
            verticalSpeed = 0;
        }

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            bool uiWantsCursor = PlaytestUI.Current != null && PlaytestUI.Current.WantsCursor;

            // Kid-friendly: the mouse is never captured. Click things to use them; HOLD the right mouse button to look.
            if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Looking = !uiWantsCursor && mouse != null && mouse.rightButton.isPressed;

            // Look while the right button is held
            if (Looking)
            {
                Vector2 d = mouse.delta.ReadValue() * lookSensitivity;
                transform.Rotate(0, d.x, 0);
                pitch = Mathf.Clamp(pitch - d.y, -80, 80);
                cameraPivot.localEulerAngles = new Vector3(pitch, 0, 0);
            }

            // Move
            Vector2 move = Vector2.zero;
            bool run = false, jump = false;
            if (!uiWantsCursor && kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1;
                if (kb.dKey.isPressed) move.x += 1;
                if (kb.aKey.isPressed) move.x -= 1;
                // Turn with the arrow keys or Q / R (for trackpads: no right mouse button needed)
                float turn = 0;
                if (kb.rightArrowKey.isPressed || kb.rKey.isPressed) turn += 1;
                if (kb.leftArrowKey.isPressed || kb.qKey.isPressed) turn -= 1;
                if (turn != 0) transform.Rotate(0, turn * 110f * Time.deltaTime, 0);
                run = kb.leftShiftKey.isPressed;
                jump = kb.spaceKey.wasPressedThisFrame;
            }

            Vector3 dir = transform.forward * move.y + transform.right * move.x;
            if (dir.sqrMagnitude > 1) dir.Normalize();

            if (cc.isGrounded)
            {
                verticalSpeed = -0.5f;
                if (jump) verticalSpeed = jumpSpeed;
            }
            verticalSpeed -= gravity * Time.deltaTime;

            cc.Move((dir * (run ? runSpeed : walkSpeed) + Vector3.up * verticalSpeed) * Time.deltaTime);
        }
    }
}
