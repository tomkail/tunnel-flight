using UnityEngine;

namespace TunnelFlight
{
    // Ship light that flares as the nearest wall closes in. Keep the light's range short so only nearby walls light up.
    [RequireComponent(typeof(Light))]
    public sealed class ProximityLight : MonoBehaviour
    {
        [SerializeField] Ship ship;
        [Tooltip("Intensity by clearance to the nearest wall, metres.")]
        [SerializeField] AnimationCurve intensityByClearance = new AnimationCurve(new Keyframe(0f, 8f), new Keyframe(3f, 1f));
        [Tooltip("Radius, metres, of the glow the ship casts on the tunnel grid (shaders with Brighten Near Ship on).")]
        [SerializeField, Min(0f)] float gridGlowRadius = 7f;
        [Tooltip("Grid brightness multiplier at the ship, by clearance to the nearest wall, metres.")]
        [SerializeField] AnimationCurve gridGlowByClearance = new AnimationCurve(new Keyframe(0f, 4f), new Keyframe(4f, 1f));

        static readonly int ShipPositionId = Shader.PropertyToID("_TF_ShipPosition");
        static readonly int ShipGlowId = Shader.PropertyToID("_TF_ShipGlow");

        Light shipLight;

        void Awake() => shipLight = GetComponent<Light>();

        void LateUpdate()
        {
            if (ship == null || !ship.Sensor.HasNearest) return;
            float clearance = Mathf.Max(0f, ship.Sensor.Nearest.wall.clearance);
            shipLight.intensity = intensityByClearance.Evaluate(clearance);
            Shader.SetGlobalVector(ShipPositionId, ship.transform.position);
            Shader.SetGlobalVector(ShipGlowId, new Vector4(gridGlowRadius, gridGlowByClearance.Evaluate(clearance), 0f, 0f));
        }
    }
}
