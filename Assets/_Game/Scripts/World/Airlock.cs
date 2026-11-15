using UnityEngine;

namespace MissionTrainer
{
    /// <summary>
    /// The airlock: two heavy round vault doors (habitat side and Mars side) in the tunnel. Purely visual:
    /// the doors open by themselves as you walk up, and like a real airlock only one is open at a time.
    /// Walking out: the inner door opens; once you're in the far half of the airlock it shuts and the outer one opens.
    /// Walking in: the same, the other way round. Nothing in the airlock has collision, so you can always walk straight through.
    /// Lights: green = open, red = closed. Built by Mission Trainer > Use NASA 3D Models (NasaModelSetup.UpgradeAirlock).
    /// </summary>
    public class Airlock : MonoBehaviour
    {
        public Transform habitat;          // positions are measured in the habitat's local space
        public Transform innerHinge, outerHinge;
        public Renderer innerLamp, outerLamp;
        public float innerZ = 6.75f, outerZ = 10.3f;
        public float openAngle = 100f, swingSpeed = 150f, openDistance = 4.5f;

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
            foreach (var c in GetComponentsInChildren<Collider>(true)) c.enabled = false;   // just for show: never blocks
        }

        public bool IsOpen(bool inner) => inner ? innerAngle > 5f : outerAngle > 5f;
        public void Toggle(bool inner) { }   // doors are automatic now

        void Update()
        {
            bool openInner = false, openOuter = false;
            if (player && habitat)
            {
                var p = habitat.InverseTransformPoint(player.position + Vector3.up);
                float mid = (innerZ + outerZ) / 2f;
                bool nearTunnel = Mathf.Abs(p.x) < 2.5f;
                if (nearTunnel && p.z < mid && new Vector2(p.x, p.z - innerZ).magnitude < openDistance) openInner = true;        // base side: inner open, outer shut
                else if (nearTunnel && p.z >= mid && new Vector2(p.x, p.z - outerZ).magnitude < openDistance + 0.5f) openOuter = true;   // Mars side: outer open, inner shut
            }

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
