using UnityEngine;

namespace TunnelFlight
{
    public enum WallHitKind { None, Scrape, Heavy, Fatal }

    public struct WallHit
    {
        public WallHitKind kind;
        // Velocity component into the wall, m/s.
        public float normalSpeed;
        public float damage;
        public Vector3 point;
        public Vector3 normal;
    }

    // Classifies wall contact by normal speed and resolves it. One number, vn, decides scrape, heavy hit or death.
    public static class WallResponse
    {
        public static WallHitKind Classify(WallSettings settings, float normalSpeed)
        {
            if (normalSpeed >= settings.fatalThreshold) return WallHitKind.Fatal;
            if (normalSpeed >= settings.heavyHitThreshold) return WallHitKind.Heavy;
            return WallHitKind.Scrape;
        }

        // Pushes the ship back inside, redirects its heading and returns the hit. Speed changes go through speedScale.
        public static WallHit Resolve(WallSettings settings, FlightModel flight, float speed, in WallProbeSample sample, float dt, out float speedScale)
        {
            speedScale = 1f;
            if (!sample.Penetrating) return default;

            Vector3 n = sample.worldNormal;
            Vector3 velocity = flight.Forward * speed;
            float vn = Mathf.Max(0f, Vector3.Dot(velocity, n));

            flight.Position -= n * -sample.wall.clearance;

            var hit = new WallHit
            {
                kind = Classify(settings, vn),
                normalSpeed = vn,
                point = sample.wallPoint,
                normal = n,
            };

            switch (hit.kind)
            {
                case WallHitKind.Scrape:
                {
                    hit.damage = (settings.scrapeDamagePerNormalSpeed * vn + settings.scrapeDamagePerSecond) * dt;
                    Vector3 tangential = velocity - vn * n;
                    flight.SetHeading(tangential);
                    float keep = speed > 0f ? tangential.magnitude / speed : 1f;
                    speedScale = keep * Mathf.Max(0f, 1f - settings.scrapeFriction * dt);
                    break;
                }
                case WallHitKind.Heavy:
                {
                    hit.damage = settings.heavyHitDamage;
                    flight.SetHeading(velocity - (1f + settings.heavyHitRestitution) * vn * n);
                    speedScale = settings.heavyHitSpeedRetained;
                    break;
                }
                case WallHitKind.Fatal:
                    hit.damage = float.PositiveInfinity;
                    speedScale = 0f;
                    break;
            }

            return hit;
        }
    }
}
