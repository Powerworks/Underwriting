using System.ComponentModel.DataAnnotations;

namespace BrokerConnect.Modules.SubmissionIntake.Api.Commands.CorrectSubmission;

public sealed record CorrectSubmissionRequest(
    [property: Required] string CorrectedBy,
    [property: Required] string CorrectionDescription,
    bool ResubmittedForNormalization);

public sealed record CorrectSubmissionResponse(
    Guid SubmissionId, bool ResubmittedForNormalization, DateTimeOffset CorrectedAt);
