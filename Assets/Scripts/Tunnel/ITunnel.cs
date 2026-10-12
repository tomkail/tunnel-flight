using UnityEngine;

namespace TunnelFlight
{
    // Game-side view of the tunnel. The com.tomkail.tunnel package is adapted to this through a
    // TunnelProvider subclass, so gameplay code never depends on the package API directly.
    // All positions and directions are world space; s is arc length in metres from the level start.
    public interface ITunnel
    {
        // False until the track has been built (the package generates the level at Restart).
        bool IsReady { get; }

        float Length { get; }

        // Centreline frame at s. Forward is the path tangent; up and right include the authored roll (twist).
        TunnelFrame GetFrame(float s);

        // Closest centreline point to a world position. hintS is the last known s, used as a warm start.
        TunnelPoint Project(Vector3 worldPosition, float hintS);

        // The single wall-distance query: polar radius of the profile at the point's angle, at its s.
        // Feeds collision, damage, proximity light, audio and HUD.
        WallSample QueryWall(TunnelPoint point);

        // Tightest bend radius over [s0, s1], used to keep the camera's line of sight.
        float MinBendRadius(float s0, float s1);
    }

    public struct TunnelFrame
    {
        public Vector3 position;
        public Quaternion rotation;

        public Vector3 Forward => rotation * Vector3.forward;
        public Vector3 Up => rotation * Vector3.up;
        public Vector3 Right => rotation * Vector3.right;

        // Cross-section coordinates (x = frame right, y = frame up) to world space.
        public Vector3 LocalToWorld(Vector2 local) => position + rotation * new Vector3(local.x, local.y, 0f);
        public Vector3 LocalDirectionToWorld(Vector2 direction) => rotation * new Vector3(direction.x, direction.y, 0f);
    }

    public struct TunnelPoint
    {
        public float s;
        // Offset from the centreline in the cross-section plane, in the rolled frame.
        public Vector2 local;
    }

    public struct WallSample
    {
        // Wall radius minus the point's distance from the centreline. Negative means outside the wall.
        public float clearance;
        // Polar radius of the profile at the point's angle.
        public float wallRadius;
        // Outward wall normal in cross-section coordinates, from the profile's implicit gradient.
        public Vector2 normal;
    }

    // Serializable handle for a tunnel. The package adapter derives from this.
    public abstract class TunnelProvider : MonoBehaviour, ITunnel
    {
        public abstract bool IsReady { get; }
        public abstract float Length { get; }
        public abstract TunnelFrame GetFrame(float s);
        public abstract TunnelPoint Project(Vector3 worldPosition, float hintS);
        public abstract WallSample QueryWall(TunnelPoint point);
        public abstract float MinBendRadius(float s0, float s1);
    }
}
