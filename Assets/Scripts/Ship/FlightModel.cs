using UnityEngine;

namespace TunnelFlight
{
    // Aircraft model: roll about the ship's forward axis, pitch about its local right axis.
    // The heading is real and never auto-aligns to the tunnel.
    public sealed class FlightModel
    {
        public Vector3 Position;
        public Quaternion Rotation = Quaternion.identity;

        // Current angular rates, degrees per second. Positive roll is clockwise seen from behind; positive pitch is nose up.
        public float RollRate { get; private set; }
        public float PitchRate { get; private set; }

        public Vector3 Forward => Rotation * Vector3.forward;

        public void Reset(Vector3 position, Quaternion rotation)
        {
            Position = position;
            Rotation = rotation;
            RollRate = 0f;
            PitchRate = 0f;
        }

        // rollInput and pitchInput are in [-1, 1]. +roll rolls right, +pitch is nose up.
        public void Step(FlightSettings settings, float rollInput, float pitchInput, float speed, float dt)
        {
            float targetRoll = Mathf.Clamp(rollInput, -1f, 1f) * settings.rollRate;
            float targetPitch = Mathf.Clamp(pitchInput, -1f, 1f) * settings.pitchRate * (settings.invertPitch ? -1f : 1f);
            float k = settings.rateResponseTime <= 0f ? 1f : 1f - Mathf.Exp(-dt / settings.rateResponseTime);
            RollRate = Mathf.Lerp(RollRate, targetRoll, k);
            PitchRate = Mathf.Lerp(PitchRate, targetPitch, k);

            // Unity rotations are left-handed: negative angle about forward rolls right, negative about right pitches up.
            Rotation = Rotation
                * Quaternion.AngleAxis(-PitchRate * dt, Vector3.right)
                * Quaternion.AngleAxis(-RollRate * dt, Vector3.forward);
            Rotation = Quaternion.Normalize(Rotation);

            Position += Forward * (speed * dt);
        }

        // Turn the heading onto a new direction with the smallest rotation, keeping roll as close as possible.
        public void SetHeading(Vector3 direction)
        {
            if (direction.sqrMagnitude < 1e-8f) return;
            Rotation = Quaternion.Normalize(Quaternion.FromToRotation(Forward, direction.normalized) * Rotation);
        }
    }
}
