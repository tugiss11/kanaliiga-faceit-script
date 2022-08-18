import scrapy

"""
how to run the spider
scrapy runspider players_spider.py -o scraped_players.json

https://docs.scrapy.org/en/latest/topics/debug.html
"""


class PlayersSpider(scrapy.Spider):
    name = 'players'  # spider name
    # the url to crawl
    start_urls = [
        'https://play.toornament.com/en_US/tournaments/5678863007994986496/participants/?page=1',
        'https://play.toornament.com/en_US/tournaments/5678863007994986496/participants/?page=2',
        'https://play.toornament.com/en_US/tournaments/5678863007994986496/participants/?page=3',
        'https://play.toornament.com/en_US/tournaments/5678863007994986496/participants/?page=4',
        'https://play.toornament.com/en_US/tournaments/5678863007994986496/participants/?page=5',
        'https://play.toornament.com/en_US/tournaments/5678863007994986496/participants/?page=6',
        'https://play.toornament.com/en_US/tournaments/5678863007994986496/participants/?page=7'
        
    ]

    def parse(self, response):
      
        divs = response.xpath('//div[@class="size-1-of-4"]')
        for div in enumerate(divs):
            link = 'https://play.toornament.com' + div[1].css('a').attrib['href'] + 'info'
            yield scrapy.Request(link, callback = self.parse_team)


    def parse_team(self, response):
        team_name = response.css('h3::text').get()    
        company_name = response.xpath("//div[contains(text(), 'Yrityksen nimi')]//text()").get().split('\n')[1].strip()
        steamids = response.css('.steam_player_id::text').getall() 
        steamid_list = []
        for steamid in steamids:
            steamid_list.append({"id" : steamid.split('\n')[1].strip()})
        yield {
            "name" : team_name,
            "players" : steamid_list,
            "company_name" : company_name
        }
