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
    private const int itemsPerPage = 30; // Reduziert für schnelleres Laden
    private Dictionary<string, Pokemon> pokemonCache = new();
    private List<string> allTypes = new();
    private List<Pokemon> filteredPokemon = new();
    
    public Command<Pokemon> PokemonTappedCommand { get; }

    public MainPage()
    {
        InitializeComponent();
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://pokeapi.co/api/v2/")
        };

        PokemonTappedCommand = new Command<Pokemon>(OnPokemonTappedCommand);
        
        _ = InitializePokedex();
    }

    private async Task InitializePokedex()
    {
        LoadingGrid.IsVisible = true;
        LoadingIndicator.IsRunning = true;
        LoadingProgressBar.Progress = 0;

        var pokemonTask = LoadAllPokemon();
        var filtersTask = InitializeFilters();
        
        await Task.WhenAll(pokemonTask, filtersTask);
        await LoadCurrentPage();
        UpdatePageLabels();
        
        LoadingGrid.IsVisible = false;
        LoadingIndicator.IsRunning = false;
    }

    private async Task InitializeFilters()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<JsonObject>("type");
            if (response != null)
            {
                allTypes = response["results"].AsArray()
                    .Select(t => t.AsObject()["name"].GetValue<string>())
                    .ToList();
            
                allTypes.Insert(0, "Alle Typen");
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    TypeFilter.ItemsSource = allTypes;
                    TypeFilter.SelectedIndex = 0;
                });
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", "Fehler beim Laden der Pokemon-Typen", "OK");
        }
    }

    private async Task LoadAllPokemon()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<PokemonListResponse>("pokemon?limit=1000");
            if (response?.Results != null)
            {
                allPokemon = response.Results;
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
            LoadingProgressBar.Progress = 0;
        
            var pageItems = allPokemon
                .Skip((currentPage - 1) * itemsPerPage)
                .Take(itemsPerPage)
                .ToList();

            var total = pageItems.Count;
            var current = 0;
            var pokemonDetails = new List<Pokemon>();
            var tasks = new List<Task<Pokemon>>();

            // Starte alle Aufrufe parallel
            foreach (var item in pageItems)
            {
                tasks.Add(GetPokemon(item.Name));
            }

            // Warte auf die Tasks und aktualisiere den Fortschritt
            while (tasks.Any())
            {
                var completedTask = await Task.WhenAny(tasks);
                tasks.Remove(completedTask);
            
                if (await completedTask != null)
                {
                    pokemonDetails.Add(await completedTask);
                }
            
                current++;
                var progress = (double)current / total;
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    LoadingProgressBar.Progress = progress;
                });
            }

            filteredPokemon = pokemonDetails;
            await ApplyFilters();

            MainThread.BeginInvokeOnMainThread(() =>
            {
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

    private async Task<Pokemon> GetPokemon(string name)
    {
        if (pokemonCache.ContainsKey(name))
        {
            return pokemonCache[name];
        }

        try
        {
            var pokemonTask = _httpClient.GetFromJsonAsync<JsonObject>($"pokemon/{name}");
            var response = await pokemonTask;
            if (response == null) return null;

            var speciesUrl = response["species"]["url"].GetValue<string>();
            var speciesTask = _httpClient.GetFromJsonAsync<JsonObject>(speciesUrl);
            var speciesResponse = await speciesTask;

            var flavorTextEntry = speciesResponse["flavor_text_entries"].AsArray()
                .FirstOrDefault(e => e.AsObject()["language"]["name"].GetValue<string>() == "de")
                ?? speciesResponse["flavor_text_entries"].AsArray()
                .FirstOrDefault(e => e.AsObject()["language"]["name"].GetValue<string>() == "en");

            var flavorText = flavorTextEntry != null 
                ? flavorTextEntry.AsObject()["flavor_text"].GetValue<string>()
                    .Replace("\f", " ")
                    .Replace("\n", " ")
                    .Replace("\r", " ")
                : "Keine Beschreibung verfügbar.";

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
                    .ToList(),
                FlavorText = flavorText
            };

            pokemonCache[name] = pokemon;
            return pokemon;
        }
        catch
        {
            return null;
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

        var pageNumbers = Enumerable.Range(1, totalPages).Select(p => p.ToString()).ToList();
        PagePicker.ItemsSource = pageNumbers;
        PagePicker.SelectedIndex = currentPage - 1;
    }

    private async void OnApplyFilterClicked(object sender, EventArgs e)
    {
        ApplyFilterButton.IsEnabled = false;
        FilterProgressBar.IsVisible = true;
        FilterProgressBar.Progress = 0;

        try
        {
            var filtered = filteredPokemon.AsEnumerable();
            double progressStep = 0.2;
            
            if (TypeFilter.SelectedIndex > 0)
            {
                string selectedType = allTypes[TypeFilter.SelectedIndex];
                filtered = filtered.Where(p => p.Types.Contains(selectedType));
                await UpdateFilterProgress(progressStep);
            }

            if (double.TryParse(MinHeightFilter.Text, out double minHeight))
            {
                filtered = filtered.Where(p => p.Height/10.0 >= minHeight);
                await UpdateFilterProgress(progressStep * 2);
            }
            if (double.TryParse(MaxHeightFilter.Text, out double maxHeight))
            {
                filtered = filtered.Where(p => p.Height/10.0 <= maxHeight);
                await UpdateFilterProgress(progressStep * 3);
            }

            if (double.TryParse(MinWeightFilter.Text, out double minWeight))
            {
                filtered = filtered.Where(p => p.Weight/10.0 >= minWeight);
                await UpdateFilterProgress(progressStep * 4);
            }
            if (double.TryParse(MaxWeightFilter.Text, out double maxWeight))
            {
                filtered = filtered.Where(p => p.Weight/10.0 <= maxWeight);
                await UpdateFilterProgress(progressStep * 5);
            }

            switch (SortPicker.SelectedIndex)
            {
                case 0: // Name (A-Z)
                    filtered = filtered.OrderBy(p => p.Name);
                    break;
                case 1: // Name (Z-A)
                    filtered = filtered.OrderByDescending(p => p.Name);
                    break;
                case 2: // Nummer (aufsteigend)
                    filtered = filtered.OrderBy(p => p.Id);
                    break;
                case 3: // Nummer (absteigend)
                    filtered = filtered.OrderByDescending(p => p.Id);
                    break;
            }

            var filteredList = filtered.ToList();
            
            MainThread.BeginInvokeOnMainThread(() =>
            {
                PokemonGridView.ItemsSource = filteredList;
                if (filteredList.Count == 0)
                {
                    DisplayAlert("Info", "Keine Pokemon gefunden", "OK");
                }
            });
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", "Fehler beim Filtern der Pokemon", "OK");
        }
        finally
        {
            ApplyFilterButton.IsEnabled = true;
            FilterProgressBar.IsVisible = false;
        }
    }

    private async Task UpdateFilterProgress(double progress)
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            FilterProgressBar.Progress = progress;
        });
        await Task.Delay(100);
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
                filteredPokemon = new List<Pokemon> { pokemon };
                await ApplyFilters();
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

    private void OnPokemonTappedCommand(Pokemon pokemon)
    {
        if (pokemon != null)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                PokemonImage.Source = pokemon.ImageUrl;
                PokemonName.Text = pokemon.Name.ToUpper();
                PokemonTypes.Text = $"Types: {string.Join(", ", pokemon.Types)}";
                PokemonHeight.Text = $"Height: {pokemon.Height/10.0}m";
                PokemonWeight.Text = $"Weight: {pokemon.Weight/10.0}kg";
                PokemonAbilities.Text = $"Abilities: {string.Join(", ", pokemon.Abilities)}";
                PokemonFlavorText.Text = pokemon.FlavorText;
                
                PokemonFrame.IsVisible = true;
                
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    try
                    {
                        await Task.Delay(100);
                        await MainScroll.ScrollToAsync(PokemonFrame, ScrollToPosition.MakeVisible, true);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Scroll error: {ex.Message}");
                    }
                });
            });
        }
    }

    private async void OnResetClicked(object sender, EventArgs e)
    {
        SearchEntry.Text = "";
        TypeFilter.SelectedIndex = 0;
        MinHeightFilter.Text = "";
        MaxHeightFilter.Text = "";
        MinWeightFilter.Text = "";
        MaxWeightFilter.Text = "";
        SortPicker.SelectedIndex = -1;
        PokemonFrame.IsVisible = false;
        PrevButton.IsVisible = true;
        NextButton.IsVisible = true;
        PageIndicator.IsVisible = true;
        currentPage = 1;
        await LoadCurrentPage();
        UpdatePageLabels();
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
    
    private async Task ApplyFilters()
    {
        try
        {
            var filtered = filteredPokemon.AsEnumerable();

            if (TypeFilter.SelectedIndex > 0)
            {
                string selectedType = allTypes[TypeFilter.SelectedIndex];
                filtered = filtered.Where(p => p.Types.Contains(selectedType));
            }

            if (double.TryParse(MinHeightFilter.Text, out double minHeight))
                filtered = filtered.Where(p => p.Height/10.0 >= minHeight);
            if (double.TryParse(MaxHeightFilter.Text, out double maxHeight))
                filtered = filtered.Where(p => p.Height/10.0 <= maxHeight);

            if (double.TryParse(MinWeightFilter.Text, out double minWeight))
                filtered = filtered.Where(p => p.Weight/10.0 >= minWeight);
            if (double.TryParse(MaxWeightFilter.Text, out double maxWeight))
                filtered = filtered.Where(p => p.Weight/10.0 <= maxWeight);

            switch (SortPicker.SelectedIndex)
            {
                case 0: // Name (A-Z)
                    filtered = filtered.OrderBy(p => p.Name);
                    break;
                case 1: // Name (Z-A)
                    filtered = filtered.OrderByDescending(p => p.Name);
                    break;
                case 2: // Nummer (aufsteigend)
                    filtered = filtered.OrderBy(p => p.Id);
                    break;
                case 3: // Nummer (absteigend)
                    filtered = filtered.OrderByDescending(p => p.Id);
                    break;
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                PokemonGridView.ItemsSource = filtered.ToList();
            });
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", "Fehler beim Filtern der Pokemon", "OK");
        }
    }
}