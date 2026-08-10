using System.ComponentModel.DataAnnotations;
using BrokerConnect.Modules.AuthorityAdministration.Domain;

namespace BrokerConnect.Modules.AuthorityAdministration.Api.Commands.GrantCellAuthorityLimit;

public sealed record GrantCellAuthorityLimitRequest(
    [Required] string GrantingParty,
    [Required] string SourceAgreementReference,
    [Required] AuthorityScope AuthorityScope,
    [Required] string Currency,
    DateOnly EffectiveDate);

public sealed record GrantCellAuthorityLimitResponse(
    Guid AuthorityLimitId, string CellId, int Version, DateTimeOffset GrantedAt);
