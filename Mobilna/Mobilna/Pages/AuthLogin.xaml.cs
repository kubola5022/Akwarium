using Microsoft.Identity.Client;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using AkwariumShared.Auth;
namespace Mobilna;

public partial class AuthLogin : ContentPage
{
    private readonly HttpClient _httpClient;

    public AuthLogin()
    {
        InitializeComponent();

        _httpClient = new HttpClient
        {
            
            BaseAddress = new Uri(ApiConfig.BaseUrl)   
        };
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        ErrorLabel.Text = string.Empty;

        var login = UsernameEntry.Text?.Trim();
        var password = PasswordEntry.Text;

        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
        {
            ErrorLabel.Text = "Podaj login i hasło.";
            return;
        }

        var request = new LoginRequest
        {
            Login = login,
            Password = password
        };

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync("/api/auth/login", request);
        }
        catch (Exception ex)
        {
            ErrorLabel.Text = "Brak połączenia z API.";
            System.Diagnostics.Debug.WriteLine(ex);
            return;
        }

        if (response.IsSuccessStatusCode)
        {
            var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();

            if (loginResponse?.Success == true)
            {
                ErrorLabel.Text = string.Empty;

               
                Application.Current.MainPage = new NavigationPage(
                    new AquariumsPage(loginResponse.UserID));

                return;
            }

            ErrorLabel.Text = loginResponse?.Error ?? "Błąd logowania.";
        }
        else if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            ErrorLabel.Text = "Nieprawidłowy login lub hasło.";
        }
        else
        {
            ErrorLabel.Text = $"Błąd serwera nr {(int)response.StatusCode}, spróbuj ponownie później";
        }
    }
    private async void OnRegisterClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new RegisterPage());
    }

}
