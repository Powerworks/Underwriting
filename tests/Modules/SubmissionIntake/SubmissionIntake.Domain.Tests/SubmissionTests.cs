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

    // 4.2 — [FR-3, AC-3.1]
    [Fact]
    public void Apply_SubmissionNormalized_sets_normalized_fields()
    {
        var submissionId = Guid.NewGuid();
        var received = new BrokerSubmissionReceived(
            submissionId, "BROKER-01", "Jane Contact", "raw-payload-ref-1",
            "Email", DateTimeOffset.UtcNow);
        var entity = Submission.Create(received);

        var normalized = new SubmissionNormalized(
            submissionId, "Commercial Property", "US-NE", 5_000_000m, "Wind exclusion",
            "Acme Warehousing LLC", new DateOnly(2026, 9, 1), "Normalized", DateTimeOffset.UtcNow);

        entity.Apply(normalized);

        entity.ClassOfBusiness.ShouldBe("Commercial Property");
        entity.Territory.ShouldBe("US-NE");
        entity.NamedInsured.ShouldBe("Acme Warehousing LLC");
        entity.LineSizeSought.ShouldBe(5_000_000m);
        entity.KeyTerms.ShouldBe("Wind exclusion");
        entity.EffectiveDateRequested.ShouldBe(new DateOnly(2026, 9, 1));
        entity.NormalizationStatus.ShouldBe("Normalized");
    }

    // 4.2 — [FR-4, AC-4.1]
    [Fact]
    public void Apply_SubmissionNormalizationFailed_sets_failure_status_and_preserves_RawPayloadRef()
    {
        var submissionId = Guid.NewGuid();
        var received = new BrokerSubmissionReceived(
            submissionId, "BROKER-01", "Jane Contact", "raw-payload-ref-1",
            "Email", DateTimeOffset.UtcNow);
        var entity = Submission.Create(received);

        var failed = new SubmissionNormalizationFailed(
            submissionId, "Unrecognized class code", "raw-payload-ref-1", DateTimeOffset.UtcNow);

        entity.Apply(failed);

        entity.NormalizationStatus.ShouldBe("Failed");
        entity.RawPayloadRef.ShouldBe("raw-payload-ref-1");
    }

    // 6.1 — [FR-2, AC-2.1]
    [Fact]
    public void Apply_SubmissionRoutingRejected_sets_IsRoutingRejected()
    {
        var submissionId = Guid.NewGuid();
        var received = new BrokerSubmissionReceived(
            submissionId, "BROKER-01", "Jane Contact", "raw-payload-ref-1",
            "Email", DateTimeOffset.UtcNow);
        var entity = Submission.Create(received);

        var rejected = new SubmissionRoutingRejected(
            submissionId, "BROKER-01", "CELL-01", "Commercial Property",
            "Broker not authorized for requested cell/class", DateTimeOffset.UtcNow);

        entity.Apply(rejected);

        entity.IsRoutingRejected.ShouldBeTrue();
    }

    // 5.1 — [FR-6, AC-6.1]
    [Fact]
    public void Apply_PotentialDuplicateSubmissionDetected_sets_IsPossibleDuplicate_and_SuspectedOriginalSubmissionId()
    {
        var submissionId = Guid.NewGuid();
        var suspectedOriginalId = Guid.NewGuid();
        var received = new BrokerSubmissionReceived(
            submissionId, "BROKER-01", "Jane Contact", "raw-payload-ref-1",
            "Email", DateTimeOffset.UtcNow);
        var entity = Submission.Create(received);

        var detected = new PotentialDuplicateSubmissionDetected(
            submissionId, suspectedOriginalId, "ClassOfBusiness+Territory+NamedInsured", 0.92m, DateTimeOffset.UtcNow);

        entity.Apply(detected);

        entity.IsPossibleDuplicate.ShouldBeTrue();
        entity.SuspectedOriginalSubmissionId.ShouldBe(suspectedOriginalId);
    }

    // 5.1 — [FR-7, AC-7.1]
    [Fact]
    public void Apply_SubmissionSuperseded_sets_SupersededBySubmissionId()
    {
        var originalSubmissionId = Guid.NewGuid();
        var supersedingSubmissionId = Guid.NewGuid();
        var received = new BrokerSubmissionReceived(
            originalSubmissionId, "BROKER-01", "Jane Contact", "raw-payload-ref-1",
            "Email", DateTimeOffset.UtcNow);
        var entity = Submission.Create(received);

        var superseded = new SubmissionSuperseded(
            originalSubmissionId, supersedingSubmissionId, "underwriter-1", DateTimeOffset.UtcNow);

        entity.Apply(superseded);

        entity.SupersededBySubmissionId.ShouldBe(supersedingSubmissionId);
    }

    // 5.1 — [FR-8, AC-8.1]
    [Fact]
    public void Apply_SubmissionConfirmedDistinct_sets_IsConfirmedDistinct()
    {
        var submissionId = Guid.NewGuid();
        var suspectedOriginalId = Guid.NewGuid();
        var received = new BrokerSubmissionReceived(
            submissionId, "BROKER-01", "Jane Contact", "raw-payload-ref-1",
            "Email", DateTimeOffset.UtcNow);
        var entity = Submission.Create(received);

        var confirmedDistinct = new SubmissionConfirmedDistinct(
            submissionId, suspectedOriginalId, "underwriter-1", DateTimeOffset.UtcNow);

        entity.Apply(confirmedDistinct);

        entity.IsConfirmedDistinct.ShouldBeTrue();
    }

    // 7.2 — [FR-9, AC-9.1]
    [Fact]
    public void Apply_BaselinePremiumGenerated_sets_BaselinePremium_RiskFactorSummary_and_ModelVersion()
    {
        var submissionId = Guid.NewGuid();
        var received = new BrokerSubmissionReceived(
            submissionId, "BROKER-01", "Jane Contact", "raw-payload-ref-1",
            "Email", DateTimeOffset.UtcNow);
        var entity = Submission.Create(received);

        var generated = new BaselinePremiumGenerated(
            submissionId, 125_000m, "{\"windExposure\":\"High\"}", "rating-model-v3", DateTimeOffset.UtcNow);

        entity.Apply(generated);

        entity.BaselinePremium.ShouldBe(125_000m);
        entity.RiskFactorSummary.ShouldBe("{\"windExposure\":\"High\"}");
        entity.ModelVersion.ShouldBe("rating-model-v3");
    }
}
