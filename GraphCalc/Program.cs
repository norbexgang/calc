using System;
using System.Windows.Forms;

namespace GraphCalc;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Magyar komment: DPI és vizuális beállítások alkalmazása a modern megjelenéshez
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        // Magyar komment: a fő űrlap elindítása
        Application.Run(new MainForm());
    }
}
