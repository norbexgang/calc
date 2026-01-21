using System;
using CalcApp.ViewModels;
using Xunit;

namespace CalcApp.Tests;

public class RelayCommandTests
{
    [Fact]
    public void Execute_CallsDelegate()
    {
        var called = false;
        var cmd = new RelayCommand(_ => called = true);

        cmd.Execute(null);

        Assert.True(called);
    }

    [Fact]
    public void CanExecute_ReturnsFalseWhenPredicateFalse()
    {
        var cmd = new RelayCommand(_ => { }, _ => false);

        var result = cmd.CanExecute(null);

        Assert.False(result);
    }

    [Fact]
    public void CanExecute_ReturnsTrueWhenNoPredicate()
    {
        var cmd = new RelayCommand(_ => { });

        var result = cmd.CanExecute(null);

        Assert.True(result);
    }
}
