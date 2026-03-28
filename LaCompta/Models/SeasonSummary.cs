namespace LaCompta.Models
{
    public class SeasonSummary
    {
        public long Id { get; set; }
        public string Season { get; set; } = string.Empty;
        public int Year { get; set; }
        public long FarmingTotal { get; set; }
        public long ForagingTotal { get; set; }
        public long FishingTotal { get; set; }
        public long MiningTotal { get; set; }
        public long OtherTotal { get; set; }
        public long TotalIncome => FarmingTotal + ForagingTotal + FishingTotal + MiningTotal + OtherTotal;
        public long TotalExpenses { get; set; }
        public long NetProfit => TotalIncome - TotalExpenses;
        public int BestDay { get; set; }
        public long BestDayIncome { get; set; }
        public string PlayerId { get; set; } = string.Empty;
    }
}
