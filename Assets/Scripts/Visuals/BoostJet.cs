using UnityEngine;

namespace TunnelFlight
{
    // Engine flame that idles at cruise and roars on boost: a hot core, an outer plume, a light that
    // throws glow on the walls behind, and a spark stream that ramps up hard with power.
    // Forced boost shifts the colours toward a warning tint.
    public sealed class BoostJet : MonoBehaviour
    {
        [SerializeField] Ship ship;
        [SerializeField] MantaShipMesh hull;
        [SerializeField] MeshRenderer core;
        [SerializeField] MeshRenderer plume;
        [Tooltip("Camera-facing glow at the nozzle. Carries the jet when it's seen end-on from the chase camera.")]
        [SerializeField] MeshRenderer flare;
        [SerializeField] ParticleSystem sparks;
        [SerializeField] Light jetLight;

        [Header("Response")]
        [Tooltip("Intensity at cruise (normal max speed, not boosting).")]
        [SerializeField, Range(0f, 1f)] float idleIntensity = 0.12f;
        [Tooltip("Extra intensity while coasting above normal max, at boost max speed.")]
        [SerializeField, Range(0f, 1f)] float coastIntensity = 0.35f;
        [Tooltip("Seconds to reach full flame when boost starts.")]
        [SerializeField, Min(0.001f)] float attackTime = 0.06f;
        [Tooltip("Seconds for the flame to die back after boost ends.")]
        [SerializeField, Min(0.001f)] float releaseTime = 0.35f;
        [Tooltip("Extra flame length when a boost starts, decaying over kickDecay.")]
        [SerializeField, Range(0f, 2f)] float kickAmount = 0.6f;
        [SerializeField, Min(0.01f)] float kickDecay = 0.25f;

        [Header("Flame shape (metres)")]
        [SerializeField] Vector2 coreLength = new Vector2(0.25f, 2.2f);
        [SerializeField] Vector2 coreRadius = new Vector2(0.03f, 0.055f);
        [SerializeField] Vector2 plumeLength = new Vector2(0.4f, 3.5f);
        [SerializeField] Vector2 plumeRadius = new Vector2(0.05f, 0.12f);
        [Tooltip("Flare diameter at idle and full boost, metres.")]
        [SerializeField] Vector2 flareSize = new Vector2(0.2f, 0.8f);
        [SerializeField, Range(0f, 0.5f)] float flicker = 0.18f;
        [SerializeField] float flickerFrequency = 30f;

        [Header("Colour")]
        [SerializeField, ColorUsage(true, true)] Color coreColor = new Color(1.6f, 1.8f, 2.2f, 1f);
        [SerializeField, ColorUsage(true, true)] Color plumeColor = new Color(0.3f, 0.6f, 2.2f, 0.6f);
        [SerializeField, ColorUsage(true, true)] Color forcedCoreColor = new Color(2.2f, 1.4f, 0.8f, 1f);
        [SerializeField, ColorUsage(true, true)] Color forcedPlumeColor = new Color(2f, 0.35f, 0.15f, 0.8f);

        [Header("Sparks")]
        [Tooltip("Sparks per second at full boost. Scales with intensity squared, so they only pour out at real power.")]
        [SerializeField, Min(0f)] float sparkRate = 450f;
        [SerializeField, Range(0f, 1f)] float sparkThreshold = 0.35f;
        [SerializeField, Min(0)] int boostStartBurst = 60;

        [Header("Light")]
        [SerializeField, Min(0f)] float lightIntensity = 6f;
        [SerializeField, Min(0f)] float lightRange = 9f;

        [Header("Debug")]
        [Tooltip("Force an intensity (0 to 1) for tuning without flying. Negative = off.")]
        [SerializeField, Range(-1f, 1f)] float intensityOverride = -1f;

        public float Intensity { get; private set; }

