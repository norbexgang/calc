using System;
using System.Globalization;
using System.IO;
using CalcApp.ViewModels;
using Xunit;

namespace CalcApp.Tests;

/// <summary>
/// A CalculatorViewModel tesztjei.
/// </summary>
public class CalculatorViewModelTests
{
    private static double ParseDisplay(string displayText)
        => double.Parse(
            displayText,
            NumberStyles.Float | NumberStyles.AllowThousands,
            CultureInfo.InvariantCulture);

    /// <summary>
    /// Teszteli, hogy a százalék hozzáadása helyes eredményt ad-e.
    /// </summary>
    [Fact]
    public void AddPercentage_ShouldReturnCorrectResult()
    {
        // Arrange
        var viewModel = new CalculatorViewModel();

        // Act
        viewModel.DigitCommand.Execute("1");
        viewModel.DigitCommand.Execute("0");
        viewModel.DigitCommand.Execute("0");
        viewModel.OperatorCommand.Execute("+");
        viewModel.DigitCommand.Execute("5");
        viewModel.DigitCommand.Execute("0");
        viewModel.PercentCommand.Execute(null);
        viewModel.EqualsCommand.Execute(null);

        // Assert
        Assert.Equal("150", viewModel.Display);
    }

    /// <summary>
    /// A faktori�lis kezelje az �cszetlen�cl lebeg�cszajos �crt�cket eg�csk�cnt, ha el�g k�zel vannak.
    /// </summary>
    [Fact]
    public void Factorial_ShouldAllowNearlyWholeNumbers()
    {
        // Arrange
        var viewModel = new CalculatorViewModel
        {
            Display = "2.999999999"
        };

        // Act
        viewModel.FactorialCommand.Execute(null);

        // Assert
        Assert.Equal("6", viewModel.Display);
    }

    [Fact]
    public void MemoryCommands_ShouldTrackRecallAndClear()
    {
        var viewModel = new CalculatorViewModel();
        viewModel.Display = "10";
        viewModel.MemoryAddCommand.Execute(null);

        viewModel.Display = "3";
        viewModel.MemoryAddCommand.Execute(null);

        viewModel.Display = "1";
        viewModel.MemorySubtractCommand.Execute(null);

        viewModel.MemoryRecallCommand.Execute(null);
        Assert.Equal("12", viewModel.Display);

        viewModel.MemoryClearCommand.Execute(null);
        Assert.Single(viewModel.MemoryItems);
        Assert.Equal("Memory: 0", viewModel.MemoryItems[0]);
    }

    [Fact]
    public void SinCommand_TreatsInputAsDegrees()
    {
        var viewModel = new CalculatorViewModel
        {
            Display = "30"
        };

        viewModel.SinCommand.Execute(null);

        Assert.Equal("0.5", viewModel.Display);
    }

    [Fact]
    public void ToggleAngleMode_ShouldSwitchBetweenDegreesAndRadians()
    {
        var viewModel = new CalculatorViewModel();

        Assert.False(viewModel.IsRadiansMode);
        Assert.Equal("DEG", viewModel.AngleModeDisplay);

        viewModel.ToggleAngleModeCommand.Execute(null);

        Assert.True(viewModel.IsRadiansMode);
        Assert.Equal("RAD", viewModel.AngleModeDisplay);
    }

    [Fact]
    public void SinCommand_InRadiansMode_UsesRadians()
    {
        var viewModel = new CalculatorViewModel
        {
            Display = "1.5707963267948966"
        };

        viewModel.ToggleAngleModeCommand.Execute(null);
        viewModel.SinCommand.Execute(null);

        Assert.Equal("1", viewModel.Display);
    }

    [Fact]
    public void AsinCommand_InDegreesMode_ReturnsDegrees()
    {
        var viewModel = new CalculatorViewModel
        {
            Display = "0.5"
        };

        viewModel.AsinCommand.Execute(null);

        Assert.Equal("30", viewModel.Display);
    }

    [Fact]
    public void AsinCommand_InRadiansMode_ReturnsRadians()
    {
        var viewModel = new CalculatorViewModel
        {
            Display = "0.5"
        };

        viewModel.ToggleAngleModeCommand.Execute(null);
        viewModel.AsinCommand.Execute(null);

        Assert.InRange(Math.Abs(ParseDisplay(viewModel.Display) - Math.PI / 6.0), 0, 1e-10);
    }

