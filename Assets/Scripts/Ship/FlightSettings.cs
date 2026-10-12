using UnityEngine;

namespace TunnelFlight
{
    [CreateAssetMenu(menuName = "Tunnel Flight/Flight Settings")]
    public sealed class FlightSettings : ScriptableObject
    {
        [Tooltip("Roll rate at full input, degrees per second. Fixed per second, so turn per metre falls with speed.")]
        public float rollRate = 220f;

        [Tooltip("Pitch rate at full input, degrees per second.")]
        public float pitchRate = 110f;

        [Tooltip("Time for the rotation rate to reach the input target, seconds. 0 = instant.")]
        [Min(0f)] public float rateResponseTime = 0.08f;

        [Tooltip("Stick up is nose down (flight-sim style). Off: stick up is nose up.")]
        public bool invertPitch = true;
    }
}
