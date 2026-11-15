using System;
using System.Collections.Generic;
using UnityEngine;

namespace MissionTrainer
{
    /// <summary>
    /// The one shared engine for Story and Freeplay. Put it in the Habitat scene.
    /// UI listens to the events and calls NextSol / SetSystem / Choose.
    /// </summary>
    public class MissionRunner : MonoBehaviour
    {
        [Tooltip("Played when you press Play directly in the Habitat scene.")]
        public string fallbackMissionId = "ch1_storm";

        [Tooltip("Start a mission by itself on Play. GameFlow turns this off and starts missions from the menus.")]
        public bool autoStart = true;

        public event Action MissionStarted;   // also fires on Restart: 3D props reset themselves
        public event Action<HabitatState> StateChanged;
        public event Action<string> HabSays;
        public event Action<Card> CardShown;
        public event Action<MissionResult> MissionEnded;
        public event Action<int> ResearchEarned;   // +points popup in the HUD
        public event Action<RockInfo, bool> RockFound;   // rock + is it new to the collection
        public event Action<int> DayStarted;       // new rocks appear, the ice site hides, the rover may break

        /// <summary>Set by the UI while a pop-up is open: the live air clock stops while you read.</summary>
        public bool Paused { get; set; }

        /// <summary>Air used every second while outside (before the difficulty multiplier): 0.5% per second.</summary>
        public const float LiveAirPerSecond = 0.5f;
        public const float IceDrillAir = 15f, StatusPower = 3f, RepairPower = 4f, IceWaterToAir = 12f;

        public MissionData Mission { get; private set; }
        public HabitatState State { get; private set; }
        public Card ActiveCard { get; private set; }
        public bool IsOver { get; private set; }
        public DifficultyConfig Config => cfg;
        public DifficultyLevel Level => lvl;

        DifficultyConfig cfg;
        DifficultyLevel lvl;
        readonly Queue<Card> pending = new Queue<Card>();

        void Start()
        {
            if (!autoStart) return;
            var gm = GameManager.Instance;
            if (gm.CurrentMission == null) gm.CurrentMission = MissionLoader.Load(fallbackMissionId);
            Begin(gm.CurrentMission, gm.Config, gm.Level);
        }

        public void Begin(MissionData mission, DifficultyConfig config, DifficultyLevel level)
        {
            Mission = mission; cfg = config; lvl = level;
            if (mission.sols != null) foreach (var e in mission.sols) e.radiation = 1;   // radiation was removed from the game
            State = HabitatState.From(mission.start, level);
            IsOver = false; ActiveCard = null; pending.Clear();
            saidOutsideTip = false;
            saidLeak = said60 = said30 = said10 = false;
            iceStepTime = 0;

            MissionStarted?.Invoke();
            if (mission.intro != null) foreach (var line in mission.intro) HabSays?.Invoke(line);
            StateChanged?.Invoke(State);
            // Events for "sol N" happen at the START of day N, so decisions come before the day is lived. Day 1 starts now.
            FireEvents(0);
            StartDay(1);
            if (HasSteps) StartStep();
        }

        // ───────── scripted missions: steps strictly in order ─────────

        public bool HasSteps => Mission != null && Mission.steps != null && Mission.steps.Length > 0;
        public StepDef CurrentStep => HasSteps && State != null && State.stepIndex < Mission.steps.Length ? Mission.steps[State.stepIndex] : null;
        public event Action<StepDef> StepStarted;
        public event Action StormStarted, Rescued;

        /// <summary>Real-time limits (seconds) for the dust storm emergency and the night with no sun.</summary>
        public const float StormSeconds = 120f, LeakAfter = 60f, NightSeconds = 75f;

        void StartStep()
        {
            var st = CurrentStep;
            if (st == null) return;
            State.stepBase = st.kind == "samples" ? State.samples : 0;
            State.iceStep = st.kind == "ice" ? 0 : State.iceStep;
            switch (st.kind)
            {
                case "clean":
                    if (State.panelDust < 0.4f) State.panelDust = 0.65f;   // dust settled overnight
                    break;
                case "repair":
                    State.roverBroken = true;
                    State.roverFault = string.IsNullOrEmpty(st.arg) ? "Something broke." : st.arg;
                    break;
                case "ping":
                    State.iceMask = 0;
                    State.atPing = false;
                    State.iceAccepted = true;    // the ice deposits appear at the pinged location
                    break;
                case "night":
                    State.nightActive = true;
                    State.nightTime = 0;
                    if (State.stormOver)
                    {
                        State.power = Mathf.Min(State.power, 48f);   // the storm buried the panels in dust before sunset
                        State.panelDust = 1f;
                    }
                    if (State.iceLost) { State.oxygen -= 10; Say("HAB: Without the ice, we have no extra water to make air tonight. Air -10%."); }
                    State.ClampAll();
                    break;
            }
            StepStarted?.Invoke(st);   // first: the UI drops old HAB lines so this step's explanation shows right away
            if (!string.IsNullOrEmpty(st.hab)) Say(st.hab);
            StateChanged?.Invoke(State);
        }

