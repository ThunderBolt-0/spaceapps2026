using UnityEngine;

namespace MissionTrainer
{
    /// <summary>
    /// The rules. Pure functions on HabitatState, shared by Story and Freeplay.
    /// env.sunlight is the fraction of clear-sky power the panels get (dust already included).
    /// </summary>
    public static class SimEngine
    {
        public static void AdvanceSol(HabitatState s, SolEnv env, DifficultyConfig cfg, DifficultyLevel lvl)
        {
            var b = cfg.balance;
            s.sol++;

            // Power: sun charges (less through dusty panels), systems drain.
            s.power += b.solarPerSol * env.sunlight * PanelFactor(s);
            s.panelDust = Mathf.Clamp01(s.panelDust + 0.02f + 0.02f * env.tau);   // dustier skies settle more dust
            s.power -= PowerDrain(s, cfg) * lvl.drainMultiplier;
            if (s.power <= 0)
            {
                s.power = 0;
                s.systemsOn.Clear(); // brownout: everything shuts off
            }

            // Oxygen: life support makes it (slower when cold and heater is off), astronaut uses it.
            if (s.IsOn(SystemIds.LifeSupport))
            {
                float made = b.oxygenMadePerSol;
                if (!s.IsOn(SystemIds.Heater)) made *= 1f - Mathf.Clamp01(env.cold);
                s.oxygen += made;
            }
            s.oxygen -= b.oxygenUsedPerSol * lvl.drainMultiplier;
            if (s.outside) s.oxygen -= b.evaOxygenPerSol * lvl.drainMultiplier;

            // Food: lights grow some, astronaut eats 1.
            if (s.IsOn(SystemIds.Greenhouse)) s.food += b.foodFromGreenhouse;
            s.food -= 1;

            // (Radiation was removed from the game: the meters are battery, air and food.)

            if (s.IsOn(SystemIds.Science)) s.scienceSols++;

            s.ClampAll();
        }

        public static float EffectiveShield(HabitatState s, DifficultyConfig cfg) =>
            s.inShelter ? Mathf.Max(s.shield, cfg.balance.shelterShield) : s.shield;

        /// <summary>Predicted oxygen change next sol, for the HUD.</summary>
        public static float NetOxygenPreview(HabitatState s, SolEnv env, DifficultyConfig cfg, DifficultyLevel lvl)
        {
            var b = cfg.balance;
            float made = 0;
            if (s.IsOn(SystemIds.LifeSupport))
                made = b.oxygenMadePerSol * (s.IsOn(SystemIds.Heater) ? 1f : 1f - Mathf.Clamp01(env.cold));
            float used = b.oxygenUsedPerSol + (s.outside ? b.evaOxygenPerSol : 0);
            return made - used * lvl.drainMultiplier;
        }

        public static float PowerDrain(HabitatState s, DifficultyConfig cfg)
        {
            float drain = 0;
            foreach (var id in s.systemsOn)
            {
                var def = cfg.System(id);
                if (def != null) drain += def.powerPerSol;
            }
            return drain;
        }

        /// <summary>Predicted battery change next sol, for the HUD arrow.</summary>
        public static float NetPowerPreview(HabitatState s, SolEnv env, DifficultyConfig cfg, DifficultyLevel lvl) =>
            cfg.balance.solarPerSol * env.sunlight * PanelFactor(s) - PowerDrain(s, cfg) * lvl.drainMultiplier;

        /// <summary>Fully dusty panels make half power.</summary>
        public static float PanelFactor(HabitatState s) => 1f - 0.5f * Mathf.Clamp01(s.panelDust);

        public static void ApplyOption(HabitatState s, Option o)
        {
            if (o.cost != null)
            {
                s.power -= o.cost.power;
                s.oxygen -= o.cost.oxygen;
                s.food -= o.cost.food;
            }
            var e = o.effects;
            if (e != null)
            {
                if (e.turnOff != null) foreach (var id in e.turnOff) s.SetSystem(id, false);
                if (e.turnOn != null) foreach (var id in e.turnOn) s.SetSystem(id, true);
                s.power += e.power;
                s.oxygen += e.oxygen;
                s.dose += e.dose;
                s.shield += e.shield;
                s.food += e.food;
                s.samples += e.samples;
                s.panelDust = Mathf.Clamp01(s.panelDust + e.panelDust);
            }
            s.ClampAll();
        }
    }
}
