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

        Assert.Equal("-", viewModel.Display);

        // Act
        viewModel.OperatorCommand.Execute("+");

        viewModel.DigitCommand.Execute("5");

        // Assert
        Assert.Equal("5", viewModel.Display);

        viewModel.EqualsCommand.Execute(null);

        Assert.Equal("5", viewModel.Display);
    }
}
