using System;
using System.Collections.Generic;
using UnityEngine;

namespace TunnelFlight
{
    // Owns the simulation: input, heat and boost, flight, the wall query and wall response.
    // Simulates in FixedUpdate and interpolates the transform between ticks.
    [DefaultExecutionOrder(-50)]
    public sealed class Ship : MonoBehaviour
    {
        [SerializeField] TunnelProvider tunnel;
        [SerializeField] ShipInput input;
        [SerializeField] FlightSettings flightSettings;
        [SerializeField] HeatBoostSettings heatBoostSettings;
        [SerializeField] WallSettings wallSettings;

        [Tooltip("Spline position the ship starts at, metres.")]
        [SerializeField] float startDistance = 10f;

        [Tooltip("Collision points in ship space. Wingtips first: the ship is wide and short, so rolling flat near a wall is a hazard. Overwritten from the hull when one is assigned.")]
        [SerializeField] List<WallProbe> probes = new List<WallProbe>
        {
            new WallProbe { name = "Left wingtip", localOffset = new Vector3(-1.5f, 0f, -0.933f) },
            new WallProbe { name = "Right wingtip", localOffset = new Vector3(1.5f, 0f, -0.933f) },
            new WallProbe { name = "Nose", localOffset = new Vector3(0f, 0f, 1f) },
        };

        [Tooltip("Take the wingtip and nose probes from the hull mesh, so collision matches the visible shape.")]
        [SerializeField] MantaShipMesh hullShape;

        [Tooltip("Resolve passes per tick, for when more than one probe is inside the wall.")]
        [SerializeField, Range(1, 4)] int resolveIterations = 2;

        public readonly FlightModel Flight = new FlightModel();
        public readonly HeatBoostModel HeatBoost = new HeatBoostModel();
        public readonly WallSensor Sensor = new WallSensor();
        public readonly Health Health = new Health();

        public ITunnel Tunnel => tunnel;
        public FlightSettings FlightSettings => flightSettings;
        public HeatBoostSettings HeatBoostSettings => heatBoostSettings;
        public IReadOnlyList<WallProbe> Probes => probes;

        public bool Running { get; private set; }
        public bool Finished { get; private set; }
        public bool InContact { get; private set; }
        public WallHit LastHit { get; private set; }
        public float S { get; private set; }
        public Vector2 Local { get; private set; }
        public Vector3 Velocity => Flight.Forward * HeatBoost.Speed;

        // Pose and spline position interpolated for rendering. Camera and feedback read these.
        public Pose RenderPose { get; private set; }
        public float RenderS { get; private set; }

        // Fires every tick the ship touches the wall.
        public event Action<WallHit> WallContact;
        public event Action<WallHit> HeavyHit;
        public event Action Died;
        public event Action ReachedEnd;
        public event Action Restarted;

        Pose previousPose;
        float previousS;
        bool warnedNoTunnel;
        bool started;

        void Reset() => input = GetComponent<ShipInput>();

        void Awake()
        {
            Health.Died += () => Died?.Invoke();
            SyncProbesToHull();
        }

        public void SyncProbesToHull()
        {
            if (hullShape == null) return;
            Transform hull = hullShape.transform;
            Vector3 ToShip(Vector3 p) => transform.InverseTransformPoint(hull.TransformPoint(p));
            probes = new List<WallProbe>
            {
                new WallProbe { name = "Left wingtip", localOffset = ToShip(hullShape.LeftTip) },
                new WallProbe { name = "Right wingtip", localOffset = ToShip(hullShape.RightTip) },
                new WallProbe { name = "Nose", localOffset = ToShip(hullShape.Nose) },
            };
        }

        void Start() => TryRestart();

        void Update()
        {
            if (input != null && input.RestartPressed) TryRestart();
            // The tunnel may become ready after Start; keep trying until the first run begins.
            if (!started) TryRestart();

            float alpha = Time.fixedDeltaTime > 0f ? Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime) : 1f;
            var current = new Pose(Flight.Position, Flight.Rotation);
            RenderPose = new Pose(
                Vector3.Lerp(previousPose.position, current.position, alpha),
                Quaternion.Slerp(previousPose.rotation, current.rotation, alpha));
            RenderS = Mathf.Lerp(previousS, S, alpha);
            transform.SetPositionAndRotation(RenderPose.position, RenderPose.rotation);
        }

        void FixedUpdate()
        {
            if (!Running) return;
            previousPose = new Pose(Flight.Position, Flight.Rotation);
            previousS = S;
            Tick(Time.fixedDeltaTime, Time.fixedUnscaledDeltaTime);
        }

        public bool TryRestart()
        {
            if (!HasDependencies()) return false;

            TunnelFrame frame = tunnel.GetFrame(startDistance);
            Flight.Reset(frame.position, frame.rotation);
            HeatBoost.Reset(heatBoostSettings);
            Health.Reset(wallSettings.maxHealth);
            S = startDistance;
            Local = Vector2.zero;
            InContact = false;
            LastHit = default;
            Finished = false;
            Running = true;
            started = true;

            Sensor.Sense(tunnel, Flight.Position, Flight.Rotation, S, probes);
            previousPose = new Pose(Flight.Position, Flight.Rotation);
            previousS = S;
            Restarted?.Invoke();
            return true;
        }

        void Tick(float dt, float realDt)
        {
            bool boostHeld = input != null && input.BoostHeld;
            float rollInput = 0f, pitchInput = 0f;
            if (input != null) input.ReadSteering(dt, out rollInput, out pitchInput);

            HeatBoost.Step(heatBoostSettings, boostHeld, dt, realDt);
            Flight.Step(flightSettings, rollInput, pitchInput, HeatBoost.Speed, dt);
            UpdateSplinePosition();

            Sensor.Sense(tunnel, Flight.Position, Flight.Rotation, S, probes);
            ResolveWalls(dt);

            if (S >= tunnel.Length)
            {
                Running = false;
                Finished = true;
                ReachedEnd?.Invoke();
            }
        }

        void ResolveWalls(float dt)
        {
            InContact = false;
            WallHit worst = default;

            for (int i = 0; i < resolveIterations; i++)
            {
                int index = Sensor.DeepestPenetrationIndex();
                if (index < 0) break;

                WallHit hit = WallResponse.Resolve(wallSettings, Flight, HeatBoost.Speed, Sensor.Samples[index], dt, out float speedScale);
                HeatBoost.ScaleSpeed(speedScale);
                if (!InContact || hit.kind > worst.kind || hit.normalSpeed > worst.normalSpeed) worst = hit;
                InContact = true;

                UpdateSplinePosition();
                Sensor.Sense(tunnel, Flight.Position, Flight.Rotation, S, probes);
            }

            if (!InContact) return;

            LastHit = worst;
            WallContact?.Invoke(worst);
            if (worst.kind == WallHitKind.Heavy) HeavyHit?.Invoke(worst);
            Health.Damage(worst.damage);
            if (Health.IsDead) Running = false;
        }

        void UpdateSplinePosition()
        {
            TunnelPoint point = tunnel.Project(Flight.Position, S);
            S = point.s;
            Local = point.local;
        }

        bool HasDependencies()
        {
            if (tunnel != null && tunnel.IsReady && flightSettings != null && heatBoostSettings != null && wallSettings != null)
                return true;

            if (!warnedNoTunnel)
            {
                warnedNoTunnel = true;
                Debug.LogWarning(tunnel == null
                    ? "[Ship] No TunnelProvider assigned. The ship waits until a tunnel is set up."
                    : "[Ship] Waiting for the tunnel or settings assets.", this);
            }
            return false;
        }
    }
}