        bool StepDone(StepDef st)
        {
            var s = State;
            switch (st.kind)
            {
                case "hab": return s.habTalked;
                case "forecast": return s.forecastSeen;
                case "system": return s.toggled.Contains(st.arg) && s.IsOn(st.arg);   // switched OFF, then ON again
                case "status": return s.statusChecked;
                case "suitUp": return s.suitOn;
                case "leaveAirlock": return s.outside;
                case "clean": return s.panelDust < 0.05f;
                case "samples": return s.samples - s.stepBase >= Mathf.Max(1, st.count);
                case "repair": return !s.roverBroken;
                case "rover": return s.roverVisited;
                case "ping": return s.atPing;
                case "ice": return s.iceStep >= Mathf.Max(1, st.count) || (st.arg == "storm" && s.iceLost);
                case "returnBase": return !s.outside && !s.inShelter;
                case "lab": return s.labScanned;
                case "sleep": return s.sol > s.stepBase;
                case "night": return s.nightOver;
                default: return true;
            }
        }

        /// <summary>Moves to the next step when the current one is done. The last step ends the mission (success).</summary>
        void TickSteps()
        {
            if (!HasSteps || IsOver) return;
            int guard = 0;
            while (CurrentStep != null && StepDone(CurrentStep) && guard++ < 4)
            {
                var done = CurrentStep;
                if (done.kind == "returnBase" && State.stormActive) EndStorm();
                State.stepIndex++;
                if (CurrentStep == null) { End(true, "Every task is done. Great work, Astronaut!"); return; }
                if (CurrentStep.kind == "sleep") State.stepBase = State.sol;
                StartStep();
            }
        }

        public void TalkToHab()
        {
            if (State == null || State.habTalked) return;
            State.habTalked = true;
            StateChanged?.Invoke(State);
        }

        public void StatusChecked()
        {
            if (State == null || State.statusChecked) return;
            State.statusChecked = true;
            StateChanged?.Invoke(State);
        }

        public void RoverVisited()
        {
            if (State == null || State.roverVisited) return;
            State.roverVisited = true;
            StateChanged?.Invoke(State);
        }

        public void ArrivedAtPing()
        {
            if (State == null || State.atPing || CurrentStep == null || CurrentStep.kind != "ping") return;
            State.atPing = true;
            StateChanged?.Invoke(State);
        }

        /// <summary>The Science lab scans every rock and ice sample you brought in: research right now.</summary>
        public bool ScanSamples()
        {
            if (State == null || IsOver || State.outside) return false;
            if (!State.IsOn(SystemIds.Science)) { Say("HAB: Turn ON the Science lab first. Its scanner needs power."); return false; }
            int rocks = State.samples - State.samplesStudied;
            int ice = State.iceWater;
            int pts = rocks * HabitatState.ResearchPerStudy + ice * 10;
            State.samplesStudied = State.samples;
            State.research += pts;
            State.labScanned = true;
            State.power -= 3 * lvl.drainMultiplier;
            State.ClampAll();
            Say($"HAB: Scan complete! {rocks} rock{(rocks == 1 ? "" : "s")} and {ice} ice sample{(ice == 1 ? "" : "s")} studied. <color=#FFD36B><b>+{pts} research points!</b></color>" +
                (ice > 0 ? " The ice is real water: tonight I'll turn it into oxygen." : ""));
            if (pts > 0) ResearchEarned?.Invoke(pts);
            StateChanged?.Invoke(State);
            return true;
        }

        // ───────── dust storm emergency ─────────

        void BeginStorm()
        {
            if (State.stormActive || State.stormOver) return;
            State.stormActive = true;
            State.stormTime = 0;
            Say("MC: EMERGENCY! The dust storm grew much faster than we predicted. Get back to the base NOW!");
            Say("HAB: You have about 2 minutes. You can stay for the rest of the ice, but if you leave early, the ice stays behind.");
            StormStarted?.Invoke();
            StateChanged?.Invoke(State);
        }

        void EndStorm()
        {
            State.stormActive = false;
            State.stormOver = true;
            if (State.iceStep < 3 && !State.iceLost) LoseIce();
            Say(State.iceLost
                ? "HAB: You made it inside! We had to leave the ice behind, so tonight there's no extra water to make air."
                : "HAB: You made it, with all the ice! That was close.");
            StateChanged?.Invoke(State);
        }

