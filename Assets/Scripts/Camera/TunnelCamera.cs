using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TunnelFlight
{
    // Sits on the centreline behind the ship, looks along the spline and rolls with the ship. It pulls back along
    // the spline as far as needed to keep the ship framed.
    // FOV and Panini strength follow speed, not the boost button.
    [RequireComponent(typeof(Camera))]
    public sealed class TunnelCamera : MonoBehaviour
    {
        [SerializeField] Ship ship;
        [SerializeField] CameraSettings settings;
        [Tooltip("Volume whose profile holds the Panini Projection override. Created at runtime if missing.")]
        [SerializeField] Volume volume;
        [Tooltip("Switches between the chase camera and first person.")]
        [SerializeField] InputAction toggleView = new InputAction("Toggle View", InputActionType.Button);
        [Tooltip("Renderers hidden in first person when the setting asks for it (the hull).")]
        [SerializeField] Renderer[] hullRenderers;

        public bool FirstPerson { get; private set; }
        public CameraSettings Settings => settings;

        public float EffectStrength { get; private set; }
        public float FollowDistance { get; private set; }

        Camera cam;
        PaniniProjection panini;
        float roll;
        float rollVelocity;
        float shakeAmplitude;
        float shakeSeed;
        float follow = -1f;
        float followVelocity;

        void Awake()
        {
            cam = GetComponent<Camera>();
            shakeSeed = Random.value * 100f;
            if (volume != null)
            {
                VolumeProfile profile = volume.profile;
                if (!profile.TryGet(out panini)) panini = profile.Add<PaniniProjection>(true);
                panini.distance.overrideState = true;
                panini.cropToFit.overrideState = true;
            }
        }

        void OnEnable()
        {
            if (toggleView.bindings.Count == 0)
            {
                toggleView.AddBinding("<Keyboard>/c");
                toggleView.AddBinding("<Gamepad>/select");
            }
            toggleView.Enable();
            if (ship == null) return;
            ship.HeavyHit += OnHeavyHit;
            ship.Restarted += OnShipRestarted;
        }

        void OnDisable()
        {
            toggleView.Disable();
            if (ship == null) return;
            ship.HeavyHit -= OnHeavyHit;
            ship.Restarted -= OnShipRestarted;
        }

        void OnHeavyHit(WallHit hit) => shakeAmplitude = Mathf.Max(shakeAmplitude, hit.normalSpeed * settings.shakePerNormalSpeed);

        void LateUpdate()
        {
            if (ship == null || settings == null || ship.Tunnel == null || !ship.Tunnel.IsReady) return;
            ITunnel tunnel = ship.Tunnel;
            float shipS = ship.RenderS;

            if (toggleView.WasPressedThisFrame()) SetFirstPerson(!FirstPerson);

            // FOV first: framing depends on it.
            ApplySpeedEffects();

            if (FirstPerson)
            {
                // Eye on the ship, looking along its real heading.
                Pose pose = ship.RenderPose;
                Vector3 eye = pose.position + pose.rotation * settings.firstPersonEye;
                transform.SetPositionAndRotation(eye + pose.rotation * Shake(), pose.rotation);
                return;
            }

            float maxFollow = Mathf.Max(settings.followDistance, settings.maxFollowDistance);
            if (settings.limitFollowToBendRadius)
            {
                float bendRadius = tunnel.MinBendRadius(Mathf.Max(0f, shipS - maxFollow), shipS);
                maxFollow = Mathf.Min(maxFollow, bendRadius * settings.bendRadiusFollowFactor);
            }
            float minFollow = Mathf.Min(settings.followDistance, maxFollow);
            float target = settings.pullBackToFrame ? FollowToFrame(tunnel, shipS, minFollow, maxFollow) : minFollow;

            if (follow < 0f) follow = target;
            float smoothing = target > follow ? settings.pullOutTime : settings.pullInTime;
            follow = smoothing <= 0f ? target : Mathf.SmoothDamp(follow, target, ref followVelocity, smoothing);
            follow = Mathf.Clamp(follow, 0f, maxFollow);
            FollowDistance = follow;

            ComputePose(tunnel, shipS, follow, true, out Vector3 position, out Quaternion rotation);
            if (settings.keepShipInFrame && FrameExcess(position, rotation, out _) > 1f) rotation = TurnToFrame(position, rotation);
            transform.SetPositionAndRotation(position + rotation * Shake(), rotation);
        }

        public void SetFirstPerson(bool value)
        {
            FirstPerson = value;
            bool hide = value && settings != null && settings.hideHullInFirstPerson;
            if (hullRenderers != null)
                foreach (Renderer r in hullRenderers) if (r != null) r.enabled = !hide;
            // Re-enter the chase view from a clean state.
            follow = -1f;
            followVelocity = 0f;
        }

        void OnShipRestarted()
        {
            follow = -1f;
            followVelocity = 0f;
            rollVelocity = 0f;
        }

        // Camera on the spline `follow` metres behind the ship, looking along it and rolled with the ship, then lifted.
        // Candidates use the target roll; the real pose uses the damped one.
        void ComputePose(ITunnel tunnel, float shipS, float followDistance, bool updateRoll, out Vector3 position, out Quaternion rotation)
        {
            float cameraS = shipS - followDistance;
            TunnelFrame frame = tunnel.GetFrame(Mathf.Max(0f, cameraS));
            // Before the start of the track, extend straight back along the first frame.
            if (cameraS < 0f) frame.position += frame.Forward * cameraS;
            Vector3 forward = frame.Forward;
            position = Vector3.Lerp(frame.position, ship.RenderPose.position, settings.positionBlend);

            float r = roll;
            Vector3 shipUp = Vector3.ProjectOnPlane(ship.RenderPose.rotation * Vector3.up, forward);
            if (shipUp.sqrMagnitude > 1e-4f)
            {
                float targetRoll = Vector3.SignedAngle(frame.Up, shipUp, forward);
                if (updateRoll)
                {
                    roll = settings.rollDamping <= 0f ? targetRoll : Mathf.SmoothDampAngle(roll, targetRoll, ref rollVelocity, settings.rollDamping);
                    r = roll;
                }
                else r = targetRoll;
            }
            rotation = Quaternion.AngleAxis(r, forward) * frame.rotation;

            if (settings.cameraLift > 0f)
            {
                Vector3 up = rotation * Vector3.up;
                float lift = LimitLift(tunnel, frame, Mathf.Max(0f, cameraS), position, up, settings.cameraLift);
                position += up * lift;
                // Tilt down toward the ship by a fraction of the angle to it.
                if (settings.liftAim > 0f && followDistance > 0.01f)
                    rotation *= Quaternion.Euler(Mathf.Atan2(lift, followDistance) * Mathf.Rad2Deg * settings.liftAim, 0f, 0f);
            }
        }

        // Largest lift up to `lift` that keeps the camera the set clearance inside the wall.
        float LimitLift(ITunnel tunnel, TunnelFrame frame, float s, Vector3 position, Vector3 up, float lift)
        {
            Vector3 offset = position - frame.position;
            var local = new Vector2(Vector3.Dot(offset, frame.Right), Vector3.Dot(offset, frame.Up));
            var upLocal = new Vector2(Vector3.Dot(up, frame.Right), Vector3.Dot(up, frame.Up));
            for (int i = 0; i < 3 && lift > 0f; i++)
            {
                float clearance = tunnel.QueryWall(new TunnelPoint { s = s, local = local + upLocal * lift }).clearance;
                if (clearance >= settings.liftWallClearance) break;
                lift = Mathf.Max(0f, lift - (settings.liftWallClearance - clearance));
            }
            return lift;
        }

        // Shortest follow distance in [min, max] that keeps the ship in the safe area (max if none does).
        float FollowToFrame(ITunnel tunnel, float shipS, float min, float max)
        {
            if (Fits(tunnel, shipS, min)) return min;
            if (!Fits(tunnel, shipS, max)) return max;
            float lo = min, hi = max;
            for (int i = 0; i < 10; i++)
            {
                float mid = 0.5f * (lo + hi);
                if (Fits(tunnel, shipS, mid)) hi = mid; else lo = mid;
            }
            return hi;
        }

        bool Fits(ITunnel tunnel, float shipS, float followDistance)
        {
            ComputePose(tunnel, shipS, followDistance, false, out Vector3 position, out Quaternion rotation);
            return FrameExcess(position, rotation, out _) <= 1f;
        }

        // How far outside the safe area the worst wingtip or nose point is: 1 is on the edge.
        // worstTangent is that point's screen-plane direction (x/z, y/z) in camera space.
        float FrameExcess(Vector3 position, Quaternion rotation, out Vector2 worstTangent)
        {
            float tanV = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * settings.frameMargin;
            float tanH = settings.circularFrame ? tanV : tanV * cam.aspect;
            Pose pose = ship.RenderPose;
            Quaternion inverse = Quaternion.Inverse(rotation);
            float worst = 0f;
            worstTangent = Vector2.zero;

            foreach (WallProbe probe in ship.Probes)
            {
                Vector3 local = inverse * (pose.position + pose.rotation * probe.localOffset - position);
                if (local.z < 0.05f)
                {
                    worstTangent = new Vector2(local.x, local.y);
                    return float.PositiveInfinity;
                }
                float x = local.x / local.z / tanH, y = local.y / local.z / tanV;
                float excess = settings.circularFrame ? Mathf.Sqrt(x * x + y * y) : Mathf.Max(Mathf.Abs(x), Mathf.Abs(y));
                if (excess > worst)
                {
                    worst = excess;
                    worstTangent = new Vector2(local.x / local.z, local.y / local.z);
                }
            }
            return worst;
        }

        // Last resort: the smallest turn that brings the worst point back onto the safe area's edge.
        Quaternion TurnToFrame(Vector3 position, Quaternion rotation)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                float excess = FrameExcess(position, rotation, out Vector2 t);
                if (excess <= 1f) break;
                if (float.IsPositiveInfinity(excess))
                {
                    // Beside or behind the camera: aim along it.
                    rotation = rotation * Quaternion.FromToRotation(Vector3.forward, new Vector3(t.x, t.y, 0.5f));
                    continue;
                }
                var from = new Vector3(t.x / excess, t.y / excess, 1f);
                rotation = rotation * Quaternion.FromToRotation(from, new Vector3(t.x, t.y, 1f));
            }
            return rotation;
        }

        Vector3 Shake()
        {
            if (shakeAmplitude <= 1e-4f) return Vector3.zero;
            float t = Time.time * settings.shakeFrequency;
            var offset = new Vector3(
                Mathf.PerlinNoise(shakeSeed, t) * 2f - 1f,
                Mathf.PerlinNoise(shakeSeed + 31f, t) * 2f - 1f,
                0f) * shakeAmplitude;
            shakeAmplitude *= Mathf.Exp(-Time.deltaTime / settings.shakeDecay);
            return offset;
        }

        void ApplySpeedEffects()
        {
            HeatBoostSettings speeds = ship.HeatBoostSettings;
            float t = Mathf.InverseLerp(speeds.normalMaxSpeed, speeds.BoostSpeed, ship.HeatBoost.Speed);
            EffectStrength = Mathf.Clamp01(settings.speedToEffect.Evaluate(t));

            float fov = Mathf.Lerp(settings.fovNormal, settings.fovBoost, EffectStrength);
            cam.fieldOfView = settings.fovIsHorizontal ? Camera.HorizontalToVerticalFieldOfView(fov, cam.aspect) : fov;

            if (panini != null)
            {
                panini.distance.value = Mathf.Lerp(settings.paniniNormal, settings.paniniBoost, EffectStrength);
                panini.cropToFit.value = settings.paniniCropToFit;
            }
        }
    }
}
