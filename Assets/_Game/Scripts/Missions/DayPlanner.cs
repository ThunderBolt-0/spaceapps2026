using System;
using System.Collections.Generic;
using UnityEngine;

namespace MissionTrainer
{
    /// <summary>
    /// Turns each day's data (real NASA readings where we have them) into:
    ///  - a Mars Report: today's sky in pictures and plain words, with its Real/Practice data label
    ///  - Today's jobs: 1-3 simple steps the player should do before ending the day.
    /// This is what tells a 7-12 year old exactly what to do, and why (because of the real data).
    /// </summary>
    public static class DayPlanner
    {
        public class Reading
        {
            public string icon, name, word, detail;
            public Color color;
        }

        public class Job
        {
            public string key;                    // stable id ("forecast", "samples", "ice"...): the HUD updates a row with the same key
            public string icon, text, why;
            public string target;                 // where the waypoint points
            public string action;                 // optional button: "forecast", "go:samples", "go:solar", "go:rover", "status", "shelter", "inside"
            public string actionLabel;
            public bool optional;                 // not needed to finish the day (the ice trip)
            public bool afterForecast = true;     // locked until the NASA forecast is checked
            public bool stepLocked;               // story steps: a later step (shown greyed, comes next)
            public bool red;                      // emergency
            public Func<HabitatState, bool> done;
        }

        static readonly Color Good = new Color(0.36f, 0.80f, 0.52f);
        static readonly Color Warn = new Color(0.96f, 0.76f, 0.30f);
        static readonly Color Bad = new Color(0.93f, 0.36f, 0.33f);
        static readonly Color Cool = new Color(0.55f, 0.8f, 1f);

        /// <summary>The day the player is about to live through (State.sol counts finished days).</summary>
        public static int Day(HabitatState s) => s.sol + 1;

        public static SolEnv Today(MissionRunner r) => r.Mission.EnvForSol(r.State.sol + 1);

        public static List<Reading> Readings(SolEnv e)
        {
            var list = new List<Reading>
            {
                new Reading { icon = "sun", name = "Sunlight", detail = $"{e.sunlight * 100:0}% of a sunny day",
                    word = e.sunlight >= 0.8f ? "Bright" : e.sunlight >= 0.4f ? "Dim" : "Very dark",
                    color = e.sunlight >= 0.8f ? Good : e.sunlight >= 0.4f ? Warn : Bad },
                new Reading { icon = "dust", name = "Dust", detail = $"tau {e.tau:0.0}",
                    word = e.tau < 1 ? "Clear sky" : e.tau < 4 ? "Dusty" : "Dust storm!",
                    color = e.tau < 1 ? Good : e.tau < 4 ? Warn : Bad },
                new Reading { icon = "snow", name = "Cold", detail = e.cold >= 0.6f ? "very cold night" : e.cold >= 0.3f ? "cold night" : "normal",
                    word = e.cold >= 0.6f ? "Freezing" : e.cold >= 0.3f ? "Cold" : "OK",
                    color = e.cold >= 0.6f ? Bad : e.cold >= 0.3f ? Cool : Good },

            };
            if (e.blackout) list.Add(new Reading { icon = "radio", name = "Radio", detail = "Sun is in the way", word = "No contact", color = Warn });
            if (e.softSand >= 0.3f) list.Add(new Reading { icon = "rover", name = "Rover", detail = "wheels dug in", word = "Stuck!", color = Warn });
            return list;
        }

        /// <summary>One short sentence: what the data means for today.</summary>
        public static string Headline(SolEnv e, HabitatState s)
        {
            if (e.tau >= 4) return "A big dust storm! Very little sun. Save power!";
            if (e.blackout) return "The Sun blocks the radio. You're on your own today.";
            if (e.cold >= 0.6f) return "A freezing night is coming. Keep the heater on!";
            if (e.tau >= 2 || e.sunlight < 0.6f) return "Dusty sky, less sun. Save some power.";
            if (e.cold >= 0.3f) return "A cold night is coming.";
            if (e.softSand >= 0.3f) return "The rover is stuck in soft sand.";
            return "A calm day on Mars. Good for exploring!";
        }

        static bool On(HabitatState s, string id) => s.IsOn(id);

