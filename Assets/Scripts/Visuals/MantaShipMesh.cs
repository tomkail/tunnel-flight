using System.Collections.Generic;
using UnityEngine;

namespace TunnelFlight
{
    // Procedural low-poly manta hull. The planform is defined for the right half (x >= 0) and mirrored.
    // Submeshes: 0 hull, 1 canopy, 2 wingtip accents. Rebuilds live when values change in the inspector.
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class MantaShipMesh : MonoBehaviour
    {
        [Header("Planform, right half (metres; x = right, y = forward)")]
        [Tooltip("Leading edge is a cubic Bezier from the nose to the tip.")]
        [SerializeField] Vector2 nose = new Vector2(0f, 1f);
        [SerializeField] Vector2 leadingControl1 = new Vector2(0.667f, 0.9f);
        [SerializeField] Vector2 leadingControl2 = new Vector2(1.4f, 0.367f);
        [SerializeField] Vector2 tip = new Vector2(1.5f, -0.933f);
        [Tooltip("Where the swept tip's inner edge meets the scalloped rear.")]
        [SerializeField] Vector2 tailJunction = new Vector2(1.1f, -0.417f);
        [SerializeField] Vector2 scallopControl = new Vector2(0.667f, -0.167f);
        [SerializeField] Vector2 tailProng = new Vector2(0.367f, -0.5f);
        [SerializeField] float rearNotch = -0.333f;

        [Header("Thickness (metres)")]
        [SerializeField, Min(0f)] float spineTop = 0.12f;
        [SerializeField, Min(0f)] float spineBottom = 0.06f;
        [Tooltip("How fast thickness falls off toward the tips. Higher is thinner wings.")]
        [SerializeField, Range(0.5f, 4f)] float spanTaper = 1.4f;
        [Tooltip("Thickest point as a fraction of chord from the leading edge.")]
        [SerializeField, Range(0.1f, 0.9f)] float chordPeak = 0.4f;

        [Header("Resolution")]
        [SerializeField, Range(3, 24)] int spanSegments = 8;
        [SerializeField, Range(2, 12)] int chordSegments = 4;

        [Header("Canopy")]
        [SerializeField] Vector2 canopyRange = new Vector2(0.1f, 0.62f);
        [SerializeField, Min(0.01f)] float canopyHalfWidth = 0.12f;
        [SerializeField, Min(0f)] float canopyHeight = 0.07f;

        [Header("Engine pod")]
        [SerializeField] bool enginePod = true;
        [SerializeField, Min(0.01f)] float podRadius = 0.075f;
        [Tooltip("Front and back of the pod along the ship, metres. The back is the nozzle.")]
        [SerializeField] Vector2 podRange = new Vector2(-0.42f, 0.15f);
        [SerializeField] float podHeight = 0.015f;
        [SerializeField, Range(4, 12)] int podSides = 6;

        [Header("Wingtip accents")]
        [Tooltip("Span fraction where the accent material starts.")]
        [SerializeField, Range(0.5f, 1f)] float accentStart = 0.82f;

        // Ship-space points for collision probes. x right, z forward.
        public Vector3 RightTip => new Vector3(tip.x, 0f, tip.y);
        public Vector3 LeftTip => new Vector3(-tip.x, 0f, tip.y);
        public Vector3 Nose => new Vector3(0f, 0f, nose.y);
        // Centre of the engine nozzle, hull space. The boost jet emits backwards from here.
        public Vector3 Nozzle => enginePod ? new Vector3(0f, podHeight, podRange.x) : new Vector3(0f, 0f, rearNotch);

        // Planform outline in hull space (y = 0): nose, right edge, tail, left edge. The loop closes back to the nose.
        public void GetOutline(List<Vector3> points, int curveSamples = 8)
        {
            points.Clear();
            var right = new List<Vector2>();
            for (int i = 0; i <= curveSamples; i++) right.Add(Cubic(nose, leadingControl1, leadingControl2, tip, i / (float)curveSamples));
            right.Add(tailJunction);
            for (int i = curveSamples - 1; i >= 0; i--) right.Add(Quadratic(tailProng, scallopControl, tailJunction, i / (float)curveSamples));
            foreach (Vector2 p in right) points.Add(new Vector3(p.x, 0f, p.y));
            points.Add(new Vector3(0f, 0f, rearNotch));
            for (int i = right.Count - 1; i > 0; i--) points.Add(new Vector3(-right[i].x, 0f, right[i].y));
        }

        Mesh mesh;
        bool dirty = true;

        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<int>[] triangles = { new List<int>(), new List<int>(), new List<int>() };

        void OnEnable() => dirty = true;

        void OnValidate() => dirty = true;

        void Update()
        {
            if (dirty) Rebuild();
        }

        void OnDestroy()
        {
            if (mesh == null) return;
            if (Application.isPlaying) Destroy(mesh);
            else DestroyImmediate(mesh);
        }

        public void Rebuild()
        {
            dirty = false;
            if (mesh == null)
            {
                mesh = new Mesh { name = "Manta Ship (procedural)", hideFlags = HideFlags.DontSave };
                GetComponent<MeshFilter>().sharedMesh = mesh;
            }

            vertices.Clear();
            normals.Clear();
            foreach (var list in triangles) list.Clear();

            BuildHull();
            BuildCanopy();
            if (enginePod) BuildEnginePod();

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.subMeshCount = triangles.Length;
            for (int i = 0; i < triangles.Length; i++) mesh.SetTriangles(triangles[i], i);
            mesh.RecalculateBounds();
        }

        void BuildHull()
        {
            List<float> stations = SpanStations();
            int columns = chordSegments + 1;
            var top = new Vector3[stations.Count * columns];
            var bottom = new Vector3[stations.Count * columns];

            for (int i = 0; i < stations.Count; i++)
            {
                float x = stations[i];
                float zl = LeadingZ(x), zt = TrailingZ(x);
                float span = Mathf.Pow(Mathf.Clamp01(1f - x / tip.x), spanTaper);
                for (int j = 0; j < columns; j++)
                {
                    float u = j / (float)chordSegments; // 0 trailing, 1 leading
                    float z = Mathf.Lerp(zt, zl, u);
                    float profile = span * ChordProfile(u);
                    top[i * columns + j] = new Vector3(x, spineTop * profile, z);
                    bottom[i * columns + j] = new Vector3(x, -spineBottom * profile, z);
                }
            }

            for (int i = 0; i < stations.Count - 1; i++)
            {
                int submesh = stations[i] >= accentStart * tip.x ? 2 : 0;
                for (int j = 0; j < chordSegments; j++)
                {
                    int a = i * columns + j, b = (i + 1) * columns + j, c = b + 1, d = a + 1;
                    foreach (float side in new[] { 1f, -1f })
                    {
                        AddQuad(submesh, Mirror(top[a], side), Mirror(top[b], side), Mirror(top[c], side), Mirror(top[d], side), Vector3.up);
                        AddQuad(submesh, Mirror(bottom[a], side), Mirror(bottom[b], side), Mirror(bottom[c], side), Mirror(bottom[d], side), Vector3.down);
                    }
                }
            }
        }

        void BuildCanopy()
        {
            float zFront = canopyRange.y, zBack = canopyRange.x, zMid = Mathf.Lerp(zBack, zFront, 0.45f);
            const float sink = 0.005f;
            Vector3 front = new Vector3(0f, TopHeight(0f, zFront) - sink, zFront);
            Vector3 back = new Vector3(0f, TopHeight(0f, zBack) - sink, zBack);
            Vector3 right = new Vector3(canopyHalfWidth, TopHeight(canopyHalfWidth, zMid) - sink, zMid);
            Vector3 left = new Vector3(-right.x, right.y, right.z);

            float length = zFront - zBack;
            float zRidgeFront = zMid + 0.15f * length, zRidgeBack = zMid - 0.2f * length;
            Vector3 ridgeFront = new Vector3(0f, TopHeight(0f, zRidgeFront) + canopyHeight, zRidgeFront);
            Vector3 ridgeBack = new Vector3(0f, TopHeight(0f, zRidgeBack) + canopyHeight * 0.85f, zRidgeBack);

            Vector3 centre = (front + back + left + right) * 0.25f + Vector3.down * 0.05f;
            AddAround(1, front, right, ridgeFront, centre);
            AddAround(1, front, ridgeFront, left, centre);
            AddAround(1, right, ridgeBack, ridgeFront, centre);
            AddAround(1, left, ridgeFront, ridgeBack, centre);
            AddAround(1, right, back, ridgeBack, centre);
            AddAround(1, left, ridgeBack, back, centre);
        }

        // Low-poly prism at the rear centre, tapering into the hull at the front. Rear cap uses the canopy material.
        void BuildEnginePod()
        {
            var centreFront = new Vector3(0f, podHeight, podRange.y);
            var centreBack = new Vector3(0f, podHeight, podRange.x);
            var front = new Vector3[podSides];
            var back = new Vector3[podSides];
            for (int k = 0; k < podSides; k++)
            {
                float angle = (k + 0.5f) / podSides * Mathf.PI * 2f;
                var ring = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                front[k] = centreFront + ring * (podRadius * 0.5f);
                back[k] = centreBack + new Vector3(ring.x * podRadius, ring.y * podRadius * 0.8f, 0f);
            }

            for (int k = 0; k < podSides; k++)
            {
                int n = (k + 1) % podSides;
                Vector3 radial = (back[k] + back[n]) * 0.5f - centreBack;
                AddQuad(0, back[k], back[n], front[n], front[k], radial);
                AddFacing(1, centreBack, back[n], back[k], Vector3.back);
            }
        }

        // Stations hit the trailing-edge kinks so the outline keeps its corners.
        List<float> SpanStations()
        {
            float[] breaks = { 0f, tailProng.x, tailJunction.x, tip.x };
            var result = new List<float> { 0f };
            float total = tip.x;
            for (int k = 0; k < breaks.Length - 1; k++)
            {
                float a = breaks[k], b = breaks[k + 1];
                int count = Mathf.Max(1, Mathf.RoundToInt(spanSegments * (b - a) / total));
                for (int n = 1; n <= count; n++) result.Add(Mathf.Lerp(a, b, n / (float)count));
            }
            return result;
        }

        float ChordProfile(float u)
        {
            float peak = 1f - chordPeak;
            float f = u < peak ? Mathf.Sin(0.5f * Mathf.PI * u / peak) : Mathf.Sin(0.5f * Mathf.PI * (1f - u) / (1f - peak));
            return Mathf.Pow(Mathf.Max(0f, f), 0.8f);
        }

        float TopHeight(float x, float z)
        {
            x = Mathf.Abs(x);
            float zl = LeadingZ(x), zt = TrailingZ(x);
            if (zl - zt < 1e-5f) return 0f;
            float u = Mathf.Clamp01((z - zt) / (zl - zt));
            return spineTop * Mathf.Pow(Mathf.Clamp01(1f - x / tip.x), spanTaper) * ChordProfile(u);
        }

        float LeadingZ(float x)
        {
            float t = SolveForX(s => Cubic(nose, leadingControl1, leadingControl2, tip, s).x, x);
            return Cubic(nose, leadingControl1, leadingControl2, tip, t).y;
        }

        float TrailingZ(float x)
        {
            if (x >= tailJunction.x) return Mathf.Lerp(tailJunction.y, tip.y, Mathf.InverseLerp(tailJunction.x, tip.x, x));
            if (x >= tailProng.x)
            {
                // Quadratic from the prong to the junction.
                float t = SolveForX(s => Quadratic(tailProng, scallopControl, tailJunction, s).x, x);
                return Quadratic(tailProng, scallopControl, tailJunction, t).y;
            }
            return Mathf.Lerp(rearNotch, tailProng.y, Mathf.InverseLerp(0f, tailProng.x, x));
        }

        // Bisection on a curve whose x increases with t.
        static float SolveForX(System.Func<float, float> xOf, float x)
        {
            float lo = 0f, hi = 1f;
            for (int i = 0; i < 32; i++)
            {
                float mid = 0.5f * (lo + hi);
                if (xOf(mid) < x) lo = mid; else hi = mid;
            }
            return 0.5f * (lo + hi);
        }

        static Vector2 Cubic(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
        {
            float u = 1f - t;
            return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
        }

        static Vector2 Quadratic(Vector2 a, Vector2 b, Vector2 c, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }

        static Vector3 Mirror(Vector3 p, float side) => new Vector3(p.x * side, p.y, p.z);

        void AddQuad(int submesh, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward)
        {
            AddFacing(submesh, a, b, c, outward);
            AddFacing(submesh, a, c, d, outward);
        }

        void AddAround(int submesh, Vector3 a, Vector3 b, Vector3 c, Vector3 insidePoint) =>
            AddFacing(submesh, a, b, c, (a + b + c) / 3f - insidePoint);

        // Flat shaded: each triangle gets its own vertices and face normal, wound to face `outward`.
        void AddFacing(int submesh, Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-12f) return;
            if (Vector3.Dot(n, outward) < 0f)
            {
                (b, c) = (c, b);
                n = -n;
            }
            n.Normalize();

            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            normals.Add(n);
            normals.Add(n);
            normals.Add(n);
            triangles[submesh].Add(start);
            triangles[submesh].Add(start + 1);
            triangles[submesh].Add(start + 2);
        }
    }
}
