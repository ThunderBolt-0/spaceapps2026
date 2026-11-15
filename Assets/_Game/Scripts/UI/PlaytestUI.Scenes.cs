using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static MissionTrainer.UIKit;

namespace MissionTrainer
{
    /// <summary>
    /// Cutscenes and pop-ups: suiting up / taking the suit off, night on Mars and the next morning, rover rides,
    /// lab scans, the "leave the ice behind?" question, new rocks for the collection, and the storm / night banners.
    /// All built in code (works in WebGL), all skippable after a moment.
    /// </summary>
    public partial class PlaytestUI
    {
        GameObject cutscene, confirmOverlay, rockOverlay;
        Image csBgTop, csBgBottom, csGround, csDome, csWindow, csSun, csPhobos, csDeimos, csIcon;
        RectTransform csStars;
        readonly List<Image> stars = new List<Image>();
        Text csTitle, csSub, csHint;
        RectTransform csLines;
        bool csSkippable, csSkip;
        Text confirmTitle, confirmBody;
        Button confirmYes, confirmNo;
        Text rockRarity, rockName, rockNew, rockFact, rockWhy, rockWhere, rockCount;
        Image rockSwatch, rockGlow;

        // banners
        Image alertBg;
        Text alertText;

        static readonly string[] NightFacts =
        {
            "Mars has two small moons: Phobos and Deimos. Phobos zooms across the sky so fast that it rises in the west and sets in the east!",
            "Nights at Meridiani can drop to about -80 °C. That's colder than almost anywhere on Earth.",
            "A Mars day (a sol) is 24 hours and 39 minutes long. You get 39 extra minutes every day!",
            "The air on Mars is about 100 times thinner than on Earth, and it's mostly carbon dioxide.",
            "From Mars, Earth looks like a bright blue evening star.",
        };

