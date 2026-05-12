#if ANDROID
using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using Application = Android.App.Application;
#endif

namespace Mobilna;

public class LocalNotificationService : INotification
{
#if ANDROID
    internal const string ChannelId = "aquarium_notifications";
    private static bool _channelInitialized;
#endif

    private int _messageId = 0;

    // *** NATYCHMIASTOWE POWIADOMIENIE ***
    public Task ShowNotificationAsync(string title, string message)
    {
#if ANDROID
        EnsureChannel();

        var context = Application.Context;

        var intent = new Intent(context, typeof(MainActivity));
        intent.AddFlags(ActivityFlags.ClearTop | ActivityFlags.NewTask);

        var pendingIntent = PendingIntent.GetActivity(
            context,
            0,
            intent,
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        var builder = new NotificationCompat.Builder(context, ChannelId)
            .SetContentTitle(title)
            .SetContentText(message)
            .SetSmallIcon(Android.Resource.Drawable.IcDialogInfo)
            .SetAutoCancel(true)
            .SetContentIntent(pendingIntent);

        var notification = builder.Build();

        NotificationManagerCompat.From(context).Notify(_messageId++, notification);
#else
        System.Diagnostics.Debug.WriteLine($"[NOTIF] {title}: {message}");
#endif
        return Task.CompletedTask;
    }

    // *** POWTARZAJĄCE SIĘ POWIADOMIENIE ***
    public Task ScheduleRepeatingNotificationAsync(
        string title,
        string message,
        DateTime firstTime,
        TimeSpan interval,
        int notificationId)
    {
#if ANDROID
        EnsureChannel();

        var context = Application.Context;

        var intent = new Intent(context, typeof(NotificationReceiver));
        intent.PutExtra("title", title);
        intent.PutExtra("message", message);

        var pendingIntent = PendingIntent.GetBroadcast(
            context,
            notificationId, // ważne – ID jako requestCode
            intent,
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        long triggerTimeMillis = GetNotifyTimeInMillis(firstTime);
        long intervalMillis = (long)interval.TotalMilliseconds;

        var alarmManager =
            (AlarmManager?)context.GetSystemService(Context.AlarmService);

        // cykliczne powiadomienie
        alarmManager?.SetRepeating(
            AlarmType.RtcWakeup,
            triggerTimeMillis,
            intervalMillis,
            pendingIntent);
#else
        System.Diagnostics.Debug.WriteLine(
            $"[SCHEDULE] {firstTime} co {interval}: {title} - {message}");
#endif
        return Task.CompletedTask;
    }

    public Task CancelScheduledNotificationAsync(int notificationId)
    {
#if ANDROID
        var context = Application.Context;

        var intent = new Intent(context, typeof(NotificationReceiver));
        var pendingIntent = PendingIntent.GetBroadcast(
            context,
            notificationId,
            intent,
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        var alarmManager =
            (AlarmManager?)context.GetSystemService(Context.AlarmService);

        alarmManager?.Cancel(pendingIntent);
#endif
        return Task.CompletedTask;
    }

#if ANDROID
    internal static void EnsureChannel()
    {
        if (_channelInitialized)
            return;

        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var channelName = "Powiadomienia akwarium";
            var channelDesc = "Powiadomienia z aplikacji mobilnej akwarium";

            var channel = new NotificationChannel(
                ChannelId,
                channelName,
                NotificationImportance.Default)
            {
                Description = channelDesc
            };

            var manager = (NotificationManager?)Application
                .Context
                .GetSystemService(Context.NotificationService);

            manager?.CreateNotificationChannel(channel);
        }

        _channelInitialized = true;
    }

    private static long GetNotifyTimeInMillis(DateTime when)
    {
        var utc = when.ToUniversalTime();
        var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return (long)(utc - epoch).TotalMilliseconds;
    }
#endif
}
