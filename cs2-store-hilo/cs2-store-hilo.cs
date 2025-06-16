using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Commands;
using StoreApi;
using System.Text.Json.Serialization;

namespace Store_HiLo;

public class Store_HiLoConfig : BasePluginConfig
{
    [JsonPropertyName("tag")]
    public string Tag { get; set; } = "{red}[Store] ";

    [JsonPropertyName("min_bet")]
    public int MinBet { get; set; } = 10;

    [JsonPropertyName("max_bet")]
    public int MaxBet { get; set; } = 1000;

    [JsonPropertyName("hi_lo_commands")]
    public List<string> HiLoCommands { get; set; } = ["hilo", "starthilo"];

    [JsonPropertyName("hi_lo_high_commands")]
    public List<string> HiLoHighCommands { get; set; } = ["high", "more"];

    [JsonPropertyName("hi_lo_low_commands")]
    public List<string> HiLoLowCommands { get; set; } = ["low", "less"];

    [JsonPropertyName("hi_lo_cashout_commands")]
    public List<string> HiLoCashoutCommands { get; set; } = ["cashout"];

    [JsonPropertyName("hi_lo_equal_commands")]
    public List<string> HiLoEqualCommands { get; set; } = ["equal"];
}

public class HiLoGame(CCSPlayerController player, int betCredits, string currentCard)
{
    public CCSPlayerController Player { get; set; } = player;
    public int BetCredits { get; set; } = betCredits;
    public string CurrentCard { get; set; } = currentCard;
    public float CurrentMultiplier { get; set; } = 1.0f;
    public bool IsActive { get; set; } = true;
    public int CorrectGuesses { get; set; } = 0;
}

public class Store_HiLo : BasePlugin, IPluginConfig<Store_HiLoConfig>
{
    public override string ModuleName => "Store Module [Hi-Lo]";
    public override string ModuleVersion => "0.0.1";
    public override string ModuleAuthor => "Nathy";

