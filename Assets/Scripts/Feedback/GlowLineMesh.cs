using System.Collections.Generic;
using UnityEngine;

namespace TunnelFlight
{
    // Camera-facing glow strokes collected into one mesh each frame. Pair with the Additive Glow shader
    // (Falloff: Across V) for soft, sci-fi vector lines. Allocation-free after warm-up.
    public sealed class GlowLineMesh
    {
        readonly Mesh mesh = new Mesh { name = "Glow Lines", hideFlags = HideFlags.DontSave };
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Color> colors = new List<Color>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<int> indices = new List<int>();
        Vector3 eye;

        public Mesh Mesh => mesh;

        public GlowLineMesh() => mesh.MarkDynamic();

        public void Begin(Vector3 cameraPosition)
        {
            eye = cameraPosition;
            vertices.Clear();
            colors.Clear();
            uvs.Clear();
            indices.Clear();
        }

        // A stroke from a to b, world space, width in metres.
        public void Line(Vector3 a, Vector3 b, float width, Color color)
        {
            Vector3 along = b - a;
            Vector3 toEye = eye - (a + b) * 0.5f;
            Vector3 side = Vector3.Cross(along, toEye);
            if (side.sqrMagnitude < 1e-12f) return;
            side = side.normalized * (width * 0.5f);

            int start = vertices.Count;
            vertices.Add(a - side);
            vertices.Add(a + side);
            vertices.Add(b + side);
            vertices.Add(b - side);
            for (int i = 0; i < 4; i++) colors.Add(color);
            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(0f, 1f));
            uvs.Add(new Vector2(1f, 1f));
            uvs.Add(new Vector2(1f, 0f));
            indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);
            indices.Add(start); indices.Add(start + 2); indices.Add(start + 3);
        }

        // A filled triangle at full strength (UVs at the stroke centre, so Across V falloff leaves it solid).
        public void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color)
        {
            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            for (int i = 0; i < 3; i++)
            {
                colors.Add(color);
                uvs.Add(new Vector2(0.5f, 0.5f));
            }
            indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);
        }

        public void End()
        {
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(indices, 0, false);
            mesh.bounds = new Bounds(eye, Vector3.one * 2000f);
        }

        public void Destroy()
        {
            if (Application.isPlaying) Object.Destroy(mesh);
            else Object.DestroyImmediate(mesh);
        }
    }

    // Seven-segment style vector glyphs for HUD readouts: digits, '.', '-' and 'm'. Glyphs are 0.6 wide, 1 tall.
    public static class StrokeFont
    {
        static readonly Vector2 TL = new Vector2(0f, 1f), TR = new Vector2(0.6f, 1f);
        static readonly Vector2 ML = new Vector2(0f, 0.5f), MR = new Vector2(0.6f, 0.5f);
        static readonly Vector2 BL = new Vector2(0f, 0f), BR = new Vector2(0.6f, 0f);

        // Segments a (top), b (top right), c (bottom right), d (bottom), e (bottom left), f (top left), g (middle).
        static readonly int[] Digits = { 0x3F, 0x06, 0x5B, 0x4F, 0x66, 0x6D, 0x7D, 0x07, 0x7F, 0x6F };

        public const float Advance = 0.85f;

        // Draws text with its bottom-left at origin, along right/up (world directions scaled to glyph height).
        public static void Draw(GlowLineMesh lines, string text, Vector3 origin, Vector3 right, Vector3 up, float width, Color color)
        {
            Vector3 cursor = origin;
            foreach (char c in text)
            {
                void S(Vector2 a, Vector2 b) => lines.Line(cursor + right * a.x + up * a.y, cursor + right * b.x + up * b.y, width, color);

                if (c >= '0' && c <= '9')
                {
                    int m = Digits[c - '0'];
                    if ((m & 0x01) != 0) S(TL, TR);
                    if ((m & 0x02) != 0) S(TR, MR);
                    if ((m & 0x04) != 0) S(MR, BR);
                    if ((m & 0x08) != 0) S(BL, BR);
                    if ((m & 0x10) != 0) S(ML, BL);
                    if ((m & 0x20) != 0) S(TL, ML);
                    if ((m & 0x40) != 0) S(ML, MR);
                    cursor += right * Advance;
                }
                else if (c == '.')
                {
                    S(new Vector2(0.05f, 0f), new Vector2(0.05f, 0.08f));
                    cursor += right * 0.35f;
                }
                else if (c == '-')
                {
                    S(ML, MR);
                    cursor += right * Advance;
                }
                else if (c == 'm')
                {
                    // Lower-case m: half height.
                    var top = new Vector2(0f, 0.5f);
                    S(BL, top);
                    S(top, new Vector2(0.6f, 0.5f));
                    S(new Vector2(0.3f, 0.5f), new Vector2(0.3f, 0f));
                    S(new Vector2(0.6f, 0.5f), BR);
                    cursor += right * Advance;
                }
                else cursor += right * Advance;
            }
        }

        public static float Width(string text)
        {
            float w = 0f;
            foreach (char c in text) w += c == '.' ? 0.35f : Advance;
            return w;
        }
    }
}
