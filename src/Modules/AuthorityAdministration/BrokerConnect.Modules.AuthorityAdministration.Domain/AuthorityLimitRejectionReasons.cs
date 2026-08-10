namespace BrokerConnect.Modules.AuthorityAdministration.Domain;

/// <summary>
/// String constants shared by the 3 rejection events (data-model.md Decision 3) —
/// kept as constants rather than a C# enum so they serialize/deserialize as plain
/// strings on Marten events without a converter, matching every other string-typed
/// field on the board.
/// </summary>
public static class AuthorityLimitRejectionReasons
{
    public const string DuplicateActiveGrant = "DuplicateActiveGrant";
    public const string ExceedsCellLimit = "ExceedsCellLimit";
    public const string RevokedRecord = "RevokedRecord";
}
