using UnityEngine;

namespace TunnelFlight
{
    // Glowing ribbons off the wingtips. They mark the ship's widest points, and get brighter with speed.
    public sealed class WingtipTrails : MonoBehaviour
    {
        [SerializeField] Ship ship;
        [SerializeField] MantaShipMesh hull;
        [SerializeField] TrailRenderer leftTrail;
        [SerializeField] TrailRenderer rightTrail;

        [Header("Look")]
        [Tooltip("Ribbon lifetime, seconds. Length on screen is roughly speed x time.")]
        [SerializeField, Range(0.02f, 1f)] float time = 0.18f;
        [SerializeField, Min(0f)] float width = 0.06f;
        [SerializeField] AnimationCurve widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
        [SerializeField, ColorUsage(true, true)] Color color = new Color(0.25f, 0.95f, 1f, 1f);

        [Header("Speed response")]
        [Tooltip("Alpha at normal max speed and at boost max speed.")]
        [SerializeField] Vector2 alphaBySpeed = new Vector2(0.45f, 1f);
        [Tooltip("Width multiplier at boost max speed.")]
        [SerializeField, Min(0f)] float boostWidthScale = 1.4f;

        float speedOverride = -1f;

        void Awake() => PlaceAtTips();

        // Force the speed response (0 = normal max, 1 = boost max) for previews. Negative = off.
        public void SetSpeedOverride(float t) => speedOverride = t;

        void OnEnable()
        {
            if (ship != null) ship.Restarted += Clear;
        }

        void OnDisable()
        {
            if (ship != null) ship.Restarted -= Clear;
        }

        public void PlaceAtTips()
        {
            if (hull == null) return;
            if (leftTrail != null) leftTrail.transform.position = hull.transform.TransformPoint(hull.LeftTip);
            if (rightTrail != null) rightTrail.transform.position = hull.transform.TransformPoint(hull.RightTip);
        }

        public void Clear()
        {
            if (leftTrail != null) leftTrail.Clear();
            if (rightTrail != null) rightTrail.Clear();
        }

        void LateUpdate() => Refresh();

        public void Refresh()
        {
            float speedT = 0f;
            bool emitting = true;
            if (speedOverride >= 0f) speedT = speedOverride;
            else if (ship != null)
            {
                HeatBoostSettings s = ship.HeatBoostSettings;
                speedT = s != null ? Mathf.InverseLerp(s.normalMaxSpeed, s.BoostSpeed, ship.HeatBoost.Speed) : 0f;
                emitting = ship.Running;
            }

            Color start = color;
            start.a *= Mathf.Lerp(alphaBySpeed.x, alphaBySpeed.y, speedT);
            Color end = start;
            end.a = 0f;
            float w = width * Mathf.Lerp(1f, boostWidthScale, speedT);
            Apply(leftTrail, start, end, w, emitting);
            Apply(rightTrail, start, end, w, emitting);
        }

        void Apply(TrailRenderer trail, Color start, Color end, float w, bool emitting)
        {
            if (trail == null) return;
            trail.time = time;
            trail.widthMultiplier = w;
            trail.widthCurve = widthCurve;
            trail.startColor = start;
            trail.endColor = end;
            trail.emitting = emitting;
        }
    }
}
