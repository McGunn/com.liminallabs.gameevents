using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LiminalLabs.GameEvents.Demo
{
    /// <summary>
    /// The demo's only raiser. It references three event ASSETS and zero scene
    /// objects — every reaction in the scene comes from things it has never heard of.
    /// [1] pulse, [2] hue shift (random payload), [3] lamps toggle; pulse also fires
    /// on a timer so the scene animates hands-off.
    /// </summary>
    public class DemoEventBroadcaster : MonoBehaviour
    {
        [SerializeField, Tooltip("Raised by [1] and by the auto-pulse timer.")]
        private GameEvent pulseEvent;

        [SerializeField, Tooltip("Raised by [2] with a random hue 0–1.")]
        private FloatGameEvent hueEvent;

        [SerializeField, Tooltip("Raised by [3] with the new lamps state.")]
        private BoolGameEvent lampsEvent;

        [SerializeField, Min(0f), Tooltip("Seconds between automatic pulses (0 = manual only).")]
        private float autoPulseInterval = 4f;

        [SerializeField, Tooltip("Lamp state broadcast on the first [3] press (toggles from here).")]
        private bool lampsStartOn = true;

        private float nextAutoPulse;
        private bool lampsOn;

        void OnEnable()
        {
            lampsOn = lampsStartOn;
            nextAutoPulse = Time.time + autoPulseInterval;
        }

        void Update()
        {
            if (DigitPressed(1)) RaisePulse();
            if (DigitPressed(2) && hueEvent != null) hueEvent.Raise(Random.value);
            if (DigitPressed(3) && lampsEvent != null)
            {
                lampsOn = !lampsOn;
                lampsEvent.Raise(lampsOn);
            }

            if (autoPulseInterval > 0f && Time.time >= nextAutoPulse)
            {
                nextAutoPulse = Time.time + autoPulseInterval;
                RaisePulse();
            }
        }

        private void RaisePulse()
        {
            if (pulseEvent != null) pulseEvent.Raise();
        }

        private static bool DigitPressed(int digit)
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return false;
            switch (digit)
            {
                case 1: return keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame;
                case 2: return keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame;
                case 3: return keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame;
                default: return false;
            }
#else
            return Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha0 + digit));
#endif
        }
    }
}
