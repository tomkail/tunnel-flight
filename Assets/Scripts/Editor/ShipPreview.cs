using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TunnelFlight.Editor
{
    // Renders the hull to PNGs for review without opening the editor.
    // -executeMethod TunnelFlight.Editor.ShipPreview.BatchRender -previewOut <folder>
    public static class ShipPreview
    {
        static readonly (string name, Vector3 position)[] Views =
        {
            ("three-quarter", new Vector3(2.6f, 1.7f, 2.8f)),
            ("chase", new Vector3(0f, 0.9f, -4.2f)),
            ("top", new Vector3(0f, 4.5f, 0.001f)),
            ("side", new Vector3(4.2f, 0.15f, 0f)),
            ("rear-three-quarter", new Vector3(2.4f, 1.1f, -3.6f)),
            ("game-camera", new Vector3(0f, 0.3f, -4f)),
        };

        public static void BatchRender()
        {
            string folder = ArgValue("-previewOut") ?? "Temp/ShipPreview";
            try
            {
                Render(folder);
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("Tunnel Flight/Render Ship Preview")]
        static void RenderFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Render("Temp/ShipPreview");
            EditorUtility.RevealInFinder("Temp/ShipPreview");
        }

        static void Render(string folder)
        {
            Directory.CreateDirectory(folder);
            // Async compilation renders a cyan placeholder for shaders that aren't compiled yet.
            bool async = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var root = new GameObject("Ship");
            ShipRig.Parts rig = ShipRig.Build(root.transform, null);

            // Full boost, with sparks pre-simulated and trails laid out behind the tips.
            rig.jet.SetIntensityOverride(1f);
            rig.jet.Tick(0.016f);
            foreach (var ps in rig.jet.GetComponentsInChildren<ParticleSystem>()) ps.Simulate(0.4f, true, true);
            // TrailRenderer only draws its newest segment outside the player loop, so the preview
            // stands in a LineRenderer with the same material, width and colours.
            rig.trails.SetSpeedOverride(1f);
            rig.trails.Refresh();
            foreach (var trail in rig.trails.GetComponentsInChildren<TrailRenderer>())
            {
                trail.enabled = false;
                var line = trail.gameObject.AddComponent<LineRenderer>();
                line.sharedMaterial = trail.sharedMaterial;
                line.alignment = LineAlignment.View;
                line.widthMultiplier = trail.widthMultiplier;
                line.widthCurve = trail.widthCurve;
                line.startColor = trail.startColor;
                line.endColor = trail.endColor;
                line.positionCount = 25;
                Vector3 start = trail.transform.position;
                for (int i = 0; i < 25; i++) line.SetPosition(i, start + Vector3.back * (i * 0.35f));
            }

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.4f;
            sun.transform.rotation = Quaternion.Euler(40f, 215f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.25f, 0.27f, 0.32f);

            var cam = new GameObject("Preview Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.09f, 0.11f);
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.05f;

            var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            cam.targetTexture = target;
            var pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);

            foreach (var (name, position) in Views)
            {
                cam.transform.position = position;
                cam.transform.LookAt(new Vector3(0f, 0f, 0f), name == "top" ? Vector3.forward : Vector3.up);
                rig.jet.Tick(0f);
                // The first render after building the scene can miss lighting setup, so render twice.
                cam.Render();
                cam.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                pixels.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(folder, $"ship-{name}.png"), pixels.EncodeToPNG());
            }

            Debug.Log($"[ShipPreview] Wrote {Views.Length} views to {Path.GetFullPath(folder)}");
            cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(target);
            ShaderUtil.allowAsyncCompilation = async;
        }

        static string ArgValue(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
