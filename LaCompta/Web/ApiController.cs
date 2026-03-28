using LaCompta.Data;
using StardewModdingAPI;
using StardewValley;
using System;
using System.Collections.Generic;
using System.IO;
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
        private readonly string _assetsPath;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        public ApiController(Repository repo, IMonitor monitor, string modPath)
        {
            _repo = repo;
            _monitor = monitor;
            _assetsPath = Path.Combine(modPath, "Assets");
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
                    case "/api/farminfo":
                        ServeFarmInfo(response);
                        break;
                    case "/api/fish":
                        ServeAllFish(queryParams, response);
                        break;
                    default:
                        // Try serving as static asset from Assets folder
                        if (!ServeStaticAsset(path, response))
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

        private void ServeFarmInfo(HttpListenerResponse response)
        {
            var info = new
            {
                FarmName = Context.IsWorldReady ? Game1.player.farmName.Value : "Unknown Farm",
                PlayerName = Context.IsWorldReady ? Game1.player.Name : "Unknown",
                Season = Context.IsWorldReady ? Game1.currentSeason : "",
                Year = Context.IsWorldReady ? Game1.year : 0,
                Day = Context.IsWorldReady ? Game1.dayOfMonth : 0
            };
            ServeJson(response, info);
        }

        private void ServeAllFish(Dictionary<string, string> query, HttpListenerResponse response)
        {
            var playerId = query.GetValueOrDefault("playerId", "");
            var fish = _repo.GetAllFish(playerId);
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
            var dashboardPath = Path.Combine(_assetsPath, "dashboard.html");
            if (File.Exists(dashboardPath))
            {
                ServeHtml(response, File.ReadAllText(dashboardPath));
            }
            else
            {
                ServeHtml(response, "<html><body><h1>LaCompta</h1><p>Dashboard file not found. Check Assets/dashboard.html</p></body></html>");
                _monitor.Log("Dashboard HTML not found at: " + dashboardPath, LogLevel.Warn);
            }
        }

        private bool ServeStaticAsset(string path, HttpListenerResponse response)
        {
            // Sanitize path to prevent directory traversal
            var relativePath = path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            if (relativePath.Contains("..")) return false;

            var filePath = Path.Combine(_assetsPath, relativePath);
            if (!File.Exists(filePath)) return false;

            var ext = Path.GetExtension(filePath).ToLower();
            var contentType = ext switch
            {
                ".html" => "text/html; charset=utf-8",
                ".css"  => "text/css; charset=utf-8",
                ".js"   => "application/javascript; charset=utf-8",
                ".png"  => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif"  => "image/gif",
                ".svg"  => "image/svg+xml",
                ".ico"  => "image/x-icon",
                ".json" => "application/json",
                ".woff" => "font/woff",
                ".woff2" => "font/woff2",
                _ => "application/octet-stream"
            };

            var bytes = File.ReadAllBytes(filePath);
            response.StatusCode = 200;
            response.ContentType = contentType;
            response.ContentLength64 = bytes.Length;
            response.OutputStream.Write(bytes, 0, bytes.Length);
            response.Close();
            return true;
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
