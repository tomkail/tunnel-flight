using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TunnelFlight.Editor
{
    // Builds the ship's visual rig: hull, wingtip trails and boost jet. Shared by scene setup and the preview renderer.
    public static class ShipRig
    {
        const string Folder = "Assets/Art/Ship";
        const string GlowShader = "TunnelFlight/Additive Glow";

        public struct Parts
        {
            public MantaShipMesh hull;
            public WingtipTrails trails;
            public BoostJet jet;
        }

        public static Parts Build(Transform parent, Ship ship)
        {
            var hullObject = new GameObject("Hull", typeof(MeshFilter), typeof(MeshRenderer), typeof(MantaShipMesh));
            hullObject.transform.SetParent(parent, false);
            hullObject.GetComponent<MeshRenderer>().sharedMaterials = ShipMaterials.LoadOrCreate();
            var hull = hullObject.GetComponent<MantaShipMesh>();
            hull.Rebuild();

            // Wingtip trails
            Material trailMaterial = Glow("WingtipTrail", new Color(1f, 1f, 1f, 1f), 2.5f, falloff: 2f);
            var trailsObject = new GameObject("Wingtip Trails");
            trailsObject.transform.SetParent(parent, false);
            var trails = trailsObject.AddComponent<WingtipTrails>();
            TrailRenderer left = Trail(trailsObject.transform, "Left", trailMaterial, hull.LeftTip);
            TrailRenderer right = Trail(trailsObject.transform, "Right", trailMaterial, hull.RightTip);
            FlightSceneSetup.Set(trails, "ship", ship);
            FlightSceneSetup.Set(trails, "hull", hull);
            FlightSceneSetup.Set(trails, "leftTrail", left);
            FlightSceneSetup.Set(trails, "rightTrail", right);

            // Boost jet
            var jetObject = new GameObject("Boost Jet");
            jetObject.transform.SetParent(parent, false);
            jetObject.transform.localPosition = hull.Nozzle;
            var jet = jetObject.AddComponent<BoostJet>();
            MeshRenderer plume = Flame(jetObject.transform, "Plume", Glow("JetPlume", Color.white, 1f, falloff: 0f, facing: true));
            MeshRenderer core = Flame(jetObject.transform, "Core", Glow("JetCore", Color.white, 1f, falloff: 0f, facing: true));
            MeshRenderer flare = Flame(jetObject.transform, "Flare", Glow("JetFlare", Color.white, 1.5f, falloff: 1f, power: 2.2f));
            flare.transform.localPosition = new Vector3(0f, 0f, -0.05f);
            ParticleSystem sparks = JetSparks(jetObject.transform, Glow("JetSparks", new Color(1f, 0.85f, 0.5f, 1f), 4f, falloff: 1f, power: 1f));

            var lightObject = new GameObject("Jet Light");
            lightObject.transform.SetParent(jetObject.transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 0f, -0.8f);
            var jetLight = lightObject.AddComponent<Light>();
            jetLight.type = LightType.Point;
            jetLight.intensity = 0f;
            jetLight.shadows = LightShadows.None;

            FlightSceneSetup.Set(jet, "ship", ship);
            FlightSceneSetup.Set(jet, "hull", hull);
            FlightSceneSetup.Set(jet, "core", core);
            FlightSceneSetup.Set(jet, "plume", plume);
            FlightSceneSetup.Set(jet, "flare", flare);
            FlightSceneSetup.Set(jet, "sparks", sparks);
            FlightSceneSetup.Set(jet, "jetLight", jetLight);
            FlightSceneSetup.Set(sparks.gameObject.AddComponent<TunnelParticleCollision>(), "ship", ship);

            return new Parts { hull = hull, trails = trails, jet = jet };
        }

        public static Material HudLineMaterial() => Glow("HudLines", Color.white, 1f, falloff: 2f, power: 1.2f);

        // HUD lines drawn over everything (no depth test), for screen-space overlays.
        public static Material HudOverlayMaterial()
        {
            Material material = Glow("HudOverlay", Color.white, 1f, falloff: 2f, power: 1.2f);
            material.SetFloat("_ZTest", (float)CompareFunction.Always);
            material.renderQueue = 3100;
            EditorUtility.SetDirty(material);
            return material;
        }

        public static Material ContactSparksMaterial() => Glow("ContactSparks", new Color(1f, 0.8f, 0.45f, 1f), 3f, falloff: 1f, power: 1f);

        static TrailRenderer Trail(Transform parent, string name, Material material, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = material;
            trail.alignment = LineAlignment.View;
            trail.minVertexDistance = 0.15f;
            trail.numCapVertices = 0;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.time = 0.18f;
            trail.widthMultiplier = 0.06f;
            return trail;
        }

        static MeshRenderer Flame(Transform parent, string name, Material material)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        static ParticleSystem JetSparks(Transform parent, Material material)
        {
            var go = new GameObject("Jet Sparks");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(10f, 35f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.03f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.75f), new Color(1f, 0.55f, 0.15f));
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 2000;

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 28f;
            shape.radius = 0.04f;
            shape.rotation = new Vector3(0f, 180f, 0f);

            // Sparks keep most of the ship's velocity, so they stream back past the camera rather than vanishing.
            var inherit = ps.inheritVelocity;
            inherit.enabled = true;
            inherit.mode = ParticleSystemInheritVelocityMode.Initial;
            inherit.curveMultiplier = 0.75f;

            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.5f, 0.15f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            colour.color = gradient;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.012f;
            renderer.lengthScale = 1.5f;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return ps;
        }

        static Material Glow(string name, Color color, float intensity, float falloff, float power = 1.5f, bool facing = false)
        {
            System.IO.Directory.CreateDirectory(Folder);
            string path = $"{Folder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find(GlowShader));
            material.SetColor("_Color", color);
            material.SetFloat("_Intensity", intensity);
            material.SetFloat("_Falloff", falloff);
            material.SetFloat("_FalloffPower", power);
            material.SetFloat("_Facing", facing ? 1f : 0f);
            material.SetFloat("_FacingPower", 1.5f);
            material.SetFloat("_FacingMin", 0.35f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
