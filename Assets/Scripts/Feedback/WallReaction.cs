using UnityEngine;

namespace TunnelFlight
{
    // The wall reacts to the ship: the grid around the wall point nearest the ship takes the danger colour and
    // glows with ripples. The spot tightens, brightens and ripples faster as the gap closes.
    // Drives shader globals read by Additive Glow materials with React To Nearest Wall Spot on (the tunnel grid).
    public sealed class WallReaction : MonoBehaviour
    {
        [SerializeField] Ship ship;

        [Header("Range")]
        [Tooltip("The wall starts reacting when the nearest wall is closer than this, metres.")]
        [SerializeField, Min(0.1f)] float showBelow = 5f;

        [Header("Spot")]
        [Tooltip("Spot radius, metres, at the wall and at Show Below.")]
        [SerializeField] Vector2 radiusNearFar = new Vector2(1.6f, 4f);
        [Tooltip("Colour by distance to the wall, spanning 0 to Show Below.")]
        [SerializeField] Gradient dangerColor = HudColors.Danger();
        [Tooltip("HDR multiplier on the reaction colour.")]
        [SerializeField, Min(0f)] float brightness = 3f;
        [Tooltip("How much colour fills the cells between grid lines (0 tints only the lines).")]
        [SerializeField, Range(0f, 1f)] float fill = 0.35f;
        [Tooltip("Strength by closeness (0 = at Show Below, 1 = touching).")]
        [SerializeField] AnimationCurve strengthByCloseness = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 2f, 0f));

        [Header("Ripple")]
        [Tooltip("Rings per metre.")]
        [SerializeField, Min(0f)] float rippleFrequency = 1.2f;
        [Tooltip("Ripple speed outward from the spot, m/s, far and at the wall.")]
        [SerializeField] Vector2 rippleSpeedFarNear = new Vector2(1f, 6f);

        [Tooltip("Seconds to smooth the spot as the nearest wall changes, so it glides rather than jumps.")]
        [SerializeField, Range(0f, 0.3f)] float smoothing = 0.05f;

        static readonly int SpotId = Shader.PropertyToID("_TF_WallSpot");
        static readonly int ColorId = Shader.PropertyToID("_TF_WallSpotColor");
        static readonly int RippleId = Shader.PropertyToID("_TF_WallSpotRipple");

        Vector3 spot;
        bool hasSpot;

        void OnDisable()
        {
            hasSpot = false;
            Shader.SetGlobalVector(SpotId, Vector4.zero);
        }

        void LateUpdate()
        {
            if (ship == null || !ship.Running || !ship.Sensor.HasNearest)
            {
                OnDisable();
                return;
            }

            WallProbeSample nearest = ship.Sensor.Nearest;
            float clearance = Mathf.Max(0f, nearest.wall.clearance);
            float closeness = 1f - Mathf.Clamp01(clearance / showBelow);
            float strength = Mathf.Clamp01(strengthByCloseness.Evaluate(closeness));

            Vector3 target = nearest.wallPoint;
            spot = !hasSpot || smoothing <= 0f ? target : Vector3.Lerp(spot, target, 1f - Mathf.Exp(-Time.deltaTime / smoothing));
            hasSpot = true;

            float radius = Mathf.Lerp(radiusNearFar.y, radiusNearFar.x, closeness);
            Color color = dangerColor.Evaluate(1f - closeness) * brightness;
            Shader.SetGlobalVector(SpotId, new Vector4(spot.x, spot.y, spot.z, strength > 0f ? radius : 0f));
            Shader.SetGlobalVector(ColorId, new Vector4(color.r, color.g, color.b, strength));
            Shader.SetGlobalVector(RippleId, new Vector4(rippleFrequency, Mathf.Lerp(rippleSpeedFarNear.x, rippleSpeedFarNear.y, closeness), fill, 0f));
        }
    }
}
