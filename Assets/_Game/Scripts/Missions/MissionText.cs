using System.Collections.Generic;
using UnityEngine;

namespace MissionTrainer
{
    /// <summary>
    /// Plain-language text made from mission data, so every mission (story or freeplay) explains itself:
    /// the goal, progress, what hazards to expect, and a "what should I do now?" hint.
    /// </summary>
    public static class MissionText
    {
        public struct Hazard
        {
            public string name, text;
            public Color color;
        }

        public static string Goal(MissionData m)
        {
            if (!string.IsNullOrEmpty(m.goalText)) return m.goalText;
            var g = m.goal;
            if (g == null) return "Stay safe.";
            switch (g.type)
            {
                case "surviveSols": return $"Keep the habitat running until sol {g.target}.";
                case "collectSamples": return $"Collect {g.target} rock samples and stay safe until sol {g.maxSols}.";
                case "reachShelter": return "Get to the radiation shelter and stay safe.";
                case "stayInContact": return $"Keep the radio working until sol {g.target}.";
                case "research": return $"Earn {g.target} research points by sol {g.maxSols}.";
                default: return "Stay safe.";
            }
        }

        public static string Progress(MissionData m, HabitatState s)
        {
            var g = m.goal;
            if (g == null) return $"Sol {s.sol}";
            switch (g.type)
            {
                case "collectSamples": return $"Samples {s.samples} of {g.target}   ·   Sol {s.sol} of {g.maxSols}";
                case "stayInContact": return $"Sol {s.sol} of {g.target}   ·   Radio {(s.IsOn(SystemIds.Radio) ? "on" : "OFF")}";
                case "research": return $"Research {s.research} of {g.target}   ·   Sol {s.sol} of {g.maxSols}";
                default: return $"Sol {s.sol} of {Mathf.Max(g.target, g.maxSols)}";
            }
        }

        public static List<Hazard> Hazards(MissionData m)
        {
            bool dust = false, cold = false, storm = false, sand = false, silent = false;
            if (m.sols != null)
                foreach (var e in m.sols)
                {
                    dust |= e.tau >= 2f;
                    cold |= e.cold >= 0.3f;
                    sand |= e.softSand >= 0.3f;
                    silent |= e.blackout;
                }
            var list = new List<Hazard>();
            if (silent) list.Add(new Hazard { name = "No contact", text = "Earth can't hear you. Use procedure cards.", color = new Color(0.4f, 0.85f, 0.85f) });
            if (dust) list.Add(new Hazard { name = "Dust storm", text = "Less sunlight = less power. Save power early.", color = new Color(0.93f, 0.6f, 0.3f) });
            if (cold) list.Add(new Hazard { name = "Cold night", text = "Keep the heater on, or life support slows down.", color = new Color(0.55f, 0.75f, 1f) });
            if (sand) list.Add(new Hazard { name = "Soft sand", text = "The rover can get stuck. Go slowly.", color = new Color(0.9f, 0.78f, 0.5f) });
            if (list.Count == 0) list.Add(new Hazard { name = "Calm sky", text = "A good time to explore and collect samples.", color = new Color(0.5f, 0.85f, 0.6f) });
            return list;
        }

        /// <summary>The single most useful next step right now (null if nothing to say).</summary>
        /// <summary>
        /// The most useful next step, plus where in the world it happens (target: "shelter", "airlock", "bunk",
        /// "solar", "samples" or "panel:&lt;systemId&gt;"; null = nowhere specific). The HUD points a marker at it.
        /// </summary>
        public static string Hint(MissionRunner r, out string target)
        {
            string where = null;
            var text = HintCore(r, w => where = w);
            target = where;
            return text;
        }

        static string HintCore(MissionRunner r, System.Action<string> setTarget)
        {
            var s = r.State;
            if (s == null || r.IsOver || r.ActiveCard != null) return null;
            var m = r.Mission;
            var env = m.EnvForSol(Mathf.Max(1, s.sol));
            var next = m.EnvForSol(s.sol + 1);
            float pTrend = SimEngine.NetPowerPreview(s, next, r.Config, r.Level);

            string Go(string where, string text) { setTarget(where); return text; }

            if (env.radiation >= 1.5f && !s.inShelter)
                return Go("shelter", "Solar storm! Go out through the airlock to the radiation shelter (yellow sign) and wait there.");
            if (env.radiation >= 1.5f && s.inShelter)
                return "You're safe in the shelter. Press N to wait out the storm.";
            bool callGoal = m.goal != null && m.goal.type == "stayInContact";
            if (callGoal && !s.IsOn(SystemIds.Radio) && !next.blackout && s.sol + 1 >= m.goal.target)
                return Go("panel:" + SystemIds.Radio, "Contact comes back next sol! Turn the Radio ON so Earth can hear you.");
            if (env.blackout && s.IsOn(SystemIds.Radio))
                return Go("panel:" + SystemIds.Radio, "Earth can't hear us during the conjunction. The Radio is only using power: turn it off for now.");
            if (!s.IsOn(SystemIds.LifeSupport))
                return Go("panel:" + SystemIds.LifeSupport, "Life support is OFF. Turn it back on at its console, or the oxygen will run out.");
            if (s.outside && s.oxygen < 40)
                return Go("airlock", "Oxygen is getting low. Go back inside the habitat.");
            if (pTrend < 0 && s.power + pTrend * 2 < 30)
                return s.IsOn(SystemIds.Science) ? Go("panel:" + SystemIds.Science, "Power is running low. Turn off the Science lab at its console.")
                     : s.IsOn(SystemIds.Radio) ? Go("panel:" + SystemIds.Radio, "Power is running low. Turn off the Radio to save power.")
                     : "Power is running low. Turn off anything you don't need.";
            if (s.food <= 2 && !s.IsOn(SystemIds.Greenhouse))
                return Go("panel:" + SystemIds.Greenhouse, "Food is low. Turn on the Greenhouse lights.");
            if (env.cold >= 0.3f && !s.IsOn(SystemIds.Heater))
                return Go("panel:" + SystemIds.Heater, "It's freezing. Turn the Heater on so life support keeps working.");
            if (s.outside && env.tau >= 4)
                return Go("airlock", "Dust storm outside! Head back inside.");
            if (s.panelDust >= 0.4f && env.tau < 4)
                return Go("solar", $"The solar panels are {s.panelDust * 100:0}% dusty, so they make less power. Go outside and press E at the panels to clean them.");
            if (s.samples > s.samplesStudied && !s.IsOn(SystemIds.Science) && s.power > 45)
                return Go("panel:" + SystemIds.Science, "You have a rock sample! Turn on the Science lab to study it.");
            if (s.IsOn(SystemIds.Science) && s.samplesStudied >= s.samples && pTrend < 0)
                return Go("panel:" + SystemIds.Science, "The Science lab has nothing to study. Switch it off to save power, or go find a rock sample.");
            if (m.goal != null && m.goal.type == "collectSamples" && s.samples < m.goal.target)
                return Go("samples", "Go outside and find the blue light beams: those mark rock samples. Press E to collect.");
            return Go("bunk", "All good. Sleep in the bunk or press N to go to the next sol.");
        }

        public static string Hint(MissionRunner r) => Hint(r, out _);
    }
}