        void BuildScenes(Transform root)
        {
            // ── Full-screen cutscene
            var cs = Rect("Cutscene", root);
            Fill(cs);
            cutscene = cs.gameObject;
            csBgBottom = cs.gameObject.AddComponent<Image>();
            var top = Rect("Top", cs);
            Anchor(top, new Vector2(0, 0.45f), Vector2.one, Vector2.zero, Vector2.zero);
            csBgTop = top.gameObject.AddComponent<Image>();
            csStars = Rect("Stars", cs);
            Anchor(csStars, new Vector2(0, 0.3f), Vector2.one, Vector2.zero, Vector2.zero);
            var rng = new System.Random(7);
            for (int i = 0; i < 70; i++)
            {
                var s = Rect("Star", csStars).gameObject.AddComponent<Image>();
                var r = s.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2((float)rng.NextDouble(), (float)rng.NextDouble());
                float size = 1.5f + (float)rng.NextDouble() * 2.5f;
                r.sizeDelta = new Vector2(size, size);
                s.raycastTarget = false;
                stars.Add(s);
            }
            csSun = Disc(cs, "Sun", 90, new Color(1f, 0.85f, 0.55f));
            csPhobos = Disc(cs, "Phobos", 26, new Color(0.75f, 0.7f, 0.65f));
            csDeimos = Disc(cs, "Deimos", 12, new Color(0.85f, 0.82f, 0.78f));
            var ground = Rect("Ground", cs);
            Anchor(ground, Vector2.zero, new Vector2(1, 0.3f), Vector2.zero, Vector2.zero);
            csGround = ground.gameObject.AddComponent<Image>();
            csDome = Disc(cs, "Dome", 260, new Color(0.18f, 0.17f, 0.19f));
            csDome.rectTransform.anchorMin = csDome.rectTransform.anchorMax = new Vector2(0.78f, 0.3f);
            csWindow = Disc(csDome.transform, "Window", 22, new Color(1f, 0.85f, 0.5f));
            csWindow.rectTransform.anchorMin = csWindow.rectTransform.anchorMax = new Vector2(0.5f, 0.68f);
            csWindow.rectTransform.anchoredPosition = Vector2.zero;

            var panel = Rect("Text", cs);
            Anchor(panel, new Vector2(0.08f, 0.32f), new Vector2(0.62f, 0.92f), Vector2.zero, Vector2.zero);
            var v = VList(panel, 10, 0, TextAnchor.UpperLeft);
            v.childForceExpandHeight = false;
            csIcon = IconImgPublic(panel, "suit", 84, Ink);
            csTitle = Label(panel, "", 40, TextAnchor.UpperLeft, Ink, FontStyle.Bold);
            csTitle.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.6f);
            csSub = Label(panel, "", 18, TextAnchor.UpperLeft, new Color(1, 1, 1, 0.85f));
            csSub.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.8f);
            csLines = Rect("Lines", panel);
            VList(csLines, 6, 0);
            csHint = Label(cs, "", 14, TextAnchor.LowerRight, new Color(1, 1, 1, 0.55f));
            Anchor(csHint.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-420, 16), new Vector2(-20, 40));
            var skip = cs.gameObject.AddComponent<Button>();
            skip.transition = Selectable.Transition.None;
            skip.navigation = new Navigation { mode = Navigation.Mode.None };
            skip.onClick.AddListener(() => { if (csSkippable) csSkip = true; });
            cutscene.SetActive(false);

            // ── Yes / no question
            confirmOverlay = Overlay(root, "Confirm", 640, out var cp);
            var head = Rect("Head", cp);
            HList(head, 10, 0, TextAnchor.MiddleLeft);
            IconImg(head, "alert", 34, Bad);
            confirmTitle = Label(head, "", 28, TextAnchor.MiddleLeft, Ink, FontStyle.Bold);
            LE(confirmTitle, flexW: 1);
            confirmBody = Label(cp, "", 18);
            var row = Rect("Buttons", cp);
            HList(row, 12, 0);
            LE(row, prefH: 54);
            confirmNo = Btn(row, "", 18, Good, 0, () => { });
            confirmYes = Btn(row, "", 18, Bad, 0, () => { });
            confirmOverlay.SetActive(false);

            // ── New rock for the collection
            rockOverlay = Overlay(root, "NewRock", 700, out var rp);
            rockNew = Label(rp, "NEW ROCK FOR YOUR COLLECTION!", 16, TextAnchor.UpperLeft, Gold, FontStyle.Bold);
            var rr = Rect("Row", rp);
            HList(rr, 16, 0, TextAnchor.UpperLeft).childForceExpandHeight = false;
            var swatchBox = Rect("Swatch", rr);
            LE(swatchBox, 150, 150).minWidth = 150;
            rockGlow = Disc(swatchBox, "Glow", 150, Gold);
            rockGlow.rectTransform.anchorMin = rockGlow.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rockSwatch = IconImgPublic(swatchBox, "rock", 120, Color.gray);
            var sr = rockSwatch.rectTransform;
            sr.anchorMin = sr.anchorMax = new Vector2(0.5f, 0.5f);
            sr.anchoredPosition = Vector2.zero;
            sr.sizeDelta = new Vector2(120, 120);
            rockSwatch.GetComponent<LayoutElement>().ignoreLayout = true;
            var col = Rect("Col", rr);
            VList(col, 6, 0);
            LE(col, flexW: 1);
            rockRarity = Label(col, "", 16, TextAnchor.UpperLeft, Gold, FontStyle.Bold);
            rockName = Label(col, "", 28, TextAnchor.UpperLeft, Ink, FontStyle.Bold);
            rockFact = Label(col, "", 16);
            rockWhy = Label(rp, "", 16, TextAnchor.UpperLeft, new Color(0.56f, 0.84f, 1f));
            rockWhere = Label(rp, "", 14, TextAnchor.UpperLeft, Muted);
            rockCount = Label(rp, "", 14, TextAnchor.UpperLeft, Gold);
            Btn(rp, "<b>Awesome!</b>", 20, Accent, 52, () => rockOverlay.SetActive(false));
            rockOverlay.SetActive(false);

            runner.RockFound += OnRockFound;
            runner.StepStarted += _ => { habQueue.Clear(); habTimer = 0; };   // a new step: its explanation shows now, not after old lines
            runner.StormStarted += () => { habTimer = 0; };
        }

        void BuildBanners(Transform hud)
        {
            alertBg = Box("Alert", hud, new Color(0.7f, 0.1f, 0.08f, 0.95f));
            var r = alertBg.rectTransform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 1);
            r.anchoredPosition = new Vector2(0, -214);
            r.sizeDelta = new Vector2(560, 60);
            alertBg.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;   // grows with its text
            var h = HList(alertBg, 12, 10, TextAnchor.MiddleLeft);
            h.childForceExpandHeight = false;
            IconImg(alertBg.transform, "alert", 32, Ink);
            alertText = Label(alertBg.transform, "", 16, TextAnchor.MiddleLeft, Ink);
            LE(alertText, flexW: 1);
            alertBg.gameObject.SetActive(false);
        }

        void TickBanners()
        {
            if (!alertBg) return;
            var s = runner.State;
            bool storm = MissionRunning && s.stormActive;
            bool night = MissionRunning && s.nightActive;
            bool show = (storm || night) && hudGroup.alpha > 0.5f;
            if (alertBg.gameObject.activeSelf != show) alertBg.gameObject.SetActive(show);
            if (!show) return;
            // Stack under the Day card, and under the OUTSIDE bar when that's showing
            float y = UnderDayCard - (evaBox && evaBox.activeSelf ? evaBg.rectTransform.rect.height + 8f : 0f);
            alertBg.rectTransform.anchoredPosition = new Vector2(0, y);
            if (storm)
            {
                float left = runner.StormSecondsLeft;
                alertText.text = $"<b>DUST STORM! Get back to base:  {(int)left / 60}:{(int)left % 60:00}</b>\n" +
                                 (runner.SuitLeaking ? "<b>Your suit is LEAKING!</b> Air is dropping fast." : "Air drops faster in the storm. Leaving before all 3 ice = losing the ice.");
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * (runner.SuitLeaking ? 10f : 5f));
                alertBg.color = Color.Lerp(new Color(0.55f, 0.06f, 0.05f, 0.95f), new Color(0.9f, 0.15f, 0.1f, 0.97f), pulse);
            }
            else
            {
                float left = runner.NightSecondsLeft;
                float rate = runner.NightPowerPerSecond;
                float lasts = rate > 0.001f ? s.power / rate : 999;
                bool ok = lasts > left;
                alertText.text = $"<b>NIGHT: no sun. Sunrise in {(int)left / 60}:{(int)left % 60:00}</b>\n" +
                                 $"Battery {s.power:0.0}%  ·  -{rate:0.00}% every second  ·  " +
                                 (ok ? "<color=#9CFFB8>You'll make it!</color>" : "<color=#FFD0A0>Not enough! Switch something off.</color>");
                alertBg.color = ok ? new Color(0.10f, 0.16f, 0.36f, 0.96f) : new Color(0.45f, 0.12f, 0.30f, 0.96f);
            }
        }

        // ───────── helpers ─────────

        static Image Disc(Transform parent, string name, float size, Color c)
        {
            var img = Box(name, parent, c, 50f);
            var r = img.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(size, size);
            img.raycastTarget = false;
            return img;
        }

        static Image IconImgPublic(Transform parent, string icon, float size, Color c) => IconImg(parent, icon, size, c);

        void SceneLook(Color top, Color bottom, Color ground, bool starsOn, bool moons, bool sun, bool dome, string icon)
        {
            csBgTop.color = top;
            csBgBottom.color = bottom;
            csGround.color = ground;
            csStars.gameObject.SetActive(starsOn);
            csPhobos.gameObject.SetActive(moons);
            csDeimos.gameObject.SetActive(moons);
            csSun.gameObject.SetActive(sun);
            csDome.gameObject.SetActive(dome);
            csIcon.gameObject.SetActive(icon != null);
            if (icon != null) csIcon.sprite = Icons.Get(icon);
            csTitle.text = csSub.text = csHint.text = "";
            Clear(csLines);
        }

        Text AddLine(string text, Color? c = null)
        {
            var row = Rect("L", csLines);
            HList(row, 8, 0, TextAnchor.MiddleLeft).childForceExpandHeight = false;
            IconImg(row, "check", 20, Good);
            var t = Label(row, text, 20, TextAnchor.MiddleLeft, c ?? Ink);
            t.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.8f);
            return t;
        }

        IEnumerator Wait(float seconds)
        {
            float t = 0;
            while (t < seconds && !csSkip) { t += Time.unscaledDeltaTime; AnimateSky(); yield return null; }
        }

        float skyT;
        void AnimateSky()
        {
            skyT += Time.unscaledDeltaTime;
            if (csStars.gameObject.activeSelf)
                for (int i = 0; i < stars.Count; i++)
                    stars[i].color = new Color(1, 1, 1, 0.35f + 0.55f * Mathf.PerlinNoise(i * 3.1f, skyT * 1.4f));
            var half = hudRoot.rect.size * 0.5f;
            // Phobos really rises in the west and sets in the east, fast. Deimos creeps slowly.
            if (csPhobos.gameObject.activeSelf)
            {
                float p = Mathf.Repeat(skyT / 6f, 1f);
                csPhobos.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-half.x, half.x, p), half.y * 0.45f + Mathf.Sin(p * Mathf.PI) * half.y * 0.35f);
                float d = skyT / 60f;
                csDeimos.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(half.x * 0.6f, -half.x * 0.2f, d), half.y * 0.75f);
            }
            if (csSun.gameObject.activeSelf)
                csSun.rectTransform.anchoredPosition = new Vector2(-half.x * 0.35f, Mathf.Lerp(-half.y * 0.45f, half.y * 0.1f, Mathf.Clamp01(skyT / 3f)));
            if (csWindow) csWindow.color = new Color(1f, 0.85f, 0.5f, 0.75f + 0.25f * Mathf.Sin(skyT * 2f));
        }

        IEnumerator RunScene(IEnumerator body)
        {
            cutscene.SetActive(true);
            cutscene.transform.SetAsLastSibling();
            csSkip = false;
            csSkippable = false;
            skyT = 0;
            yield return body;
            cutscene.SetActive(false);
            if (pendingCard != null && runner.ActiveCard == pendingCard && !AnyOverlay) { var c = pendingCard; pendingCard = null; ShowCardNow(c); }
        }

        // ───────── suit on / off (instant, no cutscene) ─────────

        public void PlaySuitScene(bool on, Action after)
        {
            if (!MissionRunning) return;
            if (!on && runner.State.outside) { runner.Say("HAB: Never take your suit off outside! There's no air to breathe out there."); return; }
            runner.SetSuit(on);
            runner.Say(on ? "HAB: Suit on and sealed. You're ready for the airlock!" : "HAB: Suit back on the stand. Ahh, fresh base air!");
            after?.Invoke();
        }

        // ───────── sleep: a camera outside the base watches the Sun set, the moons cross the sky, and the Sun rise ─────────

        public void Sleep()
        {
            if (!MissionRunning || AnyOverlay) return;
            var why = runner.CantSleepReason();
            if (why != null) { if (why.Length > 0) runner.Say("HAB: " + why); return; }
            StartCoroutine(RunScene(NightScene()));
        }

        Transform phobos, deimos;

        /// <summary>Small glowing spheres far away for Phobos and Deimos (copy of the habitat lamp's material, so WebGL keeps the shader).</summary>
        void EnsureMoons(HazardVisuals hv)
        {
            if (phobos || !hv || !hv.warningLamp) return;
            Transform Moon(string name, float size, Color c)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(go.GetComponent<Collider>());
                go.name = name;
                var m = new Material(hv.warningLamp.sharedMaterial);
                m.EnableKeyword("_EMISSION");
                m.SetColor("_BaseColor", c * 0.5f);
                m.SetColor("_EmissionColor", c * 1.4f);
                go.GetComponent<Renderer>().sharedMaterial = m;
                go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.transform.localScale = Vector3.one * size;
                go.SetActive(false);
                return go.transform;
            }
            phobos = Moon("Phobos", 7f, new Color(0.78f, 0.74f, 0.70f));
            deimos = Moon("Deimos", 3f, new Color(0.85f, 0.83f, 0.80f));
        }

        IEnumerator NightScene()
        {
            var s = runner.State;
            float p0 = s.power, o0 = s.oxygen, ice = s.iceWater;
            int food0 = s.food, sol0 = s.sol;
            bool slept = false;
            void DoNight() { if (!slept) { slept = true; runner.NextSol(); } }   // the night happens behind the scenes

            var hv = FindAnyObjectByType<HazardVisuals>();
            var flow = GameFlow.Current;
            var cam = flow ? flow.BeginCinematic() : null;
            if (!cam || !hv || !hv.sun) { if (flow) flow.EndCinematic(); DoNight(); yield break; }
            EnsureMoons(hv);

            // Captions over the live 3D view
            SceneLook(Color.clear, Color.clear, Color.clear, false, false, false, false, null);
            csGround.gameObject.SetActive(false);
            float hud0 = hudGroup.alpha;
            hudGroup.alpha = 0;
            csSkippable = true;
            csHint.text = "Click to skip";

            var hab = GameObject.Find("Habitat");
            Vector3 center = hab ? hab.transform.position : Vector3.zero;
            var terrain = Terrain.activeTerrain;
            var sunT = hv.sun.transform;
            var sunRot0 = sunT.rotation;
            const float radius = 30f, yaw0 = 205f;

            void Place(float yaw, float sunElev, float night)
            {
                float a = yaw * Mathf.Deg2Rad;
                var pos = center + new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * radius;
                pos.y = (terrain ? terrain.SampleHeight(pos) + terrain.transform.position.y : center.y) + 5.5f;
                var look = center + Vector3.up * 5f;
                cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos));
                // The Sun sits in front of the camera, low in the sky (the light shines toward the camera)
                var flat = look - pos; flat.y = 0;
                float facing = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
                sunT.rotation = Quaternion.Euler(sunElev, facing + 180f, 0);
                HazardVisuals.CinematicNight = night;
            }

            // 1. Sunset (on Mars, sunsets glow blue around the Sun)
            csTitle.text = $"Sol {sol0 + 1}: the Sun sets over Meridiani";
            csSub.text = "On Mars, the sky around a setting Sun turns blue. Fine dust scatters blue light forward.";
            for (float t = 0; t < 1f && !csSkip; t += Time.unscaledDeltaTime / 4.5f)
            {
                float elev = Mathf.Lerp(16f, -7f, t);
                Place(yaw0 + 12f * t, elev, Mathf.Clamp01(Mathf.InverseLerp(5f, -6f, elev)));
                yield return null;
            }
            DoNight();
            var n = runner.State;

            // 2. Night: the camera circles the base; Phobos zooms across, Deimos creeps
            if (!csSkip && !runner.IsOver)
            {
                csTitle.text = $"Night on Mars  ·  Sol {sol0 + 1}";
                csSub.text = $"It's about {Mathf.RoundToInt(-75 - 30 * DayPlanner.Today(runner).cold)} °C outside. HAB keeps watch while you sleep.";
                AddLine($"Battery: {p0:0}% → {n.power:0}%", n.power < p0 ? Warn : Good);
                AddLine($"Air: {o0:0}% → {n.oxygen:0}%", n.oxygen < o0 ? Warn : Good);
                AddLine($"Food: {food0} → {n.food} days");
                if (ice > 0) AddLine("Ice melted and split into fresh oxygen!", new Color(0.56f, 0.84f, 1f));
                var fact = Label(csLines, "<color=#8FD6FF>" + NightFacts[sol0 % NightFacts.Length] + "</color>", 17);
                fact.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.8f);
                phobos.gameObject.SetActive(true);
                deimos.gameObject.SetActive(true);
                for (float t = 0; t < 1f && !csSkip; t += Time.unscaledDeltaTime / 6.5f)
                {
                    Place(yaw0 + 12f + 168f * Mathf.SmoothStep(0, 1, t), -10f, 1f);
                    var fwd = cam.transform.forward; fwd.y = 0; fwd.Normalize();
                    var right = Vector3.Cross(Vector3.up, fwd);
                    // Phobos rises in the west and sets in the east, twice a day: here it races across in seconds
                    float px = Mathf.Lerp(-1.3f, 1.3f, t), py = 0.08f + Mathf.Sin(t * Mathf.PI) * 0.45f;
                    phobos.position = cam.transform.position + (fwd + right * px + Vector3.up * py).normalized * 170f;
                    deimos.position = cam.transform.position + (fwd + right * Mathf.Lerp(0.5f, 0.35f, t) + Vector3.up * 0.55f).normalized * 170f;
                    yield return null;
                }
                phobos.gameObject.SetActive(false);
                deimos.gameObject.SetActive(false);
            }

            // 3. Sunrise on the other side of the sky
            if (!csSkip && !runner.IsOver)
            {
                Clear(csLines);
                csTitle.text = $"Good morning!  ·  Day {DayPlanner.Day(runner.State)}";
                csSub.text = "The Sun rises over Meridiani Planum. From Mars it looks smaller than from Earth.";
                for (float t = 0; t < 1f && !csSkip; t += Time.unscaledDeltaTime / 4.5f)
                {
                    float elev = Mathf.Lerp(-7f, 16f, t);
                    Place(yaw0 + 180f + 12f * t, elev, Mathf.Clamp01(Mathf.InverseLerp(5f, -6f, elev)));
                    yield return null;
                }
            }

            // Back inside: put the real sky and the player's view back
            sunT.rotation = sunRot0;
            HazardVisuals.CinematicNight = -1f;
            if (phobos) { phobos.gameObject.SetActive(false); deimos.gameObject.SetActive(false); }
            flow.EndCinematic();
            hudGroup.alpha = runner.IsOver ? hudGroup.alpha : hud0;
            if (!runner.IsOver) runner.Say("HAB: Good morning! Your suit is on the stand. First mission: check the NASA forecast.");
        }

        // ───────── rover rides ─────────

        public void RideRover(RoverActor rover, bool toPing)
        {
            if (!MissionRunning || AnyOverlay) return;
            var s = runner.State;
            var st = runner.CurrentStep;
            if (!toPing && st != null && st.kind == "ice" && s.iceStep < Mathf.Max(1, st.count) && !s.iceLost)
            {
                Confirm("Leave the ice behind?",
                    $"If you drive home now, you will lose ALL your ice samples (you have {s.iceStep} of 3). Tonight there will be no extra water to make air.",
                    "Leave now", "Stay and collect",
                    () => { runner.LoseIce(); StartCoroutine(RunScene(RideScene(rover, false))); });
                return;
            }
            StartCoroutine(RunScene(RideScene(rover, toPing)));
        }

        IEnumerator RideScene(RoverActor rover, bool toPing)
        {
            SceneLook(new Color(0, 0, 0, 0.55f), new Color(0, 0, 0, 0.55f), Color.clear, false, false, false, false, "rover");
            csTitle.text = toPing ? "Driving to the pinged location" : "Driving back to base";
            csSub.text = "The real Perseverance drives at about 4 centimeters per second. We sped this trip up for the game!";
            AddLine(toPing ? "Following Mission Control's map..." : "Following our own wheel tracks home...");
            yield return Wait(1.0f);
            AddLine("Avoiding sharp rocks and soft sand...");
            yield return Wait(0.9f);
            AddLine("Arrived!");
            yield return Wait(0.5f);

            var ice = GameFlow.IceSitePos;
            Vector3 roverPos, playerPos, look;
            if (toPing) { roverPos = ice + new Vector3(6, 0, -10); playerPos = ice + new Vector3(2, 0, -7); look = ice; }
            else { roverPos = new Vector3(5, 0, 17); playerPos = new Vector3(1.2f, 0, 13.5f); look = new Vector3(0, 0, 10); }
            rover.ParkAt(roverPos, toPing ? ice : Vector3.zero);
            var t = Terrain.activeTerrain;
            if (t) playerPos.y = t.SampleHeight(playerPos) + t.transform.position.y + 0.1f;
            if (fpc) fpc.TeleportTo(playerPos, look);
            runner.State.roverAtPing = toPing;
            runner.SetLocation(true, false);
            runner.Say(toPing ? "HAB: Here we are! Drill the icy patches. Watch your air." : "HAB: We're home! Get through the airlock.");
        }

        // ───────── lab scan, HAB call, status check ─────────

        void PlayScan()
        {
            if (!runner.State.IsOn(SystemIds.Science)) { runner.Say("HAB: Turn ON the Science lab first. Its scanner needs power."); return; }
            StartCoroutine(RunScene(ScanScene()));
        }

        IEnumerator ScanScene()
        {
            var s = runner.State;
            SceneLook(new Color(0, 0, 0, 0.6f), new Color(0, 0, 0, 0.6f), Color.clear, false, false, false, false, "lab");
            csTitle.text = "Science lab scan";
            csSub.text = "The lab's instruments look at what each sample is made of, like the tools on real rovers.";
            int rocks = s.samples - s.samplesStudied;
            for (int i = 0; i < Mathf.Min(rocks, 6); i++) { AddLine($"Rock sample {i + 1}: minerals measured"); yield return Wait(0.35f); }
            for (int i = 0; i < s.iceWater; i++) { AddLine($"Ice sample {i + 1}: it's water!", new Color(0.56f, 0.84f, 1f)); yield return Wait(0.35f); }
            yield return Wait(0.4f);
            runner.ScanSamples();
        }

        void HabCall()
        {
            infoOver.text = Spaced("Connection established");
            infoTitle.text = "Hi! I'm HAB.";
            infoBody.text = Gloss("I'm the computer that runs Meridiani Base. I watch your power, air and food, and I talk to Mission Control on Earth for you.\n\n" +
                                  "Follow the mission list on the right, one step at a time. I'll explain everything as we go!");
            infoBadge.text = "Tip: words in blue can be clicked for their meaning.";
            infoOverlay.SetActive(true);
            runner.TalkToHab();
        }

        void StatusCheck()
        {
            var s = runner.State;
            var on = new List<string>();
            foreach (var t in tiles) if (s.IsOn(t.def.id)) on.Add(KidName(t.def.id));
            infoOver.text = Spaced("Status check");
            infoTitle.text = s.power >= 50 && s.oxygen >= 50 ? "All systems look good" : "Some numbers are low";
            infoBody.text = Gloss($"<b>Battery:</b> {s.power:0}%. The solar panels fill it in the day.\n" +
                                  $"<b>Air:</b> {s.oxygen:0}%. Outside, it drops every second.\n" +
                                  $"<b>Food:</b> {s.food} days.   <b>Suit:</b> {(s.suitOn ? "ON" : "on the stand")}.\n" +
                                  $"<b>Systems ON:</b> {(on.Count > 0 ? string.Join(", ", on) : "none")}.\n\n" +
                                  "Always check these before you go outside. Real astronauts do this every time!");
            infoBadge.text = "The gauges are on the top left. The system tiles are at the bottom.";
            infoOverlay.SetActive(true);
            runner.StatusChecked();
        }

        // ───────── yes / no ─────────

        void Confirm(string title, string body, string yes, string no, Action onYes)
        {
            confirmTitle.text = title;
            confirmBody.text = body;
            confirmYes.GetComponentInChildren<Text>().text = $"<b>{yes}</b>";
            confirmNo.GetComponentInChildren<Text>().text = $"<b>{no}</b>";
            confirmYes.onClick.RemoveAllListeners();
            confirmNo.onClick.RemoveAllListeners();
            confirmYes.onClick.AddListener(() => { confirmOverlay.SetActive(false); onYes?.Invoke(); });
            confirmNo.onClick.AddListener(() => confirmOverlay.SetActive(false));
            confirmOverlay.SetActive(true);
            confirmOverlay.transform.SetAsLastSibling();
        }

        // ───────── rock collection ─────────

        void OnRockFound(RockInfo rock, bool isNew)
        {
            if (!isNew || rock == null) return;
            var rc = RockCollection.RarityColor(rock.rarity);
            int bonus = RockCollection.ResearchBonus[RockCollection.RarityIndex(rock.rarity)];
            rockRarity.text = rock.rarity.ToUpperInvariant() + (bonus > 0 ? $"   ·   +{bonus} bonus research" : "");
            rockRarity.color = rc;
            rockName.text = rock.name;
            rockFact.text = rock.fact;
            rockWhy.text = "Why it matters: " + rock.why;
            rockWhere.text = $"Real find: {rock.foundBy}, {rock.when}, {rock.where}.";
            rockCount.text = $"Your collection: {RockCollection.UniqueFound} of {RockCollection.All.Length} kinds  ·  see them all on the home screen";
            rockSwatch.color = rock.Tint;
            rockGlow.color = new Color(rc.r, rc.g, rc.b, 0.35f);
            rockOverlay.SetActive(true);
            rockOverlay.transform.SetAsLastSibling();
        }
    }
}