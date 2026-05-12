using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.Maui.ApplicationModel;

namespace Mobilna;

public partial class AquariumDashboardPage : ContentPage
{
    private readonly int _aquariumId;
    private readonly string _aquariumName;

    private readonly int _userId;          
    private readonly HttpClient _httpClient;

    public AquariumDashboardPage(int aquariumId, string aquariumName, int userId)
    {
        InitializeComponent();

        _aquariumId = aquariumId;
        _aquariumName = aquariumName;
        _userId = userId; 

        Title = aquariumName;
        AquariumNameLabel.Text = aquariumName;

        // klient do AkwariumApi
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(ApiConfig.BaseUrl) 
        };
    }

    //  Czujniki i parametry 
    private async void OnSensorsTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PushAsync(new AquariumDetailPage(_aquariumId, _aquariumName));
    }

    //  Historia 
    private async void OnHistoryTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PushAsync(
         new SensorHistoryPage(_aquariumId, _aquariumName));
    }

    //  Sterowanie
    private async void OnControlTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PushAsync(
        new AquariumControlPage(_aquariumId, _aquariumName));
    }
    //  Ustawienia 
    private async void OnSettingsTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PushAsync(
        new AquariumSettingsPage(_aquariumId, _aquariumName, _userId));
    }

    private async void OnCameraTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PushAsync(new CameraPage());
    }

    //  Kafelek webówki 
    public async void OnWebTapped(object sender, TappedEventArgs e)
    {
        try
        {
            var body = new { UserId = _userId };

            var response = await _httpClient.PostAsJsonAsync(
                "/api/mobile/generate-login-link",
                body);

            response.EnsureSuccessStatusCode();

            var result = await response.Content
                .ReadFromJsonAsync<GenerateLinkResponse>();

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
        public string link { get; set; }
    }
}
