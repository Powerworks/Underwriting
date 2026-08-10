using System.Text.Json.Serialization;
using BrokerConnect.Modules.AuthorityAdministration.Domain.Events;

namespace BrokerConnect.Modules.AuthorityAdministration.Domain.Aggregates;

/// <summary>
/// Its own small single-event aggregate — deliberately not part of the
/// AuthorityLimit stream (data-model.md). No lifecycle beyond creation is modeled
/// by the source board yet; extending it (approve/deny) is future scope.
/// </summary>
public sealed class CellAuthorityIncreaseRequest
{
    public Guid RequestId { get; private init; }
    public string CellId { get; private init; } = string.Empty;
    public string RequestedBy { get; private init; } = string.Empty;
    public AuthorityScope CurrentLimit { get; private init; } = null!;
    public AuthorityScope RequestedLimit { get; private init; } = null!;
    public string Justification { get; private init; } = string.Empty;
    public DateTimeOffset RequestedAt { get; private init; }

    [JsonConstructor]
    private CellAuthorityIncreaseRequest(
        Guid requestId, string cellId, string requestedBy, AuthorityScope currentLimit,
        AuthorityScope requestedLimit, string justification, DateTimeOffset requestedAt)
    {
        RequestId = requestId;
        CellId = cellId;
        RequestedBy = requestedBy;
        CurrentLimit = currentLimit;
        RequestedLimit = requestedLimit;
        Justification = justification;
        RequestedAt = requestedAt;
    }

    public static CellAuthorityIncreaseRequest Create(CellAuthorityIncreaseRequested @event) => new(
        @event.RequestId, @event.CellId, @event.RequestedBy, @event.CurrentLimit,
        @event.RequestedLimit, @event.Justification, @event.RequestedAt);
}
