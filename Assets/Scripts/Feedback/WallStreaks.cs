using UnityEngine;

namespace TunnelFlight
{
    // Speed streaks hugging the walls. Each streak is fixed in the world just inside the wall and drawn along the
    // tunnel, its length set by the ship's speed, so passing them gives parallax close to the walls. Streaks near
    // the ship brighten. Streaks the ship has passed respawn ahead.
    [DefaultExecutionOrder(100)] // After the ship and camera have moved.
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class WallStreaks : MonoBehaviour
    {
        [SerializeField] Ship ship;
        [SerializeField] Camera viewCamera;

        [Header("Placement")]
        [SerializeField, Range(0, 1000)] int count = 260;
        [Tooltip("How far ahead of the ship streaks live, metres. Passed streaks respawn near the far end.")]
        [SerializeField, Min(1f)] float rangeAhead = 70f;
        [Tooltip("Streaks are recycled once this far behind the ship, metres.")]
        [SerializeField, Min(0f)] float rangeBehind = 20f;
        [Tooltip("Distance in from the wall, metres (min, max).")]
        [SerializeField] Vector2 inset = new Vector2(0.05f, 0.6f);

        [Header("Look")]
        [Tooltip("Streak length as seconds of travel at the ship's speed.")]
        [SerializeField, Min(0f)] float lengthSeconds = 0.03f;
        [SerializeField, Min(0.01f)] float minLength = 0.2f;
        [Tooltip("Width, metres.")]
        [SerializeField, Min(0.001f)] float width = 0.025f;
        [SerializeField] Color color = new Color(0.75f, 0.95f, 1f, 1f);
        [SerializeField, Min(0f)] float brightness = 1.2f;
        [Tooltip("Alpha at normal max speed and at boost max speed.")]
        [SerializeField] Vector2 alphaBySpeed = new Vector2(0.2f, 0.7f);
        [Tooltip("Streaks within this distance of the ship brighten, metres.")]
        [SerializeField, Min(0.1f)] float nearShipRadius = 8f;
        [SerializeField, Min(0f)] float nearShipBoost = 3f;
        [Tooltip("Fade in over this distance at the far end, metres, so streaks don't pop in.")]
        [SerializeField, Min(0.1f)] float fadeInDistance = 20f;

        struct Streak
        {
            public float s;
            public Vector3 position;
            public Vector3 direction;
        }

        GlowLineMesh lines;
        Streak[] streaks = System.Array.Empty<Streak>();
        bool seeded;

        void Awake()
        {
            lines = new GlowLineMesh();
            GetComponent<MeshFilter>().sharedMesh = lines.Mesh;
        }

        void OnEnable()
        {
            seeded = false;
            if (ship != null) ship.Restarted += Reseed;
        }

        void OnDisable()
        {
            if (ship != null) ship.Restarted -= Reseed;
            if (lines == null) return;
            lines.Begin(Vector3.zero);
            lines.End();
        }

        void OnDestroy() => lines?.Destroy();

        void Reseed() => seeded = false;

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
            float shipS = ship.RenderS;
            if (streaks.Length != count)
            {
                streaks = new Streak[count];
                seeded = false;
            }
            if (!seeded)
            {
                for (int i = 0; i < streaks.Length; i++) streaks[i] = Spawn(tunnel, Random.Range(shipS - rangeBehind, shipS + rangeAhead));
                seeded = true;
            }

            HeatBoostSettings speeds = ship.HeatBoostSettings;
            float speed = ship.HeatBoost.Speed;
            float speedT = Mathf.Clamp01(Mathf.InverseLerp(speeds.normalMaxSpeed, speeds.BoostSpeed, speed));
            float baseAlpha = Mathf.Lerp(alphaBySpeed.x, alphaBySpeed.y, speedT) * Mathf.Clamp01(speed / Mathf.Max(1f, speeds.normalMaxSpeed));
            float length = Mathf.Max(minLength, speed * lengthSeconds);
            Vector3 shipPosition = ship.RenderPose.position;

            for (int i = 0; i < streaks.Length; i++)
            {
                if (streaks[i].s < shipS - rangeBehind || streaks[i].s > shipS + rangeAhead + 1f)
                    streaks[i] = Spawn(tunnel, shipS + rangeAhead - Random.value * Mathf.Min(fadeInDistance, rangeAhead));

                Streak streak = streaks[i];
                if (streak.s < 0f || streak.s > tunnel.Length) continue;
                float ahead = streak.s - shipS;
                float fade = Mathf.Clamp01((rangeAhead - ahead) / fadeInDistance);
                float near = Mathf.Clamp01(1f - Vector3.Distance(streak.position, shipPosition) / nearShipRadius);
                float alpha = baseAlpha * fade * (1f + nearShipBoost * near * near);
                if (alpha <= 0.001f) continue;

                Color c = color * (brightness * Mathf.Max(1f, alpha));
                c.a = Mathf.Clamp01(alpha);
                lines.Line(streak.position, streak.position - streak.direction * length, width, c);
            }
        }

        static Streak Spawn(ITunnel tunnel, float s, Vector2 inset)
        {
            // Off either end of the track: kept (so it recycles normally) but not drawn.
            float clamped = Mathf.Clamp(s, 0f, tunnel.Length);
            float a = Random.value * Mathf.PI * 2f;
            var direction = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
            float radius = tunnel.QueryWall(new TunnelPoint { s = clamped, local = direction }).wallRadius;
            TunnelFrame frame = tunnel.GetFrame(clamped);
            return new Streak
            {
                s = s,
                position = frame.LocalToWorld(direction * Mathf.Max(0f, radius - Random.Range(inset.x, inset.y))),
                direction = frame.Forward,
            };
        }

        Streak Spawn(ITunnel tunnel, float s) => Spawn(tunnel, s, inset);
    }
}
