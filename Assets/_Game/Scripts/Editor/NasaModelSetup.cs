using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MissionTrainer.EditorTools
{
    /// <summary>
    /// Menu: Mission Trainer > Use NASA 3D Models.
    /// Puts NASA's public-domain 3D models (github.com/nasa/NASA-3D-Resources) into the Mars scene:
    ///  - Mars 2020 Perseverance Rover  -> the base's scout rover (sinks in soft sand, click for the rover report)
    ///  - Astronaut                     -> the suit on the suit stand (cosmetics recolor its stripe and visor)
    /// and rebuilds the habitat, solar panels, antenna and shelter with much more detail.
    /// NASA/JPL logos are removed from the textures (fictional Mission Control, see CLAUDE.md).
    /// Needs the glTFast + Draco packages (Packages/manifest.json). Safe to run again.
    /// </summary>
    public static class NasaModelSetup
    {
        const string Dir = "Assets/_Game/Art/NasaModels/";
        const string ScenePath = "Assets/_Game/Scenes/Mars.unity";

        enum Fit { Length, Footprint, Height }

        [MenuItem("Mission Trainer/Use NASA 3D Models")]
        public static void Run()
        {
            if (!File.Exists(ScenePath)) { EditorUtility.DisplayDialog("No Mars scene", "Run Build Mars Scene (3D) first.", "OK"); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath);
            string report = Apply();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("NASA 3D models: " + report);
            EditorUtility.DisplayDialog("NASA 3D models", report, "OK");
        }

        /// <summary>Works on the currently open scene. Returns a short report.</summary>
        public static string Apply()
        {
            PrepareTextures();
            var log = new StringBuilder();

            // ── Rover: Perseverance (real size: about 3 m long)
            var rover = Object.FindAnyObjectByType<RoverActor>();
            var perse = Load("Perseverance.glb");
            if (rover && rover.body && perse)
            {
                Clear(rover.body);
                var size = Place(Inst(perse), rover.body, 2.9f, Fit.Length, "Perseverance (NASA model)", out var model);
                ReplaceMaterial(model, "arm_graphics", Dir + "Perseverance_ArmGraphics_NoLogo.png", Dir + "Perseverance_ArmGraphics_NoLogo.mat");
                var col = rover.GetComponent<BoxCollider>();
                if (col) { col.center = new Vector3(0, size.y / 2, 0); col.size = size; }
                foreach (var l in rover.GetComponentsInChildren<Canvas>(true)) l.transform.localPosition = new Vector3(0, size.y + 0.6f, 0);
                log.AppendLine("Rover: Perseverance placed.");
            }
            else log.AppendLine("Rover: skipped (model not imported yet? Click into Unity and wait for the import).");

            // ── Habitat: detailed model built to fit our round interior exactly.
            // (NASA's Habitat Demonstration Unit was tried, but its shape is lower and oval: our rooms would poke through it.)
            UpgradeHabitat(log);
            UpgradeAirlock(log);
            BuildWeatherStation(log);

            // ── Suit stand: NASA astronaut suit (logo-free)
            var locker = Object.FindAnyObjectByType<SuitLocker>();
            var astro = Load("Astronaut.glb");
            if (locker && locker.display && astro)
            {
                var suit = locker.display;
                var keep = new[] { "PatchChest", "PatchShoulder" };
                for (int i = suit.transform.childCount - 1; i >= 0; i--)
                {
                    var c = suit.transform.GetChild(i);
                    if (!keep.Contains(c.name)) Object.DestroyImmediate(c.gameObject);
                }
                var astroInst = Inst(astro);
                StandUp(astroInst, "astnt1_2");   // the model is stored lying down
                var size = Place(astroInst, suit.transform, 1.85f, Fit.Height, "Astronaut (NASA model)", out var model);
                var r = model.GetComponentInChildren<Renderer>();
                var mats = r.sharedMaterials;
                int suitIdx = System.Array.FindIndex(mats, m => m && m.name.Contains("astnt1_1"));
                int visorIdx = System.Array.FindIndex(mats, m => m && m.name.Contains("astnt1_2"));
                ReplaceMaterial(model, "astnt1_1", Dir + "Astronaut_Suit_NoLogo.png", Dir + "Astronaut_Suit_NoLogo.mat");

                suit.modelRenderer = r;
                suit.modelSuitTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "Astronaut_Suit_NoLogo.png");
                suit.suitMaterialIndex = suitIdx < 0 ? 0 : suitIdx;
                suit.visorMaterialIndex = visorIdx;
                suit.trimRenderers = new Renderer[0];
                suit.visorRenderers = new Renderer[0];
                suit.commanderStripes = null;

                // Mission patch decal on the chest (the shoulder one would float off this body shape).
                var chest = suit.transform.Find("PatchChest");
                if (chest) { chest.localPosition = new Vector3(-0.12f, size.y * 0.72f, size.z * 0.5f + 0.02f); chest.localScale = Vector3.one * 0.12f; }
                var shoulder = suit.transform.Find("PatchShoulder");
                if (shoulder) Object.DestroyImmediate(shoulder.gameObject);
                suit.patchRenderers = chest ? new[] { chest.GetComponent<Renderer>() } : new Renderer[0];
                EditorUtility.SetDirty(suit);
                log.AppendLine("Suit stand: NASA astronaut suit placed (logos removed).");
            }
            else log.AppendLine("Suit stand: skipped.");

            UpgradeSolarField(log);
            UpgradeAntenna(log);
            RemoveShelter(log);   // radiation was removed from the game, so the shelter goes too (smaller download)

            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        // ───────── detailed props (no NASA model exists for these) ─────────

        const string MatDir = "Assets/_Game/Art/Materials/";
        static Material M(string name) => AssetDatabase.LoadAssetAtPath<Material>(MatDir + name + ".mat");

        static GameObject Part(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, Material mat, Vector3 euler = default, bool collider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        /// <summary>Framed 2x3 solar arrays on a torque tube with A-frame legs, braces and a junction box.</summary>
        static void UpgradeSolarField(StringBuilder log)
        {
            var field = GameObject.Find("SolarField");
            var cell = M("SolarPanel");
            var alu = M("MetalLight");
            var dark = M("MetalDark");
            if (!field || !cell || !alu || !dark) { log.AppendLine("Solar field: skipped."); return; }

            var modules = new System.Collections.Generic.List<Renderer>();
            foreach (Transform panel in field.transform)
            {
                Clear(panel);
                // Legs: two A-frames
                foreach (float x in new[] { -1.2f, 1.2f })
                {
                    Part(PrimitiveType.Cylinder, panel, "Leg", new Vector3(x, 0.45f, -0.25f), new Vector3(0.06f, 0.5f, 0.06f), alu, new Vector3(-14, 0, 0));
                    Part(PrimitiveType.Cylinder, panel, "Leg", new Vector3(x, 0.45f, 0.25f), new Vector3(0.06f, 0.5f, 0.06f), alu, new Vector3(14, 0, 0));
                    Part(PrimitiveType.Cube, panel, "Foot", new Vector3(x, 0.03f, 0), new Vector3(0.14f, 0.06f, 0.9f), dark);
                    Part(PrimitiveType.Cube, panel, "Brace", new Vector3(x, 0.38f, 0), new Vector3(0.03f, 0.03f, 0.45f), alu);
                }
                // Torque tube + tilted array
                var array = new GameObject("Array").transform;
                array.SetParent(panel, false);
                array.localPosition = new Vector3(0, 0.95f, 0);
                array.localRotation = Quaternion.Euler(18, 0, 0);
                Part(PrimitiveType.Cylinder, array, "TorqueTube", new Vector3(0, -0.06f, 0), new Vector3(0.08f, 1.55f, 0.08f), dark, new Vector3(0, 0, 90));
                Part(PrimitiveType.Cube, array, "FrameL", new Vector3(-1.52f, 0, 0), new Vector3(0.04f, 0.05f, 1.86f), alu);
                Part(PrimitiveType.Cube, array, "FrameR", new Vector3(1.52f, 0, 0), new Vector3(0.04f, 0.05f, 1.86f), alu);
                Part(PrimitiveType.Cube, array, "FrameF", new Vector3(0, 0, 0.92f), new Vector3(3.08f, 0.05f, 0.04f), alu);
                Part(PrimitiveType.Cube, array, "FrameB", new Vector3(0, 0, -0.92f), new Vector3(3.08f, 0.05f, 0.04f), alu);
                for (int ix = 0; ix < 3; ix++)
                for (int iz = 0; iz < 2; iz++)
                {
                    var m = Part(PrimitiveType.Cube, array, "Module", new Vector3(-1.0f + ix * 1.0f, 0.012f, -0.45f + iz * 0.9f), new Vector3(0.96f, 0.025f, 0.86f), cell,
                        default, collider: true);   // collider: click the panels to clean them
                    modules.Add(m.GetComponent<Renderer>());
                }
                Part(PrimitiveType.Cube, array, "JunctionBox", new Vector3(0.0f, -0.09f, -0.6f), new Vector3(0.22f, 0.08f, 0.16f), dark);
                Part(PrimitiveType.Cylinder, panel, "Cable", new Vector3(0, 0.05f, -0.75f), new Vector3(0.025f, 0.6f, 0.025f), dark, new Vector3(90, 0, 0));
            }
            var hv = Object.FindAnyObjectByType<HazardVisuals>();
            if (hv) { hv.solarPanels = modules.ToArray(); EditorUtility.SetDirty(hv); }
            log.AppendLine($"Solar field: {field.transform.childCount} detailed arrays.");
        }

        /// <summary>Three-leg lattice mast with cross-bracing, the dish on top and feed struts.</summary>
        static void UpgradeAntenna(StringBuilder log)
        {
            var ant = GameObject.Find("Antenna");
            var alu = M("MetalLight");
            var dark = M("MetalDark");
            if (!ant || !alu || !dark) { log.AppendLine("Antenna: skipped."); return; }
            var dish = ant.transform.Find("Dish");
            for (int i = ant.transform.childCount - 1; i >= 0; i--)
            {
                var c = ant.transform.GetChild(i);
                if (c != dish) Object.DestroyImmediate(c.gameObject);
            }
            const float h = 3.1f, r = 0.32f;
            var legs = new Vector3[3];
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI * 2 / 3;
                legs[i] = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
                Part(PrimitiveType.Cylinder, ant.transform, "Leg", legs[i] + Vector3.up * h / 2, new Vector3(0.05f, h / 2, 0.05f), alu, default, collider: i == 0);
            }
            for (int level = 0; level < 5; level++)
            {
                float y = 0.3f + level * 0.62f;
                for (int i = 0; i < 3; i++)
                {
                    var a = legs[i] + Vector3.up * y;
                    var b = legs[(i + 1) % 3] + Vector3.up * (y + 0.31f);
                    Strut(ant.transform, a, b, alu);
                    Strut(ant.transform, legs[i] + Vector3.up * y, legs[(i + 1) % 3] + Vector3.up * y, alu);
                }
            }
            Part(PrimitiveType.Cylinder, ant.transform, "TopPlate", new Vector3(0, h + 0.02f, 0), new Vector3(0.9f, 0.03f, 0.9f), dark);
            Part(PrimitiveType.Cube, ant.transform, "ElectronicsBox", new Vector3(0, 0.35f, 0), new Vector3(0.36f, 0.5f, 0.3f), dark);
            Part(PrimitiveType.Cylinder, ant.transform, "Cable", new Vector3(0, 0.05f, -0.8f), new Vector3(0.03f, 0.8f, 0.03f), dark, new Vector3(90, 0, 0));
            if (dish)
            {
                dish.localPosition = new Vector3(0, h + 0.25f, 0);
                // feed struts from the dish rim to the receiver
                for (int i = dish.childCount - 1; i >= 0; i--) Object.DestroyImmediate(dish.GetChild(i).gameObject);
                for (int i = 0; i < 3; i++)
                {
                    float a = i * Mathf.PI * 2 / 3;
                    Strut(dish, new Vector3(Mathf.Cos(a) * 1.1f, 0.28f, Mathf.Sin(a) * 1.1f), new Vector3(0, 0.95f, 0), dark, 0.02f);
                }
                Part(PrimitiveType.Cylinder, dish, "Receiver", new Vector3(0, 0.98f, 0), new Vector3(0.14f, 0.08f, 0.14f), dark);
            }
            log.AppendLine("Antenna: lattice mast built.");
        }

        // ───────── habitat (outside + inside) ─────────

        const float OuterR = 6.45f, InnerR = 6.15f, Wall = 3.2f, DoorTopY = 2.5f, OuterDome = 3.3f, InnerDome = 3.0f, DoorDeg = 90f;

        static bool NearDoor(float deg, float half) => Mathf.Abs(Mathf.DeltaAngle(deg, DoorDeg)) < half;
        static Vector3 Heading(float deg) => new Vector3(Mathf.Cos(deg * Mathf.Deg2Rad), 0, Mathf.Sin(deg * Mathf.Deg2Rad));

        /// <summary>Loads a material made by the scene builder, or makes a new URP Lit one.</summary>
        static Material Mat(string name, Color color, float smooth, float metal, Color? emission = null)
        {
            var m = M(name);
            if (m) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            if (emission.HasValue) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission.Value); }
            AssetDatabase.CreateAsset(m, MatDir + name + ".mat");
            return m;
        }

        static GameObject MeshGO(Transform parent, string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        /// <summary>A box from a to b. 'side' is a direction across the beam (sets which way 'width' goes).</summary>
        static GameObject Beam(Transform parent, Vector3 a, Vector3 b, float width, float depth, Vector3 side, Material mat)
        {
            var go = Part(PrimitiveType.Cube, parent, "Beam", (a + b) / 2, new Vector3(depth, width, (b - a).magnitude), mat);
            go.transform.localRotation = Quaternion.LookRotation(b - a, side);
            return go;
        }

        /// <summary>Ribs that follow a wall+dome profile, stopping at the top hub.</summary>
        static void Ribs(Transform parent, Vector2[] profile, float[] angles, float width, float depth, Material mat)
        {
            foreach (float deg in angles)
            {
                var d = Heading(deg);
                var tangent = Vector3.Cross(Vector3.up, d);
                for (int i = 0; i < profile.Length - 1; i++)
                {
                    if (profile[i + 1].x < 0.85f) break;
                    var a = d * profile[i].x + Vector3.up * profile[i].y;
                    var b = d * profile[i + 1].x + Vector3.up * profile[i + 1].y;
                    Beam(parent, a, b, width, depth, tangent, mat);
                }
            }
        }

        /// <summary>Merges all child meshes (no colliders) into one mesh per material, for speed in WebGL.</summary>
        static void Bake(Transform group, string assetName)
        {
            var byMat = new System.Collections.Generic.Dictionary<Material, System.Collections.Generic.List<CombineInstance>>();
            var toWorld = group.worldToLocalMatrix;
            foreach (var mf in group.GetComponentsInChildren<MeshFilter>())
            {
                var mr = mf.GetComponent<MeshRenderer>();
                if (!mr || !mf.sharedMesh) continue;
                if (!byMat.TryGetValue(mr.sharedMaterial, out var list)) byMat[mr.sharedMaterial] = list = new System.Collections.Generic.List<CombineInstance>();
                list.Add(new CombineInstance { mesh = mf.sharedMesh, transform = toWorld * mf.transform.localToWorldMatrix });
            }
            for (int i = group.childCount - 1; i >= 0; i--) Object.DestroyImmediate(group.GetChild(i).gameObject);
            int n = 0;
            foreach (var kv in byMat)
            {
                var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.CombineMeshes(kv.Value.ToArray(), true, true);
                mesh.RecalculateBounds();
                MeshGO(group, kv.Key.name, ProcArt.SaveMesh(mesh, assetName + "_" + n++), kv.Key);
            }
        }

        static void UpgradeHabitat(StringBuilder log)
        {
            var hab = GameObject.Find("Habitat");
            if (!hab) { log.AppendLine("Habitat: skipped (no Habitat in the scene)."); return; }
            var t = hab.transform;
            foreach (var n in new[] { "NASA_HDU", "HabDetailOutside", "HabDetailInside", "HabLockers" })
            {
                var o = t.Find(n);
                if (o) Object.DestroyImmediate(o.gameObject);
            }
            var outer = t.Find("HabShellOutside");
            if (outer) { var mr = outer.GetComponent<MeshRenderer>(); if (mr) mr.enabled = true; }
            var berm = t.Find("SandbagBerm");
            if (berm) berm.gameObject.SetActive(false);   // replaced by real bags below

            var alu = M("MetalLight");
            var dark = M("MetalDark");
            var bagMat = M("RegolithBags");
            var glow = M("RingLight");
            var panelMat = Mat("HabPanel", new Color(0.86f, 0.85f, 0.82f), 0.25f, 0f);
            var trimMat = Mat("HabTrim", new Color(0.86f, 0.42f, 0.16f), 0.35f, 0f);
            if (!alu || !dark || !bagMat) { log.AppendLine("Habitat: skipped (materials missing; run Build Mars Scene first)."); return; }

            // ── Outside
            var outside = new GameObject("HabDetailOutside").transform;
            outside.SetParent(t, false);
            var ribAngles = Enumerable.Range(0, 16).Select(i => i * 22.5f).Where(a => !NearDoor(a, 16)).ToArray();
            Ribs(outside, ProcArt.WallDome(OuterR + 0.05f, Wall, DoorTopY, OuterDome, 10), ribAngles, 0.22f, 0.12f, alu);
            // Base footing and the band where the wall meets the dome
            MeshGO(outside, "Footing", ProcArt.SaveMesh(ProcArt.Lathe(new[] { new Vector2(6.85f, -0.25f), new Vector2(6.85f, 0.32f), new Vector2(6.45f, 0.32f) },
                64, false, 16, 2, (s, c, y) => NearDoor(c, 14)), "HabFooting"), dark);
            MeshGO(outside, "WallBand", ProcArt.SaveMesh(ProcArt.Lathe(new[] { new Vector2(6.5f, 3.0f), new Vector2(6.58f, 3.06f), new Vector2(6.58f, 3.3f), new Vector2(6.5f, 3.36f) },
                64, false, 16, 1), "HabWallBand"), alu);
            // Top hub (hatch for the vent) with a vent stack
            float top = Wall + OuterDome;
            Part(PrimitiveType.Cylinder, outside, "TopHub", new Vector3(0, top - 0.05f, 0), new Vector3(1.9f, 0.14f, 1.9f), alu);
            Part(PrimitiveType.Cylinder, outside, "TopHubRim", new Vector3(0, top + 0.1f, 0), new Vector3(1.3f, 0.06f, 1.3f), dark);
            Part(PrimitiveType.Cylinder, outside, "Vent", new Vector3(0.35f, top + 0.35f, 0.1f), new Vector3(0.18f, 0.25f, 0.18f), dark);
            // Airlock tunnel ribs
            var ringMesh = ProcArt.SaveMesh(ProcArt.Lathe(new[] { new Vector2(1.56f, 0), new Vector2(1.72f, 0), new Vector2(1.72f, 0.16f), new Vector2(1.56f, 0.16f) }, 32, false, 4, 1), "TunnelRib");
            foreach (float z in new[] { 6.9f, 8.0f, 9.1f })
            {
                var ring = MeshGO(outside, "TunnelRib", ringMesh, alu);
                ring.transform.localPosition = new Vector3(0, 1.3f, z);
                ring.transform.localRotation = Quaternion.Euler(90, 0, 0);
            }
            // Regolith sandbag berm (3 rows) around the base: soil blocks radiation.
            for (int row = 0; row < 3; row++)
            {
                const int n = 64;
                float r = 7.05f - row * 0.16f;
                for (int i = 0; i < n; i++)
                {
                    float deg = (i + row * 0.5f) / n * 360f;
                    if (NearDoor(deg, 18)) continue;
                    var d = Heading(deg);
                    var b = Bag(outside, d * r + Vector3.up * (0.12f + row * 0.25f), new Vector3(0.3f, 0.72f, 0.48f), bagMat);
                    b.transform.localRotation = Quaternion.LookRotation(d) * Quaternion.Euler(0, (i * 37 % 7) - 3, 90);
                }
            }
            Bake(outside, "HabOutside");

            // ── Inside
            var inside = new GameObject("HabDetailInside").transform;
            inside.SetParent(t, false);
            Ribs(inside, ProcArt.WallDome(InnerR - 0.05f, Wall, DoorTopY, InnerDome, 10), ribAngles.Where(a => !NearDoor(a, 22)).ToArray(), 0.14f, 0.08f, alu);
            // Padded wall panels between the ribs, with an orange guide stripe (helps you find your way around)
            for (int i = 0; i < 16; i++)
            {
                float mid = 11.25f + i * 22.5f;
                if (NearDoor(mid, 15)) continue;
                var d = Heading(mid);
                var p = Part(PrimitiveType.Cube, inside, "WallPanel", d * 6.0f + Vector3.up * 1.6f, new Vector3(1.86f, 2.3f, 0.04f), panelMat);
                p.transform.localRotation = Quaternion.LookRotation(-d);
                var s = Part(PrimitiveType.Cube, inside, "GuideStripe", d * 5.975f + Vector3.up * 1.2f, new Vector3(1.86f, 0.07f, 0.012f), trimMat);
                s.transform.localRotation = Quaternion.LookRotation(-d);
            }
            MeshGO(inside, "Baseboard", ProcArt.SaveMesh(ProcArt.Lathe(new[] { new Vector2(6.1f, 0f), new Vector2(6.1f, 0.3f), new Vector2(6.02f, 0.32f) },
                64, true, 16, 1, (s, c, y) => NearDoor(c, 13)), "HabBaseboard"), dark);
            // Ceiling hub with a light ring
            float inTop = Wall + InnerDome;
            Part(PrimitiveType.Cylinder, inside, "CeilingHub", new Vector3(0, inTop - 0.12f, 0), new Vector3(1.8f, 0.12f, 1.8f), alu);
            if (glow) Part(PrimitiveType.Cylinder, inside, "CeilingLight", new Vector3(0, inTop - 0.25f, 0), new Vector3(1.2f, 0.02f, 1.2f), glow);
            Bake(inside, "HabInside");

            // ── Storage lockers (food and spare parts) in the free wall spaces: solid, so they keep their colliders
            var lockers = new GameObject("HabLockers").transform;
            lockers.SetParent(t, false);
            foreach (float deg in new[] { 335f, 65f, 115f })
            {
                var d = Heading(deg);
                var bank = new GameObject("LockerBank").transform;
                bank.SetParent(lockers, false);
                bank.localPosition = d * 5.7f;
                bank.localRotation = Quaternion.LookRotation(-d);
                Part(PrimitiveType.Cube, bank, "Body", new Vector3(0, 1.0f, 0), new Vector3(1.65f, 2.0f, 0.5f), panelMat, default, collider: true);
                for (int k = 0; k < 3; k++)
                {
                    float x = -0.54f + k * 0.54f;
                    Part(PrimitiveType.Cube, bank, "Door", new Vector3(x, 1.02f, 0.255f), new Vector3(0.5f, 1.9f, 0.015f), alu);
                    Part(PrimitiveType.Cube, bank, "Handle", new Vector3(x + 0.18f, 1.05f, 0.27f), new Vector3(0.03f, 0.22f, 0.03f), dark);
                    Part(PrimitiveType.Cube, bank, "Vent", new Vector3(x, 1.75f, 0.265f), new Vector3(0.3f, 0.08f, 0.01f), dark);
                }
                Part(PrimitiveType.Cube, bank, "Stripe", new Vector3(0, 1.2f, 0.265f), new Vector3(1.62f, 0.07f, 0.01f), trimMat);
            }
            log.AppendLine("Habitat: new ribs, footing, sandbag berm, airlock ribs, wall panels, ceiling hub and lockers.");
        }

        // ───────── airlock: two heavy round vault doors in the tunnel ─────────

        const float TunnelAxisY = 1.3f, TunnelR = 1.45f;
        const float DoorR = 1.05f, DoorCenterY = -0.25f;   // round opening (relative to the tunnel axis): floor to 2.1 m

        /// <summary>A round wall that fills the tunnel, with a round door hole. Both faces, so it looks right from each side.</summary>
        static Mesh BulkheadMesh()
        {
            const int n = 72;
            var c = new Vector2(0, DoorCenterY);
            var verts = new System.Collections.Generic.List<Vector3>();
            var norms = new System.Collections.Generic.List<Vector3>();
            var uvs = new System.Collections.Generic.List<Vector2>();
            var tris = new System.Collections.Generic.List<int>();
            for (int side = 0; side < 2; side++)
            {
                float z = side == 0 ? -0.06f : 0.06f;
                var nrm = side == 0 ? Vector3.back : Vector3.forward;
                int start = verts.Count;
                for (int i = 0; i <= n; i++)
                {
                    float a = i / (float)n * Mathf.PI * 2;
                    var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    var inner = c + d * DoorR;
                    float cd = Vector2.Dot(c, d);
                    float t = -cd + Mathf.Sqrt(cd * cd - c.sqrMagnitude + TunnelR * TunnelR);   // ray from the door center to the tunnel wall
                    var outer = c + d * t;
                    verts.Add(new Vector3(inner.x, inner.y, z)); verts.Add(new Vector3(outer.x, outer.y, z));
                    norms.Add(nrm); norms.Add(nrm);
                    uvs.Add(inner * 0.5f); uvs.Add(outer * 0.5f);
                }
                for (int i = 0; i < n; i++)
                {
                    int a = start + i * 2, b = a + 1, cc = a + 2, d = a + 3;
                    if (side == 0) tris.AddRange(new[] { a, b, cc, b, d, cc });
                    else tris.AddRange(new[] { a, cc, b, b, cc, d });
                }
            }
            var m = new Mesh();
            m.SetVertices(verts); m.SetNormals(norms); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
            m.RecalculateTangents(); m.RecalculateBounds();
            return m;
        }

        static void UpgradeAirlock(StringBuilder log)
        {
            var hab = GameObject.Find("Habitat");
            var alu = M("MetalLight");
            if (!hab || !alu) { log.AppendLine("Airlock: skipped."); return; }
            var old = hab.transform.Find("AirlockDoors");
            if (old) Object.DestroyImmediate(old.gameObject);
            var root = new GameObject("AirlockDoors").transform;
            root.SetParent(hab.transform, false);
            var steel = Mat("VaultSteel", new Color(0.46f, 0.48f, 0.52f), 0.62f, 0.9f);
            var dark = Mat("VaultDark", new Color(0.15f, 0.16f, 0.18f), 0.5f, 0.85f);
            var stripe = Mat("HabTrim", new Color(0.86f, 0.42f, 0.16f), 0.35f, 0f);
            var lampMat = Mat("AirlockLamp", new Color(0.4f, 0.1f, 0.1f), 0.8f, 0f, new Color(1f, 0.15f, 0.1f));
            var bulkMesh = ProcArt.SaveMesh(BulkheadMesh(), "AirlockBulkhead");

            Transform Door(string name, float z, out Renderer lamp)
            {
                var b = new GameObject(name).transform;
                b.SetParent(root, false);
                b.localPosition = new Vector3(0, TunnelAxisY, z);
                MeshGO(b, "Bulkhead", bulkMesh, alu);

                // Thick round frame: 24 steel blocks around the opening, with bolts on both faces
                const int seg = 24;
                for (int i = 0; i < seg; i++)
                {
                    float a = (i + 0.5f) / seg * Mathf.PI * 2;
                    var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                    var blk = Part(PrimitiveType.Cube, b, "Frame", new Vector3(0, DoorCenterY, 0) + d * (DoorR + 0.13f),
                        new Vector3(0.27f, 2 * Mathf.PI * (DoorR + 0.13f) / seg + 0.02f, 0.42f), steel);
                    blk.transform.localRotation = Quaternion.Euler(0, 0, a * Mathf.Rad2Deg);
                    if (i % 2 == 0)
                        foreach (float s in new[] { -1f, 1f })
                            Part(PrimitiveType.Cylinder, b, "FrameBolt", new Vector3(0, DoorCenterY, s * 0.22f) + d * (DoorR + 0.16f),
                                new Vector3(0.07f, 0.03f, 0.07f), dark, new Vector3(90, 0, 0));
                }

                // Hinge on the left edge; the door center sits DoorR to the right of it
                var hinge = new GameObject("Hinge").transform;
                hinge.SetParent(b, false);
                hinge.localPosition = new Vector3(-DoorR, DoorCenterY, 0);
                var c = new Vector3(DoorR, 0, 0);

                // The vault door: a thick steel disc (solid when closed) with a darker rim
                var disc = Part(PrimitiveType.Cylinder, hinge, "Door", c, new Vector3(2 * DoorR - 0.06f, 0.15f, 2 * DoorR - 0.06f), steel, new Vector3(90, 0, 0));
                var box = disc.AddComponent<BoxCollider>();
                box.size = new Vector3(1f, 2f, 1f);   // cylinder space: a thin square slab, not a giant capsule
                Part(PrimitiveType.Cylinder, hinge, "Rim", c, new Vector3(2 * DoorR + 0.02f, 0.11f, 2 * DoorR + 0.02f), dark, new Vector3(90, 0, 0));
                foreach (float s in new[] { -1f, 1f })
                {
                    float fz = s * 0.17f;
                    // Ring of bolts
                    for (int i = 0; i < 16; i++)
                    {
                        float a = i / 16f * Mathf.PI * 2;
                        Part(PrimitiveType.Cylinder, hinge, "Bolt", c + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * (DoorR - 0.14f) + new Vector3(0, 0, fz),
                            new Vector3(0.08f, 0.025f, 0.08f), dark, new Vector3(90, 0, 0));
                    }
                    // Four locking bars, like a bank vault
                    for (int i = 0; i < 4; i++)
                    {
                        float a = (45 + i * 90) * Mathf.Deg2Rad;
                        var bar = Part(PrimitiveType.Cube, hinge, "LockBar", c + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * 0.62f + new Vector3(0, 0, fz),
                            new Vector3(0.42f, 0.1f, 0.06f), dark);
                        bar.transform.localRotation = Quaternion.Euler(0, 0, 45 + i * 90);
                    }
                    // Wheel handle: hub, three spokes and a rim of short segments
                    Part(PrimitiveType.Cylinder, hinge, "WheelHub", c + new Vector3(0, 0, s * 0.23f), new Vector3(0.22f, 0.07f, 0.22f), dark, new Vector3(90, 0, 0));
                    for (int i = 0; i < 3; i++)
                    {
                        var sp = Part(PrimitiveType.Cube, hinge, "Spoke", c + new Vector3(0, 0, s * 0.26f), new Vector3(0.82f, 0.05f, 0.05f), steel);
                        sp.transform.localRotation = Quaternion.Euler(0, 0, i * 60);
                    }
                    for (int i = 0; i < 12; i++)
                    {
                        float a = (i + 0.5f) / 12 * Mathf.PI * 2;
                        var rs = Part(PrimitiveType.Cube, hinge, "WheelRim", c + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * 0.42f + new Vector3(0, 0, s * 0.27f),
                            new Vector3(0.07f, 0.24f, 0.07f), steel);
                        rs.transform.localRotation = Quaternion.Euler(0, 0, a * Mathf.Rad2Deg);
                    }
                    // Orange safety ring
                    for (int i = 0; i < 20; i++)
                    {
                        float a = (i + 0.5f) / 20 * Mathf.PI * 2;
                        var st = Part(PrimitiveType.Cube, hinge, "SafetyRing", c + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * (DoorR - 0.3f) + new Vector3(0, 0, s * 0.155f),
                            new Vector3(0.05f, 0.24f, 0.01f), stripe);
                        st.transform.localRotation = Quaternion.Euler(0, 0, a * Mathf.Rad2Deg);
                    }
                }
                // Two big hinge barrels
                foreach (float y in new[] { -0.6f, 0.6f })
                    Part(PrimitiveType.Cylinder, hinge, "HingeBarrel", new Vector3(-0.05f, y, 0), new Vector3(0.16f, 0.22f, 0.16f), dark);

                lamp = Part(PrimitiveType.Sphere, b, "Lamp", new Vector3(0, DoorCenterY + DoorR + 0.32f, 0), new Vector3(0.14f, 0.14f, 0.2f), lampMat).GetComponent<Renderer>();
                return hinge;
            }

            var inner = Door("InnerDoor", 6.75f, out var innerLamp);
            var outer = Door("OuterDoor", 10.3f, out var outerLamp);

            var al = root.gameObject.AddComponent<Airlock>();
            al.habitat = hab.transform;
            al.innerHinge = inner;
            al.outerHinge = outer;
            al.innerLamp = innerLamp;
            al.outerLamp = outerLamp;
            al.innerZ = 6.75f;
            al.outerZ = 10.3f;
            var di = inner.gameObject.AddComponent<AirlockDoor>(); di.airlock = al; di.inner = true;
            var dO = outer.gameObject.AddComponent<AirlockDoor>(); dO.airlock = al; dO.inner = false;
            log.AppendLine("Airlock: two round vault doors (click to open).");
        }
        // ───────── weather station (daily mission) ─────────

        static readonly Vector3 WeatherPos = new Vector3(-8, 0, 14);

        static void BuildWeatherStation(StringBuilder log)
        {
            var alu = M("MetalLight");
            var dark = M("MetalDark");
            var white = Mat("HabPanel", new Color(0.86f, 0.85f, 0.82f), 0.25f, 0f);
            var solar = M("SolarPanel") ?? dark;
            if (!alu || !dark) { log.AppendLine("Weather station: skipped."); return; }
            var old = GameObject.Find("WeatherStation");
            if (old) Object.DestroyImmediate(old);
            var root = new GameObject("WeatherStation").transform;
            var pos = WeatherPos;
            var terrain = Terrain.activeTerrain;
            if (terrain) pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y;
            root.position = pos;

            // Tripod and mast
            for (int i = 0; i < 3; i++)
            {
                var d = Heading(i * 120f + 30f);
                Strut(root, d * 0.6f, Vector3.up * 0.9f, alu, 0.03f);
                Part(PrimitiveType.Cube, root, "Foot", d * 0.6f + Vector3.up * 0.02f, new Vector3(0.14f, 0.04f, 0.14f), dark);
            }
            Part(PrimitiveType.Cylinder, root, "Mast", new Vector3(0, 1.25f, 0), new Vector3(0.06f, 1.25f, 0.06f), alu);
            // Sensor boom, temperature shield (stacked white plates), electronics box and a little solar panel
            Part(PrimitiveType.Cube, root, "Boom", new Vector3(0.35f, 1.9f, 0), new Vector3(0.7f, 0.04f, 0.04f), alu);
            for (int k = 0; k < 5; k++)
                Part(PrimitiveType.Cylinder, root, "ShieldPlate", new Vector3(0.66f, 1.72f + k * 0.05f, 0), new Vector3(0.2f, 0.012f, 0.2f), white);
            Part(PrimitiveType.Cube, root, "Electronics", new Vector3(0, 0.95f, 0.12f), new Vector3(0.3f, 0.36f, 0.16f), white);
            var sp = Part(PrimitiveType.Cube, root, "MiniPanel", new Vector3(0, 1.45f, -0.25f), new Vector3(0.5f, 0.02f, 0.36f), solar);
            sp.transform.localRotation = Quaternion.Euler(-35, 0, 0);
            // Wind cups that spin (faster on dusty, windy days)
            var spinner = new GameObject("WindCups").transform;
            spinner.SetParent(root, false);
            spinner.localPosition = new Vector3(0, 2.55f, 0);
            Part(PrimitiveType.Cylinder, root, "CupHub", new Vector3(0, 2.52f, 0), new Vector3(0.05f, 0.04f, 0.05f), dark);
            for (int i = 0; i < 3; i++)
            {
                var d = Heading(i * 120f);
                Strut(spinner, Vector3.zero, d * 0.22f, alu, 0.012f);
                Part(PrimitiveType.Sphere, spinner, "Cup", d * 0.24f, new Vector3(0.07f, 0.07f, 0.07f), white);
            }
            var col = root.gameObject.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0, 1.3f, 0);
            col.height = 2.7f;
            col.radius = 0.6f;
            var ws = root.gameObject.AddComponent<WeatherStation>();
            ws.runner = Object.FindAnyObjectByType<MissionRunner>();
            ws.spinner = spinner;
            log.AppendLine("Weather station: built next to the base.");
        }

        /// <summary>A low-poly sandbag (about 40 vertices instead of a capsule's 550): keeps the WebGL download small.</summary>
        static Mesh bagMesh;
        static GameObject Bag(Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            if (!bagMesh)
                bagMesh = ProcArt.SaveMesh(ProcArt.Lathe(new[] { new Vector2(0.001f, -0.5f), new Vector2(0.32f, -0.42f), new Vector2(0.5f, -0.18f),
                    new Vector2(0.5f, 0.18f), new Vector2(0.32f, 0.42f), new Vector2(0.001f, 0.5f) }, 7, false, 1, 1), "SandbagLow");
            var go = MeshGO(parent, "Bag", bagMesh, mat);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            return go;
        }

        static void RemoveShelter(StringBuilder log)
        {
            var shelter = GameObject.Find("RadiationShelter");
            if (shelter) Object.DestroyImmediate(shelter);
            foreach (var path in new[] { ProcArt.GenDir + "/Meshes/ShelterBags_0.asset", ProcArt.GenDir + "/Meshes/ShelterMound.asset", ProcArt.GenDir + "/Meshes/ShelterVault.asset" })
                AssetDatabase.DeleteAsset(path);
            log.AppendLine("Shelter: removed (radiation is no longer part of the game).");
        }

        /// <summary>Two staggered rows of regolith sandbags around the shelter mound (the real way to block radiation: lots of soil).</summary>
        static void UpgradeShelter(StringBuilder log)
        {
            var shelter = GameObject.Find("RadiationShelter");
            var bag = M("RegolithBags");
            if (!shelter || !bag) { log.AppendLine("Shelter: skipped."); return; }
            var old = shelter.transform.Find("BagRing");
            if (old) Object.DestroyImmediate(old.gameObject);
            var ring = new GameObject("BagRing").transform;
            ring.SetParent(shelter.transform, false);

            // door direction = where the passage walls point
            Vector3 door = Vector3.forward;
            var wall = shelter.transform.Find("PassageWall");
            if (wall) door = Vector3.ProjectOnPlane(wall.localPosition, Vector3.up).normalized;

            const float radius = 6.35f;
            int count = 0;
            for (int row = 0; row < 2; row++)
            {
                int n = 44;
                for (int i = 0; i < n; i++)
                {
                    float a = (i + row * 0.5f) / n * Mathf.PI * 2;
                    var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    if (Vector3.Angle(dir, door) < 20) continue;   // keep the doorway open
                    float r = radius - row * 0.12f;
                    var b = Part(PrimitiveType.Capsule, ring, "Bag", dir * r + Vector3.up * (0.14f + row * 0.26f),
                        new Vector3(0.32f, 0.46f, 0.5f), bag);
                    b.transform.localRotation = Quaternion.LookRotation(dir) * Quaternion.Euler(0, (i * 37 % 7) - 3, 90);   // lying along the ring
                    count++;
                }
            }
            Bake(ring, "ShelterBags");
            log.AppendLine($"Shelter: {count} regolith bags.");
        }

        static void Strut(Transform parent, Vector3 a, Vector3 b, Material mat, float thickness = 0.025f)
        {
            var go = Part(PrimitiveType.Cylinder, parent, "Strut", (a + b) / 2, new Vector3(thickness, (b - a).magnitude / 2, thickness), mat);
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
        }

        // ───────── helpers ─────────

        static GameObject Load(string file) => AssetDatabase.LoadAssetAtPath<GameObject>(Dir + file);

        static GameObject Inst(GameObject asset) => (GameObject)PrefabUtility.InstantiatePrefab(asset);

        static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) Object.DestroyImmediate(t.GetChild(i).gameObject);
        }

        static Bounds WorldBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>
        /// Scale a model uniformly to the wanted size, stand it on the ground (y = 0), center it,
        /// and put it under 'parent' in a new container. Returns the model's size after fitting.
        /// </summary>
        /// <summary>
        /// Some NASA models are stored lying down (Z-up). Turn the model so its longest side points up, then make sure
        /// the part with topMaterial (the visor = the head) is at the top, not the bottom.
        /// </summary>
        static void StandUp(GameObject inst, string topMaterial)
        {
            inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            inst.transform.localScale = Vector3.one;
            var b = WorldBounds(inst);
            if (b.size.z > b.size.y && b.size.z >= b.size.x) inst.transform.rotation = Quaternion.Euler(-90, 0, 0);
            else if (b.size.x > b.size.y) inst.transform.rotation = Quaternion.Euler(0, 0, 90);

            // Head up? Find the visor submesh and compare its height with the middle of the body.
            foreach (var r in inst.GetComponentsInChildren<MeshRenderer>())
            {
                var mf = r.GetComponent<MeshFilter>();
                if (!mf || !mf.sharedMesh) continue;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length && i < mf.sharedMesh.subMeshCount; i++)
                {
                    if (!mats[i] || !mats[i].name.Contains(topMaterial)) continue;
                    var head = r.transform.TransformPoint(mf.sharedMesh.GetSubMesh(i).bounds.center);
                    b = WorldBounds(inst);
                    if (head.y < b.center.y) inst.transform.rotation = Quaternion.Euler(180, 0, 0) * inst.transform.rotation;
                    return;
                }
            }
        }

        static Vector3 Place(GameObject inst, Transform parent, float size, Fit fit, string name, out GameObject model)
        {
            inst.transform.SetParent(null);
            inst.transform.position = Vector3.zero;   // rotation is kept (StandUp may have turned the model upright)
            inst.transform.localScale = Vector3.one;
            var b = WorldBounds(inst);
            float measure = fit == Fit.Height ? b.size.y : Mathf.Max(b.size.x, b.size.z);
            float s = size / Mathf.Max(0.0001f, measure);
            inst.transform.localScale = Vector3.one * s;
            b = WorldBounds(inst);

            var container = new GameObject(name).transform;
            container.SetParent(parent, false);
            inst.transform.SetParent(container, false);
            inst.transform.localPosition = new Vector3(-b.center.x, -b.min.y, -b.center.z);
            foreach (var r in inst.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            model = inst;
            return b.size;
        }

        /// <summary>Swap a material (by name) for a copy that uses our logo-free texture.</summary>
        static void ReplaceMaterial(GameObject model, string nameContains, string texPath, string matPath)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (!tex) return;
            Material copy = null;
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (!mats[i] || !mats[i].name.Contains(nameContains)) continue;
                    if (!copy)
                    {
                        AssetDatabase.DeleteAsset(matPath);
                        copy = new Material(mats[i]);
                        if (copy.HasProperty("baseColorTexture")) copy.SetTexture("baseColorTexture", tex);
                        if (copy.HasProperty("_BaseMap")) copy.SetTexture("_BaseMap", tex);
                        if (copy.HasProperty("_MainTex")) copy.SetTexture("_MainTex", tex);
                        AssetDatabase.CreateAsset(copy, matPath);
                    }
                    mats[i] = copy;
                    changed = true;
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        /// <summary>The suit texture must be readable so trim colors can recolor its stripe at runtime.</summary>
        static void PrepareTextures()
        {
            void Set(string path, bool readable)
            {
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp == null) return;
                if (imp.isReadable == readable && imp.sRGBTexture) return;
                imp.isReadable = readable;
                imp.sRGBTexture = true;
                imp.SaveAndReimport();
            }
            Set(Dir + "Astronaut_Suit_NoLogo.png", true);
            Set(Dir + "Perseverance_ArmGraphics_NoLogo.png", false);
        }
    }
}