using System.ComponentModel.DataAnnotations;
using BrokerConnect.Modules.AuthorityAdministration.Domain;

namespace BrokerConnect.Modules.AuthorityAdministration.Api.Commands.RequestCellAuthorityIncrease;

public sealed record RequestCellAuthorityIncreaseRequest(
    [Required] string UnderwriterId,
    [Required] AuthorityScope RequestedLimit,
    [Required] string Justification);

public sealed record RequestCellAuthorityIncreaseResponse(
    Guid RequestId, string CellId, string UnderwriterId, DateTimeOffset RequestedAt);
