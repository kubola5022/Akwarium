using Microsoft.Maui.Storage;
using Mobilna.Models; 
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using static Mobilna.AquariumControlPage;

namespace Mobilna;

public partial class AquariumControlPage : ContentPage
{
    private readonly int _aquariumId;
    private readonly string _aquariumName;

    private readonly HttpClient _apiClient;

   
    private readonly INotification? _notificationService;

    private bool _isInitializingStates;

    private const string CustomDevicesPrefKeyPrefix = "custom_devices_aq_";
    private readonly HttpClient _espClient;
    public ObservableCollection<ControlledDevice> CustomDevices { get; } = new();
    public ObservableCollection<ScheduleDeviceItem> ScheduleDevices { get; } = new();

    private ControlledDevice? _selectedCustomDevice;
    public ObservableCollection<SchedulePreviewItem> SchedulePreviewItems { get; } = new();
    public ObservableCollection<SchedulePreviewItem> DeletableSchedules { get; } = new();

    public AquariumControlPage(int aquariumId, string aquariumName)
    {
        InitializeComponent();

        _aquariumId = aquariumId;
        _aquariumName = aquariumName;

        // klient do AkwariumApi
        _apiClient = new HttpClient
        {
            BaseAddress = new Uri(ApiConfig.BaseUrl)
        };

        _espClient = new HttpClient
        {
            BaseAddress = new Uri(EspConfig.BaseUrl)  
        };

        _notificationService = TryGetService<INotification>();

        Title = $"Sterowanie – {_aquariumName}";

        CustomDevicesCollectionView.ItemsSource = CustomDevices;
        ScheduleDevicePicker.ItemsSource = ScheduleDevices;
        SchedulePreviewCollectionView.ItemsSource = SchedulePreviewItems;
        DeleteSchedulePicker.ItemsSource = DeletableSchedules;
    }
    private string GetSchedulePrefKey(string deviceId)
    => $"sched_{_aquariumId}_{deviceId}";
    protected override void OnAppearing()
    {
        base.OnAppearing();

        _isInitializingStates = true;

        // podstawowe urządzenia – odczyt stanu z Preferences
        var lightOn = Preferences.Get(GetPrefKey("light"), false);
        var pumpOn = Preferences.Get(GetPrefKey("pump"), false);
        var heaterOn = Preferences.Get(GetPrefKey("heater"), false);
        var filterOn = Preferences.Get(GetPrefKey("filter"), false);


        LightSwitch.IsToggled = lightOn;
        PumpSwitch.IsToggled = pumpOn;
        HeaterSwitch.IsToggled = heaterOn;
        FilterSwitch.IsToggled = filterOn;

        LightStatusLabel.Text = lightOn ? "Oświetlenie włączone" : "Oświetlenie wyłączone";
        PumpStatusLabel.Text = pumpOn ? "Pompa włączona" : "Pompa wyłączona";
        HeaterStatusLabel.Text = heaterOn ? "Grzałka włączona" : "Grzałka wyłączona";
        FilterStatusLabel.Text = filterOn ? "Filtr włączony" : "Filtr wyłączony";

        // dodatkowe urządzenia
        LoadCustomDevices();
        RebuildScheduleDevices();
        RefreshSchedulePreview();
        ApplyScheduleToSwitches();
        _isInitializingStates = false;
    }

    // ------------------- POMOCNICZE -------------------

    private static T? TryGetService<T>() where T : class
        => Application.Current?
               .Handler?
               .MauiContext?
               .Services
               .GetService(typeof(T)) as T;

    private string GetPrefKey(string deviceId)
        => $"device_{deviceId}_aq_{_aquariumId}";

    private Task ShowDeviceNotificationAsync(string title, string message)
    {
        if (_notificationService is null)
        {
            System.Diagnostics.Debug.WriteLine($"[NOTIF] {title}: {message}");
            return Task.CompletedTask;
        }

        return _notificationService.ShowNotificationAsync(title, message);
    }

    // ------------------- WYWOŁANIA DO API -------------------

    private async Task<bool> SendDeviceCommandAsync(string deviceId, bool on)
    {
        try
        {
            var dto = new
            {
                AquariumId = _aquariumId,
                DeviceId = deviceId,
                On = on
            };

            var response = await _apiClient.PostAsJsonAsync("/api/devices/control", dto);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();

                await DisplayAlert(
                    "Błąd serwera",
                    $"Sterowanie urządzeniem '{deviceId}' nie powiodło się.\n" + "spróbuj ponownie póżniej"
                    ,
                    "OK");

                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            await DisplayAlert(
                "Błąd połączenia",
                $"Nie udało się połączyć z API / ESP.\n{ex.Message}",
                "OK");
            return false;
        }
    }

