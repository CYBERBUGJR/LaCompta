using LaCompta.Data;
using StardewModdingAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Web;

namespace LaCompta.Web
{
    public class ApiController
    {
        private readonly Repository _repo;
        private readonly IMonitor _monitor;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        public ApiController(Repository repo, IMonitor monitor)
        {
            _repo = repo;
            _monitor = monitor;
        }

        public void HandleRequest(string path, string query, HttpListenerResponse response)
        {
            try
            {
                var queryParams = ParseQuery(query);

                switch (path)
                {
                    case "/":
                        ServeHomePage(response);
                        break;
                    case "/api/daily":
                        ServeDailyRecords(queryParams, response);
                        break;
                    case "/api/seasons":
                        ServeSeasonSummaries(queryParams, response);
                        break;
                    case "/api/profitability":
                        ServeProfitability(queryParams, response);
                        break;
                    case "/api/fish/legendary":
                        ServeLegendaryFish(queryParams, response);
                        break;
                    case "/api/summary":
                        ServeOverallSummary(queryParams, response);
                        break;
                    default:
                        ServeNotFound(response);
                        break;
                }
            }
            catch (Exception ex)
            {
                _monitor.Log($"API error on {path}: {ex.Message}", LogLevel.Debug);
                ServeError(response, ex.Message);
            }
        }

        private void ServeDailyRecords(Dictionary<string, string> query, HttpListenerResponse response)
        {
            var season = query.GetValueOrDefault("season", "spring");
            var year = int.TryParse(query.GetValueOrDefault("year", "1"), out var y) ? y : 1;
            var playerId = query.GetValueOrDefault("playerId", "");

            var records = _repo.GetDailyRecords(season, year, playerId);
            ServeJson(response, records);
        }

        private void ServeSeasonSummaries(Dictionary<string, string> query, HttpListenerResponse response)
        {
            var playerId = query.GetValueOrDefault("playerId", "");
            var summaries = _repo.GetAllSeasonSummaries(playerId);
            ServeJson(response, summaries);
        }

        private void ServeProfitability(Dictionary<string, string> query, HttpListenerResponse response)
        {
            var season = query.GetValueOrDefault("season", "spring");
            var year = int.TryParse(query.GetValueOrDefault("year", "1"), out var y) ? y : 1;
            var limit = int.TryParse(query.GetValueOrDefault("limit", "10"), out var l) ? l : 10;
            var playerId = query.GetValueOrDefault("playerId", "");

            var items = _repo.GetTopProfitableItems(season, year, limit, playerId);
            ServeJson(response, items);
        }

        private void ServeLegendaryFish(Dictionary<string, string> query, HttpListenerResponse response)
        {
            var playerId = query.GetValueOrDefault("playerId", "");
            var fish = _repo.GetLegendaryFish(playerId);
            ServeJson(response, fish);
        }

        private void ServeOverallSummary(Dictionary<string, string> query, HttpListenerResponse response)
        {
            var playerId = query.GetValueOrDefault("playerId", "");
            var summaries = _repo.GetAllSeasonSummaries(playerId);
            var legendaryFish = _repo.GetLegendaryFish(playerId);

            var overall = new
            {
                TotalSeasons = summaries.Count,
                TotalIncome = summaries.Sum(s => s.FarmingTotal + s.ForagingTotal + s.FishingTotal + s.MiningTotal + s.OtherTotal),
                TotalExpenses = summaries.Sum(s => s.TotalExpenses),
                LegendaryFishCount = legendaryFish.Count,
                BestSeason = summaries.OrderByDescending(s => s.FarmingTotal + s.ForagingTotal + s.FishingTotal + s.MiningTotal + s.OtherTotal).FirstOrDefault(),
                Categories = new
                {
                    Farming = summaries.Sum(s => s.FarmingTotal),
                    Foraging = summaries.Sum(s => s.ForagingTotal),
                    Fishing = summaries.Sum(s => s.FishingTotal),
                    Mining = summaries.Sum(s => s.MiningTotal),
                    Other = summaries.Sum(s => s.OtherTotal)
                }
            };

            ServeJson(response, overall);
        }

        private void ServeHomePage(HttpListenerResponse response)
        {
            var html = @"<!DOCTYPE html>
<html>
<head>
    <title>LaCompta - Farm Economics Dashboard</title>
    <style>
        body { background: #1a1a2e; color: #e6d9a8; font-family: monospace; text-align: center; padding: 50px; }
        h1 { font-size: 2em; }
        .subtitle { color: #8b7355; font-style: italic; }
        .api-links { margin-top: 30px; text-align: left; max-width: 500px; margin-left: auto; margin-right: auto; }
        a { color: #6daedb; }
    </style>
</head>
<body>
    <h1>LaCompta</h1>
    <p class='subtitle'>""Salut salut, c'est Valerie de la compta...""</p>
    <p>Farm Economics Dashboard - Coming Soon!</p>
    <div class='api-links'>
        <h3>API Endpoints:</h3>
        <ul>
            <li><a href='/api/seasons'>GET /api/seasons</a> - Season summaries</li>
            <li><a href='/api/daily?season=spring&year=1'>GET /api/daily</a> - Daily records</li>
            <li><a href='/api/profitability?season=spring&year=1'>GET /api/profitability</a> - Top profitable items</li>
            <li><a href='/api/fish/legendary'>GET /api/fish/legendary</a> - Legendary fish</li>
            <li><a href='/api/summary'>GET /api/summary</a> - Overall summary</li>
        </ul>
    </div>
</body>
</html>";
            ServeHtml(response, html);
        }

        private void ServeNotFound(HttpListenerResponse response)
        {
            response.StatusCode = 404;
            var body = JsonSerializer.Serialize(new { error = "Not found" }, JsonOptions);
            var bytes = Encoding.UTF8.GetBytes(body);
            response.ContentType = "application/json";
            response.ContentLength64 = bytes.Length;
            response.OutputStream.Write(bytes, 0, bytes.Length);
            response.Close();
        }

        private void ServeError(HttpListenerResponse response, string message)
        {
            response.StatusCode = 500;
            var body = JsonSerializer.Serialize(new { error = message }, JsonOptions);
            var bytes = Encoding.UTF8.GetBytes(body);
            response.ContentType = "application/json";
            response.ContentLength64 = bytes.Length;
            response.OutputStream.Write(bytes, 0, bytes.Length);
            response.Close();
        }

        private static void ServeJson<T>(HttpListenerResponse response, T data)
        {
            response.StatusCode = 200;
            var body = JsonSerializer.Serialize(data, JsonOptions);
            var bytes = Encoding.UTF8.GetBytes(body);
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = bytes.Length;
            response.OutputStream.Write(bytes, 0, bytes.Length);
            response.Close();
        }

        private static void ServeHtml(HttpListenerResponse response, string html)
        {
            response.StatusCode = 200;
            var bytes = Encoding.UTF8.GetBytes(html);
            response.ContentType = "text/html; charset=utf-8";
            response.ContentLength64 = bytes.Length;
            response.OutputStream.Write(bytes, 0, bytes.Length);
            response.Close();
        }

        private static Dictionary<string, string> ParseQuery(string query)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(query) || query == "?") return result;

            query = query.TrimStart('?');
            foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = pair.Split('=', 2);
                if (parts.Length == 2)
                {
                    result[HttpUtility.UrlDecode(parts[0])] = HttpUtility.UrlDecode(parts[1]);
                }
            }
            return result;
        }
    }
}
