namespace Mobilna.Models.Control;

public class DeviceCommandRequest
{
    public int AquariumId { get; set; }
    public string DeviceId { get; set; } = "";
    public bool On { get; set; }
    public string? Endpoint { get; set; }
}

public class DeviceScheduleRequest
{
    public int AquariumId { get; set; }
    public string DeviceId { get; set; } = "";
    public List<int> Days { get; set; } = new();
    public string Start { get; set; } = "";
    public string End { get; set; } = "";
    public string? Endpoint { get; set; }
}
