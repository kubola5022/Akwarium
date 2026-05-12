using AkwariumShared.Dashboard;
using Microsoft.Maui.Storage;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace Mobilna;

public partial class AquariumSettingsPage : ContentPage
{
    private readonly int _aquariumId;
    private readonly string _aquariumName;
    private readonly int _userId;
    private readonly HttpClient _httpClient;
    private CancellationTokenSource? _alertsCts;
    public ObservableCollection<ReminderItem> Reminders { get; } = new();
    public ObservableCollection<SensorAlertItem> AlertItems { get; } = new();
    private List<SensorLatestResponse> _allSensors = new();

    


    public AquariumSettingsPage(int aquariumId, string aquariumName, int userId)
    {
        InitializeComponent();

        _aquariumId = aquariumId;
        _aquariumName = aquariumName;
        _userId = userId;

        Title = $"Ustawienia – {aquariumName}";
        AquariumNameEntry.Text = aquariumName;

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(ApiConfig.BaseUrl) 
        };

        BindingContext = this; 
    }

    public async Task OpenWebAppAsync()
    {
        var body = new { UserId = _userId };

        var response = await _httpClient.PostAsJsonAsync(
            "/api/mobile/generate-login-link",
            body);

        response.EnsureSuccessStatusCode();

        var result = await response.Content
            .ReadFromJsonAsync<GenerateLinkResponse>();

        if (result == null || string.IsNullOrWhiteSpace(result.link))
            throw new Exception("Serwer nie zwrócił poprawnego linku.");

        await Launcher.Default.OpenAsync(result.link);
    }

    private async void OnAddAquariumClicked(object sender, EventArgs e)
    {
        var goWeb = await DisplayAlert(
            "Dodaj akwarium",
            "Dodawanie nowych akwariów jest dostępne w aplikacji webowej.\n\nKliknij „Przejdź do aplikacji webowej”, aby otworzyć stronę.",
            "Przejdź do aplikacji webowej",
            "Wyjdź");

        if (!goWeb)
            return;

        try
        {
            await OpenWebAppAsync(); 
        }
        catch (Exception ex)
        {
            await DisplayAlert("Błąd", ex.Message, "OK");
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await LoadAlertsAsync();
        LoadRemindersFromPreferences();
       

       
        _alertsCts?.Cancel();
        _alertsCts = new CancellationTokenSource();
        StartAlertsWatcher(_alertsCts.Token);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _alertsCts?.Cancel();
    }


    private void StartAlertsWatcher(CancellationToken token)
    {
        var interval = TimeSpan.FromSeconds(10); 

        Device.StartTimer(interval, () =>
        {
            if (token.IsCancellationRequested)
                return false;

            System.Diagnostics.Debug.WriteLine($"[ALERT_TIMER] tick {DateTime.Now}");
            _ = CheckSensorAlertsAsync();
            return true;
        });
    }



    private async Task CheckSensorAlertsAsync()
    {
        try
        {
            var sensors = await _httpClient
                .GetFromJsonAsync<List<SensorLatestResponse>>(
                    $"/api/aquariums/{_aquariumId}/sensors/latest");

            if (sensors == null || sensors.Count == 0)
                return;

            var notif = ServiceHelper.GetService<INotification>();

            foreach (var a in AlertItems)
            {
                if (!a.IsActive)
                    continue;

                var sensor = sensors.FirstOrDefault(s => s.SensorId == a.SensorId);
                if (sensor == null || sensor.Value == null)
                    continue;

                double value = sensor.Value.Value;
                double? min = TryParseNullable(a.MinText);
                double? max = TryParseNullable(a.MaxText);

                bool isAlert =
                    (min.HasValue && value < min.Value) ||
                    (max.HasValue && value > max.Value);

                if (!isAlert)
                    continue;

               
                var prefKey = PrefKey($"alert_last_{a.SensorId}");
                var lastTicks = Preferences.Get(prefKey, 0L);
                var lastAlert = lastTicks > 0 ? new DateTime(lastTicks) : DateTime.MinValue;

                if ((DateTime.Now - lastAlert) < TimeSpan.FromMinutes(5))
                    continue;

                Preferences.Set(prefKey, DateTime.Now.Ticks);

                string rangeText = $"{(min?.ToString() ?? "-")}–{(max?.ToString() ?? "-")}";

                await notif.ShowNotificationAsync(
                    $"Alert: {a.Name}",
                    $"Wartość: {value:F2}, dozwolony zakres: {rangeText}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ALERT_CHECK_ERROR] {ex}");
        }
    }



    private double? TryParseNullable(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        if (double.TryParse(text.Replace(',', '.'), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var val))
        {
            return val;
        }

        return null;
    }
    // ================== 1. ZMIANA NAZWY ==================

    private async void OnSaveAquariumNameClicked(object sender, EventArgs e)
    {
        var newName = AquariumNameEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(newName))
        {
            await DisplayAlert("Błąd", "Nazwa nie może być pusta.", "OK");
            return;
        }

        try
        {
            var body = new { AquariumName = newName };
            var response = await _httpClient.PutAsJsonAsync(
                $"/api/aquariums/{_aquariumId}/name", body);

            if (!response.IsSuccessStatusCode)
            {
                await DisplayAlert("Błąd", "Nie udało się zapisać nazwy akwarium.", "OK");
                return;
            }

            await DisplayAlert("OK", "Nazwa została zaktualizowana.", "OK");
            Title = $"Ustawienia – {newName}";
        }
        catch (Exception ex)
        {
            await DisplayAlert("Błąd", "Wystąpił problem podczas zapisu nazwy.", "OK");
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    private async void OnDeleteAlertClicked(object sender, EventArgs e)
    {
        if (sender is not Button btn || btn.BindingContext is not SensorAlertItem item)
            return;

        var confirm = await DisplayAlert(
            "Usuń alert",
            $"Czy na pewno chcesz usunąć alert dla czujnika \"{item.Name}\"?",
            "Usuń",
            "Anuluj");

        if (!confirm)
            return;

        try
        {
            
            AlertItems.Remove(item);

            
            var toSend = new List<ThresholdToUpdate>();

           
            foreach (var a in AlertItems)
            {
                double? min = TryParseNullable(a.MinText);
                double? max = TryParseNullable(a.MaxText);

                if (!a.IsActive || (!min.HasValue && !max.HasValue))
                {
                    min = null;
                    max = null;
                }

                toSend.Add(new ThresholdToUpdate
                {
                    SensorId = a.SensorId,
                    Min = min,
                    Max = max
                });
            }

            
            toSend.Add(new ThresholdToUpdate
            {
                SensorId = item.SensorId,
                Min = null,
                Max = null
            });

            var body = new ThresholdUpdateRequest
            {
                UserId = _userId,
                Thresholds = toSend
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"/api/users/{_userId}/thresholds",
                body);

            if (!response.IsSuccessStatusCode)
            {
                
                await LoadAlertsAsync();

                var msg = await response.Content.ReadAsStringAsync();
                await DisplayAlert("Błąd",
                    $"Nie udało się usunąć alertu.\nStatus: {(int)response.StatusCode} {response.ReasonPhrase}\n{msg}",
                    "OK");
                return;
            }

            await DisplayAlert("OK", "Alert został usunięty.", "OK");
        }
        catch (Exception ex)
        {
            
            await LoadAlertsAsync();
            await DisplayAlert("Błąd", "Wystąpił problem podczas usuwania alertu.", "OK");
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    // ================== 2. ALERTY / PROGI ==================
    private async void OnAddAlertClicked(object sender, EventArgs e)
    {
        if (_allSensors == null || _allSensors.Count == 0)
        {
            await DisplayAlert(
                "Brak czujników",
                "Nie masz jeszcze zdefiniowanych czujników dla tego akwarium.\n\n" +
                "Aby dodać czujnik powiązany z alertem, wejdź do aplikacji webowej " +
                "i w zakładce „Konfiguracja” dodaj nowy czujnik.",
                "OK");
            return;
        }

        
        var availableSensors = _allSensors
            .Where(s => !AlertItems.Any(a => a.SensorId == s.SensorId))
            .ToList();

        if (!availableSensors.Any())
        {
            await DisplayAlert(
                "Brak wolnych czujników",
                "Wszystkie istniejące czujniki mają już przypisane alerty.\n\n" +
                "Jeśli chcesz dodać nowy czujnik, przejdź do aplikacji webowej " +
                "i użyj zakładki „Konfiguracja”.",
                "OK");
            return;
        }

        var choice = await DisplayActionSheet(
            "Wybierz czujnik dla nowego alertu",
            "Anuluj",
            null,
            availableSensors.Select(s => s.SensorName).ToArray());

        if (string.IsNullOrEmpty(choice) || choice == "Anuluj")
            return;

        var sensor = availableSensors.First(s => s.SensorName == choice);

        AlertItems.Add(new SensorAlertItem
        {
            SensorId = sensor.SensorId,
            Name = sensor.SensorName,
            MinText = "",
            MaxText = "",
            IsActive = true
        });
    }
    private async Task LoadAlertsAsync()
    {
        try
        {
            var sensors = await _httpClient
                .GetFromJsonAsync<List<SensorLatestResponse>>(
                    $"/api/aquariums/{_aquariumId}/sensors/latest");

            _allSensors = sensors ?? new List<SensorLatestResponse>();

            AlertItems.Clear();

            foreach (var s in _allSensors)
            {
                
                if (s.MinValue != null || s.MaxValue != null)
                {
                    AlertItems.Add(new SensorAlertItem
                    {
                        SensorId = s.SensorId,
                        Name = s.SensorName,
                        MinText = s.MinValue?.ToString(CultureInfo.InvariantCulture) ?? "",
                        MaxText = s.MaxValue?.ToString(CultureInfo.InvariantCulture) ?? "",
                        IsActive = true
                    });
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Błąd",
                $"Nie udało się pobrać alertów czujników.\n{ex.Message}",
                "OK");
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }



    private async void OnSaveThresholdsClicked(object sender, EventArgs e)
    {
        try
        {
            var toSend = new List<ThresholdToUpdate>();

            foreach (var item in AlertItems)  
            {
                double? min = null;
                double? max = null;

                if (item.IsActive)
                {
                    
                    if (!string.IsNullOrWhiteSpace(item.MinText))
                    {
                        if (!double.TryParse(
                                item.MinText.Replace(',', '.'),
                                NumberStyles.Float,
                                CultureInfo.InvariantCulture,
                                out var parsedMin))
                        {
                            await DisplayAlert("Błąd",
                                $"Nieprawidłowa wartość minimalna dla czujnika {item.Name}.",
                                "OK");
                            return;
                        }
                        min = parsedMin;
                    }

                    
                    if (!string.IsNullOrWhiteSpace(item.MaxText))
                    {
                        if (!double.TryParse(
                                item.MaxText.Replace(',', '.'),
                                NumberStyles.Float,
                                CultureInfo.InvariantCulture,
                                out var parsedMax))
                        {
                            await DisplayAlert("Błąd",
                                $"Nieprawidłowa wartość maksymalna dla czujnika {item.Name}.",
                                "OK");
                            return;
                        }
                        max = parsedMax;
                    }
                }

                
                if (!item.IsActive ||
                    (string.IsNullOrWhiteSpace(item.MinText) &&
                     string.IsNullOrWhiteSpace(item.MaxText)))
                {
                    min = null;
                    max = null;
                }

                toSend.Add(new ThresholdToUpdate
                {
                    SensorId = item.SensorId,
                    Min = min,
                    Max = max
                });
            }

            var body = new ThresholdUpdateRequest
            {
                UserId = _userId,
                Thresholds = toSend
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"/api/users/{_userId}/thresholds",
                body);

            if (!response.IsSuccessStatusCode)
            {
                var msg = await response.Content.ReadAsStringAsync();
                await DisplayAlert("Błąd",
                    $"Nie udało się zapisać alertów.\nStatus: {(int)response.StatusCode} {response.ReasonPhrase}\n{msg}",
                    "OK");
                return;
            }

            
            await LoadAlertsAsync();

            await DisplayAlert("OK", "Alerty czujników zostały zapisane.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Błąd",
                $"Wystąpił problem podczas zapisu alertów.\n{ex.Message}",
                "OK");
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    private bool TryParseDouble(string text, out double value)
    {
        text = text.Replace(',', '.'); // akceptuj 6,15 i 6.15
        return double.TryParse(
            text,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value);
    }

   

    private class UpdateSensorThresholdDto
    {
        public int SensorId { get; set; }
        public double? MinValue { get; set; }
        public double? MaxValue { get; set; }
    }

    // ================== 3. PRZYPOMNIENIA – local Preferences ==================

    private string PrefKey(string suffix) => $"aq_{_aquariumId}_{suffix}";

    private void LoadRemindersFromPreferences()
    {
        Reminders.Clear();

        var json = Preferences.Get(PrefKey("reminders_json"), string.Empty);

        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var list = JsonSerializer.Deserialize<List<ReminderItem>>(json);
                if (list != null)
                {
                    foreach (var r in list)
                    {
                        
                        if (r.ReminderTime == TimeSpan.Zero)
                            r.ReminderTime = new TimeSpan(9, 0, 0);

                        Reminders.Add(r);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

       
        if (Reminders.Count == 0)
        {
            Reminders.Add(new ReminderItem
            {
                Id = 1,
                Name = "Podmiana wody",
                LastDate = DateTime.Today,
                IntervalDays = 7,
                ReminderTime = new TimeSpan(9, 0, 0) // np. 09:00
            });
        }
    }

    private void OnAddReminderClicked(object sender, EventArgs e)
    {
        var newId = Reminders.Any() ? Reminders.Max(r => r.Id) + 1 : 1;

        Reminders.Add(new ReminderItem
        {
            Id = newId,
            Name = "Nowe przypomnienie",
            LastDate = DateTime.Today,
            IntervalDays = 7
        });
    }

    private void SaveRemindersToPreferences()
    {
        var list = Reminders.ToList();
        var json = JsonSerializer.Serialize(list);
        Preferences.Set(PrefKey("reminders_json"), json);
    }
    private async void OnDeleteReminderClicked(object sender, EventArgs e)
    {
        if (sender is not Button btn || btn.BindingContext is not ReminderItem item)
            return;

        var confirm = await DisplayAlert(
            "Usuń powiadomienie",
            $"Czy na pewno chcesz usunąć powiadomienie:\n\n{item.Name} ?",
            "Usuń",
            "Anuluj");

        if (!confirm)
            return;

        try
        {
            
            var notif = ServiceHelper.GetService<INotification>();
            var notificationId = _aquariumId * 100 + item.Id;
            await notif.CancelScheduledNotificationAsync(notificationId);

            
            Reminders.Remove(item);

            
            SaveRemindersToPreferences();

            await DisplayAlert("OK", "Powiadomienie zostało usunięte.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Błąd", $"Nie udało się usunąć powiadomienia.\n{ex.Message}", "OK");
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    private async void OnSaveRemindersClicked(object sender, EventArgs e)
    {
        try
        {
           
            foreach (var r in Reminders)
            {
                if (string.IsNullOrWhiteSpace(r.Name))
                {
                    await DisplayAlert("Błąd", "Nazwa przypomnienia nie może być pusta.", "OK");
                    return;
                }

                if (r.IntervalDays <= 0)
                {
                    await DisplayAlert("Błąd",
                        $"Nieprawidłowy interwał (dni) dla przypomnienia \"{r.Name}\".",
                        "OK");
                    return;
                }
            }

           
            var list = Reminders.ToList();
            var json = JsonSerializer.Serialize(list);
            Preferences.Set(PrefKey("reminders_json"), json);

            var notif = ServiceHelper.GetService<INotification>();

            DateTime NextDate(ReminderItem r)
            {
                
                var baseDt = r.LastDate.Date + r.ReminderTime;

                
                if (r.IntervalDays <= 0)
                    return baseDt > DateTime.Now ? baseDt : DateTime.Now.AddMinutes(1);

                
                var next = baseDt;

                
                while (next <= DateTime.Now)
                    next = next.AddDays(r.IntervalDays);

                return next;
            }

            foreach (var r in Reminders)
            {
                
                var notificationId = _aquariumId * 100 + r.Id;

                
                await notif.CancelScheduledNotificationAsync(notificationId);

                
                var firstTime = NextDate(r);

                await notif.ScheduleRepeatingNotificationAsync(
                    $"Akwarium {_aquariumName}",
                    r.Name,
                    firstTime,
                    TimeSpan.FromDays(r.IntervalDays),
                    notificationId);
            }

            await DisplayAlert("OK",
                "Przypomnienia zostały zapisane i zaplanowano powiadomienia.",
                "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Błąd", "Nie udało się zapisać przypomnień.", "OK");
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    // ================== 4. WYŚWIETLANIE – local Preferences ==================

   

   

    private async void OnDeleteAquariumClicked(object sender, EventArgs e)
    {
        var confirm = await DisplayAlert(
            "Usuń akwarium",
            $"Czy na pewno chcesz usunąć \"{_aquariumName}\" razem z czujnikami i pomiarami?",
            "Usuń",
            "Anuluj");

        if (!confirm)
            return;

        try
        {
            var response = await _httpClient.DeleteAsync(
                $"/api/users/{_userId}/aquariums/{_aquariumId}");

            if (!response.IsSuccessStatusCode)
            {
                await DisplayAlert("Błąd", "Nie udało się usunąć akwarium.", "OK");
                return;
            }

            await DisplayAlert("Usunięto", "Akwarium zostało usunięte.", "OK");

            
            await Navigation.PopToRootAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Błąd", "Wystąpił problem podczas usuwania akwarium.", "OK");
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }
    // ================== POMOCNICZE KLASY DTO ==================

    

    private async void OnDeleteAllRemindersClicked(object sender, EventArgs e)
    {
        var confirm = await DisplayAlert(
            "Usuń wszystkie przypomnienia",
            "Czy na pewno chcesz usunąć wszystkie przypomnienia i anulować ich powiadomienia?",
            "Usuń",
            "Anuluj");

        if (!confirm)
            return;

        try
        {
            var notif = ServiceHelper.GetService<INotification>();

            
            foreach (var r in Reminders.ToList())
            {
                var notificationId = _aquariumId * 100 + r.Id;
                await notif.CancelScheduledNotificationAsync(notificationId);
            }

            Reminders.Clear();
            SaveRemindersToPreferences();

            await DisplayAlert("OK", "Wszystkie przypomnienia zostały usunięte.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Błąd", $"Nie udało się usunąć przypomnień.\n{ex.Message}", "OK");
        }
    }



    private class ThresholdUpdateRequest
    {
        public int UserId { get; set; }
        public List<ThresholdToUpdate> Thresholds { get; set; } = new();
    }

    public class ReminderItem
    {
        public int Id { get; set; }                  
        public string Name { get; set; } = "";      
        public DateTime LastDate { get; set; } = DateTime.Today;
        public int IntervalDays { get; set; } = 7;  
        public TimeSpan ReminderTime { get; set; } = new TimeSpan(9, 0, 0);
    }

    private class GenerateLinkResponse
    {
        public string link { get; set; } = "";
    }
}
