using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace MissionTrainer
{
    [Serializable]
    public class SolarEvent
    {
        public string id, type, date, classType, note;
        public float marsRadiationMultiplier;
        public string multiplierSource; // "real: ..." or "simulated"
    }

    [Serializable]
    public class DonkiSnapshot
    {
        public string snapshotDate, note;
        public SolarEvent[] events;
    }

    /// <summary>
    /// Solar events come from the embedded snapshot (always works, labelled Archived).
    /// FetchLiveFlareCount is optional: use it only to show a "Real · Live" badge; it falls back quietly.
    /// </summary>
    public static class DonkiService
    {
        static DonkiSnapshot snapshot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() => snapshot = null;

        public static DonkiSnapshot Snapshot => snapshot ??= ContentDb.LoadJson<DonkiSnapshot>("Data/donki_snapshot");

        public static SolarEvent PickSolarEvent(System.Random rng)
        {
            var list = new List<SolarEvent>();
            foreach (var e in Snapshot.events) if (e.marsRadiationMultiplier > 1) list.Add(e);
            return list.Count == 0 ? null : list[rng.Next(list.Count)];
        }

        [Serializable] public class SepItem { public string sepID, eventTime; }
        [Serializable] public class SepList { public SepItem[] items; }

        /// <summary>
        /// Live check of NASA DONKI: solar energetic particle (radiation storm) events in the last N days.
        /// Usage: StartCoroutine(DonkiService.FetchLiveSep(30, (count, latest) => ...)). count = -1 on failure.
        /// Uses the public DEMO_KEY (shared, rate-limited), so the game always has the embedded snapshot to fall back on.
        /// </summary>
        public static IEnumerator FetchLiveSep(int days, Action<int, string> onDone)
        {
            var end = DateTime.UtcNow;
            var start = end.AddDays(-days);
            string url = "https://api.nasa.gov/DONKI/SEP?startDate=" + start.ToString("yyyy-MM-dd") +
                         "&endDate=" + end.ToString("yyyy-MM-dd") + "&api_key=DEMO_KEY";

            using (var req = UnityWebRequest.Get(url))
            {
                req.timeout = 8;
                yield return req.SendWebRequest();
                // Strict: only a real HTTP 200 DONKI answer counts as "Live". Anything else (429 rate limit,
                // blocked request, error page) is a failure, so we never label data as live when it isn't.
                if (req.result != UnityWebRequest.Result.Success || req.responseCode != 200) { onDone(-1, null); yield break; }

                string body = req.downloadHandler.text.Trim();
                if (body.Length == 0 || body == "[]") { onDone(0, null); yield break; }   // DONKI's "no events" answer
                if (body[0] != '[') { onDone(-1, null); yield break; }                     // not DONKI data
                // JsonUtility can't read a bare array, so wrap it.
                var parsed = JsonUtility.FromJson<SepList>("{\"items\":" + body + "}");
                var items = parsed?.items ?? new SepItem[0];
                string latest = items.Length > 0 ? items[items.Length - 1].eventTime : null;
                onDone(items.Length, latest);
            }
        }
    }
}
