using BrokerConnect.Modules.SubmissionIntake.Domain.Aggregates;
using BrokerConnect.Modules.SubmissionIntake.Domain.Events;
using Shouldly;

namespace SubmissionIntake.Domain.Tests;

public class SubmissionTests
{
    // 2.2 — [FR-1, AC-1.1]
    [Fact]
    public void Create_from_BrokerSubmissionReceived_initializes_stream_state()
    {
        var submissionId = Guid.NewGuid();
        var received = new BrokerSubmissionReceived(
            submissionId, "BROKER-01", "Jane Contact", "raw-payload-ref-1",
            "Email", DateTimeOffset.UtcNow);

        var entity = Submission.Create(received);

        entity.SubmissionId.ShouldBe(submissionId);
        entity.BrokerFirmId.ShouldBe("BROKER-01");
        entity.RawPayloadRef.ShouldBe("raw-payload-ref-1");

        entity.NormalizationStatus.ShouldBeNull();
        entity.ClassOfBusiness.ShouldBeNull();
        entity.Territory.ShouldBeNull();
        entity.NamedInsured.ShouldBeNull();
        entity.LineSizeSought.ShouldBeNull();
        entity.KeyTerms.ShouldBeNull();
        entity.EffectiveDateRequested.ShouldBeNull();
        entity.IsRoutingRejected.ShouldBeFalse();
        entity.IsPossibleDuplicate.ShouldBeFalse();
        entity.SuspectedOriginalSubmissionId.ShouldBeNull();
        entity.SupersededBySubmissionId.ShouldBeNull();
        entity.IsConfirmedDistinct.ShouldBeFalse();
        entity.BaselinePremium.ShouldBeNull();
        entity.RiskFactorSummary.ShouldBeNull();
        entity.ModelVersion.ShouldBeNull();
    }
}
