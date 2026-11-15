using System;
using System.Collections.Generic;
using UnityEngine;

namespace MissionTrainer
{
    /// <summary>Everything that changes during a mission. Plain data, no Unity objects.</summary>
    [Serializable]
    public class HabitatState
    {
        // The 3 meters (0-100) + extras
        public float power;
        public float oxygen;
        public float dose;     // radiation dose meter, 100 = safe limit reached
        public float shield;   // 0..0.9, fraction of radiation blocked
        public int food;
        public int sol;
        public int samples;
        public List<string> systemsOn = new List<string>();

        // Where the astronaut is (set by the 3D world; both false = inside the habitat)
        public bool outside;     // on EVA: uses extra oxygen each sol
        public bool inShelter;   // radiation shelter: blocks most radiation

        public float panelDust;      // 0..1 dust on the solar panels (builds up every sol, cleaned on EVA)
        public int samplesStudied;   // samples the Science lab has studied
        public int research;         // research points: earned by collecting rocks and studying them in the lab

        public const int ResearchPerSample = 10, ResearchPerStudy = 15, ResearchPerIce = 40, ResearchPerRepair = 10, ResearchPerWeather = 5;

        // Today's routine (reset every morning)
        public bool forecastSeen, statusSent, weatherRead;
        public int daySamples;
        public bool iceMapWaiting, iceMapReceived, iceAccepted, iceDrilled;
        public bool roverBroken;
        public string roverFault;
        public int iceWater;     // ice brought home: the base turns it into oxygen next morning
        public int iceTotal;

        // Wearing the spacesuit (needed to go outside; taken off to sleep)
        public bool suitOn;

        // Scripted missions (steps done in order)
        public int stepIndex, stepBase;
        public List<string> toggled = new List<string>();
        public bool habTalked, statusChecked, roverVisited, atPing, labScanned;
        public int iceMask, iceStep;      // which ice deposits are drilled / how many in this step
        public bool roverAtPing;          // Perseverance drove you to the pinged location
        // Dust storm emergency
        public bool stormActive, stormOver, iceLost;
        public float stormTime;
        // Night with no sun: battery drains live until sunrise
        public bool nightActive, nightOver;
        public float nightTime;

        public void NewDay()
        {
            forecastSeen = statusSent = weatherRead = false;
            daySamples = 0;
            iceMapWaiting = iceMapReceived = iceAccepted = iceDrilled = false;
        }

        // Tracked for scoring and debrief
        public float minPower = 100, minOxygen = 100, peakDose;
        public int priorityOffered, priorityPicked;
        public int procedureOffered, procedurePicked;
        public int scienceSols;
        public List<string> choiceLog = new List<string>();

        public bool IsOn(string id) => systemsOn.Contains(id);

        public void SetSystem(string id, bool on)
        {
            if (!on && !toggled.Contains(id)) toggled.Add(id);   // counts once it has been switched off
            if (on && !IsOn(id)) systemsOn.Add(id);
            if (!on) systemsOn.Remove(id);
        }

        public static HabitatState From(StartState s, DifficultyLevel level)
        {
            var st = new HabitatState
            {
                power = s.power + level.startReserveBonus,
                oxygen = s.oxygen + level.startReserveBonus,
                dose = s.dose,
                shield = s.shield,
                food = s.food,
                systemsOn = new List<string>(s.systemsOn ?? new string[0]),
            };
            st.minPower = 100; st.minOxygen = 100;
            st.ClampAll();
            return st;
        }

        public void ClampAll()
        {
            power = Mathf.Clamp(power, 0, 100);
            oxygen = Mathf.Clamp(oxygen, 0, 100);
            dose = Mathf.Clamp(dose, 0, 100);
            shield = Mathf.Clamp(shield, 0, 0.9f);
            food = Mathf.Max(0, food);
            minPower = Mathf.Min(minPower, power);
            minOxygen = Mathf.Min(minOxygen, oxygen);
            peakDose = Mathf.Max(peakDose, dose);
        }

        /// <summary>Lets mission JSON conditions name a meter: "power", "oxygen", "dose", "food", "samples", "sol".</summary>
        public float GetMeter(string meter)
        {
            switch (meter)
            {
                case "power": return power;
                case "oxygen": return oxygen;
                case "dose": return dose;
                case "food": return food;
                case "samples": return samples;
                case "research": return research;
                case "sol": return sol;
                default: Debug.LogWarning("Unknown meter: " + meter); return 0;
            }
        }
    }
}
