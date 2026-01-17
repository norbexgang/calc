using Xunit;
using CalcApp.ViewModels;

namespace CalcApp.Tests;

public class BugReproductionTests
{
    [Fact]
    public void NegativeSign_ShouldBeTreatedAsZero_InCalculations()
    {
        // Arrange
        var viewModel = new CalculatorViewModel();

        // Input: "5" -> "+/-" (Display "-5") -> "Backspace" (Display "-")
        viewModel.DigitCommand.Execute("5");
        viewModel.SignCommand.Execute(null);
        viewModel.DeleteCommand.Execute(null);

        // After my fix, it should be "0" instead of "-"
        Assert.Equal("0", viewModel.Display);

        // Act
        viewModel.OperatorCommand.Execute("+");

        viewModel.DigitCommand.Execute("5");

        // Assert
        Assert.Equal("5", viewModel.Display);

        viewModel.EqualsCommand.Execute(null);

        Assert.Equal("5", viewModel.Display);
    }

    [Fact]
    public void OperatorSwitch_ShouldNotExecutePreviousOperation()
    {
        var viewModel = new CalculatorViewModel();
        viewModel.DigitCommand.Execute("5");
        viewModel.OperatorCommand.Execute("+"); // _leftOperand = 5, _pendingOperator = "+"
        viewModel.OperatorCommand.Execute("-"); // Should only change _pendingOperator to "-"

        viewModel.DigitCommand.Execute("3");
        viewModel.EqualsCommand.Execute(null);

        // If bug exists, it might have calculated 5+5=10 then 10-3=7
        // Or if it just switches correctly: 5-3=2
        Assert.Equal("2", viewModel.Display);
    }

    [Fact]
    public void DeleteCommand_ShouldClearError()
    {
        var viewModel = new CalculatorViewModel();
        viewModel.DigitCommand.Execute("1");
        viewModel.OperatorCommand.Execute("/");
        viewModel.DigitCommand.Execute("0");
        viewModel.EqualsCommand.Execute(null);

        Assert.Equal("Error", viewModel.Display);

        viewModel.DeleteCommand.Execute(null);
        Assert.Equal("0", viewModel.Display);
    }

    [Fact]
    public void DeleteCommand_ShouldNotLeaveMinusSign()
    {
        var viewModel = new CalculatorViewModel();
        viewModel.DigitCommand.Execute("5");
        viewModel.SignCommand.Execute(null); // "-5"
        viewModel.DeleteCommand.Execute(null); // "-"
        
        // This is where it currently stands. 
        // If I delete again, it should become "0".
        viewModel.DeleteCommand.Execute(null); 
        Assert.Equal("0", viewModel.Display);
    }

    [Fact]
    public void LeadingZero_Input_ShouldBeHandledCorrectly()
    {
        var viewModel = new CalculatorViewModel();
        viewModel.DigitCommand.Execute("0");
        viewModel.DigitCommand.Execute("0");
        Assert.Equal("0", viewModel.Display);
        
        viewModel.DigitCommand.Execute("5");
        Assert.Equal("5", viewModel.Display);
    }

    [Fact]
    public void Percent_WithoutOperator_ShouldDivideBy100()
    {
        var viewModel = new CalculatorViewModel();
        viewModel.DigitCommand.Execute("5");
        viewModel.DigitCommand.Execute("0");
        viewModel.PercentCommand.Execute(null);

        Assert.Equal("0.5", viewModel.Display);
    }

    [Fact]
    public void Percent_WithMultiplication_ShouldDivideBy100()
    {
        var viewModel = new CalculatorViewModel();
        viewModel.DigitCommand.Execute("2");
        viewModel.OperatorCommand.Execute("*");
        viewModel.DigitCommand.Execute("5");
        viewModel.DigitCommand.Execute("0");
        viewModel.PercentCommand.Execute(null); // Should be 0.5

        viewModel.EqualsCommand.Execute(null); // 2 * 0.5 = 1
        Assert.Equal("1", viewModel.Display);
    }

    [Fact]
    public void Precision_Test_FormatNumber()
    {
        var viewModel = new CalculatorViewModel();
        // 0.1 + 0.2 is often 0.30000000000000004 in double
        viewModel.DigitCommand.Execute("0");
        viewModel.DecimalCommand.Execute(null);
        viewModel.DigitCommand.Execute("1");
        viewModel.OperatorCommand.Execute("+");
        viewModel.DigitCommand.Execute("0");
        viewModel.DecimalCommand.Execute(null);
        viewModel.DigitCommand.Execute("2");
        viewModel.EqualsCommand.Execute(null);
        
        Assert.Equal("0.3", viewModel.Display);
    }

    [Fact]
    public void Factorial_170_ShouldNotOverflow()
    {
        var viewModel = new CalculatorViewModel();
        viewModel.DigitCommand.Execute("1");
        viewModel.DigitCommand.Execute("7");
        viewModel.DigitCommand.Execute("0");
        viewModel.FactorialCommand.Execute(null);
        
        Assert.NotEqual("Error", viewModel.Display);
    }

    [Fact]
    public void Factorial_171_ShouldOverflow()
    {
        var viewModel = new CalculatorViewModel();
        viewModel.DigitCommand.Execute("1");
        viewModel.DigitCommand.Execute("7");
        viewModel.DigitCommand.Execute("1");
        viewModel.FactorialCommand.Execute(null);
        
        Assert.Equal("Error", viewModel.Display);
    }

    [Fact]
    public void Decimal_AfterMinus_ShouldBeNegativeZeroPoint()
    {
        var viewModel = new CalculatorViewModel();
        viewModel.DigitCommand.Execute("5");
        viewModel.SignCommand.Execute(null); // "-5"
        viewModel.DeleteCommand.Execute(null); // "-"
        
        // This is a bit tricky now. In my latest version of ProcessDelete, 
        // it turns "-" into "0". So this test will actually test "0" -> "0."
        // Wait, I should check if I want to allow "-" state at all.
        // My previous fix in ProcessDelete:
        // if (Display.Length <= 1 || (Display.Length == 2 && Display.StartsWith('-'))) { Display = "0"; }
        // So "-5" -> Backspace -> "0". 
        // Thus "Display == "-"" is no longer possible via Delete.
        
        viewModel.DigitCommand.Execute("5");
        viewModel.SignCommand.Execute(null); // "-5"
        viewModel.DeleteCommand.Execute(null); // "0"
        viewModel.DecimalCommand.Execute(null); // "0."
        
        Assert.Equal("0.", viewModel.Display);
    }
}
