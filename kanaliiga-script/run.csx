#r "Newtonsoft.Json"
#r "System.Linq"

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;
using System;
using System.Net.Http.Headers;
using System.IO;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

private static readonly HttpClient client = new HttpClient();


public class Team
{
    public string name { get; set; }
    public List<Player> Players { get; set; }
    public string Group { get; set; }
    public int season_ending_rank { get; set; }

    public int AvgElo
    {
        get
        {
            var count = 0;
            var eloSum = 0;
            foreach (var player in Players.Where(o => o.faceit_elo.HasValue && o.faceit_elo > 0).OrderByDescending(o => o.faceit_elo).Take(5))
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
    public float? MedianElo
    {
        get
        {
            var players = Players.Where(o => o.faceit_elo.HasValue && o.faceit_elo > 0).OrderByDescending(o => o.faceit_elo).Take(5).ToArray();
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
}
public class Player
{
    public string id { get; set; }
    public string steam_name { get; set; }
    public int? faceit_elo { get; set; }
    public int csgo_hours { get; set; }
    public string faceit_name { get; set; }
    public int last_2_weeks_hours { get; set; }
}

private static string FACEIT_API_KEY = Environment.GetEnvironmentVariable("FACEIT_API_KEY");
private static string STEAM_API_KEY = Environment.GetEnvironmentVariable("STEAM_API_KEY");

private static readonly HttpClient faceit_client = new HttpClient();

public static async Task<IActionResult> Run(HttpRequest req, ILogger log)
{
    try
    {
        var format = req.Query["format"];
        faceit_client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", FACEIT_API_KEY);
        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        var teams = JsonConvert.DeserializeObject<List<Team>>(requestBody);
        foreach (var team in teams)
        {
            await FillTeamDetailsFromAPIsAsync(team, log);
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
    var fields = new List<string> { "AvgElo", "Group name", "Name" };
    var delimiter = "\t";
    result.AppendLine(string.Join(delimiter, fields));
    teams = teams.OrderByDescending(o => o.AvgElo).ToList();
    foreach (var team in teams)
    {
        var values = new List<string> { team.AvgElo.ToString().PadRight(6), team.Group, team.name, };
        result.AppendLine(string.Join(delimiter, values));
    }
    return result;
}

private static StringBuilder PrintTeamsByGroups(List<Team> teams)
{
    var result = new StringBuilder();
    var fields = new List<string> { "Group name", "Rank", "AvgElo", "Median", "Profile", "Name" };
    var delimiter = "\t";
    result.AppendLine(string.Join(delimiter, fields));
    teams = teams.OrderBy(o => o.season_ending_rank).ToList();

    var groups = teams.OrderBy(o => o.Group).Select(o => o.Group).Distinct().ToList();
    foreach (var groupname in groups)
    {
        foreach (var team in teams.Where(o => o.Group == groupname))
        {

            var values = new List<string> { groupname, team.season_ending_rank.ToString().PadRight(4), team.AvgElo.ToString().PadRight(6), team.MedianElo.ToString().PadRight(6), team.Profile.PadRight(7), team.name };
            result.AppendLine(string.Join(delimiter, values));
        }
        result.AppendLine(" ");
    }
    return result;
}

private static async Task FillTeamDetailsFromAPIsAsync(Team team, ILogger log)
{
    log.LogInformation($"{team.name}:");
    foreach (var player in team.Players)
    {
        /*var steamApiUrl = $"http://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/?key={STEAM_API_KEY}&steamids={player.id}";
        var response = await client.GetAsync(steamApiUrl);
        var responseString = await response.Content.ReadAsStringAsync();
        dynamic steamdata = JsonConvert.DeserializeObject<dynamic>(responseString);
        player.steam_name = steamdata.response.players[0].personaname; */
        if (String.IsNullOrEmpty(player.faceit_name))
        {
            var faceitplayerResponse = await faceit_client.GetAsync($"https://open.faceit.com/data/v4/players?game=csgo&game_player_id={player.id}");
            if (faceitplayerResponse.IsSuccessStatusCode)
            {
                var faceitplayerResponseString = await faceitplayerResponse.Content.ReadAsStringAsync();
                dynamic faceitplayerResponseData = JsonConvert.DeserializeObject<dynamic>(faceitplayerResponseString);
                player.faceit_elo = faceitplayerResponseData.games.csgo.faceit_elo;
                player.faceit_name = faceitplayerResponseData.games.csgo.game_player_name;
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
            dynamic faceitplayerResponseData = JsonConvert.DeserializeObject<dynamic>(faceitplayerResponseString);
            player.faceit_elo = faceitplayerResponseData.games.csgo.faceit_elo;
        }
        log.LogInformation($"{player.faceit_name} {player.faceit_elo} - {player.id}");
    }

    log.LogInformation($"AVG: {team.AvgElo} MEDIAN: {team.MedianElo}");
    log.LogInformation("--------------");
}
