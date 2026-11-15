using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using static MissionTrainer.UIKit;

namespace MissionTrainer
{
    /// <summary>
    /// All menu screens, built in code over the live 3D scene (a slow camera orbit of the base):
    /// title, invitation slides, expedition map, Crew Handbook, Freeplay hub + upgrade shop, finale,
    /// suit customization and the Science page. GameFlow decides which screen is visible.
    /// </summary>
    public class MenuUI : MonoBehaviour
    {
        public GameFlow flow;

        Canvas canvas;
        GameObject title, map, science, intro, suit, handbook, freeplay, finale;

        // Title
        Text continueHead, continueSub, mapSub, handbookSub, freeplaySub, suitSub, difficultyInfo, rocksLinkSub, nasaFoot;
        readonly Dictionary<string, Image> difficultyChips = new Dictionary<string, Image>();

        // Map
        Transform chapterRow;
        Text mapProgress;
        RectTransform mapProgressFill;

        // Handbook / freeplay / finale (rebuilt when shown)
        RectTransform handbookList, freeplayBody, finaleBody;

        // Suit screen
        Text suitCount, suitInfo;
        readonly Dictionary<string, Transform> suitGrids = new Dictionary<string, Transform>();

        // Intro slides
        Text introStep, introTitle, introBody, introNextLabel;
        RectTransform introVisual;
        Button introBack;
        int slide;
        bool introThenPlay;

        static readonly string[,] Slides =
        {
            { "You're invited", "You're going to Mars!",
              "Six astronauts will fly to Mars soon. First, someone must show that people can live there. That's you! HAB, the base computer, will help." },
            { "Every day", "Read. Do. Sleep.",
              "Each morning, a Mars Report shows today's sky. It uses real NASA data. Then do your missions, and press End the day." },
            { "Your mission", "Eight real Mars problems",
              "Real rovers faced dust storms, cold nights, soft sand and broken wheels. Get through each one and learn its lesson. Then the crew can land!" },
        };

        static readonly Dictionary<string, string> DifficultyBlurb = new Dictionary<string, string>
        {
            { "cadet", "Easy: a job list, arrows that show the way, and extra power. Best to start!" },
            { "pilot", "Medium: a job list, but no arrows. Less extra power." },
            { "commander", "Hard: no job list. Read the Mars Report and decide for yourself." },
        };

        void Awake()
        {
            EnsureEventSystem();
            var root = UIKit.Canvas(transform, "Menu", 20);
            canvas = root.GetComponent<Canvas>();
            BuildTitle(root);
            BuildMap(root);
            BuildHandbook(root);
            BuildFreeplay(root);
            BuildFinale(root);
            BuildScience(root);
            BuildIntro(root);
            BuildSuit(root);
            BuildRocks(root);
            HideAll();
        }

        GameObject[] Screens => new[] { title, map, science, intro, suit, handbook, freeplay, finale, rocks };

        public void HideAll()
        {
            foreach (var s in Screens) s.SetActive(false);
            canvas.enabled = false;
        }

        void Show(GameObject screen)
        {
            canvas.enabled = true;
            foreach (var s in Screens) s.SetActive(s == screen);
            if (screen != suit && AchievementManager.Preview.Count > 0) { AchievementManager.Preview.Clear(); flow.RefreshSuitDisplay(); }
        }

        public void ShowTitle() { Show(title); RefreshTitle(); }
        public void ShowMap() { Show(map); RefreshMap(); }
        public void ShowScience() { Show(science); }
        public void ShowHandbook() { Show(handbook); RefreshHandbook(); }
        public void ShowFreeplayHub() { Show(freeplay); RefreshFreeplay(); }
        public void ShowFinale() { Show(finale); RefreshFinale(); }
        public void ShowRocks() { Show(rocks); RefreshRocks(); }

        public void ShowSuit()
        {
            Show(suit);
            suitInfo.text = "Pick a color for your whole suit, a visor and a mission patch. Tap a locked item to try it on!";
            RefreshSuit();
        }

        /// <summary>Invitation slides. thenPlay: continue straight into the first stage (first Continue press).</summary>
        public void ShowIntro(bool thenPlay)
        {
            introThenPlay = thenPlay;
            slide = 0;
            Show(intro);
            RenderSlide();
        }

        // ───────────────────────── shared bits ─────────────────────────

        /// <summary>Full-screen dim + a back button + a big heading. Returns the area below the heading.</summary>
        RectTransform Page(Transform root, string name, string heading, string sub, out GameObject screen, float dimAlpha = 0.72f)
        {
            screen = Rect(name, root).gameObject;
            Fill((RectTransform)screen.transform);
            screen.AddComponent<Image>().color = new Color(0.02f, 0.02f, 0.03f, dimAlpha);

            var head = Rect("Head", screen.transform);
            Anchor(head, new Vector2(0, 1), new Vector2(1, 1), new Vector2(64, -150), new Vector2(-64, -40));
            var v = VList(head, 6, 0);
            v.childForceExpandWidth = false;
            LE(Btn(head, "Back to title", 15, Quiet, 34, () => flow.ShowTitle()), prefW: 150);
            Label(head, heading, 38, TextAnchor.UpperLeft, Ink, FontStyle.Bold);
            var s = Label(head, sub, 16, TextAnchor.UpperLeft, Muted);
            LE(s, prefW: 1100);

            var body = Rect("Body", screen.transform);
            Anchor(body, Vector2.zero, Vector2.one, new Vector2(64, 40), new Vector2(-64, -165));
            return body;
        }

