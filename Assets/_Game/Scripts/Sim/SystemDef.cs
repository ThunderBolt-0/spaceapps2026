using System;

namespace MissionTrainer
{
    /// <summary>The five systems the player can toggle. Use these ids in mission JSON.</summary>
    public static class SystemIds
    {
        public const string LifeSupport = "lifeSupport";
        public const string Heater = "heater";
        public const string Radio = "radio";
        public const string Greenhouse = "greenhouse";
        public const string Science = "science";
    }

    [Serializable]
    public class SystemDef
    {
        public string id;
        public string label;
        public string icon;
        public string description;  // kid-friendly: "Makes oxygen."
        public float powerPerSol;   // battery % used each sol while on
    }
}
