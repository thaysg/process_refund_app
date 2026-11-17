using FluentAssertions;
using Reimbursement.Core.Application;
using Reimbursement.Core.Domain;
using Xunit;

namespace Reimbursement.Core.Tests;

public class ReimbursementProcessorTests
{
    private readonly ReimbursementProcessor _processor = new();
    private static readonly DateOnly Ref = new(2024, 5, 1);

    private static ReimbursementRequest Req(int id, string type, string date, decimal paid, int client, int idx = -1)
    {
        ProcedureTypeParser.TryParse(type, out var t).Should().BeTrue();
        return new ReimbursementRequest(id, t, DateOnly.Parse(date), paid, client, idx == -1 ? id : idx);
    }

    [Fact]
    public void Process_SampleInput_ReturnsExpectedStatusesAndAmounts()
    {
        var requests = new List<ReimbursementRequest>
        {
            Req(1, "Consulta Médica", "2024-02-10", 400, 123, 0),
            Req(2, "Exame de Imagem", "2024-03-01", 600, 124, 1),
            Req(3, "Exame Laboratorial", "2024-04-15", 200, 125, 2),
            Req(4, "Outros", "2024-01-20", 150, 126, 3),
        };

        var results = _processor.Process(requests, Ref);

        results.Should().HaveCount(4);
        results[0].Should().BeEquivalentTo(new
        {
            Id = 1,
            ReimbursedAmount = 320m,
            Status = RequestStatus.Aprovado,
            ClientId = 123,
            Reason = DecisionReason.Approved
        }, o => o.ExcludingMissingMembers());
        results[1].Should().BeEquivalentTo(new
        {
            Id = 2,
            ReimbursedAmount = 500m,
            Status = RequestStatus.Aprovado,
            ClientId = 124,
            Reason = DecisionReason.Approved
        }, o => o.ExcludingMissingMembers());
        results[2].Should().BeEquivalentTo(new
        {
            Id = 3,
            ReimbursedAmount = 140m,
            Status = RequestStatus.Aprovado,
            ClientId = 125,
            Reason = DecisionReason.Approved
        }, o => o.ExcludingMissingMembers());
        results[3].Should().BeEquivalentTo(new
        {
            Id = 4,
            ReimbursedAmount = 75m,
            Status = RequestStatus.Rejeitado,
            ClientId = 126,
            Reason = DecisionReason.ProcedureTooOld
        }, o => o.ExcludingMissingMembers());
    }

    [Fact]
    public void Process_NinetyDayBoundary_Day90AcceptedAndDay91Rejected()
    {
        var requests = new List<ReimbursementRequest>
        {
            Req(1, "Outros", "2024-02-01", 100, 1, 0),
            Req(2, "Outros", "2024-01-31", 100, 1, 1),
        };

        var results = _processor.Process(requests, Ref);
        results.Single(r => r.Id == 1).Status.Should().Be(RequestStatus.Aprovado);
        results.Single(r => r.Id == 2).Status.Should().Be(RequestStatus.Rejeitado);
        results.Single(r => r.Id == 2).Reason.Should().Be(DecisionReason.ProcedureTooOld);
    }

    [Fact]
    public void Process_FutureDateRelativeToReference_IsRejectedAndRestProcessed()
    {
        var requests = new List<ReimbursementRequest>
        {
            Req(1, "Consulta Médica", "2024-05-02", 400, 123, 0),
            Req(2, "Consulta Médica", "2024-04-10", 400, 123, 1),
        };

        var results = _processor.Process(requests, Ref);

        results.Single(r => r.Id == 1).Status.Should().Be(RequestStatus.Rejeitado);
        results.Single(r => r.Id == 1).Reason.Should().Be(DecisionReason.ProcedureInFuture);
        results.Single(r => r.Id == 1).ReimbursedAmount.Should().Be(320m);
        results.Single(r => r.Id == 2).Status.Should().Be(RequestStatus.Aprovado);
        results.Single(r => r.Id == 2).ReimbursedAmount.Should().Be(320m);
    }

    [Fact]
    public void Process_CountLimit_FifthAcceptedAndSixthFlagged()
    {
        var refDate = new DateOnly(2024, 6, 1);
        var requests = Enumerable.Range(1, 6)
            .Select(i => Req(i, "Outros", "2024-05-20", 10, 42, i - 1))
            .ToList();

        var results = _processor.Process(requests, refDate);
        results.Take(5).Should().OnlyContain(r => r.Status == RequestStatus.Aprovado);
        results.Single(r => r.Id == 6).Status.Should().Be(RequestStatus.SuspeitoDeFraude);
        results.Single(r => r.Id == 6).Reason.Should().Be(DecisionReason.CustomerRequestLimitExceeded);
    }

