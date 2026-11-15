using System;
using System.Collections.Generic;
using UnityEngine;

namespace MissionTrainer
{
    [Serializable]
    public class GoalTemplate
    {
        public string id, title;
        public string[] intro;
        public Goal goal;
        public Card evaCard;       // optional repeating card (e.g. rock sample EVA)
        public int evaEverySols;   // 0 = never
        public string tryNext;
    }

    [Serializable]
    public class HazardTemplate
    {
        public string id, title;
        public string kind;           // dust | cold | solarEvent | silence
        public int startMin, startMax, duration;
        // 0 = leave unchanged
        public float sunlight, tau, radiation, cold;
        public string hab;
        public Card card;
    }

    [Serializable] public class GoalTemplateList { public GoalTemplate[] goals; }
    [Serializable] public class HazardTemplateList { public HazardTemplate[] hazards; }

    /// <summary>Builds a MissionData (same format as Story chapters) from 1 goal + 1-2 hazards.</summary>
    public static class FreeplayGenerator
    {
        public static MissionData Generate(int seed)
        {
            var rng = new System.Random(seed);
            var goals = ContentDb.LoadJson<GoalTemplateList>("Freeplay/goals").goals;
            var hazards = ContentDb.LoadJson<HazardTemplateList>("Freeplay/hazards").hazards;

            var g = goals[rng.Next(goals.Length)];
            int length = Mathf.Max(g.goal.maxSols, 5);

            var sols = new SolEnv[length];
            for (int i = 0; i < length; i++)
                sols[i] = new SolEnv { sol = i + 1, sunlight = 1, tau = 0.5f, radiation = 1, source = Source.Simulated("Generated sky") };

            var events = new List<MissionEvent>();
            var cards = new List<Card>();
            var titles = new List<string> { g.title };

            // 1 or 2 different hazards
            var pool = new List<HazardTemplate>(hazards);
            pool.RemoveAll(h => h.kind == "solarEvent");   // radiation was removed from the game
            int count = Mathf.Min(rng.Next(1, 3), pool.Count);
            for (int n = 0; n < count; n++)
            {
                var h = pool[rng.Next(pool.Count)];
                pool.Remove(h);
                titles.Add(h.title);

                int start = Mathf.Clamp(rng.Next(h.startMin, h.startMax + 1), 1, length);
                float radiation = h.radiation;
                Source src = Source.Simulated(h.title);

                if (h.kind == "solarEvent")
                {
                    var ev = DonkiService.PickSolarEvent(rng);
                    if (ev != null)
                    {
                        radiation = ev.marsRadiationMultiplier;
                        src = Source.Archived($"DONKI {ev.type} {ev.classType}", ev.date);
                    }
                }

                for (int k = start; k < start + Mathf.Max(1, h.duration) && k <= length; k++)
                {
                    var e = sols[k - 1];
                    if (h.sunlight > 0) e.sunlight = Mathf.Min(e.sunlight, h.sunlight);
                    if (h.tau > 0) e.tau = Mathf.Max(e.tau, h.tau);
                    if (h.cold > 0) e.cold = Mathf.Max(e.cold, h.cold);
                    if (radiation > 0) e.radiation = Mathf.Max(e.radiation, radiation);
                    e.source = src;
                }

                if (h.card != null)
                {
                    var c = Clone(h.card);
                    c.id = h.id + "_" + n;
                    cards.Add(c);
                    events.Add(new MissionEvent { sol = start, hab = h.hab, card = c.id });
                }
            }

            if (g.evaCard != null && g.evaEverySols > 0)
            {
                var eva = Clone(g.evaCard);
                eva.id = "eva";
                cards.Add(eva);
                for (int s = g.evaEverySols; s <= length; s += g.evaEverySols)
                    events.Add(new MissionEvent { sol = s, card = eva.id });
            }

            // Daily routine: rocks most days, a status report every other day, one ice map and one rover fault per mission.
            var days = new List<DayPlan>();
            int iceDay = rng.Next(2, length + 1), repairDay = rng.Next(2, length + 1);
            string[] faults = { "A wheel motor stopped working.", "Its camera is covered in dust.", "Its computer keeps restarting.", "A cable came loose in the arm." };
            for (int d = 1; d <= length; d++)
            {
                var e = sols[d - 1];
                bool safe = e.radiation < 1.5f && e.tau < 4;
                var t = new List<string> { "forecast" };
                if (safe) t.Add(rng.Next(3) == 0 ? "samples:3" : "samples:2");
                t.Add("status");
                if (d == iceDay && safe) t.Add("ice");
                else if (safe) t.Add("weather");
                if (d == repairDay) t.Add("repair");
                t.Add("clean");
                days.Add(new DayPlan { sol = d, tasks = t.ToArray(), roverFault = faults[rng.Next(faults.Length)] });
            }

            return new MissionData
            {
                days = days.ToArray(),
                id = "freeplay_" + seed,
                mode = "freeplay",
                chapter = -1,
                title = string.Join(" + ", titles),
                site = "Meridiani Planum",
                intro = g.intro,
                start = StartWithUpgrades(),
                goal = g.goal,
                abort = new[]
                {
                    new Condition { meter = "power", op = "<=", value = 0, message = "The battery ran empty. HAB did a safe abort." },
                    new Condition { meter = "oxygen", op = "<=", value = 10, message = "Oxygen got low. HAB did a safe abort to keep you safe." },
                },
                sols = sols,
                events = events.ToArray(),
                cards = cards.ToArray(),
                debrief = new Debrief
                {
                    realTeam = "Real Mars teams plan for surprises like these with procedure cards and spare power.",
                    tryNext = g.tryNext,
                    sources = new[] { "donki" },
                },
            };
        }

        static StartState StartWithUpgrades()
        {
            var s = new StartState
            {
                power = 80, oxygen = 80, food = 5,
                systemsOn = new[] { SystemIds.LifeSupport, SystemIds.Heater, SystemIds.Radio },
            };
            foreach (var item in ContentDb.ShopItems)
            {
                if (!SaveSystem.HasUpgrade(item.id)) continue;
                switch (item.stat)
                {
                    case "startPower": s.power += item.amount; break;
                    case "startOxygen": s.oxygen += item.amount; break;
                    case "startFood": s.food += Mathf.RoundToInt(item.amount); break;
                    case "shield": s.shield += item.amount; break;
                }
            }
            return s;
        }

        static Card Clone(Card c) => JsonUtility.FromJson<Card>(JsonUtility.ToJson(c));
    }
}