        /// <summary>
        /// Scripted missions: the last two finished steps (ticked), the current step (with its button), and the next
        /// two (greyed). During the dust storm a red emergency row sits on top.
        /// </summary>
        static List<Job> StepJobs(MissionRunner r)
        {
            var s = r.State;
            var steps = r.Mission.steps;
            var jobs = new List<Job>();
            int cur = s.stepIndex;
            for (int i = Mathf.Max(0, cur - 1); i < Mathf.Min(steps.Length, cur + 3); i++)
            {
                var st = steps[i];
                int idx = i;
                var j = new Job
                {
                    key = "step" + i, icon = StepIcon(st), text = StepText(st, s, i == cur), why = st.why,
                    afterForecast = false, stepLocked = i > cur, red = st.red,
                    done = h => h.stepIndex > idx,
                };
                StepAction(st, s, j);
                jobs.Add(j);
            }
            if (s.stormActive)
                jobs.Insert(0, new Job
                {
                    key = "storm", icon = "alert", red = true, afterForecast = false,
                    text = "EMERGENCY: Dust storm! Get back to base",
                    why = "Leaving before all 3 ice = losing the ice. Getting caught = rescue and restart.",
                    target = "airlock", done = h => !h.stormActive,
                });
            return jobs;
        }

        static string StepText(StepDef st, HabitatState s, bool current)
        {
            if (!current) return st.text;
            switch (st.kind)
            {
                case "samples": return $"{st.text}  ({Mathf.Min(s.samples - s.stepBase, st.count)} of {st.count})";
                case "ice": return $"{st.text}  ({s.iceStep} of {st.count})";
                case "system": return st.text + (s.toggled.Contains(st.arg) && !s.IsOn(st.arg) ? "  (now switch it ON again)" : "  (OFF, then ON)");
                default: return st.text;
            }
        }

        static string StepIcon(StepDef st) => st.kind switch
        {
            "hab" => "hab", "forecast" => "satellite", "system" => PlaytestUI.SystemLook.TryGetValue(st.arg ?? "", out var l) ? l.icon : "box",
            "status" => "check", "suitUp" => "suit", "leaveAirlock" => "up", "clean" => "sun", "samples" => "rock",
            "repair" => "rover", "rover" => "rover", "ping" => "map", "ice" => "map", "returnBase" => st.red ? "alert" : "home",
            "lab" => "lab", "sleep" => "moon", "night" => "moon", _ => "box",
        };

        static void StepAction(StepDef st, HabitatState s, Job j)
        {
            switch (st.kind)
            {
                case "hab": j.action = "talk"; j.actionLabel = "Talk"; break;
                case "forecast": j.action = "forecast"; j.actionLabel = "Open"; break;
                case "system": j.target = "panel:" + st.arg; break;
                case "status": j.action = "checkStatus"; j.actionLabel = "Check"; break;
                case "suitUp": j.target = "suit"; j.action = "suit"; j.actionLabel = "Suit up"; break;
                case "leaveAirlock": j.target = "airlockOut"; break;
                case "clean": j.target = "solar"; j.action = "go:solar"; j.actionLabel = "Go"; break;
                case "samples": j.target = "samples"; j.action = "go:samples"; j.actionLabel = "Go"; break;
                case "repair": j.target = "rover"; j.action = "go:rover"; j.actionLabel = "Go"; break;
                case "rover": j.target = "rover"; j.action = "go:rover"; j.actionLabel = "Go"; break;
                case "ping": j.target = "ice"; break;
                case "ice": j.target = "ice"; break;
                case "returnBase": j.target = "airlock"; break;
                case "lab": j.target = "panel:science"; j.action = "scan"; j.actionLabel = "Scan"; break;
                case "sleep": j.target = "bunk"; j.action = "sleep"; j.actionLabel = "Sleep"; break;
            }
        }

