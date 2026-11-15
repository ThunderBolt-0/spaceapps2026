using UnityEngine;

namespace MissionTrainer
{
    /// <summary>
    /// One stage of the expedition. The story: the Red Frontier program will send the first crew of six to Mars.
    /// First, one junior astronaut (you) must prove a person can live at Meridiani Base, and collect the
    /// lessons real rovers taught us into a Crew Handbook. Each stage = one real rover event = one lesson.
    /// </summary>
    public class ChapterInfo
    {
        public string id, stage, title, blurb, realEvent;
        public string mcMessage;     // story beat from Mission Control, shown in the briefing
        public string lesson;        // short lesson added to the Crew Handbook on success
        public string lessonDetail;  // one or two kid-friendly sentences
        public string label => stage;
    }

    public static class MissionLoader
    {
        /// <summary>Story order. Each id is a file in Resources/Missions/.</summary>
        public static readonly string[] StoryChapters =
        {
            "ch0_training", "ch1_storm", "ch6_luckywind", "ch2_coldnight", "ch3_stuck", "ch7_wheeltrouble", "ch8_icehunters", "ch4_silence", "ch9_memoryglitch",
        };

        public static readonly ChapterInfo[] Chapters =
        {
            new ChapterInfo
            {
                id = "ch0_training", stage = "Arrival", title = "First Sols", realEvent = "Your first sols at Meridiani Base",
                blurb = "Learn how the base works: power, air, food, the airlock and the rover.",
                mcMessage = "Welcome to Meridiani Base, Astronaut! The first crew of six will land after your expedition. Before they come, learn how this base keeps a person alive.",
                lesson = "Know your base",
                lessonDetail = "Every system uses power each sol. Life support makes the air. Outside, every second uses air.",
            },
            new ChapterInfo
            {
                id = "ch1_storm", stage = "Stage 1", title = "The Storm", realEvent = "Opportunity rover · June 2018",
                blurb = "A dust storm darkens the sky. Keep the base alive until it clears.",
                mcMessage = "A dust storm is growing over Meridiani. In 2018 a storm like this ended the rover Opportunity's mission. Keep the base alive until the sky clears.",
                lesson = "Save power before the storm",
                lessonDetail = "When dust blocks the Sun, switch off what you don't need early. Warm and breathing comes first.",
            },
            new ChapterInfo
            {
                id = "ch6_luckywind", stage = "Stage 2", title = "Lucky Wind", realEvent = "Spirit rover · March 2005",
                blurb = "Dust piles up on the panels. Clean them, and watch for the wind.",
                mcMessage = "A dusty week is coming. Dust on the solar panels means less power. Keep them clean, and keep your trips outside short.",
                lesson = "Clean panels, short trips",
                lessonDetail = "Dust steals solar power. Clean the panels when they get dusty, and plan quick trips to save air.",
            },
            new ChapterInfo
            {
                id = "ch2_coldnight", stage = "Stage 3", title = "Cold Night", realEvent = "Spirit rover · 2010",
                blurb = "Winter is coming and the batteries are low.",
                mcMessage = "Winter is coming. In 2010 the rover Spirit went silent during a Mars winter. Make a power plan so the base survives the cold.",
                lesson = "Winter needs a power plan",
                lessonDetail = "Keep heat and air on, and switch everything else off before the coldest nights.",
            },
            new ChapterInfo
            {
                id = "ch3_stuck", stage = "Stage 4", title = "Stuck", realEvent = "Opportunity 2005 · Spirit 2009",
                blurb = "Collect rock samples for the crew. Watch out for soft sand.",
                mcMessage = "The crew wants rock samples to study when they arrive. Please collect twelve. Be careful: soft sand trapped two rovers before you.",
                lesson = "Slow and steady in soft sand",
                lessonDetail = "Spinning wheels dig deeper. Back out slowly, a little at a time, and plan trips while oxygen is high.",
            },
            new ChapterInfo
            {
                id = "ch7_wheeltrouble", stage = "Stage 5", title = "Wheel Trouble", realEvent = "Curiosity rover · 2013",
                blurb = "Sharp rocks break the rover. Repair it and earn research.",
                mcMessage = "Sharp rocks are chewing up rover wheels near the base. Keep Perseverance rolling and bring home 200 research points.",
                lesson = "Fix it early, pick smooth roads",
                lessonDetail = "Small problems grow. Repair things on calm days, and choose the gentle route even if it's longer.",
            },
            new ChapterInfo
            {
                id = "ch8_icehunters", stage = "Stage 6", title = "Ice Hunters", realEvent = "Phoenix lander · 2008",
                blurb = "Find buried ice far from the base. Big reward, big risk.",
                mcMessage = "The crew will need water, and water can become air. Our maps show ice hidden under the ground. Choose your trips wisely!",
                lesson = "Ice is water, and water is air",
                lessonDetail = "Ice can be melted for water and split into oxygen. Go on long trips only when your air is high.",
            },
            new ChapterInfo
            {
                id = "ch4_silence", stage = "Stage 7", title = "Silence", realEvent = "Solar conjunction",
                blurb = "The Sun blocks the radio. Only procedure cards can help.",
                mcMessage = "Soon the Sun will sit between Mars and Earth, and we won't be able to talk for a while. You'll be on your own. Trust your procedure cards. Talk soon!",
                lesson = "Use procedures when Earth can't help",
                lessonDetail = "Plans written ahead of time keep you safe when nobody can answer the radio.",
            },
            new ChapterInfo
            {
                id = "ch9_memoryglitch", stage = "Stage 8", title = "Memory Glitch", realEvent = "Opportunity rover · 2014-2015",
                blurb = "The rover keeps forgetting. Send reports home every day.",
                mcMessage = "Perseverance's memory is failing, just like Opportunity's did. Send us your data every day so no science is lost. Pass this last test and the crew can launch!",
                lesson = "Send your data home daily",
                lessonDetail = "If something might forget, save it somewhere safe right away. Daily reports keep the science safe.",
            },
        };

        public static ChapterInfo Info(string id)
        {
            foreach (var c in Chapters) if (c.id == id) return c;
            return null;
        }

        /// <summary>The next stage to play (first one not completed), or null when the expedition is done.</summary>
        public static ChapterInfo NextStage()
        {
            foreach (var c in Chapters) if (Exists(c.id) && !SaveSystem.IsDone(c.id)) return c;
            return null;
        }

        public static int CompletedStages()
        {
            int n = 0;
            foreach (var c in Chapters) if (SaveSystem.IsDone(c.id)) n++;
            return n;
        }

        public static bool Exists(string id) => Resources.Load<TextAsset>("Missions/" + id) != null;

        public static MissionData Load(string id) => ContentDb.LoadJson<MissionData>("Missions/" + id);
    }
}
