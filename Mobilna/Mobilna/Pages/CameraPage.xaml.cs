namespace Mobilna;

public partial class CameraPage : ContentPage
{
    
    private const string StreamUrl = "http://10.71.91.134/";

    public CameraPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        Overlay.IsVisible = true;
        LoadingIndicator.IsRunning = true;
        StatusLabel.Text = "Łączenie z kamerą...";

        
        var html = $@"
<html>
  <head>
    <meta name='viewport' content='width=device-width, initial-scale=1, maximum-scale=1' />
    <style>
      html, body {{
        margin: 0;
        padding: 0;
        background: #000000;
        width: 100%;
        height: 100%;
        overflow: hidden;
      }}
      img {{
        width: 100%;
        height: 100%;
        object-fit: cover; /* przytnie, ale wypełni cały ekran */
        display: block;
      }}
    </style>
  </head>
  <body>
    <img src='{StreamUrl}' />
  </body>
</html>";

        CameraWebView.Source = new HtmlWebViewSource
        {
            Html = html
        };

        
        await Task.Delay(1500);

        LoadingIndicator.IsRunning = false;
        Overlay.IsVisible = false;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        
        CameraWebView.Source = null;

        LoadingIndicator.IsRunning = false;
        Overlay.IsVisible = false;
    }
}
