using System;
using UnityEngine;

namespace MissionTrainer
{
    [Serializable]
    public class ScoreBreakdown
    {
        public int safety;     // max 40: lowest power/oxygen reached
        public int priority;   // max 25: kept the right systems on
        public int procedure;  // max 15: used procedure cards
        public int shielding;  // max 10: safe trips (lowest air stayed high)
        public int science;    // max 10: samples and lab time

        public int Total => safety + priority + procedure + shielding + science;
    }

    public static class ScoreCalculator
    {
        public static ScoreBreakdown Score(HabitatState s) => new ScoreBreakdown
        {
            safety = Mathf.RoundToInt(40f * Mathf.Clamp01(Mathf.Min(s.minPower, s.minOxygen) / 50f)),
            priority = Ratio(25, s.priorityPicked, s.priorityOffered),
            procedure = Ratio(15, s.procedurePicked, s.procedureOffered),
            shielding = Mathf.RoundToInt(10f * Mathf.Clamp01(s.minOxygen / 40f)),   // safe trips: came back with air to spare
            // Science = research points (rock +10, lab study +15). 50 points = full marks.
            science = Mathf.RoundToInt(10f * Mathf.Clamp01(s.research / 50f)),
        };

        /// <summary>Never zero stars: a safe abort still earns 1.</summary>
        public static int Stars(ScoreBreakdown b, bool success)
        {
            if (!success) return 1;
            if (b.Total >= 80) return 3;
            if (b.Total >= 50) return 2;
            return 1;
        }

        static int Ratio(int max, int picked, int offered) =>
            offered == 0 ? max : Mathf.RoundToInt(max * (float)picked / offered);
    }
}
