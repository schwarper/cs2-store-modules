# Store-CoinFlip
CoinFlip module for Store: Bet on coin flips each round or duel against other players.

# Config
Config will be auto generated. Default:
```json
{
    "tag": "{red}[Store] ",
    "enable_duel": true,
    "enable_coinflip_start_of_round": true,
    "min_coinflip": 0,
    "max_coinflip": 1000,
    "min_duel": 0,
    "max_duel": 1000,
    "coinflipBet_commands": [
        "coinflip",
        "cf"
    ],
    "coinflipSendDuel_commands": [
        "coinflipduel",
        "cfduel"
    ],
    "coinflipAcceptDuel_commands": [
        "coinflipduelaccept",
        "cfduelaccept"
    ],
    "coinflipDeclineDuel_commands": [
        "coinflipdueldecline",
        "cfdueldecline"
    ],
    "heads": {
        "multiplier": 2,
        "probability": 50
    },
    "tails": {
        "multiplier": 2,
        "probability": 50
    },
    "gif_enable": true,
    "heads_gif_or_image": "https://c.tenor.com/nEu74vu_sT4AAAAC/tenor.gif",
    "tails_gif_or_image": "https://c.tenor.com/Gv5d5zs4sisAAAAC/tenor.gif",
    "gif_display_duration": 4.0
}
```