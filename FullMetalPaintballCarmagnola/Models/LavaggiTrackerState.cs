using System;

namespace Full_Metal_Paintball_Carmagnola.Models
{
    public class LavaggiTrackerState
    {
        public int CaschiCount { get; set; }
        public decimal CaschiUnitPrice { get; set; }
        public int PettorineCount { get; set; }
        public decimal PettorineUnitPrice { get; set; }
        public DateTime CaschiPeriodStartUtc { get; set; } = DateTime.UtcNow;
        public DateTime PettorinePeriodStartUtc { get; set; } = DateTime.UtcNow;
        public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
    }
}