    [Fact]
    public void AcosCommand_OutsideDomain_ShowsError()
    {
        var viewModel = new CalculatorViewModel
        {
            Display = "2"
        };

        viewModel.AcosCommand.Execute(null);

        Assert.Equal("Error", viewModel.Display);
    }

    [Fact]
    public void AtanCommand_InDegreesMode_ReturnsExpectedAngle()
    {
        var viewModel = new CalculatorViewModel
        {
            Display = "1"
        };

        viewModel.AtanCommand.Execute(null);

        Assert.Equal("45", viewModel.Display);
    }

    [Fact]
    public void LnCommand_OfEulerNumber_ReturnsOne()
    {
        var viewModel = new CalculatorViewModel
        {
            Display = "2.718281828459045"
        };

        viewModel.LnCommand.Execute(null);

        Assert.Equal("1", viewModel.Display);
    }

    [Fact]
    public void LnCommand_OfZero_ShowsError()
    {
        var viewModel = new CalculatorViewModel
        {
            Display = "0"
        };

        viewModel.LnCommand.Execute(null);

        Assert.Equal("Error", viewModel.Display);
    }

    [Fact]
    public void LogCommand_OfThousand_ReturnsThree()
    {
        var viewModel = new CalculatorViewModel
        {
            Display = "1000"
        };

        viewModel.LogCommand.Execute(null);

        Assert.Equal("3", viewModel.Display);
    }

    [Fact]
    public void ExpCommand_OfOne_ReturnsE()
    {
        var viewModel = new CalculatorViewModel
        {
            Display = "1"
        };

        viewModel.ExpCommand.Execute(null);

        Assert.InRange(Math.Abs(ParseDisplay(viewModel.Display) - Math.E), 0, 1e-10);
    }

    [Fact]
    public void Pow10Command_OfThree_ReturnsThousand()
    {
        var viewModel = new CalculatorViewModel
        {
            Display = "3"
        };

        viewModel.Pow10Command.Execute(null);

        Assert.Equal("1000", viewModel.Display);
    }

    [Fact]
    public void SquareCommand_HandlesNegativeInput()
    {
        var viewModel = new CalculatorViewModel
        {
            Display = "-12"
        };

        viewModel.SquareCommand.Execute(null);

        Assert.Equal("144", viewModel.Display);
    }

    [Fact]
    public void CubeCommand_HandlesNegativeInput()
    {
        var viewModel = new CalculatorViewModel
        {
            Display = "-3"
        };

        viewModel.CubeCommand.Execute(null);

        Assert.Equal("-27", viewModel.Display);
    }

    [Fact]
    public void CbrtCommand_HandlesNegativeInput()
    {
        var viewModel = new CalculatorViewModel
        {
            Display = "-27"
        };

        viewModel.CbrtCommand.Execute(null);

        Assert.Equal("-3", viewModel.Display);
    }

    [Fact]
    public void AbsCommand_ShouldReturnPositiveValue()
    {
        var viewModel = new CalculatorViewModel
        {
            Display = "-123.5"
        };

        viewModel.AbsCommand.Execute(null);

        Assert.Equal("123.5", viewModel.Display);
    }

    [Fact]
    public void ReciprocalCommand_OfZero_ShowsError()
    {
        var viewModel = new CalculatorViewModel
        {
            Display = "0"
        };

        viewModel.ReciprocalCommand.Execute(null);

        Assert.Equal("Error", viewModel.Display);
    }

    [Fact]
    public void PiCommand_SetsPiOnDisplay()
    {
        var viewModel = new CalculatorViewModel();

        viewModel.PiCommand.Execute(null);

        Assert.InRange(Math.Abs(ParseDisplay(viewModel.Display) - Math.PI), 0, 1e-10);
    }

    [Fact]
    public void PiCommand_ThenDigit_StartsNewInput()
    {
        var viewModel = new CalculatorViewModel();

        viewModel.PiCommand.Execute(null);
        viewModel.DigitCommand.Execute("2");

        Assert.Equal("2", viewModel.Display);
    }

    [Fact]
    public void EConstantCommand_SetsEOnDisplay()
    {
        var viewModel = new CalculatorViewModel();

        viewModel.EConstantCommand.Execute(null);

        Assert.InRange(Math.Abs(ParseDisplay(viewModel.Display) - Math.E), 0, 1e-10);
    }

