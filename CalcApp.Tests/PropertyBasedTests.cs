using System;
using FsCheck;
using FsCheck.Xunit;
using Xunit;
using System.Globalization;
using CalcApp.ViewModels;

namespace CalcApp.Tests;

public class PropertyBasedTests
{
    private const double Tolerance = 1e-9;

    [Property(MaxTest = 200)]
    public void Addition_IsCommutative(int a, int b)
    {
        // Limit magnitude to avoid overflow and precision loss
        if (Math.Abs((long)a) > 1_000_000 || Math.Abs((long)b) > 1_000_000) return;

        var vm1 = new CalculatorViewModel();
        vm1.Display = a.ToString();
        vm1.OperatorCommand.Execute("+");
        vm1.Display = b.ToString();
        vm1.EqualsCommand.Execute(null);

        var vm2 = new CalculatorViewModel();
        vm2.Display = b.ToString();
        vm2.OperatorCommand.Execute("+");
        vm2.Display = a.ToString();
        vm2.EqualsCommand.Execute(null);

        Assert.True(double.TryParse(vm1.Display, out var r1));
        Assert.True(double.TryParse(vm2.Display, out var r2));
        Assert.Equal(r1, r2, precision: 12);
    }

    [Property(MaxTest = 200)]
    public void Multiplication_IsCommutative(int a, int b)
    {
        // Keep values small to avoid overflow
        if (Math.Abs((long)a) > 10000 || Math.Abs((long)b) > 10000) return;

        var vm1 = new CalculatorViewModel();
        vm1.Display = a.ToString();
        vm1.OperatorCommand.Execute("*");
        vm1.Display = b.ToString();
        vm1.EqualsCommand.Execute(null);

        var vm2 = new CalculatorViewModel();
        vm2.Display = b.ToString();
        vm2.OperatorCommand.Execute("*");
        vm2.Display = a.ToString();
        vm2.EqualsCommand.Execute(null);

        Assert.True(double.TryParse(vm1.Display, out var r1));
        Assert.True(double.TryParse(vm2.Display, out var r2));
        // allow relative tolerance
        Assert.InRange(Math.Abs(r1 - r2), 0, Math.Max(1e-9, Math.Abs(r1) * 1e-9));
    }

    [Property(MaxTest = 200)]
    public void Factorial_SmallIntegers_AreCorrect(int n)
    {
        if (n < 0 || n > 20) return; // only test small non-negative integers where double is exact

        var vm = new CalculatorViewModel { Display = n.ToString() };
        vm.FactorialCommand.Execute(null);

        Assert.True(double.TryParse(vm.Display, System.Globalization.NumberStyles.Float | System.Globalization.NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var result), $"Display not numeric: '{vm.Display}'");

        // compute expected
        double expected = 1;
        for (int i = 1; i <= n; i++) expected *= i;

        // Allow small relative error due to double rounding for larger factorials
        var diff = Math.Abs(expected - result);
        Assert.InRange(diff, 0, Math.Max(1e-6, Math.Abs(expected) * 1e-11));
    }

    [Property(MaxTest = 200)]
    public void Percent_WithPlus_UsesLeftOperandForPercentage(double left, double right)
    {
        // Keep values reasonable
        if (!double.IsFinite(left) || !double.IsFinite(right)) return;
        if (Math.Abs(left) > 1e6 || Math.Abs(right) > 1e6) return;

        var vm = new CalculatorViewModel { Display = left.ToString(CultureInfo.InvariantCulture) };
        vm.OperatorCommand.Execute("+");
        vm.Display = right.ToString(CultureInfo.InvariantCulture);

        vm.PercentCommand.Execute(null);

        Assert.True(double.TryParse(vm.Display, System.Globalization.NumberStyles.Float | System.Globalization.NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var computed), $"Display not a number: '{vm.Display}'");

        var expected = left * right / 100.0;

        Assert.InRange(Math.Abs(computed - expected), 0, Math.Max(1e-9, Math.Abs(expected) * 1e-9));
    }

    [Property(MaxTest = 200)]
    public void Memory_AddSubtract_AccumulatesCorrectly(int[] ops)
    {
        // interpret ops as sequence where positive numbers add, negative subtract
        var vm = new CalculatorViewModel();
        long expected = 0;

        foreach (var v in ops)
        {
            var val = Math.Abs((long)v % 1000); // keep magnitude small
            vm.Display = val.ToString();
            if (v >= 0)
            {
                vm.MemoryAddCommand.Execute(null);
                expected += val;
            }
            else
            {
                vm.MemorySubtractCommand.Execute(null);
                expected -= val;
            }
        }

        vm.MemoryRecallCommand.Execute(null);
        Assert.True(double.TryParse(vm.Display, out var recalled));
        Assert.Equal((double)expected, recalled, precision: 12);
    }
}