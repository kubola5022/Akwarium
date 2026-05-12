using System.Net.Http;
using System.Net.Http.Json;

namespace Mobilna;

public partial class RegisterPage : ContentPage
{
    private readonly HttpClient _httpClient;

    public RegisterPage()
    {
        InitializeComponent();

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(ApiConfig.BaseUrl)
        };
    }

    private async void OnRegisterClicked(object sender, EventArgs e)
    {
        var email = EmailEntry.Text?.Trim() ?? "";
        var login = LoginEntry.Text?.Trim() ?? "";
        var password = PasswordEntry.Text ?? "";
        var confirm = ConfirmPasswordEntry.Text ?? "";

        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(login) ||
            string.IsNullOrWhiteSpace(password))
        {
            await DisplayAlert("Błąd", "E-mail, login i hasło są wymagane.", "OK");
            return;
        }

        if (!email.Contains("@"))
        {
            await DisplayAlert("Błąd", "Podaj poprawny adres e-mail.", "OK");
            return;
        }

        if (password != confirm)
        {
            await DisplayAlert("Błąd", "Hasła nie są takie same.", "OK");
            return;
        }

        if (password.Length < 6)
        {
            await DisplayAlert("Błąd", "Hasło musi mieć co najmniej 6 znaków.", "OK");
            return;
        }

        var request = new
        {
            Email = email,
            Login = login,
            Password = password
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/auth/register", request);

            if (!response.IsSuccessStatusCode)
            {
                var msg = await response.Content.ReadAsStringAsync();
                await DisplayAlert("Błąd", $"Rejestracja nie powiodła się.\n{msg}", "OK");
                return;
            }

            await DisplayAlert("Sukces", "Konto zostało utworzone. Możesz się zalogować.", "OK");

            
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Błąd", "Nie udało się połączyć z serwerem.", "OK");
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }
}
