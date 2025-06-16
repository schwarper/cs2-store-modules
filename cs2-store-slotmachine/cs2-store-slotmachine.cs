using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Commands;
using StoreApi;

namespace Store_SlotMachine;

public class StoreSlotMachineConfig : BasePluginConfig
{
    [JsonPropertyName("tag")]
    public string Tag { get; set; } = "{red}[Store]";
    
    [JsonPropertyName("min_bet")]
    public int MinBet { get; set; } = 10;

    [JsonPropertyName("max_bet")]
    public int MaxBet { get; set; } = 1000;

    [JsonPropertyName("slot_machine_commands")]
    public List<string> SlotMachineCommands { get; set; } =  [ "slotmachine" ];

    [JsonPropertyName("slotmachine_command_cooldown")]
    public int SlotMachineCommandCooldown { get; set; } = 10;

    [JsonPropertyName("reward_multipliers")]
    public Dictionary<string, SlotMachineSymbol> RewardMultipliers { get; set; } = new()
    {
        { "★", new SlotMachineSymbol { Multiplier = 10, Chance = 2 } },
        { "♞", new SlotMachineSymbol { Multiplier = 8, Chance = 3 } },
        { "⚓", new SlotMachineSymbol { Multiplier = 6, Chance = 3 } },
        { "☕", new SlotMachineSymbol { Multiplier = 5, Chance = 4 } },
        { "⚽", new SlotMachineSymbol { Multiplier = 4, Chance = 4 } },
        { "☀", new SlotMachineSymbol { Multiplier = 3, Chance = 5 } },
        { "☁", new SlotMachineSymbol { Multiplier = 2, Chance = 5 } },
        { "✿", new SlotMachineSymbol { Multiplier = 15, Chance = 1 } },
        { "☾", new SlotMachineSymbol { Multiplier = 20, Chance = 0.5 } }
    };

    [JsonPropertyName("slot_timers")]
    public SlotTimersConfig SlotTimers { get; set; } = new();

    [JsonPropertyName("partial_win_percentage")]
    public int PartialWinPercentage { get; set; } = 50;

    [JsonPropertyName("sequential_symbols_only")]
    public bool SequentialSymbolsOnly { get; set; } = false;

    [JsonIgnore]
    public List<string> Emojis => RewardMultipliers.Keys.ToList();
}

public class SlotTimersConfig
{
    [JsonPropertyName("first_stop")]
    public float FirstStop { get; set; } = 1.0f;

    [JsonPropertyName("second_stop")]
    public float SecondStop { get; set; } = 2.0f;

    [JsonPropertyName("third_stop")]
    public float ThirdStop { get; set; } = 3.0f;
}

public class SlotMachineSymbol
{
    [JsonPropertyName("multiplier")]
    public int Multiplier { get; set; }

    [JsonPropertyName("chance")]
    public double Chance { get; set; }
}

public class StoreSlotMachine : BasePlugin, IPluginConfig<StoreSlotMachineConfig>
{
    public override string ModuleName => "Store Module [Slot Machine]";
    public override string ModuleVersion => "0.1.0";
    public override string ModuleAuthor => "Nathy";

    private readonly Random _random = new();
    private IStoreApi? StoreApi { get; set; }
    public StoreSlotMachineConfig Config { get; set; } = new();
    private readonly ConcurrentDictionary<string, SlotMachineGame> _activeGames = new();
    private readonly ConcurrentDictionary<string, DateTime> _playerLastSlotMachineCommandTimes = new();

    private List<string> _emojis = [];

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        StoreApi = IStoreApi.Capability.Get() ?? throw new Exception("StoreApi could not be located.");

        _emojis = Config.Emojis;

