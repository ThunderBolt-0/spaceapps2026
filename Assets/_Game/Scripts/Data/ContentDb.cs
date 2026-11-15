using System;
using UnityEngine;

namespace MissionTrainer
{
    [Serializable] public class GlossaryEntry { public string key, word, meaning; }
    [Serializable] public class GlossaryList { public GlossaryEntry[] entries; }

    [Serializable]
    public class SourceEntry
    {
        public string key, name, provider, url;
        public string usedFor;  // why the game uses it (Science behind the game page)
    }
    [Serializable] public class SourceList { public SourceEntry[] sources; }

    [Serializable]
    public class ShopItem
    {
        public string id, label, description;
        public string stat;    // startPower | startOxygen | startFood | shield
        public float amount;
        public int cost;
    }
    [Serializable] public class ShopList { public ShopItem[] items; }

    /// <summary>A real NASA image (NASA Image and Video Library), embedded in Resources/NasaImages/{key}.</summary>
    [Serializable]
    public class NasaImage
    {
        public string key, nasaId, title, date, credit, url, caption;
    }
    [Serializable] public class NasaImageList { public string note; public NasaImage[] images; }

    /// <summary>
    /// Loads JSON from Resources/ (synchronous, works in WebGL).
    /// Glossary, sources and shop are cached after the first read.
    /// </summary>
    public static class ContentDb
    {
        static GlossaryList glossary;
        static SourceList sources;
        static ShopList shop;

        // Domain reload is off in this project, so clear caches each time Play starts
        // (otherwise JSON edits wouldn't show up until Unity restarts).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() { glossary = null; sources = null; shop = null; images = null; }

        public static T LoadJson<T>(string resourcePath) where T : class
        {
            var asset = Resources.Load<TextAsset>(resourcePath);
            if (asset == null) { Debug.LogError("Missing Resources/" + resourcePath + ".json"); return null; }
            return JsonUtility.FromJson<T>(asset.text);
        }

        public static GlossaryEntry Glossary(string key)
        {
            glossary ??= LoadJson<GlossaryList>("Data/glossary");
            foreach (var e in glossary.entries) if (e.key == key) return e;
            return null;
        }

        public static SourceEntry[] AllSources => (sources ??= LoadJson<SourceList>("Data/sources")).sources;

        public static SourceEntry Source(string key)
        {
            foreach (var s in AllSources) if (s.key == key) return s;
            return null;
        }

        public static ShopItem[] ShopItems => (shop ??= LoadJson<ShopList>("Data/shop")).items;

        static NasaImageList images;
        public static NasaImage[] NasaImages => (images ??= LoadJson<NasaImageList>("Data/nasa_images"))?.images ?? new NasaImage[0];

        public static NasaImage Image(string key)
        {
            foreach (var i in NasaImages) if (i.key == key) return i;
            return null;
        }

        public static Texture2D ImageTexture(string key) => Resources.Load<Texture2D>("NasaImages/" + key);
    }
}
