namespace LaCompta.Models
{
    public class ItemTransaction
    {
        public long Id { get; set; }
        public long DailyRecordId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string ItemId { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public int UnitPrice { get; set; }
        public long TotalPrice { get; set; }
        public long CostBasis { get; set; }
        public long Profit => TotalPrice - CostBasis;
        public string Season { get; set; } = string.Empty;
        public int Year { get; set; }
        public int Day { get; set; }
        public string PlayerId { get; set; } = string.Empty;
    }
}
