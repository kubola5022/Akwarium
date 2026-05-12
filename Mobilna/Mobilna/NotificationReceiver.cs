#if ANDROID
using Android.App;
using Android.Content;
using AndroidX.Core.App;

namespace Mobilna;

[BroadcastReceiver(Enabled = true, Exported = false)]
public class NotificationReceiver : BroadcastReceiver
{
    public override void OnReceive(Context context, Intent intent)
    {
        var title = intent.GetStringExtra("title") ?? "Akwarium";
        var message = intent.GetStringExtra("message") ?? "";

        LocalNotificationService.EnsureChannel();

        var mainIntent = new Intent(context, typeof(MainActivity));
        mainIntent.AddFlags(ActivityFlags.ClearTop | ActivityFlags.NewTask);

        var pendingIntent = PendingIntent.GetActivity(
            context,
            0,
            mainIntent,
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        var builder = new NotificationCompat.Builder(context, LocalNotificationService.ChannelId)
            .SetContentTitle(title)
            .SetContentText(message)
            .SetSmallIcon(Android.Resource.Drawable.IcDialogInfo)
            .SetAutoCancel(true)
            .SetContentIntent(pendingIntent);

        var notification = builder.Build();
        NotificationManagerCompat.From(context).Notify(new Random().Next(), notification);
    }
}
#endif
