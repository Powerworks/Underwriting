using System.Text.Json.Serialization;
using BrokerConnect.Modules.SubmissionIntake.Domain.Events;

namespace BrokerConnect.Modules.SubmissionIntake.Domain.Aggregates;

/// <summary>
/// Self-aggregating event stream, one stream per submission, keyed by SubmissionId.
/// See specs/002-submission-intake/design.md Components — `Submission` aggregate for
/// the full Apply-computed state table. Every event in this context except
/// `PricingModelVersionDeployed` applies to this same stream.
/// </summary>
public sealed class Submission
{
    public Guid SubmissionId { get; private init; }

    /// <summary>
    /// Alias required by Marten: <c>options.Projections.Snapshot&lt;Submission&gt;</c>
    /// fails to close its internal generic types unless the identity member is
    /// literally named <c>Id</c> — the "{TypeName}Id" convention alone is not
    /// enough. The private setter (a no-op — <see cref="SubmissionId"/> is still
    /// the only real source of truth) is required too:
    /// <c>session.Events.AggregateStreamAsync&lt;T&gt;</c> compiles a setter
    /// delegate for this code path and throws NullReferenceException against a
    /// get-only property. See 001's AuthorityLimit.cs for the reproduction.
    /// </summary>
    [JsonIgnore]
    public Guid Id
    {
        get => SubmissionId;
        private set { }
    }

    public string BrokerFirmId { get; private init; } = string.Empty;

    public string RawPayloadRef { get; private init; } = string.Empty;

    public string? NormalizationStatus { get; private set; }

    public string? ClassOfBusiness { get; private set; }

    public string? Territory { get; private set; }

    public string? NamedInsured { get; private set; }

    public decimal? LineSizeSought { get; private set; }

    public string? KeyTerms { get; private set; }

    public DateOnly? EffectiveDateRequested { get; private set; }

    public bool IsRoutingRejected { get; private set; }

    public bool IsPossibleDuplicate { get; private set; }

    public Guid? SuspectedOriginalSubmissionId { get; private set; }

    public Guid? SupersededBySubmissionId { get; private set; }

    public bool IsConfirmedDistinct { get; private set; }

    public decimal? BaselinePremium { get; private set; }

    public string? RiskFactorSummary { get; private set; }

    /// <summary>
    /// Deliberately NOT named `Version` — Marten silently overwrites a document
    /// property literally named `Version` with its own internal optimistic-
    /// concurrency sequence number. This field is `BaselinePremiumGenerated`'s
    /// pricing-model version string, unrelated to that concurrency counter.
    /// </summary>
    public string? ModelVersion { get; private set; }

    [JsonConstructor]
    private Submission(
        Guid submissionId, string brokerFirmId, string rawPayloadRef,
        string? normalizationStatus, string? classOfBusiness, string? territory,
        string? namedInsured, decimal? lineSizeSought, string? keyTerms,
        DateOnly? effectiveDateRequested, bool isRoutingRejected, bool isPossibleDuplicate,
        Guid? suspectedOriginalSubmissionId, Guid? supersededBySubmissionId,
        bool isConfirmedDistinct, decimal? baselinePremium, string? riskFactorSummary,
        string? modelVersion)
    {
        SubmissionId = submissionId;
        BrokerFirmId = brokerFirmId;
        RawPayloadRef = rawPayloadRef;
        NormalizationStatus = normalizationStatus;
        ClassOfBusiness = classOfBusiness;
        Territory = territory;
        NamedInsured = namedInsured;
        LineSizeSought = lineSizeSought;
        KeyTerms = keyTerms;
        EffectiveDateRequested = effectiveDateRequested;
        IsRoutingRejected = isRoutingRejected;
        IsPossibleDuplicate = isPossibleDuplicate;
        SuspectedOriginalSubmissionId = suspectedOriginalSubmissionId;
        SupersededBySubmissionId = supersededBySubmissionId;
        IsConfirmedDistinct = isConfirmedDistinct;
        BaselinePremium = baselinePremium;
        RiskFactorSummary = riskFactorSummary;
        ModelVersion = modelVersion;
    }

    public static Submission Create(BrokerSubmissionReceived @event) => new(
        @event.SubmissionId, @event.BrokerFirmId, @event.RawPayloadRef,
        normalizationStatus: null, classOfBusiness: null, territory: null,
        namedInsured: null, lineSizeSought: null, keyTerms: null,
        effectiveDateRequested: null, isRoutingRejected: false, isPossibleDuplicate: false,
        suspectedOriginalSubmissionId: null, supersededBySubmissionId: null,
        isConfirmedDistinct: false, baselinePremium: null, riskFactorSummary: null,
        modelVersion: null);

    public void Apply(SubmissionNormalized @event)
    {
        ClassOfBusiness = @event.ClassOfBusiness;
        Territory = @event.Territory;
        NamedInsured = @event.NamedInsured;
        LineSizeSought = @event.LineSizeSought;
        KeyTerms = @event.KeyTerms;
        EffectiveDateRequested = @event.EffectiveDateRequested;
        NormalizationStatus = @event.NormalizationStatus;
    }

    public void Apply(SubmissionNormalizationFailed @event)
    {
        NormalizationStatus = "Failed";
    }

    public void Apply(PotentialDuplicateSubmissionDetected @event)
    {
        IsPossibleDuplicate = true;
        SuspectedOriginalSubmissionId = @event.SuspectedOriginalSubmissionId;
    }

    public void Apply(SubmissionSuperseded @event)
    {
        SupersededBySubmissionId = @event.SupersedingSubmissionId;
    }

    public void Apply(SubmissionConfirmedDistinct @event)
    {
        IsConfirmedDistinct = true;
    }
}
