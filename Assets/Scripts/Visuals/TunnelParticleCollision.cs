using UnityEngine;

namespace TunnelFlight
{
    // Bounces a particle system's particles off the tunnel wall, F-Zero style: sparks hit the wall and
    // skitter along it behind the ship instead of flying freely. Uses the tunnel's wall query, not colliders.
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class TunnelParticleCollision : MonoBehaviour
    {
        [SerializeField] Ship ship;

        [Tooltip("Fraction of the into-wall speed that bounces back.")]
        [SerializeField, Range(0f, 1f)] float bounciness = 0.35f;
        [Tooltip("Fraction of along-wall speed kept per bounce. High values skitter, low values stick.")]
        [SerializeField, Range(0f, 1f)] float slide = 0.85f;
        [Tooltip("Fraction of remaining lifetime lost per bounce, so sparks die after a few hits.")]
        [SerializeField, Range(0f, 1f)] float lifetimeLossPerBounce = 0.3f;
        [Tooltip("Push particles this far inside the wall after a bounce, metres.")]
        [SerializeField, Min(0f)] float skin = 0.02f;
        [Tooltip("Cap on particles checked per frame, to bound the cost.")]
        [SerializeField, Min(1)] int maxChecksPerFrame = 1500;

        ParticleSystem system;
        ParticleSystem.Particle[] buffer;

        void Awake()
        {
            system = GetComponent<ParticleSystem>();
            buffer = new ParticleSystem.Particle[system.main.maxParticles];
        }

        void LateUpdate()
        {
            if (ship == null || ship.Tunnel == null || !ship.Tunnel.IsReady) return;
            if (system.main.simulationSpace != ParticleSystemSimulationSpace.World) return;
            if (buffer.Length < system.main.maxParticles) buffer = new ParticleSystem.Particle[system.main.maxParticles];

            int count = system.GetParticles(buffer);
            if (count == 0) return;

            ITunnel tunnel = ship.Tunnel;
            TunnelFrame shipFrame = tunnel.GetFrame(ship.RenderS);
            bool changed = false;

            for (int i = 0; i < count && i < maxChecksPerFrame; i++)
            {
                Vector3 position = buffer[i].position;

                // Particles stay near the ship, so estimate their s along the ship's frame instead of a full projection.
                float s = Mathf.Clamp(ship.RenderS + Vector3.Dot(position - shipFrame.position, shipFrame.Forward), 0f, tunnel.Length);
                TunnelFrame frame = tunnel.GetFrame(s);
                Vector3 offset = position - frame.position;
                var point = new TunnelPoint { s = s, local = new Vector2(Vector3.Dot(offset, frame.Right), Vector3.Dot(offset, frame.Up)) };
                WallSample wall = tunnel.QueryWall(point);
                if (wall.clearance >= 0f) continue;

                Vector3 normal = frame.LocalDirectionToWorld(wall.normal).normalized;
                buffer[i].velocity = Bounce(buffer[i].velocity, normal, bounciness, slide);
                buffer[i].position = position + normal * (wall.clearance - skin);
                buffer[i].remainingLifetime *= 1f - lifetimeLossPerBounce;
                changed = true;
            }

            if (changed) system.SetParticles(buffer, count);
        }

        // Reflects the outward (into-wall) component of velocity and scales the along-wall component.
        // normal is the wall's outward normal.
        public static Vector3 Bounce(Vector3 velocity, Vector3 normal, float bounciness, float slide)
        {
            float into = Vector3.Dot(velocity, normal);
            if (into <= 0f) return velocity * slide;
            Vector3 along = velocity - into * normal;
            return along * slide - normal * (into * bounciness);
        }
    }
}
