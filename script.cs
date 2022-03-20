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
        public string Group { get; set;}
        public int season_ending_rank { get; set;}
        public int AvgElo { get; set;}

        public void CalculateAverageElo()
        {
            var count = 0;
            var eloSum = 0;
            foreach (var player in Players.Where(o => o.faceit_elo != null))
            {
                eloSum = eloSum + (int)player.faceit_elo;
                count++;
            }
            if (count > 0)
            {
                this.AvgElo = eloSum/count;
            }
        }
}
public class Player
{
        public string id { get; set; }
        public string steam_name { get; set;}
        public int? faceit_elo { get; set; }
        public int csgo_hours { get; set; }
        public string faceit_name { get; set;}
        public int last_2_weeks_hours { get; set; }
}

private static string FACEIT_API_KEY = Environment.GetEnvironmentVariable("FACEIT_API_KEY");
private static string STEAM_API_KEY = Environment.GetEnvironmentVariable("STEAM_API_KEY");

private static readonly HttpClient faceit_client = new HttpClient();

public static async Task<IActionResult> Run(HttpRequest req, ILogger log)
{
    try
    {
        faceit_client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", FACEIT_API_KEY);
        var result = new StringBuilder();
        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        var teams = JsonConvert.DeserializeObject<List<Team>>(requestBody);
        teams = teams.OrderBy(o => o.season_ending_rank).ToList();
        var groups = teams.OrderBy(o => o.Group).Select(o => o.Group).Distinct().ToList();
        foreach(var groupname in groups)
        {
            foreach (var team in teams.Where(o => o.Group == groupname))
            {
                await FillTeamDetailsFromAPIsAsync(team, log);
                result.AppendLine($"{groupname}: season end rank: {team.season_ending_rank} - avg elo: {team.AvgElo} ({team.name})");
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

private static async Task FillTeamDetailsFromAPIsAsync(Team team, ILogger log)
{
    log.LogInformation($"{team.name}:");
    foreach (var player in team.Players)
    {
        /*
        TODO get total played hours and last 2 week hours
        var steamApiUrl = $"http://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/?key={STEAM_API_KEY}&steamids={player.id}";
        var response = await client.GetAsync(steamApiUrl);
        var responseString = await response.Content.ReadAsStringAsync();
        dynamic steamdata = JsonConvert.DeserializeObject<dynamic>(responseString);
        player.steam_name = steamdata.response.players[0].personaname; */
        if (String.IsNullOrEmpty(player.faceit_name)) 
        {
            var faceitplayerResponse = await faceit_client.GetAsync($"https://open.faceit.com/data/v4/players?game=csgo&game_player_id={player.id}");
            var faceitplayerResponseString = await faceitplayerResponse.Content.ReadAsStringAsync();
            dynamic faceitplayerResponseData = JsonConvert.DeserializeObject<dynamic>(faceitplayerResponseString); 
            player.faceit_elo = faceitplayerResponseData.games.csgo.faceit_elo;
            player.faceit_name = faceitplayerResponseData.games.csgo.game_player_name;
        } else { //For some reason using SteamID gives wrong results for some players
            var faceitplayerResponse = await faceit_client.GetAsync($"https://open.faceit.com/data/v4/players?game=csgo&nickname={player.faceit_name}");
            var faceitplayerResponseString = await faceitplayerResponse.Content.ReadAsStringAsync();
            dynamic faceitplayerResponseData = JsonConvert.DeserializeObject<dynamic>(faceitplayerResponseString); 
            player.faceit_elo = faceitplayerResponseData.games.csgo.faceit_elo;
        }
        log.LogInformation($"{player.faceit_name} {player.faceit_elo} - {player.id}");
    }
    team.CalculateAverageElo();
    log.LogInformation($"AVG: {team.AvgElo}");
    log.LogInformation("--------------");
}



