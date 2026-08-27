using UnityEngine;

namespace LiminalLabs.GameEvents.Demo
{
    /// <summary>
    /// Reacts to a float event by blending to the broadcast hue (plus a per-instance
    /// offset, so a family of receivers forms a palette from one payload). Subscribed
    /// in code, referencing only the event asset.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class DemoHueReceiver : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField, Tooltip("Event whose float payload (0–1) is the new hue.")]
        private FloatGameEvent hueEvent;

        [SerializeField, Range(0f, 1f), Tooltip("Added to the broadcast hue so receivers differ.")]
        private float hueOffset = 0f;

        [SerializeField, Range(0f, 1f)] private float startHue = 0.55f;
        [SerializeField, Min(0.1f)] private float blendSpeed = 4f;

        private MeshRenderer meshRenderer;
        private MaterialPropertyBlock block;
        private Color current, target;

        void Awake()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            block = new MaterialPropertyBlock();
            current = target = HueColor(startHue);
            Apply();
        }

        void OnEnable()
        {
            if (hueEvent != null) hueEvent.Subscribe(OnHue);
        }

        void OnDisable()
        {
            if (hueEvent != null) hueEvent.Unsubscribe(OnHue);
        }

        private void OnHue(float hue)
        {
            target = HueColor(hue + hueOffset);
        }

        private Color HueColor(float hue)
        {
            return Color.HSVToRGB(Mathf.Repeat(hue, 1f), 0.65f, 0.9f);
        }

        void Update()
        {
            if (current == target) return;
            current = Color.Lerp(current, target, Time.deltaTime * blendSpeed);
            Apply();
        }

        private void Apply()
        {
            block.SetColor(BaseColor, current);
            meshRenderer.SetPropertyBlock(block);
        }
    }
}
