using Xunit;

namespace CalcApp.Tests;

public class CalculatorPropertyTests
{
    [Theory]
    [InlineData("5", "+", "3", "8")]
    [InlineData("10", "/", "2", "5")]
    [InlineData("2", "^", "3", "8")]
    [InlineData("7", "-", "9", "-2")]
    public void BasicOperations_WorkAsExpected(string left, string op, string right, string expected)
    {
        var vm = new ViewModels.CalculatorViewModel();
        vm.Display = left;
        vm.OperatorCommand.Execute(op);
        vm.Display = right;
        vm.EqualsCommand.Execute(null);

        Assert.Equal(expected, vm.Display);
    }
}
