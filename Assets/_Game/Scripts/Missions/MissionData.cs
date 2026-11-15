using System;
using System.Collections.Generic;

namespace MissionTrainer
{
    // Mirrors Resources/Missions/*.json. Story chapters and FreeplayGenerator both produce this.
    // JsonUtility rules: public fields only, no dictionaries, no inheritance.
    // Write every number you care about in the JSON (missing numbers become 0).

    [Serializable]
    public class MissionData
    {
        public string id;
        public string mode;          // "story" | "freeplay"
        public int chapter;          // 0 = training, 1-5 = chapters, -1 = freeplay
        public string title;
        public string site;
        public string[] intro;       // HAB lines; [[key]] or [[key|shown word]] = glossary chip
        public StartState start;
        public Goal goal;
        public string goalText;      // optional: plain sentence shown as the goal (otherwise made from 'goal')
        public Condition[] abort;
        public SolEnv[] sols;        // environment per sol; a missing sol repeats the previous one
        public MissionEvent[] events;
        public Card[] cards;
        public Debrief debrief;
        public DayPlan[] days;       // optional: each day's tasks (missing days get sensible defaults)
        public StepDef[] steps;      // optional: a scripted story, done strictly in order (First Sols, Dust Storm)
        public string forecastNote;  // optional extra line in the NASA forecast (e.g. "a dust storm is growing")

        /// <summary>The plan for one day, or null if the file doesn't list that day.</summary>
        public DayPlan DayFor(int sol)
        {
            if (days != null) foreach (var d in days) if (d.sol == sol) return d;
            return null;
        }

        public Card FindCard(string cardId)
        {
            if (cards != null) foreach (var c in cards) if (c.id == cardId) return c;
            return null;
        }

        public SolEnv EnvForSol(int sol)
        {
            SolEnv best = null;
            if (sols != null)
                foreach (var e in sols)
                    if (e.sol <= sol && (best == null || e.sol > best.sol)) best = e;
            return best ?? new SolEnv { sol = sol, sunlight = 1, radiation = 1, source = Source.Simulated("Default sky") };
        }
    }

    /// <summary>
    /// One day's routine. Task words: "forecast", "samples:2", "status", "ice", "repair", "clean".
    /// "forecast" is always added first. Safety jobs (shelter, heater...) are added by the planner from the data.
    /// </summary>
    [Serializable]
    public class DayPlan
    {
        public int sol;
        public string[] tasks;
        public string roverFault;   // with "repair": what broke, in kid words
    }

    /// <summary>
    /// One step of a scripted mission. kind: hab | forecast | system (arg = system id) | status | suitUp | leaveAirlock |
    /// clean | samples (count) | repair (arg = what broke) | rover | ping | ice (count; arg "storm" = dust storm hits) |
    /// returnBase | lab | sleep | night
    /// </summary>
    [Serializable]
    public class StepDef
    {
        public string kind, arg;
        public int count;
        public string text, why;     // shown in the mission list
        public string hab;           // HAB explains the step when it starts
        public bool red;             // emergency look
    }

    [Serializable]
    public class StartState
    {
        public float power, oxygen, dose, shield;
        public int food;
        public string[] systemsOn;
    }

    [Serializable]
    public class Goal
    {
        public string type;   // surviveSols | collectSamples | reachShelter | stayInContact | research (points, by maxSols)
        public int target;    // sols, samples, or shield % depending on type
        public int maxSols;   // mission ends here; goal not met = safe abort
    }

    [Serializable]
    public class Condition
    {
        public string meter;   // see HabitatState.GetMeter
        public string op;      // "<=" | ">=" | "<" | ">"
        public float value;
        public string message; // shown in the debrief: what happened, kindly

        public bool IsMet(HabitatState s)
        {
            float v = s.GetMeter(meter);
            switch (op)
            {
                case "<=": return v <= value;
                case ">=": return v >= value;
                case "<": return v < value;
                case ">": return v > value;
                default: return false;
            }
        }
    }

