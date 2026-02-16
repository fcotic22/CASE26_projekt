using LLama;
using LLama.Common;
using System.Runtime.InteropServices;
using System.Windows;

namespace CASE26_projekt
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            InitializeModel();
        }

        private ChatSession? chatSession;
        private InteractiveExecutor? executor;
        private InferenceParams? inferenceParams;

        private void InitializeModel()
        {
            try
            {
                //string modelPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models", "tinyllama-merged.q4_k_m (4).gguf");
                string modelPath = System.IO.Path.Combine(KnownFolders.GetPath(KnownFolder.Downloads), "mistral_7B_solarSystems.q4_k_m.gguf");


                var parameters = new ModelParams(modelPath)
                {
                    ContextSize = 4096,
                    GpuLayerCount = 80
                };
                var weights = LLamaWeights.LoadFromFile(parameters);
                var context = weights.CreateContext(parameters);
                executor = new InteractiveExecutor(context);


                var chatHistory = new ChatHistory();
                chatHistory.AddMessage(AuthorRole.System,
                "You are a strictly domain-limited assistant specialized ONLY in solar systems and residential solar power plants installation and maintenance.\n\n" +
                "CRITICAL RULES:\n" +
                "1. You MUST answer ONLY questions related to solar panels, photovoltaic systems, inverters, batteries, mounting systems, installation, maintenance, solar energy production and performance.\n" +
                "2. If the question is NOT related to solar systems, you MUST refuse.\n" +
                "3. When refusing, respond EXACTLY with:\n" +
                "'I'm sorry, but I can only answer questions related to solar systems and photovoltaic power plants.'\n" +
                "4. Do NOT provide any additional information when refusing.\n" +
                "5. Do NOT attempt to reinterpret unrelated questions as solar-related.\n");
                chatHistory.AddMessage(AuthorRole.User, "Hello");
                chatHistory.AddMessage(AuthorRole.Assistant, "Hello, how may i help you?");

                chatSession = new ChatSession(executor, chatHistory);

                inferenceParams = new InferenceParams()
                {
                    MaxTokens = 1024,
                    AntiPrompts = new List<string> { "User:" }
                };
            }
            catch (Exception ex)
            {
                AppendText($"Greška kod učitavanja modela: {ex.ToString()}\n");
            }
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            if (chatSession == null || inferenceParams == null)
            {
                AppendText("Model još nije učitan.\n");
                return;
            }

            var input = InputTextBox.Text.Trim();
            if (string.IsNullOrEmpty(input)) return;
            else if (input.Contains("predickija"))
            {
                var output = DailyEnergyPredictionModel.Predict(); //new DailyEnergyPredictionModel.ModelInput { Daily_energy_kWh_ = 0F }, 10);
                if (output != null) 
                {
                    AppendText($"{input} + \n");
                    for(int i=0; i<10; i++) { AppendText($"{output.Daily_energy_kWh_.GetValue(i)} \n"); }
                }
            }

            AppendText($"{input}\n");
            InputTextBox.Clear();

            try
            {
                await foreach (var token in chatSession.ChatAsync(
                    new ChatHistory.Message(AuthorRole.User, input),
                    inferenceParams))
                {
                    Dispatcher.Invoke(() =>
                    {
                        OutputTextBox.AppendText(token);
                        OutputTextBox.ScrollToEnd();
                    });
                }
                OutputTextBox.AppendText("\n\n");
            }
            catch (Exception ex)
            {
                AppendText($"Greška tijekom generiranja: {ex.Message}\n");
            }
            AppendText("\n");
        }

        private void AppendText(string text)
        {
            OutputTextBox.AppendText(text);
            OutputTextBox.ScrollToEnd();
        }

        private void Button_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {

        }
    }
    public enum KnownFolder
    {
        Contacts,
        Downloads,
        Favorites,
        Links,
        SavedGames,
        SavedSearches
    }

    public static class KnownFolders
    {
        private static readonly Dictionary<KnownFolder, Guid> _guids = new()
        {
            [KnownFolder.Contacts] = new("56784854-C6CB-462B-8169-88E350ACB882"),
            [KnownFolder.Downloads] = new("374DE290-123F-4565-9164-39C4925E467B"),
            [KnownFolder.Favorites] = new("1777F761-68AD-4D8A-87BD-30B759FA33DD"),
            [KnownFolder.Links] = new("BFB9D5E0-C6A9-404C-B2B2-AE6DB6AF4968"),
            [KnownFolder.SavedGames] = new("4C5C32FF-BB9D-43B0-B5B4-2D72E54EAAA4"),
            [KnownFolder.SavedSearches] = new("7D1D3A04-DEBB-4115-95CF-2F29DA2920DA")
        };

        public static string GetPath(KnownFolder knownFolder)
        {
            return SHGetKnownFolderPath(_guids[knownFolder], 0);
        }

        [DllImport("shell32",
            CharSet = CharSet.Unicode, ExactSpelling = true, PreserveSig = false)]
        private static extern string SHGetKnownFolderPath(
            [MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags,
            nint hToken = 0);
    }
}