        public void LoseIce()
        {
            State.iceLost = true;
            State.iceWater = 0;
            State.iceStep = 0;
            State.iceMask = 7;    // the deposits are buried by the storm
            StateChanged?.Invoke(State);
        }

        /// <summary>How strong the storm is right now (0..1): drives fog, dust and air loss.</summary>
        public float StormStrength => State != null && State.stormActive ? Mathf.Clamp01(State.stormTime / StormSeconds) : 0f;
        public bool SuitLeaking => State != null && State.stormActive && State.stormTime >= LeakAfter;
        public float StormSecondsLeft => State != null ? Mathf.Max(0, StormSeconds - State.stormTime) : 0;

        /// <summary>Never a death: you pass out, HAB's rover brings you home, and the mission starts again.</summary>
        void Rescue(string why)
        {
            if (IsOver) return;
            Rescued?.Invoke();
            End(false, why + " You passed out, and HAB sent the rover to bring you home safely. Try the mission again!");
        }

        public float NightSecondsLeft => State != null ? Mathf.Max(0, NightSeconds - State.nightTime) : 0;

        /// <summary>Battery used per second at night: the systems you leave on.</summary>
        public float NightPowerPerSecond => State == null ? 0 : SimEngine.PowerDrain(State, cfg) * lvl.drainMultiplier * 0.025f;

        // ───────── the day routine ─────────

        /// <summary>Today's routine tasks (from the mission file, or defaults). Day = State.sol + 1.</summary>
        public List<string> TodayTasks()
        {
            var list = new List<string>();
            if (State == null) return list;
            int day = State.sol + 1;
            var plan = Mission.DayFor(day);
            if (plan != null && plan.tasks != null) list.AddRange(plan.tasks);
            else
            {
                var e = Mission.EnvForSol(day);
                bool safe = e.tau < 4;
                if (safe) list.Add("samples:2");
                if (!e.blackout) list.Add("status");
                if (safe) list.Add("weather");
                if (State.panelDust >= 0.3f) list.Add("clean");
            }
            if (!list.Contains("forecast")) list.Insert(0, "forecast");
            if (State.roverBroken && !list.Contains("repair")) list.Add("repair");   // not fixed yesterday: still on the list
            return list;
        }

        void StartDay(int day)
        {
            State.NewDay();
            var tasks = TodayTasks();
            if (tasks.Contains("repair") && !State.roverBroken)
            {
                State.roverBroken = true;
                State.roverFault = Mission.DayFor(day)?.roverFault ?? "A wheel motor stopped working.";
                Say($"HAB: Perseverance sent an error: {State.roverFault} It can't send its data report until it's fixed.");
            }
            DayStarted?.Invoke(day);
            StateChanged?.Invoke(State);
            FireEvents(day);
            var env = Mission.EnvForSol(day);
            if (tasks.Contains("ice") && env.tau < 4) DeliverIceMap();   // never send kids out into a storm
        }

        void DeliverIceMap()
        {
            if (!State.IsOn(SystemIds.Radio) || NoContact)
            {
                State.iceMapWaiting = true;
                Say("HAB: Mission Control is trying to send us a map. Turn ON the Radio to get it!");
                return;
            }
            State.iceMapWaiting = false;
            State.iceMapReceived = true;
            Say("MC: We sent you a map! Our orbiter spotted what may be ice under the ground, far from the base.");
            pending.Enqueue(IceCard());
            ShowNextPending();
            StateChanged?.Invoke(State);
        }

        Card IceCard() => new Card
        {
            id = "ice_map",
            icon = "map",
            prompt = "NASA's map shows ice under the ground, far away. Go dig for it? (Practice map: made up for the game.)",
            options = new[]
            {
                new Option
                {
                    label = "Go for the ice!", icon = "suit", action = "iceGo",
                    note = $"Long walk outside (air drops while you're out) · Drilling: Air -{IceDrillAir * lvl.drainMultiplier:0}%",
                    hab = $"Brave! Follow the blue marker. Ice gives +{HabitatState.ResearchPerIce} research, and I can turn it into air. Come back before your air gets low!",
                },
                new Option
                {
                    label = "Not today, stay safe", icon = "home", tags = new[] { "priority" },
                    hab = "Safe choice. Real astronauts skip risky trips when supplies are low.",
                },
            },
        };

        public void ForecastSeen()
        {
            if (State == null || IsOver || State.forecastSeen) return;
            State.forecastSeen = true;
            StateChanged?.Invoke(State);
        }

