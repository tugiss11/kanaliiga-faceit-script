/*Go to team info page and extract steam ids:
eg:https://play.toornament.com/en_US/tournaments/5161204601415041024/participants/5338543473750147072/info
*/
var team = {};
team.players = [];
team.group = 'B';
team.season_ending_rank = 1;
team.name = document.getElementsByTagName('h3')[0].innerHTML;
Array.from(document.getElementsByClassName('text standard small steam_player_id')).forEach(item => team.players.push({ id : item.textContent.split("\n")[1].trim()}));
console.log(JSON.stringify(team))