    [Fact]
    public void Process_ThirtyDayWindowBoundary_Day30IncludedAndDay31Excluded()
    {
        var refDate = new DateOnly(2024, 6, 1);
        var requests = new List<ReimbursementRequest>
        {
            Req(1, "Outros", "2024-05-01", 10, 1, 0),
            Req(2, "Outros", "2024-05-10", 10, 1, 1),
            Req(3, "Outros", "2024-05-15", 10, 1, 2),
            Req(4, "Outros", "2024-05-20", 10, 1, 3),
            Req(5, "Outros", "2024-05-25", 10, 1, 4),
            Req(6, "Outros", "2024-05-31", 10, 1, 5),
        };
        var results = _processor.Process(requests, refDate);
        results.Single(r => r.Id == 6).Status.Should().Be(RequestStatus.SuspeitoDeFraude);

        var requests2 = new List<ReimbursementRequest>
        {
            Req(1, "Outros", "2024-04-30", 10, 2, 0),
            Req(2, "Outros", "2024-05-10", 10, 2, 1),
            Req(3, "Outros", "2024-05-15", 10, 2, 2),
            Req(4, "Outros", "2024-05-20", 10, 2, 3),
            Req(5, "Outros", "2024-05-25", 10, 2, 4),
            Req(6, "Outros", "2024-05-31", 10, 2, 5),
        };
        var results2 = _processor.Process(requests2, refDate);
        results2.Single(r => r.Id == 1).Status.Should().Be(RequestStatus.Aprovado);
        results2.Single(r => r.Id == 6).Status.Should().Be(RequestStatus.Aprovado);
    }

    [Fact]
    public void Process_TotalLimit_Exactly1500AllowedAnd1500_01Flagged()
    {
        var refDate = new DateOnly(2024, 6, 1);
        var requests = new List<ReimbursementRequest>
        {
            Req(1, "Outros", "2024-05-10", 1000, 1, 0),
            Req(2, "Outros", "2024-05-11", 1000, 1, 1),
            Req(3, "Outros", "2024-05-12", 1000, 1, 2),
            Req(4, "Outros", "2024-05-13", 0.02m, 1, 3),
        };
        var results = _processor.Process(requests, refDate);
        results.Single(r => r.Id == 1).Status.Should().Be(RequestStatus.Aprovado);
        results.Single(r => r.Id == 2).Status.Should().Be(RequestStatus.Aprovado);
        results.Single(r => r.Id == 3).Status.Should().Be(RequestStatus.Aprovado);
        results.Single(r => r.Id == 3).ReimbursedAmount.Should().Be(500m);
        results.Single(r => r.Id == 4).Status.Should().Be(RequestStatus.SuspeitoDeFraude);
        results.Single(r => r.Id == 4).Reason.Should().Be(DecisionReason.CustomerTotalLimitExceeded);
    }

    [Fact]
    public void Process_AmountExceededWithCountUnderLimit_IsFlagged()
    {
        var refDate = new DateOnly(2024, 6, 1);
        var requests = new List<ReimbursementRequest>
        {
            Req(1, "Outros", "2024-05-10", 1000, 1, 0),
            Req(2, "Outros", "2024-05-11", 1000, 1, 1),
            Req(3, "Outros", "2024-05-12", 1000, 1, 2),
            Req(4, "Outros", "2024-05-13", 2, 1, 3),
        };
        var results = _processor.Process(requests, refDate);
        results.Single(r => r.Id == 4).Status.Should().Be(RequestStatus.SuspeitoDeFraude);
        results.Single(r => r.Id == 4).Reason.Should().Be(DecisionReason.CustomerTotalLimitExceeded);
    }

    [Fact]
    public void Process_CountExceededWithAmountUnderLimit_IsFlagged()
    {
        var refDate = new DateOnly(2024, 6, 1);
        var requests = Enumerable.Range(1, 6)
            .Select(i => Req(i, "Outros", "2024-05-20", 10, 1, i - 1))
            .ToList();
        var results = _processor.Process(requests, refDate);
        results.Single(r => r.Id == 6).Status.Should().Be(RequestStatus.SuspeitoDeFraude);
        results.Single(r => r.Id == 6).Reason.Should().Be(DecisionReason.CustomerRequestLimitExceeded);
        results.Single(r => r.Id == 6).ReimbursedAmount.Should().Be(5m);
    }