        /// <summary>Send today's status report to Earth: needs the Radio and contact, costs power right away.</summary>
        public bool SendStatus()
        {
            if (State == null || IsOver || ActiveCard != null || State.statusSent) return false;
            if (!State.IsOn(SystemIds.Radio)) { Say("HAB: Turn ON the Radio first, then we can send the report."); return false; }
            if (NoContact) { Say("HAB: The Sun is in the way. Earth can't hear us today."); return false; }
            float cost = StatusPower * lvl.drainMultiplier;
            State.power -= cost;
            State.statusSent = true;
            State.ClampAll();
            State.choiceLog.Add($"Sol {State.sol}: sent a status report to Earth");
            Say($"HAB: Report sent! It used {cost:0}% power. Radio waves take minutes to reach Earth.");
            Say(State.oxygen < 40 || State.power < 40
                ? "MC: Report received. Your reserves are low: rest and save power and air today."
                : "MC: Report received. Everything looks great. Keep up the good work!");
            StateChanged?.Invoke(State);
            CheckEnd();
            return true;
        }

        /// <summary>Drill the ice at the ice site (outside, after accepting the map). High risk, high reward.</summary>
        public bool DrillIce() => DrillIce(0);

        /// <summary>True if ice deposit #index is there to drill (scripted missions have 3 at the pinged location).</summary>
        public bool IceAvailable(int index)
        {
            if (State == null || IsOver || !State.iceAccepted || State.iceLost) return false;
            if (HasSteps) return (State.iceMask & (1 << index)) == 0;
            return index == 0 && !State.iceDrilled;
        }

        public float IceCost => (HasSteps ? 5f : IceDrillAir) * (lvl != null ? lvl.drainMultiplier : 1);
        public int IceResearch => HasSteps ? 20 : HabitatState.ResearchPerIce;

        public bool DrillIce(int index)
        {
            if (State == null || IsOver || ActiveCard != null || !State.outside || !IceAvailable(index)) return false;
            float cost = IceCost;
            State.oxygen -= cost;
            if (HasSteps) { State.iceMask |= 1 << index; State.iceStep++; }
            else State.iceDrilled = true;
            State.iceWater++;
            State.iceTotal++;
            State.research += IceResearch;
            if (Mission.mode != "test") { AchievementManager.AddIce(); AnnounceUnlocks(); }
            State.ClampAll();
            State.choiceLog.Add($"Sol {State.sol}: drilled ice (+{IceResearch} research)");
            Say($"HAB: ICE! Real water ice from Mars! <color=#FFD36B><b>+{IceResearch} research points.</b></color> Drilling used {cost:0}% air." +
                (HasSteps ? $" ({State.iceStep} of 3)" : " Hurry back inside!"));
            ResearchEarned?.Invoke(IceResearch);
            StateChanged?.Invoke(State);
            CheckEnd();
            return true;
        }

        /// <summary>Fix Perseverance (called by the rover after the repair hold). Costs power for the spare part.</summary>
        public bool RepairRover()
        {
            if (State == null || IsOver || !State.roverBroken) return false;
            float cost = RepairPower * lvl.drainMultiplier;
            State.power -= cost;
            State.roverBroken = false;
            State.research += HabitatState.ResearchPerRepair;
            State.ClampAll();
            State.choiceLog.Add($"Sol {State.sol}: repaired Perseverance");
            Say($"HAB: Perseverance is fixed! <color=#FFD36B><b>+{HabitatState.ResearchPerRepair} research points.</b></color> The spare part used {cost:0}% power. Click it to read its report.");
            ResearchEarned?.Invoke(HabitatState.ResearchPerRepair);
            StateChanged?.Invoke(State);
            CheckEnd();
            return true;
        }

        /// <summary>Read the base's weather station (outside). Small research reward; the UI shows the readings.</summary>
        public bool ReadWeather()
        {
            if (State == null || IsOver || ActiveCard != null || !State.outside) return false;
            if (!State.weatherRead)
            {
                State.weatherRead = true;
                State.research += HabitatState.ResearchPerWeather;
                State.choiceLog.Add($"Sol {State.sol}: read the weather station");
                ResearchEarned?.Invoke(HabitatState.ResearchPerWeather);
                StateChanged?.Invoke(State);
            }
            return true;
        }

        // ───────── live air while outside ─────────

        float liveTick;
        bool warned30, warned20;

        bool saidLeak, said60, said30, said10;
        float stepTick, iceStepTime;

