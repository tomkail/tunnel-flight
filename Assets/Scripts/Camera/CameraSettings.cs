using UnityEngine;

namespace TunnelFlight
{
    [CreateAssetMenu(menuName = "Tunnel Flight/Camera Settings")]
    public sealed class CameraSettings : ScriptableObject
    {
        [Header("Follow")]
        [Tooltip("Arc length behind the ship's spline position, metres.")]
        [Range(0f, 10f)] public float followDistance = 4f;

        [Tooltip("Shorten the follow distance on tight bends so the camera keeps line of sight to the ship.")]
        public bool limitFollowToBendRadius = true;

        [Tooltip("Max follow distance as a fraction of the tightest bend radius between camera and ship.")]
        [Range(0.1f, 2f)] public float bendRadiusFollowFactor = 0.5f;

        [Tooltip("Blend the camera position from the spline (0) toward the ship (1).")]
        [Range(0f, 1f)] public float positionBlend = 0f;

        [Tooltip("Roll follow damping, seconds. 0 snaps to the ship's roll.")]
        [Range(0f, 0.5f)] public float rollDamping = 0.05f;

        [Header("Lift")]
        [Tooltip("Raise the chase camera off the centreline along the ship's up, metres, so the wings are seen from slightly above instead of edge-on. 0 is the centreline.")]
        [Range(0f, 3f)] public float cameraLift = 0f;
        [Tooltip("How far a lifted camera tilts down toward the ship: 0 keeps looking along the spline, 1 aims at the ship.")]
        [Range(0f, 1f)] public float liftAim = 0.5f;
        [Tooltip("Lift is reduced near the wall to keep at least this much room between the camera and the wall, metres.")]
        [Range(0f, 2f)] public float liftWallClearance = 0.5f;

        [Header("Framing")]
        [Tooltip("Pull the camera back along the spline as far as needed to keep the wingtips and nose inside the safe area. The view direction stays on the spline.")]
        public bool pullBackToFrame = true;
        [Tooltip("Furthest the camera may pull back, metres of arc length. Also limited by the bend-radius rule above.")]
        [Range(2f, 40f)] public float maxFollowDistance = 16f;
        [Tooltip("Seconds to pull out when the ship nears the frame edge. Short, so the ship doesn't leave the frame.")]
        [Range(0f, 1f)] public float pullOutTime = 0.12f;
        [Tooltip("Seconds to ease back in once there's room again. Longer reads as stable.")]
        [Range(0f, 3f)] public float pullInTime = 0.8f;
        [Tooltip("Safe area is a circle (same in every direction, so rolling doesn't change the framing). Off: the screen rectangle.")]
        public bool circularFrame = true;
        [Tooltip("Size of the safe area as a fraction of the vertical half field of view. Below 1 leaves room for Panini's edge crop.")]
        [Range(0.2f, 1f)] public float frameMargin = 0.8f;
        [Tooltip("Last resort when the pull-back limit is reached: turn the camera just enough to keep the ship in the safe area.")]
        public bool keepShipInFrame = true;

        [Header("First person")]
        [Tooltip("Eye position in ship space, metres: over the front of the canopy, so only a sliver of nose shows.")]
        public Vector3 firstPersonEye = new Vector3(0f, 0.3f, 0.7f);
        [Tooltip("Hide the hull in first person. Off keeps the nose and wings in view as a reference.")]
        public bool hideHullInFirstPerson = false;

        [Header("Speed effects")]
        [Tooltip("Field of view at normal max speed, degrees.")]
        public float fovNormal = 90f;

        [Tooltip("Field of view at boost max speed, degrees.")]
        public float fovBoost = 120f;

        [Tooltip("Treat the FOV values as horizontal FOV (converted to Unity's vertical FOV by aspect).")]
        public bool fovIsHorizontal = true;

        [Tooltip("Panini distance at normal max speed.")]
        [Range(0f, 1f)] public float paniniNormal = 0.2f;

        [Tooltip("Panini distance at boost max speed.")]
        [Range(0f, 1f)] public float paniniBoost = 0.7f;

        [Tooltip("Panini crop to fit.")]
        [Range(0f, 1f)] public float paniniCropToFit = 1f;

        [Tooltip("Maps speed (0 = normal max, 1 = boost max) to effect strength. Ease-in so effects mostly arrive above normal max.")]
        public AnimationCurve speedToEffect = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 2f, 0f));

        [Header("Shake")]
        [Tooltip("Shake amplitude per m/s of impact normal speed, metres.")]
        public float shakePerNormalSpeed = 0.02f;

        [Tooltip("Shake decay time constant, seconds.")]
        [Min(0.01f)] public float shakeDecay = 0.25f;

        [Tooltip("Shake noise frequency, Hz.")]
        public float shakeFrequency = 25f;
    }
}
