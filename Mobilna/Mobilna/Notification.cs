namespace Mobilna;

public interface INotification
{
    // natychmiastowe powiadomienie
    Task ShowNotificationAsync(string title, string message);

    // cykliczne powiadomienie: od kiedy + co jaki interwał + ID
    Task ScheduleRepeatingNotificationAsync(
        string title,
        string message,
        DateTime firstTime,
        TimeSpan interval,
        int notificationId);

    // anulowanie zaplanowanego powiadomienia
    Task CancelScheduledNotificationAsync(int notificationId);
}
