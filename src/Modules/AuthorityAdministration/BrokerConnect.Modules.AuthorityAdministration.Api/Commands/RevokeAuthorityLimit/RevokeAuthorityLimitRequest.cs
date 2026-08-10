using System.ComponentModel.DataAnnotations;

namespace BrokerConnect.Modules.AuthorityAdministration.Api.Commands.RevokeAuthorityLimit;

public sealed record RevokeAuthorityLimitRequest(
    [Required] string Reason,
    [Required] string Immediacy); // "Immediate" | "NextDecisionPoint"

public sealed record RevokeAuthorityLimitResponse(Guid AuthorityLimitId, DateTimeOffset RevokedAt);