    [Fact]
    public void Process_BothLimitsViolated_IsFlagged()
    {
        var refDate = new DateOnly(2024, 6, 1);
        var requests = new List<ReimbursementRequest>
        {
            Req(1, "Outros", "2024-05-10", 600, 1, 0),
            Req(2, "Outros", "2024-05-11", 600, 1, 1),
            Req(3, "Outros", "2024-05-12", 600, 1, 2),
            Req(4, "Outros", "2024-05-13", 600, 1, 3),
            Req(5, "Outros", "2024-05-14", 600, 1, 4),
            Req(6, "Outros", "2024-05-15", 10, 1, 5),
        };
        var results = _processor.Process(requests, refDate);
        results.Take(5).Should().OnlyContain(r => r.Status == RequestStatus.Aprovado);
        results.Single(r => r.Id == 6).Status.Should().Be(RequestStatus.SuspeitoDeFraude);
    }

    [Fact]
    public void Process_RejectedRequests_DoNotConsumeLimits()
    {
        var refDate = new DateOnly(2024, 6, 1);
        var requests = new List<ReimbursementRequest>
        {
            Req(1, "Outros", "2024-01-01", 1000, 1, 0),
            Req(2, "Outros", "2024-05-20", 1000, 1, 1),
            Req(3, "Outros", "2024-05-21", 1000, 1, 2),
            Req(4, "Outros", "2024-05-22", 1000, 1, 3),
        };
        var results = _processor.Process(requests, refDate);
        results.Single(r => r.Id == 1).Status.Should().Be(RequestStatus.Rejeitado);
        results.Single(r => r.Id == 4).Status.Should().Be(RequestStatus.Aprovado);
    }

    [Fact]
    public void Process_FlaggedRequests_DoNotConsumeLimits()
    {
        var refDate = new DateOnly(2024, 6, 1);
        var requests = new List<ReimbursementRequest>
        {
            Req(1, "Outros", "2024-05-10", 1000, 1, 0),
            Req(2, "Outros", "2024-05-11", 1000, 1, 1),
            Req(3, "Outros", "2024-05-12", 800, 1, 2),
            Req(4, "Outros", "2024-05-13", 1000, 1, 3),
            Req(5, "Outros", "2024-05-14", 200, 1, 4),
        };

        var results = _processor.Process(requests, refDate);

        results.Single(r => r.Id == 1).Status.Should().Be(RequestStatus.Aprovado);
        results.Single(r => r.Id == 1).ReimbursedAmount.Should().Be(500m);
        results.Single(r => r.Id == 2).Status.Should().Be(RequestStatus.Aprovado);
        results.Single(r => r.Id == 2).ReimbursedAmount.Should().Be(500m);
        results.Single(r => r.Id == 3).Status.Should().Be(RequestStatus.Aprovado);
        results.Single(r => r.Id == 3).ReimbursedAmount.Should().Be(400m);
        results.Single(r => r.Id == 4).Status.Should().Be(RequestStatus.SuspeitoDeFraude);
        results.Single(r => r.Id == 4).ReimbursedAmount.Should().Be(500m);
        results.Single(r => r.Id == 5).Status.Should().Be(RequestStatus.Aprovado);
        results.Single(r => r.Id == 5).ReimbursedAmount.Should().Be(100m);
    }

    [Fact]
    public void Process_DifferentClients_AreIsolated()
    {
        var refDate = new DateOnly(2024, 6, 1);
        var requests = new List<ReimbursementRequest>
        {
            Req(1, "Outros", "2024-05-10", 1000, 1, 0),
            Req(2, "Outros", "2024-05-11", 1000, 1, 1),
            Req(3, "Outros", "2024-05-12", 1000, 1, 2),
            Req(4, "Outros", "2024-05-10", 1000, 2, 3),
            Req(5, "Outros", "2024-05-11", 1000, 2, 4),
            Req(6, "Outros", "2024-05-12", 1000, 2, 5),
        };

        var results = _processor.Process(requests, refDate);

        results.Should().OnlyContain(r => r.Status == RequestStatus.Aprovado);
    }

    [Fact]
    public void Process_ShuffledInput_IsDeterministicAndPreservesInputOrder()
    {
        var refDate = new DateOnly(2024, 6, 1);
        var r1 = Req(1, "Outros", "2024-05-25", 10, 1, 0);
        var r2 = Req(2, "Outros", "2024-05-20", 10, 1, 1);
        var r3 = Req(3, "Outros", "2024-05-21", 10, 1, 2);
        var r4 = Req(4, "Outros", "2024-05-22", 10, 1, 3);
        var r5 = Req(5, "Outros", "2024-05-23", 10, 1, 4);
        var r6 = Req(6, "Outros", "2024-05-26", 10, 1, 5);
        var shuffled = new List<ReimbursementRequest> { r1, r6, r3, r5, r2, r4 }
            .Select((r, i) => r with { InputIndex = i })
            .ToList();

        var results = _processor.Process(shuffled, refDate);

        results.Select(r => r.Id).Should().Equal(1, 6, 3, 5, 2, 4);
        results.Select(r => r.Status).Should().Equal(
            RequestStatus.Aprovado,
            RequestStatus.SuspeitoDeFraude,
            RequestStatus.Aprovado,
            RequestStatus.Aprovado,
            RequestStatus.Aprovado,
            RequestStatus.Aprovado);
    }