    [Fact]
    public void EConstantCommand_CanBeUsedInOperation()
    {
        var viewModel = new CalculatorViewModel();

        viewModel.EConstantCommand.Execute(null);
        viewModel.OperatorCommand.Execute("+");
        viewModel.DigitCommand.Execute("1");
        viewModel.EqualsCommand.Execute(null);

        Assert.InRange(Math.Abs(ParseDisplay(viewModel.Display) - (Math.E + 1.0)), 0, 1e-10);
    }

    [Fact]
    public void ModCommand_ShouldCalculateRemainder()
    {
        var viewModel = new CalculatorViewModel
        {
            Display = "10"
        };

        viewModel.ModCommand.Execute(null);
        viewModel.Display = "3";
        viewModel.EqualsCommand.Execute(null);

        Assert.Equal("1", viewModel.Display);
    }

    [Fact]
    public void ModCommand_WithZeroRightOperand_ShowsError()
    {
        var viewModel = new CalculatorViewModel
        {
            Display = "10"
        };

        viewModel.ModCommand.Execute(null);
        viewModel.Display = "0";
        viewModel.EqualsCommand.Execute(null);

        Assert.Equal("Error", viewModel.Display);
    }

    [Fact]
    public void DivisionByZero_ShowsError()
    {
        var viewModel = new CalculatorViewModel();
        viewModel.DigitCommand.Execute("1");
        viewModel.OperatorCommand.Execute("/");
        viewModel.DigitCommand.Execute("0");
        viewModel.EqualsCommand.Execute(null);

        Assert.Equal("Error", viewModel.Display);
    }

    [Fact]
    public void EqualsRepetition_ReusesLastOperator()
    {
        var viewModel = new CalculatorViewModel();
        viewModel.DigitCommand.Execute("5");
        viewModel.OperatorCommand.Execute("+");
        viewModel.DigitCommand.Execute("3");
        viewModel.EqualsCommand.Execute(null);

        Assert.Equal("8", viewModel.Display);

        viewModel.EqualsCommand.Execute(null);

        Assert.Equal("11", viewModel.Display);
    }

    [Fact]
    public void OpenLogsCommand_PreparesDirectoryBeforeOpening()
    {
        var tempLogsRoot = Path.Combine(Path.GetTempPath(), "CalcAppTests", Guid.NewGuid().ToString());
        string? openedPath = null;
        var viewModel = new CalculatorViewModel(
            logPathProvider: () => tempLogsRoot,
            logDirectoryOpener: path => openedPath = path);

        try
        {
            viewModel.OpenLogsCommand.Execute(null);

            Assert.Equal(tempLogsRoot, openedPath);
            Assert.True(Directory.Exists(tempLogsRoot));
        }
        finally
        {
            if (Directory.Exists(tempLogsRoot))
            {
                Directory.Delete(tempLogsRoot, true);
            }

            var parent = Path.GetDirectoryName(tempLogsRoot);
            if (parent != null && Directory.Exists(parent) && Directory.GetFileSystemEntries(parent).Length == 0)
            {
                Directory.Delete(parent);
            }
        }
    }

    [Fact]
    public void DisplaySetter_RaisesPropertyChanged_WhenNoApplicationDispatcherIsAvailable()
    {
        var viewModel = new CalculatorViewModel();
        var raisedCount = 0;
        string? lastProperty = null;

        viewModel.PropertyChanged += (_, args) =>
        {
            raisedCount++;
            lastProperty = args.PropertyName;
        };

        viewModel.Display = "42";

        Assert.Equal(1, raisedCount);
        Assert.Equal(nameof(CalculatorViewModel.Display), lastProperty);
    }

    [Fact]
    public void OpenLogsCommand_UsesLocalAppDataPathByDefault()
    {
        string? openedPath = null;
        var expectedPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CalcApp",
            "logs");

        var viewModel = new CalculatorViewModel(
            logPathProvider: null,
            logDirectoryOpener: path => openedPath = path);

        viewModel.OpenLogsCommand.Execute(null);

        Assert.Equal(expectedPath, openedPath);
        Assert.True(Directory.Exists(expectedPath));
    }
}