        void Update()
        {
            if (State == null || IsOver) return;
            stepTick -= Time.deltaTime;
            if (stepTick <= 0) { stepTick = 0.2f; TickSteps(); if (IsOver) return; }
            if (Paused || ActiveCard != null) return;
            float dt = Time.deltaTime;
            bool changed = false;

            // The storm arrives while you're collecting ice (after your first deposit, or 25 s at the site)
            var cur = CurrentStep;
            if (cur != null && cur.kind == "ice" && cur.arg == "storm" && !State.stormActive && !State.stormOver)
            {
                iceStepTime += dt;
                if (iceStepTime >= 25f || State.iceStep >= 1) BeginStorm();
            }
            else iceStepTime = 0;

            // Dust storm emergency: a real 2-minute countdown
            if (State.stormActive)
            {
                State.stormTime += dt;
                float left = StormSecondsLeft;
                if (left <= 60 && !said60) { said60 = true; Say("HAB: One minute left! The dust is getting thicker."); }
                if (SuitLeaking && !saidLeak) { saidLeak = true; Say("HAB: WARNING! Sand scratched a seal: your suit is LEAKING air. Hurry home!"); }
                if (left <= 30 && !said30) { said30 = true; Say("HAB: 30 seconds! I can barely see you on the camera!"); }
                if (left <= 10 && !said10) { said10 = true; Say("HAB: 10 seconds!"); }
                if (left <= 0 && State.outside) { Rescue("The dust storm caught you outside."); return; }
                if (!State.outside && !State.inShelter && State.stormTime > 1f) EndStorm();   // safe inside the base
                changed = true;
            }

            // Night with no sun: the battery drains every second; the Air maker needs power
            if (State.nightActive && !State.nightOver)
            {
                State.nightTime += dt;
                State.power -= NightPowerPerSecond * dt;
                if (!State.IsOn(SystemIds.LifeSupport)) State.oxygen -= 0.6f * dt;
                else if (!State.IsOn(SystemIds.Heater)) State.oxygen -= 0.15f * dt;   // the cold slows the Air maker
                if (State.power <= 0)
                {
                    State.power = 0;
                    State.systemsOn.Clear();
                    End(false, "The battery ran empty in the night, so everything shut down. HAB kept you safe. Next time, switch off what you don't need before night.");
                    return;
                }
                if (State.nightTime >= NightSeconds)
                {
                    State.nightOver = true;
                    State.nightActive = false;
                    Say("HAB: Sunrise! The solar panels are waking up. You made it through the night!");
                }
                changed = true;
            }

            if (State.outside)
            {
                float mult = 1f + (State.stormActive ? 0.6f : 0f) + (SuitLeaking ? 1.4f : 0f);
                State.oxygen -= LiveAirPerSecond * lvl.drainMultiplier * mult * dt;
                changed = true;
            }
            else warned30 = warned20 = false;

            liveTick -= dt;
            if (!changed || liveTick > 0) return;
            liveTick = 0.25f;
            State.ClampAll();
            if (State.outside)
            {
                if (State.oxygen <= 30 && !warned30) { warned30 = true; Say("HAB: Your air is at 30%. Start heading back inside!"); }
                if (State.oxygen <= 20 && !warned20) { warned20 = true; Say("HAB: Air at 20%! Come inside NOW."); }
                if (State.oxygen <= AirFloor) { Rescue("Your air ran too low outside."); return; }
            }
            StateChanged?.Invoke(State);
            CheckEnd();
        }

        /// <summary>Air lost per second outside right now (storm and leaks included).</summary>
        public float AirPerSecondNow => State == null ? 0 :
            LiveAirPerSecond * (lvl != null ? lvl.drainMultiplier : 1) * (1f + (State.stormActive ? 0.6f : 0f) + (SuitLeaking ? 1.4f : 0f));

        /// <summary>Seconds of air left outside before HAB has to call you back (for the HUD).</summary>
        public float SecondsOfAirLeft(float floor)
        {
            if (State == null) return 0;
            return Mathf.Max(0, (State.oxygen - floor) / Mathf.Max(0.01f, AirPerSecondNow));
        }

        /// <summary>The lowest air level before a safe abort (the mission's own rule, or a small safety floor).</summary>
        public float AirFloor
        {
            get
            {
                float floor = SafetyAirFloor;
                if (Mission?.abort != null)
                    foreach (var c in Mission.abort)
                        if (c.meter == "oxygen" && (c.op == "<=" || c.op == "<")) floor = Mathf.Max(floor, c.value);
                return floor;
            }
        }

        const float SafetyAirFloor = 3f;

        /// <summary>Start the same mission again (picks up a changed difficulty).</summary>
        public void Restart()
        {
            var gm = GameManager.Instance;
            Begin(Mission, gm.Config, gm.Level);
        }

        /// <summary>The player can always stop safely. Never a punishment, just a debrief.</summary>
        public void SafeAbort()
        {
            if (IsOver) return;
            End(false, "You called a safe abort. Good astronauts know when to stop and stay safe.");
        }