        /// <summary>Big menu button: title on top, small line underneath.</summary>
        Button MenuButton(Transform parent, string head, string sub, Color c, UnityEngine.Events.UnityAction onClick, out Text headText, out Text subText, float height = 52)
        {
            var b = Btn(parent, "", 20, c, height, onClick);
            Clear(b.transform);   // replace the default single label with title + subtitle
            headText = Label(b.transform, head, 19, TextAnchor.UpperLeft, Ink, FontStyle.Bold);
            Anchor(headText.rectTransform, Vector2.zero, Vector2.one, new Vector2(20, 6), new Vector2(-20, -6));
            subText = Label(b.transform, sub, 13, TextAnchor.LowerLeft, Muted);
            Anchor(subText.rectTransform, Vector2.zero, Vector2.one, new Vector2(20, 6), new Vector2(-20, -6));
            return b;
        }

        static void Spacer(Transform parent, float h) => LE(Rect("Space", parent), prefH: h);

        // ───────────────────────── title ─────────────────────────

        void BuildTitle(Transform root)
        {
            title = Rect("Title", root).gameObject;
            Fill((RectTransform)title.transform);

            // Soft dark fade on the left so text reads over the 3D scene.
            var scrim = Rect("Scrim", title.transform).gameObject.AddComponent<Image>();
            scrim.sprite = FadeRight();
            scrim.color = new Color(0.02f, 0.02f, 0.03f, 0.84f);
            scrim.raycastTarget = false;
            Anchor(scrim.rectTransform, Vector2.zero, new Vector2(0.62f, 1), Vector2.zero, Vector2.zero);

            var col = Rect("Column", title.transform);
            Anchor(col, new Vector2(0, 0), new Vector2(0, 1), new Vector2(70, 30), new Vector2(70 + 600, -40));
            var v = VList(col, 10, 0);

            Overline(col, "Red Frontier program");
            Label(col, "The Red Frontier", 60, TextAnchor.UpperLeft, Ink, FontStyle.Bold);
            Label(col, "Live on Mars like a real astronaut. Real NASA data decides what happens each day!", 20, TextAnchor.UpperLeft, new Color(1, 1, 1, 0.85f));
            Spacer(col, 6);

            // Three big picture buttons: the only choices a new player needs.
            var tilesRow = Rect("Tiles", col);
            HList(tilesRow, 12, 0);
            LE(tilesRow, prefH: 170);
            BigTile(tilesRow, "play", "Story Mode", Accent, () => flow.ContinueExpedition(), out continueHead, out continueSub, 1.4f);
            BigTile(tilesRow, "globe", "Free play", Quiet, () => flow.ShowFreeplayHub(), out _, out freeplaySub, 1f);
            BigTile(tilesRow, "suit", "My suit", Quiet, () => flow.ShowSuit(false), out _, out suitSub, 1f);

            // Small picture links
            var links = Rect("Links", col);
            HList(links, 8, 0);
            LE(links, prefH: 44);
            SmallLink(links, "map", "Map", () => flow.ShowChapterMap(), out mapSub);
            SmallLink(links, "book", "Crew Handbook", () => flow.ShowHandbook(), out handbookSub);
            SmallLink(links, "satellite", "NASA science", () => flow.ShowScience(), out _);
            SmallLink(links, "rock", "Rocks", () => flow.ShowRocks(), out rocksLinkSub);
            SmallLink(links, "check", "How to play", () => flow.ShowIntro(), out _);

            Spacer(col, 4);
            var seg = Rect("Level", col);
            HList(seg, 8, 0);
            LE(seg, prefH: 42);
            var dl = Label(seg, "Level:", 17, TextAnchor.MiddleLeft, Ink, FontStyle.Bold);
            LE(dl, prefW: 70);
            foreach (var lvl in GameManager.Instance.Config.levels)
            {
                var id = lvl.id;
                var b = Btn(seg, LevelName(id), 17, Quiet, 0, () => { GameManager.Instance.DifficultyId = id; RefreshTitle(); });
                LE(b, flexW: 1);
                difficultyChips[id] = b.GetComponent<Image>();
            }
            difficultyInfo = Label(col, "", 14, TextAnchor.UpperLeft, Muted);

            var foot = Label(title.transform, "A Mars mission built on real NASA science. Real numbers are marked \"Real NASA data\".", 13, TextAnchor.LowerLeft, Muted);
            nasaFoot = foot;
            NasaLive.Changed += RefreshNasaFoot;
            Anchor(foot.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(70, 8), new Vector2(-24, 44));
        }

        void RefreshNasaFoot()
        {
            if (!nasaFoot) return;
            nasaFoot.text = "A Mars mission built on real NASA science.   <color=#8FD6FF>" + NasaLive.BadgeLine() + "</color>\n<color=#FFFFFF88>" + NasaLive.MarsDateLine() + "</color>";
        }

        public static string LevelName(string id) => id == "cadet" ? "Easy" : id == "pilot" ? "Medium" : id == "commander" ? "Hard" : id;

        void BigTile(Transform parent, string icon, string head, Color c, UnityEngine.Events.UnityAction onClick, out Text headText, out Text subText, float flex)
        {
            var b = Btn(parent, "", 20, c, 0, onClick);
            LE(b, flexW: flex);
            Clear(b.transform);
            var vv = Rect("V", b.transform);
            Fill(vv);
            VList(vv, 4, 14, TextAnchor.MiddleCenter);
            var img = Rect("Icon", vv).gameObject.AddComponent<Image>();
            img.sprite = Icons.Get(icon);
            img.preserveAspect = true;
            img.raycastTarget = false;
            LE(img, 56, 56);
            headText = Label(vv, head, 24, TextAnchor.MiddleCenter, Ink, FontStyle.Bold);
            subText = Label(vv, "", 13, TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.8f));
        }

