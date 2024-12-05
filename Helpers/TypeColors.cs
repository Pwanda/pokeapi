using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace pokeapi2.Helpers
{
    public static class TypeColors
    {
        public static Dictionary<string, Color> Colors = new()
        {
            { "normal", Color.FromArgb("#A8A878") },
            { "fire", Color.FromArgb("#F08030") },
            { "water", Color.FromArgb("#6890F0") },
            { "electric", Color.FromArgb("#F8D030") },
            { "grass", Color.FromArgb("#78C850") },
            { "ice", Color.FromArgb("#98D8D8") },
            { "fighting", Color.FromArgb("#C03028") },
            { "poison", Color.FromArgb("#A040A0") },
            { "ground", Color.FromArgb("#E0C068") },
            { "flying", Color.FromArgb("#A890F0") },
            { "psychic", Color.FromArgb("#F85888") },
            { "bug", Color.FromArgb("#A8B820") },
            { "rock", Color.FromArgb("#B8A038") },
            { "ghost", Color.FromArgb("#705898") },
            { "dragon", Color.FromArgb("#7038F8") },
            { "dark", Color.FromArgb("#705848") },
            { "steel", Color.FromArgb("#B8B8D0") },
            { "fairy", Color.FromArgb("#EE99AC") }
        };

        public static Color GetTypeColor(string type)
        {
            if (Colors.TryGetValue(type.ToLower(), out var color))
                return color;
            return Colors["normal"];
        }

        public static LinearGradientBrush GetTypeGradient(List<string> types)
        {
            if (types == null || types.Count == 0)
                return new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 1),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Colors["normal"], 0.0f),
                        new GradientStop(Colors["normal"], 1.0f)
                    }
                };

            if (types.Count == 1)
            {
                var color = GetTypeColor(types[0]);
                return new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 1),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(color, 0.0f),
                        new GradientStop(color, 1.0f)
                    }
                };
            }

            return new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(GetTypeColor(types[0]), 0.0f),
                    new GradientStop(GetTypeColor(types[1]), 1.0f)
                }
            };
        }
    }
}