using System.Windows;
using System.Windows.Media.Animation;

namespace CalcApp;

public partial class App : Application
{
    public static readonly QuadraticEase EaseOut = new() { EasingMode = EasingMode.EaseOut };
    public static readonly QuadraticEase EaseIn = new() { EasingMode = EasingMode.EaseIn };
}
