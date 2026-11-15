using UnityEngine;

namespace MissionTrainer
{
    /// <summary>
    /// The airlock in the tunnel: an inner door (habitat side) and an outer door (Mars side).
    /// It's for show: each door swings open whenever you're near it, and nothing in the airlock blocks you
    /// (door and wall colliders are switched off), so you can always walk straight through.
    /// Lights: green = open, red = closed. If you step outside without your suit, HAB suits you up.
    /// Built by Mission Trainer > Use NASA 3D Models (NasaModelSetup.UpgradeAirlock).
    /// </summary>
    public class Airlock : MonoBehaviour
    {
        public Transform habitat;          // positions are measured in the habitat's local space
        public Transform innerHinge, outerHinge;
        public Renderer innerLamp, outerLamp;
        public float innerZ = 6.75f, outerZ = 10.3f;
        public float openAngle = 100f, swingSpeed = 260f, openDistance = 4.5f;

        float innerAngle, outerAngle;
        Transform player;
        Material innerMat, outerMat;

        void Awake()
        {
            var fpc = FindAnyObjectByType<FirstPersonController>();
            if (fpc) player = fpc.transform;
            if (!habitat) habitat = transform.parent;
            if (innerLamp) innerMat = innerLamp.material;
            if (outerLamp) outerMat = outerLamp.material;
            // Just for show: nothing in the airlock can block the player.
            foreach (var c in GetComponentsInChildren<Collider>(true)) c.enabled = false;
        }

        void Update()
        {
            if (!player || !habitat) return;
            var p = habitat.InverseTransformPoint(player.position + Vector3.up);
            bool openInner = new Vector2(p.x, p.z - innerZ).magnitude < openDistance;
            bool openOuter = new Vector2(p.x, p.z - outerZ).magnitude < openDistance;

            innerAngle = Mathf.MoveTowards(innerAngle, openInner ? openAngle : 0, swingSpeed * Time.deltaTime);
            outerAngle = Mathf.MoveTowards(outerAngle, openOuter ? openAngle : 0, swingSpeed * Time.deltaTime);
            if (innerHinge) innerHinge.localRotation = Quaternion.Euler(0, innerAngle, 0);    // swings into the habitat
            if (outerHinge) outerHinge.localRotation = Quaternion.Euler(0, -outerAngle, 0);   // swings out onto Mars

            SetLamp(innerMat, innerAngle > 5f);
            SetLamp(outerMat, outerAngle > 5f);
        }

        static void SetLamp(Material m, bool open)
        {
            if (!m) return;
            var c = open ? new Color(0.1f, 1f, 0.3f) * 1.8f : new Color(1f, 0.15f, 0.1f) * 0.9f;
            m.SetColor("_EmissionColor", c);
            m.SetColor("_BaseColor", new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b)) * 0.6f);
        }
    }
}
