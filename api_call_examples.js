/*API CALL EXAMPLES
from https://github.com/faceitFinder/faceitFinder/
*/
const { default: fetch } = require('node-fetch')

const getDatas = (steamId) => fetch(`http://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/?key=${process.env.STEAM_TOKEN}&steamids=${steamId}`)
  .then(res => {
    if (res.status == 200) return res.json()
    else throw 'An error has occured'
  })
  .then(data => {
    if (data.response.players) return data.response.players[0]
    else throw 'Invalid steamid'
  })

const headerFaceit = {
    Authorization: `Bearer ${process.env.FACEIT_TOKEN}`
  }
  
const fetchData = async (url, error) => fetch(url, {
    method: 'GET',
    headers: headerFaceit
  })
    .then(res => {
      if (res.status == 200) return res.json()
      else throw error
    })
    .then(data => data)

const getId = async (steamId) => (await Faceit.fetchData(`https://open.faceit.com/data/v4/players?game=csgo&game_player_id=${steamId}`, 'Faceit profile not found')).player_id

const getDatas = (playerId) => Faceit.fetchData(`https://open.faceit.com/data/v4/players/${playerId}`, 'Couldn\'t get faceit datas')