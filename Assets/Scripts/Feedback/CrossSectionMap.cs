using UnityEngine;

namespace TunnelFlight
{
    // Screen-space cross-section indicator: the tunnel's profile at the ship, the profile further ahead (dimmer,
    // showing upcoming shape and twist), the ship as a bar with a fin for its up, and a line to the nearest wall in
    // the danger colour. Drawn a metre in front of the camera with a material that ignores depth.
    [DefaultExecutionOrder(100)] // After the camera has moved.
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class CrossSectionMap : MonoBehaviour
    {
        [SerializeField] Ship ship;
        [SerializeField] Camera viewCamera;
        [Tooltip("Wingtips for the ship marker.")]
        [SerializeField] MantaShipMesh hull;

        [Header("Placement")]
        [Tooltip("Map centre in viewport coordinates. Off the centre line, where the floor stripe runs; Panini bends it a little at boost.")]
        [SerializeField] Vector2 viewportCentre = new Vector2(0.86f, 0.18f);
        [Tooltip("Map radius as a fraction of screen height.")]
        [SerializeField, Range(0.02f, 0.3f)] float screenRadius = 0.09f;
        [Tooltip("Rotate the map with the view, so left on the map is left on screen. Off: the tunnel's frame stays upright.")]
        [SerializeField] bool alignToView = true;

        [Header("Content")]
        [Tooltip("Second, dimmer profile this far ahead, metres. 0 hides it.")]
        [SerializeField, Min(0f)] float lookAhead = 40f;
        [SerializeField, Range(16, 96)] int segments = 48;

        [Header("Look")]
        [SerializeField] Color outlineColor = new Color(0.3f, 0.9f, 1f, 1f);
        [SerializeField] Color aheadColor = new Color(0.3f, 0.9f, 1f, 0.35f);
        [SerializeField] Color shipColor = Color.white;
        [SerializeField] Color floorColor = new Color(1f, 0.55f, 0.15f, 1f);
        [SerializeField] Gradient dangerColor = HudColors.Danger();
        [SerializeField, Min(0.1f)] float gradientRange = 5f;
        [SerializeField, Min(0f)] float brightness = 2.4f;
        [Tooltip("Line width as a fraction of screen height.")]
        [SerializeField, Min(0.0005f)] float lineWidth = 0.003f;

        const float Depth = 1f;

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
            Transform cam = viewCamera.transform;
            float screenHeight = 2f * Depth * Mathf.Tan(viewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float radius = screenRadius * screenHeight;
            float width = lineWidth * screenHeight;
            Vector3 centre = viewCamera.ViewportToWorldPoint(new Vector3(viewportCentre.x, viewportCentre.y, Depth));

            float s = Mathf.Clamp(ship.RenderS, 0f, tunnel.Length);
            TunnelFrame frame = tunnel.GetFrame(s);
            Vector3 axisRight = alignToView ? cam.right : frame.Right;
            Vector3 axisUp = alignToView ? cam.up : frame.Up;

            // Profile ahead, re-expressed in this frame with its relative twist, so shape and roll changes show.
            float sAhead = Mathf.Min(s + lookAhead, tunnel.Length);
            TunnelFrame ahead = tunnel.GetFrame(sAhead);
            Vector3 aheadUp = Vector3.ProjectOnPlane(ahead.Up, frame.Forward);
            float twist = aheadUp.sqrMagnitude > 1e-6f ? Vector3.SignedAngle(frame.Up, aheadUp, frame.Forward) : 0f;
            Quaternion twistRotation = Quaternion.AngleAxis(twist, frame.Forward);
            bool drawAhead = lookAhead > 0f && sAhead > s + 0.5f;

            // Scale so the larger profile fills the map.
            float maxRadius = 0.01f;
            for (int i = 0; i < segments; i++)
            {
                Vector2 d = Direction(i);
                maxRadius = Mathf.Max(maxRadius, Radius(tunnel, s, d));
                if (drawAhead) maxRadius = Mathf.Max(maxRadius, Radius(tunnel, sAhead, d));
            }
            float scale = radius / maxRadius;

            Vector3 Map(Vector3 offset) => centre + (axisRight * Vector3.Dot(offset, axisRight) + axisUp * Vector3.Dot(offset, axisUp)) * scale;

            // Outlines.
            Vector3 previous = default, previousAhead = default;
            for (int i = 0; i <= segments; i++)
            {
                Vector2 d = Direction(i);
                Vector3 point = Map(frame.LocalDirectionToWorld(d * Radius(tunnel, s, d)));
                if (i > 0) lines.Line(previous, point, width, outlineColor * brightness);
                previous = point;

                if (!drawAhead) continue;
                Vector3 pointAhead = Map(twistRotation * frame.LocalDirectionToWorld(d * Radius(tunnel, sAhead, d)));
                if (i > 0) lines.Line(previousAhead, pointAhead, width * 0.7f, aheadColor * brightness);
                previousAhead = pointAhead;
            }

            // Floor tick, outside the outline at the frame's down.
            float floorRadius = Radius(tunnel, s, Vector2.down);
            lines.Line(
                Map(frame.LocalDirectionToWorld(Vector2.down * floorRadius)),
                Map(frame.LocalDirectionToWorld(Vector2.down * (floorRadius + maxRadius * 0.15f))),
                width * 1.5f, floorColor * brightness);

            // Ship: wingtip bar and a fin along its up.
            Pose pose = ship.RenderPose;
            Vector3 shipCentre = Map(pose.position - frame.position);
            if (hull != null)
            {
                Vector3 left = Map(hull.transform.TransformPoint(hull.LeftTip) - frame.position);
                Vector3 right = Map(hull.transform.TransformPoint(hull.RightTip) - frame.position);
                lines.Line(left, right, width * 1.4f, shipColor * brightness);
            }
            Vector3 fin = Map(pose.position + pose.rotation * Vector3.up * (maxRadius * 0.12f) - frame.position);
            lines.Line(shipCentre, fin, width * 1.2f, shipColor * brightness);

            // Nearest wall.
            if (ship.Sensor.HasNearest)
            {
                WallProbeSample nearest = ship.Sensor.Nearest;
                float clearance = Mathf.Max(0f, nearest.wall.clearance);
                Color danger = dangerColor.Evaluate(Mathf.Clamp01(clearance / gradientRange)) * brightness;
                danger.a = 1f - Mathf.Clamp01(clearance / gradientRange) * 0.6f;
                Vector3 probe = Map(nearest.worldPosition - frame.position);
                Vector3 wall = Map(nearest.wallPoint - frame.position);
                lines.Line(probe, wall, width, danger);
                Vector3 r = axisRight * width * 2.5f, u = axisUp * width * 2.5f;
                lines.Line(wall + u, wall + r, width, danger);
                lines.Line(wall + r, wall - u, width, danger);
                lines.Line(wall - u, wall - r, width, danger);
                lines.Line(wall - r, wall + u, width, danger);
            }

            // Corner brackets.
            float b = radius * 1.18f, arm = radius * 0.25f;
            Color bracket = outlineColor * (brightness * 0.6f);
            bracket.a = 0.6f;
            for (int corner = 0; corner < 4; corner++)
            {
                float sx = corner % 2 == 0 ? -1f : 1f, sy = corner < 2 ? -1f : 1f;
                Vector3 c = centre + cam.right * (sx * b) + cam.up * (sy * b);
                lines.Line(c, c - cam.right * (sx * arm), width * 0.8f, bracket);
                lines.Line(c, c - cam.up * (sy * arm), width * 0.8f, bracket);
            }
        }

        Vector2 Direction(int i)
        {
            float a = i / (float)segments * Mathf.PI * 2f;
            return new Vector2(Mathf.Sin(a), Mathf.Cos(a));
        }

        static float Radius(ITunnel tunnel, float s, Vector2 direction) =>
            tunnel.QueryWall(new TunnelPoint { s = s, local = direction }).wallRadius;
    }
}
