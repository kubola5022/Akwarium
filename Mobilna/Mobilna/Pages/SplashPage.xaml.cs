namespace Mobilna;

public partial class SplashPage : ContentPage
{
    private bool _isAnimating;

    public SplashPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        
        RootLayout.Opacity = 0;
        FishLabel.TranslationY = 0;
        _isAnimating = true;

        
        _ = AnimateFishAsync();

        
        await RootLayout.FadeTo(1, 600, Easing.CubicOut);

        
        await Task.Delay(3000);

        
        Application.Current.MainPage = new NavigationPage(new AuthLogin());
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _isAnimating = false; 
    }

    private async Task AnimateFishAsync()
    {
        while (_isAnimating)
        {
            
            await FishLabel.TranslateTo(0, -12, 400, Easing.SinInOut);
            await FishLabel.TranslateTo(0, 0, 400, Easing.SinInOut);
        }
    }
}
