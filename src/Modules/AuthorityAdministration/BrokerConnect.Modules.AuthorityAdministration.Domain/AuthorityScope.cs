namespace BrokerConnect.Modules.AuthorityAdministration.Domain;

/// <summary>
/// Canonical internal shape for the board's "authorityScope"/"scope"/"requestedScope"
/// fields — reconciled to one name here per data-model.md's note on the naming
/// inconsistency inherited from the board (Cell-tier uses maxLineSize/maxAggregate,
/// Underwriter-tier scope has no maxAggregate). MaxAggregate is null for
/// Underwriter-tier scopes.
/// </summary>
public sealed record AuthorityScope(
    IReadOnlyList<string> ClassesOfBusiness,
    string Territory,
    decimal MaxLineSize,
    decimal? MaxAggregate);
