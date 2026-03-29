using LaCompta.Data;
using LaCompta.Services;
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
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace LaCompta.Web
{
    public class ApiController
    {
        private readonly Repository _repo;
        private readonly IMonitor _monitor;
        private readonly string _assetsPath;
        private ExcelExportService _excelService;
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
            _excelService = new ExcelExportService(repo, monitor);
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
                    case "/api/players":
                        ServePlayers(response);
                        break;
                    case "/api/transactions":
                        ServeTransactions(queryParams, response);
                        break;
                    case "/api/report/xlsx":
                        ServeReportXlsx(queryParams, response);
                        break;
                    default:
                        // Sprite endpoint: /api/sprite/{itemId}
                        if (path.StartsWith("/api/sprite/"))
                        {
                            var itemId = path.Substring("/api/sprite/".Length);
                            ServeItemSprite(itemId, response);
                        }
                        // Try serving as static asset from Assets folder
                        else if (!ServeStaticAsset(path, response))
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
                PlayerId = Context.IsWorldReady ? Game1.player.UniqueMultiplayerID.ToString() : "",
                Season = Context.IsWorldReady ? Game1.currentSeason : "",
                Year = Context.IsWorldReady ? Game1.year : 0,
                Day = Context.IsWorldReady ? Game1.dayOfMonth : 0,
                IsMultiplayer = Context.IsMultiplayer
            };
            ServeJson(response, info);
        }

        private void ServePlayers(HttpListenerResponse response)
        {
            var players = new List<object>();
            if (Context.IsWorldReady)
            {
                // Only list players who are currently online (not all historical farmers)
                foreach (var farmer in Game1.getOnlineFarmers())
                {
                    players.Add(new
                    {
                        PlayerId = farmer.UniqueMultiplayerID.ToString(),
                        Name = farmer.Name,
                        IsCurrentPlayer = farmer.UniqueMultiplayerID == Game1.player.UniqueMultiplayerID
                    });
                }
            }
            ServeJson(response, players);
        }

        private void ServeAllFish(Dictionary<string, string> query, HttpListenerResponse response)
        {
            var playerId = query.GetValueOrDefault("playerId", "");
            var fish = _repo.GetAllFish(playerId);
            ServeJson(response, fish);
        }

        private void ServeReportXlsx(Dictionary<string, string> query, HttpListenerResponse response)
        {
            var playerId = query.GetValueOrDefault("playerId", "");
            if (string.IsNullOrEmpty(playerId) && Context.IsWorldReady)
                playerId = Game1.player.UniqueMultiplayerID.ToString();

            try
            {
                var xlsxBytes = _excelService.GenerateXlsx(playerId);
                if (xlsxBytes.Length == 0 && !string.IsNullOrEmpty(playerId))
                    xlsxBytes = _excelService.GenerateXlsx("");
                if (xlsxBytes.Length == 0)
                {
                    ServeJson(response, new { error = "No data to export. Play some days and ship items first!" });
                    return;
                }

                var farmName = Context.IsWorldReady ? Game1.player.farmName.Value : "Farm";
                response.StatusCode = 200;
                response.ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                response.AddHeader("Content-Disposition", $"attachment; filename=\"LaCompta-{farmName}-report.xlsx\"");
                response.ContentLength64 = xlsxBytes.Length;
                response.OutputStream.Write(xlsxBytes, 0, xlsxBytes.Length);
                response.Close();
            }
            catch (Exception ex)
            {
                _monitor.Log($"XLSX export error: {ex.Message}", LogLevel.Error);
                ServeError(response, "Excel export failed: " + ex.Message);
            }
        }

        private void ServeTransactions(Dictionary<string, string> query, HttpListenerResponse response)
        {
            var season = query.GetValueOrDefault("season", "");
            var year = int.TryParse(query.GetValueOrDefault("year", "0"), out var y) ? y : 0;
            var day = int.TryParse(query.GetValueOrDefault("day", "0"), out var d) ? d : 0;
            var category = query.GetValueOrDefault("category", "");
            var playerId = query.GetValueOrDefault("playerId", "");

            var transactions = _repo.GetAllTransactions(season, year, day, category, playerId);
            ServeJson(response, transactions);
        }

        // Sprite cache to avoid re-extracting on every request
        private static readonly Dictionary<string, byte[]> _spriteCache = new();

        private void ServeItemSprite(string itemId, HttpListenerResponse response)
        {
            try
            {
                // Return cached if available
                if (_spriteCache.TryGetValue(itemId, out var cached))
                {
                    response.StatusCode = 200;
                    response.ContentType = "image/png";
                    response.ContentLength64 = cached.Length;
                    response.OutputStream.Write(cached, 0, cached.Length);
                    response.Close();
                    return;
                }

                if (!Context.IsWorldReady || Game1.objectSpriteSheet == null)
                {
                    ServeNotFound(response);
                    return;
                }

                // Parse item ID (strip "(O)" prefix if present)
                var numericId = itemId.Replace("(O)", "");
                if (!int.TryParse(numericId, out var id))
                {
                    ServeNotFound(response);
                    return;
                }

                // Object spritesheet: 24 items per row, 16x16 each
                var spriteSheet = Game1.objectSpriteSheet;
                int spriteSize = 16;
                int columns = spriteSheet.Width / spriteSize;
                int x = (id % columns) * spriteSize;
                int y = (id / columns) * spriteSize;

                // Extract pixel data from the spritesheet
                var pixelData = new Color[spriteSize * spriteSize];
                spriteSheet.GetData(0, new Rectangle(x, y, spriteSize, spriteSize), pixelData, 0, pixelData.Length);

                // Scale up 2x for better visibility (32x32)
                int scale = 2;
                int outSize = spriteSize * scale;
                var scaled = new Color[outSize * outSize];
                for (int py = 0; py < spriteSize; py++)
                {
                    for (int px = 0; px < spriteSize; px++)
                    {
                        var c = pixelData[py * spriteSize + px];
                        for (int sy = 0; sy < scale; sy++)
                            for (int sx = 0; sx < scale; sx++)
                                scaled[(py * scale + sy) * outSize + (px * scale + sx)] = c;
                    }
                }

                // Create a new texture and save as PNG
                using var texture = new Texture2D(Game1.graphics.GraphicsDevice, outSize, outSize);
                texture.SetData(scaled);
                using var ms = new System.IO.MemoryStream();
                texture.SaveAsPng(ms, outSize, outSize);
                var pngBytes = ms.ToArray();

                // Cache it
                _spriteCache[itemId] = pngBytes;

                response.StatusCode = 200;
                response.ContentType = "image/png";
                response.ContentLength64 = pngBytes.Length;
                response.OutputStream.Write(pngBytes, 0, pngBytes.Length);
                response.Close();
            }
            catch (System.Exception ex)
            {
                _monitor.Log($"Sprite error for {itemId}: {ex.Message}", LogLevel.Debug);
                ServeNotFound(response);
            }
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
