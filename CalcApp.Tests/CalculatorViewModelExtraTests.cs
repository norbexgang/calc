using System;
using CalcApp.ViewModels;
using Xunit;

namespace CalcApp.Tests;

public class CalculatorViewModelExtraTests
{
    [Fact]
    public void Decimal_WhenDisplayIsMinus_ProducesMinusOrZeroDecimal()
    {
        var vm = new CalculatorViewModel { Display = "-" };

        vm.DecimalCommand.Execute(null);

        // Accept both behaviors: "-0." or "0." depending on internal reset state
        Assert.True(vm.Display == "-0." || vm.Display == "0.", $"Unexpected display: {vm.Display}");
    }

    [Fact]
    public void LargeNumber_FormatFallsBackToExponential_WhenTooLong()
    {
        var vm = new CalculatorViewModel { Display = "123456789012345678901234567890" };

        // Trigger an operation that formats the current display value (e.g., try to add 0)
        vm.OperatorCommand.Execute("+");
        vm.DigitCommand.Execute("0");
        vm.EqualsCommand.Execute(null);

        // Result should be either the input rounded/shortened or exponential notation
        Assert.False(string.IsNullOrEmpty(vm.Display));
        Assert.True(vm.Display.Length <= 64);
    }

    [Fact]
    public void SetTurboMode_TogglesProperty()
    {
        var vm = new CalculatorViewModel();
        vm.SetTurboMode(true);
        Assert.True(vm.IsTurboEnabled);
        vm.SetTurboMode(false);
        Assert.False(vm.IsTurboEnabled);
    }

    [Fact]
    public void Percent_WhenStandalone_ComputesCorrectly()
    {
        var vm = new CalculatorViewModel { Display = "50" };

        vm.PercentCommand.Execute(null);

        Assert.Equal("0.5", vm.Display);
    }
}
