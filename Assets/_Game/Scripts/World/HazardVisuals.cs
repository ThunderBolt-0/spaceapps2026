using UnityEngine;
using UnityEngine.UI;

namespace MissionTrainer
{
    /// <summary>
    /// Makes each sol's hazards visible, from the same SolEnv data the engine uses:
    ///  tau        -> sky turns brown and dim, sun halo fades, fog closes in, dust blows, solar panels get dusty
    ///  cold       -> night: dark sky with stars, cold light, frost on the ground and on the visor edges
    ///  radiation  -> aurora curtains + pulsing red warning lamp in the habitat
    ///  (softSand is handled by RoverActor)
    /// Everything eases smoothly. WebGL-friendly: no extra lights, one particle system.
    /// </summary>
    public class HazardVisuals : MonoBehaviour
    {
        public MissionRunner runner;
        public Light sun;
        public Light habitatLight;
        public Camera cam;
        public Terrain terrain;
        public Material skyMaterial;
        public Renderer[] solarPanels;
        public GameObject auroraRoot;
        public Renderer[] auroraRenderers;
        public Renderer warningLamp;
        public ParticleSystem dust;

        [Header("Sky: clear day / dust storm / night")]
        public Color dayZenith = new Color(0.48f, 0.34f, 0.26f);
        public Color dayHorizon = new Color(0.86f, 0.66f, 0.48f);
        public Color stormZenith = new Color(0.30f, 0.20f, 0.13f);
        public Color stormHorizon = new Color(0.44f, 0.30f, 0.19f);
        public Color nightZenith = new Color(0.010f, 0.012f, 0.030f);
        public Color nightHorizon = new Color(0.05f, 0.05f, 0.08f);

        [Header("Other")]
        public Color habitatWarm = new Color(1f, 0.92f, 0.82f);
        public Color alarmRed = new Color(1f, 0.15f, 0.1f);
        public Color auroraColor = new Color(0.4f, 1f, 0.6f, 0.5f);
        public Vector3 frostTint = new Vector3(1.15f, 1.5f, 2.0f);   // multiplies orange ground toward pale blue-white

        // eased state
        float tDust, cDust, tNight, cNight, tFog, cFog, tFrostGround, cFrostGround, tVisorFrost, cVisorFrost, tSun = 1, cSun = 1;
        float dustRate, appliedFrost = -1;
        bool storm, snapNext = true;

        Material sky;
        Material[] panelMats, auroraMats;
        float cPanelDust;
        Material lampMat;
        TerrainLayer[] layers;
        Image[] frostEdges;
        float baseSunIntensity;

        static readonly int ZenithId = Shader.PropertyToID("_ZenithColor");
        static readonly int HorizonId = Shader.PropertyToID("_HorizonColor");
        static readonly int GroundId = Shader.PropertyToID("_GroundColor");
        static readonly int HaloId = Shader.PropertyToID("_HaloStrength");
        static readonly int StarsId = Shader.PropertyToID("_StarIntensity");
        static readonly int SunDirId = Shader.PropertyToID("_SunDir");
        static readonly int SunColorId = Shader.PropertyToID("_SunColor");

        void Awake()
        {
            if (!runner) runner = FindAnyObjectByType<MissionRunner>();
            if (!cam) cam = Camera.main;
            baseSunIntensity = sun ? sun.intensity : 1.8f;

            // Work on copies so play mode never edits the saved assets.
            if (skyMaterial) { sky = new Material(skyMaterial); RenderSettings.skybox = sky; }
            panelMats = Instances(solarPanels);
            auroraMats = Instances(auroraRenderers);
            if (warningLamp) lampMat = warningLamp.material;
            CloneTerrainLayers();
            BuildFrostOverlay();
        }

        static Material[] Instances(Renderer[] rs)
        {
            var m = new Material[rs?.Length ?? 0];
            for (int i = 0; i < m.Length; i++) m[i] = rs[i].material;
            return m;
        }

        void CloneTerrainLayers()
        {
            if (!terrain || terrain.terrainData == null) return;
            var data = Instantiate(terrain.terrainData);
            var src = data.terrainLayers;
            layers = new TerrainLayer[src.Length];
            for (int i = 0; i < src.Length; i++) layers[i] = Instantiate(src[i]);
            data.terrainLayers = layers;
            terrain.terrainData = data;
            var col = terrain.GetComponent<TerrainCollider>();
            if (col) col.terrainData = data;
        }

