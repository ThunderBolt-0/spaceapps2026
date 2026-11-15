using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace MissionTrainer
{
    /// <summary>
    /// Runs the game inside the one 3D scene: Title -> Chapter map / Freeplay / Science page -> Mission -> Debrief.
    /// While a menu is open a second camera slowly orbits the base and the player is frozen.
    /// Finds everything it needs by itself, so adding this component to the Mars scene is enough.
    /// </summary>
    public class GameFlow : MonoBehaviour
    {
        public static GameFlow Current { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatic() => Current = null;

        public MissionRunner runner;
        public PlaytestUI hud;
        public FirstPersonController player;
        public Camera playerCamera;
        public Camera menuCamera;

        [Tooltip("The full suit on the stand in the habitat (found automatically).")]
        public AstronautSuit suitDisplay;

        MenuUI menu;
        bool suitOpen, suitFromGame;
        Quaternion suitStartRot;

        void Awake()
        {
            Current = this;
            if (!runner) runner = FindAnyObjectByType<MissionRunner>();
            if (!hud) hud = FindAnyObjectByType<PlaytestUI>();
            if (!player) player = FindAnyObjectByType<FirstPersonController>();
            if (!playerCamera && player) playerCamera = player.GetComponentInChildren<Camera>(true);
            if (!menuCamera) menuCamera = CreateMenuCamera();
            if (!suitDisplay)
            {
                var locker = FindAnyObjectByType<SuitLocker>();
                if (locker) suitDisplay = locker.display;
            }
            runner.autoStart = false;   // we start missions ourselves
            WireWorld();

            menu = gameObject.AddComponent<MenuUI>();
            menu.flow = this;
        }

        /// <summary>Give the solar panels and the antenna their jobs (works on scenes built before these existed).</summary>
        void WireWorld()
        {
            var field = GameObject.Find("SolarField");
            if (field)
                foreach (Transform panel in field.transform)
                    if (!panel.GetComponent<SolarPanelStation>()) panel.gameObject.AddComponent<SolarPanelStation>().runner = runner;

            var antenna = GameObject.Find("Antenna");
            if (antenna && !antenna.GetComponent<RadioDish>())
            {
                var dish = antenna.AddComponent<RadioDish>();
                dish.runner = runner;
                dish.dish = antenna.transform.Find("Dish");
            }

            // Radiation was removed from the game, so the radiation shelter has no job: take it out of the world.
            var shelter = GameObject.Find("RadiationShelter");
            if (shelter) shelter.SetActive(false);
            var loc = FindAnyObjectByType<PlayerLocation>();
            if (loc) loc.shelter = new Bounds(new Vector3(0, -1000, 0), Vector3.zero);

            MakeExtraSampleSites();   // before the ice sites (they are copied from rock sites too)
            if (!FindAnyObjectByType<IceSite>())
                for (int i = 0; i < 3; i++) MakeIceSite(i);
        }

        /// <summary>More rock sites around the base (copies of the scene's sites), so a 5-rock mission has enough rocks.</summary>
        static readonly Vector3[] ExtraRocks = { new Vector3(18, 0, 30), new Vector3(-24, 0, 36), new Vector3(28, 0, 4), new Vector3(-36, 0, -2), new Vector3(6, 0, 52) };

        void MakeExtraSampleSites()
        {
            var template = FindAnyObjectByType<SampleSite>();
            if (!template || FindObjectsByType<SampleSite>().Length > 5) return;
            var terrain = Terrain.activeTerrain;
            foreach (var p in ExtraRocks)
            {
                var pos = p;
                if (terrain) pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y;
                var go = Instantiate(template.gameObject, pos, Quaternion.Euler(0, pos.x * 13f, 0));
                go.name = "SampleSite (extra)";
            }
        }

        /// <summary>Far from the base: the long walk is the risk. (Simulated spot; Meridiani's real ice is not mapped here.)</summary>
        public static readonly Vector3 IceSitePos = new Vector3(-58, 0, 78);
        static readonly Vector3[] IceOffsets = { Vector3.zero, new Vector3(9, 0, 6), new Vector3(-7, 0, 10) };

        /// <summary>Builds an ice deposit by copying a rock sample site (same materials, so WebGL keeps its shaders) and tinting it icy blue.</summary>
        void MakeIceSite(int index)
        {
            var template = FindAnyObjectByType<SampleSite>();
            if (!template) return;
            var go = Instantiate(template.gameObject);
            go.name = "IceSite";
            Destroy(go.GetComponent<SampleSite>());

            var visuals = new GameObject("Visuals").transform;
            visuals.SetParent(go.transform, false);
            for (int i = go.transform.childCount - 1; i >= 0; i--)
            {
                var c = go.transform.GetChild(i);
                if (c != visuals) c.SetParent(visuals, true);
            }
            foreach (var r in visuals.GetComponentsInChildren<Renderer>(true))
            {
                var m = r.material;   // instance copy
                var tint = r.name.ToLower().Contains("beam") || r.name.ToLower().Contains("marker")
                    ? new Color(0.45f, 0.85f, 1f, m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor").a : 1f)
                    : new Color(0.82f, 0.93f, 1f);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
            }
            visuals.localScale = Vector3.one * 1.6f;

            var pos = IceSitePos + IceOffsets[index];
            var terrain = Terrain.activeTerrain;
            if (terrain) pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y;
            go.transform.position = pos;
            go.name = "IceSite" + index;

            var ice = go.AddComponent<IceSite>();
            ice.index = index;
            ice.runner = runner;
            ice.visuals = visuals.gameObject;
            visuals.gameObject.SetActive(false);
        }

        void OnDestroy() { if (Current == this) Current = null; }

        void Start()
        {
            StartCoroutine(NasaLive.Refresh());   // real space weather + today's Mars date, once per launch
            var gm = GameManager.Instance;
            if (gm.PlayOnLoad && gm.CurrentMission != null)
            {
                gm.PlayOnLoad = false;
                Play(gm.CurrentMission, gm.Mode);
            }
            else ShowTitle();
        }

        // ───────── screens ─────────

        public void ShowTitle() { EndSuitView(); SetPlaying(false); menu.ShowTitle(); }
        public void ShowChapterMap() { EndSuitView(); SetPlaying(false); menu.ShowMap(); }
        public void ShowScience() { EndSuitView(); SetPlaying(false); menu.ShowScience(); }
        public void ShowHandbook() { EndSuitView(); SetPlaying(false); menu.ShowHandbook(); }
        public void ShowFreeplayHub() { EndSuitView(); SetPlaying(false); menu.ShowFreeplayHub(); }
        public void ShowFinale() { EndSuitView(); SetPlaying(false); menu.ShowFinale(); }
        public void ShowRocks() { EndSuitView(); SetPlaying(false); menu.ShowRocks(); }

        /// <summary>
        /// The one big button: first time = invitation slides then Stage "Arrival";
        /// after that = straight into the next stage's briefing; when everything is done = the finale.
        /// </summary>
        public void ContinueExpedition()
        {
            if (!SaveSystem.SeenIntro) { SetPlaying(false); menu.ShowIntro(true); return; }
            var next = MissionLoader.NextStage();
            if (next == null) ShowFinale();
            else PlayStory(next.id);
        }

        // ───────── suit ─────────

        /// <summary>Open "Your suit": the camera moves to the suit stand and the suit slowly turns.
        /// fromGame = opened at the suit stand during a mission (Back returns to the mission).</summary>
        public void ShowSuit(bool fromGame)
        {
            suitFromGame = fromGame && runner.Mission != null && !runner.IsOver;
            SetPlaying(false);
            menu.ShowSuit();
            if (!suitDisplay || !menuCamera) return;

            suitOpen = true;
            suitStartRot = suitDisplay.transform.localRotation;
            var orbit = menuCamera.GetComponent<MenuOrbit>();
            if (orbit) orbit.enabled = false;

            var d = suitDisplay.transform;
            var center = d.position + Vector3.up * 1.15f;
            var camPos = center + d.forward * 2.3f + Vector3.up * 0.2f;
            var look = Quaternion.LookRotation(center - camPos);
            // Aim a little right of the suit so it sits in the open left part of the screen (panel is on the right).
            menuCamera.transform.position = camPos;
            menuCamera.transform.rotation = Quaternion.LookRotation(center + look * Vector3.right * 0.55f - camPos);
            menuCamera.fieldOfView = 42;
        }

        public void CloseSuit()
        {
            bool backToGame = suitFromGame;
            EndSuitView();
            if (backToGame) { menu.HideAll(); SetPlaying(true); }
            else ShowTitle();
        }

        public void RefreshSuitDisplay()
        {
            if (suitDisplay) suitDisplay.Apply();
            if (player) { var s = player.GetComponent<AstronautSuit>(); if (s) s.Apply(); }
        }

        void EndSuitView()
        {
            if (!suitOpen) return;
            suitOpen = false;
            suitFromGame = false;
            if (suitDisplay) suitDisplay.transform.localRotation = suitStartRot;
            if (menuCamera)
            {
                menuCamera.fieldOfView = 50;
                var orbit = menuCamera.GetComponent<MenuOrbit>();
                if (orbit) orbit.enabled = true;
            }
        }

        void Update()
        {
            if (suitOpen && suitDisplay) suitDisplay.transform.Rotate(0, 14f * Time.unscaledDeltaTime, 0, Space.Self);
        }

        public void PlayStory(string missionId)
        {
            var m = MissionLoader.Load(missionId);
            if (m != null) Play(m, GameMode.Story);
        }

        public void PlayFreeplay() => Play(FreeplayGenerator.Generate(System.Environment.TickCount), GameMode.Freeplay);

        void Play(MissionData mission, GameMode mode)
        {
            EndSuitView();
            var gm = GameManager.Instance;
            gm.SetMission(mission, mode);
            menu.HideAll();
            hud.ClearForNewMission();
            SetPlaying(true);
            // Briefing first (goal, hazards, how to play); the mission starts when the player presses Start.
            hud.ShowBriefing(mission, () => runner.Begin(mission, gm.Config, gm.Level));
        }

        public void ShowIntro() { SetPlaying(false); menu.ShowIntro(false); }

        /// <summary>Night cutscene: switch to the free camera (the player stays where they are). Returns null if there is none.</summary>
        public Camera BeginCinematic()
        {
            if (!menuCamera) return null;
            var orbit = menuCamera.GetComponent<MenuOrbit>();
            if (orbit) orbit.enabled = false;
            menuCamera.fieldOfView = 55;
            menuCamera.gameObject.SetActive(true);
            if (playerCamera) playerCamera.gameObject.SetActive(false);
            if (player) player.enabled = false;
            return menuCamera;
        }

        public void EndCinematic()
        {
            if (menuCamera)
            {
                var orbit = menuCamera.GetComponent<MenuOrbit>();
                if (orbit) orbit.enabled = true;
                menuCamera.fieldOfView = 50;
                menuCamera.gameObject.SetActive(false);
            }
            if (playerCamera) playerCamera.gameObject.SetActive(true);
            if (player) player.enabled = true;
        }

        void SetPlaying(bool playing)
        {
            if (player)
            {
                player.enabled = playing;
                var loc = player.GetComponent<PlayerLocation>();
                if (loc) loc.enabled = playing;
            }
            if (playerCamera) playerCamera.gameObject.SetActive(playing);
            if (menuCamera) menuCamera.gameObject.SetActive(!playing);
            if (hud) hud.SetVisible(playing);
            if (!playing)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        Camera CreateMenuCamera()
        {
            var go = new GameObject("MenuCamera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            if (playerCamera)
            {
                cam.clearFlags = playerCamera.clearFlags;
                cam.backgroundColor = playerCamera.backgroundColor;
                cam.farClipPlane = playerCamera.farClipPlane;
            }
            cam.fieldOfView = 50;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            go.AddComponent<AudioListener>();
            go.AddComponent<MenuOrbit>();
            return cam;
        }
    }
}
