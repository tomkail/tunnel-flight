using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Pkg = Tunnel;
using Tunnel.Splines;

namespace TunnelFlight
{
    // A time attack level authored as a spline: centreline from the SplineContainer, twist from roll keys,
    // shape from superellipse keys (channels a, b, n). Restarts the tunnel with it when Play mode starts.
    public sealed class SplineLevel : MonoBehaviour
    {
        [SerializeField] Pkg.TunnelBehaviour tunnel;
        [SerializeField] SplineContainer container;
        [Tooltip("Supplies the profile (a SuperellipseProfile) and track settings. Its rules should be empty.")]
        [SerializeField] Pkg.TunnelGeneratorSettings settings;
        [SerializeField] SplineSourceSettings sourceSettings = new SplineSourceSettings();
        [Tooltip("Tunnel twist: roll about the centreline, degrees.")]
        [SerializeField] List<RollKey> roll = new List<RollKey>();
        [Tooltip("Shape along the level. Channels: a (half-width), b (half-height), n (exponent).")]
        [SerializeField] List<SplineProfileKey> profile = new List<SplineProfileKey>();

        SplinePathSource source;

        void Start() => Rebuild();

        // Re-reads the spline and keys and restarts the tunnel. Call after editing the level in Play mode.
        [ContextMenu("Rebuild Level")]
        public void Rebuild()
        {
            if (tunnel == null || container == null || settings == null)
            {
                Debug.LogWarning("[SplineLevel] Needs a TunnelBehaviour, a SplineContainer and generator settings.", this);
                return;
            }
            Matrix4x4 splineToTunnel = tunnel.transform.worldToLocalMatrix * container.transform.localToWorldMatrix;
            source = new SplinePathSource(container.Spline, splineToTunnel, sourceSettings, roll, profile);
            tunnel.Restart(settings, 0, 0f, source);
        }
    }
}
