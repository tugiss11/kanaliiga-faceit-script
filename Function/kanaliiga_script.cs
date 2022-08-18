using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace kanaliiga_script
{
    public static class kanaliiga_script
    {
        private static readonly HttpClient client = new HttpClient();
        private static string FACEIT_API_KEY = Environment.GetEnvironmentVariable("FACEIT_API_KEY");
        private static string AZURE_STORAGE_CONNECTION_STRING = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");
        private static string STEAM_API_KEY = Environment.GetEnvironmentVariable("STEAM_API_KEY");

        private static readonly HttpClient faceit_client = new HttpClient();

        private const string CONTAINER_NAME = "kanaliiga";

        private const string BLOB_NAME = "data.json";

        [FunctionName("kanaliiga_script")]
        public static async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            try
            {

                BlobContainerClient container = new BlobContainerClient(AZURE_STORAGE_CONNECTION_STRING, CONTAINER_NAME);
                BlobClient blobClient = container.GetBlobClient(BLOB_NAME);
                var format = req.Query["format"];
                var reload = req.Query["reload"];
                faceit_client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", FACEIT_API_KEY);
                var teams = new List<Team>();
                var shouldUpdateBlob = false;
                if (reload == "true" || !blobClient.Exists())
                {
                    string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                    teams = JsonConvert.DeserializeObject<List<Team>>(requestBody);
                    log.LogInformation($"Fetching new data for {teams.Count} teams");
                    var semaphoreSlim = new SemaphoreSlim(3);
                    var tasks = teams.Select(async team =>
                    {
                        await semaphoreSlim.WaitAsync();
                        try
                        {
                            await FillTeamDetailsFromAPIsAsync(team, log);
                        }
                        finally
                        {
                            semaphoreSlim.Release();
                        }
                    });
                    await Task.WhenAll(tasks);

                    shouldUpdateBlob = true;
                }
                else
                {
                    using var stream = await blobClient.OpenReadAsync(null);
                    teams = await System.Text.Json.JsonSerializer.DeserializeAsync<List<Team>>(stream);
                    string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                    var teamsInBody = JsonConvert.DeserializeObject<List<Team>>(requestBody);
                    var existingTeamNames = teams.Select(t => t.name).ToList();
                    teams.AddRange(teamsInBody.Where(o => !existingTeamNames.Contains(o.name)));
                    log.LogInformation($"Fetching new data for {teams.Count(o => !o.HasData)} teams");
                    var semaphoreSlim = new SemaphoreSlim(2);
                    var tasks = teams.Where(o => !o.HasData).Select(async team =>
                    {
                        await semaphoreSlim.WaitAsync();
                        try
                        {
                            await FillTeamDetailsFromAPIsAsync(team, log);
                        }
                        finally
                        {
                            semaphoreSlim.Release();
                        }
                    });
                    await Task.WhenAll(tasks);

                    log.LogInformation($"Using stored data for {teams.Count} teams");
                }

                StringBuilder result;
                if (format == "elo")
                {
                    result = PrintTeamsByElo(teams);
                }
                else
                {
                    result = PrintTeamsByGroups(teams);
                }
                var jsonToUpload = JsonConvert.SerializeObject(teams);
                if (shouldUpdateBlob)
                {
                    using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(jsonToUpload)))
                    {
                        await blobClient.UploadAsync(ms, true);
                        log.LogInformation($"New blob stored with {teams.Count} teams");
                    }
                }

                return (ActionResult)new OkObjectResult(result.ToString());
            }
            catch (Exception ex)
            {
                log.LogInformation($"{ex.Message}");
                log.LogInformation($"{ex.StackTrace}");
                return (ActionResult)new OkObjectResult("Taking a break.");
            }
        }


        private static StringBuilder PrintTeamsByElo(List<Team> teams)
        {
            var result = new StringBuilder();
            var fields = new List<string> { "AvgElo", "Median", "TotalGames", "Median", "Winrate", "AvgTotalHours", "AvgLast2Weeks", "?", "HasFaceit".PadRight(8), "Name" };
            var delimiter = "\t";
            result.AppendLine(string.Join(delimiter, fields));
            teams = teams.OrderByDescending(o => o.AvgElo).ThenByDescending(n => n.AvgTotalHours).ToList();
            foreach (var team in teams)
            {
                var values = new List<string> { team.AvgElo.ToString().PadRight(6), team.MedianElo.ToString().PadRight(6), team.TotalGames.ToString().PadRight(10), team.MedianGames.ToString().PadRight(6), team.WinRate.ToString().PadRight(6),
                    team.AvgTotalHours.ToString().PadRight(13), team.AvgLast2Week.ToString().PadRight(13), team.PrivateCount.ToString(), team.PlayersWithFaceitElo.PadRight(8), $"{team.name} ({team.company_name})" };
                result.AppendLine(string.Join(delimiter, values));
            }
            return result;
        }

        private static StringBuilder PrintTeamsByGroups(List<Team> teams)
        {
            var result = new StringBuilder();
            var fields = new List<string> { "Group name", "Rank", "AvgElo", "Median", "AvgTotalHours", "AvgLast2Weeks", "?", "HasFaceit".PadRight(8), "Name" };
            var delimiter = "\t";
            result.AppendLine(string.Join(delimiter, fields));
            teams = teams.OrderBy(o => o.season_ending_rank).ToList();

            var groups = teams.OrderBy(o => o.Group).Select(o => o.Group).Distinct().ToList();
            foreach (var groupname in groups)
            {
                foreach (var team in teams.Where(o => o.Group == groupname))
                {
                    var values = new List<string> { groupname, team.season_ending_rank.ToString().PadRight(4), team.AvgElo.ToString().PadRight(6), team.MedianElo.ToString().PadRight(6),
                    team.AvgTotalHours.ToString().PadRight(13), team.AvgLast2Week.ToString().PadRight(13), team.PrivateCount.ToString(), team.PlayersWithFaceitElo.PadRight(8),  $"{team.name} (${team.company_name})" };
                    result.AppendLine(string.Join(delimiter, values));
                }
                result.AppendLine(" ");
            }
            return result;
        }

        private static async Task FillTeamDetailsFromAPIsAsync(Team team, ILogger log)
        {
            log.LogInformation($"Team name: {team.name}");
            foreach (var player in team.Players)
            {
                var steamApiUrl = $"https://api.steampowered.com/IPlayerService/GetRecentlyPlayedGames/v1/?key={STEAM_API_KEY}&steamid={player.id}";
                var response = await client.GetAsync(steamApiUrl);
                var responseString = await response.Content.ReadAsStringAsync();
                RecentlyPlayedGames steamdata = JsonConvert.DeserializeObject<RecentlyPlayedGames>(responseString);
                Game gamedata = steamdata?.response?.games?.FirstOrDefault(o => o.appid == 730);
                if (gamedata != null)
                {
                    player.playtime_2weeks = gamedata.playtime_2weeks;
                    player.playtime_forever = gamedata.playtime_forever;
                }
                else
                {
                    player.is_public = false;
                }


                if (String.IsNullOrEmpty(player.faceit_name))
                {
                    var faceitplayerResponse = await faceit_client.GetAsync($"https://open.faceit.com/data/v4/players?game=csgo&game_player_id={player.id}");
                    if (faceitplayerResponse.IsSuccessStatusCode)
                    {
                        var faceitplayerResponseString = await faceitplayerResponse.Content.ReadAsStringAsync();
                        dynamic faceitplayerResponseData = JsonConvert.DeserializeObject<dynamic>(faceitplayerResponseString);
                        player.faceit_elo = faceitplayerResponseData.games.csgo.faceit_elo;
                        player.faceit_name = faceitplayerResponseData.nickname;
                        player.steam_name = faceitplayerResponseData.steam_nickname;
                        var faceitstatsResponse = await faceit_client.GetAsync($"https://open.faceit.com/data/v4/players/{faceitplayerResponseData.player_id}/stats/csgo");
                        if (faceitstatsResponse.IsSuccessStatusCode)
                        {
                            var faceitstatsResponseString = await faceitstatsResponse.Content.ReadAsStringAsync();
                            //log.LogInformation(faceitstatsResponseString);
                            dynamic faceitstatsResponseData = JsonConvert.DeserializeObject<dynamic>(faceitstatsResponseString);
                            player.faceit_matches = faceitstatsResponseData.lifetime.Matches;
                            player.faceit_kd = faceitstatsResponseData.lifetime["Average K/D Ratio"];
                            player.faceit_winrate = faceitstatsResponseData.lifetime["Win Rate %"];
                        }
                    }
                    else
                    {
                        player.faceit_elo = 1000; // Using new profile elo
                        player.faceit_name = "No faceit profile";
                    }
                }
                else
                { //For some reason using SteamID gives wrong results for some players
                    var faceitplayerResponse = await faceit_client.GetAsync($"https://open.faceit.com/data/v4/players?game=csgo&nickname={player.faceit_name}");
                    var faceitplayerResponseString = await faceitplayerResponse.Content.ReadAsStringAsync();
                    if (faceitplayerResponse.IsSuccessStatusCode)
                    {
                        dynamic faceitplayerResponseData = JsonConvert.DeserializeObject<dynamic>(faceitplayerResponseString);
                        player.faceit_elo = faceitplayerResponseData.games.csgo.faceit_elo;
                        player.steam_name = faceitplayerResponseData.steam_nickname;
                        var faceitstatsResponse = await faceit_client.GetAsync($"https://open.faceit.com/data/v4/players/{faceitplayerResponseData.player_id}/stats/csgo");
                        if (faceitstatsResponse.IsSuccessStatusCode)
                        {
                            var faceitstatsResponseString = await faceitstatsResponse.Content.ReadAsStringAsync();
                            dynamic faceitstatsResponseData = JsonConvert.DeserializeObject<dynamic>(faceitstatsResponseString);
                            player.faceit_matches = faceitstatsResponseData.lifetime.matches;
                            player.faceit_kd = faceitstatsResponseData.lifetime["K/D Ratio"];
                            player.faceit_winrate = faceitstatsResponseData.lifetime["Win Rate %"];
                        }
                    }
                }
                log.LogInformation($"{player.faceit_name} ({player.faceit_elo}) - {player.id} - {player.playtime_2weeks}mins {player.faceit_matches} matches");
            }

            log.LogInformation($"AVG: {team.AvgElo} MEDIAN: {team.MedianElo}");
            log.LogInformation("--------------");
        }

        [FunctionName("fun_facts")]
        public static async Task<ActionResult> FunFactAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req,
        ILogger log)
        {
            BlobContainerClient container = new BlobContainerClient(AZURE_STORAGE_CONNECTION_STRING, CONTAINER_NAME);
            BlobClient blobClient = container.GetBlobClient(BLOB_NAME);
            using var stream = await blobClient.OpenReadAsync(null);
            var teams = await System.Text.Json.JsonSerializer.DeserializeAsync<List<Team>>(stream);
            log.LogInformation($"Using stored data for {teams.Count} teams");
            var allPlayers = teams.SelectMany(o => o.Players).ToList();
            var result = new StringBuilder();
            result.AppendLine($"Fetched data for {allPlayers.Count} players in {teams.Count} teams and {allPlayers.Count(o => o.HasFaceit)} players have Faceit profiles.");
            result.AppendLine($"{allPlayers.Count(o => o.HasFaceitMatches)} players have played matches in Faceit and {allPlayers.Count(o => o.faceit_matches > 10)} have over 10 Faceit games.");
            result.AppendLine($"There are {allPlayers.Count(o => o.faceit_elo >= 3000)} players with over 3k ELO while average ELO is {Math.Round(Convert.ToDecimal(allPlayers.Where(o => o.HasFaceitMatches).Average(o => o.faceit_elo)), 2)}.");
            result.AppendLine($"Highest amount of Faceit games for a player is {allPlayers.Max(o => o.faceit_matches)} while average for players with any games played is {Math.Round(Convert.ToDecimal(allPlayers.Where(o => o.HasFaceitMatches).Average(o => o.faceit_matches)), 2)}.");
            result.AppendLine($"Highest play time is {Math.Round((decimal)allPlayers.Max(o => o.playtime_forever / 60), 2)} hours, lowest play time is {Math.Round((decimal)allPlayers.Where(o => o.is_public).Min(o => o.playtime_forever / 60), 2)} and average play time is {Math.Round(allPlayers.Where(o => o.is_public).Average(o => o.playtime_forever / 60), 2)} hours.");
            result.AppendLine($"{allPlayers.Count(o => !o.is_public)} players have the played hours hidden in the profile.");
            return (ActionResult)new OkObjectResult(result.ToString());
        }
    }

    public class Game
    {
        public int appid { get; set; }
        public string name { get; set; }
        public int playtime_2weeks { get; set; }
        public int playtime_forever { get; set; }
        public string img_icon_url { get; set; }
        public int playtime_windows_forever { get; set; }
        public int playtime_mac_forever { get; set; }
        public int playtime_linux_forever { get; set; }
    }

    public class RecentlyPlayedGamesResponse
    {
        public int total_count { get; set; }
        public List<Game> games { get; set; }
    }

    public class RecentlyPlayedGames
    {
        public RecentlyPlayedGamesResponse response { get; set; }
    }


    public class Team
    {
        public string name { get; set; }
        public string company_name { get; set; } = "";
        public List<Player> Players { get; set; }
        public string Group { get; set; } = "";
        public int season_ending_rank { get; set; } = 0;

        public bool HasData => Players.Any(o => o.faceit_matches > 0);

        public string PlayersWithFaceitElo
        {
            get
            {
                return $"{Players.Count(o => o.HasFaceit)}/{Players.Count()}";
            }
        }

        public int AvgElo
        {
            get
            {
                if (Players.Count(o => o.HasFaceitMatches) == 0)
                {
                    return 0;
                }
                var count = 0;
                var eloSum = 0;
                foreach (var player in Players.Where(o => o.HasFaceitMatches).OrderByDescending(o => o.faceit_elo).Take(5))
                {
                    eloSum = eloSum + player.faceit_elo.Value;
                    count++;
                }
                if (count > 0)
                {
                    return eloSum / count;
                }
                return 0;
            }
        }

        public int AvgMatches
        {
            get
            {
                var count = 0;
                var matchSum = 0;
                foreach (var player in Players.OrderByDescending(o => o.faceit_elo).Take(5))
                {
                    matchSum = matchSum + player.faceit_matches;
                    count++;
                }
                if (count > 0)
                {
                    return matchSum / count;
                }
                return 0;
            }
        }

        public int AvgTotalHours
        {
            get
            {
                var count = 0;
                var hoursSum = 0;
                foreach (var player in Players.Where(o => o.faceit_elo.HasValue && o.faceit_elo > 0).OrderByDescending(o => o.faceit_elo).Take(5))
                {
                    if (player.is_public)
                    {
                        hoursSum = hoursSum + player.playtime_forever;
                        count++;
                    }

                }
                if (count > 0)
                {
                    return hoursSum / 60 / count;
                }
                return 0;
            }
        }


        public int AvgLast2Week
        {
            get
            {
                var count = 0;
                var hoursSum = 0;
                foreach (var player in Players.Where(o => o.faceit_elo.HasValue && o.faceit_elo > 0).OrderByDescending(o => o.faceit_elo).Take(5))
                {
                    if (player.is_public)
                    {
                        hoursSum = hoursSum + player.playtime_2weeks;
                        count++;
                    }

                }
                if (count > 0)
                {
                    return hoursSum / 60 / count;
                }
                return 0;
            }
        }



        public float? MedianElo
        {
            get
            {
                if (Players.Count(o => o.HasFaceitMatches) == 0)
                {
                    return 0;
                }
                var players = Players.Where(o => o.HasFaceitMatches).OrderByDescending(o => o.faceit_elo).Take(5).ToArray();
                var n = players.Length;
                if (n % 2 == 0)
                {
                    return (players[(n / 2) - 1].faceit_elo + players[(n / 2)].faceit_elo) / 2.0F;
                }
                else
                {
                    return players[(n / 2)].faceit_elo;
                }

            }
        }

        public int PrivateCount => Players.Count(o => !o.is_public);

        public string Profile
        {
            get
            {
                if (AvgElo > MedianElo + 150)
                {
                    return "1v5";
                }
                if (MedianElo > AvgElo + 150)
                {
                    return "4v5";
                }
                return "Balanced";
            }

        }

        public List<Player> TopPlayers
        {
            get
            {
                return Players.Where(o => o.HasFaceitMatches).OrderByDescending(o => o.faceit_elo).Take(5).ToList();
            }
        }

        public int TotalGames => TopPlayers.Any() ? TopPlayers.Sum(o => o.faceit_matches) : 0;

        public float? MedianGames
        {
            get
            {
                if (Players.Count(o => o.HasFaceitMatches) == 0)
                {
                    return 0;
                }
                var players = TopPlayers.ToArray();
                var n = players.Length;
                if (n % 2 == 0)
                {
                    return (TopPlayers[(n / 2) - 1].faceit_matches + TopPlayers[(n / 2)].faceit_matches) / 2.0F;
                }
                else
                {
                    return players[(n / 2)].faceit_matches;
                }

            }
        }

        public decimal AvgKd => TopPlayers.Any() ? Math.Round((decimal)TopPlayers.Average(o => o.faceit_kd), 0) : 0;
        public decimal WinRate => TopPlayers.Any() ? Math.Round((decimal)TopPlayers.Average(o => o.faceit_winrate), 1) : 0;
    }
    public class Player
    {
        public string id { get; set; }
        public string steam_name { get; set; }
        public int? faceit_elo { get; set; }
        public int playtime_2weeks { get; set; }
        public string faceit_name { get; set; }
        public int playtime_forever { get; set; }
        public bool is_public { get; set; } = true;
        public bool HasFaceit
        {
            get
            {
                return faceit_name != "No faceit profile";
            }
        }

        public bool HasFaceitMatches
        {
            get
            {
                return faceit_matches > 0;
            }
        }

        public int faceit_matches { get; set; } = 0;
        public double faceit_winrate { get; set; }
        public double faceit_kd { get; set; }
    }


}