        void OnEnable()
        {
            if (!runner) return;
            runner.StateChanged += Retarget;
            runner.MissionStarted += Snap;
        }

        void OnDisable()
        {
            if (!runner) return;
            runner.StateChanged -= Retarget;
            runner.MissionStarted -= Snap;
        }

        void Snap() => snapNext = true;

        void Retarget(HabitatState s)
        {
            var env = runner.Mission.EnvForSol(Mathf.Max(1, s.sol));
            tDust = Mathf.Clamp01((env.tau - 0.5f) / 7f);
            tNight = Mathf.Clamp01(env.cold);
            tSun = Mathf.Clamp01(env.sunlight);
            tFog = s.outside ? 0.0018f + 0.0065f * env.tau : 0.0012f;   // the habitat air is clear
            tFrostGround = tNight * 0.75f;
            bool heaterOff = !s.IsOn(SystemIds.Heater);
            tVisorFrost = s.outside || s.inShelter ? tNight : (heaterOff ? tNight * 0.8f : 0f);
            storm = false;   // radiation was removed from the game
            dustRate = s.outside ? env.tau * 22f : 0f;

            // Dust storm emergency: the sky darkens, fog closes in and dust blows harder every second
            float st = runner.StormStrength;
            if (s.stormActive)
            {
                tDust = Mathf.Max(tDust, 0.35f + 0.65f * st);
                tSun = Mathf.Min(tSun, 1f - 0.85f * st);
                if (s.outside) { tFog = 0.006f + 0.07f * st; dustRate = 80f + 600f * st; }
            }
            else if (s.stormOver) { tDust = Mathf.Max(tDust, 0.8f); tSun = Mathf.Min(tSun, 0.2f); }
            // Night with no sun (the night survival step)
            if (s.nightActive) { tNight = 1f; tSun = 0f; }
            else if (s.nightOver) { tNight = 0f; tSun = Mathf.Max(tSun, 0.4f); }

            if (snapNext)
            {
                cDust = tDust; cNight = tNight; cSun = tSun; cFog = tFog; cFrostGround = tFrostGround; cVisorFrost = tVisorFrost;
                snapNext = false;
            }
        }

        /// <summary>Night cutscene: 0 = day, 1 = night (-1 = off, use the mission data). The sky, sun light and stars follow it.</summary>
        public static float CinematicNight = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatic() => CinematicNight = -1f;

