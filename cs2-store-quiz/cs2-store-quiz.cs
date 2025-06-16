using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Commands;
using StoreApi;
using System.Text.Json.Serialization;

namespace Store_Quiz
{
    public class StoreQuizConfig : BasePluginConfig
    {
        [JsonPropertyName("tag")]
        public string Tag { get; set; } = "{red}[Store] ";

        [JsonPropertyName("question_interval_seconds")]
        public int QuestionIntervalSeconds { get; set; } = 30;

        [JsonPropertyName("questions")]
        public List<Question> Questions { get; set; } = [];
    }

    public abstract class Question
    {
        [JsonPropertyName("question")]
        public string QuestionText { get; set; } = string.Empty;

        [JsonPropertyName("answer")]
        public string Answer { get; set; } = string.Empty;

        [JsonPropertyName("credits")]
        public int Credits { get; set; } = 0;
    }

    public class StoreQuiz : BasePlugin, IPluginConfig<StoreQuizConfig>
    {
        public override string ModuleName { get; } = "Store Module [Quiz]";
        public override string ModuleVersion { get; } = "0.0.1";
        public override string ModuleAuthor => "Nathy";

        public StoreQuizConfig Config { get; set; } = null!;
        private Timer? _quizTimer;
        private int _currentQuestionIndex;
        private bool _questionAnswered;
        private IStoreApi? _storeApi;
        private readonly object _timerLock = new();

        public void OnConfigParsed(StoreQuizConfig config)
        {
            config.Tag = config.Tag.ReplaceColorTags();
            Config = config;
        }

        public override void OnAllPluginsLoaded(bool hotReload)
        {
            _storeApi = IStoreApi.Capability.Get();

            if (Config.Questions.Count == 0)
            {
                Console.WriteLine("No questions available in the configuration.");
                return;
            }

            _quizTimer = new Timer(AskQuestion, null, Timeout.Infinite, Timeout.Infinite);
            StartQuizTimer();

            AddCommandListener("say", OnPlayerChatAll);
            AddCommandListener("say_team", OnPlayerChatTeam);
        }

        private void StartQuizTimer()
        {
            lock (_timerLock)
            {
                _quizTimer?.Change(0, Timeout.Infinite);
            }
        }

        private void AskQuestion(object? state)
        {
            lock (_timerLock)
            {
                if (Config.Questions.Count == 0)
                {
                    Console.WriteLine("No questions available.");
                    return;
                }

                if (!_questionAnswered)
                {
                    MoveToNextQuestion();
                }

                _questionAnswered = false;
                Question question = Config.Questions[_currentQuestionIndex];

                Server.NextFrame(() =>
                {
                    Server.PrintToChatAll(Config.Tag + Localizer["Quiz.Question", question.QuestionText]);
                });

                _quizTimer?.Change(Config.QuestionIntervalSeconds * 1000, Timeout.Infinite);
            }
        }

        private HookResult OnPlayerChatAll(CCSPlayerController? player, CommandInfo message)
        {
            if (player == null)
            {
                return HookResult.Handled;
            }

            if (_questionAnswered)
                return HookResult.Continue;

            string answer = message.GetArg(1);
            Question currentQuestion = Config.Questions[_currentQuestionIndex];

            if (!answer.Equals(currentQuestion.Answer, StringComparison.OrdinalIgnoreCase))
                return HookResult.Continue;

            _questionAnswered = true;
            Server.PrintToChatAll(Config.Tag + Localizer["Quiz.AnsweredCorrectly", player.PlayerName]);

            if (_storeApi != null && currentQuestion.Credits > 0)
            {
                _storeApi.GivePlayerCredits(player, currentQuestion.Credits);
                Server.PrintToChatAll(Config.Tag + Localizer["Quiz.Awarded", player.PlayerName, currentQuestion.Credits]);
            }

            MoveToNextQuestion();
            return HookResult.Continue;
        }

        private HookResult OnPlayerChatTeam(CCSPlayerController? player, CommandInfo message)
        {
            if (player == null)
            {
                return HookResult.Handled;
            }

            if (!_questionAnswered)
            {
                string answer = message.GetArg(1);
                Question currentQuestion = Config.Questions[_currentQuestionIndex];

                if (!answer.Equals(currentQuestion.Answer, StringComparison.OrdinalIgnoreCase))
                    return HookResult.Continue;

                _questionAnswered = true;
                Server.PrintToChatAll(Config.Tag + Localizer["Quiz.AnsweredCorrectly", player.PlayerName]);

                if (_storeApi != null && currentQuestion.Credits > 0)
                {
                    _storeApi.GivePlayerCredits(player, currentQuestion.Credits);
                    Server.PrintToChatAll(Config.Tag + Localizer["Quiz.Awarded", player.PlayerName, currentQuestion.Credits]);
                }

                MoveToNextQuestion();
            }
            else
            {
                player.PrintToChat(Config.Tag + Localizer["Quiz.AlreadyAnswered"]);
            }
            return HookResult.Continue;
        }

        private void MoveToNextQuestion()
        {
            _currentQuestionIndex = (_currentQuestionIndex + 1) % Config.Questions.Count;
        }
    }
}