using AkwariumShared.Dashboard;
using System.Net.Http;
using System.Net.Http.Json;
using AkwariumShared.Dashboard;
namespace Mobilna;

public partial class SensorHistoryPage : ContentPage
{
    private readonly int _aquariumId;
    private readonly string _aquariumName;
    private readonly HttpClient _httpClient;

    private List<SensorLatestDto> _sensors = new();
    private string _currentRange = "week";  

    public SensorHistoryPage(int aquariumId, string aquariumName)
    {
        InitializeComponent();

        _aquariumId = aquariumId;
        _aquariumName = aquariumName;

        Title = $"Historia – {aquariumName}";

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(ApiConfig.BaseUrl)   
        };

        SelectedDatePicker.Date = DateTime.Today;

        
        HighlightRangeButton(WeekButton);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_sensors.Count == 0)
            await LoadSensorsAsync();
    }

    private async Task LoadSensorsAsync()
    {
        try
        {
            var url = $"/api/aquariums/{_aquariumId}/sensors/latest";
            var list = await _httpClient.GetFromJsonAsync<List<SensorLatestDto>>(url);

            _sensors = list ?? new List<SensorLatestDto>();
            SensorsPicker.ItemsSource = _sensors;
            SensorsPicker.ItemDisplayBinding = new Binding(nameof(SensorLatestDto.SensorName));
        }
        catch (Exception ex)
        {
            await DisplayAlert("Błąd", "Nie udało się pobrać listy czujników.", "OK");
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    // kliknięcie: Dzień / Tydzień / Miesiąc
    private void OnRangeClicked(object sender, EventArgs e)
    {
        if (sender is Button btn)
        {
            _currentRange = btn.CommandParameter?.ToString() ?? "day";
            HighlightRangeButton(btn);
        }
    }

    private void HighlightRangeButton(Button active)
    {
       
        DayButton.FontAttributes = FontAttributes.None;
        WeekButton.FontAttributes = FontAttributes.None;
        MonthButton.FontAttributes = FontAttributes.None;

        active.FontAttributes = FontAttributes.Bold;
    }

   
    private async void OnLoadHistoryClicked(object sender, EventArgs e)
    {
        if (SensorsPicker.SelectedItem is not SensorLatestDto sensor)
        {
            await DisplayAlert("Brak czujnika", "Wybierz czujnik z listy.", "OK");
            return;
        }

        var selectedDate = SelectedDatePicker.Date.Date;

        DateTime to = selectedDate.AddDays(1);   
        DateTime from = _currentRange switch
        {
            "day" => selectedDate,
            "week" => to.AddDays(-7),
            "month" => to.AddMonths(-1),
            _ => to.AddDays(-7)
        };

        var fromStr = Uri.EscapeDataString(from.ToString("O"));
        var toStr = Uri.EscapeDataString(to.ToString("O"));

        var url = $"/api/sensors/{sensor.SensorId}/history?from={fromStr}&to={toStr}&take=500";

        try
        {
            var history = await _httpClient
                .GetFromJsonAsync<List<SensorHistoryPointDto>>(url);

            var list = history ?? new List<SensorHistoryPointDto>();

            var sorted = list
            .OrderByDescending(x => x.TimeAdded)
            .ToList();

            HistoryCollectionView.ItemsSource = sorted;
            EmptyLabel.IsVisible = sorted.Count == 0;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Błąd", "Nie udało się pobrać historii pomiarów.", "OK");
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }
}
