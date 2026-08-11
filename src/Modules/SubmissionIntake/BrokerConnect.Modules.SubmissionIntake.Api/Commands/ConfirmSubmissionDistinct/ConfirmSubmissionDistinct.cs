using System.ComponentModel.DataAnnotations;

namespace BrokerConnect.Modules.SubmissionIntake.Api.Commands.ConfirmSubmissionDistinct;

public sealed record ConfirmSubmissionDistinctRequest([property: Required] string ConfirmedBy);

public sealed record ConfirmSubmissionDistinctResponse(
    Guid SubmissionId, Guid SuspectedOriginalSubmissionId, DateTimeOffset ConfirmedAt);
