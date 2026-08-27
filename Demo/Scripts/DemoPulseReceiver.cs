using UnityEngine;

namespace LiminalLabs.GameEvents.Demo
{
    /// <summary>
    /// Reacts to a pulse event with a scale punch and a white flash — subscribed in
    /// code, referencing only the event asset. The per-instance delay turns a row of
    /// receivers into a visible wave, making a single Raise() unmistakable.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class DemoPulseReceiver : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField, Tooltip("Event that makes this object jump.")]
        private GameEvent pulseEvent;

        [SerializeField, Tooltip("Resting color of this receiver.")]
        private Color baseColor = new Color(0.25f, 0.55f, 0.85f);

        [SerializeField, Min(0f), Tooltip("Seconds after the raise before this instance reacts (stagger for a wave).")]
        private float reactionDelay = 0f;

        [SerializeField, Min(1f)] private float punchScale = 1.45f;
        [SerializeField, Min(0.1f)] private float recoverSpeed = 6f;

        private MeshRenderer meshRenderer;
        private MaterialPropertyBlock block;
        private Vector3 restScale;
        private float punchLevel;                 // 1 at the punch, decays to 0
        private float pendingAt = float.PositiveInfinity;

        void Awake()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            block = new MaterialPropertyBlock();
            restScale = transform.localScale;
            ApplyVisual();
        }

        void OnEnable()
        {
            if (pulseEvent != null) pulseEvent.Subscribe(OnPulse);
        }

        void OnDisable()
        {
            if (pulseEvent != null) pulseEvent.Unsubscribe(OnPulse);
        }

        private void OnPulse()
        {
            pendingAt = Time.time + reactionDelay;
        }

        void Update()
        {
            if (Time.time >= pendingAt)
            {
                pendingAt = float.PositiveInfinity;
                punchLevel = 1f;
            }
            if (punchLevel > 0f)
            {
                punchLevel = Mathf.Max(0f, punchLevel - Time.deltaTime * recoverSpeed / punchScale);
                ApplyVisual();
            }
        }

        private void ApplyVisual()
        {
            transform.localScale = restScale * Mathf.LerpUnclamped(1f, punchScale, punchLevel);
            block.SetColor(BaseColor, Color.Lerp(baseColor, Color.white, punchLevel));
            meshRenderer.SetPropertyBlock(block);
        }
    }
}
