using System;

namespace MissionTrainer
{
    /// <summary>One difficulty level. Applied on top of any mission; mission files never mention difficulty.</summary>
    [Serializable]
    public class DifficultyLevel
    {
        public string id;              // cadet | pilot | commander
        public string label;
        public float startReserveBonus; // added to starting power and oxygen
        public float drainMultiplier;   // scales power and oxygen use
        public bool hintsOn;            // HAB marks the safest option
        public float timerSeconds;      // 0 = no timer (TODO: card timers not built yet)
    }

    /// <summary>Tuning numbers for SimEngine. All per sol, in meter percent.</summary>
    [Serializable]
    public class BalanceSettings
    {
        public float solarPerSol;        // battery % gained on a clear sol (sunlight = 1)
        public float oxygenMadePerSol;   // by life support
        public float oxygenUsedPerSol;   // by the astronaut
        public float baseDosePerSol;     // radiation dose % with radiation = 1 and no shield
        public int foodFromGreenhouse;   // food added per sol with lights on
        public float evaOxygenPerSol;    // extra oxygen used if a sol ends while you are outside
        public float shelterShield;      // fraction of radiation blocked inside the shelter
        public float sampleOxygenCost;   // oxygen used to pick up one rock sample
        public float sampleDose;         // dose per sample (x radiation level)
        public float cleanOxygenCost;    // oxygen used to clean the solar panels on an EVA
    }

    /// <summary>Contents of Resources/Data/difficulty.json.</summary>
    [Serializable]
    public class DifficultyConfig
    {
        public BalanceSettings balance;
        public SystemDef[] systems;
        public DifficultyLevel[] levels;

        public static DifficultyConfig Load() => ContentDb.LoadJson<DifficultyConfig>("Data/difficulty");

        public DifficultyLevel Level(string id)
        {
            foreach (var l in levels) if (l.id == id) return l;
            return levels[0];
        }

        public SystemDef System(string id)
        {
            foreach (var s in systems) if (s.id == id) return s;
            return null;
        }
    }
}
