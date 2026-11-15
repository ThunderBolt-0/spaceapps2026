using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static MissionTrainer.UIKit;

namespace MissionTrainer
{
    /// <summary>
    /// The in-mission UI, designed for 7-12 year olds. Every day follows the same three steps:
    ///   1. Mars Report (pop-up): today's sky from real NASA data, in pictures and plain words.
    ///   2. Today's jobs: a checklist of 1-3 steps (with one-click "Do it" buttons and an arrow in the world).
    ///   3. End the day: one big button.
    /// Big picture gauges (Good / Low / Danger), big clickable system tiles (no walking needed),
    /// HAB as a friendly face with one sentence at a time. Built in code with UIKit + Icons (works in WebGL).
    /// </summary>
    [RequireComponent(typeof(MissionRunner))]
    public partial class PlaytestUI : MonoBehaviour
    {
        public static PlaytestUI Current { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatic() => Current = null;

        [Tooltip("On for the 3D scene (world arrow, click-to-use). Off = flat test bench.")]
        public bool worldMode;

        /// <summary>Kid-friendly names and icons for the five systems.</summary>
        public static readonly Dictionary<string, (string name, string icon)> SystemLook = new Dictionary<string, (string, string)>
        {
            { SystemIds.LifeSupport, ("Air maker", "air") }, { SystemIds.Heater, ("Heater", "heat") }, { SystemIds.Radio, ("Radio", "radio") },
            { SystemIds.Greenhouse, ("Plant lights", "plant") }, { SystemIds.Science, ("Science lab", "lab") },
        };
        public static string KidName(string id) => SystemLook.TryGetValue(id, out var v) ? v.name : id;

        class Gauge { public string name; public Image icon, fillImage; public Text label, detail; public RectTransform fill; }
        class Tile { public SystemDef def; public Button button; public Image bg, icon; public Text name, state, cost; }
        class JobRow { public DayPlanner.Job job; public Image check, bg; public Text text, why; public Button action; }
        bool StepMode => runner != null && runner.HasSteps;

        MissionRunner runner;
        FirstPersonController fpc;
        Canvas canvas;
        CanvasGroup hudGroup;
        RectTransform hudRoot;

        // HUD
        Gauge power, air;
        Text foodText, dayText, goalText, stampText, promptText, hintLine;
        Image stampIcon, crosshair;
        GameObject promptBox;
        readonly List<Tile> tiles = new List<Tile>();
        RectTransform jobsList;
        Text jobsFooter;
        readonly List<JobRow> jobRows = new List<JobRow>();
        Button endDay;
        Image endDayBg;
        Text endDayLabel, endDaySub;

        // HAB speech
        Image habFace;
        Text habHeader, habText;
        CanvasGroup habGroup;
        readonly Queue<string> habQueue = new Queue<string>();
        float habTimer, habIdle;
        readonly List<string> seenWords = new List<string>();

        // Waypoint arrow
        RectTransform marker, markerArrow;
        Text markerLabel;
        string markerTarget;
        readonly Dictionary<string, SystemPanel> panelsById = new Dictionary<string, SystemPanel>();
        SleepPod bunk;
        SampleSite[] sampleSites;
        SolarPanelStation[] solarStations;
        PlayerLocation playerLocation;

        // Overlays
        GameObject briefing, report, cardOverlay, pauseOverlay, debriefOverlay, glossaryOverlay, infoOverlay;
        RectTransform briefingBody, reportBody;
        Action briefingStart;
        Card pendingCard;
        int lastDay = -1;
        Text cardPrompt, cardSol, pauseTitle, pauseGoal, glossWord, glossMeaning, infoOver, infoTitle, infoBody, infoBadge;
        Transform cardOptions, pauseWords;
        GameObject pauseHelp;
        Button pauseQuit;
        // debrief
        bool lastSuccess;
        Text dTitle, dReason, dChoices, dRealTeam, dTryNext, dSciTitle, dSciText, dSources, dRewards, dMapLabel, dLesson;
        Image[] dStars;
        Text[] dScoreLabels;
        RectTransform[] dScoreFills, dSciPhoto;
        Button dMapButton, dTitleButton, dRetryButton;
        Image dRetryImage, dMapImage;

        GameObject[] Overlays => new[] { briefing, report, cardOverlay, pauseOverlay, debriefOverlay, glossaryOverlay, infoOverlay, cutscene, confirmOverlay, rockOverlay };
        bool AnyOverlay => Overlays.Any(o => o && o.activeSelf);

        /// <summary>True while a pop-up is open (the player controller stops moving).</summary>
        public bool WantsCursor => canvas && canvas.enabled && AnyOverlay;

        bool MissionRunning => runner.Mission != null && runner.State != null && !runner.IsOver;

        public const string HowToPlay =
            "<b>1.</b> Every morning, read the <b>Mars Report</b>. It uses real NASA data.\n" +
            "<b>2.</b> Do <b>Today's missions</b>. Follow the arrow and walk there. Outside, your air drops every second!\n" +
            "<b>3.</b> Press <b>End the day</b> (or N) to sleep.\n" +
            "Walk: W A S D.   Turn: arrow keys, or hold the right mouse button.";

        // ───────────────────────── public API ─────────────────────────

        public void SetPrompt(string s)
        {
            if (!promptText || promptText.text == s) return;
            promptText.text = s;
            promptBox.SetActive(!string.IsNullOrEmpty(s));
        }

        public void SetVisible(bool visible)
        {
            if (!visible) ClearForNewMission();
            if (canvas) canvas.enabled = visible;
        }

        public void ClearForNewMission()
        {
            foreach (var o in Overlays) if (o) o.SetActive(false);
            habQueue.Clear();
            habTimer = 0;
            habIdle = 99;
            if (habGroup) habGroup.alpha = 0;
            seenWords.Clear();
            pendingCard = null;
            lastDay = -1;
            SetPrompt("");
        }

        // ───────────────────────── lifecycle ─────────────────────────

        void Awake()
        {
            Current = this;
            runner = GetComponent<MissionRunner>();
            if (!worldMode && Camera.main == null)
            {
                var cam = new GameObject("Main Camera").AddComponent<Camera>();
                cam.tag = "MainCamera";
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.07f, 0.08f, 0.13f);
            }
            EnsureEventSystem();
            Build();
            ClearForNewMission();

            runner.StateChanged += Refresh;
            runner.HabSays += OnHabSays;
            runner.CardShown += ShowCard;
            runner.MissionEnded += ShowDebrief;
            runner.MissionStarted += OnMissionStarted;
            runner.ResearchEarned += ShowResearchPopup;
        }

        // ── Outside: a live air timer (every second outside costs air). Sits just under the Day card, whatever its height.
        GameObject evaBox;
        Image evaBg;
        Text evaText;
        RectTransform dayCard;

        /// <summary>Top of the free space under the Day card (banners stack from here).</summary>
        float UnderDayCard => -(16f + (dayCard ? dayCard.rect.height : 120f) + 10f);

        void BuildEva(Transform hud)
        {
            evaBg = Box("Outside", hud, new Color(0.45f, 0.12f, 0.08f, 0.88f));
            var r = evaBg.rectTransform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 1);
            r.anchoredPosition = new Vector2(0, -150);
            r.sizeDelta = new Vector2(540, 58);
            evaBg.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;   // grows with its text
            var h = HList(evaBg, 12, 10, TextAnchor.MiddleLeft);
            h.childForceExpandHeight = false;
            IconImg(evaBg.transform, "air", 30, Ink);
            evaText = Label(evaBg.transform, "", 15, TextAnchor.MiddleLeft, Ink);
            LE(evaText, flexW: 1);
            evaBox = evaBg.gameObject;
            evaBox.SetActive(false);
        }

