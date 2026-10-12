using UnityEngine;
using UnityEngine.InputSystem;

namespace TunnelFlight
{
    // Developer readout. The real HUD's form is still open in the design.
    public sealed class DebugHud : MonoBehaviour
    {
        [SerializeField] Ship ship;
        [SerializeField] TunnelCamera tunnelCamera;
        [SerializeField] bool visible = true;
        [Tooltip("Wall-readability aids, each switchable from the HUD for A/B tests. Labelled by GameObject name.")]
        [SerializeField] Behaviour[] aids;

        GUIStyle style;
        GUIStyle toggleStyle;

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame) visible = !visible;
        }

        void OnGUI()
        {
            if (!visible || ship == null) return;
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 16 };
            toggleStyle ??= new GUIStyle(GUI.skin.toggle) { fontSize = 16 };

            GUILayout.BeginArea(new Rect(12, 12, 360, 700), GUI.skin.box);
            if (ship.Tunnel == null || !ship.Tunnel.IsReady)
            {
                GUILayout.Label("Waiting for tunnel", style);
                GUILayout.EndArea();
                return;
            }

            HeatBoostModel hb = ship.HeatBoost;
            GUILayout.Label($"s {ship.S:0.0} / {ship.Tunnel.Length:0} m", style);
            GUILayout.Label($"Speed {hb.Speed:0.0} m/s ({hb.Speed / ship.HeatBoostSettings.normalMaxSpeed:0.00}x)", style);
            GUILayout.Label($"Heat {hb.Heat:0}%  {(hb.ForcedLock ? "FORCED BOOST" : hb.Boosting ? "boost" : "")}", style);
            Bar(hb.Heat / HeatBoostModel.MaxHeat, hb.ForcedLock ? Color.red : new Color(1f, 0.55f, 0.1f));
            GUILayout.Label($"Health {ship.Health.Current:0}", style);
            Bar(ship.Health.Max > 0f ? ship.Health.Current / ship.Health.Max : 0f, Color.green);

            for (int i = 0; i < ship.Sensor.Samples.Count && i < ship.Probes.Count; i++)
                GUILayout.Label($"{ship.Probes[i].name}: {ship.Sensor.Samples[i].wall.clearance:0.00} m", style);

            if (ship.InContact) GUILayout.Label($"Contact: {ship.LastHit.kind}, vn {ship.LastHit.normalSpeed:0.0}", style);
            if (tunnelCamera != null) GUILayout.Label($"Cam follow {tunnelCamera.FollowDistance:0.0} m, fx {tunnelCamera.EffectStrength:0.00}", style);
            GUILayout.Label("C / Select: toggle first person", style);

            if (tunnelCamera != null && tunnelCamera.Settings != null)
            {
                CameraSettings cs = tunnelCamera.Settings;
                GUILayout.Label($"Camera lift {cs.cameraLift:0.00} m", style);
                cs.cameraLift = GUILayout.HorizontalSlider(cs.cameraLift, 0f, 3f);
                GUILayout.Label($"Lift aim {cs.liftAim:0.00}", style);
                cs.liftAim = GUILayout.HorizontalSlider(cs.liftAim, 0f, 1f);
            }
            if (aids != null)
                foreach (Behaviour aid in aids)
                    if (aid != null) aid.enabled = GUILayout.Toggle(aid.enabled, " " + aid.gameObject.name, toggleStyle);
            if (ship.Health.IsDead) GUILayout.Label("DEAD - R / Start to restart", style);
            if (ship.Finished) GUILayout.Label("FINISHED - R / Start to restart", style);
            GUILayout.EndArea();
        }

        void Bar(float t, Color color)
        {
            Rect r = GUILayoutUtility.GetRect(320, 10);
            GUI.DrawTexture(r, Texture2D.grayTexture);
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(t), r.height), Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