        bool saidOutsideTip;

        /// <summary>World objects (rover, locker...) can make HAB speak.</summary>
        public void Say(string line) => HabSays?.Invoke(line);

        /// <summary>Called by the 3D world when the astronaut walks in or out.</summary>
        public void SetLocation(bool outside, bool inShelter)
        {
            if (State == null || IsOver) return;
            if (State.outside == outside && State.inShelter == inShelter) return;
            State.outside = outside;
            State.inShelter = inShelter;
            if (outside && !State.suitOn)
            {
                State.suitOn = true;   // never stuck: walking out without a suit just suits you up
                Say("HAB: Whoa, you forgot your spacesuit! I helped you put it on in the airlock. Next time, use the suit stand.");
            }
            if (outside && !saidOutsideTip)
            {
                saidOutsideTip = true;
                Say($"HAB: You're outside on an [[eva|EVA]]. Each sol out here uses {cfg.balance.evaOxygenPerSol * lvl.drainMultiplier:0}% extra oxygen.");
            }
            StateChanged?.Invoke(State);
        }

        /// <summary>Pick up a rock sample in the world. Costs oxygen (no choice is free).</summary>
        public bool TryCollectSample(bool quickTrip = false)
        {
            if (IsOver || ActiveCard != null || State == null || (!quickTrip && !State.outside)) return false;
            var b = cfg.balance;
            State.oxygen -= b.sampleOxygenCost + (quickTrip && !State.outside ? 2 : 0);
            State.samples++;
            State.daySamples++;
            State.research += HabitatState.ResearchPerSample;
            // Which rock is it? A rarity roll from real Mars finds (the rock collection on the home screen)
            var rock = RockCollection.Roll(out bool isNew);
            if (rock != null)
            {
                int bonus = RockCollection.ResearchBonus[RockCollection.RarityIndex(rock.rarity)];
                State.research += bonus;
                RockFound?.Invoke(rock, isNew);
                if (!isNew) Say($"HAB: It's {rock.name} ({rock.rarity}). You have {RockCollection.Count(rock.id)} of these now." + (bonus > 0 ? $" +{bonus} bonus research!" : ""));
                AnnounceUnlocks();
            }
            State.ClampAll();
            State.choiceLog.Add($"Sol {State.sol}: collected rock sample #{State.samples} (+{HabitatState.ResearchPerSample} research points)");
            Say($"HAB: Sample #{State.samples} collected! <color=#FFD36B><b>+{HabitatState.ResearchPerSample} research points.</b></color> That used {b.sampleOxygenCost:0}% oxygen."
                + (State.samples > State.samplesStudied && !State.IsOn(SystemIds.Science) ? $" Turn on the Science lab to study it for +{HabitatState.ResearchPerStudy} more!" : ""));
            ResearchEarned?.Invoke(HabitatState.ResearchPerSample);
            StateChanged?.Invoke(State);
            CheckEnd();
            return true;
        }

        public void SetSystem(string id, bool on)
        {
            if (IsOver || ActiveCard != null) return;
            State.SetSystem(id, on);
            StateChanged?.Invoke(State);
            if (on && id == SystemIds.Radio && State.iceMapWaiting) DeliverIceMap();
        }

        void AnnounceUnlocks()
        {
            foreach (var u in AchievementManager.CheckCollection())
                Say($"HAB: New suit item unlocked: <color=#FFD36B><b>{u}</b></color>! Try it on from the home screen (My suit).");
        }

        /// <summary>Why you can't sleep right now (null = you can).</summary>
        public string CantSleepReason()
        {
            if (State == null || IsOver) return "";
            if (State.outside) return "You can't sleep outside! Go back inside the base first.";
            if (State.stormActive) return "Not now: the storm emergency isn't over!";
            if (State.nightActive) return "Stay awake! Watch the battery until sunrise.";
            var st = CurrentStep;
            if (st != null && st.kind != "sleep") return "Finish your missions first: " + st.text;
            return null;
        }

        /// <summary>The "Next sol" button (sleep). You take your suit off for the night.</summary>
        public void NextSol()
        {
            if (IsOver || ActiveCard != null) return;
            var why = CantSleepReason();
            if (why != null) { if (why.Length > 0) Say("HAB: " + why); return; }
            State.suitOn = false;
            AdvanceOne();
        }

        public void SetSuit(bool on)
        {
            if (State == null || IsOver || State.suitOn == on) return;
            if (!on && State.outside) { Say("HAB: Never take your suit off outside! There's no air to breathe out there."); return; }
            State.suitOn = on;
            StateChanged?.Invoke(State);
        }