        MaterialPropertyBlock block;
        float kick;
        float forcedBlend;
        bool wasBoosting;
        float noiseSeed;
        static readonly int ColorId = Shader.PropertyToID("_Color");

        void Awake() => EnsureInitialized();

        // Also called from Tick, so the jet works when driven outside Play mode (previews, tuning).
        void EnsureInitialized()
        {
            if (block != null) return;
            block = new MaterialPropertyBlock();
            noiseSeed = Random.value * 100f;
            if (core != null) core.GetComponent<MeshFilter>().sharedMesh = FlameMesh.Shared;
            if (plume != null) plume.GetComponent<MeshFilter>().sharedMesh = FlameMesh.Shared;
            if (flare != null) flare.GetComponent<MeshFilter>().sharedMesh = QuadMesh.Shared;
            if (hull != null) transform.position = hull.transform.TransformPoint(hull.Nozzle);
        }

        void OnEnable()
        {
            if (ship != null) ship.Restarted += OnRestarted;
        }

        void OnDisable()
        {
            if (ship != null) ship.Restarted -= OnRestarted;
        }

        void OnRestarted()
        {
            Intensity = 0f;
            kick = 0f;
            if (sparks != null) sparks.Clear();
        }

        public void SetIntensityOverride(float value) => intensityOverride = value;

        void LateUpdate() => Tick(Time.deltaTime);