    public class SchedulePreviewItem
    {
        public string DeviceId { get; set; } = "";
        public string DeviceName { get; set; } = "";
        public string Description { get; set; } = "";
    }

    private class StoredSchedule
    {
        public List<int> Days { get; set; } = new();
        public string Start { get; set; } = "";
        public string End { get; set; } = "";
    }

    

    // ------------------- STEROWANIE PODSTAWOWYMI URZĄDZENIAMI -------------------

    private async void OnLightSwitchToggled(object sender, ToggledEventArgs e)
    {
        if (_isInitializingStates) return;

        try
        {
            await SendDeviceCommandAsync("light", e.Value);
            Preferences.Set(GetPrefKey("light"), e.Value);
            LightStatusLabel.Text = e.Value ? "Oświetlenie włączone" : "Oświetlenie wyłączone";
        }
        catch (Exception ex)
        {
            await DisplayAlert("Błąd", $"Nie udało się sterować oświetleniem:\n{ex.Message}", "OK");
        }
    }

    private async void OnPumpSwitchToggled(object sender, ToggledEventArgs e)
    {
        if (_isInitializingStates) return;

        try
        {
            await SendDeviceCommandAsync("pump", e.Value);
            Preferences.Set(GetPrefKey("pump"), e.Value);
            PumpStatusLabel.Text = e.Value ? "Pompa włączona" : "Pompa wyłączona";
        }
        catch (Exception ex)
        {
            await DisplayAlert("Błąd", $"Nie udało się sterować pompą:\n{ex.Message}", "OK");
        }
    }


    private async void OnHeaterSwitchToggled(object sender, ToggledEventArgs e)
    {
        if (_isInitializingStates) return;

        try
        {
            await SendDeviceCommandAsync("heater", e.Value);
            Preferences.Set(GetPrefKey("heater"), e.Value);
            HeaterStatusLabel.Text = e.Value ? "Grzałka włączona" : "Grzałka wyłączona";
        }
        catch (Exception ex)
        {
            await DisplayAlert("Błąd", $"Nie udało się sterować grzałką:\n{ex.Message}", "OK");
        }
    }

    private async void OnFilterSwitchToggled(object sender, ToggledEventArgs e)
    {
        if (_isInitializingStates) return;

        try
        {
            await SendDeviceCommandAsync("filter", e.Value);
            Preferences.Set(GetPrefKey("filter"), e.Value);
            FilterStatusLabel.Text = e.Value ? "Filtr włączony" : "Filtr wyłączony";
        }
        catch (Exception ex)
        {
            await DisplayAlert("Błąd", $"Nie udało się sterować filtrem:\n{ex.Message}", "OK");
        }
    }

    // ------------------- DODATKOWE URZĄDZENIA -------------------

    private void LoadCustomDevices()
    {
        CustomDevices.Clear();

        var key = $"{CustomDevicesPrefKeyPrefix}{_aquariumId}";
        var json = Preferences.Get(key, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            var list = JsonSerializer.Deserialize<List<ControlledDevice>>(json) ?? new();
            foreach (var d in list)
                CustomDevices.Add(d);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    private void SaveCustomDevices()
    {
        var key = $"{CustomDevicesPrefKeyPrefix}{_aquariumId}";
        var json = JsonSerializer.Serialize(CustomDevices.ToList());
        Preferences.Set(key, json);
    }

    private async void OnAddDeviceClicked(object sender, EventArgs e)
    {
        var name = await DisplayPromptAsync("Nowe urządzenie",
            "Podaj nazwę urządzenia (np. 'Napowietrzacz'):",
            "OK", "Anuluj");

        if (string.IsNullOrWhiteSpace(name))
            return;

        var endpoint = await DisplayPromptAsync("Ścieżka ESP",
            "Podaj ścieżkę sterowania w ESP (np. /relay1):",
            "OK", "Anuluj", "/device1");

        if (string.IsNullOrWhiteSpace(endpoint))
            return;

        endpoint = endpoint.Trim();
        if (!endpoint.StartsWith("/"))
            endpoint = "/" + endpoint;

        var device = new ControlledDevice
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name.Trim(),
            Endpoint = endpoint,
            IsOn = false
        };

        CustomDevices.Add(device);
        SaveCustomDevices();
        RebuildScheduleDevices();
    }

    private async void OnCustomDeviceToggled(object sender, ToggledEventArgs e)
    {
        if (_isInitializingStates) return;
        if (sender is not Switch sw || sw.BindingContext is not ControlledDevice device)
            return;

        device.IsOn = e.Value;
        SaveCustomDevices();

       
        
        await SendDeviceCommandAsync(device.Id, e.Value);

        await ShowDeviceNotificationAsync(
            device.Name,
            e.Value ? $"{device.Name} zostało włączone." : $"{device.Name} zostało wyłączone.");
    }

    private void OnCustomDeviceSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedCustomDevice = e.CurrentSelection.FirstOrDefault() as ControlledDevice;
    }

