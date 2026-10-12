using UnityEngine;
using UnityEngine.InputSystem;

namespace TunnelFlight
{
    // Roll, pitch and boost. Bindings are editable in the inspector; defaults are added when empty.
    public sealed class ShipInput : MonoBehaviour
    {
        [SerializeField] InputAction roll = new InputAction("Roll", InputActionType.Value, expectedControlType: "Axis");
        [SerializeField] InputAction pitch = new InputAction("Pitch", InputActionType.Value, expectedControlType: "Axis");
        [SerializeField] InputAction boost = new InputAction("Boost", InputActionType.Button);
        [Tooltip("Debug restart until death handling is designed.")]
        [SerializeField] InputAction restart = new InputAction("Restart", InputActionType.Button);

        [Header("Keyboard")]
        [Tooltip("Seconds for a held key to reach full deflection, so taps give small corrections. Sticks are unaffected.")]
        [SerializeField, Range(0f, 1f)] float keyRampTime = 0.3f;
        [Tooltip("Full pitch from keys, as a fraction of full stick.")]
        [SerializeField, Range(0.1f, 1f)] float keyPitchScale = 0.7f;
        [Tooltip("Full roll from keys, as a fraction of full stick.")]
        [SerializeField, Range(0.1f, 1f)] float keyRollScale = 1f;

        float rollKeys, pitchKeys;

        public float Roll => roll.ReadValue<float>();
        public float Pitch => pitch.ReadValue<float>();

        // Steering for one simulation step. Keys ramp toward full deflection; releasing them stops at once.
        public void ReadSteering(float dt, out float rollValue, out float pitchValue)
        {
            rollValue = Axis(roll, ref rollKeys, keyRollScale, dt);
            pitchValue = Axis(pitch, ref pitchKeys, keyPitchScale, dt);
        }

        float Axis(InputAction action, ref float keys, float scale, float dt)
        {
            float raw = action.ReadValue<float>();
            if (!(action.activeControl?.device is Keyboard))
            {
                keys = 0f;
                return raw;
            }
            float target = raw * scale;
            if (Mathf.Sign(target) != Mathf.Sign(keys)) keys = 0f;
            keys = keyRampTime <= 0f ? target : Mathf.MoveTowards(keys, target, scale * dt / keyRampTime);
            return keys;
        }
        public bool BoostHeld => boost.IsPressed();
        public bool RestartPressed => restart.WasPressedThisFrame();

        void Awake()
        {
            if (roll.bindings.Count == 0)
            {
                roll.AddBinding("<Gamepad>/leftStick/x");
                roll.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/a").With("Positive", "<Keyboard>/d");
                roll.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/leftArrow").With("Positive", "<Keyboard>/rightArrow");
            }
            if (pitch.bindings.Count == 0)
            {
                pitch.AddBinding("<Gamepad>/leftStick/y");
                pitch.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/s").With("Positive", "<Keyboard>/w");
                pitch.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/downArrow").With("Positive", "<Keyboard>/upArrow");
            }
            if (boost.bindings.Count == 0)
            {
                boost.AddBinding("<Gamepad>/rightTrigger");
                boost.AddBinding("<Gamepad>/buttonSouth");
                boost.AddBinding("<Keyboard>/space");
            }
            if (restart.bindings.Count == 0)
            {
                restart.AddBinding("<Gamepad>/start");
                restart.AddBinding("<Keyboard>/r");
            }
        }

        void OnEnable()
        {
            roll.Enable();
            pitch.Enable();
            boost.Enable();
            restart.Enable();
        }

        void OnDisable()
        {
            roll.Disable();
            pitch.Disable();
            boost.Disable();
            restart.Disable();
        }
    }
}
