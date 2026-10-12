using UnityEngine;

namespace TunnelFlight
{
    [CreateAssetMenu(menuName = "Tunnel Flight/Wall Settings")]
    public sealed class WallSettings : ScriptableObject
    {
        [Header("Bands (normal speed vn = speed x sin(incidence), m/s)")]
        [Tooltip("Below this, contact is a scrape (A).")]
        public float heavyHitThreshold = 6f;

        [Tooltip("At or above this, contact is fatal (B).")]
        public float fatalThreshold = 20f;

        [Header("Scrape")]
        [Tooltip("Health lost per second of contact, per m/s of normal speed.")]
        public float scrapeDamagePerNormalSpeed = 4f;

        [Tooltip("Health lost per second of contact regardless of angle.")]
        public float scrapeDamagePerSecond = 2f;

        [Tooltip("Fraction of speed lost per second while scraping.")]
        [Range(0f, 1f)] public float scrapeFriction = 0.15f;

        [Header("Heavy hit [Proposed]")]
        public float heavyHitDamage = 25f;

        [Tooltip("Fraction of speed kept after a heavy hit.")]
        [Range(0f, 1f)] public float heavyHitSpeedRetained = 0.6f;

        [Tooltip("How much of the normal velocity bounces back. 0 = slide, 1 = mirror.")]
        [Range(0f, 1f)] public float heavyHitRestitution = 0.3f;

        [Header("Health")]
        public float maxHealth = 100f;
    }
}
