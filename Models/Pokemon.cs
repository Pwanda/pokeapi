namespace pokeapi2.Models
{
    public class Pokemon
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public List<string> Types { get; set; } = new();
        public int Height { get; set; }
        public int Weight { get; set; }
        public List<string> Abilities { get; set; } = new();
        public bool IsExpanded { get; set; }
    }
    
    public class PokemonListItem
    {
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
    }

    public class PokemonListResponse
    {
        public List<PokemonListItem> Results { get; set; } = new();
    }
}