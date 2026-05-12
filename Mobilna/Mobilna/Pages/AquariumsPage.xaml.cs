using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.Maui.ApplicationModel;
using Mobilna.Models.Aquarium;

namespace Mobilna;

public partial class AquariumsPage : ContentPage
{
    private readonly int _userId;
    private readonly HttpClient _httpClient;

    public ObservableCollection<AquariumDto> Aquariums { get; } = new();

    public AquariumsPage(int userId)
    {
        InitializeComponent();

        _userId = userId;

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(ApiConfig.BaseUrl)
        };

        AquariumsCollectionView.ItemsSource = Aquariums;

        ToolbarItems.Add(new ToolbarItem("Wyloguj", null, OnLogoutClicked));
    }

    private void OnLogoutClicked()
    {
        Application.Current.MainPage = new NavigationPage(new AuthLogin());
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAquariumsAsync();
    }

    private async Task LoadAquariumsAsync()
    {
        try
        {
            var list = await _httpClient
                .GetFromJsonAsync<List<AquariumDto>>($"/api/aquariums/{_userId}");

            Aquariums.Clear();

            if (list != null && list.Count > 0)
            {
                foreach (var aq in list)
                    Aquariums.Add(aq);
            }
           
        }
        catch (Exception ex)
        {
            await DisplayAlert("Błąd", "Nie udało się pobrać akwariów.", "OK");
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    private async void OnAquariumTapped(object sender, TappedEventArgs e)
    {
        if (sender is Frame frame && frame.BindingContext is AquariumDto selected)
        {
            await Navigation.PushAsync(
                new AquariumDashboardPage(
                    selected.AquariumId,
                    selected.AquariumName,
                    _userId));
        }
    }

    
    private async void OnGoToWebAppClicked(object sender, EventArgs e)
    {
        try
        {
            var body = new { UserId = _userId };

            var response = await _httpClient.PostAsJsonAsync(
                "/api/mobile/generate-login-link",
                body);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<GenerateLinkResponse>();

            if (result == null || string.IsNullOrWhiteSpace(result.link))
            {
                await DisplayAlert("Błąd", "Serwer nie zwrócił poprawnego linku.", "OK");
                return;
            }

            await Launcher.Default.OpenAsync(result.link);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Błąd", ex.Message, "OK");
        }
    }

    private class GenerateLinkResponse
    {
        public string link { get; set; } = "";
    }
}
