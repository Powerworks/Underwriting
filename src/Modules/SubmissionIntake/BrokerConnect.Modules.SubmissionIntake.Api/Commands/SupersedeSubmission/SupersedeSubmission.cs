using System.ComponentModel.DataAnnotations;

namespace BrokerConnect.Modules.SubmissionIntake.Api.Commands.SupersedeSubmission;

public sealed record SupersedeSubmissionRequest(
    [property: Required] Guid SupersedingSubmissionId,
    [property: Required] string LinkedBy);

public sealed record SupersedeSubmissionResponse(
    Guid OriginalSubmissionId, Guid SupersedingSubmissionId, DateTimeOffset LinkedAt);
