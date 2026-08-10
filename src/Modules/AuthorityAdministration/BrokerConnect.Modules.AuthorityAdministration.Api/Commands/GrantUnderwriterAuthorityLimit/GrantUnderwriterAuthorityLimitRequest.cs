using System.ComponentModel.DataAnnotations;
using BrokerConnect.Modules.AuthorityAdministration.Domain;

namespace BrokerConnect.Modules.AuthorityAdministration.Api.Commands.GrantUnderwriterAuthorityLimit;

public sealed record GrantUnderwriterAuthorityLimitRequest(
    [Required] AuthorityScope RequestedScope,
    [Required] string GrantingAuthority);

public sealed record GrantUnderwriterAuthorityLimitResponse(
    Guid AuthorityLimitId, string UnderwriterId, string CellId, int Version,
    string ValidationResult, DateTimeOffset GrantedAt);
