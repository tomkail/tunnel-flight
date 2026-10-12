using System;
using System.Collections.Generic;
using UnityEngine;

namespace TunnelFlight
{
    [Serializable]
    public struct WallProbe
    {
        public string name;
        [Tooltip("Probe position in ship space, metres.")]
        public Vector3 localOffset;
    }

    public struct WallProbeSample
    {
        public bool valid;
        public Vector3 worldPosition;
        public TunnelPoint point;
        public WallSample wall;
        // Outward wall normal and the wall point at the probe's angle, world space.
        public Vector3 worldNormal;
        public Vector3 wallPoint;

        public bool Penetrating => valid && wall.clearance < 0f;
    }

    // Runs the tunnel's wall query for each probe. Everything that reacts to the wall reads these samples.
    public sealed class WallSensor
    {
        WallProbeSample[] samples = Array.Empty<WallProbeSample>();

        public IReadOnlyList<WallProbeSample> Samples => samples;
        public int NearestIndex { get; private set; } = -1;
        public bool HasNearest => NearestIndex >= 0;
        public WallProbeSample Nearest => HasNearest ? samples[NearestIndex] : default;

        public void Sense(ITunnel tunnel, Vector3 shipPosition, Quaternion shipRotation, float hintS, IReadOnlyList<WallProbe> probes)
        {
            if (samples.Length != probes.Count) samples = new WallProbeSample[probes.Count];
            NearestIndex = -1;
            float nearest = float.PositiveInfinity;

            for (int i = 0; i < probes.Count; i++)
            {
                Vector3 world = shipPosition + shipRotation * probes[i].localOffset;
                TunnelPoint point = tunnel.Project(world, hintS);
                WallSample wall = tunnel.QueryWall(point);
                TunnelFrame frame = tunnel.GetFrame(point.s);
                Vector2 direction = point.local.sqrMagnitude > 1e-8f ? point.local.normalized : wall.normal;

                samples[i] = new WallProbeSample
                {
                    valid = true,
                    worldPosition = world,
                    point = point,
                    wall = wall,
                    worldNormal = frame.LocalDirectionToWorld(wall.normal).normalized,
                    wallPoint = frame.LocalToWorld(direction * wall.wallRadius),
                };

                if (wall.clearance < nearest)
                {
                    nearest = wall.clearance;
                    NearestIndex = i;
                }
            }
        }

        public int DeepestPenetrationIndex()
        {
            int index = -1;
            float deepest = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                if (samples[i].Penetrating && samples[i].wall.clearance < deepest)
                {
                    deepest = samples[i].wall.clearance;
                    index = i;
                }
            }
            return index;
        }
    }
}
