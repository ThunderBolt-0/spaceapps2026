using UnityEngine;

namespace MissionTrainer
{
    /// <summary>
    /// A rock sample on the surface. Each site shows WHICH kind of rock it is, so you can choose from far away:
    ///  - the light beam's color is the rarity (grey Common, green Uncommon, blue Rare, purple Epic, gold Legendary),
    ///    and rarer beams glow brighter;
    ///  - the rock has its real color (yellow jarosite, white gypsum, shiny metal meteorites...) and rarer rocks are bigger.
    /// Click to pick it up (costs oxygen); it goes into your rock collection. A fresh rock appears every morning.
    /// </summary>
    public class SampleSite : MonoBehaviour, IInteractable
    {
        public MissionRunner runner;
        public GameObject rock;
        public GameObject marker;
        bool taken;

        public RockInfo Rock { get; private set; }
        Material rockMat;
        Renderer[] beams;
        Material[] beamMats;
        Vector3 rockScale;

        void Awake()
        {
            if (!runner) runner = FindAnyObjectByType<MissionRunner>();
            if (rock) rockScale = rock.transform.localScale;
            // No flags: the rock and its colored beam say everything.
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("Flag")) t.gameObject.SetActive(false);
            if (marker)
            {
                var list = new System.Collections.Generic.List<Renderer>();
                foreach (var r in marker.GetComponentsInChildren<Renderer>(true)) if (r.name.StartsWith("Beam")) list.Add(r);
                beams = list.ToArray();
            }
        }

        void OnEnable() { if (runner) { runner.MissionStarted += ResetSite; runner.DayStarted += NewDay; } }
        void OnDisable() { if (runner) { runner.MissionStarted -= ResetSite; runner.DayStarted -= NewDay; } }

        void NewDay(int day) => ResetSite();

        public bool Available => !taken;

        public string Prompt
        {
            get
            {
                if (taken || !runner || runner.State == null || runner.IsOver || runner.ActiveCard != null) return null;
                string kind = Rock != null ? (RockCollection.Has(Rock.id) ? $"{Rock.name} ({Rock.rarity})" : $"a {Rock.rarity} rock you haven't found yet!") : "rock sample";
                return $"Collect {kind}  (-{runner.Config.balance.sampleOxygenCost:0}% oxygen)";
            }
        }

        public void Interact()
        {
            if (!runner.TryCollectSample(false, Rock)) return;
            taken = true;
            if (rock) rock.SetActive(false);
            if (marker) marker.SetActive(false);
        }

        void ResetSite()
        {
            taken = false;
            if (rock) rock.SetActive(true);
            if (marker) marker.SetActive(true);
            Rock = RockCollection.Pick();
            ApplyLook();
        }

        /// <summary>Rock color, size and beam color from the rock type and its rarity.</summary>
        void ApplyLook()
        {
            if (Rock == null) return;
            int tier = RockCollection.RarityIndex(Rock.rarity);
            if (rock)
            {
                var r = rock.GetComponent<Renderer>();
                if (r)
                {
                    if (!rockMat) rockMat = r.material;   // our own copy
                    var tint = Rock.Tint;
                    rockMat.color = new Color(Mathf.Clamp01(tint.r * 1.35f), Mathf.Clamp01(tint.g * 1.35f), Mathf.Clamp01(tint.b * 1.35f));
                    bool metal = RockCollection.IsMetal(Rock);
                    if (rockMat.HasProperty("_Metallic")) rockMat.SetFloat("_Metallic", metal ? 0.9f : 0.1f);
                    if (rockMat.HasProperty("_Smoothness")) rockMat.SetFloat("_Smoothness", metal ? 0.75f : 0.3f);
                }
                rock.transform.localScale = rockScale * (1f + 0.18f * tier);
            }
            if (beams != null)
            {
                if (beamMats == null) { beamMats = new Material[beams.Length]; for (int i = 0; i < beams.Length; i++) beamMats[i] = beams[i].material; }
                var rc = RockCollection.RarityColor(Rock.rarity);
                foreach (var m in beamMats)
                {
                    if (!m) continue;
                    float a = 0.16f + 0.07f * tier;
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(rc.r, rc.g, rc.b, a));
                }
                foreach (var b in beams) b.transform.localScale = new Vector3(0.35f + 0.08f * tier, b.transform.localScale.y, 1f);
            }
        }
    }
}
