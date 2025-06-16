using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Utils;
using StoreApi;
using System.Text.Json.Serialization;

namespace Store_AdBonus;

public class Store_AdBonusConfig : BasePluginConfig
{
    [JsonPropertyName("tag")]
    public string Tag { get; set; } = "{red}[Store] ";

    [JsonPropertyName("ad_texts")]
    public List<string> AdTexts { get; set; } = ["YourAd1", "YourAd2"];

    [JsonPropertyName("bonus_credits")]
    public int BonusCredits { get; set; } = 100;

    [JsonPropertyName("interval_in_seconds")]
    public int IntervalSeconds { get; set; } = 300;

    [JsonPropertyName("show_ad_message")]
    public bool ShowAdMessage { get; set; } = true;

    [JsonPropertyName("ad_message_delay_seconds")]
    public int AdMessageDelaySeconds { get; set; } = 120;

    [JsonPropertyName("ad_message")]
    public string AdMessage { get; set; } = "Add '{blue}YourAd{white}' to your nickname and earn bonus credits!";

    [JsonPropertyName("show_ad_message_to_non_advertisers_only")]
    public bool ShowAdMessageToNonAdvertisersOnly { get; set; } = true;

}

public class Store_AdBonus : BasePlugin, IPluginConfig<Store_AdBonusConfig>
{
    public override string ModuleName => "Store Module [Name Bonus]";
    public override string ModuleVersion => "0.1.0";
    public override string ModuleAuthor => "Nathy";

    private IStoreApi? storeApi;
    private float intervalInSeconds;
    public Store_AdBonusConfig Config { get; set; } = null!;

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        storeApi = IStoreApi.Capability.Get();

        if (storeApi == null)
        {
            return;
        }

        intervalInSeconds = Config.IntervalSeconds;
        StartCreditTimer();

        if (Config.ShowAdMessage)
        {
            StartAdMessageTimer();
        }
    }

    public void OnConfigParsed(Store_AdBonusConfig config)
    {
        config.Tag = config.Tag.ReplaceColorTags();
        Config = config;
    }

    private void StartCreditTimer()
    {
        AddTimer(intervalInSeconds, () =>
        {
            GrantCreditsToEligiblePlayers();
            StartCreditTimer();
        });
    }

    private void GrantCreditsToEligiblePlayers()
    {
        List<CCSPlayerController> players = Utilities.GetPlayers();

        foreach (CCSPlayerController player in players)
        {
            if (player != null && !player.IsBot && player.IsValid)
            {
                foreach (string adText in Config.AdTexts)
                {
                    if (player.PlayerName.Contains(adText))
                    {
                        storeApi?.GivePlayerCredits(player, Config.BonusCredits);
                        player.PrintToChat(Config.Tag + Localizer["You have been awarded", Config.BonusCredits, adText]);
                        break;
                    }
                }
            }
        }
    }

    private void StartAdMessageTimer()
    {
        AddTimer(Config.AdMessageDelaySeconds, () =>
        {
            BroadcastAdMessage();
            StartAdMessageTimer();
        });
    }

    private void BroadcastAdMessage()
    {
        string message = Config.AdMessage.ReplaceColorTags();
        List<CCSPlayerController> players = Utilities.GetPlayers();

        foreach (CCSPlayerController player in players)
        {
            if (player != null && !player.IsBot && player.IsValid)
            {
                bool hasAdText = false;

                foreach (string adText in Config.AdTexts)
                {
                    if (player.PlayerName.Contains(adText))
                    {
                        hasAdText = true;
                        break;
                    }
                }

                if (!Config.ShowAdMessageToNonAdvertisersOnly || !hasAdText)
                {
                    player.PrintToChat(Config.Tag + message);
                }
            }
        }
    }
}
