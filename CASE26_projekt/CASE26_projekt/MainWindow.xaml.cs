using LLama;
using LLama.Common;
using System.Runtime.InteropServices;
using System.Windows;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Linq;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Input;
using System.Collections.Specialized;

namespace CASE26_projekt
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<Message> Messages { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            InitializeModel();

            Messages = new ObservableCollection<Message>();
            var systemMessage = new Message
            {
                Role = MessageRoleType.System,
                Content = "System message",
                Timestamp = System.DateTime.Now
            };
            Messages.Add(systemMessage);
            Messages.Add(new Message { Role = MessageRoleType.Assistant, Content = "Hello, how may I help you?", Timestamp = System.DateTime.Now });

            DataContext = this;

            chatMessages.ItemsSource = Messages;
            Messages.CollectionChanged += Messages_CollectionChanged;
        }

        private void Messages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            Dispatcher.InvokeAsync(() =>
            {
                try { chatScrollViewer.ScrollToBottom(); } catch { }
            });
        }

        private ChatSession? chatSession;
        private InteractiveExecutor? executor;
        private InferenceParams? inferenceParams;

        private void InitializeModel()
        {
            try
            {
                string modelPath = System.IO.Path.Combine(KnownFolders.GetPath(KnownFolder.Downloads), "mistral_7B_solarSystems.q4_k_m.gguf");
            
                var parameters = new ModelParams(modelPath)
                {
                    ContextSize =4096,
                    GpuLayerCount =80
                };
                var weights = LLamaWeights.LoadFromFile(parameters);
                var context = weights.CreateContext(parameters);
                executor = new InteractiveExecutor(context);


                var chatHistory = new ChatHistory();
                chatHistory.AddMessage(AuthorRole.System,
                "You are a strictly domain-limited assistant specialized ONLY in solar systems and residential solar power plants installation and maintenance.\n\n" +
                "CRITICAL RULES:\n" +
                "1. You MUST answer ONLY questions related to solar panels, photovoltaic systems, inverters, batteries, mounting systems, installation, maintenance, solar energy production and performance.\n" +
                "2. If the question is NOT related to solar systems in any way, you MUST refuse.\n" +
                "3. When refusing, respond EXACTLY with:\n" +
                "'I'm sorry, but I can only answer questions related to solar systems and photovoltaic power plants.'\n" +
                "4. Do NOT provide any additional information when refusing.\n" +
                "5. Do NOT attempt to reinterpret unrelated questions as solar-related.\n" +
                "6. If a question seems like it isn't related to the topic DO NOT ANSWEAR AND REFUSE AS INSTRUCTED");
                chatSession = new ChatSession(executor, chatHistory);
                inferenceParams = new InferenceParams() { MaxTokens =4096, AntiPrompts = new List<string> { "User:" } };
            }
            catch (System.Exception ex)
            {
                AppendText($"Error loading model: {ex.ToString()}\n");
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var view = System.Windows.Data.CollectionViewSource.GetDefaultView(Messages);
            if (view != null)
            {
                view.Filter = x => ((Message)x).Role != MessageRoleType.System;
                chatMessages.ItemsSource = view;
            }
        }

        private async void btnSend_Click(object sender, RoutedEventArgs e)
        {
            var message = txtMessage.Text.Trim();
            if (string.IsNullOrEmpty(message))
            {
                MessageBox.Show("Please enter a message.", "CHATBOT", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            var userMsg = new Message { Role = MessageRoleType.User, Content = message, Timestamp = System.DateTime.Now };
            Messages.Add(userMsg);
            
            txtMessage.Text = string.Empty;
            txtMessage.Focus();
            
            if (ContainsPredictionKeyword(message))
            {
                var span = ParseTimeSpanFromQuery(message);
                if (span == null)
                {
                    Messages.Add(new Message { Role = MessageRoleType.Assistant, Content = "I couldn't parse the time period from your request. Please specify e.g. 'next hour', '30 minutes', '2 hours'.", Timestamp = System.DateTime.Now });
                    return;
                }
                
                int intervals = Get15MinIntervalCount(span.Value);
                try
                {
                    var output = DailyEnergyPredictionModel.Predict(null, intervals);
                    if (output != null && output.Daily_energy_kWh_ != null && output.Daily_energy_kWh_.Length >0)
                    {
                        var preds = output.Daily_energy_kWh_;
                        var sb = new System.Text.StringBuilder();
                        sb.AppendLine($"Prediction for the next {span.Value}: \n");
                        for (int i =0; i < preds.Length; i++)
                        {
                            sb.AppendLine($"Interval {i +1}: {preds[i].ToString(CultureInfo.InvariantCulture)} kWh");
                        }
                        var avg = preds.Average();
                        sb.AppendLine($"\nAverage over 15 minute period: {avg.ToString(CultureInfo.InvariantCulture)} kWh");
                        
                        Messages.Add(new Message { Role = MessageRoleType.Assistant, Content = sb.ToString(), Timestamp = System.DateTime.Now });
                        return;
                    }
                    else
                    {
                        Messages.Add(new Message { Role = MessageRoleType.Assistant, Content = "Prediction not available (empty result).", Timestamp = System.DateTime.Now });
                        return;
                    }
                }
                catch (System.Exception ex)
                {
                    Messages.Add(new Message { Role = MessageRoleType.Assistant, Content = "Error executing prediction: " + ex.Message, Timestamp = System.DateTime.Now });
                    return;
                }
            }
            
            if (chatSession != null && inferenceParams != null)
            {
                var responseBuilder = new System.Text.StringBuilder();
                await foreach (var token in chatSession.ChatAsync(new ChatHistory.Message(AuthorRole.User, message), inferenceParams))
                {
                    responseBuilder.Append(token);
                }
                var response = responseBuilder.ToString();
                response = Regex.Replace(response, @"\s*User:\s*$", string.Empty, RegexOptions.IgnoreCase);
                Messages.Add(new Message { Role = MessageRoleType.Assistant, Content = response, Timestamp = System.DateTime.Now });
            }
            else
            {
                Messages.Add(new Message { Role = MessageRoleType.Assistant, Content = "Model not loaded.", Timestamp = System.DateTime.Now });
            }
        }

        private void txtMessage_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                e.Handled = true;
                btnSend_Click(sender, e);
            }
        }

        private void btnDeleteChat_Click(object sender, RoutedEventArgs e)
        {
            while (Messages.Count >2)
            {
                Messages.RemoveAt(2);
            }
        }

        private void AppendText(string text)
        {
            OutputTextBox.AppendText(text);
            OutputTextBox.ScrollToEnd();
        }

        private bool ContainsPredictionKeyword(string input)
        {
            return input.IndexOf("prediction", System.StringComparison.OrdinalIgnoreCase) >=0
                || input.IndexOf("predict", System.StringComparison.OrdinalIgnoreCase) >=0
                || input.IndexOf("forecast", System.StringComparison.OrdinalIgnoreCase) >=0;
        }

        private TimeSpan? ParseTimeSpanFromQuery(string input)
        {
            input = input.ToLowerInvariant();

            input = input.Replace("-", " ").Replace(",", " ");

            if (input.Contains("half hour") || input.Contains("half an hour") || input.Contains("next half hour"))
                return TimeSpan.FromMinutes(30);
            if (input.Contains("quarter hour") || input.Contains("next quarter hour") || input.Contains("quarter of an hour"))
                return TimeSpan.FromMinutes(15);

            var numberWords = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["one"] =1, ["two"] =2, ["three"] =3, ["four"] =4, ["five"] =5,
                ["six"] =6, ["seven"] =7, ["eight"] =8, ["nine"] =9, ["ten"] =10,
                ["couple"] =2, ["few"] =3
            };

            var explicitRegex = new Regex(@"(?<num>\d+)\s*(?<unit>minutes|minute|mins|min|hours|hour|hrs|hr|days|day|weeks|week)", RegexOptions.IgnoreCase);
            var m = explicitRegex.Match(input);
            if (m.Success)
            {
                if (!int.TryParse(m.Groups["num"].Value, out int num)) return null;
                var unit = m.Groups["unit"].Value;
                if (unit.StartsWith("min")) return TimeSpan.FromMinutes(num);
                if (unit.StartsWith("hr") || unit.StartsWith("hour") || unit.StartsWith("h")) return TimeSpan.FromHours(num);
                if (unit.StartsWith("day")) return TimeSpan.FromDays(num);
                if (unit.StartsWith("week")) return TimeSpan.FromDays(7 * num);
            }

            var nextRegex = new Regex(@"(?:next|in|for the next)\s+(?<num>\w+)?\s*(?<unit>minutes|minute|mins|min|hours|hour|hrs|hr|days|day|weeks|week)?", RegexOptions.IgnoreCase);
            m = nextRegex.Match(input);
            if (m.Success)
            {
                var numGroup = m.Groups["num"].Value;
                var unitGroup = m.Groups["unit"].Value;

                int numVal =1; 
                if (!string.IsNullOrEmpty(numGroup))
                {
                    if (!int.TryParse(numGroup, out numVal))
                    {
                        if (numberWords.TryGetValue(numGroup, out var w)) numVal = w;
                        else numVal = ParseNumberWord(numGroup) ??1;
                    }
                }

                if (string.IsNullOrEmpty(unitGroup))
                {
                    return TimeSpan.FromHours(numVal);
                }

                var unit = unitGroup;
                if (unit.StartsWith("min")) return TimeSpan.FromMinutes(numVal);
                if (unit.StartsWith("hr") || unit.StartsWith("hour") || unit.StartsWith("h")) return TimeSpan.FromHours(numVal);
                if (unit.StartsWith("day")) return TimeSpan.FromDays(numVal);
                if (unit.StartsWith("week")) return TimeSpan.FromDays(7 * numVal);
            }

            var inRegex = new Regex(@"in\s+(a|an|one|two|three|four|five|couple|few)\s*(minute|minutes|min|hour|hours|day|days|week|weeks)", RegexOptions.IgnoreCase);
            m = inRegex.Match(input);
            if (m.Success)
            {
                var numWord = m.Groups[1].Value;
                var unitWord = m.Groups[2].Value;
                int num =1;
                if (!int.TryParse(numWord, out num))
                {
                    if (numberWords.TryGetValue(numWord, out var w)) num = w;
                    else num = ParseNumberWord(numWord) ??1;
                }
                if (unitWord.StartsWith("min")) return TimeSpan.FromMinutes(num);
                if (unitWord.StartsWith("hour")) return TimeSpan.FromHours(num);
                if (unitWord.StartsWith("day")) return TimeSpan.FromDays(num);
                if (unitWord.StartsWith("week")) return TimeSpan.FromDays(7 * num);
            }

            if (input.Contains("next hour") || input.Contains("this hour") || input.Contains("the next hour") )
                return TimeSpan.FromHours(1);
            if (input.Contains("next day") || input.Contains("tomorrow"))
                return TimeSpan.FromDays(1);
            if (input.Contains("next week") || input.Contains("next7 days") || input.Contains("week"))
                return TimeSpan.FromDays(7);

            var shortMinRegex = new Regex(@"(?<num>\d+)\s*min\b", RegexOptions.IgnoreCase);
            m = shortMinRegex.Match(input);
            if (m.Success && int.TryParse(m.Groups["num"].Value, out int mins)) return TimeSpan.FromMinutes(mins);

            return null;
        }

        private int? ParseNumberWord(string word)
        {
            word = word.ToLowerInvariant();
            var map = new Dictionary<string, int>
            {
                ["one"] =1, ["two"] =2, ["three"] =3, ["four"] =4, ["five"] =5,
                ["six"] =6, ["seven"] =7, ["eight"] =8, ["nine"] =9, ["ten"] =10,
                ["eleven"] =11, ["twelve"] =12, ["thirteen"] =13, ["fourteen"] =14,
                ["fifteen"] =15, ["sixteen"] =16, ["seventeen"] =17, ["eighteen"] =18,
                ["nineteen"] =19, ["twenty"] = 20, ["twenty one"] = 21, ["twenty two"] = 22,
                ["twenty three"] = 23, ["twenty four"] = 24,
            };
            if (map.TryGetValue(word, out var v)) return v;
            return null;
        }

        private int Get15MinIntervalCount(TimeSpan span)
        {
            var intervals = (int)Math.Ceiling(span.TotalMinutes / 15.0);
            return Math.Max(1, intervals);
        }

        private void Button_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
        }
    }
}
