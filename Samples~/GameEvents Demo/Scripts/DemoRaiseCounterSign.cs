using System.Text;
using UnityEngine;

namespace LiminalLabs.GameEvents.Demo
{
    /// <summary>
    /// A world-space sign showing live raise counts and listener counts straight off
    /// the event assets — the runtime diagnostics surface, visible without opening
    /// any editor window.
    /// </summary>
    [RequireComponent(typeof(TextMesh))]
    public class DemoRaiseCounterSign : MonoBehaviour
    {
        [SerializeField, Tooltip("Events to report, one line each.")]
        private GameEventBase[] events;

        private TextMesh textMesh;
        private readonly StringBuilder builder = new StringBuilder(128);
        private float nextRefresh;

        void Awake()
        {
            textMesh = GetComponent<TextMesh>();
        }

        void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.25f;

            builder.Length = 0;
            builder.AppendLine("live from the event assets");
            foreach (GameEventBase gameEvent in events)
            {
                if (gameEvent == null) continue;
                builder.Append(gameEvent.name)
                    .Append("  raised ").Append(gameEvent.TotalRaiseCount)
                    .Append("x  ").Append(gameEvent.ListenerCount).AppendLine(" listeners");
            }
            textMesh.text = builder.ToString();
        }
    }
}
