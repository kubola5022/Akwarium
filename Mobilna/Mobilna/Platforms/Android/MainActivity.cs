using Android.App;
using Android.Content.PM;
using Android.OS;

namespace Mobilna;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTask,        // ⬅️ to jest ważne dla pluginu
    ConfigurationChanges = ConfigChanges.ScreenSize
                          | ConfigChanges.Orientation
                          | ConfigChanges.UiMode
                          | ConfigChanges.ScreenLayout
                          | ConfigChanges.SmallestScreenSize
                          | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
}
