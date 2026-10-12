using UnityEngine;

namespace TunnelFlight
{
    // Wind whoosh panned toward the nearest wall: louder, higher and brighter (less low-passed) as it closes in.
    // With no clip assigned it plays a generated wind-noise loop, so it works before real audio exists.
    [RequireComponent(typeof(AudioSource))]
    public sealed class ProximityAudio : MonoBehaviour
    {
        [SerializeField] Ship ship;
        [Tooltip("Used for panning. Defaults to the main camera.")]
        [SerializeField] Transform listener;
        [Tooltip("Generate a wind-noise loop when the AudioSource has no clip.")]
        [SerializeField] bool generateWindIfNoClip = true;
        [SerializeField] AnimationCurve volumeByClearance = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(4f, 0f));
        [SerializeField] AnimationCurve pitchByClearance = new AnimationCurve(new Keyframe(0f, 1.6f), new Keyframe(4f, 0.8f));
        [Tooltip("Low-pass cutoff by clearance, Hz. Opens up as the wall closes in.")]
        [SerializeField] AnimationCurve cutoffByClearance = new AnimationCurve(new Keyframe(0f, 9000f), new Keyframe(4f, 700f));
        [Tooltip("Volume multiplier at boost speed relative to normal max.")]
        [SerializeField] float boostVolumeScale = 1.3f;
        [SerializeField, Range(0f, 1f)] float panAmount = 0.8f;

        AudioSource source;
        AudioLowPassFilter lowPass;
        AudioClip generated;

        void Awake()
        {
            source = GetComponent<AudioSource>();
            lowPass = GetComponent<AudioLowPassFilter>();
            if (lowPass == null) lowPass = gameObject.AddComponent<AudioLowPassFilter>();
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0f;
            if (source.clip == null && generateWindIfNoClip) source.clip = generated = CreateWindClip();
            if (source.clip != null && !source.isPlaying) source.Play();
        }

        void OnDisable()
        {
            if (source != null) source.volume = 0f;
        }

        void OnDestroy()
        {
            if (generated != null) Destroy(generated);
        }

        void LateUpdate()
        {
            if (ship == null || !ship.Running || !ship.Sensor.HasNearest)
            {
                source.volume = 0f;
                return;
            }
            if (listener == null && Camera.main != null) listener = Camera.main.transform;

            WallProbeSample nearest = ship.Sensor.Nearest;
            float clearance = Mathf.Max(0f, nearest.wall.clearance);
            float speedT = Mathf.InverseLerp(ship.HeatBoostSettings.normalMaxSpeed, ship.HeatBoostSettings.BoostSpeed, ship.HeatBoost.Speed);

            source.volume = volumeByClearance.Evaluate(clearance) * Mathf.Lerp(1f, boostVolumeScale, speedT);
            source.pitch = pitchByClearance.Evaluate(clearance);
            lowPass.cutoffFrequency = Mathf.Clamp(cutoffByClearance.Evaluate(clearance), 10f, 22000f);
            if (listener != null)
            {
                Vector3 toWall = (nearest.wallPoint - ship.RenderPose.position).normalized;
                source.panStereo = Vector3.Dot(toWall, listener.right) * panAmount;
            }
        }

        // Band-limited noise with a slow swell, crossfaded end to start so it loops without a click.
        static AudioClip CreateWindClip(int sampleRate = 44100, float seconds = 3f)
        {
            int n = Mathf.RoundToInt(sampleRate * seconds), fade = sampleRate / 5;
            var raw = new float[n + fade];
            var random = new System.Random(20261012);
            float low1 = 0f, low2 = 0f, slow = 0f;
            for (int i = 0; i < raw.Length; i++)
            {
                float white = (float)random.NextDouble() * 2f - 1f;
                low1 += (white - low1) * 0.12f;
                low2 += (low1 - low2) * 0.03f;
                slow += ((float)random.NextDouble() - slow) * 0.00005f;
                // Band-pass (difference of low-passes), gently swelling.
                raw[i] = (low1 - low2) * (0.7f + 0.6f * slow);
            }

            var data = new float[n];
            float peak = 1e-4f;
            for (int i = 0; i < n; i++)
            {
                data[i] = i < fade ? Mathf.Lerp(raw[n + i], raw[i], i / (float)fade) : raw[i];
                peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            }
            for (int i = 0; i < n; i++) data[i] *= 0.8f / peak;

            AudioClip clip = AudioClip.Create("Wind (generated)", n, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
