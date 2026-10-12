using UnityEngine;

namespace TunnelFlight
{
    // Glowing outline of the tunnel's cross-section at the ship, drawn just inside the walls. The arc facing the
    // nearest wall brightens and takes the danger colour as it closes in, so where you sit in the cross-section
    // reads at a glance. Uses the same wall query as collision.
    [DefaultExecutionOrder(100)] // After the ship and camera have moved.
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class CrossSectionRing : MonoBehaviour
    {
        [SerializeField] Ship ship;
        [SerializeField] Camera viewCamera;
        [Tooltip("Used to push the ring ahead in first person, where a ring at the eye would be out of view.")]
        [SerializeField] TunnelCamera tunnelCamera;

        [Header("Placement")]
        [Tooltip("Ring position relative to the ship along the tunnel, metres.")]
        [SerializeField] float offsetAhead = 0f;
        [SerializeField] float firstPersonOffsetAhead = 6f;
        [Tooltip("Inset from the wall, metres.")]
        [SerializeField, Min(0f)] float inset = 0.03f;
        [SerializeField, Range(16, 128)] int segments = 72;

        [Header("Look")]
        [SerializeField] Color baseColor = new Color(1f, 1f, 1f, 0.85f);
        [SerializeField, Min(0f)] float brightness = 2.5f;
        [Tooltip("Line width, metres.")]
        [SerializeField, Min(0.001f)] float width = 0.1f;

        [Header("Nearest wall highlight")]
        [Tooltip("Half-width of the highlighted arc around the nearest wall, degrees.")]
        [SerializeField, Range(5f, 180f)] float highlightSpread = 45f;
        [Tooltip("Colour by distance to the wall, spanning 0 to Gradient Range metres.")]
        [SerializeField] Gradient dangerColor = HudColors.Danger();
        [SerializeField, Min(0.1f)] float gradientRange = 5f;
        [SerializeField, Min(0f)] float highlightBrightness = 2.5f;
        [Tooltip("Highlighted arc is drawn this much wider, as a multiple of Width.")]
        [SerializeField, Min(1f)] float highlightWidth = 1.8f;

        GlowLineMesh lines;

        void Awake()
        {
            lines = new GlowLineMesh();
            GetComponent<MeshFilter>().sharedMesh = lines.Mesh;
        }

        void OnDisable()
        {
            if (lines == null) return;
            lines.Begin(Vector3.zero);
            lines.End();
        }

        void OnDestroy() => lines?.Destroy();

        void LateUpdate()
        {
            if (lines == null) return;
            if (viewCamera == null) viewCamera = Camera.main;
            if (viewCamera == null) return;
            lines.Begin(viewCamera.transform.position);
            if (ship != null && ship.Running && ship.Tunnel != null && ship.Tunnel.IsReady) Draw(ship.Tunnel);
            lines.End();
        }

        void Draw(ITunnel tunnel)
        {
            bool firstPerson = tunnelCamera != null && tunnelCamera.FirstPerson;
            float s = Mathf.Clamp(ship.RenderS + (firstPerson ? firstPersonOffsetAhead : offsetAhead), 0f, tunnel.Length);
            TunnelFrame frame = tunnel.GetFrame(s);

            // Highlight toward the nearest wall, in cross-section coordinates.
            Vector2 toWall = Vector2.zero;
            float proximity = 0f;
            Color danger = baseColor;
            if (ship.Sensor.HasNearest)
            {
                WallProbeSample nearest = ship.Sensor.Nearest;
                toWall = nearest.wall.normal;
                float clearance = Mathf.Max(0f, nearest.wall.clearance);
                proximity = 1f - Mathf.Clamp01(clearance / gradientRange);
                danger = dangerColor.Evaluate(Mathf.Clamp01(clearance / gradientRange));
            }
            float cosSpread = Mathf.Cos(highlightSpread * Mathf.Deg2Rad);

            Vector3 previous = default;
            float previousWeight = 0f;
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                var direction = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
                float radius = tunnel.QueryWall(new TunnelPoint { s = s, local = direction }).wallRadius;
                Vector3 point = frame.LocalToWorld(direction * Mathf.Max(0f, radius - inset));

                // 1 at the nearest wall, falling to 0 at the edge of the spread.
                float facing = toWall == Vector2.zero ? 0f : Vector2.Dot(direction, toWall.normalized);
                float weight = proximity * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(cosSpread, 1f, facing));

                if (i > 0)
                {
                    float w = 0.5f * (weight + previousWeight);
                    Color color = Color.Lerp(baseColor * brightness, danger * highlightBrightness, w);
                    color.a = Mathf.Lerp(baseColor.a, 1f, w);
                    lines.Line(previous, point, width * Mathf.Lerp(1f, highlightWidth, w), color);
                }
                previous = point;
                previousWeight = weight;
            }
        }
    }
}
