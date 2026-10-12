using UnityEngine;

namespace TunnelFlight
{
    // Sci-fi wall readout: a glowing line from the ship's closest point to the nearest wall, end markers and the
    // distance drawn beside it. Colour follows danger (clearance), and it pulses when closing on the wall fast.
    // Reads the ship's wall sensor, so it shows the same query collision uses.
    [DefaultExecutionOrder(100)] // After the camera has moved.
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class WallDistanceHud : MonoBehaviour
    {
        [SerializeField] Ship ship;
        [SerializeField] Camera viewCamera;

        [Header("Visibility")]
        [Tooltip("Shown when the nearest wall is closer than this, metres. Fades in over the last part.")]
        [SerializeField, Min(0.1f)] float showBelow = 6f;
        [SerializeField, Min(0.01f)] float fadeRange = 1.5f;

        [Header("Look")]
        [Tooltip("Colour by distance to the wall, metres (0 = touching). The gradient spans 0 to Gradient Range.")]
        [SerializeField] Gradient dangerColor = HudColors.Danger();
        [SerializeField, Min(0.1f)] float gradientRange = 5f;
        [Tooltip("HDR multiplier on the danger colour.")]
        [SerializeField, Min(0f)] float brightness = 2f;
        [Tooltip("Line width as a fraction of the distance from the camera, so it holds its size on screen.")]
        [SerializeField, Min(0.0001f)] float lineWidth = 0.006f;
        [Tooltip("Readout glyph height as a fraction of the distance from the camera.")]
        [SerializeField, Min(0.001f)] float textHeight = 0.06f;
        [Tooltip("Marker size at each end as a fraction of the distance from the camera.")]
        [SerializeField, Min(0.001f)] float markerSize = 0.025f;

        [Header("Closing speed")]
        [Tooltip("Speed toward the wall, m/s, at which the readout pulses fully.")]
        [SerializeField, Min(0.1f)] float pulseAtClosingSpeed = 8f;
        [SerializeField, Min(0.1f)] float pulseFrequency = 9f;

        GlowLineMesh lines;
        readonly char[] digits = new char[8];
        string label = "";
        float labelValue = float.NaN;

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
            Vector3 eye = viewCamera.transform.position;
            lines.Begin(eye);

            if (ship != null && ship.Running && ship.Sensor.HasNearest)
                DrawReadout(eye);

            lines.End();
        }

        void DrawReadout(Vector3 eye)
        {
            WallProbeSample nearest = ship.Sensor.Nearest;
            Vector3 probe = ship.transform.TransformPoint(ship.Probes[ship.Sensor.NearestIndex].localOffset);

            // The query's clearance is radial; along the wall normal the true gap is a little shorter.
            Vector3 radial = nearest.wallPoint - nearest.worldPosition;
            float distance = Mathf.Max(0f, nearest.wall.clearance);
            if (radial.sqrMagnitude > 1e-8f) distance *= Mathf.Abs(Vector3.Dot(radial.normalized, nearest.worldNormal));
            Vector3 wall = probe + nearest.worldNormal * distance;

            float alpha = Mathf.Clamp01((showBelow - distance) / fadeRange);
            if (alpha <= 0f) return;

            float closing = Mathf.Max(0f, Vector3.Dot(ship.Velocity, nearest.worldNormal));
            float pulse = Mathf.Clamp01(closing / pulseAtClosingSpeed) * (0.5f + 0.5f * Mathf.Sin(Time.time * pulseFrequency * Mathf.PI * 2f));
            Color color = dangerColor.Evaluate(Mathf.Clamp01(distance / gradientRange)) * brightness * (1f + pulse);
            color.a = alpha;

            float scaleAtProbe = Vector3.Distance(eye, probe);
            float scaleAtWall = Vector3.Distance(eye, wall);
            float width = lineWidth * Mathf.Min(scaleAtProbe, scaleAtWall);

            // Connector, with a small gap at the ship end so it reads as a measurement rather than a beam.
            Vector3 dir = wall - probe;
            float length = dir.magnitude;
            if (length > 1e-4f) lines.Line(probe + dir * 0.12f, wall, width, color);

            // Markers: a tick across the line at the ship, a diamond on the wall.
            Vector3 screenRight = viewCamera.transform.right, screenUp = viewCamera.transform.up;
            Vector3 across = length > 1e-4f ? Vector3.Cross(dir / length, viewCamera.transform.forward).normalized : screenRight;
            float mp = markerSize * scaleAtProbe, mw = markerSize * scaleAtWall;
            lines.Line(probe - across * mp, probe + across * mp, width, color);
            Vector3 r = screenRight * mw, u = screenUp * mw;
            lines.Line(wall + u, wall + r, width, color);
            lines.Line(wall + r, wall - u, width, color);
            lines.Line(wall - u, wall - r, width, color);
            lines.Line(wall - r, wall + u, width, color);

            // Readout beside the midpoint, offset off the line on screen, facing the camera.
            Vector3 mid = (probe + wall) * 0.5f;
            float scaleAtMid = Vector3.Distance(eye, mid);
            float h = textHeight * scaleAtMid;
            string text = Label(distance);
            Vector3 offset = across * (h * 0.8f);
            if (Vector3.Dot(offset, screenRight) < 0f) offset = -offset;
            Vector3 origin = mid + offset - screenUp * (h * 0.5f);
            StrokeFont.Draw(lines, text, origin, screenRight * h, screenUp * h, width * 0.8f, color);

            // Bracket from the line to the readout.
            lines.Line(mid, mid + offset * 0.85f, width * 0.6f, color * 0.7f);
        }

        // Cached so the string is only rebuilt when the shown value changes.
        string Label(float metres)
        {
            float rounded = Mathf.Round(metres * 10f) / 10f;
            if (rounded == labelValue) return label;
            labelValue = rounded;
            int tenths = Mathf.Clamp(Mathf.RoundToInt(rounded * 10f), 0, 9999);
            int n = 0;
            int whole = tenths / 10;
            if (whole >= 100) digits[n++] = (char)('0' + whole / 100);
            if (whole >= 10) digits[n++] = (char)('0' + whole / 10 % 10);
            digits[n++] = (char)('0' + whole % 10);
            digits[n++] = '.';
            digits[n++] = (char)('0' + tenths % 10);
            digits[n++] = 'm';
            label = new string(digits, 0, n);
            return label;
        }
    }
}
