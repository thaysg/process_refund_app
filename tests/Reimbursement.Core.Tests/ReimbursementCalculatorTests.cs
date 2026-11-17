using FluentAssertions;
using Reimbursement.Core.Application;
using Reimbursement.Core.Domain;
using Xunit;

namespace Reimbursement.Core.Tests;

public class ReimbursementCalculatorTests
{
    private readonly ReimbursementCalculator _calculator = new(RefundPolicy.Default);

    [Theory]
    [InlineData(ProcedureType.ConsultaMedica, 100, 80)]
    [InlineData(ProcedureType.ExameDeImagem, 100, 90)]
    [InlineData(ProcedureType.ExameLaboratorial, 100, 70)]
    [InlineData(ProcedureType.Outros, 100, 50)]
    public void Calculate_AppliesCorrectPercentage(ProcedureType type, decimal paid, decimal expected)
    {
        _calculator.Calculate(paid, type).Should().Be(expected);
    }

    [Fact]
    public void Calculate_CapsAt500_JustBelow()
    {
        _calculator.Calculate(555m, ProcedureType.ExameDeImagem).Should().Be(499.50m);
    }

    [Fact]
    public void Calculate_CapsAt500_Exactly500()
    {
        _calculator.Calculate(1000m, ProcedureType.Outros).Should().Be(500.00m);
        _calculator.Calculate(625m, ProcedureType.ConsultaMedica).Should().Be(500.00m);
    }

    [Fact]
    public void Calculate_CapsAt500_JustAbove()
    {
        _calculator.Calculate(700m, ProcedureType.ConsultaMedica).Should().Be(500.00m);
        _calculator.Calculate(626m, ProcedureType.ConsultaMedica).Should().Be(500.00m);
    }

    [Fact]
    public void Calculate_RoundsToTwoDecimals_AwayFromZero()
    {
        _calculator.Calculate(10.05m, ProcedureType.ExameLaboratorial).Should().Be(7.04m);
        _calculator.Calculate(10.05m, ProcedureType.Outros).Should().Be(5.03m);
    }

    [Fact]
    public void Calculate_CapsAtPolicyLimit_ForHighPrecisionAmounts()
    {
        _calculator.Calculate(555.55m, ProcedureType.ExameDeImagem).Should().Be(500m);
        _calculator.Calculate(555.56m, ProcedureType.ExameDeImagem).Should().Be(500m);
    }

    [Fact]
    public void Calculate_SmallAmount_RoundsToCents()
    {
        _calculator.Calculate(0.01m, ProcedureType.ConsultaMedica).Should().Be(0.01m);
    }

    [Fact]
    public void Calculate_CustomPolicy_IsHonored()
    {
        var policy = RefundPolicy.Default with { MaxReimbursementPerProcedure = 100m };
        var calculator = new ReimbursementCalculator(policy);
        calculator.Calculate(500m, ProcedureType.ConsultaMedica).Should().Be(100m);
    }
}
