using System.ComponentModel.DataAnnotations;

namespace BrokerConnect.Modules.SubmissionIntake.Api.Commands.ReceiveBrokerSubmission;

public sealed record ReceiveBrokerSubmissionRequest(
    [property: Required, MaxLength(200)] string BrokerFirmId,
    [property: Required, MaxLength(200)] string SubmittingContact,
    string? CellIdHint,
    string? ClassOfBusinessHint,
    [property: Required] object RawPayload,
    [property: Required, MaxLength(50)] string SourceChannel);

public sealed record ReceiveBrokerSubmissionResponse(Guid SubmissionId, DateTimeOffset ReceivedAt);
