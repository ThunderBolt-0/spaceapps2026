using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MissionTrainer.EditorTools
{
    /// <summary>
    /// Menu: Mission Trainer > Update Suit (patches + customization).
    /// 1) Paints the mission patch decals into Assets/_Game/Resources/Patches/{id}.png (embroidered-badge style).
    /// 2) Adds chest + shoulder decal quads to the suit on the stand in the Mars scene (no terrain rebuild).
    /// The scene builder calls the same code.
    /// </summary>
    public static class SuitSetup
    {
        const string ScenePath = "Assets/_Game/Scenes/Mars.unity";
        const string PatchDir = "Assets/_Game/Resources/Patches";
        const string PatchMatPath = "Assets/_Game/Art/Materials/SuitPatch.mat";

        [MenuItem("Mission Trainer/Update Suit (patches + customization)")]
        public static void Run()
        {
            GeneratePatches();
            if (!File.Exists(ScenePath))
            {
                EditorUtility.DisplayDialog("No Mars scene", "Patches painted. Run Build Mars Scene (3D) to get the suit stand.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath);
            bool done = false;
            foreach (var suit in Object.FindObjectsByType<AstronautSuit>())
            {
                if (suit.visorRenderers == null || suit.visorRenderers.Length == 0) continue;   // only the full suit on the stand
                AddDecals(suit);
                done = true;
            }
            if (done)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("Suit updated with patch decals. Press Play, then 'Your suit' on the title screen.");
            }
            else Debug.LogWarning("No suit stand found in Mars scene.");
        }

        /// <summary>Replace the old patch object with chest + left-shoulder decal quads.</summary>
        public static void AddDecals(AstronautSuit suit)
        {
            var root = suit.transform;
            if (suit.patchSlot) Object.DestroyImmediate(suit.patchSlot);
            foreach (var n in new[] { "PatchChest", "PatchShoulder" })
            {
                var old = root.Find(n);
                if (old) Object.DestroyImmediate(old.gameObject);
            }

            var mat = PatchMaterial();
            var chest = Decal(root, "PatchChest", new Vector3(-0.16f, 1.40f, 0.176f), Quaternion.Euler(0, 180, 0), 0.11f, mat);
            var shoulder = Decal(root, "PatchShoulder", new Vector3(-0.462f, 1.36f, 0f), Quaternion.Euler(0, 90, -8), 0.15f, mat);
            suit.patchRenderers = new[] { chest, shoulder };
            suit.patchSlot = null;
            EditorUtility.SetDirty(suit);
        }

        static Renderer Decal(Transform parent, string name, Vector3 pos, Quaternion rot, float size, Material mat)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name;
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(parent, false);
            q.transform.localPosition = pos;
            q.transform.localRotation = rot;
            q.transform.localScale = Vector3.one * size;
            var r = q.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return r;
        }

        /// <summary>URP Lit with alpha clipping, saved as an asset so the WebGL build keeps the variant.</summary>
        static Material PatchMaterial()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(PatchMatPath);
            bool isNew = m == null;
            if (isNew) m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0.15f);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_AlphaClip", 1);
            m.SetFloat("_Cutoff", 0.5f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(PatchDir + "/patch_trainee.png");
            if (tex) m.SetTexture("_BaseMap", tex);
            if (isNew) AssetDatabase.CreateAsset(m, PatchMatPath);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ───────────────────────── patch art ─────────────────────────

        const int S = 256;

        class Painter
        {
            public readonly Color[] px = new Color[S * S];

            /// <summary>Composite a shape given as a signed distance (negative = inside), in -1..1 units.</summary>
            public void Draw(Func<Vector2, float> sdf, Color c)
            {
                for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    var q = new Vector2((x + 0.5f) / S * 2 - 1, (y + 0.5f) / S * 2 - 1);
                    float cov = Mathf.Clamp01(0.5f - sdf(q) * S * 0.5f) * c.a;
                    if (cov <= 0) continue;
                    int i = y * S + x;
                    var d = px[i];
                    px[i] = new Color(Mathf.Lerp(d.r, c.r, cov), Mathf.Lerp(d.g, c.g, cov), Mathf.Lerp(d.b, c.b, cov), Mathf.Max(d.a, cov));
                }
            }

            /// <summary>Everything outside the badge circle becomes transparent.</summary>
            public void ClipToBadge(float r)
            {
                for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    var q = new Vector2((x + 0.5f) / S * 2 - 1, (y + 0.5f) / S * 2 - 1);
                    px[y * S + x].a *= Mathf.Clamp01(0.5f - (q.magnitude - r) * S * 0.5f);
                }
            }

            /// <summary>Faint thread texture so it reads as embroidery, not flat vector.</summary>
            public void Thread()
            {
                for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float t = 0.94f + 0.06f * Mathf.Sin((x + y) * 1.3f) + 0.03f * (ProcArt.Hash01(x, y, 5) - 0.5f);
                    var c = px[y * S + x];
                    px[y * S + x] = new Color(c.r * t, c.g * t, c.b * t, c.a);
                }
            }
        }

        // SDF helpers (-1..1 space)
        static float Circle(Vector2 q, Vector2 c, float r) => (q - c).magnitude - r;
        static float Ring(Vector2 q, Vector2 c, float r, float w) => Mathf.Abs((q - c).magnitude - r) - w;
        static float Seg(Vector2 q, Vector2 a, Vector2 b, float w)
        {
            var pa = q - a; var ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude - w;
        }
        static float Wave(Vector2 q, float y0, float amp, float freq, float w) => Mathf.Abs(q.y - (y0 + amp * Mathf.Sin(q.x * freq))) - w;

        static void Badge(string id, Color bg, Color border, Action<Painter> emblem)
        {
            var p = new Painter();
            p.Draw(q => Circle(q, Vector2.zero, 0.97f), bg);
            emblem(p);
            p.Draw(q => Ring(q, Vector2.zero, 0.885f, 0.075f), border);
            p.Draw(q => Ring(q, Vector2.zero, 0.79f, 0.012f), new Color(border.r, border.g, border.b, 0.6f));
            p.Thread();
            p.ClipToBadge(0.97f);
            Save(id, p.px);
        }

        public static void GeneratePatches()
        {
            Directory.CreateDirectory(PatchDir);
            var gold = new Color(0.95f, 0.78f, 0.35f);
            var silver = new Color(0.82f, 0.84f, 0.88f);
            var bronze = new Color(0.78f, 0.52f, 0.30f);

            // Trainee: Mars with an orbit and a small ship-star
            Badge("patch_trainee", new Color(0.07f, 0.10f, 0.22f), silver, p =>
            {
                p.Draw(q => Circle(q, new Vector2(0, -0.05f), 0.36f), new Color(0.78f, 0.38f, 0.22f));
                p.Draw(q => Wave(q, -0.12f, 0.05f, 6, 0.03f), new Color(0.6f, 0.28f, 0.18f, 0.8f));
                p.Draw(q => Mathf.Abs(new Vector2(q.x / 0.62f, (q.y + 0.05f) / 0.2f).magnitude - 1) * 0.2f - 0.012f, silver);
                p.Draw(q => Circle(q, new Vector2(0.55f, 0.06f), 0.06f), Color.white);
            });

            // Chapter 1, The Storm: dimmed sun behind dust bands
            Badge("patch_ch1", new Color(0.55f, 0.36f, 0.24f), new Color(0.95f, 0.55f, 0.2f), p =>
            {
                p.Draw(q => Circle(q, new Vector2(0.18f, 0.25f), 0.2f), new Color(1f, 0.85f, 0.6f, 0.75f));
                p.Draw(q => Wave(q, 0.05f, 0.08f, 5, 0.07f), new Color(0.42f, 0.26f, 0.17f));
                p.Draw(q => Wave(q, -0.2f, 0.07f, 6, 0.08f), new Color(0.36f, 0.22f, 0.15f));
                p.Draw(q => Wave(q, -0.45f, 0.06f, 7, 0.09f), new Color(0.30f, 0.18f, 0.12f));
            });

            // Chapter 2, Cold Night: crescent moon and stars
            Badge("patch_ch2", new Color(0.05f, 0.07f, 0.17f), new Color(0.65f, 0.82f, 1f), p =>
            {
                p.Draw(q => Mathf.Max(Circle(q, new Vector2(-0.08f, 0.02f), 0.4f), -Circle(q, new Vector2(0.1f, 0.12f), 0.36f)), new Color(0.88f, 0.93f, 1f));
                foreach (var s in new[] { new Vector2(0.38f, 0.35f), new Vector2(0.45f, -0.2f), new Vector2(0.15f, -0.45f), new Vector2(-0.45f, 0.42f) })
                    p.Draw(q => Circle(q, s, 0.035f), Color.white);
            });

            // Chapter 3, Stuck: rover wheel in sand ripples
            Badge("patch_ch3", new Color(0.70f, 0.54f, 0.37f), new Color(0.45f, 0.30f, 0.20f), p =>
            {
                p.Draw(q => Wave(q, -0.5f, 0.04f, 8, 0.05f), new Color(0.55f, 0.40f, 0.27f));
                p.Draw(q => Wave(q, -0.66f, 0.04f, 8, 0.05f), new Color(0.55f, 0.40f, 0.27f));
                var c = new Vector2(0, 0.05f);
                p.Draw(q => Ring(q, c, 0.36f, 0.07f), new Color(0.22f, 0.22f, 0.24f));
                for (int i = 0; i < 6; i++)
                {
                    float a = i * Mathf.PI / 3;
                    var e = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.32f;
                    p.Draw(q => Seg(q, c, e, 0.03f), new Color(0.75f, 0.76f, 0.78f));
                }
                p.Draw(q => Circle(q, c, 0.08f), new Color(0.75f, 0.76f, 0.78f));
            });

            // Chapter 4, Silence: the Sun between Earth and Mars, broken signal
            Badge("patch_ch4", new Color(0.04f, 0.05f, 0.12f), new Color(0.3f, 0.8f, 0.8f), p =>
            {
                p.Draw(q => Circle(q, Vector2.zero, 0.2f), new Color(1f, 0.85f, 0.35f));
                p.Draw(q => Circle(q, new Vector2(-0.55f, 0), 0.1f), new Color(0.3f, 0.55f, 0.95f));
                p.Draw(q => Circle(q, new Vector2(0.55f, 0), 0.085f), new Color(0.85f, 0.42f, 0.25f));
                foreach (float x in new[] { -0.4f, -0.3f, 0.3f, 0.4f })
                    p.Draw(q => Seg(q, new Vector2(x, 0), new Vector2(x + 0.05f, 0), 0.018f), new Color(0.3f, 0.8f, 0.8f));
            });

            // Chapter 5, Solar Storm: sun with rays
            Badge("patch_ch5", new Color(0.25f, 0.06f, 0.05f), gold, p =>
            {
                for (int i = 0; i < 12; i++)
                {
                    float a = i * Mathf.PI / 6;
                    var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    p.Draw(q => Seg(q, d * 0.3f, d * (i % 2 == 0 ? 0.62f : 0.5f), 0.035f), new Color(1f, 0.6f, 0.15f));
                }
                p.Draw(q => Circle(q, Vector2.zero, 0.27f), new Color(1f, 0.82f, 0.3f));
            });

            // Freeplay sol patches: sunrise over the plain + 1/2/3 chevrons
            void SolPatch(string id, Color border, int chevrons)
            {
                Badge(id, new Color(0.42f, 0.25f, 0.16f), border, p =>
                {
                    p.Draw(q => Circle(q, new Vector2(0, -0.2f), 0.26f), new Color(1f, 0.82f, 0.5f));
                    p.Draw(q => q.y + 0.2f, new Color(0.55f, 0.32f, 0.2f));   // ground below the horizon hides half the sun
                    for (int i = 0; i < chevrons; i++)
                    {
                        float y = 0.22f + i * 0.17f;
                        p.Draw(q => Mathf.Min(Seg(q, new Vector2(-0.22f, y), new Vector2(0, y + 0.1f), 0.035f),
                                              Seg(q, new Vector2(0, y + 0.1f), new Vector2(0.22f, y), 0.035f)), border);
                    }
                });
            }
            SolPatch("patch_sol10", bronze, 1);
            SolPatch("patch_sol25", silver, 2);
            SolPatch("patch_sol50", gold, 3);

            AssetDatabase.Refresh();
        }

        static void Save(string id, Color[] px)
        {
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            tex.SetPixels(px);
            tex.Apply();
            string path = $"{PatchDir}/{id}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Default;
            imp.alphaSource = TextureImporterAlphaSource.FromInput;
            imp.alphaIsTransparency = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.mipmapEnabled = true;
            imp.maxTextureSize = S;
            imp.SaveAndReimport();
        }
    }
}
