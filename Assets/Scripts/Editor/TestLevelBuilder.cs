using System.Collections.Generic;
using System.IO;
using Tunnel;
using Tunnel.Splines;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace TunnelFlight.Editor
{
    // Builds the placeholder test level for build step 1: a spline route with straights, bends and a climb,
    // twist keys and superellipse shape changes, rendered as a smooth emissive-grid tunnel.
    public static class TestLevelBuilder
    {
        const string Folder = "Assets/Settings/Gameplay";

        // Route knots, metres. Auto-smooth tangents keep the camera path C1.
        static readonly Vector3[] Route =
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(0f, 0f, 160f),
            new Vector3(40f, 0f, 290f),
            new Vector3(140f, 20f, 350f),
            new Vector3(240f, 60f, 310f),
            new Vector3(280f, 120f, 210f),
            new Vector3(250f, 170f, 110f),
            new Vector3(160f, 190f, 70f),
            new Vector3(60f, 180f, 130f),
            new Vector3(0f, 150f, 250f),
            new Vector3(-20f, 100f, 390f),
            new Vector3(0f, 60f, 530f),
            new Vector3(0f, 40f, 720f),
            new Vector3(0f, 40f, 920f),
        };

        public static PackageTunnelProvider Build(Ship ship)
        {
            TunnelGeneratorSettings settings = LoadOrCreateSettings();

            var tunnelObject = new GameObject("Tunnel");
            var tunnel = tunnelObject.AddComponent<TunnelBehaviour>();
            tunnel.settings = settings;
            tunnel.restartOnStart = false;
            tunnel.advanceMode = AdvanceMode.LateUpdate;

            var provider = tunnelObject.AddComponent<PackageTunnelProvider>();
            FlightSceneSetup.Set(provider, "tunnel", tunnel);
            FlightSceneSetup.Set(provider, "ship", ship);

            var meshObject = new GameObject("Tunnel Mesh", typeof(MeshFilter), typeof(MeshRenderer));
            meshObject.transform.SetParent(tunnelObject.transform, false);
            meshObject.GetComponent<MeshRenderer>().sharedMaterial = LoadOrCreateWallMaterial();
            ConfigureMesh(meshObject.AddComponent<TunnelMesh>(), tunnel);

            // Emissive grid as an additive layer on the same geometry, so the tunnel reads in the dark.
            var gridObject = new GameObject("Tunnel Grid Glow", typeof(MeshFilter), typeof(MeshRenderer));
            gridObject.transform.SetParent(tunnelObject.transform, false);
            var gridRenderer = gridObject.GetComponent<MeshRenderer>();
            gridRenderer.sharedMaterial = LoadOrCreateGridGlowMaterial();
            gridRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ConfigureMesh(gridObject.AddComponent<TunnelMesh>(), tunnel);

            // One stripe along the tunnel's "floor" (down in the rolled frame), so twist and orientation read at a glance.
            var stripeObject = new GameObject("Tunnel Floor Stripe", typeof(MeshFilter), typeof(MeshRenderer));
            stripeObject.transform.SetParent(tunnelObject.transform, false);
            var stripeRenderer = stripeObject.GetComponent<MeshRenderer>();
            stripeRenderer.sharedMaterial = LoadOrCreateStripeMaterial();
            stripeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ConfigureMesh(stripeObject.AddComponent<TunnelMesh>(), tunnel, uvAround: 1f);

            var levelObject = new GameObject("Level (spline)");
            var container = levelObject.AddComponent<SplineContainer>();
            container.Spline = RouteSpline();
            var level = levelObject.AddComponent<SplineLevel>();
            FlightSceneSetup.Set(level, "tunnel", tunnel);
            FlightSceneSetup.Set(level, "container", container);
            FlightSceneSetup.Set(level, "settings", settings);
            SetKeys(level);

            return provider;
        }

        static void ConfigureMesh(TunnelMesh mesh, TunnelBehaviour tunnel, float uvAround = 8f)
        {
            mesh.tunnel = tunnel;
            mesh.surface = new SmoothSurface { radialSegments = 64, uvAroundTiling = uvAround };
            mesh.settings.spacingMode = RingSpacingMode.CurvatureAdaptive;
            mesh.settings.adaptiveMinSpacing = 0.5f;
            mesh.settings.adaptiveMaxSpacing = 2f;
            mesh.settings.lengthUnit = MeshLengthUnit.Metres;
            mesh.settings.length = 320f;
            mesh.settings.rearMarginSegments = 8f;
            mesh.settings.uvAlong = UvAlongMode.MetresPerTile;
            mesh.settings.metresPerTile = 8f;
            mesh.fade.enabled = false;
        }

        static Spline RouteSpline()
        {
            var spline = new Spline();
            foreach (Vector3 p in Route) spline.Add(new BezierKnot(new float3(p.x, p.y, p.z)), TangentMode.AutoSmooth);
            return spline;
        }

        static void SetKeys(SplineLevel level)
        {
            var roll = new List<RollKey>
            {
                new RollKey { distance = 0f, roll = 0f },
                new RollKey { distance = 250f, roll = 0f },
                new RollKey { distance = 450f, roll = 120f, easing = Easing.SmoothStep },
                new RollKey { distance = 650f, roll = 120f },
                new RollKey { distance = 900f, roll = -60f, easing = Easing.SmoothStep },
                new RollKey { distance = 1150f, roll = 300f, easing = Easing.Linear },
                new RollKey { distance = 1400f, roll = 360f, easing = Easing.SmoothStep },
            };

            var shape = new List<SplineProfileKey>();
            void Key(float d, float a, float b, float n)
            {
                shape.Add(new SplineProfileKey { channel = "a", distance = d, value = a, easing = Easing.SmoothStep });
                shape.Add(new SplineProfileKey { channel = "b", distance = d, value = b, easing = Easing.SmoothStep });
                shape.Add(new SplineProfileKey { channel = "n", distance = d, value = n, easing = Easing.SmoothStep });
            }
            Key(0f, 7f, 7f, 2f);       // Circle at the start: no orientation reference yet.
            Key(150f, 9f, 5f, 2.4f);   // Wide oval.
            Key(400f, 9f, 5f, 2.4f);
            Key(500f, 6.5f, 6.5f, 4f); // Rounded square through the climb.
            Key(800f, 6.5f, 6.5f, 4f);
            Key(900f, 10f, 4.5f, 2f);  // Flat, wide oval with the twist.
            Key(1150f, 10f, 4.5f, 2f);
            Key(1250f, 6f, 6f, 1.4f);  // Diamond-ish.
            Key(1400f, 7f, 5.5f, 3f);

            var so = new SerializedObject(level);
            WriteList(so.FindProperty("roll"), roll.Count, (p, i) =>
            {
                p.FindPropertyRelative("distance").floatValue = roll[i].distance;
                p.FindPropertyRelative("roll").floatValue = roll[i].roll;
                p.FindPropertyRelative("easing").enumValueIndex = (int)roll[i].easing;
            });
            WriteList(so.FindProperty("profile"), shape.Count, (p, i) =>
            {
                p.FindPropertyRelative("channel").stringValue = shape[i].channel;
                p.FindPropertyRelative("distance").floatValue = shape[i].distance;
                p.FindPropertyRelative("value").floatValue = shape[i].value;
                p.FindPropertyRelative("easing").enumValueIndex = (int)shape[i].easing;
            });
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void WriteList(SerializedProperty list, int count, System.Action<SerializedProperty, int> write)
        {
            list.arraySize = count;
            for (int i = 0; i < count; i++) write(list.GetArrayElementAtIndex(i), i);
        }

        static TunnelGeneratorSettings LoadOrCreateSettings()
        {
            string path = $"{Folder}/TestLevelTunnel.asset";
            var settings = AssetDatabase.LoadAssetAtPath<TunnelGeneratorSettings>(path);
            if (settings != null) return settings;
            settings = ScriptableObject.CreateInstance<TunnelGeneratorSettings>();
            settings.integrationMode = IntegrationMode.Local;
            settings.profile = new SuperellipseProfile { initialA = 7f, initialB = 7f, initialN = 2f, segments = 64 };
            settings.rules = new List<TunnelRule>();
            settings.track.generateAllOnRestart = true;
            Directory.CreateDirectory(Folder);
            AssetDatabase.CreateAsset(settings, path);
            return settings;
        }

        // Lit wall with the grid in its base map, so the ship and jet lights flare it up close.
        static Material LoadOrCreateWallMaterial()
        {
            Material material = LoadOrCreate("TunnelWall", "Universal Render Pipeline/Lit");
            material.SetColor("_BaseColor", new Color(0.45f, 0.47f, 0.52f));
            material.SetFloat("_Smoothness", 0.3f);
            material.SetTexture("_BaseMap", GridTexture());
            return Save(material);
        }

        static Material LoadOrCreateGridGlowMaterial()
        {
            Material material = LoadOrCreate("TunnelGridGlow", "TunnelFlight/Additive Glow");
            material.SetTexture("_MainTex", GridTexture());
            material.SetColor("_Color", new Color(0.2f, 0.75f, 1f, 1f));
            material.SetFloat("_Intensity", 1.3f);
            material.SetFloat("_Falloff", 0f);
            material.SetFloat("_FadeStart", 40f);
            material.SetFloat("_FadeEnd", 260f);
            material.SetFloat("_ProximityBoost", 1f);
            material.SetFloat("_WallReact", 1f);
            return Save(material);
        }

        static Material LoadOrCreateStripeMaterial()
        {
            Material material = LoadOrCreate("TunnelFloorStripe", "TunnelFlight/Additive Glow");
            material.SetTexture("_MainTex", StripeTexture());
            material.SetColor("_Color", new Color(1f, 0.45f, 0.1f, 1f));
            material.SetFloat("_Intensity", 2.2f);
            material.SetFloat("_Falloff", 0f);
            material.SetFloat("_FadeStart", 60f);
            material.SetFloat("_FadeEnd", 300f);
            material.SetFloat("_ProximityBoost", 1f);
            return Save(material);
        }

        static Material LoadOrCreate(string name, string shader)
        {
            string path = $"{Folder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null && material.shader.name == shader) return material;
            if (material != null) AssetDatabase.DeleteAsset(path);
            material = new Material(Shader.Find(shader));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static Material Save(Material material)
        {
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        // One tile = 8 m along (u) x an eighth of the way around (v). Major lines on the tile edges, minor between:
        // with 8 tiles around, 32 lines run along the tunnel, every fourth brighter; rings every 4 m, brighter every 8.
        static Texture2D GridTexture() => LoadOrCreateTexture("TunnelGridV2.png", 256, 256, (x, y) =>
        {
            if (x < 8 || y < 8) return (byte)255;
            if ((x >= 128 && x < 131) || (y % 64) < 3) return (byte)140;
            return (byte)14;
        });

        // A single soft stripe at v = 0.5. Profile angles run clockwise from up, so that's 180 degrees: the frame's down.
        static Texture2D StripeTexture() => LoadOrCreateTexture("TunnelFloorStripeV2.png", 16, 256, (x, y) =>
        {
            int d = Mathf.Abs(y - 128);
            return (byte)(d <= 2 ? 255 : d <= 4 ? 110 : 0);
        });

        static Texture2D LoadOrCreateTexture(string file, int width, int height, System.Func<int, int, byte> value)
        {
            string path = $"{Folder}/{file}";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                byte v = value(x, y);
                pixels[y * width + x] = new Color32(v, v, v, 255);
            }
            texture.SetPixels32(pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.anisoLevel = 16;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
