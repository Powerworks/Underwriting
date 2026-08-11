using System.Text.Json.Serialization;

namespace BrokerConnect.Modules.SubmissionIntake.Api.ReadModels.PricedSubmissionView;

/// <summary>
/// "The broker's ask alongside the AI baseline, side by side, before the underwriter
/// opens the file - this is what makes assessment 'auditing the AI-generated model'
/// rather than pricing from scratch" (requirements.md's narrative). Field list is the
/// board's PricedSubmissionView field list: submissionId, brokerRequestedTerms,
/// baselinePremium, riskFactorSummary, modelVersion.
/// </summary>
public sealed class PricedSubmissionView
{
    public Guid SubmissionId { get; set; }

    // Marten's document-identity resolution requires the identity member to be
    // literally named Id (see Submission.Id's remarks in the Domain project; same
    // constraint applied to every other read model in this module).
    [JsonIgnore]
    public Guid Id => SubmissionId;

    // requirements.md types this "Custom" with no defined shape (design.md Technical
    // Decisions: "brokerRequestedTerms (Custom) has zero listed event dependency
    // anywhere on the board"). JSON-serialized string, same treatment as
    // BaselinePricingResult.RiskFactorSummary (7.5) -- the only concrete C# shape a
    // board "Custom" type can take on a Marten document.
    public string? BrokerRequestedTerms { get; set; }

    public decimal? BaselinePremium { get; set; }

    public string? RiskFactorSummary { get; set; }

    public string? ModelVersion { get; set; }
}
