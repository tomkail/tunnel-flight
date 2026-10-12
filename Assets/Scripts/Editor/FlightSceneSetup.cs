using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TunnelFlight.Editor
{
    // Builds the flight scene and the tuning assets. Safe to re-run: existing assets are kept, the scene is rebuilt.
    // The TunnelProvider slot on the ship is left empty until the tunnel package adapter exists.
    public static class FlightSceneSetup
    {
        const string SettingsFolder = "Assets/Settings/Gameplay";
        const string ScenePath = "Assets/Scenes/Flight.unity";

        [MenuItem("Tunnel Flight/Create Flight Scene")]
        public static void CreateFlightScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        // Batch mode entry point: -executeMethod TunnelFlight.Editor.FlightSceneSetup.BatchCreate
        public static void BatchCreate()
        {
            Build();
            EditorApplication.Exit(0);
        }

        static void Build()
        {
            // New scene first: creating it unloads unused assets, which would null references loaded before it.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Directory.CreateDirectory(SettingsFolder);
            var flight = LoadOrCreate<FlightSettings>("FlightSettings");
            var heat = LoadOrCreate<HeatBoostSettings>("HeatBoostSettings");
            var walls = LoadOrCreate<WallSettings>("WallSettings");
            var cameraSettings = LoadOrCreate<CameraSettings>("CameraSettings");
            VolumeProfile profile = LoadOrCreateVolumeProfile();
            Material sparkMaterial = ShipRig.ContactSparksMaterial();

            // Ship
            var ship = new GameObject("Ship");
            var input = ship.AddComponent<ShipInput>();
            var shipComponent = ship.AddComponent<Ship>();
            Set(shipComponent, "input", input);
            Set(shipComponent, "flightSettings", flight);
            Set(shipComponent, "heatBoostSettings", heat);
            Set(shipComponent, "wallSettings", walls);

            // Hull, wingtip trails and boost jet. The ship takes its wingtip and nose probes from the hull.
            ShipRig.Parts rig = ShipRig.Build(ship.transform, shipComponent);
            Set(shipComponent, "hullShape", rig.hull);

            var lightObject = new GameObject("Ship Light");
            lightObject.transform.SetParent(ship.transform, false);
            var shipLight = lightObject.AddComponent<Light>();
            shipLight.type = LightType.Point;
            shipLight.range = 6f;
            shipLight.intensity = 1f;
            Set(lightObject.AddComponent<ProximityLight>(), "ship", shipComponent);

            var whoosh = new GameObject("Whoosh Audio");
            whoosh.transform.SetParent(ship.transform, false);
            whoosh.AddComponent<AudioSource>().playOnAwake = false;
            Set(whoosh.AddComponent<ProximityAudio>(), "ship", shipComponent);

            var scrape = new GameObject("Scrape Audio");
            scrape.transform.SetParent(ship.transform, false);
            var scrapeSource = scrape.AddComponent<AudioSource>();
            scrapeSource.playOnAwake = false;
            scrapeSource.loop = true;

            var impact = new GameObject("Impact Audio");
            impact.transform.SetParent(ship.transform, false);
            var impactSource = impact.AddComponent<AudioSource>();
            impactSource.playOnAwake = false;

            ParticleSystem sparks = CreateSparks(sparkMaterial);
            Set(sparks.gameObject.AddComponent<TunnelParticleCollision>(), "ship", shipComponent);
            var contact = ship.AddComponent<ContactFeedback>();
            Set(contact, "ship", shipComponent);
            Set(contact, "sparks", sparks);
            Set(contact, "scrapeLoop", scrapeSource);
            Set(contact, "impactSource", impactSource);

            // Tunnel: the package's TunnelBehaviour, adapted to ITunnel, playing the spline test level.
            PackageTunnelProvider tunnelProvider = TestLevelBuilder.Build(shipComponent);
            Set(shipComponent, "tunnel", tunnelProvider);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.12f, 0.13f, 0.16f);
            // No sky inside a tunnel: without this the walls reflect the default skybox.
            RenderSettings.skybox = null;
            RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;

            // Post-processing volume with the Panini override.
            var volumeObject = new GameObject("Global Volume");
            var volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;

            // Camera
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = cameraObject.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 2000f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            var tunnelCamera = cameraObject.AddComponent<TunnelCamera>();
            Set(tunnelCamera, "ship", shipComponent);
            Set(tunnelCamera, "settings", cameraSettings);
            Set(tunnelCamera, "volume", volume);
            var hullRenderer = rig.hull.GetComponent<MeshRenderer>();
            var cameraSo = new SerializedObject(tunnelCamera);
            SerializedProperty hullList = cameraSo.FindProperty("hullRenderers");
            hullList.arraySize = 1;
            hullList.GetArrayElementAtIndex(0).objectReferenceValue = hullRenderer;
            cameraSo.ApplyModifiedPropertiesWithoutUndo();

            var wallHud = new GameObject("Wall Distance HUD", typeof(MeshFilter), typeof(MeshRenderer));
            var wallHudRenderer = wallHud.GetComponent<MeshRenderer>();
            wallHudRenderer.sharedMaterial = ShipRig.HudLineMaterial();
            wallHudRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var wallHudComponent = wallHud.AddComponent<WallDistanceHud>();
            Set(wallHudComponent, "ship", shipComponent);
            Set(wallHudComponent, "viewCamera", cam);

            // Wall-readability aids, each switchable from the debug HUD.
            Material hudLines = ShipRig.HudLineMaterial();
            var ring = LineRenderer<CrossSectionRing>("Cross-Section Ring", hudLines, shipComponent, cam);
            Set(ring, "tunnelCamera", tunnelCamera);
            var map = LineRenderer<CrossSectionMap>("Cross-Section Map", ShipRig.HudOverlayMaterial(), shipComponent, cam);
            Set(map, "hull", rig.hull);
            var streaks = LineRenderer<WallStreaks>("Wall Streaks", hudLines, shipComponent, cam);
            var reaction = new GameObject("Wall Reaction").AddComponent<WallReaction>();
            Set(reaction, "ship", shipComponent);

            var hud = new GameObject("Debug HUD").AddComponent<DebugHud>();
            Set(hud, "ship", shipComponent);
            Set(hud, "tunnelCamera", tunnelCamera);
            var hudSo = new SerializedObject(hud);
            Behaviour[] aids = { wallHudComponent, ring, reaction, map, streaks, whoosh.GetComponent<ProximityAudio>() };
            SerializedProperty aidList = hudSo.FindProperty("aids");
            aidList.arraySize = aids.Length;
            for (int i = 0; i < aids.Length; i++) aidList.GetArrayElementAtIndex(i).objectReferenceValue = aids[i];
            hudSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log($"[FlightSceneSetup] Built {ScenePath} with the spline test level.");
        }

        // A GlowLineMesh-drawing component on its own renderer.
        static T LineRenderer<T>(string name, Material material, Ship ship, Camera cam) where T : MonoBehaviour
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var component = go.AddComponent<T>();
            Set(component, "ship", ship);
            Set(component, "viewCamera", cam);
            return component;
        }

        static ParticleSystem CreateSparks(Material material)
        {
            var go = new GameObject("Sparks");
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 14f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
            main.startColor = new Color(1f, 0.75f, 0.3f);
            main.gravityModifier = 0.5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 500;

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.05f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.03f;
            renderer.sharedMaterial = material;
            return ps;
        }

        static T LoadOrCreate<T>(string name) where T : ScriptableObject
        {
            string path = $"{SettingsFolder}/{name}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static VolumeProfile LoadOrCreateVolumeProfile()
        {
            string path = $"{SettingsFolder}/FlightVolumeProfile.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            if (!profile.Has<PaniniProjection>())
            {
                var panini = profile.Add<PaniniProjection>(true);
                panini.name = nameof(PaniniProjection);
                AssetDatabase.AddObjectToAsset(panini, profile);
            }
            EditorUtility.SetDirty(profile);
            return profile;
        }

        static void AddToBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == ScenePath)) return;
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        public static void Set(Object target, string field, Object value)
        {
            if (target == null) return;
            var so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"[FlightSceneSetup] {target.GetType().Name} has no serialized field '{field}'.");
                return;
            }
            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