        void SmallLink(Transform parent, string icon, string label, UnityEngine.Events.UnityAction onClick, out Text sub)
        {
            var b = Btn(parent, "", 14, new Color(1, 1, 1, 0.08f), 0, onClick);
            LE(b, flexW: 1);
            Clear(b.transform);
            var h = Rect("H", b.transform);
            Fill(h);
            HList(h, 6, 8, TextAnchor.MiddleCenter).childForceExpandHeight = false;
            var img = Rect("Icon", h).gameObject.AddComponent<Image>();
            img.sprite = Icons.Get(icon);
            img.preserveAspect = true;
            img.raycastTarget = false;
            LE(img, 22, 22);
            var col = Rect("C", h);
            VList(col, 0, 0, TextAnchor.MiddleLeft);
            var head = Label(col, label, 14, TextAnchor.MiddleLeft, Ink, FontStyle.Bold);
            head.horizontalOverflow = HorizontalWrapMode.Overflow;
            sub = Label(col, "", 11, TextAnchor.MiddleLeft, Muted);
            sub.horizontalOverflow = HorizontalWrapMode.Overflow;
            // Never narrower than its own label (so the text stays inside the box)
            LE(b).minWidth = 22 + 6 + head.preferredWidth + 22;
        }

        void RefreshTitle()
        {
            string id = GameManager.Instance.DifficultyId;
            foreach (var kv in difficultyChips) kv.Value.color = kv.Key == id ? Accent : Quiet;
            difficultyInfo.text = DifficultyBlurb.TryGetValue(id, out var s) ? s : "";

            int done = MissionLoader.CompletedStages();
            var next = MissionLoader.NextStage();
            continueHead.text = "Story Mode";
            continueSub.text = !SaveSystem.SeenIntro ? "Start here!" : next == null ? "Watch the crew land" : $"Next: {next.title}";

            mapSub.text = $"{done} of {MissionLoader.Chapters.Length} done";
            handbookSub.text = $"{done} of {MissionLoader.Chapters.Length} lessons";
            freeplaySub.text = $"Random missions  ·  {SaveSystem.Credits} credits";
            suitSub.text = $"{AchievementManager.UnlockedCount} of {AchievementManager.Catalog.Length} unlocked";
            rocksLinkSub.text = $"{RockCollection.UniqueFound} of {RockCollection.All.Length}";
            RefreshNasaFoot();
        }

        // ───────────────────────── intro slides ─────────────────────────

        void BuildIntro(Transform root)
        {
            intro = Rect("Intro", root).gameObject;
            Fill((RectTransform)intro.transform);
            intro.AddComponent<Image>().color = new Color(0.02f, 0.02f, 0.03f, 0.6f);

            var p = Box("Panel", intro.transform, GlassDark, 1.2f).rectTransform;
            p.anchorMin = p.anchorMax = p.pivot = new Vector2(0.5f, 0.5f);
            p.sizeDelta = new Vector2(800, 480);   // fixed height so the buttons don't jump between slides
            VList(p, 14, 36);

            introStep = Overline(p, "");
            introTitle = Label(p, "", 38, TextAnchor.UpperLeft, Ink, FontStyle.Bold);
            introBody = Label(p, "", 19, TextAnchor.UpperLeft, new Color(1, 1, 1, 0.85f));
            introVisual = Rect("Visual", p);
            HList(introVisual, 10, 0, TextAnchor.UpperLeft);
            LE(Rect("Space", p), flexH: 1);

            var row = Rect("Buttons", p);
            HList(row, 10, 0);
            LE(row, prefH: 50);
            introBack = Btn(row, "Back", 17, Quiet, 0, () => { if (slide > 0) { slide--; RenderSlide(); } });
            LE(introBack, prefW: 140);
            LE(Rect("Space", row), flexW: 1);
            var next = Btn(row, "Next", 18, Accent, 0, NextSlide);
            LE(next, prefW: 280);
            introNextLabel = next.GetComponentInChildren<Text>();
        }

        void RenderSlide()
        {
            int n = Slides.GetLength(0);
            introStep.text = Spaced($"{Slides[slide, 0]}   ·   {slide + 1} of {n}");
            introTitle.text = Slides[slide, 1];
            introBody.text = Slides[slide, 2];
            introBack.interactable = slide > 0;
            introNextLabel.text = slide < n - 1 ? "<b>Next</b>" : introThenPlay ? "<b>Start the expedition</b>" : "<b>Got it</b>";

            Clear(introVisual);
            introVisual.gameObject.SetActive(slide > 0);
            if (slide == 1)
            {
                Tile(introVisual, "1. Mars Report", "Real NASA data shows today's sky.", Good);
                Tile(introVisual, "2. Do your missions", "Outside, air drops every second!", Warn);
                Tile(introVisual, "3. End the day", "Sleep until tomorrow.", Accent);
            }
            else if (slide == 2)
            {
                Tile(introVisual, "Real events", "Each one really happened to a rover.", Accent);
                Tile(introVisual, "Keep 3 things safe", "Battery, air and food.", Warn);
                Tile(introVisual, "Nobody gets hurt", "If it goes wrong, just try again!", Good);
            }
        }

        static void Tile(Transform parent, string head, string text, Color c)
        {
            var b = Box(head, parent, new Color(c.r, c.g, c.b, 0.14f));
            VList(b, 4, 14);
            LE(b, flexW: 1);
            Label(b.transform, $"<b>{head}</b>", 17, TextAnchor.UpperLeft, c);
            Label(b.transform, text, 14, TextAnchor.UpperLeft, Muted);
        }

        void NextSlide()
        {
            if (slide < Slides.GetLength(0) - 1) { slide++; RenderSlide(); return; }
            SaveSystem.SeenIntro = true;
            if (introThenPlay) flow.ContinueExpedition();
            else flow.ShowTitle();
        }

        // ───────────────────────── expedition map ─────────────────────────

        void BuildMap(Transform root)
        {
            var body = Page(root, "Map", "The expedition", "Eight stages, each based on something that really happened on Mars. Every stage you finish adds a lesson to the Crew Handbook.", out map, 0.55f);

            var prog = Rect("Progress", body);
            Anchor(prog, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -40), Vector2.zero);
            VList(prog, 6, 0);
            mapProgress = Label(prog, "", 15, TextAnchor.UpperLeft, Ink, FontStyle.Bold);
            mapProgressFill = Bar(prog, 6, new Color(1, 1, 1, 0.12f), Accent, out _);