        /// <summary>
        /// Today's jobs, in order: 1. check the NASA forecast, then safety jobs from the data, then the day's routine
        /// (rock samples, status report, ice map, rover repair, panels), then power/food advice.
        /// "End the day" is always the last step (a button).
        /// </summary>
        public static List<Job> Jobs(MissionRunner r)
        {
            if (r.HasSteps) return StepJobs(r);
            var s = r.State;
            var m = r.Mission;
            var e = Today(r);
            var cfg = r.Config;
            var jobs = new List<Job>();
            float trend = SimEngine.NetPowerPreview(s, e, cfg, r.Level);
            bool safeOut = e.tau < 4;
            var tasks = r.TodayTasks();

            // 1. The forecast (real NASA data where we have it)
            jobs.Add(new Job { key = "forecast", icon = "satellite", text = "Check the NASA forecast", why = "See today's Mars weather first.",
                action = "forecast", actionLabel = "Open", afterForecast = false, done = st => st.forecastSeen });

            // Safety first

            if (!On(s, SystemIds.LifeSupport))
                jobs.Add(new Job { key = "air", icon = "air", text = "Turn ON the Air maker", why = "It makes the air you breathe.",
                    target = "panel:" + SystemIds.LifeSupport, afterForecast = false, done = st => st.IsOn(SystemIds.LifeSupport) });
            if (e.cold >= 0.3f && !On(s, SystemIds.Heater))
                jobs.Add(new Job { key = "heater", icon = "heat", text = "Turn ON the Heater", why = "Cold slows the Air maker down.",
                    target = "panel:" + SystemIds.Heater, done = st => st.IsOn(SystemIds.Heater) });

            // The day's routine
            foreach (var t in tasks)
            {
                string word = t.Split(':')[0];
                int count = t.Contains(":") && int.TryParse(t.Split(':')[1], out var n) ? n : 1;
                switch (word)
                {
                    case "samples":
                        if (!safeOut) break;
                        jobs.Add(new Job { key = "samples", icon = "rock",
                            text = count == 1 ? $"Collect a rock sample  (+{HabitatState.ResearchPerSample} research)" : $"Collect {count} rock samples  (+{HabitatState.ResearchPerSample} each)",
                            why = $"Rocks here hold clues about water. Picking one up uses {cfg.balance.sampleOxygenCost * r.Level.drainMultiplier:0}% air.",
                            target = "samples", action = "go:samples", actionLabel = "Go", done = st => st.daySamples >= count });
                        break;
                    case "status":
                        if (e.blackout) break;
                        jobs.Add(new Job { key = "status", icon = "radio", text = "Send a status report to Earth",
                            why = On(s, SystemIds.Radio) ? $"Uses {MissionRunner.StatusPower * r.Level.drainMultiplier:0}% power. Earth needs to know you're OK." : "Turn ON the Radio first.",
                            target = "panel:" + SystemIds.Radio, action = "status", actionLabel = "Send", done = st => st.statusSent });
                        break;
                    case "ice":
                        if (s.iceMapWaiting)
                            jobs.Add(new Job { key = "ice", icon = "map", text = "Turn ON the Radio to get NASA's ice map", why = "Mission Control is trying to send it.",
                                target = "panel:" + SystemIds.Radio, optional = true, done = st => st.iceMapReceived });
                        else if (s.iceAccepted)
                            jobs.Add(new Job { key = "ice", icon = "map", text = "Dig up the ice  (optional, risky!)",
                                why = $"+{HabitatState.ResearchPerIce} research and more air tomorrow. Drilling uses {MissionRunner.IceDrillAir * r.Level.drainMultiplier:0}% air. Follow the blue marker.",
                                target = "ice", optional = true, done = st => st.iceDrilled });
                        else if (s.iceMapReceived)
                            jobs.Add(new Job { key = "ice", icon = "map", text = "Ice trip: you chose to stay safe", why = "That's OK. Safety first.",
                                optional = true, done = st => true });
                        break;
                    case "repair":
                        jobs.Add(new Job { key = "repair", icon = "rover", text = $"Repair Perseverance  (+{HabitatState.ResearchPerRepair} research)",
                            why = (s.roverFault ?? "It sent an error.") + $" The part uses {MissionRunner.RepairPower * r.Level.drainMultiplier:0}% power.",
                            target = "rover", action = safeOut ? "go:rover" : null, actionLabel = "Go", done = st => !st.roverBroken });
                        break;
                    case "weather":
                        if (!safeOut || !UnityEngine.Object.FindAnyObjectByType<WeatherStation>()) break;
                        jobs.Add(new Job { key = "weather", icon = "globe", text = $"Read the weather station  (+{HabitatState.ResearchPerWeather} research)",
                            why = "It's outside near the base. Your air drops while you walk.",
                            target = "weather", action = "go:weather", actionLabel = "Go", done = st => st.weatherRead });
                        break;
                    case "clean":
                        if (!safeOut || s.panelDust < 0.1f) break;
                        jobs.Add(new Job { key = "clean", icon = "sun", text = "Clean the dusty solar panels", why = "Dusty panels make less power.",
                            target = "solar", action = "go:solar", actionLabel = "Go", done = st => st.panelDust < 0.05f });
                        break;
                }
            }
            // Very dusty panels always get a job (even on days the file doesn't list it)
            if (!jobs.Exists(j => j.key == "clean") && safeOut && s.panelDust >= 0.5f)
                jobs.Add(new Job { key = "clean", icon = "sun", text = "Clean the dusty solar panels", why = "Dusty panels make less power.",
                    target = "solar", action = "go:solar", actionLabel = "Go", done = st => st.panelDust < 0.05f });

            // Power plan (from today's real sunlight): when the report says "save power", give a matching job.
            bool saveDay = e.tau >= 2 || e.sunlight < 0.6f;
            if (trend < 0 && (s.power + trend * 2 < 40 || saveDay))
            {
                bool needRadio = (tasks.Contains("status") && !s.statusSent) || s.iceMapWaiting || (m.goal != null && m.goal.type == "stayInContact");
                if (On(s, SystemIds.Science) && s.samplesStudied >= s.samples)
                    jobs.Add(new Job { key = "save", icon = "lab", text = "Turn OFF the Science lab", why = "Less sun today. Save power.",
                        target = "panel:" + SystemIds.Science, done = st => !st.IsOn(SystemIds.Science) });
                else if (On(s, SystemIds.Radio) && !needRadio)
                    jobs.Add(new Job { key = "save", icon = "radio", text = "Turn OFF the Radio", why = "Less sun today. Save power.",
                        target = "panel:" + SystemIds.Radio, done = st => !st.IsOn(SystemIds.Radio) });
                else if (On(s, SystemIds.Greenhouse) && s.food > 2)
                    jobs.Add(new Job { key = "save", icon = "plant", text = "Turn OFF the Plant lights", why = "Less sun today. Save power.",
                        target = "panel:" + SystemIds.Greenhouse, done = st => !st.IsOn(SystemIds.Greenhouse) });
            }

            // Radio rules
            if (e.blackout && On(s, SystemIds.Radio))
                jobs.Add(new Job { key = "radioOff", icon = "radio", text = "Turn OFF the Radio", why = "Nobody can hear us. It only uses power.",
                    target = "panel:" + SystemIds.Radio, done = st => !st.IsOn(SystemIds.Radio) });
            if (m.goal != null && m.goal.type == "stayInContact" && !e.blackout && !On(s, SystemIds.Radio))
                jobs.Add(new Job { key = "radioOn", icon = "radio", text = "Turn ON the Radio", why = "Earth can hear us again!",
                    target = "panel:" + SystemIds.Radio, done = st => st.IsOn(SystemIds.Radio) });

            // Food
            if (s.food <= 2 && !On(s, SystemIds.Greenhouse))
                jobs.Add(new Job { key = "food", icon = "plant", text = "Turn ON the Plant lights", why = "Food is running low.",
                    target = "panel:" + SystemIds.Greenhouse, done = st => st.IsOn(SystemIds.Greenhouse) });

            // Science lab: study the rocks you brought in
            if (s.samples > s.samplesStudied && !On(s, SystemIds.Science) && s.power > 40)
                jobs.Add(new Job { key = "lab", icon = "lab", text = $"Turn ON the Science lab  (+{HabitatState.ResearchPerStudy} research)", why = "It studies one rock each night.",
                    target = "panel:" + SystemIds.Science, done = st => st.IsOn(SystemIds.Science) });

            // Storm or radiation day: staying in is a job too
            if (!safeOut)
                jobs.Add(new Job { key = "stayIn", icon = "home", text = "Stay inside today", why = "The dust storm is too strong to go out.",
                    target = "airlock", action = s.outside ? "inside" : null, actionLabel = "Go", afterForecast = false, done = st => !st.outside });

            if (jobs.Count > 7) jobs.RemoveRange(7, jobs.Count - 7);
            return jobs;
        }
    }
}