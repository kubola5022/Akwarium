using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mobilna.Models.Aquarium
{
    public class SensorLatestDto
    {
        public int SensorId { get; set; }
        public string SensorName { get; set; } = string.Empty;
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public double? Value { get; set; }
        public DateTime? TimeAdded { get; set; }

        public string FormattedValue
        {
            get
            {
                if (!Value.HasValue)
                    return "Brak odczytu";

                var unit = Type.ToLower() switch
                {
                    "temperature" or "temp" => "°C",
                    "ph" => " pH",
                    "level" => " %",
                    _ => ""
                };

                return $"{Value:0.0}{unit}";
            }
        }

        public string FormattedTime =>
            TimeAdded.HasValue ? $"Ostatni pomiar: {TimeAdded:G}" : "";
    }
}
