

namespace AkwariumShared.Dashboard;

    public class ThresholdDto
    {
        public int SensorId { get; set; }
        public double? Min { get; set; }
        public double? Max { get; set; }
    }

    public class ThresholdUpdateRequest
    {
        public int UserId { get; set; }
        public List<ThresholdDto> Thresholds { get; set; } = new();
   }
public class UpdateSensorThresholdDto
{
    public int SensorId { get; set; }
    public double? MinValue { get; set; }
    public double? MaxValue { get; set; }
}
public class SensorThresholdItem
{
    public int SensorId { get; set; }
    public string Name { get; set; } = "";
    public string MinText { get; set; } = "";
    public string MaxText { get; set; } = "";
    public bool IsActive { get; set; }
}

public class ThresholdToUpdate
{
    public int SensorId { get; set; }
    public double? Min { get; set; }
    public double? Max { get; set; }
}
public class CreateAquariumRequest
{
    public int UserId { get; set; }
    public string AquariumName { get; set; } = string.Empty;
}

public class CreateSensorRequest
{
    public int AquariumId { get; set; }
    public string SensorName { get; set; } = string.Empty;
    public string SensorType { get; set; } = string.Empty;
    public string? Description { get; set; }
}





public class AquariumDto
{
    public int AquariumId { get; set; }
    public string AquariumName { get; set; } = "";
}

public class SensorLatestDto
{
    public int SensorId { get; set; }
    public string SensorName { get; set; } = "";
    public string SensorType { get; set; } = "";
    public string Description { get; set; } = "";
    public double? MinValue { get; set; }
    public double? MaxValue { get; set; }
    public double? Value { get; set; }
    public DateTime? TimeAdded { get; set; }
}

public class SensorHistoryDto
{
    public double Value { get; set; }
    public DateTime TimeAdded { get; set; }
}

public class SensorUpdateRequest
{
    public string? SensorName { get; set; }
    public string? Description { get; set; }
    public double? MinValue { get; set; }
    public double? MaxValue { get; set; }
}


public record SensorLatestResponse(
    int SensorId,
    string SensorName,
    string SensorType,
    string Description,
    double? MinValue,
    double? MaxValue,
    double? Value,
    DateTime? TimeAdded);

public record SensorHistoryResponse(
    double Value,
    DateTime TimeAdded);

public class SensorHistoryPointDto
{
    public double Value { get; set; }
    public DateTime TimeAdded { get; set; }
}

public class SensorForSettingsDto
{
    public int SensorId { get; set; }
    public string SensorName { get; set; } = "";
    public string SensorType { get; set; } = "";
    public string? Description { get; set; }
    public double? MinValue { get; set; }
    public double? MaxValue { get; set; }
    public double? Value { get; set; }
    public double? LastValue { get; set; }
    public DateTime? TimeAdded { get; set; }
}

public record DeviceCommandRequest(
    int AquariumId,
    string DeviceId,
    bool On,
    string? Endpoint // np. "/relay1" dla custom, może być null dla światła/pompy/grzałki
);

public record DeviceScheduleRequest(
    int AquariumId,
    string DeviceId,
    List<int> Days,
    string Start,   // "HH:mm"
    string End,     // "HH:mm"
    string? Endpoint
);

public record HeaterControlRequest(
    int AquariumId,
    string DeviceId,   // spodziewamy się "heater"
    bool On);

public record HeaterScheduleRequest(
    int AquariumId,
    string DeviceId,   // "heater"
    List<int> Days,
    string Start,      // "HH:mm"
    string End);



public class SensorAlertItem
{
    public int SensorId { get; set; }
    public string Name { get; set; } = "";

    public string? MinText { get; set; }
    public string? MaxText { get; set; }

    public bool IsActive { get; set; }
}


public record SensorEditDto(int SensorId, int AquariumId, string SensorName, string SensorType, string? Description);

public record UpdateSensorRequest(string SensorName, string SensorType, string? Description);