        void TickEva()
        {
            if (!evaBox) return;
            bool show = MissionRunning && worldMode && runner.State.outside && hudGroup.alpha > 0.5f;
            if (evaBox.activeSelf != show) evaBox.SetActive(show);
            if (!show) return;
            evaBg.rectTransform.anchoredPosition = new Vector2(0, UnderDayCard);
            float floor = runner.AirFloor;
            float secs = runner.SecondsOfAirLeft(floor);
            float perSec = MissionRunner.LiveAirPerSecond * runner.Level.drainMultiplier;
            evaText.text = $"<b>OUTSIDE</b>  ·  Air <b>{runner.State.oxygen:0.0}%</b>  ·  -{perSec:0.0}% every second\n" +
                           (AnyOverlay ? "Paused while you read." : $"About <b>{secs:0} seconds</b> of air left. Walk back to the airlock in time!");
            float danger = Mathf.Clamp01(1 - (runner.State.oxygen - floor) / 30f);
            float pulse = danger > 0.5f ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f) : 0f;
            evaBg.color = Color.Lerp(new Color(0.18f, 0.2f, 0.26f, 0.88f), new Color(0.75f, 0.12f, 0.08f, 0.92f), Mathf.Max(danger * 0.7f, pulse));
        }

        // "+10 research points" floats up in the middle of the screen
        Text researchPopup;
        float researchTimer;

        void ShowResearchPopup(int points)
        {
            if (!researchPopup) return;
            researchPopup.text = $"+{points} research points!";
            researchTimer = 2.2f;
        }

        void TickResearchPopup()
        {
            if (!researchPopup) return;
            researchTimer -= Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(researchTimer / 2.2f);
            researchPopup.gameObject.SetActive(researchTimer > 0);
            researchPopup.color = new Color(1f, 0.83f, 0.42f, Mathf.Clamp01(t * 3f));
            researchPopup.rectTransform.anchoredPosition = new Vector2(0, 40 + (1 - t) * 50);
        }

        void Start()
        {
            if (!worldMode) return;
            fpc = FindAnyObjectByType<FirstPersonController>();
            foreach (var p in FindObjectsByType<SystemPanel>()) panelsById[p.systemId] = p;
            bunk = FindAnyObjectByType<SleepPod>();
            sampleSites = FindObjectsByType<SampleSite>();
            playerLocation = FindAnyObjectByType<PlayerLocation>();
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
            if (!runner) return;
            runner.StateChanged -= Refresh;
            runner.HabSays -= OnHabSays;
            runner.CardShown -= ShowCard;
            runner.MissionEnded -= ShowDebrief;
            runner.MissionStarted -= OnMissionStarted;
            runner.ResearchEarned -= ShowResearchPopup;
        }

        void OnMissionStarted()
        {
            hudGroup.alpha = 1;
            lastDay = -1;
            pendingCard = null;
        }

        void Update()
        {
            runner.Paused = !canvas.enabled || AnyOverlay;   // the air clock stops while you read a pop-up
            if (!canvas.enabled) return;
            TickSubtitles();
            TickResearchPopup();
            TickEva();
            TickBanners();

            var kb = Keyboard.current;
            if (kb != null)
            {
                bool enter = kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame;
                if (briefing.activeSelf && enter) StartFromBriefing();
                else if (report.activeSelf && enter) CloseReport();
                else if (kb.tabKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)
                {
                    if (glossaryOverlay.activeSelf) glossaryOverlay.SetActive(false);
                    else if (infoOverlay.activeSelf) infoOverlay.SetActive(false);
                    else if (pauseOverlay.activeSelf) pauseOverlay.SetActive(false);
                    else if (!AnyOverlay && MissionRunning) OpenPause();
                }
                else if (kb.nKey.wasPressedThisFrame && !AnyOverlay && MissionRunning) Sleep();

                if (cardOverlay.activeSelf && runner.ActiveCard != null)
                    for (int i = 0; i < runner.ActiveCard.options.Length && i < 4; i++)
                        if (kb[Key.Digit1 + i].wasPressedThisFrame && runner.CanChoose(runner.ActiveCard.options[i])) PickOption(i);
            }

            // Glow the End-the-day button once all jobs are done.
            if (MissionRunning && !AnyOverlay && AllJobsDone())
                endDayBg.color = Color.Lerp(Accent, new Color(1f, 0.72f, 0.4f), 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f));

            if (worldMode)
            {
                if (crosshair) crosshair.gameObject.SetActive(fpc && fpc.Looking);
                UpdateMarker();
            }
        }

        // ───────────────────────── building ─────────────────────────

        static Image IconImg(Transform parent, string icon, float size, Color c)
        {
            var img = Rect("Icon_" + icon, parent).gameObject.AddComponent<Image>();
            img.sprite = Icons.Get(icon);
            img.preserveAspect = true;
            img.color = c;
            img.raycastTarget = false;
            LE(img, size, size).minWidth = size;
            return img;
        }

        static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        void Build()
        {
            var root = UIKit.Canvas(transform, "HUD", 10);
            canvas = root.GetComponent<Canvas>();
            if (!worldMode) Fill(Box("Background", root, new Color(0.07f, 0.08f, 0.13f)).rectTransform);

            var hud = Rect("HudGroup", root);
            Fill(hud);
            hudGroup = hud.gameObject.AddComponent<CanvasGroup>();
            hudRoot = hud;

            if (worldMode) BuildMarker(hud);
            BuildGauges(hud);
            BuildDay(hud);
            BuildJobs(hud);
            BuildTiles(hud);
            BuildHab(hud);
            BuildEndDay(hud);
            if (worldMode) BuildCenter(hud);
            if (worldMode) BuildEva(hud);
            BuildBanners(hud);
            researchPopup = Label(hud, "", 34, TextAnchor.MiddleCenter, Ink, FontStyle.Bold);
            researchPopup.horizontalOverflow = HorizontalWrapMode.Overflow;
            researchPopup.raycastTarget = false;
            researchPopup.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.6f);
            Center(researchPopup.rectTransform, new Vector2(0, 40), new Vector2(600, 50));
            researchPopup.gameObject.SetActive(false);

            BuildBriefing(root);
            BuildReport(root);
            BuildCard(root);
            BuildPause(root);
            BuildDebrief(root);
            BuildInfo(root);
            BuildGlossary(root);
            BuildScenes(root);
        }

        static RectTransform Card(Transform parent, string name, Vector2 anchor, Vector2 pos, float width)
        {
            var box = Box(name, parent, Glass);
            var r = box.rectTransform;
            r.anchorMin = r.anchorMax = anchor;
            r.pivot = anchor;
            r.anchoredPosition = pos;
            r.sizeDelta = new Vector2(width, 0);
            VList(box, 6, 14);
            box.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return r;
        }

        void BuildGauges(Transform hud)
        {
            var c = Card(hud, "Gauges", new Vector2(0, 1), new Vector2(16, -16), 300);
            power = MakeGauge(c, "battery", "Battery");
            air = MakeGauge(c, "air", "Air");
            foodText = Label(c, "", 14, TextAnchor.UpperLeft, Muted);
        }

        Gauge MakeGauge(Transform parent, string icon, string name)
        {
            var g = new Gauge { name = name };
            var row = Rect(name, parent);
            HList(row, 10, 0);
            LE(row, prefH: 46);
            g.icon = IconImg(row, icon, 40, Good);
            var col = Rect("Col", row);
            VList(col, 5, 0, TextAnchor.MiddleLeft);
            LE(col, flexW: 1);
            g.label = Label(col, name, 18, TextAnchor.MiddleLeft, Ink, FontStyle.Bold);
            g.fill = Bar(col, 10, new Color(1, 1, 1, 0.12f), Good, out g.fillImage);
            g.detail = Label(parent, "", 13, TextAnchor.UpperLeft, Muted);
            return g;
        }

        void SetGauge(Gauge g, float value01, string word, Color color, string detail)
        {
            g.label.text = $"{g.name}   <color={Hex(color)}>{word}</color>";
            g.icon.color = color;
            g.fill.anchorMax = new Vector2(Mathf.Clamp01(value01), 1);
            g.fillImage.color = color;
            g.detail.text = detail;
        }

        void BuildDay(Transform hud)
        {
            var c = Card(hud, "Day", new Vector2(0.5f, 1), new Vector2(0, -16), 470);
            dayCard = c;
            c.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.UpperCenter;
            dayText = Label(c, "", 26, TextAnchor.UpperCenter, Ink, FontStyle.Bold);
            goalText = Label(c, "", 15, TextAnchor.UpperCenter, Muted);
            var stamp = Rect("Stamp", c);
            HList(stamp, 6, 0, TextAnchor.MiddleCenter);
            LE(stamp, prefH: 22);
            stampIcon = IconImg(stamp, "satellite", 18, Good);
            stampText = Label(stamp, "", 13, TextAnchor.MiddleLeft, Ink);
            stampText.horizontalOverflow = HorizontalWrapMode.Overflow;
            var row = Rect("Buttons", c);
            HList(row, 8, 0, TextAnchor.MiddleCenter);
            LE(row, prefH: 32);
            LE(Btn(row, "Mars Report", 14, Quiet, 0, () => { if (MissionRunning && !AnyOverlay) OpenReport(); }), prefW: 150);
            LE(Btn(row, "Menu", 14, Quiet, 0, () => { if (MissionRunning && !AnyOverlay) OpenPause(); }), prefW: 100);
        }

        void BuildJobs(Transform hud)
        {
            var c = Card(hud, "Jobs", new Vector2(1, 1), new Vector2(-16, -16), 360);
            Overline(c, "Today's missions", Warn);
            jobsList = Rect("List", c);
            VList(jobsList, 6, 0);
            jobsFooter = Label(c, "", 13, TextAnchor.UpperLeft, Muted);
        }

        void BuildTiles(Transform hud)
        {
            var row = Rect("Systems", hud);
            row.anchorMin = row.anchorMax = row.pivot = Vector2.zero;
            row.anchoredPosition = new Vector2(16, 16);
            row.sizeDelta = new Vector2(5 * 100 + 4 * 8, 112);
            HList(row, 8, 0);
            foreach (var def in GameManager.Instance.Config.systems)
            {
                var t = new Tile { def = def };
                var d = def;
                t.button = Btn(row, "", 14, Quiet, 0, () => { if (MissionRunning) runner.SetSystem(d.id, !runner.State.IsOn(d.id)); });
                LE(t.button, prefW: 100);
                Clear(t.button.transform);
                t.bg = t.button.GetComponent<Image>();
                var v = Rect("V", t.button.transform);
                Fill(v);
                VList(v, 2, 8, TextAnchor.UpperCenter);
                t.icon = IconImg(v, SystemLook.TryGetValue(def.id, out var look) ? look.icon : "box", 34, Ink);
                t.name = Label(v, KidName(def.id), 13, TextAnchor.UpperCenter, Ink, FontStyle.Bold);
                t.state = Label(v, "", 13, TextAnchor.UpperCenter, Ink, FontStyle.Bold);
                t.cost = Label(v, "", 11, TextAnchor.UpperCenter, Muted);
                tiles.Add(t);
            }
            hintLine = Label(hud, worldMode ? "Click a tile to switch it ON or OFF.   Walk: W A S D.   Turn: arrow keys (or hold the right mouse button)." : "Click a tile to switch it ON or OFF.", 13, TextAnchor.LowerLeft, Muted);
            hintLine.gameObject.AddComponent<Shadow>();
            Anchor(hintLine.rectTransform, Vector2.zero, new Vector2(0, 0), new Vector2(18, 132), new Vector2(18 + 700, 152));
        }

        void BuildHab(Transform hud)
        {
            var box = Box("HAB", hud, GlassDark);
            var r = box.rectTransform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0);
            r.anchoredPosition = new Vector2(30, 160);
            r.sizeDelta = new Vector2(560, 0);
            HList(box, 12, 12, TextAnchor.UpperLeft).childForceExpandHeight = false;
            box.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            habFace = IconImg(box.transform, "hab", 52, new Color(0.56f, 0.84f, 1f));
            var col = Rect("Col", box.transform);
            VList(col, 3, 0);
            LE(col, flexW: 1);
            habHeader = Overline(col, "HAB", new Color(0.56f, 0.84f, 1f));
            habText = Label(col, "", 18);
            habGroup = box.gameObject.AddComponent<CanvasGroup>();
            habGroup.blocksRaycasts = false;
        }

        void BuildEndDay(Transform hud)
        {
            endDay = Btn(hud, "", 20, Accent, 0, () => { if (MissionRunning && !AnyOverlay) Sleep(); });
            Clear(endDay.transform);
            endDayBg = endDay.GetComponent<Image>();
            var r = endDayBg.rectTransform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(1, 0);
            r.anchoredPosition = new Vector2(-16, 16);
            r.sizeDelta = new Vector2(260, 104);
            var h = Rect("H", endDay.transform);
            Fill(h);
            HList(h, 10, 14, TextAnchor.MiddleLeft);
            IconImg(h, "moon", 42, Ink);
            var col = Rect("Col", h);
            VList(col, 2, 0, TextAnchor.MiddleLeft);
            LE(col, flexW: 1);
            endDayLabel = Label(col, "End the day", 22, TextAnchor.MiddleLeft, Ink, FontStyle.Bold);
            endDaySub = Label(col, "", 12, TextAnchor.MiddleLeft, new Color(1, 1, 1, 0.8f));
        }

        void BuildCenter(Transform hud)
        {
            crosshair = Box("Crosshair", hud, new Color(1, 1, 1, 0.8f), 8);
            Center(crosshair.rectTransform, Vector2.zero, new Vector2(6, 6));
            crosshair.gameObject.SetActive(false);

            var pb = Box("Prompt", hud, GlassDark);
            var pr = pb.rectTransform;
            pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(0.5f, 0.5f);
            pr.anchoredPosition = new Vector2(0, -60);
            HList(pb, 0, 10, TextAnchor.MiddleCenter);
            var fit = pb.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            promptText = Label(pb.transform, "", 18, TextAnchor.MiddleCenter);
            promptText.horizontalOverflow = HorizontalWrapMode.Overflow;
            promptBox = pb.gameObject;
            promptBox.SetActive(false);
        }

        GameObject Overlay(Transform root, string name, float width, out RectTransform panel)
        {
            var blocker = Rect(name, root);
            Fill(blocker);
            blocker.gameObject.AddComponent<Image>().color = new Color(0.01f, 0.01f, 0.02f, 0.82f);
            panel = Box("Panel", blocker, GlassDark, 1.2f).rectTransform;
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(width, 0);
            VList(panel, 12, 26);
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            blocker.gameObject.SetActive(false);
            return blocker.gameObject;
        }

        // ───────────────────────── refresh (every state change) ─────────────────────────

        void Refresh(HabitatState s)
        {
            var m = runner.Mission;
            var cfg = runner.Config;
            var lvl = runner.Level;
            var today = DayPlanner.Today(runner);
            float pTrend = SimEngine.NetPowerPreview(s, today, cfg, lvl);
            float oTrend = SimEngine.NetOxygenPreview(s, today, cfg, lvl);

            SetGauge(power, s.power / 100f, Word(s.power), Level(s.power),
                $"{s.power:0}%   ·   {(pTrend >= 0 ? "+" : "")}{pTrend:0} tonight" + (s.panelDust >= 0.2f ? $"   ·   panels {s.panelDust * 100:0}% dusty" : ""));
            float perSec = MissionRunner.LiveAirPerSecond * lvl.drainMultiplier;
            SetGauge(air, s.oxygen / 100f, Word(s.oxygen), Level(s.oxygen),
                s.outside ? $"<color=#FFB0A0><b>{s.oxygen:0.0}%   ·   -{perSec:0.0}% every second outside</b></color>"
                : s.IsOn(SystemIds.LifeSupport) ? $"{s.oxygen:0}%   ·   {(oTrend >= 0 ? "+" : "")}{oTrend:0} tonight" : "<color=#F08080>Air maker is OFF!</color>");
            foodText.text = (s.outside ? "<color=#FFB0A0><b>Outside</b></color>" : "Inside the base") + "\n" +
                $"Food: {s.food} days      Rocks: {s.samples}" + (s.samplesStudied > 0 ? $" ({s.samplesStudied} studied)" : "")
                + $"\n<color=#FFD36B><b>Research points: {s.research}</b></color>      Suit: " + (s.suitOn ? "<color=#9CFFB8><b>ON</b></color>" : "<color=#FFFFFF99>on the stand</color>");

            int maxDays = Mathf.Max(m.goal?.maxSols ?? 0, m.goal?.target ?? 0);
            dayText.text = StepMode
                ? $"DAY {DayPlanner.Day(s)}  ·  STEP {Mathf.Min(s.stepIndex + 1, m.steps.Length)} OF {m.steps.Length}"
                : $"DAY {Mathf.Min(DayPlanner.Day(s), Mathf.Max(1, maxDays))} OF {maxDays}";
            goalText.text = MissionText.Goal(m);
            bool real = today.source != null && today.source.kind != "simulated";
            stampIcon.color = real ? Good : new Color(1, 1, 1, 0.5f);
            string dateNow = System.DateTime.Now.ToString("d MMM yyyy", System.Globalization.CultureInfo.InvariantCulture);
            string liveTag = NasaLive.State == NasaLive.Status.Live ? $"  ·  <color=#6CFF8F>NASA live {dateNow}</color>" : $"  ·  <color=#FFFFFF77>{dateNow}</color>";
            stampText.text = (real ? $"<color=#6CFF8F><b>Real NASA data</b></color> <color=#FFFFFF99>{today.source.date}</color>" : "<color=#FFFFFF99>Practice sky (made up)</color>") + liveTag;

            bool canAct = !runner.IsOver && runner.ActiveCard == null;
            foreach (var t in tiles)
            {
                bool on = s.IsOn(t.def.id);
                t.bg.color = on ? new Color(0.18f, 0.55f, 0.33f, 0.92f) : new Color(0.12f, 0.12f, 0.16f, 0.85f);
                t.icon.color = on ? Ink : new Color(1, 1, 1, 0.4f);
                t.state.text = on ? "<color=#9CFFB8>ON</color>" : "<color=#FFFFFF77>OFF</color>";
                t.cost.text = $"uses {t.def.powerPerSol * lvl.drainMultiplier:0} power";
                t.button.interactable = canAct;
            }

            // New day -> a fresh job list (the first job is: check the NASA forecast)
            if (!runner.IsOver && s.sol != lastDay)
            {
                lastDay = s.sol;
                BuildJobRows();
            }
            else SyncJobs();
            RefreshJobs();
        }

        static string Word(float v) => v >= 50 ? "Good" : v >= 25 ? "Low" : "Danger";
        static Color Level(float v) => v >= 50 ? Good : v >= 25 ? Warn : Bad;

        // ───────────────────────── jobs ─────────────────────────

        bool ShowJobs => StepMode || runner.Level.id != "commander";

        void BuildJobRows()
        {
            Clear(jobsList);
            jobRows.Clear();
            if (!ShowJobs) return;
            foreach (var job in DayPlanner.Jobs(runner)) AddJobRow(job);
        }

        /// <summary>
        /// Keeps the list in step with the day: a job with the same key gets its new text (e.g. the ice map arrived),
        /// new jobs are added (e.g. study the rock you just found), finished jobs stay ticked, stale unfinished ones go away.
        /// </summary>
        void SyncJobs()
        {
            if (!ShowJobs || !MissionRunning) return;
            var s = runner.State;
            var fresh = DayPlanner.Jobs(runner);
            if (StepMode)
            {
                // Story steps: the list is exactly what the planner says (last step, current, next two)
                string sig = string.Join("|", fresh.Select(j => j.key + j.text + j.why + j.action + j.stepLocked));
                string old = string.Join("|", jobRows.Select(r => r.job.key + r.job.text + r.job.why + r.job.action + r.job.stepLocked));
                if (sig == old) return;
                Clear(jobsList);
                jobRows.Clear();
                foreach (var j in fresh) AddJobRow(j);
                return;
            }
            bool dirty = false;
            var merged = new List<DayPlanner.Job>();
            foreach (var r in jobRows)
            {
                var f = fresh.FirstOrDefault(j => j.key == r.job.key);
                bool doneNow = r.job.done != null && r.job.done(s);
                if (f != null)
                {
                    if (f.text != r.job.text || f.why != r.job.why || f.action != r.job.action || f.stepLocked != r.job.stepLocked) dirty = true;
                    merged.Add(doneNow && (f.done == null || f.done(s)) ? r.job : f);
                }
                else if (doneNow) merged.Add(r.job);
                else dirty = true;   // no longer needed
            }
            foreach (var f in fresh)
                if (!merged.Any(j => j.key == f.key) && !(f.done != null && f.done(s)) && merged.Count < 8) { merged.Add(f); dirty = true; }
            if (!dirty) return;
            Clear(jobsList);
            jobRows.Clear();
            foreach (var j in merged) AddJobRow(j);
        }

        void AddJobRow(DayPlanner.Job job)
        {
            var j = job;
            var row = new JobRow { job = j };
            row.bg = Box("Job", jobsList, CardBg);
            HList(row.bg, 10, 8, TextAnchor.MiddleLeft).childForceExpandHeight = false;
            row.check = IconImg(row.bg.transform, "box", 26, Ink);
            var col = Rect("Col", row.bg.transform);
            VList(col, 1, 0, TextAnchor.MiddleLeft);
            LE(col, flexW: 1);
            row.text = Label(col, "", 15, TextAnchor.MiddleLeft, Ink, FontStyle.Bold);
            row.why = Label(col, j.why, 12, TextAnchor.MiddleLeft, Muted);
            // No teleport buttons ("Go", "Go now"): you walk there yourself, and every second outside costs air.
            bool teleport = j.action != null && (j.action.StartsWith("go:") || j.action == "shelter" || j.action == "inside");
            if (!string.IsNullOrEmpty(j.action) && !teleport)
            {
                row.action = Btn(row.bg.transform, j.actionLabel ?? "Do it", 14, Accent, 0, () => DoAction(j));
                LE(row.action, 70, 32);
            }
            jobRows.Add(row);
        }

        static bool Done(DayPlanner.Job j, HabitatState s) => j.done != null && j.done(s);
        static bool Locked(DayPlanner.Job j, HabitatState s) => j.stepLocked || (j.afterForecast && !s.forecastSeen);

        void RefreshJobs()
        {
            if (!ShowJobs)
            {
                jobsFooter.text = "Commander: no job list. Read the Mars Report and decide for yourself!";
                endDaySub.text = "Sleep until tomorrow";
                endDayBg.color = Accent;
                markerTarget = runner.State != null && runner.State.iceAccepted && !runner.State.iceDrilled ? "ice" : null;
                return;
            }
            var s = runner.State;
            int left = 0;
            foreach (var r in jobRows)
            {
                bool done = Done(r.job, s);
                bool locked = !done && Locked(r.job, s);
                if (!done && !r.job.optional) left++;
                r.check.sprite = Icons.Get(done ? "check" : "box");
                r.check.color = done ? Good : locked ? new Color(1, 1, 1, 0.3f) : Ink;
                string opt = r.job.optional && !done ? "  <color=#FFD36B>(optional)</color>" : "";
                r.text.text = done ? $"<color=#9CFFB8>{r.job.text}</color>" : locked ? $"<color=#FFFFFF66>{r.job.text}</color>" : r.job.text + (r.job.text.Contains("optional") ? "" : opt);
                r.why.text = locked ? (r.job.stepLocked ? "Coming up next." : "Check the forecast first.") : r.job.why;
                bool red = r.job.red && !done;
                r.bg.color = done ? new Color(0.36f, 0.80f, 0.52f, 0.12f)
                    : red ? new Color(0.75f, 0.12f, 0.08f, 0.55f + 0.3f * Mathf.PingPong(Time.unscaledTime * 2f, 1f))
                    : !locked && StepMode ? new Color(0.93f, 0.52f, 0.24f, 0.22f) : CardBg;
                if (red) r.text.text = $"<color=#FFD0C8>{r.job.text}</color>";
                if (r.action) r.action.gameObject.SetActive(!done && !locked);
            }

            // End the day: only when you're allowed to sleep (inside, and in story steps only when it's bedtime)
            string cant = MissionRunning ? runner.CantSleepReason() : "";
            if (StepMode)
            {
                jobsFooter.text = runner.CurrentStep != null ? $"Step {runner.State.stepIndex + 1} of {runner.Mission.steps.Length}" : "All done!";
                endDaySub.text = cant == null ? "Time for bed!" : s.outside ? "Go inside to sleep" : "Not bedtime yet";
                endDayBg.color = cant == null ? Accent : new Color(0.25f, 0.25f, 0.3f, 0.9f);
            }
            else
            {
                jobsFooter.text = jobRows.Count == 0 ? "No missions today. Explore, then end the day!" : left == 0 ? "All done! Now press End the day." : "Then press End the day.";
                endDaySub.text = s.outside ? "Go inside to sleep" : left == 0 ? "All missions done. Sleep!" : $"{left} mission{(left == 1 ? "" : "s")} left";
                if (left > 0 || s.outside) endDayBg.color = s.outside ? new Color(0.25f, 0.25f, 0.3f, 0.9f) : new Color(0.55f, 0.32f, 0.18f, 0.95f);
            }

            // Arrow in the world: first open job (Easy level, and always in story steps), or the shelter / ice trip (any level).
            var next = jobRows.FirstOrDefault(r => !Done(r.job, s) && !Locked(r.job, s) && !string.IsNullOrEmpty(r.job.target));
            string target = next?.job.target;
            bool always = target == "shelter" || target == "ice";
            if (!always && !StepMode && s.iceAccepted && !s.iceDrilled && s.outside) target = "ice";
            markerTarget = runner.Level.hintsOn || StepMode || target == "shelter" || target == "ice" ? target : null;
        }

        bool AllJobsDone() => StepMode ? runner.CantSleepReason() == null : !ShowJobs || (!runner.State.outside && jobRows.All(r => r.job.optional || Done(r.job, runner.State)));

        void DoAction(DayPlanner.Job j)
        {
            if (!MissionRunning || AnyOverlay) return;
            switch (j.action)
            {
                case "forecast": OpenReport(); break;
                case "status": runner.SendStatus(); break;
                case "talk": HabCall(); break;
                case "checkStatus": StatusCheck(); break;
                case "suit": PlaySuitScene(true, null); break;
                case "scan": PlayScan(); break;
                case "sleep": Sleep(); break;
                case "go:samples": GoOutsideTo("samples", "Click the rock to collect it."); break;
                case "go:solar": GoOutsideTo("solar", "Click the panels to clean them."); break;
                case "go:rover": GoOutsideTo("rover", "Click the rover and stay close while it's fixed."); break;
                case "go:weather": GoOutsideTo("weather", "Click the weather station to read it."); break;
                case "shelter":
                    if (fpc && playerLocation)
                    {
                        var b = playerLocation.shelter;
                        fpc.TeleportTo(new Vector3(b.center.x, b.min.y + 0.05f, b.center.z), Vector3.zero);
                    }
                    runner.SetLocation(false, true);
                    break;
                case "inside":
                    GoInside();
                    break;
            }
            RefreshJobs();
        }

        /// <summary>One click takes you out next to the job. From then on your air drains until you come back in.</summary>
        void GoOutsideTo(string target, string tip)
        {
            if (!fpc || !TryTarget(target, out var pos, out _)) return;
            if (!runner.State.suitOn) { PlaySuitScene(true, () => GoOutsideTo(target, tip)); return; }   // suit first!
            var flat = new Vector3(-pos.x, 0, -pos.z);   // the base is at the middle of the map
            var stand = pos + (flat.sqrMagnitude > 0.01f ? flat.normalized : Vector3.forward) * 3f;
            var terrain = Terrain.activeTerrain;
            stand.y = terrain ? terrain.SampleHeight(stand) + terrain.transform.position.y + 0.1f : pos.y;
            fpc.TeleportTo(stand, pos);
            runner.SetLocation(true, false);
            runner.Say($"HAB: You're outside. {tip} Your air drops every second out here, so come back in soon!");
        }

        void GoInside()
        {
            if (fpc) fpc.ReturnHome();
            runner.SetLocation(false, false);
        }

        // ───────────────────────── Mars Report ─────────────────────────

        void BuildReport(Transform root)
        {
            report = Overlay(root, "MarsReport", 880, out var p);
            reportBody = Rect("Body", p);
            VList(reportBody, 12, 0);
            Btn(p, "<b>Let's go!</b>     <color=#FFFFFF99><size=14>Enter</size></color>", 21, Accent, 56, CloseReport);
        }

        void OpenReport()
        {
            if (!MissionRunning) return;
            runner.ForecastSeen();   // job 1 of every day
            var s = runner.State;
            var e = DayPlanner.Today(runner);
            Clear(reportBody);

            var head = Rect("Head", reportBody);
            HList(head, 10, 0, TextAnchor.MiddleLeft);
            IconImg(head, "satellite", 34, Good);
            var hc = Rect("HC", head);
            VList(hc, 0, 0);
            LE(hc, flexW: 1);
            Overline(hc, "Mars Report", Good);
            Label(hc, $"Day {DayPlanner.Day(s)}: here is today's sky", 28, TextAnchor.UpperLeft, Ink, FontStyle.Bold);

            var row = Rect("Row", reportBody);
            HList(row, 12, 0, TextAnchor.UpperLeft).childForceExpandHeight = false;
            var photo = NasaPhoto(row, runner.Mission.id, 150, false);
            if (photo) LE(photo, prefW: 280, flexW: 0).minWidth = 260;
            var grid = Rect("Readings", row);
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(250, 78);
            g.spacing = new Vector2(8, 8);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = photo ? 2 : 3;
            LE(grid, flexW: 1);
            foreach (var rd in DayPlanner.Readings(e))
            {
                var b = Box(rd.name, grid, new Color(rd.color.r, rd.color.g, rd.color.b, 0.13f));
                HList(b, 10, 10, TextAnchor.MiddleLeft).childForceExpandHeight = false;
                IconImg(b.transform, rd.icon, 36, rd.color);
                var c = Rect("C", b.transform);
                VList(c, 0, 0, TextAnchor.MiddleLeft);
                LE(c, flexW: 1);
                Label(c, rd.name, 12, TextAnchor.MiddleLeft, Muted);
                Label(c, rd.word, 19, TextAnchor.MiddleLeft, rd.color, FontStyle.Bold);
                Label(c, rd.detail, 11, TextAnchor.MiddleLeft, Muted);
            }

            // Where today's numbers come from (honest label)
            bool real = e.source != null && e.source.kind != "simulated";
            var stamp = Box("Stamp", reportBody, real ? new Color(0.36f, 0.80f, 0.52f, 0.14f) : new Color(1, 1, 1, 0.05f));
            HList(stamp, 10, 10, TextAnchor.MiddleLeft).childForceExpandHeight = false;
            IconImg(stamp.transform, real ? "satellite" : "book", 24, real ? Good : Muted);
            Label(stamp.transform, real
                ? $"<b><color=#6CFF8F>REAL NASA DATA</color></b>  {e.source.label}  <color=#FFFFFF99>({e.source.date})</color>"
                : "<b>PRACTICE DAY</b>  <color=#FFFFFFAA>These numbers are made up, based on real Mars science.</color>", 14);

            // What it means today
            var mean = Rect("Mean", reportBody);
            HList(mean, 10, 0, TextAnchor.MiddleLeft).childForceExpandHeight = false;
            bool danger = e.tau >= 4;
            IconImg(mean, danger ? "alert" : "check", 30, danger ? Bad : Good);
            Label(mean, DayPlanner.Headline(e, s), 22, TextAnchor.MiddleLeft, Ink, FontStyle.Bold);
            if (!string.IsNullOrEmpty(runner.Mission.forecastNote))
                Label(reportBody, runner.Mission.forecastNote, 17, TextAnchor.UpperLeft, Warn, FontStyle.Bold);

            // Real NASA data right now: live space weather from DONKI + today's real Mars date
            bool live = NasaLive.State == NasaLive.Status.Live;
            var now = Box("NasaNow", reportBody, live ? new Color(0.20f, 0.42f, 0.30f, 1f) : new Color(0.22f, 0.22f, 0.27f, 1f));
            VList(now, 4, 12);
            Overline(now.transform, live ? "Real NASA data · right now" : "Real NASA data · saved", live ? Good : Muted);
            Label(now.transform, $"<b>{NasaLive.BadgeLine()}</b>", 13, TextAnchor.UpperLeft, live ? new Color(0.7f, 1f, 0.8f) : Muted);
            Label(now.transform, NasaLive.Summary(), 15);
            Label(now.transform, NasaLive.MarsDateLine(), 13, TextAnchor.UpperLeft, new Color(0.56f, 0.84f, 1f));

            if (cardOverlay.activeSelf) { pendingCard = runner.ActiveCard; cardOverlay.SetActive(false); }
            report.SetActive(true);
        }

        void CloseReport()
        {
            report.SetActive(false);
            if (pendingCard != null && runner.ActiveCard == pendingCard) { var c = pendingCard; pendingCard = null; ShowCardNow(c); }
            else pendingCard = null;
        }

        // ───────────────────────── briefing ─────────────────────────

        void BuildBriefing(Transform root)
        {
            briefing = Overlay(root, "Briefing", 920, out var p);
            briefingBody = Rect("Body", p);
            VList(briefingBody, 12, 0);
            Btn(p, "<b>Start!</b>     <color=#FFFFFF99><size=14>Enter</size></color>", 22, Accent, 58, StartFromBriefing);
        }

        static string HazardIcon(string name) => name switch
        {
            "Dust storm" => "dust", "Cold night" => "snow", "Solar storm" => "shield", "Soft sand" => "rover", "No contact" => "radio", _ => "sun",
        };

        /// <summary>Before a mission: what it's about (real event + real NASA picture), the goal, how a day works.</summary>
        public void ShowBriefing(MissionData m, Action onStart)
        {
            briefingStart = onStart;
            hudGroup.alpha = 0;
            Clear(briefingBody);

            var info = MissionLoader.Info(m.id);
            Overline(briefingBody, (m.mode == "freeplay" ? "Freeplay mission" : info != null ? "The Red Frontier  ·  " + info.stage : "Mission") + "  ·  " + m.site);
            Label(briefingBody, m.title, 44, TextAnchor.UpperLeft, Ink, FontStyle.Bold);

            var row = Rect("Row", briefingBody);
            HList(row, 14, 0, TextAnchor.UpperLeft).childForceExpandHeight = false;
            var photo = NasaPhoto(row, m.id, 170, true);
            if (photo) LE(photo, prefW: 330, flexW: 0).minWidth = 300;
            var col = Rect("Col", row);
            VList(col, 10, 0);
            LE(col, flexW: 1);
            if (info != null && m.chapter > 0) Label(col, "<b>Based on a real event:</b> " + info.realEvent, 15, TextAnchor.UpperLeft, new Color(0.56f, 0.84f, 1f));
            if (info != null && !string.IsNullOrEmpty(info.mcMessage))
            {
                var mc = Box("MC", col, new Color(0.99f, 0.80f, 0.32f, 0.10f));
                VList(mc, 4, 12);
                Overline(mc.transform, "Message from Mission Control", Gold);
                Label(mc.transform, info.mcMessage, 16);
            }
            var goal = Box("Goal", col, new Color(0.93f, 0.52f, 0.24f, 0.16f));
            VList(goal, 4, 12);
            Overline(goal.transform, "Your mission");
            Label(goal.transform, MissionText.Goal(m), 22, TextAnchor.UpperLeft, Ink, FontStyle.Bold);

            Overline(briefingBody, "How every day works", Muted);
            var steps = Rect("Steps", briefingBody);
            HList(steps, 10, 0);
            LE(steps, prefH: 70);
            Step(steps, "satellite", "1. Read the Mars Report", "Real NASA data about today");
            Step(steps, "check", "2. Do today's missions", "Walk there, then hurry back");
            Step(steps, "moon", "3. End the day", "The big orange button");

            var hz = Rect("Hazards", briefingBody);
            HList(hz, 8, 0, TextAnchor.MiddleLeft);
            LE(hz, prefH: 34);
            Label(hz, "<b>Watch out for:</b>", 15, TextAnchor.MiddleLeft, Muted);
            foreach (var h in MissionText.Hazards(m))
            {
                var chip = Box(h.name, hz, new Color(h.color.r, h.color.g, h.color.b, 0.16f));
                HList(chip, 6, 6, TextAnchor.MiddleLeft);
                IconImg(chip.transform, HazardIcon(h.name), 22, h.color);
                Label(chip.transform, h.name, 15, TextAnchor.MiddleLeft, h.color, FontStyle.Bold).horizontalOverflow = HorizontalWrapMode.Overflow;
            }

            var lvl = GameManager.Instance.Level;
            Label(briefingBody, $"Level: <b>{MenuUI.LevelName(lvl.id)}</b>" + (lvl.hintsOn ? "  ·  You get a job list and arrows that show the way." : lvl.id == "commander" ? "  ·  No job list: you decide!" : "  ·  You get a job list."), 14, TextAnchor.UpperLeft, Muted);

            briefing.SetActive(true);
        }

        static void Step(Transform parent, string icon, string head, string sub)
        {
            var b = Box(head, parent, CardBg);
            HList(b, 10, 10, TextAnchor.MiddleLeft).childForceExpandHeight = false;
            LE(b, flexW: 1);
            IconImg(b.transform, icon, 34, Warn);
            var c = Rect("C", b.transform);
            VList(c, 0, 0, TextAnchor.MiddleLeft);
            LE(c, flexW: 1);
            Label(c, head, 16, TextAnchor.MiddleLeft, Ink, FontStyle.Bold);
            Label(c, sub, 12, TextAnchor.MiddleLeft, Muted);
        }

        void StartFromBriefing()
        {
            if (!briefing.activeSelf) return;
            briefing.SetActive(false);
            hudGroup.alpha = 1;
            var start = briefingStart;
            briefingStart = null;
            start?.Invoke();
        }

        // ───────────────────────── decision cards ─────────────────────────

        void BuildCard(Transform root)
        {
            cardOverlay = Overlay(root, "CardOverlay", 800, out var p);
            var head = Rect("Head", p);
            HList(head, 10, 0, TextAnchor.MiddleLeft);
            IconImg(head, "alert", 30, Warn);
            LE(Overline(head, "Decision time  ·  every choice has a cost", Warn), flexW: 1);
            cardSol = Label(head, "", 13, TextAnchor.MiddleRight, Muted);
            LE(cardSol, prefW: 90);
            cardPrompt = Label(p, "", 28, TextAnchor.UpperLeft, Ink, FontStyle.Bold);
            cardOptions = Rect("Options", p);
            VList(cardOptions, 8, 0);
        }

        static string OptionIcon(string icon) => icon switch
        {
            "microscope" => "lab", "power" => "battery", "card" => "book", "suit" => "suit", "home" => "home", "heater" => "heat",
            "radio" => "radio", "moon" => "moon", "plant" => "plant", "sun" => "sun", "dust" => "dust", "rover" => "rover",
            "shield" => "shelter", "rock" => "rock", "snow" => "snow", "check" => "check", _ => "play",
        };

        void ShowCard(Card card)
        {
            if (report.activeSelf || briefing.activeSelf || (cutscene && cutscene.activeSelf)) { pendingCard = card; return; }   // after the Mars Report / cutscene
            ShowCardNow(card);
        }

        void ShowCardNow(Card card)
        {
            // Show the HAB line that goes with this card now.
            if (habQueue.Count > 0)
            {
                while (habQueue.Count > 1) habQueue.Dequeue();
                NextSubtitle();
            }
            Clear(cardOptions);
            cardPrompt.text = Gloss(card.prompt);
            cardSol.text = "Day " + DayPlanner.Day(runner.State);
            bool hints = runner.Level.hintsOn;

            for (int i = 0; i < card.options.Length; i++)
            {
                var opt = card.options[i];
                int index = i;
                var b = Btn(cardOptions, "", 18, CardBg, 74, () => PickOption(index));
                Clear(b.transform);
                var h = Rect("H", b.transform);
                Fill(h);
                HList(h, 12, 10, TextAnchor.MiddleLeft).childForceExpandHeight = false;
                Label(h, $"{i + 1}", 18, TextAnchor.MiddleCenter, Muted, FontStyle.Bold).rectTransform.sizeDelta = new Vector2(18, 20);
                IconImg(h, OptionIcon(opt.icon), 38, Ink);
                var c = Rect("C", h);
                VList(c, 2, 0, TextAnchor.MiddleLeft);
                LE(c, flexW: 1);
                string tip = hints && opt.HasTag("priority") ? "   <color=#FFD54F><size=13>HAB says: safest!</size></color>" : "";
                string locked = !opt.needsRadio ? ""
                    : runner.NoContact ? "   <color=#F08080><size=13>no contact with Earth now</size></color>"
                    : !runner.State.IsOn(SystemIds.Radio) ? "   <color=#F08080><size=13>needs the Radio ON</size></color>" : "";
                Label(c, $"<b>{opt.label}</b>{tip}{locked}", 18);
                Label(c, "Cost: " + opt.Describe(runner.Config), 13, TextAnchor.MiddleLeft, new Color(1f, 0.78f, 0.6f));
                b.interactable = runner.CanChoose(opt);
            }
            cardOverlay.SetActive(true);
        }

        void PickOption(int index)
        {
            cardOverlay.SetActive(false);   // hide first: Choose may show the next card
            runner.Choose(index);
        }

        // ───────────────────────── pause ─────────────────────────

        void BuildPause(Transform root)
        {
            pauseOverlay = Overlay(root, "Pause", 620, out var p);
            Overline(p, "Paused");
            pauseTitle = Label(p, "", 28, TextAnchor.UpperLeft, Ink, FontStyle.Bold);
            pauseGoal = Label(p, "", 16, TextAnchor.UpperLeft, Muted);
            Btn(p, "<b>Keep playing</b>", 19, Accent, 52, () => pauseOverlay.SetActive(false));
            Btn(p, "How to play", 16, Quiet, 42, () => pauseHelp.SetActive(!pauseHelp.activeSelf));
            pauseHelp = Label(p, HowToPlay, 16).gameObject;
            pauseHelp.SetActive(false);

            Overline(p, "Science words you've seen  ·  tap one to learn it", Muted);
            var words = Rect("Words", p);
            var grid = words.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(135, 34);
            grid.spacing = new Vector2(6, 6);
            words.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            pauseWords = words;

            var row = Rect("Row", p);
            HList(row, 8, 0);
            LE(row, prefH: 44);
            LE(Btn(row, "Start this mission again", 15, Quiet, 0, () => { ClearForNewMission(); runner.Restart(); }), flexW: 1);
            LE(Btn(row, "Stop safely", 15, Quiet, 0, () => { pauseOverlay.SetActive(false); runner.SafeAbort(); }), flexW: 1);
            pauseQuit = Btn(row, "Title screen", 15, Quiet, 0, () => GameFlow.Current?.ShowTitle());
            LE(pauseQuit, flexW: 1);
        }

        void OpenPause()
        {
            pauseTitle.text = runner.Mission.title;
            pauseGoal.text = MissionText.Goal(runner.Mission);
            pauseQuit.gameObject.SetActive(GameFlow.Current != null);
            pauseHelp.SetActive(false);
            Clear(pauseWords);
            foreach (var key in seenWords)
            {
                var k = key;
                Btn(pauseWords, ContentDb.Glossary(k)?.word ?? k, 14, new Color(0.30f, 0.55f, 0.80f, 0.4f), 0, () => ShowGlossary(k));
            }
            if (seenWords.Count == 0) Label(pauseWords, "None yet", 13, TextAnchor.MiddleLeft, Muted);
            pauseOverlay.SetActive(true);
        }

        // ───────────────────────── debrief ─────────────────────────

        void BuildDebrief(Transform root)
        {
            debriefOverlay = Overlay(root, "DebriefOverlay", 1160, out var p);

            var head = Rect("Head", p);
            HList(head, 20, 0);
            var ht = Rect("HeadText", head);
            VList(ht, 4, 0);
            LE(ht, flexW: 1);
            dTitle = Label(ht, "", 36, TextAnchor.UpperLeft, Ink, FontStyle.Bold);
            dReason = Label(ht, "", 17, TextAnchor.UpperLeft, Muted);
            var starRow = Rect("Stars", head);
            HList(starRow, 6, 0, TextAnchor.MiddleRight).childForceExpandHeight = false;
            LE(starRow, prefW: 200);
            dStars = new[] { Star(starRow, 58), Star(starRow, 58), Star(starRow, 58) };

            var score = Rect("Score", p);
            HList(score, 16, 0);
            string[] names = { "Safety", "Smart choices", "Procedures", "Safe trips", "Science" };
            dScoreLabels = new Text[5];
            dScoreFills = new RectTransform[5];
            for (int i = 0; i < 5; i++)
            {
                var col = Rect(names[i], score);
                VList(col, 4, 0);
                LE(col, flexW: 1);
                dScoreLabels[i] = Label(col, names[i], 13, TextAnchor.UpperLeft, Muted);
                dScoreFills[i] = Bar(col, 6, new Color(1, 1, 1, 0.12f), Gold, out _);
            }

            var cols = Rect("Columns", p);
            HList(cols, 12, 0, TextAnchor.UpperLeft);
            dChoices = DebriefPanel(cols, "What you did");
            dRealTeam = DebriefPanel(cols, "What the real rover team did");
            dTryNext = DebriefPanel(cols, "Try this next time");

            var sci = Box("RealScience", p, new Color(0.42f, 0.66f, 0.98f, 0.10f));
            HList(sci, 14, 14, TextAnchor.UpperLeft).childForceExpandHeight = true;
            var sciText = Rect("Text", sci.transform);
            VList(sciText, 4, 0);
            LE(sciText, flexW: 1);
            Overline(sciText, "Real science", UIKit.Shield);
            dSciTitle = Label(sciText, "", 19, TextAnchor.UpperLeft, Ink, FontStyle.Bold);
            dSciText = Label(sciText, "", 15);
            var ph = Rect("Photo", sci.transform);
            VList(ph, 0, 0);
            LE(ph, prefW: 260, flexW: 0).minWidth = 240;
            dSciPhoto = new[] { ph };

            dLesson = Label(p, "", 16, TextAnchor.UpperLeft, new Color(0.56f, 0.84f, 1f));
            dSources = Label(p, "", 13, TextAnchor.UpperLeft, Muted);
            dRewards = Label(p, "", 15, TextAnchor.UpperLeft, Gold, FontStyle.Bold);

            var row = Rect("Buttons", p);
            HList(row, 10, 0);
            LE(row, prefH: 54);
            dRetryButton = Btn(row, "Try again", 19, Accent, 0, () => { ClearForNewMission(); runner.Restart(); });
            LE(dRetryButton, flexW: 1);
            dRetryImage = dRetryButton.GetComponent<Image>();
            dMapButton = Btn(row, "Expedition map", 19, Quiet, 0, OnDebriefNext);
            LE(dMapButton, flexW: 1.4f);
            dMapLabel = dMapButton.GetComponentInChildren<Text>();
            dMapImage = dMapButton.GetComponent<Image>();
            dTitleButton = Btn(row, "Title screen", 19, Quiet, 0, () => GameFlow.Current?.ShowTitle());
            LE(dTitleButton, flexW: 1);
        }

        Text DebriefPanel(Transform parent, string title)
        {
            var box = Box(title, parent, CardBg);
            VList(box, 8, 16);
            LE(box, flexW: 1);
            Overline(box.transform, title);
            return Label(box.transform, "", 15);
        }

        void ShowDebrief(MissionResult r)
        {
            foreach (var o in new[] { cardOverlay, pauseOverlay, report, infoOverlay }) o.SetActive(false);
            pendingCard = null;
            hudGroup.alpha = 0;
            habGroup.alpha = 0;
            habQueue.Clear();
            habTimer = 0;
            lastSuccess = r.success;

            var m = runner.Mission;
            var b = r.breakdown;
            bool freeplay = m.mode == "freeplay";
            dTitle.text = r.success ? "You did it!" : "Safe stop";
            dReason.text = Gloss(r.reason) + (r.success ? "" : "  Nobody got hurt. Let's learn from it!");
            SetStars(dStars, r.stars);

            int[] got = { b.safety, b.priority, b.procedure, b.shielding, b.science };
            int[] max = { 40, 25, 15, 10, 10 };
            string[] names = { "Safety", "Smart choices", "Procedures", "Safe trips", "Science" };
            for (int i = 0; i < 5; i++)
            {
                dScoreLabels[i].text = $"{names[i]}  <b><color=#F5F2EE>{got[i]}/{max[i]}</color></b>";
                dScoreFills[i].anchorMax = new Vector2(got[i] / (float)max[i], 1);
            }

            var log = r.finalState.choiceLog;
            dChoices.text = log.Count == 0 ? "No big choices this time." : string.Join("\n", log.Skip(Mathf.Max(0, log.Count - 5)).Select(Gloss));
            var d = m.debrief;
            dRealTeam.text = Gloss(d?.realTeam ?? "");
            dTryNext.text = Gloss(d?.tryNext ?? "");
            dSciTitle.transform.parent.parent.gameObject.SetActive(d?.realScience != null && !string.IsNullOrEmpty(d.realScience.title));
            dSciTitle.text = d?.realScience?.title ?? "";
            dSciText.text = Gloss(d?.realScience?.text ?? "");
            Clear(dSciPhoto[0]);
            var photo = NasaPhoto(dSciPhoto[0], m.id == "ch5_solarstorm" ? "ch5_aurora" : m.id, 100, false);
            dSciPhoto[0].gameObject.SetActive(photo != null);

            int real = 0, sim = 0;
            if (m.sols != null) foreach (var s in m.sols) { if (s.source != null && s.source.kind != "simulated") real++; else sim++; }
            var srcNames = d?.sources != null ? d.sources.Select(k => ContentDb.Source(k)?.name ?? k).ToArray() : new string[0];
            dSources.text = $"<b>Real NASA data in this mission:</b> {real} day{(real == 1 ? "" : "s")} of real measurements, {sim} practice day{(sim == 1 ? "" : "s")}." +
                            (srcNames.Length > 0 ? "  Sources: " + string.Join(" · ", srcNames) : "");

            string rewards = "";
            if (r.finalState.research > 0) rewards += $"<color=#FFD36B>{r.finalState.research} research points</color>   ";
            if (r.creditsEarned > 0) rewards += $"+{r.creditsEarned} credits   ";
            if (r.newUnlocks != null && r.newUnlocks.Count > 0) rewards += "New for your suit: " + string.Join(", ", r.newUnlocks);
            dRewards.text = rewards;
            dRewards.gameObject.SetActive(rewards.Length > 0);

            var info = MissionLoader.Info(m.id);
            bool story = m.mode == "story" && info != null;
            dLesson.gameObject.SetActive(story);
            if (story)
                dLesson.text = r.success
                    ? $"<b>{(r.firstCompletion ? "New lesson in your Crew Handbook" : "Crew Handbook lesson")}:</b> {info.lesson}."
                    : $"<b>Lesson still to learn:</b> {info.lesson}. Try again!";

            bool hasFlow = GameFlow.Current != null;
            dMapButton.gameObject.SetActive(hasFlow);
            dTitleButton.gameObject.SetActive(hasFlow);
            var next = story && r.success ? MissionLoader.NextStage() : null;
            bool finale = story && r.success && next == null;
            dMapLabel.text = freeplay ? "<b>New mission</b>" : finale ? "<b>See the crew land!</b>" : next != null ? $"<b>Next: {next.title}</b>" : "Expedition map";
            bool forward = freeplay || finale || next != null;
            dMapImage.color = forward ? Accent : Quiet;
            dRetryImage.color = forward ? Quiet : Accent;
            debriefOverlay.SetActive(true);
        }

        void OnDebriefNext()
        {
            var flow = GameFlow.Current;
            if (flow == null) return;
            if (runner.Mission.mode == "freeplay") { flow.PlayFreeplay(); return; }
            var next = MissionLoader.NextStage();
            if (lastSuccess && next == null) flow.ShowFinale();
            else if (lastSuccess && next != null) flow.PlayStory(next.id);
            else flow.ShowChapterMap();
        }

        // ───────────────────────── rover report, glossary ─────────────────────────

        void BuildInfo(Transform root)
        {
            infoOverlay = Overlay(root, "Info", 640, out var p);
            infoOver = Overline(p, "");
            infoTitle = Label(p, "", 28, TextAnchor.UpperLeft, Ink, FontStyle.Bold);
            infoBody = Label(p, "", 18);
            var badge = Box("Badge", p, CardBg);
            VList(badge, 0, 12);
            infoBadge = Label(badge.transform, "", 14, TextAnchor.UpperLeft, Muted);
            Btn(p, "Got it", 19, Accent, 50, () => infoOverlay.SetActive(false));
        }

        /// <summary>Click the rover: it reports today's readings (the same NASA data as the Mars Report).</summary>
        public void ShowRoverReport(bool stuck)
        {
            if (!MissionRunning || AnyOverlay) return;
            var e = DayPlanner.Today(runner);
            infoOver.text = Spaced("Rover report");
            infoTitle.text = stuck ? "I'm stuck in soft sand!" : "Ready to explore!";
            infoBody.text = Gloss($"Dust in the sky ([[tau]]): <b>{e.tau:0.0}</b>\nSunlight: <b>{e.sunlight * 100:0}%</b> of a sunny day");
            bool real = e.source != null && e.source.kind != "simulated";
            infoBadge.text = real ? $"<b><color=#6CFF8F>Real NASA data</color></b>  {e.source.label} ({e.source.date})" : "<b>Practice data</b>: made up, based on real Mars science.";
            infoOverlay.SetActive(true);
        }

        /// <summary>The weather mast's readings. Numbers come from today's sky data (simulated unless the day is labelled Real).</summary>
        public void ShowWeatherReport()
        {
            if (!MissionRunning || AnyOverlay) return;
            var e = DayPlanner.Today(runner);
            int high = Mathf.RoundToInt(-5 - 25 * e.cold - 3 * e.tau);
            int low = Mathf.RoundToInt(-75 - 30 * e.cold);
            int wind = Mathf.RoundToInt(4 + e.tau * 4);
            infoOver.text = Spaced("Weather station");
            infoTitle.text = e.tau >= 4 ? "Dust storm!" : e.cold >= 0.6f ? "Freezing!" : "Today's weather";
            infoBody.text = Gloss($"Warmest today: <b>{high} °C</b>\nColdest tonight: <b>{low} °C</b>\nWind: <b>{wind} m/s</b>\nAir pressure: <b>about 6 millibars</b> (Earth: about 1,000)\nDust in the sky ([[tau]]): <b>{e.tau:0.0}</b>\n" +
                "<color=#8FD6FF>Real rovers have weather stations too. Perseverance's is called MEDA.</color>");
            infoBadge.text = "<b>Practice data</b>: temperatures and wind are made up from today's sky, based on real Mars weather.";
            infoOverlay.SetActive(true);
        }

        void BuildGlossary(Transform root)
        {
            glossaryOverlay = Overlay(root, "GlossaryOverlay", 560, out var p);
            Overline(p, "Science word");
            glossWord = Label(p, "", 32, TextAnchor.UpperLeft, Ink, FontStyle.Bold);
            glossMeaning = Label(p, "", 21);
            Btn(p, "Got it!", 19, Accent, 50, () => glossaryOverlay.SetActive(false));
        }

        void ShowGlossary(string key)
        {
            var e = ContentDb.Glossary(key);
            glossWord.text = e?.word ?? key;
            glossMeaning.text = e?.meaning ?? "(No meaning written yet.)";
            glossaryOverlay.SetActive(true);
        }

        // ───────────────────────── HAB speech ─────────────────────────

        void OnHabSays(string line)
        {
            foreach (Match match in GlossaryTag.Matches(line))
                if (!seenWords.Contains(match.Groups[1].Value)) seenWords.Add(match.Groups[1].Value);
            habQueue.Enqueue(line);
            if (habTimer <= 0) NextSubtitle();
        }

        void NextSubtitle()
        {
            if (habQueue.Count == 0) return;
            var line = habQueue.Dequeue();
            bool fromEarth = line.StartsWith("MC: ");
            if (fromEarth)
            {
                line = line.Substring(4);
                habHeader.text = Spaced($"Message from Earth  ·  sent {UnityEngine.Random.Range(4, 25)} minutes ago");
                habHeader.color = Gold;
                habFace.sprite = Icons.Get("satellite");
                habFace.color = Gold;
            }
            else
            {
                if (line.StartsWith("HAB: ")) line = line.Substring(5);
                habHeader.text = Spaced("HAB");
                habHeader.color = new Color(0.56f, 0.84f, 1f);
                habFace.sprite = Icons.Get("hab");
                habFace.color = new Color(0.56f, 0.84f, 1f);
            }
            habText.text = Gloss(line);
            habTimer = Mathf.Clamp(2.5f + line.Length * 0.05f, 4f, 10f);
            if (habQueue.Count > 0) habTimer = Mathf.Min(habTimer, 3.5f);
            habIdle = 0;
            habGroup.alpha = 1;
        }

        void TickSubtitles()
        {
            float dt = Time.unscaledDeltaTime;
            if (habTimer > 0)
            {
                habTimer -= dt;
                if (habTimer <= 0 && habQueue.Count > 0) NextSubtitle();
            }
            else
            {
                habIdle += dt;
                habGroup.alpha = Mathf.Clamp01(1 - (habIdle - 8f));
            }
        }

        // ───────────────────────── waypoint arrow ─────────────────────────

        static readonly Dictionary<string, string> TargetNames = new Dictionary<string, string>
        {
            { "shelter", "Radiation shelter" }, { "airlock", "Way inside" }, { "bunk", "Bed" }, { "solar", "Solar panels" }, { "samples", "Rock sample" },
            { "rover", "Perseverance" }, { "ice", "Ice deposit" }, { "weather", "Weather station" },
            { "suit", "Suit stand" }, { "airlockOut", "Airlock" },
        };
        SuitLocker suitStand;
        RoverActor rover;
        IceSite iceSite;
        WeatherStation weather;

        bool TryTarget(string t, out Vector3 pos, out string name)
        {
            pos = default;
            name = null;
            if (string.IsNullOrEmpty(t)) return false;
            if (t.StartsWith("panel:"))
            {
                var id = t.Substring(6);
                if (!panelsById.TryGetValue(id, out var p) || !p) return false;
                pos = p.transform.position + Vector3.up * 1.6f;
                name = KidName(id);
                return true;
            }
            TargetNames.TryGetValue(t, out name);
            var from = Camera.main ? Camera.main.transform.position : Vector3.zero;
            switch (t)
            {
                case "shelter": if (!playerLocation) return false; pos = playerLocation.shelter.center + Vector3.up; return true;
                case "airlock": if (!playerLocation) return false; pos = playerLocation.airlock.center + new Vector3(0, 0, playerLocation.airlock.extents.z); return true;
                case "bunk": if (!bunk) return false; pos = bunk.transform.position + Vector3.up; return true;
                case "solar": return Nearest(solarStations, from, out pos);
                case "rover":
                    if (!rover) rover = FindAnyObjectByType<RoverActor>();
                    if (!rover) return false;
                    pos = rover.transform.position + Vector3.up * 1.5f;
                    return true;
                case "suit":
                    if (!suitStand) suitStand = FindAnyObjectByType<SuitLocker>();
                    if (!suitStand) return false;
                    pos = suitStand.transform.position + Vector3.up * 1.4f;
                    return true;
                case "airlockOut":
                    if (!playerLocation) return false;
                    pos = playerLocation.airlock.center + new Vector3(0, 0, playerLocation.airlock.extents.z + 1.5f);
                    return true;
                case "weather":
                    if (!weather) weather = FindAnyObjectByType<WeatherStation>();
                    if (!weather) return false;
                    pos = weather.transform.position + Vector3.up * 1.2f;
                    return true;
                case "ice":
                    if (!iceSite) iceSite = FindAnyObjectByType<IceSite>();
                    if (!iceSite || !iceSite.Active) return false;
                    pos = iceSite.transform.position + Vector3.up;
                    return true;
                case "samples":
                    if (sampleSites == null) return false;
                    float best = float.MaxValue;
                    foreach (var s in sampleSites)
                        if (s && s.Available && (s.transform.position - from).sqrMagnitude < best)
                        {
                            best = (s.transform.position - from).sqrMagnitude;
                            pos = s.transform.position + Vector3.up;
                        }
                    return best < float.MaxValue;
            }
            return false;
        }

        static bool Nearest(Component[] items, Vector3 from, out Vector3 pos)
        {
            pos = default;
            if (items == null) return false;
            float best = float.MaxValue;
            foreach (var c in items)
                if (c && (c.transform.position - from).sqrMagnitude < best)
                {
                    best = (c.transform.position - from).sqrMagnitude;
                    pos = c.transform.position + Vector3.up * 1.2f;
                }
            return best < float.MaxValue;
        }

        void UpdateMarker()
        {
            var cam = Camera.main;
            if (solarStations == null || solarStations.Length == 0) solarStations = FindObjectsByType<SolarPanelStation>();
            if (!cam || hudGroup.alpha < 0.5f || AnyOverlay || !MissionRunning || !TryTarget(markerTarget, out var world, out var name))
            {
                marker.gameObject.SetActive(false);
                return;
            }
            float dist = Vector3.Distance(cam.transform.position, world);
            if (dist < 2.2f) { marker.gameObject.SetActive(false); return; }
            marker.gameObject.SetActive(true);
            markerLabel.text = $"{name}  <color=#FFFFFFAA>{dist:0} m</color>";

            var sp = cam.WorldToScreenPoint(world);
            bool behind = sp.z < 0;
            if (behind) sp = -sp;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(hudRoot, sp, null, out var local);
            var half = hudRoot.rect.size * 0.5f - new Vector2(80, 100);
            bool onScreen = !behind && Mathf.Abs(local.x) < half.x && Mathf.Abs(local.y) < half.y;
            if (onScreen)
            {
                marker.anchoredPosition = local;
                markerArrow.gameObject.SetActive(false);
            }
            else
            {
                var dir = local.sqrMagnitude < 1f ? Vector2.down : local.normalized;
                if (behind && Mathf.Abs(dir.y) > Mathf.Abs(dir.x)) dir.y = -Mathf.Abs(dir.y);
                float scale = Mathf.Min(half.x / Mathf.Max(0.001f, Mathf.Abs(dir.x)), half.y / Mathf.Max(0.001f, Mathf.Abs(dir.y)));
                marker.anchoredPosition = dir * scale;
                markerArrow.gameObject.SetActive(true);
                markerArrow.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90);
            }
        }

        void BuildMarker(Transform hud)
        {
            marker = Rect("Marker", hud);
            marker.anchorMin = marker.anchorMax = new Vector2(0.5f, 0.5f);
            marker.sizeDelta = new Vector2(10, 10);
            var ring = Box("Ring", marker, new Color(1, 0.85f, 0.4f, 0.3f), 3);
            Center(ring.rectTransform, Vector2.zero, new Vector2(34, 34));
            ring.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            var dot = Box("Diamond", marker, Warn, 6);
            Center(dot.rectTransform, Vector2.zero, new Vector2(16, 16));
            dot.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            var arrow = Rect("Arrow", marker).gameObject.AddComponent<Image>();
            arrow.sprite = Icons.Get("up");
            arrow.color = Warn;
            arrow.raycastTarget = false;
            Center(arrow.rectTransform, Vector2.zero, new Vector2(40, 40));
            markerArrow = arrow.rectTransform;
            markerLabel = Label(marker, "", 16, TextAnchor.UpperCenter, Ink, FontStyle.Bold);
            markerLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            markerLabel.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1.5f, -1.5f);
            Center(markerLabel.rectTransform, new Vector2(0, -34), new Vector2(260, 24));
            marker.gameObject.SetActive(false);
        }
    }
}