namespace LaCompta.Models
{
    public class FishRecord
    {
        public long Id { get; set; }
        public string FishName { get; set; } = string.Empty;
        public string FishId { get; set; } = string.Empty;
        public bool IsLegendary { get; set; }
        public int Quantity { get; set; }
        public long TotalRevenue { get; set; }
        public string Season { get; set; } = string.Empty;
        public int Year { get; set; }
        public int Day { get; set; }
        public string PlayerId { get; set; } = string.Empty;
    }
}
