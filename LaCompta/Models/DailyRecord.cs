namespace LaCompta.Models
{
    public class DailyRecord
    {
        public long Id { get; set; }
        public string Season { get; set; } = string.Empty;
        public int Year { get; set; }
        public int Day { get; set; }
        public long FarmingIncome { get; set; }
        public long ForagingIncome { get; set; }
        public long FishingIncome { get; set; }
        public long MiningIncome { get; set; }
        public long OtherIncome { get; set; }
        public long TotalExpenses { get; set; }
        public long TotalIncome => FarmingIncome + ForagingIncome + FishingIncome + MiningIncome + OtherIncome;
        public long NetProfit => TotalIncome - TotalExpenses;
        public string PlayerId { get; set; } = string.Empty;
    }
}
