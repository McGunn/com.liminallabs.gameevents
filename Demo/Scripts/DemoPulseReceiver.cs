using UnityEngine;

namespace LiminalLabs.GameEvents.Demo
{
    /// <summary>
    /// Reacts to a pulse event with a scale punch and a white flash — a
    /// <see cref="GameEventReceiver"/>, so the subscription lifecycle is inherited
    /// and this class is purely its reaction. The per-instance delay turns a row of
    /// receivers into a visible wave, making a single Raise() unmistakable.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class DemoPulseReceiver : GameEventReceiver
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

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

        protected override void OnEventRaised()
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