        void Update()
        {
            float k = 1f - Mathf.Exp(-1.2f * Time.deltaTime);
            cDust = Mathf.Lerp(cDust, tDust, k);
            cNight = Mathf.Lerp(cNight, tNight, k);
            cSun = Mathf.Lerp(cSun, tSun, k);
            cFog = Mathf.Lerp(cFog, tFog, k * 2.5f);
            if (CinematicNight >= 0)
            {
                cNight = CinematicNight;
                cSun = 1f;
                cDust = Mathf.Min(cDust, 0.25f);
                cFog = 0.0016f;   // a clear view from outside the base
            }
            cFrostGround = Mathf.Lerp(cFrostGround, tFrostGround, k);
            cVisorFrost = Mathf.Lerp(cVisorFrost, tVisorFrost, k);

            var zenith = Color.Lerp(Color.Lerp(dayZenith, stormZenith, cDust), nightZenith, cNight);
            var horizon = Color.Lerp(Color.Lerp(dayHorizon, stormHorizon, cDust), nightHorizon, cNight);

            if (sky)
            {
                sky.SetColor(ZenithId, zenith);
                sky.SetColor(HorizonId, horizon);
                sky.SetColor(GroundId, horizon * 0.6f);
                sky.SetFloat(HaloId, 0.35f * (1 - cDust) * (1 - cNight));
                sky.SetFloat(StarsId, cNight * (1 - cDust) * 1.3f);
                sky.SetColor(SunColorId, Color.Lerp(new Color(1f, 0.95f, 0.85f), new Color(1f, 0.7f, 0.45f), cDust) * (1 - cNight));
                if (sun) sky.SetVector(SunDirId, -sun.transform.forward);
            }

            RenderSettings.fogColor = horizon;
            RenderSettings.fogDensity = cFog;
            float amb = Mathf.Lerp(1f, 0.25f, cNight);
            RenderSettings.ambientSkyColor = Color.Lerp(zenith, horizon, 0.3f) * 1.05f * amb + new Color(0.02f, 0.025f, 0.04f);
            RenderSettings.ambientEquatorColor = horizon * 0.75f * amb + new Color(0.015f, 0.015f, 0.02f);
            RenderSettings.ambientGroundColor = new Color(0.30f, 0.19f, 0.13f) * amb;

            if (sun)
            {
                sun.intensity = baseSunIntensity * Mathf.Lerp(0.15f, 1f, cSun) * Mathf.Lerp(1f, 0.06f, cNight);
                sun.color = Color.Lerp(Color.Lerp(new Color(1f, 0.94f, 0.84f), new Color(1f, 0.72f, 0.48f), cDust), new Color(0.6f, 0.7f, 1f), cNight);
                sun.shadowStrength = Mathf.Lerp(0.85f, 0.25f, Mathf.Max(cDust, cNight));
            }

            // Dust settles on the solar panels during storms.
            // Panels show the real dust level from the engine (cleaning them on an EVA makes them shine again).
            float panelTarget = runner && runner.State != null ? runner.State.panelDust : 0f;
            cPanelDust = Mathf.Lerp(cPanelDust, panelTarget, k * 3f);
            for (int i = 0; i < panelMats.Length; i++)
            {
                panelMats[i].color = Color.Lerp(Color.white, new Color(2.0f, 1.35f, 0.9f), cPanelDust * 0.8f);
                panelMats[i].SetFloat("_Smoothness", Mathf.Lerp(0.85f, 0.25f, cPanelDust));
            }

            // Frost on the ground (re-assign layers only when it changed enough; it's not free).
            if (layers != null && Mathf.Abs(cFrostGround - appliedFrost) > 0.03f)
            {
                appliedFrost = cFrostGround;
                var tint = Vector3.Lerp(Vector3.one, frostTint, appliedFrost);
                foreach (var l in layers) l.diffuseRemapMax = new Vector4(tint.x, tint.y, tint.z, 1);
                terrain.terrainData.terrainLayers = layers;
            }

            if (frostEdges != null)
                foreach (var img in frostEdges)
                {
                    var c = img.color; c.a = cVisorFrost * 0.33f; img.color = c;
                }

            if (dust) { var em = dust.emission; em.rateOverTime = dustRate; }

            float t = Time.time;
            if (auroraRoot && auroraRoot.activeSelf != storm) auroraRoot.SetActive(storm);
            if (storm)
                for (int i = 0; i < auroraMats.Length; i++)
                {
                    var c = auroraColor;
                    c.a = auroraColor.a * (0.55f + 0.45f * Mathf.Sin(t * 0.5f + i * 1.7f)) * (0.4f + 0.6f * Mathf.Max(cNight, 0.5f));
                    auroraMats[i].color = c;
                }

            float pulse = storm ? (0.5f + 0.5f * Mathf.Sin(t * 5f)) : 0f;
            if (lampMat)
            {
                lampMat.color = storm ? alarmRed : new Color(0.3f, 0.08f, 0.08f);
                lampMat.SetColor("_EmissionColor", alarmRed * 4f * pulse);
            }
            if (habitatLight) habitatLight.color = Color.Lerp(habitatWarm, alarmRed, pulse * 0.45f);
        }

        /// <summary>Frost creeping in from the visor edges: soft layered rings.</summary>
        void BuildFrostOverlay()
        {
            var go = new GameObject("FrostOverlay", typeof(Canvas));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -10;   // under the HUD

            var list = new System.Collections.Generic.List<Image>();
            foreach (float depth in new[] { 0.04f, 0.09f, 0.15f })
            {
                list.Add(Edge(go.transform, new Vector2(0, 1 - depth), Vector2.one));
                list.Add(Edge(go.transform, Vector2.zero, new Vector2(1, depth)));
                list.Add(Edge(go.transform, Vector2.zero, new Vector2(depth, 1)));
                list.Add(Edge(go.transform, new Vector2(1 - depth, 0), Vector2.one));
            }
            frostEdges = list.ToArray();
        }

        static Image Edge(Transform parent, Vector2 min, Vector2 max)
        {
            var r = new GameObject("Frost", typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(parent, false);
            r.anchorMin = min; r.anchorMax = max;
            r.offsetMin = r.offsetMax = Vector2.zero;
            var img = r.gameObject.AddComponent<Image>();
            img.color = new Color(0.85f, 0.93f, 1f, 0f);
            img.raycastTarget = false;
            return img;
        }
    }
}
