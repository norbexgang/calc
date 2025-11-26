using System;
using System.Globalization;
using System.Linq;
using System.Speech.Recognition;
using System.Windows;
using Serilog;
using CalcApp.ViewModels;

namespace CalcApp
{
    /// <summary>
    /// A beszédvezérlést kezelő osztály.
    /// </summary>
    public class SpeechControl : IDisposable
    {
        private const string CultureCode = "hu-HU";
        private const float ConfidenceThreshold = 0.7f;
        private readonly CalculatorViewModel _viewModel;
        private SpeechRecognitionEngine? _sr;

        /// <summary>
        /// Inicializálja a SpeechControl új példányát.
        /// </summary>
        /// <param name="viewModel">A számológép nézetmodellje.</param>
        public SpeechControl(CalculatorViewModel viewModel)
        {
            _viewModel = viewModel;
            InitSpeech();
        }

        /// <summary>
        /// Inicializálja a beszédfelismerőt.
        /// </summary>
        private void InitSpeech()
        {
            try
            {
                var culture = new CultureInfo(CultureCode);
                var recognizerInfo = SpeechRecognitionEngine.InstalledRecognizers()
                    .FirstOrDefault(r => r.Culture.Equals(culture));

                if (recognizerInfo == null)
                {
                    Log.Warning("Nincs telepítve magyar ({CultureCode}) speech recognizer.", CultureCode);
                    return;
                }

                _sr = new SpeechRecognitionEngine(recognizerInfo);

                var numbers = new Choices(new string[] { "nulla", "egy", "kettő", "három", "négy", "öt", "hat", "hét", "nyolc", "kilenc" });
                var ops = new Choices(new string[] {
                    "plusz", "mínusz", "szor", "oszt", "egyenlő", "pont", "törlés", "vissza",
                    "szinusz", "koszinusz", "tangens", "gyök", "faktoriális",
                    "memória hozzáad", "memória kivon", "memória előhív", "memória törlés",
                    "napló"
                });

                var gb = new GrammarBuilder { Culture = culture };
                gb.Append(new Choices(numbers, ops));
                var grammar = new Grammar(gb);

                _sr.LoadGrammar(grammar);
                _sr.SetInputToDefaultAudioDevice();
                _sr.SpeechRecognized += OnSpeechRecognized;
                _sr.RecognizeAsync(RecognizeMode.Multiple);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Speech init error");
            }
        }

        /// <summary>
        /// A beszédfelismerés eseménykezelője.
        /// </summary>
        private void OnSpeechRecognized(object? sender, SpeechRecognizedEventArgs e)
        {
            if (e.Result.Confidence < ConfidenceThreshold) return;
            var text = e.Result.Text.ToLowerInvariant();

            Application.Current.Dispatcher.Invoke(() =>
            {
                switch (text)
                {
                    case "nulla": _viewModel.DigitCommand.Execute("0"); break;
                    case "egy": _viewModel.DigitCommand.Execute("1"); break;
                    case "kettő": _viewModel.DigitCommand.Execute("2"); break;
                    case "három": _viewModel.DigitCommand.Execute("3"); break;
                    case "négy": _viewModel.DigitCommand.Execute("4"); break;
                    case "öt": _viewModel.DigitCommand.Execute("5"); break;
                    case "hat": _viewModel.DigitCommand.Execute("6"); break;
                    case "hét": _viewModel.DigitCommand.Execute("7"); break;
                    case "nyolc": _viewModel.DigitCommand.Execute("8"); break;
                    case "kilenc": _viewModel.DigitCommand.Execute("9"); break;

                    case "plusz": _viewModel.OperatorCommand.Execute("+"); break;
                    case "mínusz": _viewModel.OperatorCommand.Execute("-"); break;
                    case "szor": _viewModel.OperatorCommand.Execute("*"); break;
                    case "oszt": _viewModel.OperatorCommand.Execute("/"); break;
                    case "egyenlő": _viewModel.EqualsCommand.Execute(null); break;
                    case "pont": _viewModel.DecimalCommand.Execute(null); break;
                    case "törlés": _viewModel.ClearCommand.Execute(null); break;
                    case "vissza": _viewModel.DeleteCommand.Execute(null); break;

                    case "szinusz": _viewModel.SinCommand.Execute(null); break;
                    case "koszinusz": _viewModel.CosCommand.Execute(null); break;
                    case "tangens": _viewModel.TanCommand.Execute(null); break;
                    case "gyök": _viewModel.SqrtCommand.Execute(null); break;
                    case "faktoriális": _viewModel.FactorialCommand.Execute(null); break;

                    case "memória hozzáad": _viewModel.MemoryAddCommand.Execute(null); break;
                    case "memória kivon": _viewModel.MemorySubtractCommand.Execute(null); break;
                    case "memória előhív": _viewModel.MemoryRecallCommand.Execute(null); break;
                    case "memória törlés": _viewModel.MemoryClearCommand.Execute(null); break;
                    case "napló": _viewModel.OpenLogsCommand.Execute(null); break;
                }
            });
        }

        /// <summary>
        /// Felszabadítja az erőforrásokat.
        /// </summary>
        public void Dispose()
        {
            try { _sr?.RecognizeAsyncCancel(); _sr?.Dispose(); } catch { }
        }
    }
}