    [Fact]
    public void Process_ComputesAmountEvenWhenRejected()
    {
        var refDate = new DateOnly(2024, 6, 1);
        var requests = new List<ReimbursementRequest>
        {
            Req(1, "Outros", "2024-01-01", 150, 1, 0),
        };
        var results = _processor.Process(requests, refDate);
        results[0].Status.Should().Be(RequestStatus.Rejeitado);
        results[0].ReimbursedAmount.Should().Be(75m);
    }

    [Fact]
    public void Process_SameDateRequests_UseInputOrderAsFraudTieBreaker()
    {
        var refDate = new DateOnly(2024, 6, 1);
        // Same client, same procedure date: evaluation order must follow input order, not Id.
        var requests = new List<ReimbursementRequest>
        {
            Req(10, "Outros", "2024-05-20", 10, 1, 0),
            Req(9, "Outros", "2024-05-20", 10, 1, 1),
            Req(8, "Outros", "2024-05-20", 10, 1, 2),
            Req(7, "Outros", "2024-05-20", 10, 1, 3),
            Req(6, "Outros", "2024-05-20", 10, 1, 4),
            Req(5, "Outros", "2024-05-20", 10, 1, 5),
        };

        var results = _processor.Process(requests, refDate);

        results.Single(r => r.Id == 10).Status.Should().Be(RequestStatus.Aprovado);
        results.Single(r => r.Id == 9).Status.Should().Be(RequestStatus.Aprovado);
        results.Single(r => r.Id == 8).Status.Should().Be(RequestStatus.Aprovado);
        results.Single(r => r.Id == 7).Status.Should().Be(RequestStatus.Aprovado);
        results.Single(r => r.Id == 6).Status.Should().Be(RequestStatus.Aprovado);
        results.Single(r => r.Id == 5).Status.Should().Be(RequestStatus.SuspeitoDeFraude);
        results.Single(r => r.Id == 5).Reason.Should().Be(DecisionReason.CustomerRequestLimitExceeded);
        results.Select(r => r.Id).Should().Equal(10, 9, 8, 7, 6, 5);
    }

    [Fact]
    public void Process_CustomPolicy_IsHonored()
    {
        var policy = RefundPolicy.Default with
        {
            MaxReimbursementPerProcedure = 100m,
            FraudWindowDays = 10,
            MaxRequestsPerWindow = 2,
            MaxTotalPerWindow = 1000m
        };
        var processor = new ReimbursementProcessor(policy, new ReimbursementCalculator(policy));
        var refDate = new DateOnly(2024, 6, 1);
        var requests = new List<ReimbursementRequest>
        {
            Req(1, "Consulta Médica", "2024-05-25", 500, 1, 0), // 400 capped to 100 by custom policy
            Req(2, "Consulta Médica", "2024-05-26", 100, 1, 1), // 2nd in 10-day window -> approved
            Req(3, "Consulta Médica", "2024-05-27", 100, 1, 2), // 3rd -> count limit exceeded
        };

        var results = processor.Process(requests, refDate);

        results.Single(r => r.Id == 1).ReimbursedAmount.Should().Be(100m);
        results.Single(r => r.Id == 1).Status.Should().Be(RequestStatus.Aprovado);
        results.Single(r => r.Id == 2).Status.Should().Be(RequestStatus.Aprovado);
        results.Single(r => r.Id == 3).Status.Should().Be(RequestStatus.SuspeitoDeFraude);
        results.Single(r => r.Id == 3).Reason.Should().Be(DecisionReason.CustomerRequestLimitExceeded);
    }

    [Fact]
    public void Process_LargeBatch_CompletesWithinBoundWithCorrectCount()
    {
        const int clientCount = 200;
        const int perClient = 100; // 20,000 total
        var refDate = new DateOnly(2026, 1, 1);
        var requests = new List<ReimbursementRequest>(clientCount * perClient);
        int id = 1;
        int inputIndex = 0;
        for (int c = 1; c <= clientCount; c++)
        {
            for (int i = 0; i < perClient; i++)
            {
                // Spread across many days to exercise the sliding window.
                var date = refDate.AddDays(-(i % 60));
                requests.Add(Req(id++, "Outros", date.ToString("yyyy-MM-dd"), 10m, c, inputIndex++));
            }
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var results = _processor.Process(requests, refDate);
        sw.Stop();

        results.Should().HaveCount(clientCount * perClient);
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
    }
}
