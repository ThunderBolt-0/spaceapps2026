using UnityEngine;

namespace MissionTrainer
{
    /// <summary>
    /// The airlock: two heavy round vault doors (habitat side and Mars side) in the tunnel.
    /// Click a door to open it; it swings slowly on its big hinges and closes by itself once you walk away.
    /// A closed door is solid; an open door never blocks you. Nothing else in the airlock has collision,
    /// so you can't get stuck. Lights: green = open, red = closed.
    /// Built by Mission Trainer > Use NASA 3D Models (NasaModelSetup.UpgradeAirlock).
    /// </summary>
    public class Airlock : MonoBehaviour
    {
        public Transform habitat;          // positions are measured in the habitat's local space
        public Transform innerHinge, outerHinge;
        public Renderer innerLamp, outerLamp;
        public float innerZ = 6.75f, outerZ = 10.3f;
        public float openAngle = 100f, swingSpeed = 75f, autoCloseDistance = 9f;

        bool innerOpen, outerOpen, saidHint;
        float innerAngle, outerAngle;
        Collider[] innerCols, outerCols;
        Transform player;
        Material innerMat, outerMat;
        MissionRunner runner;

        void Awake()
        {
            var fpc = FindAnyObjectByType<FirstPersonController>();
            if (fpc) player = fpc.transform;
            runner = FindAnyObjectByType<MissionRunner>();
            if (!habitat) habitat = transform.parent;
            if (innerLamp) innerMat = innerLamp.material;
            if (outerLamp) outerMat = outerLamp.material;
            // Walls and frames never block; only the closed doors do.
            foreach (var c in GetComponentsInChildren<Collider>(true)) c.enabled = false;
            innerCols = innerHinge ? innerHinge.GetComponentsInChildren<Collider>(true) : new Collider[0];
            outerCols = outerHinge ? outerHinge.GetComponentsInChildren<Collider>(true) : new Collider[0];
            if (runner) runner.MissionStarted += () => { innerOpen = outerOpen = false; innerAngle = outerAngle = 0; };
        }

        public bool IsOpen(bool inner) => inner ? innerOpen : outerOpen;

        public void Toggle(bool inner)
        {
            if (inner) innerOpen = !innerOpen; else outerOpen = !outerOpen;
            if (!saidHint && runner && runner.State != null && !runner.IsOver)
            {
                saidHint = true;
                runner.Say("HAB: These are heavy pressure doors. They keep the base's air inside, like a giant bank vault.");
            }
        }

        void Update()
        {
            if (player && habitat)
            {
                var p = habitat.InverseTransformPoint(player.position + Vector3.up);
                if (innerOpen && new Vector2(p.x, p.z - innerZ).magnitude > autoCloseDistance) innerOpen = false;
                if (outerOpen && new Vector2(p.x, p.z - outerZ).magnitude > autoCloseDistance) outerOpen = false;
            }

            innerAngle = Mathf.MoveTowards(innerAngle, innerOpen ? openAngle : 0, swingSpeed * Time.deltaTime);
            outerAngle = Mathf.MoveTowards(outerAngle, outerOpen ? openAngle : 0, swingSpeed * Time.deltaTime);
            if (innerHinge) innerHinge.localRotation = Quaternion.Euler(0, innerAngle, 0);    // swings into the habitat
            if (outerHinge) outerHinge.localRotation = Quaternion.Euler(0, -outerAngle, 0);   // swings out onto Mars

            // A closed door is solid. Once it starts opening it never blocks you (but you can still click it).
            SetSolid(innerCols, !innerOpen && innerAngle < 1f);
            SetSolid(outerCols, !outerOpen && outerAngle < 1f);
            SetLamp(innerMat, innerAngle > 5f);
            SetLamp(outerMat, outerAngle > 5f);
        }

        static void SetSolid(Collider[] cols, bool solid)
        {
            foreach (var c in cols)
            {
                if (!c) continue;
                c.enabled = true;
                c.isTrigger = !solid;
            }
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
