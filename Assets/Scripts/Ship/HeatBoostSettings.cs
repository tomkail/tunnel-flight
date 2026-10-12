using UnityEngine;

namespace TunnelFlight
{
    [CreateAssetMenu(menuName = "Tunnel Flight/Heat Boost Settings")]
    public sealed class HeatBoostSettings : ScriptableObject
    {
        [Header("Speed")]
        [Tooltip("Normal max speed, m/s. Cruise speed when not boosting.")]
        public float normalMaxSpeed = 50f;

        [Tooltip("Boost target speed as a multiple of normal max.")]
        public float boostSpeedMultiplier = 1.7f;

        [Tooltip("How fast speed approaches the boost target while boosting, 1/s (kUp).")]
        public float boostResponse = 4f;

        [Tooltip("Time constant of the exponential decay from above normal max back to it, seconds (tau).")]
        [Min(0.01f)] public float coastTimeConstant = 2f;

        [Tooltip("How fast speed recovers to normal max after losing speed (e.g. a heavy hit), 1/s.")]
        public float recoverResponse = 1.5f;

        [Header("Heat (real time, percent)")]
        [Tooltip("Base heat fill, percent per second.")]
        public float heatFillPerSecond = 6f;

        [Tooltip("Extra fill per unit of speed ratio above normal max. 0.5 fills 50% faster at 2x normal max. [Proposed]")]
        public float heatFillPerSpeedAboveMax = 0.5f;

        [Tooltip("Heat drained while boosting, percent per second.")]
        public float boostHeatDrainPerSecond = 25f;

        [Tooltip("Heat needed to start a voluntary boost. Stops boost flickering on and off at near-zero heat.")]
        [Range(0f, 100f)] public float minHeatToStartBoost = 5f;

        public float BoostSpeed => normalMaxSpeed * boostSpeedMultiplier;
    }
}
