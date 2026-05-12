namespace Mobilna;

public static class ServiceHelper
{
    public static T GetService<T>() where T : notnull
        => (T)Application.Current!
            .Handler!
            .MauiContext!
            .Services
            .GetService(typeof(T))!;
}
