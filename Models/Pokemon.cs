using System.Collections.Generic;

namespace pokeapi2.Models
{
    public class Pokemon
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string ImageUrl { get; set; }
        public List<string> Types { get; set; }
        public int Height { get; set; }
        public int Weight { get; set; }
        public List<string> Abilities { get; set; }
        public string FlavorText { get; set; }
    }

    public class PokemonListItem
    {
        public string Name { get; set; }
        public string Url { get; set; }
    }

    public class PokemonListResponse
    {
        public List<PokemonListItem> Results { get; set; }
    }
}