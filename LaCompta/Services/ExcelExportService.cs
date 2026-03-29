using ClosedXML.Excel;
using LaCompta.Data;
using LaCompta.Models;
using StardewModdingAPI;
using StardewValley;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LaCompta.Services
{
    public class ExcelExportService
    {
        private readonly Repository _repo;
        private readonly IMonitor _monitor;

        private static readonly string[] CatLabels = { "Farming", "Foraging", "Fishing", "Mining", "Other" };

        // Category colors (ClosedXML uses XLColor)
        private static readonly XLColor[] CatColors = {
            XLColor.FromArgb(0x4C, 0xAF, 0x50),
            XLColor.FromArgb(0x8B, 0xC3, 0x4A),
            XLColor.FromArgb(0x21, 0x96, 0xF3),
            XLColor.FromArgb(0xFF, 0x98, 0x00),
            XLColor.FromArgb(0x9E, 0x9E, 0x9E),
        };

        public ExcelExportService(Repository repo, IMonitor monitor)
        {
            _repo = repo;
            _monitor = monitor;
        }

        public byte[] GenerateXlsx(string playerId)
        {
            var summaries = _repo.GetAllSeasonSummaries(playerId);
            if (summaries.Count == 0)
                return Array.Empty<byte>();

            var farmName = Context.IsWorldReady ? Game1.player.farmName.Value : "Farm";
            using var wb = new XLWorkbook();

            // Overview sheet
            BuildOverviewSheet(wb, summaries, farmName);

            // Per-season sheets
            foreach (var summary in summaries)
            {
                var records = _repo.GetDailyRecords(summary.Season, summary.Year, playerId);
                BuildSeasonSheet(wb, summary, records);
            }

            // Sales sheet
            BuildSalesSheet(wb, summaries, playerId);

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        public void GenerateXlsxToFile(string playerId, string outputPath)
        {
            var bytes = GenerateXlsx(playerId);
            if (bytes.Length > 0)
                File.WriteAllBytes(outputPath, bytes);
        }

        private void BuildOverviewSheet(IXLWorkbook wb, List<SeasonSummary> summaries, string farmName)
        {
            var ws = wb.Worksheets.Add("Overview");

            // Title
            ws.Cell(1, 1).Value = $"La Compta - {farmName}";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 16;
            ws.Range(1, 1, 1, 5).Merge();

            // Summary stats
            var totalIncome = summaries.Sum(s => SumIncome(s));
            var totalExpenses = summaries.Sum(s => s.TotalExpenses);
            var net = totalIncome - totalExpenses;

            ws.Cell(3, 1).Value = "Total Income";
            ws.Cell(3, 2).Value = totalIncome;
            ws.Cell(3, 2).Style.NumberFormat.Format = "#,##0";
            ws.Cell(3, 2).Style.Font.FontColor = XLColor.FromArgb(0x4C, 0xAF, 0x50);

            ws.Cell(4, 1).Value = "Total Expenses";
            ws.Cell(4, 2).Value = totalExpenses;
            ws.Cell(4, 2).Style.NumberFormat.Format = "#,##0";

            ws.Cell(5, 1).Value = "Net Profit";
            ws.Cell(5, 2).Value = net;
            ws.Cell(5, 2).Style.NumberFormat.Format = "#,##0";
            ws.Cell(5, 2).Style.Font.FontColor = net >= 0 ? XLColor.FromArgb(0x2E, 0xCC, 0x71) : XLColor.FromArgb(0xE7, 0x4C, 0x3C);

            ws.Cell(6, 1).Value = "Seasons Tracked";
            ws.Cell(6, 2).Value = summaries.Count;

            ws.Range(3, 1, 6, 1).Style.Font.Bold = true;

            // Season summary table
            int row = 8;
            var headers = new[] { "Season", "Farming", "Foraging", "Fishing", "Mining", "Other", "Expenses", "Total Income", "Net Profit", "Best Day" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(row, i + 1).Value = headers[i];
                ws.Cell(row, i + 1).Style.Font.Bold = true;
                ws.Cell(row, i + 1).Style.Fill.BackgroundColor = XLColor.FromArgb(0x1A, 0x1A, 0x2E);
                ws.Cell(row, i + 1).Style.Font.FontColor = XLColor.FromArgb(0xE6, 0xD9, 0xA8);
                // Category header colors
                if (i >= 1 && i <= 5)
                {
                    ws.Cell(row, i + 1).Style.Fill.BackgroundColor = CatColors[i - 1];
                    ws.Cell(row, i + 1).Style.Font.FontColor = XLColor.White;
                }
            }
            row++;

            foreach (var s in summaries)
            {
                var income = SumIncome(s);
                var sNet = income - s.TotalExpenses;
                ws.Cell(row, 1).Value = $"{Cap(s.Season)} Y{s.Year}";
                ws.Cell(row, 2).Value = s.FarmingTotal;
                ws.Cell(row, 3).Value = s.ForagingTotal;
                ws.Cell(row, 4).Value = s.FishingTotal;
                ws.Cell(row, 5).Value = s.MiningTotal;
                ws.Cell(row, 6).Value = s.OtherTotal;
                ws.Cell(row, 7).Value = s.TotalExpenses;
                ws.Cell(row, 8).Value = income;
                ws.Cell(row, 9).Value = sNet;
                ws.Cell(row, 10).Value = $"Day {s.BestDay} ({s.BestDayIncome}g)";

                // Format numbers
                for (int c = 2; c <= 9; c++)
                    ws.Cell(row, c).Style.NumberFormat.Format = "#,##0";

                // Net color
                ws.Cell(row, 9).Style.Font.FontColor = sNet >= 0
                    ? XLColor.FromArgb(0x2E, 0xCC, 0x71)
                    : XLColor.FromArgb(0xE7, 0x4C, 0x3C);

                // Alternating rows
                if ((row - 9) % 2 == 0)
                    ws.Range(row, 1, row, 10).Style.Fill.BackgroundColor = XLColor.FromArgb(0xF5, 0xF5, 0xF0);

                row++;
            }

            // Comedy footer
            row += 2;
            ws.Cell(row, 1).Value = "\"This spreadsheet is brought to you by URSSAF.\"";
            ws.Cell(row, 1).Style.Font.Italic = true;
            ws.Cell(row, 1).Style.Font.FontColor = XLColor.Gray;

            ws.Columns().AdjustToContents();
        }

        private void BuildSeasonSheet(IXLWorkbook wb, SeasonSummary summary, List<DailyRecord> records)
        {
            var tabName = $"{Cap(summary.Season)} Y{summary.Year}";
            var ws = wb.Worksheets.Add(tabName);

            // Headers
            var headers = new[] { "Day", "Farming", "Foraging", "Fishing", "Mining", "Other", "Expenses", "Total", "Net" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(1, i + 1).Value = headers[i];
                ws.Cell(1, i + 1).Style.Font.Bold = true;
                ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromArgb(0x1A, 0x1A, 0x2E);
                ws.Cell(1, i + 1).Style.Font.FontColor = XLColor.FromArgb(0xE6, 0xD9, 0xA8);
                if (i >= 1 && i <= 5)
                {
                    ws.Cell(1, i + 1).Style.Fill.BackgroundColor = CatColors[i - 1];
                    ws.Cell(1, i + 1).Style.Font.FontColor = XLColor.White;
                }
            }

            // Daily rows
            int row = 2;
            foreach (var r in records)
            {
                var total = r.FarmingIncome + r.ForagingIncome + r.FishingIncome + r.MiningIncome + r.OtherIncome;
                var net = total - r.TotalExpenses;

                ws.Cell(row, 1).Value = r.Day;
                ws.Cell(row, 2).Value = r.FarmingIncome;
                ws.Cell(row, 3).Value = r.ForagingIncome;
                ws.Cell(row, 4).Value = r.FishingIncome;
                ws.Cell(row, 5).Value = r.MiningIncome;
                ws.Cell(row, 6).Value = r.OtherIncome;
                ws.Cell(row, 7).Value = r.TotalExpenses;
                ws.Cell(row, 8).Value = total;
                ws.Cell(row, 9).Value = net;

                for (int c = 2; c <= 9; c++)
                    ws.Cell(row, c).Style.NumberFormat.Format = "#,##0";

                ws.Cell(row, 9).Style.Font.FontColor = net >= 0
                    ? XLColor.FromArgb(0x2E, 0xCC, 0x71)
                    : XLColor.FromArgb(0xE7, 0x4C, 0x3C);

                if (row % 2 == 0)
                    ws.Range(row, 1, row, 9).Style.Fill.BackgroundColor = XLColor.FromArgb(0xF5, 0xF5, 0xF0);

                row++;
            }

            // Totals row with SUM formulas
            int lastDataRow = row - 1;
            ws.Cell(row, 1).Value = "TOTAL";
            ws.Cell(row, 1).Style.Font.Bold = true;
            for (int c = 2; c <= 9; c++)
            {
                var colLetter = (char)('A' + c - 1);
                ws.Cell(row, c).FormulaA1 = $"SUM({colLetter}2:{colLetter}{lastDataRow})";
                ws.Cell(row, c).Style.Font.Bold = true;
                ws.Cell(row, c).Style.NumberFormat.Format = "#,##0";
            }
            ws.Range(row, 1, row, 9).Style.Fill.BackgroundColor = XLColor.FromArgb(0xE8, 0xE0, 0xC8);

            ws.Columns().AdjustToContents();
        }

        private void BuildSalesSheet(IXLWorkbook wb, List<SeasonSummary> summaries, string playerId)
        {
            var ws = wb.Worksheets.Add("Sales Ledger");

            var headers = new[] { "Item", "Category", "Season", "Year", "Day", "Qty", "Unit Price", "Revenue", "Cost", "Profit" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(1, i + 1).Value = headers[i];
                ws.Cell(1, i + 1).Style.Font.Bold = true;
                ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromArgb(0x1A, 0x1A, 0x2E);
                ws.Cell(1, i + 1).Style.Font.FontColor = XLColor.FromArgb(0xE6, 0xD9, 0xA8);
            }

            int row = 2;
            foreach (var s in summaries)
            {
                var transactions = _repo.GetAllTransactions(s.Season, s.Year, 0, "", playerId);
                foreach (var tx in transactions)
                {
                    var profit = tx.TotalPrice - tx.CostBasis;
                    ws.Cell(row, 1).Value = tx.ItemName;
                    ws.Cell(row, 2).Value = tx.Category;
                    ws.Cell(row, 3).Value = Cap(tx.Season);
                    ws.Cell(row, 4).Value = tx.Year;
                    ws.Cell(row, 5).Value = tx.Day;
                    ws.Cell(row, 6).Value = tx.Quantity;
                    ws.Cell(row, 7).Value = tx.UnitPrice;
                    ws.Cell(row, 8).Value = tx.TotalPrice;
                    ws.Cell(row, 9).Value = tx.CostBasis;
                    ws.Cell(row, 10).Value = profit;

                    for (int c = 6; c <= 10; c++)
                        ws.Cell(row, c).Style.NumberFormat.Format = "#,##0";

                    ws.Cell(row, 10).Style.Font.FontColor = profit >= 0
                        ? XLColor.FromArgb(0x2E, 0xCC, 0x71)
                        : XLColor.FromArgb(0xE7, 0x4C, 0x3C);

                    // Category color badge
                    int catIdx = Array.IndexOf(CatLabels, tx.Category);
                    if (catIdx >= 0)
                        ws.Cell(row, 2).Style.Font.FontColor = CatColors[catIdx];

                    if (row % 2 == 0)
                        ws.Range(row, 1, row, 10).Style.Fill.BackgroundColor = XLColor.FromArgb(0xF5, 0xF5, 0xF0);

                    row++;
                }
            }

            // Auto-filter
            if (row > 2)
                ws.Range(1, 1, row - 1, 10).SetAutoFilter();

            ws.Columns().AdjustToContents();
        }

        private static long SumIncome(SeasonSummary s) =>
            s.FarmingTotal + s.ForagingTotal + s.FishingTotal + s.MiningTotal + s.OtherTotal;

        private static string Cap(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);
    }
}
