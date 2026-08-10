using System.ComponentModel.DataAnnotations;

namespace BrokerConnect.Modules.AuthorityAdministration.Api.Commands.ReviseAuthorityLimit;

public sealed record ReviseAuthorityLimitRequest(
    [Required] decimal NewMaxGrossPremium,
    [Required] decimal NewMaxLimit,
    [Required] string Reason);

public sealed record ReviseAuthorityLimitResponse(Guid AuthorityLimitId, int Version, DateTimeOffset RevisedAt);
