using Xunit;
using CalcApp.ViewModels;

namespace CalcApp.Tests;

public class CalculatorViewModelTests
{
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
}
