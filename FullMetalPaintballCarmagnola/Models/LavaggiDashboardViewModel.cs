using System.Collections.Generic;

namespace Full_Metal_Paintball_Carmagnola.Models
{
    public class LavaggiDashboardViewModel
    {
        public LavaggiTrackerState State { get; set; } = new();
        public List<LavaggioHistoryEntry> History { get; set; } = new();
    }
}
