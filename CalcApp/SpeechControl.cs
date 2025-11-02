using System;
using System.Globalization;
using System.Linq;
using System.Speech.Recognition;
using System.Windows;

namespace CalcApp
{
    public class SpeechControl : IDisposable
    {
        private SpeechRecognitionEngine? _sr;
        private readonly MainWindow _window;

        public SpeechControl(MainWindow window)
        {
            _window = window;
            InitSpeech();
        }

        private void InitSpeech()
        {
            try
            {
                var culture = new CultureInfo("hu-HU");
                var recognizerInfo = SpeechRecognitionEngine.InstalledRecognizers()
                    .FirstOrDefault(r => r.Culture.Equals(culture));

                if (recognizerInfo == null)
                {
                    System.Diagnostics.Debug.WriteLine("Nincs telepítve magyar (hu-HU) speech recognizer.");
                    return;
                }

                _sr = new SpeechRecognitionEngine(recognizerInfo);

                var numbers = new Choices("nulla","egy","kettő","három","négy","öt","hat","hét","nyolc","kilenc");
                var ops = new Choices("plusz","mínusz","szor","oszt","egyenlő","pont","törlés","vissza");

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
                System.Diagnostics.Debug.WriteLine($"Speech init error: {ex}");
            }
        }

        private void OnSpeechRecognized(object? sender, SpeechRecognizedEventArgs e)
        {
            if (e.Result.Confidence < 0.65) return;
            var text = e.Result.Text.ToLowerInvariant();

            _window.Dispatcher.Invoke(() =>
            {
                switch (text)
                {
                    case "nulla": _window.ProcessDigit("0"); break;
                    case "egy": _window.ProcessDigit("1"); break;
                    case "kettő": _window.ProcessDigit("2"); break;
                    case "három": _window.ProcessDigit("3"); break;
                    case "négy": _window.ProcessDigit("4"); break;
                    case "öt": _window.ProcessDigit("5"); break;
                    case "hat": _window.ProcessDigit("6"); break;
                    case "hét": _window.ProcessDigit("7"); break;
                    case "nyolc": _window.ProcessDigit("8"); break;
                    case "kilenc": _window.ProcessDigit("9"); break;

                    case "plusz": _window.ProcessOperator("+"); break;
                    case "mínusz": _window.ProcessOperator("-"); break;
                    case "szor": _window.ProcessOperator("*"); break;
                    case "oszt": _window.ProcessOperator("/"); break;
                    case "egyenlő": _window.ProcessEquals(); break;
                    case "pont": _window.ProcessDecimal(); break;
                    case "törlés": _window.ResetCalculatorState(); break;
                    case "vissza": _window.ProcessDelete(); break;
                }
            });
        }

        public void Dispose()
        {
            try { _sr?.RecognizeAsyncCancel(); _sr?.Dispose(); } catch { }
        }
    }
}