        CreateCommands();
        RegisterListener<Listeners.OnTick>(OnTick);
    }

    public void OnConfigParsed(StoreSlotMachineConfig config)
    {
        config.Tag = config.Tag.ReplaceColorTags();
        
        config.MinBet = Math.Max(0, config.MinBet);
        config.MaxBet = Math.Max(config.MinBet + 1, config.MaxBet);

        Config = config;
    }

    private void CreateCommands()
    {
        foreach (string cmd in Config.SlotMachineCommands)
        {
            AddCommand($"css_{cmd}", "Start a slot machine bet", Command_Slot);
        }
    }

    [CommandHelper(minArgs: 1, usage: "<bet_amount>")]
    private void Command_Slot(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null) return;

        if (StoreApi == null) throw new Exception("StoreApi could not be located.");

        if (_playerLastSlotMachineCommandTimes.TryGetValue(player.SteamID.ToString(), out DateTime lastCommandTime))
        {
            double cooldownRemaining = (DateTime.Now - lastCommandTime).TotalSeconds;
            if (cooldownRemaining < Config.SlotMachineCommandCooldown)
            {
                int secondsRemaining = (int)(Config.SlotMachineCommandCooldown - cooldownRemaining);
                info.ReplyToCommand(Config.Tag + Localizer["In cooldown", secondsRemaining]);
                return;
            }
        }

        _playerLastSlotMachineCommandTimes[player.SteamID.ToString()] = DateTime.Now;

        if (!int.TryParse(info.GetArg(1), out int betAmount))
        {
            info.ReplyToCommand(Config.Tag + Localizer["Invalid amount of credits"]);
            return;
        }

        if (betAmount < Config.MinBet)
        {
            info.ReplyToCommand(Config.Tag + Localizer["Minimum bet amount", Config.MinBet]);
            return;
        }

        if (betAmount > Config.MaxBet)
        {
            info.ReplyToCommand(Config.Tag + Localizer["Maximum bet amount", Config.MaxBet]);
            return;
        }

        if (StoreApi.GetPlayerCredits(player) < betAmount)
        {
            info.ReplyToCommand(Config.Tag + Localizer["Not enough credits"]);
            return;
        }

        StartSlotGame(player, betAmount);
    }

    private void StartSlotGame(CCSPlayerController player, int betAmount)
    {
        StoreApi!.GivePlayerCredits(player, -betAmount);

        List<string> results = [];
        for (int i = 0; i < 3; i++)
        {
            string symbol = GetRandomSymbol();
            results.Add(symbol);
        }

        SlotMachineGame game = new(player, betAmount, results);
        _activeGames[player.SteamID.ToString()] = game;

        game.IsInProgress = true;

        AddTimer(Config.SlotTimers.FirstStop, () => StopSlot(game, 0));
        AddTimer(Config.SlotTimers.SecondStop, () => StopSlot(game, 1));
        AddTimer(Config.SlotTimers.ThirdStop, () => StopSlot(game, 2));
    }

    private void StopSlot(SlotMachineGame game, int slotIndex)
    {
        if (!game.IsInProgress) return;

        game.StoppedSlots[slotIndex] = true;

        if (game.StoppedSlots.All(stopped => stopped))
        {
            EndSlotGame(game);
        }
    }

    private void EndSlotGame(SlotMachineGame game)
    {
        if (!game.IsInProgress) return;

        string resultString = string.Join(" ", game.Results);
        int multiplier = CalculateMultiplier(game.Results);
        int winnings = game.BetAmount * multiplier;

        if (multiplier == 1)
        {
            winnings = game.BetAmount * Config.PartialWinPercentage / 100;
        }

        StoreApi!.GivePlayerCredits(game.Player, winnings);

        game.Player.PrintToChat(Config.Tag + Localizer["Result", resultString]);
        switch (multiplier)
        {
            case > 1:
                game.Player.PrintToChat(Config.Tag + Localizer["Winnings", winnings]);
                break;
            case 1:
                game.Player.PrintToChat(Config.Tag + Localizer["2 Symbols", winnings]);
                break;
            default:
                game.Player.PrintToChat(Config.Tag + Localizer["You lost your bet", game.BetAmount]);
                break;
        }

        game.Player.PrintToCenter(Localizer["Result", resultString]);

        _activeGames.TryRemove(game.Player.SteamID.ToString(), out _);

        game.IsInProgress = false;
    }

    private int CalculateMultiplier(List<string> results)
    {
        if (results.All(s => s == results[0]))
        {
            return Config.RewardMultipliers[results[0]].Multiplier;
        }

        if (Config.SequentialSymbolsOnly)
        {
            if (results[0] == results[1] || results[1] == results[2])
            {
                return 1;
            }
        }
        else
        {
            if (results[0] == results[1] || results[1] == results[2] || results[0] == results[2])
            {
                return 1;
            }
        }

        return 0;
    }

    private string GetRandomSymbol()
    {
        double totalChance = Config.RewardMultipliers.Sum(kv => kv.Value.Chance);
        double randomNumber = _random.NextDouble() * totalChance;

        foreach (var symbol in Config.RewardMultipliers)
        {
            if (randomNumber < symbol.Value.Chance)
            {
                return symbol.Key;
            }
            randomNumber -= symbol.Value.Chance;
        }

        return Config.RewardMultipliers.Last().Key;
    }

    private void OnTick()
    {
        foreach (SlotMachineGame game in _activeGames.Values.ToList().Where(game => game.IsInProgress))
        {
            for (int i = 0; i < game.Results.Count; i++)
            {
                if (!game.StoppedSlots[i])
                {
                    game.Results[i] = GetRandomSymbol();
                }
            }

            string resultString = string.Join(" ", game.Results);
            game.Player.PrintToCenter(resultString);
        }
    }
}

public class SlotMachineGame(CCSPlayerController player, int betAmount, List<string> results)
{
    public CCSPlayerController Player { get; set; } = player;
    public int BetAmount { get; set; } = betAmount;
    public List<string> Results { get; set; } = results;
    public bool IsInProgress { get; set; } = false;
    public bool[] StoppedSlots { get; set; } = new bool[results.Count];
}