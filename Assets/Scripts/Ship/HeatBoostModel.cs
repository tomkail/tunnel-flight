using System;
using UnityEngine;

namespace TunnelFlight
{
    // Speed, heat and boost. Plain C# so it can be stepped in tests and replays.
    // Speed runs on game time; heat runs on real time and never pauses.
    public sealed class HeatBoostModel
    {
        public const float MaxHeat = 100f;

        public float Speed { get; private set; }
        public float Heat { get; private set; }
        public bool Boosting { get; private set; }
        public bool ForcedLock { get; private set; }

        public event Action ForcedLockStarted;
        public event Action ForcedLockEnded;

        public void Reset(HeatBoostSettings settings, float heat = 0f)
        {
            Speed = settings.normalMaxSpeed;
            Heat = Mathf.Clamp(heat, 0f, MaxHeat);
            Boosting = false;
            ForcedLock = false;
        }

        public void Step(HeatBoostSettings settings, bool boostHeld, float dt, float realDt)
        {
            // Boost needs fuel. A held boost continues down to 0 but needs a little heat to start.
            bool wantsBoost = ForcedLock || boostHeld;
            float startThreshold = Boosting || ForcedLock ? 0f : settings.minHeatToStartBoost;
            Boosting = wantsBoost && Heat > startThreshold;

            float speedAboveMax = Mathf.Max(0f, Speed / settings.normalMaxSpeed - 1f);
            float fill = settings.heatFillPerSecond * (1f + settings.heatFillPerSpeedAboveMax * speedAboveMax);
            Heat += fill * realDt;
            if (Boosting) Heat -= settings.boostHeatDrainPerSecond * realDt;
            Heat = Mathf.Clamp(Heat, 0f, MaxHeat);

            if (!ForcedLock && Heat >= MaxHeat)
            {
                ForcedLock = true;
                Boosting = true;
                ForcedLockStarted?.Invoke();
            }
            else if (ForcedLock && Heat <= 0f)
            {
                ForcedLock = false;
                Boosting = false;
                ForcedLockEnded?.Invoke();
            }

            float vMax = settings.normalMaxSpeed;
            if (Boosting)
                Speed += (settings.BoostSpeed - Speed) * (1f - Mathf.Exp(-settings.boostResponse * dt));
            else if (Speed > vMax)
                Speed = vMax + (Speed - vMax) * Mathf.Exp(-dt / settings.coastTimeConstant);
            else
                Speed += (vMax - Speed) * (1f - Mathf.Exp(-settings.recoverResponse * dt));
        }

        public void AddHeat(float amount) => Heat = Mathf.Clamp(Heat + amount, 0f, MaxHeat);

        public void ScaleSpeed(float factor) => Speed = Mathf.Max(0f, Speed * factor);
    }
}
