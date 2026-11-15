using UnityEngine;

namespace MissionTrainer
{
    /// <summary>Tells the engine if the astronaut is inside the habitat, in the shelter, or outside (EVA).</summary>
    public class PlayerLocation : MonoBehaviour
    {
        public MissionRunner runner;

        [Header("Round habitat (+ airlock tunnel counts as inside)")]
        public Vector3 habitatCenter = Vector3.zero;
        public float habitatRadius = 6.15f;
        public float habitatHeight = 6.5f;
        public Bounds airlock = new Bounds(new Vector3(0, 1.4f, 8.2f), new Vector3(2.8f, 2.8f, 4.6f));

        [Header("Radiation shelter")]
        public Bounds shelter = new Bounds(new Vector3(22, 1.2f, 18), new Vector3(5.4f, 2.4f, 5.4f));

        void Awake() { if (!runner) runner = FindAnyObjectByType<MissionRunner>(); }

        void Update()
        {
            if (!runner || runner.State == null) return;
            var p = transform.position + Vector3.up;   // chest height
            var flat = new Vector2(p.x - habitatCenter.x, p.z - habitatCenter.z);
            bool inHabitat = (flat.sqrMagnitude < habitatRadius * habitatRadius && p.y > habitatCenter.y - 1 && p.y < habitatCenter.y + habitatHeight)
                             || airlock.Contains(p);
            bool inShelter = shelter.Contains(p);
            runner.SetLocation(!inHabitat && !inShelter, inShelter);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(habitatCenter + Vector3.up, habitatRadius);
            Gizmos.DrawWireCube(airlock.center, airlock.size);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(shelter.center, shelter.size);
        }
    }
}