    private readonly Random random = new();
    private readonly string[] cardValues = ["A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K"];
    private readonly string[] cardSuits = ["♠", "♥", "♣", "♦"];
    public Store_HiLoConfig Config { get; set; } = new();
    public IStoreApi? StoreApi { get; set; }
    private readonly Dictionary<string, HiLoGame> activeGames = [];

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        StoreApi = IStoreApi.Capability.Get() ?? throw new Exception("StoreApi could not be located.");
        CreateCommands();
    }

    public void OnConfigParsed(Store_HiLoConfig config)
    {
        config.Tag = config.Tag.ReplaceColorTags();
        Config = config;
    }

    private void CreateCommands()
    {
        foreach (string cmd in Config.HiLoCommands)
        {
            AddCommand($"css_{cmd}", "Start a Hi-Lo bet", Command_HiLo);
        }
        foreach (string cmd in Config.HiLoLowCommands)
        {
            AddCommand($"css_{cmd}", "Guess low", Command_HiLoLess);
        }
        foreach (string cmd in Config.HiLoHighCommands)
        {
            AddCommand($"css_{cmd}", "Guess high", Command_HiLoMore);
        }
        foreach (string cmd in Config.HiLoCashoutCommands)
        {
            AddCommand($"css_{cmd}", "Cash out", Command_HiLoCashout);
        }
        foreach (string cmd in Config.HiLoEqualCommands)
        {
            AddCommand($"css_{cmd}", "Guess equal", Command_HiLoEqual);
        }
    }

    [CommandHelper(minArgs: 1, usage: "<credits>")]
    public void Command_HiLo(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null) return;

        if (StoreApi == null) throw new Exception("StoreApi could not be located.");

        if (!int.TryParse(info.GetArg(1), out int credits))
        {
            info.ReplyToCommand(Config.Tag + Localizer["Invalid amount of credits"]);
            return;
        }

        if (credits < Config.MinBet)
        {
            info.ReplyToCommand(Config.Tag + Localizer["Minimum bet amount", Config.MinBet]);
            return;
        }

        if (credits > Config.MaxBet)
        {
            info.ReplyToCommand(Config.Tag + Localizer["Maximum bet amount", Config.MaxBet]);
            return;
        }

        if (StoreApi.GetPlayerCredits(player) < credits)
        {
            info.ReplyToCommand(Config.Tag + Localizer["Not enough credits"]);
            return;
        }

        string initialCard = DrawCard();
        StartHiLoGame(player, credits, initialCard);
    }

    private void StartHiLoGame(CCSPlayerController player, int credits, string initialCard)
    {
        StoreApi!.GivePlayerCredits(player, -credits);
        player.PrintToChat(Config.Tag + Localizer["Bet placed", credits]);

        HiLoGame game = new(player, credits, initialCard);
        activeGames[player.SteamID.ToString()] = game;

        player.PrintToChat(Config.Tag + Localizer["Current card", game.CurrentCard]);
        player.PrintToChat(Config.Tag + Localizer["Make guess"]);
    }

    [CommandHelper(minArgs: 0)]
    public void Command_HiLoMore(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !activeGames.TryGetValue(player.SteamID.ToString(), out HiLoGame? game) || !game.IsActive)
        {
            info.ReplyToCommand(Config.Tag + Localizer["No active game"]);
            return;
        }

        ProcessGuess(player, game, "more");
    }

    [CommandHelper(minArgs: 0)]
    public void Command_HiLoLess(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !activeGames.TryGetValue(player.SteamID.ToString(), out HiLoGame? game) || !game.IsActive)
        {
            info.ReplyToCommand(Config.Tag + Localizer["No active game"]);
            return;
        }

        ProcessGuess(player, game, "less");
    }

    [CommandHelper(minArgs: 0)]
    public void Command_HiLoCashout(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !activeGames.TryGetValue(player.SteamID.ToString(), out HiLoGame? game) || !game.IsActive)
        {
            info.ReplyToCommand(Config.Tag + Localizer["No active game"]);
            return;
        }

        Cashout(player, game);
    }

    [CommandHelper(minArgs: 0)]
    public void Command_HiLoEqual(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !activeGames.TryGetValue(player.SteamID.ToString(), out HiLoGame? game) || !game.IsActive)
        {
            info.ReplyToCommand(Config.Tag + Localizer["No active game"]);
            return;
        }

        ProcessGuess(player, game, "equal");
    }

    private void ProcessGuess(CCSPlayerController player, HiLoGame game, string guess)
    {
        string nextCard = DrawCard();
        bool guessedCorrectly = false;
        float bonusFactor = 1.0f;

        switch (guess)
        {
            case "more":
                {
                    guessedCorrectly = CompareCards(nextCard, game.CurrentCard) > 0;
                    bonusFactor = GetBonusFactor(game.CurrentCard, true);
                    break;
                }
            case "less":
                {
                    guessedCorrectly = CompareCards(nextCard, game.CurrentCard) < 0;
                    bonusFactor = GetBonusFactor(game.CurrentCard, false);
                    break;
                }
            case "equal":
                {
                    guessedCorrectly = CompareCards(nextCard, game.CurrentCard) == 0;
                    break;
                }
        }

        if (guessedCorrectly)
        {
            player.PrintToChat(Config.Tag + Localizer["Correct guess"]);
            game.CorrectGuesses++;

            if (guess == "equal")
            {
                game.CurrentMultiplier *= 10.0f;
            }
            else
            {
                float multiplierIncrease = 1.2f + (game.CorrectGuesses * 0.05f);
                game.CurrentMultiplier *= multiplierIncrease * bonusFactor;
            }

            player.PrintToChat(Config.Tag + Localizer["Current multiplier", game.CurrentMultiplier.ToString("F2")]);

            game.CurrentCard = nextCard;
            player.PrintToChat(Config.Tag + Localizer["Current card", game.CurrentCard]);
        }
        else
        {
            player.PrintToChat(Config.Tag + Localizer["Incorrect guess", nextCard]);
            EndGame(player, game, false);
        }
    }

    private float GetBonusFactor(string currentCard, bool isHigher)
    {
        int currentValue = Array.IndexOf(cardValues, currentCard[..^1]);

        if (isHigher)
        {
            if (currentValue == cardValues.Length - 1)
            {
                return 1.0f;
            }

            int options = cardValues.Length - 1 - currentValue;
            return options switch
            {
                1 => 2.0f,
                2 => 1.5f,
                3 => 1.3f,
                _ => 1.0f
            };
        }
        else
        {
            if (currentValue == 0)
            {
                return 1.0f;
            }

            int options = currentValue;
            return options switch
            {
                1 => 2.0f,
                2 => 1.5f,
                3 => 1.3f,
                _ => 1.0f
            };
        }
    }

    private void EndGame(CCSPlayerController player, HiLoGame game, bool cashout = false)
    {
        if (cashout)
        {
            int winnings = (int)(game.BetCredits * game.CurrentMultiplier);
            int profit = winnings - game.BetCredits;
            StoreApi!.GivePlayerCredits(player, winnings);

            player.PrintToChat(Config.Tag + Localizer["Cash out", game.BetCredits, game.CurrentMultiplier.ToString("F2"), winnings, profit]);
        }
        else
        {
            int lostCredits = game.BetCredits * (game.CorrectGuesses > 0 ? (int)Math.Pow(2.0, game.CorrectGuesses) : 1);
            player.PrintToChat(Config.Tag + Localizer["Game ended", game.CorrectGuesses, game.CurrentMultiplier.ToString("F2"), lostCredits, game.BetCredits]);
        }

        game.IsActive = false;
        activeGames.Remove(player.SteamID.ToString());
    }

    private void Cashout(CCSPlayerController player, HiLoGame game)
    {
        EndGame(player, game, true);
    }

    private string DrawCard()
    {
        string value = cardValues[random.Next(cardValues.Length)];
        string suit = cardSuits[random.Next(cardSuits.Length)];
        return $"{value}{suit}";
    }

    private int CompareCards(string card1, string card2)
    {
        int value1 = Array.IndexOf(cardValues, card1[..^1]);
        int value2 = Array.IndexOf(cardValues, card2[..^1]);
        return value1.CompareTo(value2);
    }
}