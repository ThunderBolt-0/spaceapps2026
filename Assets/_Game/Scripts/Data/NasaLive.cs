using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace MissionTrainer
{
    /// <summary>
    /// Real NASA data for "right now", fetched once when the game starts:
    ///  - DONKI space weather (solar flares, CMEs, radiation storms) for the last 30 days, with dates.
    ///    Tries api.nasa.gov with the key in Resources/Data/nasa_config.json, then DEMO_KEY, then NASA CCMC's own
    ///    DONKI web service (no key). If all fail, the game uses the saved DONKI snapshot (labelled with its date).
    ///  - Today's Mars date (Mars Sol Date, Perseverance mission sol, Mars season) calculated from today's real date.
    /// Honest labels: Live only after a real HTTP 200 DONKI answer.
    /// </summary>
    public static class NasaLive
    {
        [Serializable] class Config { public string apiKey; }
        [Serializable] class FlareItem { public string flrID, beginTime, peakTime, classType; }
        [Serializable] class FlareList { public FlareItem[] items; }
        [Serializable] class TimeItem { public string activityID, startTime, sepID, eventTime; }
        [Serializable] class TimeList { public TimeItem[] items; }

        public enum Status { NotChecked, Checking, Live, Saved }

        public static Status State { get; private set; }
        public static DateTime CheckedAtLocal { get; private set; }
        public static string Via { get; private set; }            // "api.nasa.gov" | "NASA CCMC DONKI"
        public static int Flares, Cmes, Seps;
        public static bool FlaresKnown;
        public static string BiggestFlare, BiggestFlareDate, LatestCmeDate, LatestSepDate;
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            State = Status.NotChecked; Changed = null; Flares = Cmes = Seps = 0;
            BiggestFlare = BiggestFlareDate = LatestCmeDate = LatestSepDate = Via = null;
        }

        static string ApiKey()
        {
            try
            {
                var c = ContentDb.LoadJson<Config>("Data/nasa_config");
                if (c != null && !string.IsNullOrWhiteSpace(c.apiKey)) return c.apiKey.Trim();
            }
            catch { }
            return "DEMO_KEY";
        }

        /// <summary>Start with: StartCoroutine(NasaLive.Refresh()). Safe to call again (e.g. a "Check again" button).</summary>
        public static IEnumerator Refresh()
        {
            if (State == Status.Checking) yield break;
            State = Status.Checking;
            Changed?.Invoke();

            var end = DateTime.UtcNow;
            var start = end.AddDays(-30);
            string range = "startDate=" + start.ToString("yyyy-MM-dd") + "&endDate=" + end.ToString("yyyy-MM-dd");
            string key = ApiKey();
            var bases = new System.Collections.Generic.List<(string url, string via)>
            {
                ("https://api.nasa.gov/DONKI/{0}?" + range + "&api_key=" + key, "api.nasa.gov"),
            };
            if (key != "DEMO_KEY") bases.Add(("https://api.nasa.gov/DONKI/{0}?" + range + "&api_key=DEMO_KEY", "api.nasa.gov"));
            bases.Add(("https://kauai.ccmc.gsfc.nasa.gov/DONKI/WS/get/{0}?" + range, "NASA CCMC DONKI"));

            foreach (var b in bases)
            {
                string flr = null, cme = null, sep = null;
                // CME is the test: if it answers, this route works. Flares and radiation storms are extras
                // (NASA's flare endpoint sometimes answers 404 while the others work).
                yield return Get(string.Format(b.url, "CME"), r => cme = r);
                if (cme == null) continue;   // this route doesn't work right now: try the next one
                yield return Get(string.Format(b.url, "SEP"), r => sep = r);
                yield return Get(string.Format(b.url, "FLR"), r => flr = r);
                Parse(flr, cme, sep);
                State = Status.Live;
                Via = b.via;
                CheckedAtLocal = DateTime.Now;
                Changed?.Invoke();
                yield break;
            }
            State = Status.Saved;
            Changed?.Invoke();
        }

        /// <summary>Strict: only HTTP 200 with a JSON array (DONKI's format) counts.</summary>
        static IEnumerator Get(string url, Action<string> done)
        {
            using (var req = UnityWebRequest.Get(url))
            {
                req.timeout = 10;
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success || req.responseCode != 200) { done(null); yield break; }
                string body = (req.downloadHandler.text ?? "").Trim();
                if (body.Length == 0) body = "[]";
                done(body[0] == '[' ? body : null);
            }
        }

        static void Parse(string flr, string cme, string sep)
        {
            FlaresKnown = flr != null;
            var flares = flr != null ? JsonUtility.FromJson<FlareList>("{\"items\":" + flr + "}")?.items ?? new FlareItem[0] : new FlareItem[0];
            Flares = flares.Length;
            BiggestFlare = null; BiggestFlareDate = null;
            float best = -1;
            foreach (var f in flares)
            {
                float v = FlareStrength(f.classType);
                if (v > best) { best = v; BiggestFlare = f.classType; BiggestFlareDate = ShortDate(f.peakTime ?? f.beginTime); }
            }
            var cmes = cme != null ? JsonUtility.FromJson<TimeList>("{\"items\":" + cme + "}")?.items ?? new TimeItem[0] : new TimeItem[0];
            Cmes = cmes.Length;
            LatestCmeDate = cmes.Length > 0 ? ShortDate(cmes[cmes.Length - 1].startTime) : null;
            var seps = sep != null ? JsonUtility.FromJson<TimeList>("{\"items\":" + sep + "}")?.items ?? new TimeItem[0] : new TimeItem[0];
            Seps = seps.Length;
            LatestSepDate = seps.Length > 0 ? ShortDate(seps[seps.Length - 1].eventTime) : null;
        }

        /// <summary>A1 &lt; B1 &lt; C1 &lt; M1 &lt; X1 (each letter is 10 times stronger).</summary>
        public static float FlareStrength(string cls)
        {
            if (string.IsNullOrEmpty(cls)) return 0;
            int p = "ABCMX".IndexOf(char.ToUpperInvariant(cls[0]));
            float.TryParse(cls.Substring(1), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var n);
            return p < 0 ? 0 : Mathf.Pow(10, p) * Mathf.Max(1, n);
        }

        static string ShortDate(string iso)
        {
            if (string.IsNullOrEmpty(iso)) return null;
            return DateTime.TryParse(iso, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal, out var d)
                ? d.ToString("d MMM yyyy", System.Globalization.CultureInfo.InvariantCulture) : iso.Substring(0, Math.Min(10, iso.Length));
        }

        /// <summary>Radiation level for a game day, from real live data: a recent radiation storm or a big flare raises it.</summary>
        public static float LiveRadiationLevel()
        {
            if (State != Status.Live) return 1f;
            if (Seps > 0) return 2f;                                        // like Curiosity's RAD in Sept 2017 (about double)
            if (FlareStrength(BiggestFlare) >= FlareStrength("X1")) return 1.5f;
            return 1f;
        }

        /// <summary>One line for badges: "Live · checked 14 Nov 2026, 4:35 PM" or "Saved Sept 2017".</summary>
        public static string BadgeLine()
        {
            switch (State)
            {
                case Status.Live: return $"Real NASA data · Live · checked {CheckedAtLocal.ToString("d MMM yyyy, h:mm tt", System.Globalization.CultureInfo.InvariantCulture)}";
                case Status.Checking: return "Connecting to NASA...";
                default: return $"Real NASA data · Saved {DonkiService.Snapshot?.snapshotDate ?? "snapshot"} (NASA didn't answer just now)";
            }
        }

        /// <summary>Kid-friendly summary of the live space weather.</summary>
        public static string Summary()
        {
            if (State != Status.Live) return "We couldn't reach NASA just now, so the game uses saved real NASA data.";
            string flare = !FlaresKnown ? "" : Flares == 0 ? "no solar flares, " : $"{Flares} solar flare{(Flares == 1 ? "" : "s")} (biggest: {BiggestFlare}, {BiggestFlareDate}), ";
            string cme = Cmes == 0 ? "no big clouds of Sun material (CMEs)" : $"{Cmes} big clouds of Sun material (CMEs, latest {LatestCmeDate})";
            string sep = Seps == 0 ? "no radiation storms" : $"{Seps} radiation storm{(Seps == 1 ? "" : "s")} (latest {LatestSepDate})";
            return $"In the last 30 days the Sun had {flare}{cme}, and {sep}.";
        }

        // ───────── Mars date (calculated from today's real date; Mars24 method by NASA GISS) ─────────

        static double JulianTT(DateTime utc) => utc.Subtract(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalDays + 2440587.5 + 69.184 / 86400.0;

        /// <summary>Mars Sol Date: days on Mars since 1873 (like a Mars calendar count).</summary>
        public static double MarsSolDate(DateTime utc) => (JulianTT(utc) - 2405522.0028779) / 1.0274912517;

        /// <summary>Perseverance's mission sol today (landed 18 Feb 2021 = sol 0).</summary>
        public static int PerseveranceSol(DateTime utc) =>
            (int)Math.Floor(MarsSolDate(utc) - MarsSolDate(new DateTime(2021, 2, 18, 20, 55, 0, DateTimeKind.Utc)));

        /// <summary>Solar longitude Ls in degrees: where Mars is in its year (0 = northern spring starts).</summary>
        public static double SolarLongitude(DateTime utc)
        {
            double dt = JulianTT(utc) - 2451545.0;
            double M = (19.3871 + 0.52402073 * dt) * Math.PI / 180.0;
            double alpha = 270.3871 + 0.524038496 * dt;
            double eq = (10.691 + 3.0e-7 * dt) * Math.Sin(M) + 0.623 * Math.Sin(2 * M) + 0.050 * Math.Sin(3 * M) + 0.005 * Math.Sin(4 * M) + 0.0005 * Math.Sin(5 * M);
            double ls = (alpha + eq) % 360.0;
            return ls < 0 ? ls + 360 : ls;
        }

        public static string MarsSeason(double ls)
        {
            string s = ls < 90 ? "spring" : ls < 180 ? "summer" : ls < 270 ? "autumn" : "winter";
            return $"northern {s}";
        }

        /// <summary>Dust storms are most common when Mars is closest to the Sun (Ls about 180 to 330).</summary>
        public static bool DustSeason(double ls) => ls >= 180 && ls <= 330;

        public static string MarsDateLine()
        {
            var now = DateTime.UtcNow;
            double ls = SolarLongitude(now);
            return $"Today on Earth: {DateTime.Now.ToString("d MMM yyyy", System.Globalization.CultureInfo.InvariantCulture)}  ·  Perseverance's sol about {PerseveranceSol(now)}  ·  Mars season: {MarsSeason(ls)} (Ls {ls:0}°)" +
                   (DustSeason(ls) ? "  ·  dust storm season!" : "");
        }
    }
}
