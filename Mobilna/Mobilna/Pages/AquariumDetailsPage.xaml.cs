using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Json;
using Mobilna.Models.Aquarium;

namespace Mobilna;

public partial class AquariumDetailPage : ContentPage
{
    private readonly int _aquariumId;
    private readonly HttpClient _httpClient;

    public ObservableCollection<SensorLatestDto> Sensors { get; } = new();

    public AquariumDetailPage(int aquariumId, string aquariumName)
    {
        InitializeComponent();

        _aquariumId = aquariumId;
        AquariumNameLabel.Text = aquariumName;

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(ApiConfig.BaseUrl)
        };

        SensorsCollectionView.ItemsSource = Sensors;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadSensorsAsync();
    }

    private async void OnRefreshClicked(object sender, EventArgs e)
    {
        await LoadSensorsAsync();
    }

    private async Task LoadSensorsAsync()
    {
        try
        {
            var list = await _httpClient
                .GetFromJsonAsync<List<SensorLatestDto>>(
                    $"/api/aquariums/{_aquariumId}/sensors/latest");

            Sensors.Clear();

            if (list != null)
            {
                foreach (var s in list)
                    Sensors.Add(s);
            }
        }
        catch (HttpRequestException)
        {
            await DisplayAlert("Błąd", "Brak połączenia z API.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Błąd", "Nie udało się pobrać parametrów akwarium.", "OK");
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    
}
