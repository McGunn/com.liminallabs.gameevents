using UnityEngine;

namespace LiminalLabs.GameEvents.Demo
{
    /// <summary>
    /// A lamp lit or dark from a bool. Two wiring styles, same component:
    /// assign <see cref="lampsEvent"/> and it subscribes itself in code, or leave it
    /// empty and drive <see cref="SetLit"/> from a BoolGameEventListener's UnityEvent
    /// — the designer path, no code involved. The demo shows one of each.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class DemoLamp : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField, Tooltip("Optional: subscribe directly to this event. Leave empty when a Bool Event Listener component drives SetLit instead.")]
        private BoolGameEvent lampsEvent;

        [SerializeField] private bool startLit = true;
        [SerializeField] private Color litColor = new Color(1f, 0.85f, 0.35f);
        [SerializeField] private Color unlitColor = new Color(0.16f, 0.16f, 0.18f);
        [SerializeField, Min(0.1f)] private float blendSpeed = 8f;

        private MeshRenderer meshRenderer;
        private MaterialPropertyBlock block;
        private float litLevel;
        private bool lit;

        void Awake()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            block = new MaterialPropertyBlock();
            lit = startLit;
            litLevel = lit ? 1f : 0f;
            Apply();
        }

        void OnEnable()
        {
            if (lampsEvent != null) lampsEvent.Subscribe(SetLit);
        }

        void OnDisable()
        {
            if (lampsEvent != null) lampsEvent.Unsubscribe(SetLit);
        }

        /// <summary>Public so a BoolGameEventListener (or anything else) can drive it.</summary>
        public void SetLit(bool value)
        {
            lit = value;
        }

        void Update()
        {
            float target = lit ? 1f : 0f;
            if (Mathf.Approximately(litLevel, target)) return;
            litLevel = Mathf.MoveTowards(litLevel, target, Time.deltaTime * blendSpeed);
            Apply();
        }

        private void Apply()
        {
            block.SetColor(BaseColor, Color.Lerp(unlitColor, litColor, litLevel));
            meshRenderer.SetPropertyBlock(block);
        }
    }
}
