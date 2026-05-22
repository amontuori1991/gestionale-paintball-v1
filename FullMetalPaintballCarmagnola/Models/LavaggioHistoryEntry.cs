using System;

namespace Full_Metal_Paintball_Carmagnola.Models
{
    public class LavaggioHistoryEntry
    {
        public Guid EntryId { get; set; } = Guid.NewGuid();
        public string CategoryKey { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string PersonName { get; set; } = string.Empty;
        public int PieceCount { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime PeriodStartUtc { get; set; }
        public DateTime PeriodEndUtc { get; set; }
        public DateTime ResetAtUtc { get; set; }
        public bool IsPaid { get; set; }
    }
}
