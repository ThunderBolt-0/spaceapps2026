using System.Text;
using TMPro;
using UnityEngine;

namespace MissionTrainer
{
    /// <summary>"Science behind the game": every dataset and why the game uses it.</summary>
    public class SciencePageView : MonoBehaviour
    {
        public TMP_Text body;

        void OnEnable()
        {
            var sb = new StringBuilder();
            foreach (var s in ContentDb.AllSources)
            {
                sb.Append("<b>").Append(s.name).Append("</b>  <size=70%>").Append(s.provider).Append("</size>\n");
                sb.Append(s.usedFor).Append('\n');
                if (!string.IsNullOrEmpty(s.url)) sb.Append("<size=70%>").Append(s.url).Append("</size>\n");
                sb.Append('\n');
            }
            var snap = DonkiService.Snapshot;
            sb.Append($"<size=70%>Solar events: DONKI snapshot from {snap.snapshotDate} (Archived, not live).</size>");
            if (body) body.text = sb.ToString();
        }
    }
}