    private async void OnDeleteDeviceClicked(object sender, EventArgs e)
    {
        if (_selectedCustomDevice == null)
        {
            await DisplayAlert("Usuń urządzenie",
                "Najpierw wybierz urządzenie z listy dodatkowych urządzeń.",
                "OK");
            return;
        }

        bool confirm = await DisplayAlert(
            "Usuń urządzenie",
            $"Czy na pewno chcesz usunąć urządzenie '{_selectedCustomDevice.Name}'?",
            "Usuń", "Anuluj");

        if (!confirm)
            return;

        CustomDevices.Remove(_selectedCustomDevice);
        SaveCustomDevices();
        _selectedCustomDevice = null;
        RebuildScheduleDevices();
    }

    public class ControlledDevice
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Endpoint { get; set; } = "";
        public bool IsOn { get; set; }
    }

    // ------------------- HARMONOGRAM – DYNAMICZNY -------------------

    private void OnEditScheduleClicked(object sender, EventArgs e)
    {
        ScheduleFrame.IsVisible = !ScheduleFrame.IsVisible;
    }

    private void RebuildScheduleDevices()
    {
        ScheduleDevices.Clear();

        // 4 urządzenia wbudowane
        ScheduleDevices.Add(new ScheduleDeviceItem
        {
            Id = "light",
            Name = "Oświetlenie",
            Endpoint = "/light"
        });
        ScheduleDevices.Add(new ScheduleDeviceItem
        {
            Id = "pump",
            Name = "Pompa",
            Endpoint = "/pump"
        });
        ScheduleDevices.Add(new ScheduleDeviceItem
        {
            Id = "heater",
            Name = "Grzałka",
            Endpoint = "/heater"
        });
        ScheduleDevices.Add(new ScheduleDeviceItem
        {
            Id = "filter",
            Name = "Filtr",
            Endpoint = "/filter"
        });

        // dodatkowe urządzenia
        foreach (var d in CustomDevices)
        {
            ScheduleDevices.Add(new ScheduleDeviceItem
            {
                Id = d.Id,
                Name = d.Name,
                Endpoint = d.Endpoint
            });
        }

        ScheduleDevicePicker.ItemsSource = null;
        ScheduleDevicePicker.ItemsSource = ScheduleDevices;
    }

    private string? GetSelectedDeviceId()
    {
        if (ScheduleDevicePicker.SelectedItem is ScheduleDeviceItem item)
            return item.Id;
        return null;
    }

    private async void OnSendScheduleClicked(object sender, EventArgs e)
    {
        var deviceId = GetSelectedDeviceId();
        if (string.IsNullOrEmpty(deviceId))
        {
            await DisplayAlert("Błąd", "Wybierz urządzenie dla harmonogramu.", "OK");
            return;
        }

        var days = new List<int>();
        if (MonCheck.IsChecked) days.Add(1);
        if (TueCheck.IsChecked) days.Add(2);
        if (WedCheck.IsChecked) days.Add(3);
        if (ThuCheck.IsChecked) days.Add(4);
        if (FriCheck.IsChecked) days.Add(5);
        if (SatCheck.IsChecked) days.Add(6);
        if (SunCheck.IsChecked) days.Add(7);

        if (days.Count == 0)
        {
            await DisplayAlert("Błąd", "Wybierz chociaż jeden dzień.", "OK");
            return;
        }

        var start = StartTimePicker.Time;
        var end = EndTimePicker.Time;

        if (start >= end)
        {
            await DisplayAlert("Błąd", "Godzina 'Od' musi być wcześniejsza niż 'Do'.", "OK");
            return;
        }

        var dto = new
        {
            AquariumId = _aquariumId,
            DeviceId = deviceId,
            Days = days,
            Start = start.ToString(@"hh\:mm"),
            End = end.ToString(@"hh\:mm")
        };

        try
        {
            var resp = await _apiClient.PostAsJsonAsync("/api/devices/schedule", dto);
            resp.EnsureSuccessStatusCode();

           
            SaveScheduleLocally(deviceId, days, start, end);

            
            RefreshSchedulePreview();

            await DisplayAlert("OK", "Harmonogram wysłany do ESP.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Błąd", $"Nie udało się wysłać harmonogramu, spróbuj ponownie później", "OK");
        }

    }


    private async void OnConfirmDeleteScheduleClicked(object sender, EventArgs e)
    {
        if (DeleteSchedulePicker.SelectedItem is not SchedulePreviewItem selected)
        {
            await DisplayAlert("Błąd", "Wybierz harmonogram do usunięcia.", "OK");
            return;
        }

        bool confirm = await DisplayAlert(
            "Usuń harmonogram",
            $"Czy na pewno chcesz usunąć harmonogram dla:\n\n{selected.DeviceName}\n{selected.Description} ?",
            "Usuń",
            "Anuluj");

        if (!confirm)
            return;

        try
        {
           
            var dto = new
            {
                AquariumId = _aquariumId,
                DeviceId = selected.DeviceId,
                Days = Array.Empty<int>(),
                Start = "",
                End = ""
            };

            var response = await _apiClient.PostAsJsonAsync("/api/devices/schedule/disable", dto);
            response.EnsureSuccessStatusCode();

            
            Preferences.Remove(GetSchedulePrefKey(selected.DeviceId));

            
            RefreshSchedulePreview();
            LoadDeletableSchedules();

            await DisplayAlert("OK", "Harmonogram został usunięty.", "OK");

            
            if (DeletableSchedules.Count == 0)
                DeleteScheduleFrame.IsVisible = false;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Błąd", $"Nie udało się usunąć harmonogramu:\n{ex.Message}", "OK");
        }
    }


    private void OnOpenDeleteSchedulePanelClicked(object sender, EventArgs e)
    {
      
        ScheduleFrame.IsVisible = false;

        LoadDeletableSchedules();

        DeleteScheduleFrame.IsVisible = true;
    }

    private void LoadDeletableSchedules()
    {
        DeletableSchedules.Clear();

        foreach (var item in SchedulePreviewItems) 
        {
            DeletableSchedules.Add(new SchedulePreviewItem
            {
                DeviceId = item.DeviceId,
                DeviceName = item.DeviceName,
                Description = item.Description
            });
        }

       
        if (DeletableSchedules.Count > 0)
            DeleteSchedulePicker.SelectedIndex = 0;
    }

    
    private void OnCancelDeleteScheduleClicked(object sender, EventArgs e)
    {
        DeleteScheduleFrame.IsVisible = false;
    }


    private async void OnDeleteScheduleClicked(object sender, EventArgs e)
    {
        var deviceId = GetSelectedDeviceId();
        if (string.IsNullOrEmpty(deviceId))
        {
            await DisplayAlert("Błąd", "Wybierz urządzenie, dla którego chcesz usunąć harmonogram.", "OK");
            return;
        }

        bool confirm = await DisplayAlert(
        "Usuń harmonogram",
        $"Czy na pewno chcesz usunąć harmonogram dla wybranego urządzenia?",
        "Usuń", "Anuluj");

        if (!confirm)
            return;

        try
        {
            var dto = new
            {
                AquariumId = _aquariumId,
                DeviceId = deviceId,
                Days = Array.Empty<int>(),
                Start = "",
                End = ""
            };

            var response = await _apiClient.PostAsJsonAsync(
                "/api/devices/schedule/disable", dto);
            response.EnsureSuccessStatusCode();         

            
            Preferences.Remove(GetSchedulePrefKey(deviceId));

            
            RefreshSchedulePreview();

            await DisplayAlert("OK", "Harmonogram został usunięty.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Błąd",
                $"Nie udało się usunąć harmonogramu:\n{ex.Message}", "OK");
        }
    }

    public class ScheduleDeviceItem
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Endpoint { get; set; } = "";
    }

    // ------------------- DTO DO KOMUNIKACJI Z API -------------------

    public class DeviceCommandRequest
    {
        public int AquariumId { get; set; }
        public string DeviceId { get; set; } = "";
        public bool On { get; set; }
        public string? Endpoint { get; set; }
    }

    public class DeviceScheduleRequest
    {
        public int AquariumId { get; set; }
        public string DeviceId { get; set; } = "";
        public List<int> Days { get; set; } = new();
        public string Start { get; set; } = "";
        public string End { get; set; } = "";
        public string? Endpoint { get; set; }
    }


    private void SaveScheduleLocally(string deviceId, List<int> days, TimeSpan start, TimeSpan end)
    {
        var stored = new StoredSchedule
        {
            Days = days,
            Start = start.ToString(@"hh\:mm"),
            End = end.ToString(@"hh\:mm")
        };

        var json = JsonSerializer.Serialize(stored);
        Preferences.Set(GetSchedulePrefKey(deviceId), json);
    }

    private void RefreshSchedulePreview()
    {
        SchedulePreviewItems.Clear();

        foreach (var dev in ScheduleDevices)
        {
            var key = GetSchedulePrefKey(dev.Id);
            var json = Preferences.Get(key, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
                continue;

            StoredSchedule? sched;
            try
            {
                sched = JsonSerializer.Deserialize<StoredSchedule>(json);
            }
            catch
            {
                continue;
            }

            if (sched == null || sched.Days == null || sched.Days.Count == 0)
                continue;

            var daysText = FormatDaysShort(sched.Days);
            var desc = $"{daysText} {sched.Start}–{sched.End}";

            SchedulePreviewItems.Add(new SchedulePreviewItem
            {
                DeviceId = dev.Id,
                DeviceName = dev.Name,
                Description = desc
            });
        }

        NoScheduleLabel.IsVisible = SchedulePreviewItems.Count == 0;
    }

    private void ApplyScheduleToSwitches()
    {
        // Urządzenia wbudowane
        if (IsDeviceScheduledOnNow("light"))
        {
            LightSwitch.IsToggled = true;
            LightStatusLabel.Text = "Oświetlenie włączone (wg harmonogramu)";
        }

        if (IsDeviceScheduledOnNow("pump"))
        {
            PumpSwitch.IsToggled = true;
            PumpStatusLabel.Text = "Pompa włączona (wg harmonogramu)";
        }

        if (IsDeviceScheduledOnNow("heater"))
        {
            HeaterSwitch.IsToggled = true;
            HeaterStatusLabel.Text = "Grzałka włączona (wg harmonogramu)";
        }

        if (IsDeviceScheduledOnNow("filter"))
        {
            FilterSwitch.IsToggled = true;
            FilterStatusLabel.Text = "Filtr włączony (wg harmonogramu)";
        }

        
        foreach (var dev in CustomDevices)
        {
            if (IsDeviceScheduledOnNow(dev.Id))
            {
                dev.IsOn = true;
            }
        }

        
        CustomDevicesCollectionView.ItemsSource = null;
        CustomDevicesCollectionView.ItemsSource = CustomDevices;
    }

    private bool IsDeviceScheduledOnNow(string deviceId)
    {
        var key = GetSchedulePrefKey(deviceId);
        var json = Preferences.Get(key, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
            return false;

        StoredSchedule? sched;
        try
        {
            sched = JsonSerializer.Deserialize<StoredSchedule>(json);
        }
        catch
        {
            return false;
        }

        if (sched == null || sched.Days == null || sched.Days.Count == 0)
            return false;

        
        int day = (int)DateTime.Now.DayOfWeek; 
        if (day == 0)
            day = 7;

        if (!sched.Days.Contains(day))
            return false;

        if (!TimeSpan.TryParseExact(sched.Start, @"hh\:mm", CultureInfo.InvariantCulture, out var start))
            return false;
        if (!TimeSpan.TryParseExact(sched.End, @"hh\:mm", CultureInfo.InvariantCulture, out var end))
            return false;

        var now = DateTime.Now.TimeOfDay;
        return now >= start && now < end;
    }

    private string FormatDaysShort(List<int> days)
    {
        var map = new Dictionary<int, string>
    {
        { 1, "Pn" },
        { 2, "Wt" },
        { 3, "Śr" },
        { 4, "Cz" },
        { 5, "Pt" },
        { 6, "So" },
        { 7, "Nd" }
    };

        var sorted = days.Distinct().OrderBy(d => d);
        return string.Join(", ", sorted.Select(d =>
            map.TryGetValue(d, out var name) ? name : d.ToString()));
    }

}