        /// <summary>True during a solar-conjunction sol: Earth can't hear the radio.</summary>
        public bool NoContact => Mission != null && State != null && Mission.EnvForSol(Mathf.Max(1, State.sol)).blackout;

        public bool CanChoose(Option o)
        {
            if (o.needsRadio && (!State.IsOn(SystemIds.Radio) || NoContact)) return false;
            if (o.cost != null && o.cost.credits > SaveSystem.Credits) return false;
            return true;
        }

        public void Choose(int index)
        {
            if (IsOver || ActiveCard == null) return;
            var card = ActiveCard;
            var opt = card.options[index];
            if (!CanChoose(opt)) return;

            TrackTags(card, opt);
            SimEngine.ApplyOption(State, opt);
            if (opt.action == "iceGo") State.iceAccepted = true;
            if (opt.cost != null && opt.cost.credits > 0) SaveSystem.Credits -= opt.cost.credits;
            State.choiceLog.Add($"Sol {State.sol}: {card.prompt} -> {opt.label}");
            if (!string.IsNullOrEmpty(opt.hab)) HabSays?.Invoke(opt.hab);

            ActiveCard = null;
            StateChanged?.Invoke(State);
            if (CheckEnd()) return;

            // Waiting for Earth costs sols.
            int wait = opt.cost != null ? opt.cost.sols : 0;
            for (int i = 0; i < wait && !IsOver && ActiveCard == null; i++) AdvanceOne();

            if (!IsOver && ActiveCard == null) ShowNextPending();
        }

        void AdvanceOne()
        {
            SimEngine.AdvanceSol(State, Mission.EnvForSol(State.sol + 1), cfg, lvl);
            StudySamples();
            MeltIce();
            StateChanged?.Invoke(State);
            if (CheckEnd()) return;
            StartDay(State.sol + 1);   // the next day begins: its routine, events (and cards) happen now
        }

        /// <summary>Overnight the base melts the ice and splits the water into oxygen (real idea: making air from local ice).</summary>
        void MeltIce()
        {
            if (State.iceWater <= 0) return;
            if (!State.IsOn(SystemIds.LifeSupport)) { Say("HAB: We have ice to turn into air, but the Air maker is off!"); return; }
            float air = IceWaterToAir * State.iceWater;
            State.oxygen += air;
            State.iceWater = 0;
            State.ClampAll();
            Say($"HAB: Overnight I melted the ice and split the water into oxygen. <b>Air +{air:0}%!</b> Real Mars bases may make air and water like this.");
        }

        /// <summary>Solar panels collect dust every sol. Cleaning them outside costs oxygen and restores full power.</summary>
        /// <param name="quickTrip">true = one-click from the HUD (mouse-only play): counts as a short walk outside, costs a bit more air.</param>
        public bool CleanPanels(bool quickTrip = false)
        {
            if (IsOver || ActiveCard != null || State == null || (!quickTrip && !State.outside)) return false;
            if (State.panelDust < 0.05f) { Say("HAB: The panels are already clean."); return false; }
            float cost = Mathf.Max(1, cfg.balance.cleanOxygenCost) + (quickTrip && !State.outside ? 2 : 0);
            State.oxygen -= cost;
            State.panelDust = 0;
            State.ClampAll();
            State.choiceLog.Add($"Sol {State.sol}: cleaned the solar panels");
            Say($"HAB: Panels clean! They make full power again. That used {cost:0}% oxygen. (Real: gusts of wind sometimes cleaned the panels of Spirit and Opportunity!)");
            StateChanged?.Invoke(State);
            CheckEnd();
            return true;
        }

        static readonly string[] LabFindings =
        {
            "full of tiny grey balls called [[blueberries]]. They formed in water, long ago!",
            "made of thin layers, like pages in a book. Water and wind laid them down over a very long time.",
            "rich in a mineral called hematite. On Earth it often forms in water. Big clue: Mars was once wetter!",
            "another page in the story of Mars. The crew will love this one.",
        };

        /// <summary>With the Science lab on, one collected sample is studied per sol (this is what the lab is for).</summary>
        void StudySamples()
        {
            if (!State.IsOn(SystemIds.Science)) return;
            if (State.samplesStudied >= State.samples)
            {
                if (State.samples == 0) Say("HAB: The Science lab has nothing to study yet. Bring a rock sample, or switch the lab off to save power.");
                return;
            }
            State.samplesStudied++;
            State.research += HabitatState.ResearchPerStudy;
            Say($"HAB: The lab studied sample #{State.samplesStudied}: it is {LabFindings[Mathf.Min(State.samplesStudied - 1, LabFindings.Length - 1)]} <color=#FFD36B><b>+{HabitatState.ResearchPerStudy} research points!</b></color>");
            ResearchEarned?.Invoke(HabitatState.ResearchPerStudy);
        }

