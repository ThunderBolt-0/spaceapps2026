using UnityEngine;

namespace MissionTrainer
{
    /// <summary>
    /// Your scout rover (the NASA Perseverance model). Click it to read today's rover report: the real (or simulated)
    /// readings for this sol, with their data label. Sinks and tilts when the sol's softSand value is high.
    /// Some days it breaks: a red light blinks, the report is offline, and you repair it on an EVA
    /// (stay next to it for a few seconds while your air drains; the spare part costs power).
    /// </summary>
    public class RoverActor : MonoBehaviour, IInteractable
    {
        public MissionRunner runner;
        public Transform body;            // everything that sinks
        public float maxSink = 0.45f;
        public float maxTilt = 9f;
        public float repairSeconds = 4f;

        float target, current;
        Vector3 bodyStart;
        float repairT;
        bool repairing;
        Renderer faultLamp;
        Material faultMat;

        void Awake()
        {
            if (!runner) runner = FindAnyObjectByType<MissionRunner>();
            if (body) bodyStart = body.localPosition;
        }

        void Start() { MakeFaultLamp(); SaveHome(); }

        void OnEnable() { if (runner) runner.StateChanged += Retarget; }
        void OnDisable() { if (runner) runner.StateChanged -= Retarget; }

        void Retarget(HabitatState s) =>
            target = Mathf.Clamp01(runner.Mission.EnvForSol(Mathf.Max(1, s.sol)).softSand);

        bool Broken => runner && runner.State != null && runner.State.roverBroken && !runner.IsOver;

        /// <summary>A small red light on a mast. Uses a copy of the habitat's warning-lamp material (keeps WebGL shaders).</summary>
        void MakeFaultLamp()
        {
            var hv = FindAnyObjectByType<HazardVisuals>();
            if (!hv || !hv.warningLamp) return;
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(go.GetComponent<Collider>());
            go.name = "FaultLamp";
            go.transform.SetParent(transform, false);
            var col = GetComponent<BoxCollider>();
            float top = col ? col.center.y + col.size.y / 2 : 2.2f;
            go.transform.localPosition = new Vector3(0, top + 0.25f, 0);
            go.transform.localScale = Vector3.one * 0.22f;
            faultLamp = go.GetComponent<Renderer>();
            faultMat = new Material(hv.warningLamp.sharedMaterial);
            faultMat.EnableKeyword("_EMISSION");
            faultLamp.sharedMaterial = faultMat;
            go.SetActive(false);
        }

        void Update()
        {
            current = Mathf.MoveTowards(current, target, Time.deltaTime * 0.3f);
            if (body)
            {
                body.localPosition = bodyStart + Vector3.down * maxSink * current;
                body.localRotation = Quaternion.Euler(maxTilt * 0.4f * current, 0, maxTilt * current);
            }

            if (faultLamp)
            {
                bool on = Broken;
                if (faultLamp.gameObject.activeSelf != on) faultLamp.gameObject.SetActive(on);
                if (on)
                {
                    float blink = Mathf.PingPong(Time.time * 2.5f, 1f);
                    faultMat.SetColor("_EmissionColor", new Color(1f, 0.1f, 0.05f) * (0.3f + 3f * blink));
                    faultMat.SetColor("_BaseColor", Color.Lerp(new Color(0.4f, 0.05f, 0.05f), new Color(1f, 0.2f, 0.1f), blink));
                }
            }

            if (!repairing) return;
            var cam = Camera.main;
            bool near = cam && Vector3.Distance(cam.transform.position, transform.position) < 8f;
            if (!Broken) { repairing = false; return; }
            if (!near || runner.ActiveCard != null)
            {
                repairing = false;
                repairT = 0;
                runner.Say("HAB: Repair stopped. Stay next to the rover until it's done.");
                return;
            }
            if (runner.Paused) return;
            repairT += Time.deltaTime;
            if (repairT >= repairSeconds)
            {
                repairing = false;
                repairT = 0;
                runner.RepairRover();
            }
        }

        public bool IsStuck => current > 0.2f;

        // ── Rides (scripted missions): to the pinged location and back home
        Vector3 homePos;
        Quaternion homeRot;
        bool homeSaved;

        void SaveHome()
        {
            if (homeSaved) return;
            homeSaved = true;
            homePos = transform.position;
            homeRot = transform.rotation;
            if (runner) runner.MissionStarted += () => { transform.SetPositionAndRotation(homePos, homeRot); };
        }

        /// <summary>Park the rover somewhere (used by the ride cutscene).</summary>
        public void ParkAt(Vector3 pos, Vector3 facing)
        {
            SaveHome();
            var t = Terrain.activeTerrain;
            if (t) pos.y = t.SampleHeight(pos) + t.transform.position.y;
            var flat = facing - pos; flat.y = 0;
            transform.SetPositionAndRotation(pos, flat.sqrMagnitude > 0.01f ? Quaternion.LookRotation(flat) : transform.rotation);
        }

        string RideAction()
        {
            var st = runner.CurrentStep;
            if (st == null || runner.State == null) return null;
            if (st.kind == "rover" && !runner.State.roverVisited) return "board";
            if (st.kind == "ping" && !runner.State.atPing) return "toPing";
            if (runner.State.roverAtPing && (st.kind == "ice" || st.kind == "returnBase")) return "home";
            return null;
        }

        public string Prompt
        {
            get
            {
                if (!runner || runner.State == null || runner.IsOver) return null;
                if (Broken)
                {
                    if (repairing) return $"Repairing Perseverance...  {repairT / repairSeconds * 100:0}%  (stay close!)";
                    return $"Repair Perseverance  (-{MissionRunner.RepairPower * runner.Level.drainMultiplier:0}% power · takes {repairSeconds:0} s · +{HabitatState.ResearchPerRepair} research)";
                }
                switch (RideAction())
                {
                    case "board": return "Climb aboard Perseverance";
                    case "toPing": return "Drive Perseverance to the pinged location";
                    case "home": return "Drive Perseverance back to base";
                }
                return "Read the rover's report";
            }
        }

        public void Interact()
        {
            if (Broken)
            {
                if (!runner.State.outside) { runner.Say("HAB: You need to be outside, next to the rover, to fix it."); return; }
                if (!repairing) { repairing = true; repairT = 0; runner.Say($"HAB: Fixing it: {runner.State.roverFault} Stay close for {repairSeconds:0} seconds."); }
                return;
            }
            var ui = PlaytestUI.Current;
            switch (RideAction())
            {
                case "board":
                    runner.RoverVisited();
                    runner.Say("HAB: You're aboard! Perseverance can drive you to places that are far away.");
                    return;
                case "toPing":
                    if (ui) ui.RideRover(this, true);
                    return;
                case "home":
                    if (ui) ui.RideRover(this, false);
                    return;
            }
            if (ui) ui.ShowRoverReport(IsStuck);
            else runner.Say(IsStuck ? "HAB: The rover is stuck in [[softsand|soft sand]]!" : "HAB: The rover is ready to explore.");
        }
    }
}
