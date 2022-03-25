# kanaliiga-faceit-script
Script for fetching Faceit elo with list of steam ids or faceit nicknames.  
Currently deployable as Azure Function which can be called with Teams.json as request body.

# Teams data
Manually inputted to Teams.json by running javascript in Toornament website  
TODO, get from Toornament API? Kanaliiga stats API when?

# Debugging

Start function app
```
func host start
```
Send POST request
```
bash test.sh
```