    [Serializable]
    public class SolEnv
    {
        public int sol;
        public float sunlight;   // 0..1 of clear-sky panel power (468 Wh = 1.0 for Opportunity)
        public float tau;        // dust in the sky (shown to the player)
        public float radiation;  // 1 = normal Mars surface
        public float cold;       // 0..1, slows life support if heater is off; also night/frost in 3D
        public float softSand;   // 0..1, how deep the rover is stuck (3D only)
        public bool blackout;    // no contact with Earth (solar conjunction): "Ask Earth" options are locked
        public Source source;
    }

    /// <summary>Honest labelling: every figure is Real (Live / Archived + date) or Simulated.</summary>
    [Serializable]
    public class Source
    {
        public string kind;   // "live" | "archived" | "simulated"
        public string label;
        public string date;

        public string BadgeText()
        {
            switch (kind)
            {
                case "live": return "Real · Live";
                case "archived": return "Real · Archived " + date;
                default: return "Simulated";
            }
        }

        public static Source Simulated(string label) => new Source { kind = "simulated", label = label };
        public static Source Archived(string label, string date) => new Source { kind = "archived", label = label, date = date };
    }

    [Serializable]
    public class MissionEvent
    {
        public int sol;       // 0 = right at the start
        public string mc;     // optional Mission Control message: only heard if the Radio is on (and not in a conjunction)
        public string hab;    // optional HAB line
        public string card;   // optional card id
    }

    [Serializable]
    public class Card
    {
        public string id;
        public string prompt;
        public string icon;
        public Option[] options;
    }

    [Serializable]
    public class Option
    {
        public string label;
        public string icon;
        public string hab;          // HAB reply after choosing
        public bool needsRadio;     // greyed out when the radio is off
        public Cost cost;
        public Effects effects;
        public string[] tags;       // "priority" (safest systems choice), "procedure" (used a procedure card)
        public string action;       // optional engine action: "iceGo" = accept today's ice map
        public string note;         // optional extra cost line (costs that happen later, e.g. while drilling)

        public bool HasTag(string t) => tags != null && Array.IndexOf(tags, t) >= 0;

        /// <summary>The cost line shown on the card BEFORE the player picks.</summary>
        public string Describe(DifficultyConfig cfg)
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(note)) parts.Add(note);
            if (cost != null)
            {
                if (cost.power > 0) parts.Add($"Power -{cost.power:0}%");
                if (cost.oxygen > 0) parts.Add($"Oxygen -{cost.oxygen:0}%");
                if (cost.food > 0) parts.Add($"Food -{cost.food}");
                if (cost.sols > 0) parts.Add($"Wait {cost.sols} sol" + (cost.sols > 1 ? "s" : ""));
                if (cost.credits > 0) parts.Add($"{cost.credits} credits");
            }
            if (effects != null && effects.turnOff != null)
                foreach (var id in effects.turnOff)
                    parts.Add("Turns off " + (cfg?.System(id)?.label ?? id));
            if (effects != null && effects.turnOn != null)
                foreach (var id in effects.turnOn)
                {
                    var def = cfg?.System(id);
                    parts.Add(def != null ? $"Turns on {def.label} (-{def.powerPerSol:0}% power per sol)" : "Turns on " + id);
                }
            return parts.Count == 0 ? "No supplies used" : string.Join(" · ", parts);
        }
    }

    [Serializable]
    public class Cost
    {
        public float power, oxygen;
        public int food, sols, credits;
    }

    [Serializable]
    public class Effects
    {
        public string[] turnOn, turnOff;
        public float power, oxygen, dose, shield;
        public float panelDust;     // negative = wind cleans the panels
        public int food, samples;
    }

    [Serializable]
    public class Debrief
    {
        public string realTeam;     // What the real team did
        public string tryNext;      // What to try next time
        public RealScience realScience;
        public string[] sources;    // keys into Resources/Data/sources.json
    }

    [Serializable]
    public class RealScience
    {
        public string title, text, glossary;
    }

    /// <summary>Result handed to the debrief screen.</summary>
    public class MissionResult
    {
        public bool success;
        public string reason;
        public int stars;
        public ScoreBreakdown breakdown;
        public int creditsEarned;
        public bool firstCompletion;   // story: this stage was just completed for the first time
        public List<string> newUnlocks = new List<string>();
        public HabitatState finalState;
    }
}
