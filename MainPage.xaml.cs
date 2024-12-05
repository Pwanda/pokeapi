using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using pokeapi2.Models;

namespace pokeapi2;

public partial class MainPage : ContentPage
{
    private readonly HttpClient _httpClient;
    private bool isSearching;
    private List<PokemonListItem> allPokemon = new();
    private int currentPage = 1;
    private const int itemsPerPage = 50;
    private Dictionary<string, Pokemon> pokemonCache = new();

    public MainPage()
    {
        InitializeComponent();
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://pokeapi.co/api/v2/")
        };
        _ = InitializePokedex();
    }

    private async Task InitializePokedex()
    {
        LoadingGrid.IsVisible = true;
        LoadingIndicator.IsRunning = true;
        LoadingProgressBar.Progress = 0;
        await LoadAllPokemon();
        await LoadCurrentPage();
        UpdatePageLabels();
        LoadingGrid.IsVisible = false;
        LoadingIndicator.IsRunning = false;
    }

    private async Task LoadAllPokemon()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<PokemonListResponse>("pokemon?limit=1000");
            if (response?.Results != null)
            {
                allPokemon = response.Results;
                await LoadCurrentPage();
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", "Fehler beim Laden der Pokemon-Liste", "OK");
        }
    }

    private async Task LoadCurrentPage()
    {
        try
        {
            LoadingGrid.IsVisible = true;
            PokemonGridView.ItemsSource = null;
            
            var pageItems = allPokemon
                .Skip((currentPage - 1) * itemsPerPage)
                .Take(itemsPerPage)
                .ToList();

            var total = pageItems.Count;
            var current = 0;
            var pokemonDetails = new List<Pokemon>();

            foreach (var item in pageItems)
            {
                var pokemon = await GetPokemon(item.Name);
                if (pokemon != null)
                {
                    pokemonDetails.Add(pokemon);
                }
                current++;
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    LoadingProgressBar.Progress = (double)current / total;
                });
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                PokemonGridView.ItemsSource = pokemonDetails;
                UpdatePageLabels();
            });
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", "Fehler beim Laden der Pokemon", "OK");
        }
        finally
        {
            LoadingGrid.IsVisible = false;
        }
    }

    private void UpdatePageLabels()
    {
        int totalPages = (allPokemon.Count + itemsPerPage - 1) / itemsPerPage;
        int totalPokemon = allPokemon.Count;
        int startNum = (currentPage - 1) * itemsPerPage + 1;
        int endNum = Math.Min(currentPage * itemsPerPage, totalPokemon);

        PageIndicator.Text = $"Pokemon {startNum}-{endNum} von {totalPokemon}";
        PrevButton.IsEnabled = currentPage > 1;
        NextButton.IsEnabled = currentPage < totalPages;

        // Erstelle die Auswahlmöglichkeiten für die Seitenzahlen
        var pageNumbers = Enumerable.Range(1, totalPages).Select(p => p.ToString()).ToList();
        PagePicker.ItemsSource = pageNumbers;
        PagePicker.SelectedIndex = currentPage - 1;
    }

    private async void OnPrevClicked(object sender, EventArgs e)
    {
        if (currentPage > 1)
        {
            currentPage--;
            await LoadCurrentPage();
            UpdatePageLabels();
        }
    }

    private async void OnNextClicked(object sender, EventArgs e)
    {
        int totalPages = (allPokemon.Count + itemsPerPage - 1) / itemsPerPage;
        if (currentPage < totalPages)
        {
            currentPage++;
            await LoadCurrentPage();
            UpdatePageLabels();
        }
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.NewTextValue))
        {
            SuggestionsView.IsVisible = false;
            return;
        }

        var suggestions = allPokemon
            .Where(p => p.Name.Contains(e.NewTextValue.ToLower()))
            .Take(10)
            .ToList();

        SuggestionsView.ItemsSource = suggestions;
        SuggestionsView.IsVisible = suggestions.Any();
    }

    private async void OnSuggestionSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is PokemonListItem selected)
        {
            SearchEntry.Text = selected.Name;
            SuggestionsView.IsVisible = false;
            await SearchPokemon(selected.Name);
        }
    }

    private async void OnSearchSubmitted(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SearchEntry.Text)) return;
        await SearchPokemon(SearchEntry.Text.ToLower().Trim());
    }

    private async Task SearchPokemon(string name)
    {
        if (isSearching) return;

        isSearching = true;
        LoadingGrid.IsVisible = true;
        LoadingProgressBar.Progress = 0;

        try
        {
            var pokemon = await GetPokemon(name);
            if (pokemon != null)
            {
                PokemonImage.Source = pokemon.ImageUrl;
                PokemonName.Text = pokemon.Name.ToUpper();
                PokemonTypes.Text = $"Types: {string.Join(", ", pokemon.Types)}";
                PokemonHeight.Text = $"Height: {pokemon.Height/10.0}m";
                PokemonWeight.Text = $"Weight: {pokemon.Weight/10.0}kg";
                PokemonAbilities.Text = $"Abilities: {string.Join(", ", pokemon.Abilities)}";
                PokemonFrame.IsVisible = true;

                PokemonGridView.ItemsSource = new List<Pokemon> { pokemon };
                PrevButton.IsVisible = false;
                NextButton.IsVisible = false;
                PageIndicator.IsVisible = false;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", "Pokemon nicht gefunden", "OK");
            PokemonFrame.IsVisible = false;
        }
        finally
        {
            isSearching = false;
            LoadingGrid.IsVisible = false;
        }
    }

    private async Task<Pokemon> GetPokemon(string name)
    {
        if (pokemonCache.ContainsKey(name))
        {
            return pokemonCache[name];
        }

        try
        {
            var response = await _httpClient.GetFromJsonAsync<JsonObject>($"pokemon/{name}");
            if (response == null) return null;

            var pokemon = new Pokemon
            {
                Id = response["id"].GetValue<int>(),
                Name = response["name"].GetValue<string>().ToUpperInvariant(),
                ImageUrl = response["sprites"]["front_default"].GetValue<string>(),
                Types = response["types"].AsArray()
                    .Select(t => t.AsObject()["type"]["name"].GetValue<string>())
                    .ToList(),
                Height = response["height"].GetValue<int>(),
                Weight = response["weight"].GetValue<int>(),
                Abilities = response["abilities"].AsArray()
                    .Select(a => a.AsObject()["ability"]["name"].GetValue<string>())
                    .ToList()
            };

            pokemonCache[name] = pokemon;
            return pokemon;
        }
        catch
        {
            return null;
        }
    }

    private void OnPokemonSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is Pokemon selectedPokemon)
        {
            // Hole die aktuelle Liste von Pokémon
            var currentPokemon = PokemonGridView.ItemsSource as List<Pokemon>;

            // Schließe vorher geöffnete Pokémon
            foreach (var pokemon in currentPokemon)
            {
                if (pokemon != selectedPokemon)
                {
                    pokemon.IsExpanded = false;
                }
            }

            // Toggle den Zustand des ausgewählten Pokémon
            selectedPokemon.IsExpanded = !selectedPokemon.IsExpanded;

            // Aktualisiere die Ansicht
            PokemonGridView.ItemsSource = null;
            PokemonGridView.ItemsSource = currentPokemon;
        }

        // Lösche die Auswahl
        PokemonGridView.SelectedItem = null;
    }



    private async void OnResetClicked(object sender, EventArgs e)
    {
        SearchEntry.Text = "";
        PokemonFrame.IsVisible = false;
        PrevButton.IsVisible = true;
        NextButton.IsVisible = true;
        PageIndicator.IsVisible = true;
        await ResetGridView();
    }

    private async Task ResetGridView()
    {
        await LoadCurrentPage();
        UpdatePageLabels();
    }
    
    private void OnPokemonTapped(object sender, EventArgs e)
    {
        var pokemon = (sender as Grid)?.BindingContext as Pokemon;
        if (pokemon != null)
        {
            pokemon.IsExpanded = !pokemon.IsExpanded;
        }
    }
    
    private async void OnPagePickerSelectedIndexChanged(object sender, EventArgs e)
    {
        if (PagePicker.SelectedIndex != -1)
        {
            currentPage = PagePicker.SelectedIndex + 1;
            await LoadCurrentPage();
            UpdatePageLabels();
        }
    }
}