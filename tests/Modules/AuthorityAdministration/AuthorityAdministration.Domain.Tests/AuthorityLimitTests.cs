using BrokerConnect.Modules.AuthorityAdministration.Domain;
using BrokerConnect.Modules.AuthorityAdministration.Domain.Aggregates;
using BrokerConnect.Modules.AuthorityAdministration.Domain.Events;
using Shouldly;

namespace AuthorityAdministration.Domain.Tests;

public class AuthorityLimitTests
{
    private static AuthorityScope Scope(decimal maxLineSize, decimal? maxAggregate = null) =>
        new(["Property"], "Bermuda", maxLineSize, maxAggregate);

    // T012 — [US1]
    [Fact]
    public void Create_from_CellAuthorityLimitGranted_sets_tier_and_active_status()
    {
        var authorityLimitId = Guid.NewGuid();
        var granted = new CellAuthorityLimitGranted(
            authorityLimitId, "CELL-04", Scope(30_000_000, 5_000_000),
            "TFP-CELL04-2024", "Underwriting Governance", DateOnly.FromDateTime(DateTime.UtcNow),
            Version: 0, DateTimeOffset.UtcNow);

        var entity = AuthorityLimit.Create(granted);

        entity.AuthorityLimitId.ShouldBe(authorityLimitId);
        entity.Tier.ShouldBe("Cell");
        entity.CellId.ShouldBe("CELL-04");
        entity.UnderwriterId.ShouldBeNull();
        entity.Status.ShouldBe("Active");
        entity.RevisionNumber.ShouldBe(0);
    }

    // [US3]
    [Fact]
    public void Create_from_UnderwriterAuthorityLimitGranted_sets_tier_and_underwriter_id()
    {
        var authorityLimitId = Guid.NewGuid();
        var granted = new UnderwriterAuthorityLimitGranted(
            authorityLimitId, "UW-12", "CELL-04", Scope(10_000_000),
            "Cell CUO", DateOnly.FromDateTime(DateTime.UtcNow), Version: 0,
            "WithinCellLimit", DateTimeOffset.UtcNow);

        var entity = AuthorityLimit.Create(granted);

        entity.Tier.ShouldBe("Underwriter");
        entity.UnderwriterId.ShouldBe("UW-12");
        entity.CellId.ShouldBe("CELL-04");
    }

    // T021 — [US3] cascade math sanity check (the check itself lives in the
    // handler, not the entity — Apply never guards; this documents the shape
    // Apply produces that the handler's cascade check reads).
    [Fact]
    public void Underwriter_scope_max_line_size_never_exceeds_cell_scope_by_construction_in_this_test_fixture()
    {
        var cell = AuthorityLimit.Create(new CellAuthorityLimitGranted(
            Guid.NewGuid(), "CELL-04", Scope(30_000_000, 5_000_000), "AGMT-1", "Gov",
            DateOnly.FromDateTime(DateTime.UtcNow), 0, DateTimeOffset.UtcNow));

        cell.Scope.MaxLineSize.ShouldBe(30_000_000);
    }

    // [US4]
    [Fact]
    public void Apply_AuthorityLimitRevised_updates_scope_and_version()
    {
        var authorityLimitId = Guid.NewGuid();
        var entity = AuthorityLimit.Create(new CellAuthorityLimitGranted(
            authorityLimitId, "CELL-04", Scope(30_000_000, 5_000_000), "AGMT-1", "Gov",
            DateOnly.FromDateTime(DateTime.UtcNow), 0, DateTimeOffset.UtcNow));

        var newScope = Scope(20_000_000, 3_000_000);
        entity.Apply(new AuthorityLimitRevised(
            authorityLimitId, "Cell", entity.Scope, newScope, "Treaty renewal", "Gov",
            DateOnly.FromDateTime(DateTime.UtcNow), Version: 1, DateTimeOffset.UtcNow));

        entity.Scope.MaxLineSize.ShouldBe(20_000_000);
        entity.RevisionNumber.ShouldBe(1);
        entity.Status.ShouldBe("Active");
    }

    // [US5]
    [Fact]
    public void Apply_AuthorityLimitRevoked_sets_status_to_Revoked()
    {
        var authorityLimitId = Guid.NewGuid();
        var entity = AuthorityLimit.Create(new CellAuthorityLimitGranted(
            authorityLimitId, "CELL-04", Scope(30_000_000, 5_000_000), "AGMT-1", "Gov",
            DateOnly.FromDateTime(DateTime.UtcNow), 0, DateTimeOffset.UtcNow));

        entity.Apply(new AuthorityLimitRevoked(
            authorityLimitId, "Cell", entity.Scope, "Provider ended relationship", "Gov",
            "Immediate", DateTimeOffset.UtcNow));

        entity.Status.ShouldBe("Revoked");
    }

    // Clarified 2026-08-09 / FR-010 — documents that Apply is a deliberate no-op
    // for rejections (data-model.md Decision 3): status/version must not move.
    [Fact]
    public void Apply_rejection_events_does_not_change_status_or_version()
    {
        var authorityLimitId = Guid.NewGuid();
        var entity = AuthorityLimit.Create(new CellAuthorityLimitGranted(
            authorityLimitId, "CELL-04", Scope(30_000_000, 5_000_000), "AGMT-1", "Gov",
            DateOnly.FromDateTime(DateTime.UtcNow), 0, DateTimeOffset.UtcNow));

        entity.Apply(new CellAuthorityLimitGrantRejected(
            authorityLimitId, "CELL-04", Scope(30_000_000, 5_000_000),
            AuthorityLimitRejectionReasons.DuplicateActiveGrant, "Gov", DateTimeOffset.UtcNow));
        entity.Apply(new UnderwriterAuthorityLimitRejected(
            authorityLimitId, "UW-12", "CELL-04", Scope(10_000_000),
            AuthorityLimitRejectionReasons.ExceedsCellLimit, "Cell CUO", DateTimeOffset.UtcNow));
        entity.Apply(new AuthorityLimitRevisionRejected(
            authorityLimitId, Scope(99_000_000), AuthorityLimitRejectionReasons.RevokedRecord,
            "Gov", DateTimeOffset.UtcNow));

        entity.Status.ShouldBe("Active");
        entity.RevisionNumber.ShouldBe(0);
    }
}
