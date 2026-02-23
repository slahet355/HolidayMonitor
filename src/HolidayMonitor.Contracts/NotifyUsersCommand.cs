namespace HolidayMonitor.Contracts;

/// <summary>
/// Sent by UserPref to Notifier to push holiday alerts to subscribed users via SignalR.
/// </summary>
public class NotifyUsersCommand : ICommand
{
    public List<string> UserIds { get; set; } = [];
    public string CountryCode { get; set; } = string.Empty;
    public string CountryName { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string LocalName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime DetectedAtUtc { get; set; }
}
