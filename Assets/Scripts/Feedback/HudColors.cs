using UnityEngine;

namespace TunnelFlight
{
    public static class HudColors
    {
        // Red at the wall, through amber and yellow, to cyan with room to spare. Evaluate at clearance / range.
        public static Gradient Danger()
        {
            var g = new Gradient();
            g.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.15f, 0.1f), 0f),
                    new GradientColorKey(new Color(1f, 0.55f, 0.1f), 0.25f),
                    new GradientColorKey(new Color(1f, 0.9f, 0.2f), 0.5f),
                    new GradientColorKey(new Color(0.3f, 0.9f, 1f), 1f),
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }
    }
}
