using System.ComponentModel;
using CalcApp.ViewModels;
using Xunit;

namespace CalcApp.Tests;

public class BaseViewModelTests
{
    private sealed class TestViewModel : BaseViewModel
    {
        private int _value;
        public int Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }
    }

    [Fact]
    public void SetProperty_UpdatesAndRaisesPropertyChanged()
    {
        var vm = new TestViewModel();
        string? propertyName = null;
        vm.PropertyChanged += (sender, args) => propertyName = args.PropertyName;

        vm.Value = 5;

        Assert.Equal(5, vm.Value);
        Assert.Equal(nameof(TestViewModel.Value), propertyName);
    }

    [Fact]
    public void SetProperty_DoesNotRaiseWhenSameValue()
    {
        var vm = new TestViewModel { Value = 3 };
        var called = false;
        vm.PropertyChanged += (s, e) => called = true;

        vm.Value = 3;

        Assert.False(called);
    }
}
