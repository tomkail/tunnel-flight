using UnityEditor;
using UnityEngine;

namespace TunnelFlight.Editor
{
    // Placeholder materials for the manta hull's submeshes: hull, canopy, wingtip accents.
    public static class ShipMaterials
    {
        const string Folder = "Assets/Art/Ship";

        public static Material[] LoadOrCreate()
        {
            System.IO.Directory.CreateDirectory(Folder);
            return new[]
            {
                Lit("Hull", new Color(0.82f, 0.84f, 0.88f), 0.65f),
                Lit("Canopy", new Color(0.05f, 0.07f, 0.1f), 0.95f),
                Glow("Wingtips", new Color(0.2f, 0.9f, 1f)),
            };
        }

        static Material Lit(string name, Color color, float smoothness)
        {
            string path = $"{Folder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // Keywords set before CreateAsset don't survive import, so re-apply emission on the saved asset.
        public static Material EnsureEmission(Material material)
        {
            if (material.IsKeywordEnabled("_EMISSION")) return material;
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        // Additive glow rather than Lit emission, which loses its keyword on load in this project.
        static Material Glow(string name, Color color)
        {
            string path = $"{Folder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null && material.shader.name == "TunnelFlight/Additive Glow") return material;
            if (material != null) AssetDatabase.DeleteAsset(path);
            material = new Material(Shader.Find("TunnelFlight/Additive Glow"));
            material.SetColor("_Color", color);
            material.SetFloat("_Intensity", 2f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
