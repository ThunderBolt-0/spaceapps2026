using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MissionTrainer.EditorTools
{
    /// <summary>
    /// Menu: Mission Trainer > Build Mars Scene (3D).
    /// Generates Assets/_Game/Scenes/Mars.unity: a Meridiani Planum terrain (plain, ripples, craters, bedrock),
    /// faceted rocks, a domed habitat with tunnel airlock, solar field, antenna, buried radiation shelter,
    /// an Opportunity-style rover, sample sites, a Mars sky and gentle post-processing.
    /// All textures/meshes/materials are generated once and saved under Assets/_Game/Art so the
    /// WebGL build just loads them. Re-running replaces the scene (it asks first).
    /// </summary>
    public static class MarsSceneBuilder
    {
        const string ScenePath = "Assets/_Game/Scenes/Mars.unity";
        const string MatDir = "Assets/_Game/Art/Materials";
        static string Gen => ProcArt.GenDir;

        static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();
        static readonly Dictionary<string, ProcArt.TexSet> Tex = new Dictionary<string, ProcArt.TexSet>();
        static Font font;
        static Terrain terrain;

        // ── Layout (meters). Habitat at the origin, airlock door facing +Z.
        const float HabOuterR = 6.45f, HabInnerR = 6.15f, HabWall = 3.2f, DoorTop = 2.5f;
        static readonly Vector3 ShelterPos = new Vector3(22, 0, 18);
        const float ShelterDoorAngle = 220f;  // faces the habitat
        static readonly Vector3 RoverPos = new Vector3(-14, 0, 24);
        static readonly Vector3 AntennaPos = new Vector3(10, 0, -9);
        static readonly Vector2[] SampleSites = { new Vector2(12, 42), new Vector2(-32, 12), new Vector2(40, -8), new Vector2(-6, 60) };
        static readonly Vector4[] Craters = // x, z, radius, depth
        {
            new Vector4(38, -30, 11, 2.2f),      // small and close, like Eagle crater where Opportunity landed
            new Vector4(-175, 150, 65, 13),      // big, like Endurance crater
            new Vector4(95, 62, 6, 1.0f), new Vector4(-62, -72, 8, 1.4f), new Vector4(160, -125, 22, 4f), new Vector4(-34, 96, 4, 0.6f),
        };

        [MenuItem("Mission Trainer/Build Mars Scene (3D)")]
        public static void Build()
        {
            if (File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("Rebuild Mars scene?",
                    "This regenerates the terrain, art and Assets/_Game/Scenes/Mars.unity. Hand edits in that scene will be lost.",
                    "Rebuild", "Cancel"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            try
            {
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                Directory.CreateDirectory(MatDir);
                ProcArt.EnsureFolders();
                Mats.Clear();
                Tex.Clear();

                Progress("Painting textures", 0.05f);
                MakeTextures();
                SuitSetup.GeneratePatches();

                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                Progress("Sky and light", 0.35f);
                var sky = SetupSkyAndLight(out var sun);

                Progress("Terrain", 0.45f);
                terrain = BuildTerrain();

                var mission = new GameObject("Mission");
                var runner = mission.AddComponent<MissionRunner>();
                runner.fallbackMissionId = "test_mission";
                mission.AddComponent<PlaytestUI>().worldMode = true;

                Progress("Habitat", 0.6f);
                var habLight = BuildHabitat(runner, out var lamp, out var solarPanels);

                Progress("Rocks, shelter, rover", 0.75f);
                ScatterRocks();
                var shelterBounds = BuildShelter();
                BuildRover(runner);
                BuildSampleSites(runner);
                var aurora = BuildAurora(out var auroraRenderers);

                Progress("Player", 0.9f);
                var player = BuildPlayer(runner, shelterBounds, out var cam, out var dust);
                BuildPostProcessing();

                var hv = mission.AddComponent<HazardVisuals>();
                hv.runner = runner;
                hv.sun = sun;
                hv.habitatLight = habLight;
                hv.cam = cam;
                hv.terrain = terrain;
                hv.skyMaterial = sky;
                hv.solarPanels = solarPanels;
                hv.auroraRoot = aurora;
                hv.auroraRenderers = auroraRenderers;
                hv.warningLamp = lamp;
                hv.dust = dust;

                GameFlowSetup.Attach();   // title screen, chapter map, science page
                NasaModelSetup.Apply();   // NASA 3D models (rover, habitat outside, suit) if they're imported

                AssetDatabase.SaveAssets();
                EditorSceneManager.SaveScene(scene, ScenePath);

                var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
                scenes.RemoveAll(s => s.path == ScenePath);
                scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();

                Selection.activeGameObject = null;   // a selected Player draws its location gizmos in the Game view
                Debug.Log("Built " + ScenePath + ". Press Play, click the Game view, walk with WASD, use panels with E.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        static void Progress(string what, float t) => EditorUtility.DisplayProgressBar("Building Mars", what, t);

        // ───────────────────────── textures ─────────────────────────

        static void MakeTextures()
        {
            // Fine reddish-tan soil. Low contrast on purpose: real Martian soil reads as one even color up close,
            // with tiny, sparse dark grains (hematite "blueberries") and soft clumping.
            Tex["Dust"] = ProcArt.Surface("MarsDust", 1024, 2.5f, (float u, float v, out Color c, out float h) =>
            {
                float mid = ProcArt.FbmTile(u, v, 6, 4, 1);        // soft clumps
                float grain = ProcArt.FbmTile(u, v, 128, 2, 2);    // sand grain
                float fine = ProcArt.FbmTile(u, v, 32, 3, 3);
                var vor = ProcArt.VoronoiTile(u, v, 120, 4, out int cell);
                float pick = ProcArt.Hash01(cell, 0, 9);
                float berry = pick > 0.86f ? 1 - Mathf.SmoothStep(0.06f, 0.13f, vor.x) : 0;          // ~14% of cells, ~2 mm specks
                float pebble = pick < 0.03f ? 1 - Mathf.SmoothStep(0.10f, 0.22f, vor.x) : 0;          // a few light pebbles
                c = new Color(0.64f, 0.41f, 0.27f) * (0.95f + 0.07f * (mid - 0.5f) * 2 + 0.04f * (fine - 0.5f) * 2) * (0.97f + 0.06f * grain);
                c = Color.Lerp(c, new Color(0.30f, 0.24f, 0.22f), berry * 0.55f);
                c = Color.Lerp(c, new Color(0.74f, 0.58f, 0.44f), pebble * 0.5f);
                c.a = 1;
                h = grain * 0.35f + fine * 0.3f + mid * 0.2f + berry * 0.25f + pebble * 0.3f;
            });

            // Darker basaltic sand in soft wind ripples (common at Meridiani). Gentle color shift, mostly shape.
            Tex["Sand"] = ProcArt.Surface("MarsRippleSand", 1024, 3.5f, (float u, float v, out Color c, out float h) =>
            {
                float warp = ProcArt.FbmTile(u, v, 3, 3, 5);
                float ph = u * 6 + v + warp * 1.6f;
                float f = ph - Mathf.Floor(ph);
                float r = f < 0.7f ? f / 0.7f : (1 - f) / 0.3f;      // gentle upwind side, steeper lee side
                float rip = Mathf.SmoothStep(0, 1, r);
                float grain = ProcArt.FbmTile(u, v, 128, 2, 6);
                float mid = ProcArt.FbmTile(u, v, 6, 3, 7);
                c = Color.Lerp(new Color(0.44f, 0.31f, 0.22f), new Color(0.56f, 0.38f, 0.26f), rip * 0.6f + mid * 0.25f) * (0.97f + 0.06f * grain);
                c.a = 1;
                h = rip * 0.7f + grain * 0.2f + mid * 0.1f;
            });

            // Pale layered bedrock (Burns formation style): fine layering, thin dust-filled cracks, soft tones.
            Tex["Outcrop"] = ProcArt.Surface("MarsOutcrop", 1024, 3.5f, (float u, float v, out Color c, out float h) =>
            {
                var vor = ProcArt.VoronoiTile(u, v, 4, 11, out int cell);
                float crack = 1 - Mathf.SmoothStep(0.008f, 0.035f, vor.y - vor.x);
                float tone = ProcArt.Hash01(cell, 1, 3);
                float warp = ProcArt.FbmTile(u, v, 3, 3, 12);
                float layers = 0.5f + 0.5f * Mathf.Sin((v * 30 + warp * 3) * Mathf.PI * 2);
                float n = ProcArt.FbmTile(u, v, 16, 4, 13);
                float dustCover = Mathf.SmoothStep(0.45f, 0.7f, ProcArt.FbmTile(u, v, 4, 3, 14));
                c = new Color(0.70f, 0.55f, 0.42f) * (0.94f + 0.06f * tone) * (0.96f + 0.04f * layers) * (0.94f + 0.1f * n);
                c = Color.Lerp(c, new Color(0.62f, 0.40f, 0.27f), Mathf.Max(crack * 0.7f, dustCover * 0.6f));
                c.a = 1;
                h = 0.6f - crack * 0.5f + layers * 0.06f + n * 0.2f - dustCover * 0.15f;
            });

            // Basalt rock with small pits.
            Tex["Rock"] = ProcArt.Surface("MarsRock", 512, 5f, (float u, float v, out Color c, out float h) =>
            {
                float n = ProcArt.FbmTile(u, v, 6, 5, 21), n2 = ProcArt.FbmTile(u, v, 24, 3, 22);
                var vor = ProcArt.VoronoiTile(u, v, 30, 23, out int cell);
                float pit = ProcArt.Hash01(cell, 3, 1) > 0.6f ? 1 - Mathf.SmoothStep(0.08f, 0.2f, vor.x) : 0;
                c = Color.Lerp(new Color(0.21f, 0.16f, 0.14f), new Color(0.47f, 0.34f, 0.26f), n) * (0.9f + 0.2f * n2);
                c = Color.Lerp(c, new Color(0.10f, 0.08f, 0.08f), pit * 0.6f);
                c.a = 1;
                h = n * 0.6f + n2 * 0.3f - pit * 0.3f;
            });

            // Sintered-regolith bricks for the shelter.
            Tex["Brick"] = ProcArt.Surface("RegolithBrick", 512, 6f, (float u, float v, out Color c, out float h) =>
            {
                float rows = 8, cols = 4;
                float ry = v * rows;
                int row = Mathf.FloorToInt(ry);
                float rx = u * cols + (row % 2) * 0.5f;
                int col = Mathf.FloorToInt(rx);
                float fx = rx - col, fy = ry - row;
                float mortar = 1 - Mathf.SmoothStep(0.02f, 0.05f, Mathf.Min(Mathf.Min(fx, 1 - fx) * 0.5f, Mathf.Min(fy, 1 - fy)));
                float tone = ProcArt.Hash01(((col % 4) + 4) % 4, row, 31);
                float n = ProcArt.FbmTile(u, v, 16, 4, 32);
                c = new Color(0.58f, 0.42f, 0.31f) * (0.85f + 0.2f * tone) * (0.9f + 0.2f * n);
                c = Color.Lerp(c, new Color(0.36f, 0.26f, 0.2f), mortar);
                c.a = 1;
                h = 1 - mortar * 0.8f + n * 0.15f;
            });

            // Habitat outside: white composite panels with seams and a little dust.
            Tex["HabOut"] = ProcArt.Surface("HabExterior", 512, 3f, (float u, float v, out Color c, out float h) =>
            {
                float su = u * 4 - Mathf.Floor(u * 4), sv = v * 2 - Mathf.Floor(v * 2);
                float seam = 1 - Mathf.SmoothStep(0.004f, 0.012f, Mathf.Min(Mathf.Min(su, 1 - su), Mathf.Min(sv, 1 - sv) * 0.5f));
                float n = ProcArt.FbmTile(u, v, 6, 4, 41), dust = ProcArt.FbmTile(u, v, 3, 3, 42);
                c = new Color(0.86f, 0.85f, 0.82f) * (0.96f + 0.05f * n);
                c = Color.Lerp(c, new Color(0.72f, 0.55f, 0.42f), Mathf.Clamp01(dust - 0.45f) * 0.6f);
                c = Color.Lerp(c, new Color(0.45f, 0.44f, 0.43f), seam * 0.8f);
                c.a = 1;
                h = 1 - seam;
            });

            // Habitat inside: soft grey panels, seams, small fasteners.
            Tex["HabIn"] = ProcArt.Surface("HabInterior", 512, 3f, (float u, float v, out Color c, out float h) =>
            {
                float su = u * 3 - Mathf.Floor(u * 3), sv = v * 2 - Mathf.Floor(v * 2);
                float edge = Mathf.Min(Mathf.Min(su, 1 - su), Mathf.Min(sv, 1 - sv) * 0.67f);
                float seam = 1 - Mathf.SmoothStep(0.004f, 0.012f, edge);
                float bolt = 0;
                foreach (var p in new[] { new Vector2(0.04f, 0.06f), new Vector2(0.96f, 0.06f), new Vector2(0.04f, 0.94f), new Vector2(0.96f, 0.94f) })
                    bolt = Mathf.Max(bolt, 1 - Mathf.SmoothStep(0.008f, 0.014f, Vector2.Distance(new Vector2(su, sv * 0.67f), new Vector2(p.x, p.y * 0.67f))));
                float n = ProcArt.FbmTile(u, v, 8, 3, 51);
                c = new Color(0.78f, 0.79f, 0.81f) * (0.97f + 0.04f * n);
                c = Color.Lerp(c, new Color(0.5f, 0.52f, 0.55f), seam * 0.7f + bolt * 0.5f);
                c.a = 1;
                h = 1 - seam + bolt * 0.5f;
            });

            // Floor grating.
            Tex["Grate"] = ProcArt.Surface("FloorGrate", 512, 7f, (float u, float v, out Color c, out float h) =>
            {
                float gx = u * 10 - Mathf.Floor(u * 10), gy = v * 40 - Mathf.Floor(v * 40);
                float bar = Mathf.Max(1 - Mathf.SmoothStep(0.08f, 0.14f, Mathf.Min(gx, 1 - gx)), 1 - Mathf.SmoothStep(0.18f, 0.26f, Mathf.Min(gy, 1 - gy)));
                float n = ProcArt.FbmTile(u, v, 8, 3, 61);
                c = Color.Lerp(new Color(0.07f, 0.07f, 0.08f), new Color(0.34f, 0.35f, 0.37f) * (0.9f + 0.2f * n), bar);
                c.a = 1;
                h = bar;
            });

            // Solar cells.
            Tex["Solar"] = ProcArt.Surface("SolarCells", 512, 2f, (float u, float v, out Color c, out float h) =>
            {
                float cx = u * 6 - Mathf.Floor(u * 6), cy = v * 10 - Mathf.Floor(v * 10);
                float grid = 1 - Mathf.SmoothStep(0.01f, 0.025f, Mathf.Min(Mathf.Min(cx, 1 - cx), Mathf.Min(cy, 1 - cy)));
                float fing = Mathf.Abs(cx - 0.33f) < 0.006f || Mathf.Abs(cx - 0.66f) < 0.006f ? 1 : 0;
                c = new Color(0.04f, 0.07f, 0.17f) * (0.85f + 0.3f * cy);
                c = Color.Lerp(c, new Color(0.55f, 0.58f, 0.62f), Mathf.Max(grid, fing * 0.6f));
                c.a = 1;
                h = grid;
            });

            // Brushed metal + suit fabric.
            Tex["Metal"] = ProcArt.Surface("Metal", 256, 2f, (float u, float v, out Color c, out float h) =>
            {
                float n = ProcArt.FbmTile(u, v, 4, 3, 71), s = ProcArt.FbmTile(u, v, 64, 2, 72);
                c = Color.white * (0.85f + 0.1f * n + 0.05f * s); c.a = 1;
                h = s * 0.3f;
            });
            Tex["Fabric"] = ProcArt.Surface("SuitFabric", 256, 3f, (float u, float v, out Color c, out float h) =>
            {
                float weave = Mathf.Sin(u * Mathf.PI * 2 * 48) * Mathf.Sin(v * Mathf.PI * 2 * 48);
                float n = ProcArt.FbmTile(u, v, 4, 3, 81);
                c = new Color(0.93f, 0.93f, 0.92f) * (0.95f + 0.05f * n + 0.02f * weave); c.a = 1;
                h = 0.5f + 0.5f * weave;
            });

            // Emissive console screen.
            Tex["Screen"] = new ProcArt.TexSet
            {
                albedo = ProcArt.Image("ConsoleScreen", 256, 128, (u, v) =>
                {
                    var c = new Color(0.01f, 0.03f, 0.05f);
                    float gx = u * 16 - Mathf.Floor(u * 16), gy = v * 8 - Mathf.Floor(v * 8);
                    if (gx < 0.04f || gy < 0.06f) c += new Color(0.0f, 0.06f, 0.08f);
                    for (int i = 0; i < 4; i++)
                    {
                        float y0 = 0.12f + i * 0.13f, len = 0.25f + ProcArt.Hash01(i, 5, 1) * 0.5f;
                        if (v > y0 && v < y0 + 0.05f && u > 0.08f && u < 0.08f + len) c = new Color(0.15f, 0.75f, 0.85f);
                    }
                    if (v > 0.8f && v < 0.83f && u > 0.08f && u < 0.92f) c = new Color(0.9f, 0.6f, 0.2f);
                    c.a = 1;
                    return c;
                }, TextureWrapMode.Clamp, false),
            };

            // Alpha textures: soft dust mote, light beam, aurora curtain.
            Tex["Mote"] = new ProcArt.TexSet
            {
                albedo = ProcArt.Image("SoftMote", 64, 64, (u, v) =>
                {
                    float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2;
                    return new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(1 - d), 2));
                }, TextureWrapMode.Clamp, true),
            };
            Tex["Beam"] = new ProcArt.TexSet
            {
                albedo = ProcArt.Image("Beam", 32, 128, (u, v) =>
                {
                    float x = 1 - Mathf.Abs(u - 0.5f) * 2;
                    return new Color(1, 1, 1, x * x * Mathf.Pow(1 - v, 1.6f));
                }, TextureWrapMode.Clamp, true),
            };
            Tex["Aurora"] = new ProcArt.TexSet
            {
                albedo = ProcArt.Image("AuroraCurtain", 256, 128, (u, v) =>
                {
                    float folds = 0.55f + 0.45f * Mathf.Sin((u * 7 + ProcArt.FbmTile(u, 0.5f, 4, 3, 91) * 2) * Mathf.PI * 2);
                    float a = Mathf.SmoothStep(0, 0.12f, v) * Mathf.Pow(1 - v, 1.3f) * folds;
                    a *= Mathf.SmoothStep(0, 0.15f, u) * Mathf.SmoothStep(1, 0.85f, u);
                    var c = Color.Lerp(new Color(0.4f, 1f, 0.6f), new Color(0.9f, 0.4f, 0.6f), Mathf.SmoothStep(0.5f, 1f, v));
                    c.a = a;
                    return c;
                }, TextureWrapMode.Clamp, true),
            };
        }

        // ───────────────────────── sky + light + post ─────────────────────────

        static Material SetupSkyAndLight(out Light sun)
        {
            var sky = new Material(Shader.Find("MissionTrainer/MarsSky"));
            sky = SaveMat("MarsSky", sky);

            sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.8f;
            sun.color = new Color(1f, 0.94f, 0.84f);
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sun.transform.rotation = Quaternion.Euler(30, -40, 0);   // low afternoon sun: long, readable shadows
            sky.SetVector("_SunDir", -sun.transform.forward);
            sky.SetFloat("_SunSize", 0.00004f);   // ~0.5° wide: small, like the real sun from Mars

            RenderSettings.skybox = sky;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.50f, 0.38f, 0.30f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.46f, 0.34f);
            RenderSettings.ambientGroundColor = new Color(0.30f, 0.19f, 0.13f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.0025f;
            RenderSettings.fogColor = new Color(0.86f, 0.66f, 0.48f);
            return sky;
        }

        static void BuildPostProcessing()
        {
            string path = Gen + "/MarsPostFX.asset";
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.35f);
            bloom.scatter.Override(0.65f);
            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.25f);
            color.contrast.Override(12f);
            color.saturation.Override(-6f);
            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.22f);
            vig.smoothness.Override(0.5f);
            foreach (var comp in profile.components) AssetDatabase.AddObjectToAsset(comp, profile);
            EditorUtility.SetDirty(profile);

            var vol = new GameObject("PostProcessing").AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = profile;
        }

        // ───────────────────────── terrain ─────────────────────────

        const float TerrainSize = 1200, TerrainHeight = 40, TerrainBase = -15;
        const int HeightRes = 1025, SplatRes = 512;

        static float GroundHeight(float x, float z)
        {
            float h = 2.4f * ProcArt.Fbm(x * 0.004f, z * 0.004f, 4, 11.3f)
                    + 0.6f * ProcArt.Fbm(x * 0.02f, z * 0.02f, 3, 4.7f)
                    + 0.12f * ProcArt.Fbm(x * 0.09f, z * 0.09f, 2, 9.1f);

            foreach (var c in Craters)
            {
                float r = Mathf.Sqrt((x - c.x) * (x - c.x) + (z - c.y) * (z - c.y));
                float R = c.z, depth = c.w;
                if (r > R * 2.2f) continue;
                if (r < R) h -= depth * (1 - (r / R) * (r / R));
                float k = (r - R) / (0.35f * R);
                h += depth * 0.28f * Mathf.Exp(-k * k);
            }

            float rr = Mathf.Sqrt(x * x + z * z);
            // low hills on the far horizon hide the terrain edge
            h += Mathf.SmoothStep(0, 1, Mathf.InverseLerp(330, 560, rr)) * (9 + 14 * (ProcArt.Fbm(x * 0.006f, z * 0.006f, 3, 2.2f) + 0.5f));
            // flat pads where the base and the shelter are built
            float ds = Mathf.Sqrt((x - ShelterPos.x) * (x - ShelterPos.x) + (z - ShelterPos.z) * (z - ShelterPos.z));
            float pad = Mathf.Min(Mathf.InverseLerp(22, 42, rr), Mathf.InverseLerp(9, 18, ds));
            h = Mathf.Lerp(0, h, Mathf.SmoothStep(0, 1, pad));
            return h;
        }

        static Terrain BuildTerrain()
        {
            string dataPath = Gen + "/MarsTerrain.asset";
            AssetDatabase.DeleteAsset(dataPath);
            var data = new TerrainData { heightmapResolution = HeightRes, alphamapResolution = SplatRes, baseMapResolution = 512 };
            data.size = new Vector3(TerrainSize, TerrainHeight, TerrainSize);

            var heights = new float[HeightRes, HeightRes];
            for (int iz = 0; iz < HeightRes; iz++)
            for (int ix = 0; ix < HeightRes; ix++)
            {
                float x = ix / (float)(HeightRes - 1) * TerrainSize - TerrainSize / 2;
                float z = iz / (float)(HeightRes - 1) * TerrainSize - TerrainSize / 2;
                heights[iz, ix] = Mathf.Clamp01((GroundHeight(x, z) - TerrainBase) / TerrainHeight);
            }
            data.SetHeights(0, 0, heights);

            data.terrainLayers = new[]
            {
                Layer("Dust", Tex["Dust"], 6f, 0.04f),
                Layer("RippleSand", Tex["Sand"], 9f, 0.06f),
                Layer("Outcrop", Tex["Outcrop"], 11f, 0.08f),
            };

            var alpha = new float[SplatRes, SplatRes, 3];
            for (int iz = 0; iz < SplatRes; iz++)
            for (int ix = 0; ix < SplatRes; ix++)
            {
                float nx = ix / (float)(SplatRes - 1), nz = iz / (float)(SplatRes - 1);
                float x = nx * TerrainSize - TerrainSize / 2, z = nz * TerrainSize - TerrainSize / 2;
                float r = Mathf.Sqrt(x * x + z * z);

                // Broad, soft patches (tens of meters) so the ground varies gently instead of in spots.
                float sand = 0.55f * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.02f, 0.3f, ProcArt.Fbm(x * 0.006f, z * 0.006f, 3, 5.5f)));
                float rock = 0.7f * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.18f, 0.34f, ProcArt.Fbm(x * 0.004f, z * 0.004f, 3, 21.7f)));
                foreach (var c in Craters)
                {
                    float cr = Mathf.Sqrt((x - c.x) * (x - c.x) + (z - c.y) * (z - c.y));
                    float k = (cr - c.z * 1.05f) / (0.45f * c.z);
                    rock = Mathf.Max(rock, 0.75f * Mathf.Exp(-k * k));                       // bedrock shows on the rim
                    sand = Mathf.Max(sand, 0.7f * (1 - Mathf.SmoothStep(0.4f, 0.9f, cr / c.z))); // sand pools in the floor
                }
                rock = Mathf.Max(rock, Mathf.SmoothStep(0, 0.8f, Mathf.InverseLerp(10f, 24f, data.GetSteepness(nx, nz))));
                float pad = Mathf.InverseLerp(18, 34, r);   // trampled dust around the base
                sand *= pad; rock *= pad;

                float w2 = Mathf.Clamp01(rock), w1 = Mathf.Clamp01(sand) * (1 - w2), w0 = Mathf.Clamp01(1 - w1 - w2);
                float sum = w0 + w1 + w2;
                alpha[iz, ix, 0] = w0 / sum; alpha[iz, ix, 1] = w1 / sum; alpha[iz, ix, 2] = w2 / sum;
            }
            data.SetAlphamaps(0, 0, alpha);
            AssetDatabase.CreateAsset(data, dataPath);

            var go = Terrain.CreateTerrainGameObject(data);
            go.name = "MeridianiPlanum";
            go.transform.position = new Vector3(-TerrainSize / 2, TerrainBase, -TerrainSize / 2);
            var t = go.GetComponent<Terrain>();
            t.heightmapPixelError = 5;
            t.basemapDistance = 220;
            t.drawInstanced = false;   // WebGL2 friendly
            t.shadowCastingMode = ShadowCastingMode.On;
            var tmat = GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.defaultTerrainMaterial : null;
            if (tmat != null) t.materialTemplate = tmat;
            go.isStatic = true;
            return t;
        }

        static TerrainLayer Layer(string name, ProcArt.TexSet tex, float tile, float smooth)
        {
            string path = $"{Gen}/{name}.terrainlayer";
            AssetDatabase.DeleteAsset(path);
            var l = new TerrainLayer
            {
                diffuseTexture = tex.albedo,
                normalMapTexture = tex.normal,
                tileSize = new Vector2(tile, tile),
                normalScale = 1f,
                smoothness = smooth,
                metallic = 0,
            };
            AssetDatabase.CreateAsset(l, path);
            return l;
        }

        static float H(float x, float z) => terrain.SampleHeight(new Vector3(x, 0, z)) + terrain.transform.position.y;

        static Quaternion GroundTilt(float x, float z, float yaw, float amount)
        {
            var n = terrain.terrainData.GetInterpolatedNormal((x + TerrainSize / 2) / TerrainSize, (z + TerrainSize / 2) / TerrainSize);
            return Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(Vector3.up, n), amount) * Quaternion.Euler(0, yaw, 0);
        }

        // ───────────────────────── rocks ─────────────────────────

        static void ScatterRocks()
        {
            var root = new GameObject("Rocks").transform;
            var rockMat = LitMat("Rock", Color.white, Tex["Rock"], 1f, 0.15f, 0, true);
            var slabMat = LitMat("OutcropSlab", Color.white, Tex["Outcrop"], 0.5f, 0.1f, 0, true);
            var rocks = new Mesh[8];
            for (int i = 0; i < rocks.Length; i++) rocks[i] = ProcArt.SaveMesh(ProcArt.Rock(100 + i, 0.75f), "Rock" + i);
            var slabs = new Mesh[4];
            for (int i = 0; i < slabs.Length; i++) slabs[i] = ProcArt.SaveMesh(ProcArt.Rock(200 + i, 0.22f), "Slab" + i);

            var avoid = new List<Vector3> { new Vector3(0, 0, 0), ShelterPos, RoverPos, AntennaPos, new Vector3(-18, 0, -8) };
            var avoidR = new List<float> { 16, 9, 5, 4, 10 };
            foreach (var s in SampleSites) { avoid.Add(new Vector3(s.x, 0, s.y)); avoidR.Add(3); }

            bool Blocked(float x, float z)
            {
                for (int i = 0; i < avoid.Count; i++)
                    if ((new Vector2(x - avoid[i].x, z - avoid[i].z)).sqrMagnitude < avoidR[i] * avoidR[i]) return true;
                return false;
            }

            var rng = new System.Random(7);
            float R() => (float)rng.NextDouble();
            int placed = 0;
            for (int tries = 0; placed < 340 && tries < 4000; tries++)
            {
                float a = R() * Mathf.PI * 2, d = 10 + Mathf.Pow(R(), 0.8f) * 240;
                float x = Mathf.Cos(a) * d, z = Mathf.Sin(a) * d;
                if (Blocked(x, z)) continue;
                bool boulder = R() < 0.05f;
                float s = boulder ? 0.9f + R() * 1.4f : 0.1f + Mathf.Pow(R(), 3) * 0.8f;
                var go = new GameObject("Rock" + placed);
                go.transform.SetParent(root, false);
                go.transform.position = new Vector3(x, H(x, z) - s * 0.25f, z);
                go.transform.rotation = GroundTilt(x, z, R() * 360, 0.7f);
                go.transform.localScale = new Vector3(s, s, s);
                go.AddComponent<MeshFilter>().sharedMesh = rocks[rng.Next(rocks.Length)];
                go.AddComponent<MeshRenderer>().sharedMaterial = rockMat;
                if (s > 0.5f) { var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh; mc.convex = true; }
                go.isStatic = true;
                placed++;
            }

            // Pale bedrock slabs around crater rims and in patches.
            int slabCount = 0;
            for (int tries = 0; slabCount < 70 && tries < 3000; tries++)
            {
                float x, z;
                if (R() < 0.6f)
                {
                    var c = Craters[rng.Next(3)];
                    float a = R() * Mathf.PI * 2, d = c.z * (0.9f + R() * 0.5f);
                    x = c.x + Mathf.Cos(a) * d; z = c.y + Mathf.Sin(a) * d;
                }
                else { float a = R() * Mathf.PI * 2, d = 25 + R() * 150; x = Mathf.Cos(a) * d; z = Mathf.Sin(a) * d; }
                if (Blocked(x, z)) continue;
                var go = new GameObject("Slab" + slabCount);
                go.transform.SetParent(root, false);
                float s = 0.8f + R() * 2.2f;
                go.transform.position = new Vector3(x, H(x, z) - 0.06f * s, z);
                go.transform.rotation = GroundTilt(x, z, R() * 360, 1f);
                go.transform.localScale = new Vector3(s, s, s);
                go.AddComponent<MeshFilter>().sharedMesh = slabs[rng.Next(slabs.Length)];
                go.AddComponent<MeshRenderer>().sharedMaterial = slabMat;
                go.isStatic = true;
                slabCount++;
            }
        }

        // ───────────────────────── habitat ─────────────────────────

        static bool IsDoor(float centerDeg, float doorDeg, float halfWidthDeg) => Mathf.Abs(Mathf.DeltaAngle(centerDeg, doorDeg)) < halfWidthDeg;

        static Light BuildHabitat(MissionRunner runner, out Renderer warningLamp, out Renderer[] solarPanels)
        {
            var hab = new GameObject("Habitat").transform;
            var outMat = LitMat("HabExterior", Color.white, Tex["HabOut"], 1, 0.35f, 0);
            var inMat = LitMat("HabInterior", Color.white, Tex["HabIn"], 1, 0.3f, 0);
            var floorMat = LitMat("FloorGrate", Color.white, Tex["Grate"], 10, 0.45f, 0.5f);
            var metalDark = LitMat("MetalDark", new Color(0.2f, 0.21f, 0.23f), Tex["Metal"], 2, 0.5f, 0.6f);
            var metalLight = LitMat("MetalLight", new Color(0.72f, 0.74f, 0.77f), Tex["Metal"], 2, 0.55f, 0.7f);
            var bagMat = LitMat("RegolithBags", new Color(0.95f, 0.85f, 0.78f), Tex["Dust"], 3, 0.05f, 0);

            const int segs = 48;
            const float offset = -3.75f;   // puts a segment center exactly at 90° (the door)
            bool DoorCut(int s, float center, float midY) => IsDoor(center, 90, 12) && midY < DoorTop;

            MeshObj(hab, "HabShellOutside", ProcArt.SaveMesh(ProcArt.Lathe(ProcArt.WallDome(HabOuterR, HabWall, DoorTop, 3.3f, 10), segs, false, 12, 3.2f, DoorCut, offset), "HabShellOut"), outMat, true);
            MeshObj(hab, "HabShellInside", ProcArt.SaveMesh(ProcArt.Lathe(ProcArt.WallDome(HabInnerR, HabWall, DoorTop, 3.0f, 10), segs, true, 12, 3.2f, DoorCut, offset), "HabShellIn"), inMat, true);
            MeshObj(hab, "SandbagBerm", ProcArt.SaveMesh(ProcArt.Lathe(new[] { new Vector2(6.45f, -0.3f), new Vector2(6.8f, 0.5f), new Vector2(7.3f, 0.55f), new Vector2(7.9f, -0.3f) }, segs, false, 24, 1.5f,
                (s, c, y) => IsDoor(c, 90, 16), offset), "HabBerm"), bagMat, false);
            MeshObj(hab, "RingLight", ProcArt.SaveMesh(ProcArt.Lathe(new[] { new Vector2(6.13f, 3.02f), new Vector2(5.98f, 3.1f), new Vector2(6.13f, 3.18f) }, segs, true, 1, 1), "HabRingLight"),
                LitMat("RingLight", new Color(1f, 0.95f, 0.85f), null, 1, 0.5f, 0, false, new Color(1f, 0.9f, 0.75f) * 1.6f), false);

            var floor = Prim(PrimitiveType.Cylinder, hab, "Floor", new Vector3(0, -0.05f, 0), new Vector3(HabInnerR * 2 + 0.1f, 0.05f, HabInnerR * 2 + 0.1f), floorMat, false);
            floor.AddComponent<MeshCollider>().sharedMesh = floor.GetComponent<MeshFilter>().sharedMesh;

            // Door frame (covers the gap between inner and outer shells)
            foreach (float side in new[] { -1f, 1f })
            {
                float a = (90 + side * 11.6f) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var j = Prim(PrimitiveType.Cube, hab, "DoorJamb", dir * 6.3f + Vector3.up * DoorTop / 2, new Vector3(0.22f, DoorTop, 0.5f), metalDark);
                j.transform.rotation = Quaternion.LookRotation(dir);
            }
            Prim(PrimitiveType.Cube, hab, "DoorLintel", new Vector3(0, DoorTop + 0.1f, 6.3f), new Vector3(2.7f, 0.2f, 0.5f), metalDark);

            // Tunnel airlock
            var tunnel = new GameObject("Airlock").transform;
            tunnel.SetParent(hab, false);
            tunnel.localPosition = new Vector3(0, 1.3f, 5.9f);
            tunnel.localRotation = Quaternion.Euler(90, 0, 0);
            MeshObj(tunnel, "TunnelInside", ProcArt.SaveMesh(ProcArt.Lathe(new[] { new Vector2(1.4f, 0), new Vector2(1.4f, 4.6f) }, 24, true, 4, 3.2f), "TunnelIn"), inMat, true);
            MeshObj(tunnel, "TunnelOutside", ProcArt.SaveMesh(ProcArt.Lathe(new[] { new Vector2(1.55f, 0), new Vector2(1.55f, 4.6f) }, 24, false, 4, 3.2f), "TunnelOut"), outMat, false);
            MeshObj(tunnel, "HatchRing", ProcArt.SaveMesh(ProcArt.Lathe(new[] { new Vector2(1.3f, 4.5f), new Vector2(1.7f, 4.5f), new Vector2(1.7f, 4.75f), new Vector2(1.3f, 4.75f) }, 24, false, 2, 1), "HatchRing"), metalDark, false);
            Prim(PrimitiveType.Cube, hab, "TunnelFloor", new Vector3(0, -0.05f, 8.2f), new Vector3(2.2f, 0.1f, 4.8f), floorMat);
            Label(hab, "AIRLOCK", new Vector3(0, 2.15f, 5.95f), Quaternion.identity, 0.004f, 40, new Color(1, 1, 1, 0.85f), false);

            // Lights: one warm point light, no shadows (cheap); the ring strip adds glow via bloom.
            var light = new GameObject("HabitatLight").AddComponent<Light>();
            light.transform.SetParent(hab, false);
            light.transform.localPosition = new Vector3(0, 3.4f, 0);
            light.type = LightType.Point;
            light.range = 12;
            light.intensity = 1.6f;
            light.color = new Color(1f, 0.92f, 0.82f);
            light.shadows = LightShadows.None;

            var lampBase = Prim(PrimitiveType.Cylinder, hab, "WarningLampBase", new Vector3(0, 2.95f, 5.7f), new Vector3(0.22f, 0.04f, 0.22f), metalDark, false);
            warningLamp = Prim(PrimitiveType.Sphere, hab, "WarningLamp", new Vector3(0, 2.88f, 5.7f), new Vector3(0.18f, 0.12f, 0.18f),
                LitMat("WarningLamp", new Color(0.3f, 0.08f, 0.08f), null, 1, 0.8f, 0, false, Color.black), false).GetComponent<Renderer>();

            // Consoles around the wall (facing the center)
            Transform At(string name, float deg, float radius)
            {
                var t = new GameObject(name).transform;
                t.SetParent(hab, false);
                var dir = new Vector3(Mathf.Cos(deg * Mathf.Deg2Rad), 0, Mathf.Sin(deg * Mathf.Deg2Rad));
                t.localPosition = dir * radius;
                t.localRotation = Quaternion.LookRotation(-dir);
                return t;
            }
            var screenMat = LitMat("ConsoleScreen", Color.white, Tex["Screen"], 1, 0.9f, 0, false, Color.white * 1.2f, Tex["Screen"].albedo);

            Transform Console(string id, float deg)
            {
                var root = At("Console_" + id, deg, 5.75f);
                Prim(PrimitiveType.Cube, root, "Cabinet", new Vector3(0, 0.48f, 0.02f), new Vector3(1.15f, 0.96f, 0.42f), metalDark);
                Prim(PrimitiveType.Cube, root, "Desk", new Vector3(0, 0.99f, 0.12f), new Vector3(1.25f, 0.05f, 0.55f), metalLight);
                Prim(PrimitiveType.Cube, root, "Back", new Vector3(0, 1.6f, -0.12f), new Vector3(1.2f, 1.2f, 0.08f), metalDark);
                Prim(PrimitiveType.Cube, root, "Screen", new Vector3(0, 1.68f, -0.075f), new Vector3(1.0f, 0.58f, 0.01f), screenMat, false);
                var ind = Prim(PrimitiveType.Cube, root, "StatusBar", new Vector3(0, 1.2f, -0.075f), new Vector3(0.7f, 0.05f, 0.012f),
                    LitMat("Indicator", new Color(0.25f, 0.25f, 0.3f), null, 1, 0.6f, 0, false, Color.black), false);
                var p = root.gameObject.AddComponent<SystemPanel>();
                p.runner = runner;
                p.systemId = id;
                p.indicator = ind.GetComponent<Renderer>();
                p.label = Label(root, id, new Vector3(0, 2.06f, -0.07f), Quaternion.Euler(0, 180, 0), 0.0032f, 44, new Color(0.85f, 0.92f, 1f), false);
                return root;
            }

            var lifeSupport = Console(SystemIds.LifeSupport, 140);
            var heater = Console(SystemIds.Heater, 172);
            var green = Console(SystemIds.Greenhouse, 205);
            var radio = Console(SystemIds.Radio, 40);
            var science = Console(SystemIds.Science, 8);

            // Life support: oxygen tanks
            var tankMat = LitMat("O2Tank", new Color(0.6f, 0.75f, 0.88f), Tex["Metal"], 1, 0.6f, 0.4f);
            foreach (float x in new[] { -0.85f, 0.85f })
            {
                Prim(PrimitiveType.Capsule, lifeSupport, "O2Tank", new Vector3(x, 0.75f, -0.05f), new Vector3(0.38f, 0.65f, 0.38f), tankMat);
            }
            // Heater: radiator fins + glow when on
            for (int i = 0; i < 7; i++)
                Prim(PrimitiveType.Cube, heater, "Fin" + i, new Vector3(-0.45f + i * 0.15f, 0.5f, 0.26f), new Vector3(0.03f, 0.6f, 0.06f), metalLight, false);
            heater.GetComponent<SystemPanel>().showWhenOn = new[]
            {
                Prim(PrimitiveType.Cube, heater, "HeaterGlow", new Vector3(0, 0.5f, 0.23f), new Vector3(1.0f, 0.55f, 0.01f),
                    LitMat("HeaterGlow", new Color(1f, 0.4f, 0.1f), null, 1, 0.2f, 0, false, new Color(1f, 0.3f, 0.05f) * 2.2f), false),
            };
            // Greenhouse: racks of plants + grow lights when on
            var soil = LitMat("Soil", new Color(0.24f, 0.16f, 0.11f), Tex["Dust"], 2, 0.05f, 0);
            var leaf = LitMat("Leaf", new Color(0.28f, 0.58f, 0.25f), null, 1, 0.35f, 0);
            var rack = At("GreenhouseRack", 228, 5.6f);
            for (int shelf = 0; shelf < 3; shelf++)
            {
                float y = 0.35f + shelf * 0.7f;
                Prim(PrimitiveType.Cube, rack, "Shelf" + shelf, new Vector3(0, y, 0), new Vector3(1.6f, 0.04f, 0.5f), metalLight);
                Prim(PrimitiveType.Cube, rack, "Tray" + shelf, new Vector3(0, y + 0.07f, 0), new Vector3(1.5f, 0.1f, 0.42f), soil, false);
                for (int p = 0; p < 6; p++)
                {
                    float px = -0.62f + p * 0.25f;
                    var lf = Prim(PrimitiveType.Sphere, rack, "Leaf", new Vector3(px, y + 0.2f, ProcArt.Hash01(p, shelf, 3) * 0.1f - 0.05f),
                        new Vector3(0.2f, 0.12f + ProcArt.Hash01(p, shelf, 4) * 0.1f, 0.2f), leaf, false);
                    lf.transform.localRotation = Quaternion.Euler(0, p * 47, 0);
                }
            }
            Prim(PrimitiveType.Cube, rack, "Post", new Vector3(-0.78f, 1.0f, 0), new Vector3(0.04f, 2.0f, 0.04f), metalLight, false);
            Prim(PrimitiveType.Cube, rack, "Post2", new Vector3(0.78f, 1.0f, 0), new Vector3(0.04f, 2.0f, 0.04f), metalLight, false);
            var growMat = LitMat("GrowLight", new Color(0.9f, 0.4f, 1f), null, 1, 0.2f, 0, false, new Color(0.9f, 0.35f, 1f) * 2.2f);
            var lights = new List<GameObject>();
            for (int shelf = 0; shelf < 3; shelf++)
                lights.Add(Prim(PrimitiveType.Cube, rack, "GrowLight" + shelf, new Vector3(0, 0.35f + shelf * 0.7f + 0.6f, 0), new Vector3(1.4f, 0.02f, 0.12f), growMat, false));
            green.GetComponent<SystemPanel>().showWhenOn = lights.ToArray();
            // Radio: rack with blinking-style lights
            Prim(PrimitiveType.Cube, radio, "RadioRack", new Vector3(0.95f, 0.9f, -0.05f), new Vector3(0.5f, 1.8f, 0.45f), metalDark);
            // Science: microscope + sample tray, screen glow when on
            Prim(PrimitiveType.Cylinder, science, "MicroscopeBase", new Vector3(0.35f, 1.05f, 0.15f), new Vector3(0.18f, 0.04f, 0.18f), metalDark, false);
            Prim(PrimitiveType.Cylinder, science, "MicroscopeTube", new Vector3(0.35f, 1.22f, 0.12f), new Vector3(0.06f, 0.16f, 0.06f), metalLight, false);
            Prim(PrimitiveType.Cube, science, "SampleTray", new Vector3(-0.3f, 1.04f, 0.18f), new Vector3(0.4f, 0.04f, 0.25f), metalLight, false);
            science.GetComponent<SystemPanel>().showWhenOn = new[]
            {
                Prim(PrimitiveType.Cube, science, "LabLight", new Vector3(-0.3f, 1.07f, 0.18f), new Vector3(0.36f, 0.01f, 0.21f),
                    LitMat("LabGlow", new Color(0.4f, 0.8f, 1f), null, 1, 0.5f, 0, false, new Color(0.3f, 0.7f, 1f) * 1.8f), false),
            };

            // Bunk (sleep = next sol)
            var bunk = At("SleepPod", 262, 5.2f);
            Prim(PrimitiveType.Cube, bunk, "Frame", new Vector3(0, 0.25f, 0), new Vector3(2.1f, 0.5f, 0.95f), metalDark);
            Prim(PrimitiveType.Cube, bunk, "Mattress", new Vector3(0, 0.56f, 0), new Vector3(1.95f, 0.14f, 0.85f), LitMat("Mattress", new Color(0.32f, 0.42f, 0.58f), Tex["Fabric"], 3, 0.15f, 0));
            Prim(PrimitiveType.Cube, bunk, "Pillow", new Vector3(-0.75f, 0.68f, 0), new Vector3(0.35f, 0.1f, 0.6f), LitMat("Pillow", new Color(0.9f, 0.9f, 0.92f), Tex["Fabric"], 2, 0.15f, 0));
            Prim(PrimitiveType.Cube, bunk, "Canopy", new Vector3(0, 1.55f, -0.35f), new Vector3(2.1f, 0.06f, 0.4f), metalDark, false);
            bunk.gameObject.AddComponent<SleepPod>().runner = runner;

            // Suit stand
            var locker = At("SuitLocker", 300, 5.55f);
            Prim(PrimitiveType.Cube, locker, "Alcove", new Vector3(0, 1.2f, -0.25f), new Vector3(1.3f, 2.4f, 0.12f), metalDark);
            Prim(PrimitiveType.Cylinder, locker, "Stand", new Vector3(0, 0.03f, 0.15f), new Vector3(0.8f, 0.03f, 0.8f), metalLight, false);
            var display = BuildAstronaut(locker, new Vector3(0, 0.06f, 0.15f));
            var sl = locker.gameObject.AddComponent<SuitLocker>();
            sl.runner = runner;
            sl.display = display;

            // Table + stools in the middle
            Prim(PrimitiveType.Cylinder, hab, "Table", new Vector3(-1.4f, 0.75f, -1.2f), new Vector3(1.4f, 0.03f, 1.4f), metalLight);
            Prim(PrimitiveType.Cylinder, hab, "TableLeg", new Vector3(-1.4f, 0.37f, -1.2f), new Vector3(0.12f, 0.37f, 0.12f), metalDark, false);
            foreach (var p in new[] { new Vector3(-0.3f, 0, -1.5f), new Vector3(-2.3f, 0, -0.6f) })
                Prim(PrimitiveType.Cylinder, hab, "Stool", p + Vector3.up * 0.23f, new Vector3(0.45f, 0.23f, 0.45f), metalDark);

            // ── Outside: solar field, antenna
            var solarMat = LitMat("SolarPanel", Color.white, Tex["Solar"], 1, 0.85f, 0.2f);
            var solarRoot = new GameObject("SolarField").transform;
            var panels = new List<Renderer>();
            for (int row = 0; row < 2; row++)
            for (int i = 0; i < 5; i++)
            {
                float x = -26 + i * 3.4f, z = -12 + row * 4.2f;
                var p = new GameObject("SolarPanel").transform;
                p.SetParent(solarRoot, false);
                p.position = new Vector3(x, H(x, z), z);
                p.rotation = Quaternion.Euler(0, 40, 0);
                Prim(PrimitiveType.Cylinder, p, "Leg", new Vector3(0, 0.45f, 0), new Vector3(0.08f, 0.45f, 0.08f), metalLight, false);
                var panel = Prim(PrimitiveType.Cube, p, "Panel", new Vector3(0, 0.95f, 0), new Vector3(3.0f, 0.04f, 1.8f), solarMat);
                panel.transform.localRotation = Quaternion.Euler(18, 0, 0);
                panels.Add(panel.GetComponent<Renderer>());
            }
            solarPanels = panels.ToArray();

            var ant = new GameObject("Antenna").transform;
            ant.position = new Vector3(AntennaPos.x, H(AntennaPos.x, AntennaPos.z), AntennaPos.z);
            Prim(PrimitiveType.Cylinder, ant, "Mast", new Vector3(0, 1.6f, 0), new Vector3(0.14f, 1.6f, 0.14f), metalLight);
            var dishMat = LitMat("Dish", new Color(0.9f, 0.9f, 0.9f), Tex["HabOut"], 1, 0.4f, 0.1f, false, null, null, true);
            var dish = MeshObj(ant, "Dish", ProcArt.SaveMesh(ProcArt.Lathe(Paraboloid(1.3f, 0.35f, 10), 32, false, 1, 1), "Dish"), dishMat, false);
            dish.transform.localPosition = new Vector3(0, 3.3f, 0);
            dish.transform.localRotation = Quaternion.Euler(-35, 60, 0);
            Prim(PrimitiveType.Cylinder, dish.transform, "Feed", new Vector3(0, 0.55f, 0), new Vector3(0.04f, 0.55f, 0.04f), metalDark, false);

            return light;
        }

        static Vector2[] Paraboloid(float radius, float depth, int steps)
        {
            var p = new Vector2[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                float r = Mathf.Max(0.001f, radius * i / steps);
                p[i] = new Vector2(r, depth * (r / radius) * (r / radius));
            }
            return p;
        }

        // ───────────────────────── shelter, rover, samples, aurora ─────────────────────────

        static Bounds BuildShelter()
        {
            var root = new GameObject("RadiationShelter").transform;
            float y = H(ShelterPos.x, ShelterPos.z);
            root.position = new Vector3(ShelterPos.x, y, ShelterPos.z);
            var brick = LitMat("ShelterBrick", Color.white, Tex["Brick"], 3, 0.08f, 0);
            var regolith = LitMat("Regolith", new Color(0.95f, 0.88f, 0.82f), Tex["Dust"], 6, 0.03f, 0);
            var metalDark = Mats["MetalDark"];

            bool Door(int s, float center, float midY) => IsDoor(center, ShelterDoorAngle, 15) && midY < 2.3f;
            MeshObj(root, "Mound", ProcArt.SaveMesh(ProcArt.Lathe(ProcArt.Mound(6.2f, 3.6f, 12, -0.4f), 40, false, 10, 3, Door), "ShelterMound"), regolith, true);
            MeshObj(root, "Vault", ProcArt.SaveMesh(ProcArt.Lathe(ProcArt.Mound(3.4f, 2.7f, 10, 0f), 40, true, 8, 2, Door), "ShelterVault"), brick, true);
            var sFloor = Prim(PrimitiveType.Cylinder, root, "Floor", new Vector3(0, 0.02f, 0), new Vector3(6.9f, 0.02f, 6.9f), Mats["FloorGrate"], false);
            sFloor.AddComponent<MeshCollider>().sharedMesh = sFloor.GetComponent<MeshFilter>().sharedMesh;

            // Entrance passage: two walls and a lintel from the inner vault out through the mound
            float a = ShelterDoorAngle * Mathf.Deg2Rad;
            var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            var side = Vector3.Cross(Vector3.up, dir);
            var rot = Quaternion.LookRotation(dir);
            foreach (float s in new[] { -1f, 1f })
            {
                var w = Prim(PrimitiveType.Cube, root, "PassageWall", dir * 4.6f + side * s * 1.0f + Vector3.up * 1.15f, new Vector3(0.3f, 2.3f, 3.0f), brick);
                w.transform.localRotation = rot;
            }
            var lintel = Prim(PrimitiveType.Cube, root, "PassageRoof", dir * 4.6f + Vector3.up * 2.4f, new Vector3(2.3f, 0.25f, 3.0f), brick);
            lintel.transform.localRotation = rot;
            var frame = Prim(PrimitiveType.Cube, root, "DoorFrame", dir * 6.05f + Vector3.up * 2.4f, new Vector3(2.5f, 0.3f, 0.2f), metalDark);
            frame.transform.localRotation = rot;

            // Sign on a post, plus a soft light inside
            var post = new GameObject("Sign").transform;
            post.SetParent(root, false);
            post.localPosition = dir * 7.2f + side * 1.8f;
            post.localRotation = Quaternion.LookRotation(-dir);
            Prim(PrimitiveType.Cylinder, post, "Post", new Vector3(0, 0.8f, 0), new Vector3(0.06f, 0.8f, 0.06f), metalDark, false);
            Prim(PrimitiveType.Cube, post, "Board", new Vector3(0, 1.65f, 0), new Vector3(1.3f, 0.45f, 0.04f), LitMat("SignYellow", new Color(0.95f, 0.75f, 0.15f), null, 1, 0.3f, 0));
            // The visitor stands on the board's local -Z side looking +Z, so the label faces +Z (identity) to read correctly.
            Label(post, "RADIATION\nSHELTER", new Vector3(0, 1.65f, -0.025f), Quaternion.identity, 0.0035f, 40, new Color(0.1f, 0.08f, 0.05f), false).fontStyle = FontStyle.Bold;

            var l = new GameObject("ShelterLight").AddComponent<Light>();
            l.transform.SetParent(root, false);
            l.transform.localPosition = new Vector3(0, 2.1f, 0);
            l.type = LightType.Point;
            l.range = 5;
            l.intensity = 0.9f;
            l.color = new Color(1f, 0.85f, 0.65f);
            l.shadows = LightShadows.None;
            Prim(PrimitiveType.Cube, root, "Bench", new Vector3(-dir.x * 2.2f, 0.25f, -dir.z * 2.2f), new Vector3(2.4f, 0.5f, 0.6f), brick).transform.localRotation = rot;

            return new Bounds(root.position + Vector3.up * 1.2f, new Vector3(5.4f, 2.4f, 5.4f));
        }

        static void BuildRover(MissionRunner runner)
        {
            var root = new GameObject("Rover").transform;
            root.position = new Vector3(RoverPos.x, H(RoverPos.x, RoverPos.z), RoverPos.z);
            root.rotation = GroundTilt(RoverPos.x, RoverPos.z, 35, 1f);
            var body = new GameObject("Body").transform;
            body.SetParent(root, false);

            var white = LitMat("RoverWhite", new Color(0.82f, 0.81f, 0.78f), Tex["Metal"], 1, 0.35f, 0.2f);
            var gold = LitMat("GoldFoil", new Color(0.85f, 0.62f, 0.25f), Tex["Metal"], 2, 0.65f, 1f);
            var dark = LitMat("RoverDark", new Color(0.12f, 0.12f, 0.13f), Tex["Metal"], 1, 0.4f, 0.6f);
            var alu = LitMat("Aluminum", new Color(0.65f, 0.66f, 0.68f), Tex["Metal"], 1, 0.55f, 0.9f);
            var solar = Mats["SolarPanel"];

            Prim(PrimitiveType.Cube, body, "WarmBox", new Vector3(0, 0.62f, 0), new Vector3(0.95f, 0.38f, 1.25f), gold);
            Prim(PrimitiveType.Cube, body, "Deck", new Vector3(0, 0.83f, 0), new Vector3(1.05f, 0.04f, 1.35f), white);
            var main = Prim(PrimitiveType.Cube, body, "SolarMain", new Vector3(0, 0.87f, -0.1f), new Vector3(1.45f, 0.025f, 1.5f), solar);
            foreach (float s in new[] { -1f, 1f })
            {
                var wing = Prim(PrimitiveType.Cube, body, "SolarWing", new Vector3(s * 1.15f, 0.89f, -0.1f), new Vector3(0.85f, 0.025f, 1.3f), solar);
                wing.transform.localRotation = Quaternion.Euler(0, 0, -s * 6);
            }
            Prim(PrimitiveType.Cube, body, "SolarRear", new Vector3(0, 0.88f, -1.05f), new Vector3(1.2f, 0.025f, 0.55f), solar);
            // Mast with camera head (Pancam-style) and antennas
            Prim(PrimitiveType.Cylinder, body, "Mast", new Vector3(0.3f, 1.25f, 0.55f), new Vector3(0.06f, 0.4f, 0.06f), alu);
            var head = Prim(PrimitiveType.Cube, body, "CameraBar", new Vector3(0.3f, 1.7f, 0.58f), new Vector3(0.36f, 0.1f, 0.1f), white);
            foreach (float s in new[] { -1f, 1f })
            {
                var lens = Prim(PrimitiveType.Cylinder, body, "Lens", new Vector3(0.3f + s * 0.13f, 1.7f, 0.64f), new Vector3(0.05f, 0.02f, 0.05f), dark, false);
                lens.transform.localRotation = Quaternion.Euler(90, 0, 0);
            }
            var hga = MeshObj(body, "HighGainAntenna", AssetDatabase.LoadAssetAtPath<Mesh>(Gen + "/Meshes/Dish.asset"), Mats["Dish"], false);
            hga.transform.localPosition = new Vector3(-0.35f, 0.95f, 0.35f);
            hga.transform.localScale = Vector3.one * 0.2f;
            hga.transform.localRotation = Quaternion.Euler(-60, 20, 0);
            Prim(PrimitiveType.Cylinder, body, "LowGainAntenna", new Vector3(-0.35f, 1.15f, -0.45f), new Vector3(0.015f, 0.3f, 0.015f), alu, false);
            // Rocker-bogie suspension + 6 wheels
            for (int i = 0; i < 6; i++)
            {
                float x = i < 3 ? -0.68f : 0.68f;
                float z = -0.62f + (i % 3) * 0.6f;
                var w = Prim(PrimitiveType.Cylinder, body, "Wheel" + i, new Vector3(x, 0.13f, z), new Vector3(0.26f, 0.08f, 0.26f), alu);
                w.transform.localRotation = Quaternion.Euler(0, 0, 90);
                var hub = Prim(PrimitiveType.Cylinder, body, "Hub" + i, new Vector3(x * 1.07f, 0.13f, z), new Vector3(0.1f, 0.02f, 0.1f), dark, false);
                hub.transform.localRotation = Quaternion.Euler(0, 0, 90);
            }
            foreach (float s in new[] { -1f, 1f })
            {
                var rocker = Prim(PrimitiveType.Cube, body, "Rocker", new Vector3(s * 0.6f, 0.38f, 0), new Vector3(0.04f, 0.04f, 1.3f), alu, false);
                rocker.transform.localRotation = Quaternion.Euler(8, 0, 0);
                foreach (float z in new[] { -0.62f, 0f, 0.58f })
                    Prim(PrimitiveType.Cube, body, "Strut", new Vector3(s * 0.64f, 0.26f, z), new Vector3(0.03f, 0.25f, 0.03f), alu, false);
            }
            var rover = root.gameObject.AddComponent<RoverActor>();
            rover.runner = runner;
            rover.body = body;
            // collider for interaction
            var col = root.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.6f, 0);
            col.size = new Vector3(1.6f, 1.2f, 1.7f);
        }

        static void BuildSampleSites(MissionRunner runner)
        {
            var sampleMat = LitMat("SampleRock", new Color(0.55f, 0.55f, 0.62f), Tex["Rock"], 1, 0.4f, 0.15f);
            var beamMat = TransparentUnlit("SampleBeam", new Color(0.45f, 0.85f, 1f, 0.22f), true, Tex["Beam"].albedo);
            var flagMat = LitMat("Flag", new Color(0.95f, 0.45f, 0.15f), Tex["Fabric"], 1, 0.2f, 0, false, null, null, true);
            var pole = Mats["MetalLight"];
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Gen + "/Meshes/Rock3.asset");

            for (int i = 0; i < SampleSites.Length; i++)
            {
                var p = SampleSites[i];
                var site = new GameObject("SampleSite" + (i + 1)).transform;
                site.position = new Vector3(p.x, H(p.x, p.y), p.y);

                var rock = MeshObj(site, "Rock", mesh, sampleMat, false);
                rock.transform.localPosition = new Vector3(0, 0.08f, 0);
                rock.transform.localScale = Vector3.one * 0.35f;
                rock.transform.localRotation = Quaternion.Euler(0, i * 70, 0);
                var bc = rock.AddComponent<SphereCollider>();
                bc.radius = 1.2f;

                var marker = new GameObject("Marker").transform;
                marker.SetParent(site, false);
                Prim(PrimitiveType.Cylinder, marker, "FlagPole", new Vector3(0.45f, 0.6f, 0), new Vector3(0.015f, 0.6f, 0.015f), pole, false);
                var flag = Prim(PrimitiveType.Quad, marker, "Flag", new Vector3(0.6f, 1.08f, 0), new Vector3(0.3f, 0.18f, 1), flagMat, false);
                flag.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.TwoSided;
                for (int k = 0; k < 2; k++)
                {
                    var beam = Prim(PrimitiveType.Quad, marker, "Beam", new Vector3(0, 4f, 0), new Vector3(0.35f, 8f, 1), beamMat, false);
                    beam.transform.localRotation = Quaternion.Euler(0, k * 90, 0);
                    beam.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                }

                var ss = site.gameObject.AddComponent<SampleSite>();
                ss.runner = runner;
                ss.rock = rock;
                ss.marker = marker.gameObject;
            }
        }

        static GameObject BuildAurora(out Renderer[] renderers)
        {
            var root = new GameObject("Aurora");
            var mat = TransparentUnlit("Aurora", new Color(0.4f, 1f, 0.6f, 0.5f), true, Tex["Aurora"].albedo);
            var list = new List<Renderer>();
            for (int i = 0; i < 6; i++)
            {
                float a = (i / 6f) * Mathf.PI * 2 + 0.3f;
                var q = Prim(PrimitiveType.Quad, root.transform, "Curtain" + i, new Vector3(Mathf.Cos(a) * 170, 70 + (i % 2) * 14, Mathf.Sin(a) * 170),
                    new Vector3(200, 60, 1), mat, false);
                q.transform.rotation = Quaternion.LookRotation(new Vector3(Mathf.Cos(a), 0.1f, Mathf.Sin(a)));
                var r = q.GetComponent<Renderer>();
                r.shadowCastingMode = ShadowCastingMode.Off;
                list.Add(r);
            }
            renderers = list.ToArray();
            root.SetActive(false);
            return root;
        }

        // ───────────────────────── player + suit ─────────────────────────

        static GameObject BuildPlayer(MissionRunner runner, Bounds shelterBounds, out Camera cam, out ParticleSystem dust)
        {
            var player = new GameObject("Player");
            player.transform.position = new Vector3(1.5f, 0.05f, 0.5f);
            player.transform.rotation = Quaternion.Euler(0, -70, 0);   // looking at the consoles
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.32f;
            cc.center = new Vector3(0, 0.9f, 0);
            cc.stepOffset = 0.4f;
            cc.slopeLimit = 50;

            var camGo = new GameObject("PlayerCamera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(player.transform, false);
            camGo.transform.localPosition = new Vector3(0, 1.62f, 0);
            cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.04f;
            cam.farClipPlane = 800;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 68;
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<Interactor>();

            var fpc = player.AddComponent<FirstPersonController>();
            fpc.cameraPivot = camGo.transform;
            fpc.runner = runner;
            var loc = player.AddComponent<PlayerLocation>();
            loc.runner = runner;
            loc.habitatCenter = Vector3.zero;
            loc.habitatRadius = HabInnerR;
            loc.habitatHeight = 6.5f;
            loc.airlock = new Bounds(new Vector3(0, 1.4f, 8.2f), new Vector3(2.8f, 2.8f, 4.6f));
            loc.shelter = shelterBounds;

            // No body in view: a clean helmet-eye view reads as more real than primitive arms.
            // The full suit (with cosmetics) is on the suit stand in the habitat.

            // Blowing dust: one small particle system following the player (rate set from tau)
            var dustGo = new GameObject("Dust");
            dustGo.transform.SetParent(player.transform, false);
            dustGo.transform.localPosition = new Vector3(0, 2.5f, 0);
            dust = dustGo.AddComponent<ParticleSystem>();
            dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = dust.main;
            main.loop = true;
            main.duration = 5;
            main.startLifetime = 5;
            main.startSpeed = 0.3f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.25f);
            main.maxParticles = 300;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = true;
            var em = dust.emission;
            em.rateOverTime = 0;
            var shape = dust.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(28, 5, 28);
            var vel = dust.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(2f, 4.5f);
            vel.y = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
            vel.z = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            var col = dust.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.2f), new GradientAlphaKey(1, 0.8f), new GradientAlphaKey(0, 1) });
            col.color = grad;
            var pr = dustGo.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = TransparentUnlit("Dust", new Color(0.85f, 0.62f, 0.42f, 0.55f), false, Tex["Mote"].albedo);
            pr.renderMode = ParticleSystemRenderMode.Billboard;
            pr.shadowCastingMode = ShadowCastingMode.Off;

            return player;
        }

        /// <summary>Full suit for the stand: white fabric, colored trim, dark visor, backpack, patch.</summary>
        static AstronautSuit BuildAstronaut(Transform parent, Vector3 pos)
        {
            var root = new GameObject("AstronautDisplay").transform;
            root.SetParent(parent, false);
            root.localPosition = pos;
            var white = LitMat("SuitWhite", Color.white, Tex["Fabric"], 2, 0.25f, 0);
            var gloveMat = LitMat("SuitGlove", new Color(0.78f, 0.78f, 0.8f), Tex["Fabric"], 3, 0.3f, 0);
            var trim = LitMat("SuitTrim", new Color(0.2f, 0.5f, 1f), Tex["Fabric"], 2, 0.35f, 0);
            var visor = LitMat("SuitVisor", new Color(0.06f, 0.06f, 0.08f), null, 1, 0.97f, 0.6f);
            var pack = LitMat("SuitPack", new Color(0.85f, 0.85f, 0.86f), Tex["Metal"], 1, 0.35f, 0.1f);
            var dark = Mats["MetalDark"];

            Prim(PrimitiveType.Capsule, root, "Torso", new Vector3(0, 1.25f, 0), new Vector3(0.56f, 0.42f, 0.4f), white, false);
            Prim(PrimitiveType.Sphere, root, "Helmet", new Vector3(0, 1.84f, 0), Vector3.one * 0.40f, white, false);
            Prim(PrimitiveType.Cylinder, root, "NeckRing", new Vector3(0, 1.64f, 0), new Vector3(0.34f, 0.04f, 0.34f), dark, false);
            var visorR = Prim(PrimitiveType.Sphere, root, "Visor", new Vector3(0, 1.85f, 0.085f), new Vector3(0.33f, 0.25f, 0.27f), visor, false).GetComponent<Renderer>();
            Prim(PrimitiveType.Cube, root, "LifePack", new Vector3(0, 1.3f, -0.29f), new Vector3(0.5f, 0.62f, 0.24f), pack, false);
            Prim(PrimitiveType.Cube, root, "ChestBox", new Vector3(0, 1.28f, 0.2f), new Vector3(0.26f, 0.14f, 0.08f), dark, false);
            var stripes = new List<Renderer>();
            foreach (float s in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Capsule, root, "Arm", new Vector3(0.37f * s, 1.2f, 0), new Vector3(0.16f, 0.34f, 0.16f), white, false, Quaternion.Euler(0, 0, s * 8));
                Prim(PrimitiveType.Sphere, root, "Glove", new Vector3(0.42f * s, 0.86f, 0), new Vector3(0.12f, 0.14f, 0.1f), gloveMat, false);
                Prim(PrimitiveType.Capsule, root, "Leg", new Vector3(0.14f * s, 0.48f, 0), new Vector3(0.2f, 0.44f, 0.2f), white, false);
                Prim(PrimitiveType.Cube, root, "Boot", new Vector3(0.14f * s, 0.07f, 0.04f), new Vector3(0.18f, 0.14f, 0.28f), dark, false);
                stripes.Add(Prim(PrimitiveType.Cylinder, root, "ArmTrim", new Vector3(0.375f * s, 1.3f, 0), new Vector3(0.17f, 0.02f, 0.17f), trim, false, Quaternion.Euler(0, 0, s * 8)).GetComponent<Renderer>());
                stripes.Add(Prim(PrimitiveType.Cylinder, root, "LegTrim", new Vector3(0.14f * s, 0.55f, 0), new Vector3(0.21f, 0.02f, 0.21f), trim, false).GetComponent<Renderer>());
            }
            var patch = Prim(PrimitiveType.Cylinder, root, "Patch", new Vector3(-0.21f, 1.42f, 0.19f), new Vector3(0.1f, 0.005f, 0.1f), LitMat("Patch", new Color(0.95f, 0.55f, 0.2f), null, 1, 0.3f, 0), false);
            patch.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var cmd = Prim(PrimitiveType.Cube, root, "CommanderStripes", new Vector3(0, 1.05f, 0.205f), new Vector3(0.42f, 0.035f, 0.01f), LitMat("CommanderRed", new Color(0.8f, 0.08f, 0.08f), null, 1, 0.3f, 0), false);

            var suit = root.gameObject.AddComponent<AstronautSuit>();
            suit.trimRenderers = stripes.ToArray();
            suit.visorRenderers = new[] { visorR };
            suit.patchSlot = patch;
            suit.commanderStripes = cmd;
            SuitSetup.AddDecals(suit);   // replaces the placeholder patch with chest + shoulder decals
            return suit;
        }

        // ───────────────────────── helpers ─────────────────────────

        static GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 localPos, Vector3 localScale, Material mat,
            bool keepCollider = true, Quaternion? localRot = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            if (localRot.HasValue) go.transform.localRotation = localRot.Value;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static GameObject MeshObj(Transform parent, string name, Mesh mesh, Material mat, bool collider)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }

        static Text Label(Transform parent, string text, Vector3 localPos, Quaternion localRot, float scale, int fontSize, Color color, bool billboard)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(parent, false);
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(500, 200);
            rt.localPosition = localPos;
            rt.localRotation = localRot;
            rt.localScale = Vector3.one * scale;
            if (billboard) go.AddComponent<Billboard>();

            var t = new GameObject("Text", typeof(RectTransform)).AddComponent<Text>();
            t.transform.SetParent(go.transform, false);
            var tr = t.rectTransform;
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.offsetMin = tr.offsetMax = Vector2.zero;
            t.font = font;
            t.fontSize = fontSize;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = color;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.text = text;
            return t;
        }

        static Material SaveMat(string name, Material m)
        {
            string path = $"{MatDir}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                existing.shader = m.shader;
                existing.CopyPropertiesFromMaterial(m);
                Object.DestroyImmediate(m);
                m = existing;
            }
            else AssetDatabase.CreateAsset(m, path);
            EditorUtility.SetDirty(m);
            Mats[name] = m;
            return m;
        }

        /// <summary>URP Lit material saved as an asset. tiling repeats the texture; emission optional.</summary>
        static Material LitMat(string name, Color color, ProcArt.TexSet? tex, float tiling, float smoothness, float metallic,
            bool instancing = false, Color? emission = null, Texture2D emissionMap = null, bool doubleSided = false)
        {
            if (Mats.TryGetValue(name, out var cached)) return cached;
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            if (tex.HasValue)
            {
                if (tex.Value.albedo) m.SetTexture("_BaseMap", tex.Value.albedo);
                if (tex.Value.normal)
                {
                    m.SetTexture("_BumpMap", tex.Value.normal);
                    m.SetFloat("_BumpScale", 1f);
                    m.EnableKeyword("_NORMALMAP");
                }
                m.SetTextureScale("_BaseMap", new Vector2(tiling, tiling));
            }
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                if (emissionMap) m.SetTexture("_EmissionMap", emissionMap);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            if (doubleSided)
            {
                m.SetFloat("_Cull", 0);
                m.doubleSidedGI = true;
            }
            m.enableInstancing = instancing;
            return SaveMat(name, m);
        }

        /// <summary>URP Unlit, transparent (alpha or additive), double-sided, optional texture.</summary>
        static Material TransparentUnlit(string name, Color color, bool additive, Texture2D tex)
        {
            if (Mats.TryGetValue(name, out var cached)) return cached;
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetColor("_BaseColor", color);
            if (tex) m.SetTexture("_BaseMap", tex);
            m.SetFloat("_Surface", 1);
            m.SetFloat("_Blend", additive ? 2 : 0);
            m.SetFloat("_Cull", 0);
            m.SetFloat("_ZWrite", 0);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_DstBlendAlpha", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            return SaveMat(name, m);
        }
    }
}
