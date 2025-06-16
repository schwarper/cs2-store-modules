using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using StoreApi;
using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core.Translations;

namespace Store_Crash;

public class StoreCrashConfig : BasePluginConfig
{
    [JsonPropertyName("tag")]
    public string Tag { get; set; } = "{red}[Store] ";
    
    [JsonPropertyName("min_bet")]
    public int MinBet { get; set; } = 10;

    [JsonPropertyName("max_bet")]
    public int MaxBet { get; set; } = 1000;

    [JsonPropertyName("min_multiplier")]
    public float MinMultiplier { get; set; } = 1.1f;

    [JsonPropertyName("max_multiplier")]
    public float MaxMultiplier { get; set; } = 9.9f;

    [JsonPropertyName("multiplier_increment")]
    public float MultiplierIncrement { get; set; } = 0.01f;

    [JsonPropertyName("crash_commands")]
    public List<string> CrashCommands { get; set; } = ["crash"];

    [JsonPropertyName("crash_command_cooldown")]
    public int CrashCommandCooldown { get; set; } = 10;

    [JsonPropertyName("multiplier_ranges")]
    public List<MultiplierRange> MultiplierRanges { get; set; } =
    [
        new() { Start = 1.0f, End = 2.0f, Chance = 55 },
        new() { Start = 2.0f, End = 3.0f, Chance = 25 },
        new() { Start = 3.0f, End = 4.0f, Chance = 10 },
        new() { Start = 4.0f, End = 5.0f, Chance = 7 },
        new() { Start = 5.0f, End = 15.0f, Chance = 3 }
    ];
}

public class MultiplierRange
{
    [JsonPropertyName("start")]
    public float Start { get; set; }

    [JsonPropertyName("end")]
    public float End { get; set; }

    [JsonPropertyName("chance")]
    public int Chance { get; set; }
}

public class CrashGame(CCSPlayerController player, int betCredits, float targetMultiplier, float crashMultiplier)
{
    public CCSPlayerController Player { get; set; } = player;
    public int BetCredits { get; set; } = betCredits;
    public float TargetMultiplier { get; set; } = targetMultiplier;
    public float CurrentMultiplier { get; set; }
    public bool IsActive { get; set; } = true;
    public float CrashMultiplier { get; set; } = crashMultiplier;
}

public class StoreCrash : BasePlugin, IPluginConfig<StoreCrashConfig>
{
    public override string ModuleName => "Store Module [Crash]";
    public override string ModuleVersion => "0.2.0";
    public override string ModuleAuthor => "Nathy";

    private readonly Random _random = new();
    private IStoreApi? StoreApi { get; set; }
    public StoreCrashConfig Config { get; set; } = new();
    private readonly ConcurrentDictionary<string, CrashGame> _activeGames = new();
    private readonly ConcurrentDictionary<string, DateTime> _playerLastCrashCommandTimes = new();

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        StoreApi = IStoreApi.Capability.Get() ?? throw new Exception("StoreApi could not be located.");
        CreateCommands();
        RegisterListener<Listeners.OnTick>(OnTick);
    }

    public void OnConfigParsed(StoreCrashConfig config)
    {
        config.Tag = config.Tag.ReplaceColorTags();
        
        config.MinBet = Math.Max(0, config.MinBet);
        config.MaxBet = Math.Max(config.MinBet + 1, config.MaxBet);

        Config = config;
    }

    private void CreateCommands()
    {
        foreach (string cmd in Config.CrashCommands)
        {
            AddCommand($"css_{cmd}", "Start a crash bet", Command_Crash);
        }
    }

    [CommandHelper(minArgs: 2, usage: "<credits> <multiplier>")]
    private void Command_Crash(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null) return;

        if (StoreApi == null) throw new Exception("StoreApi could not be located.");

        if (_playerLastCrashCommandTimes.TryGetValue(player.SteamID.ToString(), out DateTime lastCommandTime))
        {
            double cooldownRemaining = (DateTime.Now - lastCommandTime).TotalSeconds;
            if (cooldownRemaining < Config.CrashCommandCooldown)
            {
                int secondsRemaining = (int)(Config.CrashCommandCooldown - cooldownRemaining);
                info.ReplyToCommand(Config.Tag + Localizer["In cooldown", secondsRemaining]);
                return;
            }
        }

        _playerLastCrashCommandTimes[player.SteamID.ToString()] = DateTime.Now;

        if (!int.TryParse(info.GetArg(1), out int credits))
        {
            info.ReplyToCommand(Config.Tag + Localizer["Invalid amount of credits"]);
            return;
        }

        if (!float.TryParse(info.GetArg(2), out float targetMultiplier))
        {
            info.ReplyToCommand(Config.Tag + Localizer["Invalid multiplier"]);
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

        if (targetMultiplier < Config.MinMultiplier || targetMultiplier > Config.MaxMultiplier)
        {
            info.ReplyToCommand(Config.Tag + Localizer["Multiplier range", Config.MinMultiplier, Config.MaxMultiplier]);
            return;
        }

        if (StoreApi.GetPlayerCredits(player) < credits)
        {
            info.ReplyToCommand(Config.Tag + Localizer["Not enough credits"]);
            return;
        }

        float crashMultiplier = SimulateCrashMultiplier();
        StartCrashGame(player, credits, targetMultiplier, crashMultiplier);
    }

    private void StartCrashGame(CCSPlayerController player, int credits, float targetMultiplier, float crashMultiplier)
    {
        StoreApi!.GivePlayerCredits(player, -credits);
        player.PrintToChat(Config.Tag + Localizer["Bet placed", credits, targetMultiplier]);

        CrashGame game = new(player, credits, targetMultiplier, crashMultiplier);
        _activeGames[player.SteamID.ToString()] = game;
    }

    private void OnTick()
    {
        foreach (CrashGame game in _activeGames.Values.ToList().Where(game => game.IsActive))
        {
            game.CurrentMultiplier += Config.MultiplierIncrement;

            game.Player.PrintToCenter(Localizer["Current multiplier"] + $"{game.CurrentMultiplier:0.00}");

            if (game.CurrentMultiplier >= game.CrashMultiplier)
            {
                EndCrashGame(game);
            }
        }
    }

    private void EndCrashGame(CrashGame game)
    {
        float actualMultiplier = game.CurrentMultiplier;
        float targetMultiplier = game.TargetMultiplier;

        game.Player.PrintToCenter(Localizer["Multiplier crashed"] + $"{actualMultiplier:0.00}");

        if (actualMultiplier >= targetMultiplier)
        {
            int winnings = (int)(game.BetCredits * targetMultiplier);
            StoreApi!.GivePlayerCredits(game.Player, winnings);
            game.Player.PrintToChat(Config.Tag + Localizer["Bet win", winnings.ToString(), targetMultiplier.ToString("0.00"), actualMultiplier.ToString("0.00")]);
        }
        else
        {
            game.Player.PrintToChat(Config.Tag + Localizer["Bet lost", actualMultiplier.ToString("0.00"), targetMultiplier.ToString("0.00")]);
        }

        game.IsActive = false;
        _activeGames.TryRemove(game.Player.SteamID.ToString(), out _);
    }

    private float SimulateCrashMultiplier()
    {
        int randomNumber = _random.Next(1, 101);
        int accumulatedChance = 0;

        foreach (MultiplierRange range in Config.MultiplierRanges)
        {
            accumulatedChance += range.Chance;

            if (randomNumber <= accumulatedChance)
            {
                return (float)Math.Round(range.Start + _random.NextDouble() * (range.End - range.Start), 2);
            }
        }

        return Config.MultiplierRanges.Last().End;
    }
}
