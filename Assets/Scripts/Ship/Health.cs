using System;
using UnityEngine;

namespace TunnelFlight
{
    // No regeneration yet; whether there is any is an open question in the design.
    public sealed class Health
    {
        public float Max { get; private set; }
        public float Current { get; private set; }
        public bool IsDead => Current <= 0f;

        public event Action<float> Damaged;
        public event Action Died;

        public void Reset(float max)
        {
            Max = max;
            Current = max;
        }

        public void Damage(float amount)
        {
            if (IsDead || amount <= 0f) return;
            Current = Mathf.Max(0f, Current - amount);
            Damaged?.Invoke(amount);
            if (IsDead) Died?.Invoke();
        }
    }
}
