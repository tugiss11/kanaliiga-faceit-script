# kanaliiga-faceit-script
Script for fetching Faceit elo with list of steam ids or faceit nicknames.  
Currently deployable as Azure Function which can be called with Teams.json as request body.

# Scraping steam ids from Toornament

Run scraper
```
cd scraping\toornament\spiders
scrapy runspider players-spider.py -o scraped_players.json

```

# Debugging

Start function app
```
func host start
```
Send POST request
```
bash test.sh
```

# Static web site

https://kanaliigascript.z16.web.core.windows.net/index.html
