using UnityEngine;
using UnityEngine.InputSystem;

namespace TunnelFlight
{
    // Sparks, scrape audio, impact audio and rumble on wall contact, plus a low rumble when close to a wall.
    public sealed class ContactFeedback : MonoBehaviour
    {
        [SerializeField] Ship ship;
        [SerializeField] ParticleSystem sparks;
        [Tooltip("Looping grind, played while in contact.")]
        [SerializeField] AudioSource scrapeLoop;
        [SerializeField] AudioSource impactSource;
        [SerializeField] AudioClip heavyHitClip;

        [Header("Scrape")]
        [SerializeField] float sparksPerSecondPerNormalSpeed = 60f;
        [SerializeField] float sparksPerSecondMin = 40f;
        [SerializeField] AnimationCurve scrapeVolumeByNormalSpeed = new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(6f, 1f));

        [Header("Rumble")]
        [SerializeField] bool rumble = true;
        [SerializeField, Range(0f, 1f)] float scrapeRumble = 0.35f;
        [SerializeField, Range(0f, 1f)] float heavyHitRumble = 1f;
        [SerializeField] float heavyHitRumbleDuration = 0.25f;
        [Tooltip("Low rumble by clearance to the nearest wall, metres, so a close wall can be felt before contact.")]
        [SerializeField] AnimationCurve proximityRumbleByClearance = new AnimationCurve(new Keyframe(0f, 0.25f), new Keyframe(1.5f, 0f));

        float contactTime = -1f;
        WallHit contact;
        float heavyRumbleUntil;

        void OnEnable()
        {
            if (ship == null) return;
            ship.WallContact += OnContact;
            ship.HeavyHit += OnHeavyHit;
        }

        void OnDisable()
        {
            if (ship != null)
            {
                ship.WallContact -= OnContact;
                ship.HeavyHit -= OnHeavyHit;
            }
            StopRumble();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) StopRumble();
        }

        void OnContact(WallHit hit)
        {
            contact = hit;
            contactTime = Time.time;
        }

        void OnHeavyHit(WallHit hit)
        {
            if (impactSource != null && heavyHitClip != null) impactSource.PlayOneShot(heavyHitClip);
            heavyRumbleUntil = Time.time + heavyHitRumbleDuration;
        }

        void LateUpdate()
        {
            // Contact events come from FixedUpdate, so treat contact as live for one fixed step.
            bool touching = ship != null && ship.Running && contactTime >= 0f && Time.time - contactTime <= Time.fixedDeltaTime * 1.5f;

            if (sparks != null)
            {
                var emission = sparks.emission;
                emission.rateOverTime = touching ? sparksPerSecondMin + sparksPerSecondPerNormalSpeed * contact.normalSpeed : 0f;
                if (touching)
                {
                    sparks.transform.SetPositionAndRotation(contact.point, Quaternion.LookRotation(-contact.normal));
                    if (!sparks.isPlaying) sparks.Play();
                }
            }

            if (scrapeLoop != null)
            {
                scrapeLoop.volume = touching ? scrapeVolumeByNormalSpeed.Evaluate(contact.normalSpeed) : 0f;
                if (touching && scrapeLoop.clip != null && !scrapeLoop.isPlaying) scrapeLoop.Play();
            }

            UpdateRumble(touching);
        }

        void UpdateRumble(bool touching)
        {
            Gamepad pad = Gamepad.current;
            if (!rumble || pad == null) return;
            float high = Time.time < heavyRumbleUntil ? heavyHitRumble : 0f;
            float low = touching ? scrapeRumble : 0f;
            if (ship != null && ship.Running && ship.Sensor.HasNearest)
                low = Mathf.Max(low, Mathf.Clamp01(proximityRumbleByClearance.Evaluate(Mathf.Max(0f, ship.Sensor.Nearest.wall.clearance))));
            pad.SetMotorSpeeds(Mathf.Max(low, high * 0.6f), Mathf.Max(low * 0.5f, high));
        }

        static void StopRumble() => Gamepad.current?.ResetHaptics();
    }
}