        void FireEvents(int sol)
        {
            if (Mission.events == null) return;
            foreach (var ev in Mission.events)
            {
                if (ev.sol != sol) continue;
                if (!string.IsNullOrEmpty(ev.mc))
                {
                    // Messages from Earth only arrive if the Radio is on and the Sun isn't in the way.
                    if (State.IsOn(SystemIds.Radio) && !NoContact) HabSays?.Invoke("MC: " + ev.mc);
                    else HabSays?.Invoke("HAB: A message from Mission Control was sent, but the Radio was off, so we missed it. Keep the Radio on to hear them.");
                }
                if (!string.IsNullOrEmpty(ev.hab)) HabSays?.Invoke(ev.hab);
                if (!string.IsNullOrEmpty(ev.card))
                {
                    var c = Mission.FindCard(ev.card);
                    if (c != null) pending.Enqueue(c);
                    else Debug.LogWarning($"Mission {Mission.id}: card '{ev.card}' not found");
                }
            }
            ShowNextPending();
        }

        void ShowNextPending()
        {
            if (ActiveCard != null || pending.Count == 0) return;
            ActiveCard = pending.Dequeue();
            CardShown?.Invoke(ActiveCard);
        }

        void TrackTags(Card card, Option chosen)
        {
            bool offersPriority = false, offersProcedure = false;
            foreach (var o in card.options)
            {
                offersPriority |= o.HasTag("priority");
                offersProcedure |= o.HasTag("procedure");
            }
            if (offersPriority) { State.priorityOffered++; if (chosen.HasTag("priority")) State.priorityPicked++; }
            if (offersProcedure) { State.procedureOffered++; if (chosen.HasTag("procedure")) State.procedurePicked++; }
        }

        bool CheckEnd()
        {
            if (Mission.abort != null)
                foreach (var c in Mission.abort)
                    if (c.IsMet(State)) { End(false, c.message); return true; }
            if (State.oxygen <= SafetyAirFloor)
            {
                End(false, "Your air got very low, so HAB guided you safely back inside. Next time, head back when the Air gauge turns yellow.");
                return true;
            }

            if (GoalMet()) { End(true, "Goal complete!"); return true; }

            if (Mission.goal != null && Mission.goal.maxSols > 0 && State.sol >= Mission.goal.maxSols)
            {
                End(false, "Time ran out before the goal was done. HAB called a safe abort.");
                return true;
            }
            return false;
        }

        bool GoalMet()
        {
            var g = Mission.goal;
            if (g == null) return false;
            switch (g.type)
            {
                case "surviveSols": return State.sol >= g.target;
                case "collectSamples": return State.samples >= g.target;
                case "reachShelter": return State.shield * 100f >= g.target;
                case "stayInContact": return State.sol >= g.target && State.IsOn(SystemIds.Radio);
                case "research": return State.research >= g.target;
                default: return false;
            }
        }

        void End(bool success, string reason)
        {
            IsOver = true;
            ActiveCard = null;
            pending.Clear();

            var b = ScoreCalculator.Score(State);
            var result = new MissionResult
            {
                success = success,
                reason = reason,
                breakdown = b,
                stars = ScoreCalculator.Stars(b, success),
                finalState = State,
            };

            if (Mission.mode == "freeplay")
            {
                // Only samples brought home count; a healthy reserve is a bonus.
                if (success)
                    result.creditsEarned = State.research + (Mathf.Min(State.power, State.oxygen) >= 50 ? 20 : 0);   // research points become credits
                SaveSystem.Credits += result.creditsEarned;
                SaveSystem.FreeplaySols += State.sol;
                if (result.creditsEarned > SaveSystem.HighScore) SaveSystem.HighScore = result.creditsEarned;
            }
            else if (Mission.mode == "story")
            {
                SaveSystem.SetBestStars(Mission.id, result.stars);
                if (success)
                {
                    result.firstCompletion = !SaveSystem.IsDone(Mission.id);
                    SaveSystem.MarkDone(Mission.id);   // the stage's lesson goes into the Crew Handbook
                }
            }

            // Only samples brought home count.
            if (success && Mission.mode != "test")
            {
                SaveSystem.TotalSamples += State.samples;
                SaveSystem.TotalResearch += State.research;
            }

            // mode "test" saves nothing
            if (Mission.mode != "test") result.newUnlocks = AchievementManager.OnMissionEnded(Mission, result);
            MissionEnded?.Invoke(result);
        }
    }
}