        public void Tick(float dt)
        {
            EnsureInitialized();
            bool boosting = false, forced = false;
            float target;

            if (intensityOverride >= 0f)
            {
                target = intensityOverride;
                Intensity = target;
            }
            else
            {
                target = 0f;
                if (ship != null && ship.Running && ship.HeatBoostSettings != null)
                {
                    HeatBoostModel hb = ship.HeatBoost;
                    HeatBoostSettings s = ship.HeatBoostSettings;
                    boosting = hb.Boosting;
                    forced = hb.ForcedLock;
                    float coast = Mathf.InverseLerp(s.normalMaxSpeed, s.BoostSpeed, hb.Speed);
                    target = boosting ? 1f : idleIntensity + coastIntensity * coast;
                }
                float rate = target > Intensity ? attackTime : releaseTime;
                Intensity = Mathf.Lerp(Intensity, target, 1f - Mathf.Exp(-dt / rate));
            }

            if (boosting && !wasBoosting)
            {
                kick = 1f;
                if (sparks != null && boostStartBurst > 0) sparks.Emit(boostStartBurst);
            }
            wasBoosting = boosting;
            kick *= Mathf.Exp(-dt / kickDecay);
            forcedBlend = Mathf.MoveTowards(forcedBlend, forced ? 1f : 0f, dt * 4f);

            float noise = (Mathf.PerlinNoise(noiseSeed, Time.time * flickerFrequency) - 0.5f) * 2f;
            float lengthScale = (1f + kick * kickAmount) * (1f + noise * flicker);

            Shape(core, coreLength, coreRadius, lengthScale, Color.Lerp(coreColor, forcedCoreColor, forcedBlend));
            Shape(plume, plumeLength, plumeRadius, lengthScale * (1f + noise * flicker * 0.5f), Color.Lerp(plumeColor, forcedPlumeColor, forcedBlend));

            ShapeFlare(lengthScale, Color.Lerp(coreColor, forcedCoreColor, forcedBlend));

            if (sparks != null)
            {
                float power = Mathf.InverseLerp(sparkThreshold, 1f, Intensity);
                var emission = sparks.emission;
                emission.rateOverTime = sparkRate * power * power * (1f + kick);
                if (!sparks.isPlaying) sparks.Play();
            }

            if (jetLight != null)
            {
                jetLight.intensity = lightIntensity * Intensity * (1f + noise * flicker);
                jetLight.range = lightRange;
                Color c = Color.Lerp(plumeColor, forcedPlumeColor, forcedBlend);
                jetLight.color = new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b));
            }
        }

        void Shape(MeshRenderer flame, Vector2 length, Vector2 radius, float lengthScale, Color color)
        {
            if (flame == null) return;
            float t = Intensity;
            float l = Mathf.Lerp(length.x, length.y, t) * lengthScale;
            float r = Mathf.Lerp(radius.x, radius.y, t);
            flame.transform.localScale = new Vector3(r, r, l);
            flame.enabled = t > 0.01f;

            color.a *= Mathf.Clamp01(0.35f + t);
            block.SetColor(ColorId, color);
            flame.SetPropertyBlock(block);
        }

        void ShapeFlare(float lengthScale, Color color)
        {
            if (flare == null) return;
            Camera cam = Camera.main;
            if (cam != null) flare.transform.rotation = Quaternion.LookRotation(flare.transform.position - cam.transform.position, cam.transform.up);
            float size = Mathf.Lerp(flareSize.x, flareSize.y, Intensity) * Mathf.Lerp(1f, lengthScale, 0.5f);
            flare.transform.localScale = Vector3.one * size;
            flare.enabled = Intensity > 0.01f;
            color.a *= Mathf.Clamp01(0.3f + Intensity);
            block.SetColor(ColorId, color);
            flare.SetPropertyBlock(block);
        }

        static class QuadMesh
        {
            static Mesh mesh;

            public static Mesh Shared => mesh != null ? mesh : mesh = Build();

            static Mesh Build()
            {
                var m = new Mesh { name = "Jet Flare", hideFlags = HideFlags.DontSave };
                m.SetVertices(new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) });
                m.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) });
                m.SetColors(new[] { Color.white, Color.white, Color.white, Color.white });
                m.SetNormals(new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back });
                m.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
                m.RecalculateBounds();
                return m;
            }
        }

        // Unit flame: radius 1 at the nozzle (z = 0), tapering to a point at z = -1.
        // Vertex alpha fades toward the tip; low-poly, with a bulge just behind the nozzle.
        static class FlameMesh
        {
            static Mesh mesh;

            public static Mesh Shared => mesh != null ? mesh : mesh = Build();

            static Mesh Build()
            {
                const int sides = 10;
                float[] stations = { 0f, 0.12f, 0.35f, 0.65f, 1f };
                float[] radii = { 1f, 1.15f, 0.85f, 0.45f, 0f };
                float[] alphas = { 1f, 1f, 0.75f, 0.4f, 0f };

                var vertices = new Vector3[stations.Length * (sides + 1)];
                var colors = new Color[vertices.Length];
                var uvs = new Vector2[vertices.Length];
                for (int i = 0; i < stations.Length; i++)
                {
                    for (int k = 0; k <= sides; k++)
                    {
                        float a = k / (float)sides * Mathf.PI * 2f;
                        int v = i * (sides + 1) + k;
                        vertices[v] = new Vector3(Mathf.Cos(a) * radii[i], Mathf.Sin(a) * radii[i], -stations[i]);
                        colors[v] = new Color(1f, 1f, 1f, alphas[i]);
                        uvs[v] = new Vector2(stations[i], k / (float)sides);
                    }
                }

                var triangles = new int[(stations.Length - 1) * sides * 6];
                int t = 0;
                for (int i = 0; i < stations.Length - 1; i++)
                {
                    for (int k = 0; k < sides; k++)
                    {
                        int a = i * (sides + 1) + k, b = a + 1, c = a + sides + 1, d = c + 1;
                        triangles[t++] = a; triangles[t++] = c; triangles[t++] = b;
                        triangles[t++] = b; triangles[t++] = c; triangles[t++] = d;
                    }
                }

                var m = new Mesh { name = "Boost Flame", hideFlags = HideFlags.DontSave };
                m.SetVertices(vertices);
                m.SetColors(colors);
                m.SetUVs(0, uvs);
                m.SetTriangles(triangles, 0);
                m.RecalculateNormals();
                m.RecalculateBounds();
                return m;
            }
        }
    }
}