            // Two rows of six cards (10 stages + the finale)
            var row = Rect("Stages", body);
            Anchor(row, new Vector2(0, 0), new Vector2(1, 1), new Vector2(-8, 0), new Vector2(8, -56));
            var grid = row.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(150, 220);
            grid.spacing = new Vector2(8, 8);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 6;
            grid.childAlignment = TextAnchor.UpperCenter;
            chapterRow = row;
        }

        void RefreshMap()
        {
            Clear(chapterRow);
            int done = MissionLoader.CompletedStages();
            int total = MissionLoader.Chapters.Length;
            var next = MissionLoader.NextStage();
            mapProgress.text = next == null
                ? $"All {total} stages done. The crew has landed!"
                : $"{done} of {total} stages done  ·  the crew lands after the last stage";
            mapProgressFill.anchorMax = new Vector2(done / (float)total, 1);

            foreach (var ch in MissionLoader.Chapters)
            {
                bool exists = MissionLoader.Exists(ch.id);
                bool isDone = SaveSystem.IsDone(ch.id);
                bool isNext = next != null && ch.id == next.id;
                var c = ch;
                StageCard(isNext ? "Next  ·  " + ch.stage : ch.stage, ch.title, ch.blurb, ch.realEvent,
                    isDone ? $"<color=#8FD6FF><b>Lesson learned:</b></color> {ch.lesson}" : "<color=#FFFFFF66>Lesson: finish this stage to learn it</color>",
                    SaveSystem.GetStars(ch.id), isNext, isDone,
                    exists ? (isDone ? "Play again" : "Play") : "Coming soon", exists, () => flow.PlayStory(c.id));
            }

            bool landed = next == null;
            StageCard("Finale", "The crew lands", "Bring the first crew of six to Mars, with your Crew Handbook.", "Your expedition's goal",
                landed ? "<color=#8FD6FF>Welcome to Mars, crew!</color>" : "<color=#FFFFFF66>Finish every stage first</color>",
                -1, false, landed, landed ? "Watch" : "Locked", landed, () => flow.ShowFinale());
        }

        void StageCard(string label, string head, string blurb, string realEvent, string lesson, int stars, bool next, bool done,
            string action, bool canPlay, UnityEngine.Events.UnityAction onPlay)
        {
            var card = Box(head, chapterRow, next ? new Color(0.93f, 0.52f, 0.24f, 0.24f) : GlassDark);
            var v = VList(card, 4, 10);
            v.childForceExpandHeight = false;
            if (next) card.gameObject.AddComponent<Outline>().effectColor = new Color(0.93f, 0.52f, 0.24f, 0.85f);

            Overline(card.transform, label, next ? Accent : done ? new Color(0.56f, 0.84f, 1f) : Muted);
            Label(card.transform, head, 17, TextAnchor.UpperLeft, Ink, FontStyle.Bold);
            var b = Label(card.transform, blurb, 12, TextAnchor.UpperLeft, Muted);
            LE(b, flexH: 1);
            Label(card.transform, realEvent, 11, TextAnchor.UpperLeft, new Color(0.56f, 0.84f, 1f, 0.9f));
            if (done) Label(card.transform, lesson, 11, TextAnchor.UpperLeft, Ink);
            if (stars >= 0)
            {
                var starRow = Rect("Stars", card.transform);
                HList(starRow, 4, 0).childForceExpandHeight = false;
                LE(starRow, prefH: 22);
                SetStars(new[] { Star(starRow, 20), Star(starRow, 20), Star(starRow, 20) }, stars);
            }
            var play = Btn(card.transform, action, 15, canPlay ? (next || (!done && stars < 0) ? Accent : Quiet) : new Color(1, 1, 1, 0.04f), 38, onPlay);
            play.interactable = canPlay;
        }

        // ───────────────────────── crew handbook ─────────────────────────

        void BuildHandbook(Transform root)
        {
            var body = Page(root, "Handbook", "Crew Handbook", "The lessons you collect for the first crew. Each one was learned the hard way by a real rover on Mars.", out handbook);
            var scroll = Scroll(body, out var content, 10);
            Fill((RectTransform)scroll.transform);
            handbookList = content;
        }

        void RefreshHandbook()
        {
            Clear(handbookList);
            int n = 1;
            foreach (var ch in MissionLoader.Chapters)
            {
                bool done = SaveSystem.IsDone(ch.id);
                var card = Box(ch.id, handbookList, done ? new Color(0.42f, 0.66f, 0.98f, 0.12f) : new Color(1, 1, 1, 0.04f));
                VList(card, 4, 16);
                Overline(card.transform, $"Lesson {n++}  ·  {ch.stage}: {ch.title}", done ? new Color(0.56f, 0.84f, 1f) : Muted);
                if (done)
                {
                    Label(card.transform, ch.lesson, 22, TextAnchor.UpperLeft, Ink, FontStyle.Bold);
                    Label(card.transform, ch.lessonDetail, 16);
                    if (ch.id != "ch0_training") Label(card.transform, "Learned from: " + ch.realEvent, 13, TextAnchor.UpperLeft, new Color(0.56f, 0.84f, 1f, 0.85f));
                    var photo = NasaPhoto(card.transform, ch.id, 150);
                    if (photo) LE(photo, prefW: 520);
                }
                else
                {
                    Label(card.transform, "Not learned yet", 20, TextAnchor.UpperLeft, new Color(1, 1, 1, 0.45f), FontStyle.Bold);
                    Label(card.transform, $"Finish {ch.stage} ({ch.title}) to add this lesson.", 15, TextAnchor.UpperLeft, Muted);
                }
            }
            Spacer(handbookList, 10);
        }

        // ───────────────────────── freeplay hub + shop ─────────────────────────

        void BuildFreeplay(Transform root)
        {
            var body = Page(root, "Freeplay", "Freeplay", "Random missions with random hazards. Bring rock samples home and keep healthy reserves to earn credits. Spend credits on upgrades that help every Freeplay mission.", out freeplay);
            freeplayBody = body;
        }

        void RefreshFreeplay()
        {
            Clear(freeplayBody);
            var col = Rect("Col", freeplayBody);
            Fill(col);
            VList(col, 12, 0);

            var stats = Rect("Stats", col);
            HList(stats, 10, 0);
            LE(stats, prefH: 74);
            Stat(stats, "Credits", SaveSystem.Credits.ToString(), Gold);
            Stat(stats, "Sols survived", SaveSystem.FreeplaySols.ToString(), Ink);
            Stat(stats, "Best mission", SaveSystem.HighScore + " credits", Ink);
            Stat(stats, "Research points", SaveSystem.TotalResearch.ToString(), Gold);
            Label(col, "Collect rocks (+10 research) and study them in the Science lab (+15). Your research points become credits.", 14, TextAnchor.UpperLeft, Muted);

            var go = Btn(col, "<b>Start a random mission</b>", 20, Accent, 56, () => flow.PlayFreeplay());

            Overline(col, "Upgrade shop  ·  bought once, used in every Freeplay mission", Muted);
            var grid = Rect("Shop", col);
            HList(grid, 10, 0, TextAnchor.UpperLeft);
            LE(grid, prefH: 150);
            foreach (var item in ContentDb.ShopItems)
            {
                if (item.stat == "shield") continue;   // radiation was removed from the game: this upgrade has no job
                var it = item;
                bool owned = SaveSystem.HasUpgrade(it.id);
                bool afford = SaveSystem.Credits >= it.cost;
                var card = Box(it.id, grid, owned ? new Color(0.36f, 0.80f, 0.52f, 0.14f) : CardBg);
                VList(card, 4, 14);
                LE(card, flexW: 1);
                Label(card.transform, it.label, 17, TextAnchor.UpperLeft, Ink, FontStyle.Bold);
                var d = Label(card.transform, it.description, 13, TextAnchor.UpperLeft, Muted);
                LE(d, flexH: 1);
                var buy = Btn(card.transform, owned ? "Owned" : $"Buy · {it.cost} credits", 14, owned ? Quiet : afford ? Accent : Quiet, 36, () =>
                {
                    if (SaveSystem.HasUpgrade(it.id) || SaveSystem.Credits < it.cost) return;
                    SaveSystem.Credits -= it.cost;
                    SaveSystem.AddUpgrade(it.id);
                    RefreshFreeplay();
                });
                buy.interactable = !owned && afford;
            }
        }

        static void Stat(Transform parent, string label, string value, Color c)
        {
            var b = Box(label, parent, CardBg);
            VList(b, 2, 12);
            LE(b, flexW: 1);
            Label(b.transform, Spaced(label), 11, TextAnchor.UpperLeft, Muted, FontStyle.Bold);
            Label(b.transform, value, 26, TextAnchor.UpperLeft, c, FontStyle.Bold);
        }

        // ───────────────────────── finale ─────────────────────────

        void BuildFinale(Transform root)
        {
            finale = Rect("Finale", root).gameObject;
            Fill((RectTransform)finale.transform);
            var scrim = finale.AddComponent<Image>();
            scrim.sprite = FadeRight();
            scrim.color = new Color(0.02f, 0.02f, 0.03f, 0.86f);

            var col = Rect("Column", finale.transform);
            Anchor(col, new Vector2(0, 0), new Vector2(0, 1), new Vector2(84, 40), new Vector2(84 + 620, -50));
            finaleBody = col;
        }

        void RefreshFinale()
        {
            Clear(finaleBody);
            var v = finaleBody.GetComponent<VerticalLayoutGroup>();
            if (!v) VList(finaleBody, 10, 0);

            Overline(finaleBody, "Expedition complete", Gold);
            Label(finaleBody, "The crew has landed!", 50, TextAnchor.UpperLeft, Ink, FontStyle.Bold);

            var mc = Box("MC", finaleBody, new Color(0.99f, 0.80f, 0.32f, 0.10f));
            VList(mc, 4, 16);
            Overline(mc.transform, "Message from Mission Control", Gold);
            Label(mc.transform, "To our junior astronaut: the first crew of six is safe at Meridiani Base. They are reading your Crew Handbook right now. You showed that people can live on Mars, by learning from the rovers that came before you. Thank you!", 17);

            Overline(finaleBody, "Your Crew Handbook", Muted);
            foreach (var ch in MissionLoader.Chapters)
                Label(finaleBody, SaveSystem.IsDone(ch.id)
                    ? $"<color=#8FD6FF><b>{ch.lesson}</b></color>  <color=#FFFFFF88>{ch.realEvent}</color>"
                    : $"<color=#FFFFFF55>{ch.stage}: not learned yet</color>", 16);

            int stars = MissionLoader.Chapters.Sum(c => SaveSystem.GetStars(c.id));
            Label(finaleBody, $"Stars: {stars} of {MissionLoader.Chapters.Length * 3}   ·   Suit items: {AchievementManager.UnlockedCount} of {AchievementManager.Catalog.Length}   ·   Rock samples brought home: {SaveSystem.TotalSamples}   ·   Research points: {SaveSystem.TotalResearch}",14, TextAnchor.UpperLeft, Muted);
            Label(finaleBody, "Want more? Replay any stage for 3 stars (and Commander stripes), or try Freeplay.", 14, TextAnchor.UpperLeft, Muted);

            var row = Rect("Buttons", finaleBody);
            HList(row, 10, 0);
            LE(row, prefH: 48);
            LE(Btn(row, "<b>Back to title</b>", 17, Accent, 0, () => flow.ShowTitle()), flexW: 1);
            LE(Btn(row, "Crew Handbook", 17, Quiet, 0, () => flow.ShowHandbook()), flexW: 1);
            LE(Btn(row, "Freeplay", 17, Quiet, 0, () => flow.ShowFreeplayHub()), flexW: 1);
        }

        // ───────────────────────── your suit ─────────────────────────

        // Suit screen: tabs, big item cards with rarity, progress bars, and try-on for locked items
        string suitTab = "trim";
        readonly Dictionary<string, Image> suitTabs = new Dictionary<string, Image>();
        RectTransform suitList;
        Image suitProgressFill;

        static readonly (string slot, string name)[] SuitTabs = { ("trim", "Suit color"), ("visor", "Visor"), ("patch", "Patches"), ("stripes", "Extras") };

        void BuildSuit(Transform root)
        {
            suit = Rect("Suit", root).gameObject;
            Fill((RectTransform)suit.transform);

            var p = Box("Panel", suit.transform, GlassDark, 1.2f).rectTransform;
            Anchor(p, new Vector2(0.5f, 0), new Vector2(1, 1), new Vector2(0, 20), new Vector2(-20, -20));
            var v = VList(p, 8, 22);
            v.childForceExpandHeight = false;

            var top = Rect("Top", p);
            HList(top, 0, 0);
            LE(top, prefH: 34);
            LE(Btn(top, "Back", 15, Quiet, 0, () => flow.CloseSuit()), prefW: 120);
            LE(Rect("Space", top), flexW: 1);

            Overline(p, "Your suit");
            Label(p, "Make it yours!", 32, TextAnchor.UpperLeft, Ink, FontStyle.Bold);
            suitCount = Label(p, "", 14, TextAnchor.UpperLeft, Muted);
            suitProgressFill = Bar(p, 8, new Color(1, 1, 1, 0.1f), Gold, out _).GetComponent<Image>();

            var tabs = Rect("Tabs", p);
            HList(tabs, 6, 0);
            LE(tabs, prefH: 40);
            foreach (var t in SuitTabs)
            {
                var slot = t.slot;
                var b = Btn(tabs, t.name, 16, Quiet, 0, () => { suitTab = slot; RefreshSuit(); });
                LE(b, flexW: 1);
                suitTabs[slot] = b.GetComponent<Image>();
            }

            var scroll = Scroll(p, out var content, 8);
            LE(scroll, flexH: 1, prefH: 300);
            suitList = content;

            var info = Box("Info", p, new Color(1, 1, 1, 0.07f));
            VList(info, 0, 12);
            LE(info, prefH: 70);
            suitInfo = Label(info.transform, "", 15);
        }

        void RefreshSuit()
        {
            int un = AchievementManager.UnlockedCount, all = AchievementManager.Catalog.Length;
            suitCount.text = $"Suit collection: {un} of {all}. Earn more by finishing stages, collecting rare rocks and drilling ice.";
            suitProgressFill.rectTransform.anchorMax = new Vector2(un / (float)all, 1);
            foreach (var kv in suitTabs) kv.Value.color = kv.Key == suitTab ? Accent : Quiet;

            Clear(suitList);
            var items = AchievementManager.InSlot(suitTab).ToList();
            for (int i = 0; i < items.Count; i += 2)
            {
                var row = Rect("Row", suitList);
                HList(row, 8, 0);
                LE(row, prefH: 86);
                for (int k = i; k < i + 2; k++)
                {
                    if (k < items.Count) SuitCard(row, items[k]);
                    else LE(Rect("Empty", row), flexW: 1);
                }
            }
        }

        void SuitCard(Transform row, Cosmetic item)
        {
            bool unlocked = AchievementManager.IsUnlocked(item.id);
            bool worn = SaveSystem.GetEquipped(item.slot, "") == item.id || (AchievementManager.IsEquipped(item) && !AchievementManager.Preview.ContainsKey(item.slot));
            bool previewing = AchievementManager.Preview.TryGetValue(item.slot, out var pv) && pv == item.id;
            var rc = AchievementManager.RarityColor(item.rarity);
            var b = Btn(row, "", 14, unlocked ? new Color(rc.r * 0.25f, rc.g * 0.25f, rc.b * 0.25f, 0.95f) : new Color(0.1f, 0.1f, 0.12f, 0.95f), 0, () => OnSuitItem(item));
            LE(b, flexW: 1);
            Clear(b.transform);
            if (worn || previewing)
            {
                var o = b.gameObject.AddComponent<Outline>();
                o.effectColor = previewing ? Gold : Good;
                o.effectDistance = new Vector2(3, -3);
            }
            var h = Rect("H", b.transform);
            Fill(h);
            HList(h, 10, 10, TextAnchor.MiddleLeft).childForceExpandHeight = false;

            // Swatch: patch badge, or a color disc (visors look glossy)
            var sw = Rect("Swatch", h);
            LE(sw, 58, 58).minWidth = 58;
            if (item.slot == "patch")
            {
                var raw = Rect("Patch", sw).gameObject.AddComponent<RawImage>();
                raw.texture = PatchMaker.Get(item.id);
                raw.raycastTarget = false;
                raw.color = unlocked ? Color.white : new Color(1, 1, 1, 0.35f);
                Fill(raw.rectTransform);
            }
            else
            {
                var disc = Box("Disc", sw, item.color, 50f);
                Fill(disc.rectTransform);
                disc.color = unlocked ? item.color : Color.Lerp(item.color, new Color(0.1f, 0.1f, 0.12f), 0.65f);
                if (item.slot == "visor")
                {
                    var shine = Box("Shine", disc.transform, new Color(1, 1, 1, 0.35f), 50f);
                    Anchor(shine.rectTransform, new Vector2(0.18f, 0.55f), new Vector2(0.5f, 0.85f), Vector2.zero, Vector2.zero);
                }
            }

            var col = Rect("C", h);
            VList(col, 2, 0, TextAnchor.MiddleLeft);
            LE(col, flexW: 1);
            Label(col, item.label, 15, TextAnchor.MiddleLeft, unlocked ? Ink : new Color(1, 1, 1, 0.7f), FontStyle.Bold);
            Label(col, item.rarity.ToUpperInvariant(), 11, TextAnchor.MiddleLeft, rc, FontStyle.Bold);
            if (unlocked)
                Label(col, worn ? "<color=#9CFFB8>Wearing</color>" : "Tap to wear", 12, TextAnchor.MiddleLeft, Muted);
            else
            {
                var (cur, max) = AchievementManager.Progress(item);
                Label(col, "Locked: " + item.howToUnlock, 11, TextAnchor.MiddleLeft, new Color(1f, 0.8f, 0.6f));
                if (max > 0)
                {
                    var fill = Bar(col, 5, new Color(1, 1, 1, 0.1f), rc, out _);
                    fill.anchorMax = new Vector2(Mathf.Clamp01(cur / (float)max), 1);
                }
            }
        }

        void OnSuitItem(Cosmetic c)
        {
            if (!AchievementManager.IsUnlocked(c.id))
            {
                // Try it on! (a preview only: it goes back when you leave this screen)
                AchievementManager.Preview[c.slot] = c.id;
                flow.RefreshSuitDisplay();
                var (cur, max) = AchievementManager.Progress(c);
                suitInfo.text = $"Trying on <b>{c.label}</b> <color=#{ColorUtility.ToHtmlStringRGB(AchievementManager.RarityColor(c.rarity))}>({c.rarity})</color>. Looks great!\n" +
                                $"<color=#FFC89A>To keep it: {c.howToUnlock}</color>" + (max > 0 ? $"  ({cur} of {max})" : "");
                RefreshSuit();
                return;
            }
            AchievementManager.Preview.Remove(c.slot);
            AchievementManager.Equip(c);
            flow.RefreshSuitDisplay();
            bool worn = AchievementManager.IsEquipped(c);
            suitInfo.text = c.slot == "stripes" && !worn ? $"<b>{c.label}</b> taken off." : $"Now wearing: <b>{c.label}</b>";
            RefreshSuit();
        }

        // ───────────────────────── rock collection ─────────────────────────

        GameObject rocks;
        RectTransform rockGrid, rockDetail;
        Text rocksSub;

        void BuildRocks(Transform root)
        {
            var body = Page(root, "Rocks", "Rock collection", "Every rock you pick up on Mars might be a rare find. All of these are real rocks discovered by Mars rovers.", out rocks, 0.85f);
            var grid = Rect("Grid", body);
            Anchor(grid, Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(4 * 168 + 3 * 10, -28));
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(168, 150);
            g.spacing = new Vector2(10, 10);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = 4;
            rockGrid = grid;
            rocksSub = Label(body, "", 15, TextAnchor.UpperLeft, Gold, FontStyle.Bold);
            Anchor(rocksSub.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -24), Vector2.zero);
            var det = Box("Detail", body, GlassDark, 1.2f).rectTransform;
            Anchor(det, new Vector2(0, 0), new Vector2(1, 1), new Vector2(4 * 168 + 3 * 10 + 20, 0), new Vector2(0, -28));
            VList(det, 8, 20);
            rockDetail = det;
        }

        void RefreshRocks()
        {
            Clear(rockGrid);
            var all = RockCollection.All;
            rocksSub.text = $"Found {RockCollection.UniqueFound} of {all.Length} kinds. Rarer rocks are harder to find!";
            foreach (var r in all)
            {
                var rock = r;
                bool has = RockCollection.Has(r.id);
                var rc = RockCollection.RarityColor(r.rarity);
                var b = Btn(rockGrid, "", 14, has ? new Color(rc.r * 0.22f, rc.g * 0.22f, rc.b * 0.22f, 0.95f) : new Color(0.08f, 0.08f, 0.1f, 0.95f), 0, () => ShowRock(rock));
                Clear(b.transform);
                var o = b.gameObject.AddComponent<Outline>();
                o.effectColor = has ? rc : new Color(1, 1, 1, 0.12f);
                o.effectDistance = new Vector2(2, -2);
                var v = Rect("V", b.transform);
                Fill(v);
                VList(v, 3, 10, TextAnchor.UpperCenter).childForceExpandHeight = false;
                var img = Rect("Icon", v).gameObject.AddComponent<Image>();
                img.sprite = Icons.Get("rock");
                img.preserveAspect = true;
                img.raycastTarget = false;
                img.color = has ? r.Tint : new Color(0, 0, 0, 0.85f);
                LE(img, 64, 64);
                Label(v, has ? r.name : "???", 13, TextAnchor.UpperCenter, has ? Ink : Muted, FontStyle.Bold);
                Label(v, r.rarity.ToUpperInvariant() + (has ? $"  x{RockCollection.Count(r.id)}" : ""), 11, TextAnchor.UpperCenter, rc, FontStyle.Bold);
            }
            var first = all.FirstOrDefault(r => RockCollection.Has(r.id)) ?? (all.Length > 0 ? all[0] : null);
            if (first != null) ShowRock(first);
        }

        void ShowRock(RockInfo r)
        {
            Clear(rockDetail);
            bool has = RockCollection.Has(r.id);
            var rc = RockCollection.RarityColor(r.rarity);
            var head = Rect("Head", rockDetail);
            HList(head, 14, 0, TextAnchor.MiddleLeft).childForceExpandHeight = false;
            var img = Rect("Icon", head).gameObject.AddComponent<Image>();
            img.sprite = Icons.Get("rock");
            img.preserveAspect = true;
            img.color = has ? r.Tint : new Color(0, 0, 0, 0.85f);
            LE(img, 90, 90).minWidth = 90;
            var c = Rect("C", head);
            VList(c, 2, 0, TextAnchor.MiddleLeft);
            LE(c, flexW: 1);
            Label(c, r.rarity.ToUpperInvariant(), 13, TextAnchor.MiddleLeft, rc, FontStyle.Bold);
            Label(c, has ? r.name : "Not found yet", 24, TextAnchor.MiddleLeft, Ink, FontStyle.Bold);
            if (has)
            {
                Label(rockDetail, r.fact, 16);
                Label(rockDetail, "<b>Why it matters:</b> " + r.why, 15, TextAnchor.UpperLeft, new Color(0.56f, 0.84f, 1f));
                Label(rockDetail, $"<b>Real find:</b> {r.foundBy}, {r.when}\n{r.where}", 14, TextAnchor.UpperLeft, Muted);
                Label(rockDetail, $"You have collected {RockCollection.Count(r.id)}.", 14, TextAnchor.UpperLeft, Gold);
            }
            else
            {
                string chance = r.rarity switch { "Common" => "common", "Uncommon" => "about 1 in 4 rocks is Uncommon", "Rare" => "about 1 in 8 rocks is Rare", "Epic" => "about 1 in 16 rocks is Epic", _ => "only 1 in 50 rocks is Legendary!" };
                Label(rockDetail, $"Keep collecting rock samples on Mars to find this one. ({chance})", 16, TextAnchor.UpperLeft, Muted);
            }
        }

        // ───────────────────────── science page ─────────────────────────

        void BuildScience(Transform root)
        {
            var body = Page(root, "Science", "Science behind the game", "Real NASA numbers in this game wear a badge, so you always know where they came from.", out science, 0.8f);
            var scroll = Scroll(body, out var content, 14);
            Fill((RectTransform)scroll.transform);

            var badges = Rect("Badges", content);
            HList(badges, 10, 0);
            LE(badges, prefH: 64);
            BadgeCard(badges, "Real · Live", "Fetched from the internet right now.", Good);
            BadgeCard(badges, "Real · Archived", "Real measurements from a past date (the date is shown).", UIKit.Shield);

            // Live NASA data: ask DONKI about the Sun's storms right now.
            var live = Box("Live", content, new Color(0.36f, 0.80f, 0.52f, 0.10f));
            VList(live, 6, 14);
            Overline(live.transform, "Space weather right now  ·  NASA DONKI", Good);
            Label(live.transform, "DONKI is NASA's space weather database. It records every solar flare and every storm of particles from the Sun. Ask it about the last 30 days:", 15);
            liveResult = Label(live.transform, "", 15, TextAnchor.UpperLeft, Ink);
            var ask = Btn(live.transform, "Ask DONKI now (live)", 15, Good, 38, AskDonki);
            LE(ask, prefW: 240);
            live.GetComponent<VerticalLayoutGroup>().childForceExpandWidth = false;
            LE(liveResult, prefW: 800);

            Overline(content, "Real NASA pictures in this game");
            foreach (var img in ContentDb.NasaImages)
                Label(content, $"<b>{img.title}</b> ({img.date})  <color=#FFFFFF99>{img.credit}  ·  {img.nasaId}</color>", 14);

            Overline(content, "Datasets");
            foreach (var s in ContentDb.AllSources)
            {
                var card = Box(s.key, content, CardBg);
                VList(card, 4, 14);
                Label(card.transform, $"<b>{s.name}</b>   <color=#FFFFFF88><size=13>{s.provider}</size></color>", 18);
                Label(card.transform, s.usedFor, 15);
                if (!string.IsNullOrEmpty(s.url)) Label(card.transform, s.url, 12, TextAnchor.UpperLeft, new Color(0.56f, 0.84f, 1f, 0.9f));
            }

            var snap = DonkiService.Snapshot;
            if (snap != null)
                Label(content, $"If NASA can't be reached, the game shows a DONKI snapshot saved on {snap.snapshotDate} (Real · Archived).", 15, TextAnchor.UpperLeft, Muted);

            Overline(content, "Words to know");
            var gl = ContentDb.LoadJson<GlossaryList>("Data/glossary");
            if (gl?.entries != null)
                foreach (var e in gl.entries)
                    if (e.key != "radiation" && e.key != "dose" && e.key != "shielding")   // removed from the game
                    Label(content, $"<b>{e.word}</b>  <color=#FFFFFFAA>{e.meaning}</color>", 15);
            Spacer(content, 20);
        }

        Text liveResult;
        bool asking;

        void AskDonki()
        {
            if (asking) return;
            asking = true;
            liveResult.text = "Asking NASA DONKI...";
            StartCoroutine(AskRoutine());
        }

        System.Collections.IEnumerator AskRoutine()
        {
            yield return NasaLive.Refresh();
            asking = false;
            ShowLiveResult();
        }

        void ShowLiveResult()
        {
            if (!liveResult) return;
            if (NasaLive.State == NasaLive.Status.Live)
                liveResult.text = $"<b><color=#6CFF8F>{NasaLive.BadgeLine()}</color></b>  (via {NasaLive.Via})\n{NasaLive.Summary()}\n" +
                                  $"<color=#8FD6FF>{NasaLive.MarsDateLine()}</color>";
            else if (NasaLive.State == NasaLive.Status.Checking)
                liveResult.text = "Connecting to NASA...";
            else
                liveResult.text = "<color=#F0C080>Couldn't reach NASA DONKI right now</color> (no internet, or the shared key is busy). " +
                                  $"The game uses its saved DONKI data from {DonkiService.Snapshot?.snapshotDate} <b>(Real · Archived)</b>.\n" +
                                  $"<color=#8FD6FF>{NasaLive.MarsDateLine()}</color>";
        }

        static void BadgeCard(Transform parent, string head, string text, Color c)
        {
            var b = Box(head, parent, new Color(c.r, c.g, c.b, 0.14f));
            VList(b, 2, 10);
            LE(b, flexW: 1);
            Label(b.transform, $"<b>{head}</b>", 15, TextAnchor.UpperLeft, c);
            Label(b.transform, text, 13, TextAnchor.UpperLeft, Muted);
        }
    }
}