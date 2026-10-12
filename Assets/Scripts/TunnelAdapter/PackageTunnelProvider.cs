using UnityEngine;
using Pkg = Tunnel;

namespace TunnelFlight
{
    // Adapts com.tomkail.tunnel to ITunnel. Converts between the package's tunnel-local space and world space,
    // and keeps the package's focus anchor behind the ship so the streamed mesh covers what the camera sees.
    [DefaultExecutionOrder(-40)] // After Ship (-50) moves, before TunnelBehaviour advances in LateUpdate.
    public sealed class PackageTunnelProvider : TunnelProvider
    {
        [SerializeField] Pkg.TunnelBehaviour tunnel;
        [Tooltip("Ship whose position drives the package's focus anchor.")]
        [SerializeField] Ship ship;
        [Tooltip("Keep the focus this far behind the ship, metres, so walls beside and behind the camera stay meshed.")]
        [SerializeField, Min(0f)] float focusBehindShip = 12f;

        public Pkg.TunnelBehaviour Behaviour => tunnel;

        public override bool IsReady => tunnel != null && tunnel.IsReady;

        public override float Length => tunnel.Track.Length;

        void OnEnable()
        {
            if (ship != null) ship.Restarted += OnShipRestarted;
        }

        void OnDisable()
        {
            if (ship != null) ship.Restarted -= OnShipRestarted;
        }

        // The package's meshes only stream forward, so a respawn behind the mesh window would leave the
        // ship in empty space. Move the focus back and rebuild them around it.
        void OnShipRestarted()
        {
            if (!IsReady) return;
            tunnel.Focus.distance = FocusDistance();
            tunnel.Advance();
            foreach (Pkg.TunnelMesh mesh in tunnel.GetComponentsInChildren<Pkg.TunnelMesh>()) mesh.Rebuild();
        }

        float FocusDistance() => Mathf.Clamp(ship.S - focusBehindShip, 0f, Length);

        void Update()
        {
            if (!IsReady || ship == null) return;
            tunnel.Focus.distance = FocusDistance();
        }

        public override TunnelFrame GetFrame(float s)
        {
            Pkg.TunnelFrame local = tunnel.Track.GetFrame(s);
            return new TunnelFrame { position = tunnel.LocalToWorld(local.position), rotation = tunnel.LocalToWorld(local.rotation) };
        }

        public override TunnelPoint Project(Vector3 worldPosition, float hintS)
        {
            Pkg.TrackProjection p = tunnel.Track.Project(tunnel.WorldToLocal(worldPosition), hintS);
            return new TunnelPoint { s = p.distance, local = p.localXY };
        }

        public override WallSample QueryWall(TunnelPoint point)
        {
            Pkg.WallSample wall = tunnel.Track.GetWall(point.s, point.local);
            return new WallSample { clearance = wall.clearance, wallRadius = wall.radius, normal = wall.normal };
        }

        public override float MinBendRadius(float s0, float s1) => tunnel.Track.MinBendRadius(s0, s1, out _);
    }
}
