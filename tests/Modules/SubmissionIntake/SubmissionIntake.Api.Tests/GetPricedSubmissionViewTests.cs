using BrokerConnect.Modules.SubmissionIntake.Api.ReadModels.PricedSubmissionView;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;
using PricedSubmissionViewDoc = BrokerConnect.Modules.SubmissionIntake.Api.ReadModels.PricedSubmissionView.PricedSubmissionView;

namespace SubmissionIntake.Api.Tests;

// T7.8: Layer 2 test per design.md Test Strategy. Unlike 4.8/4.10/6.7's paginated list
// endpoints (which split out a pure FilterAndPaginate method), this is a single-item
// lookup by id (mirrors AuthorityAdministration's GetAuthorityMatrix) -- NSubstitute
// mocks IQuerySession.LoadAsync directly, same "plain was-this-call-made/what-did-it-
// return" shape as 3.1's ReceiveBrokerSubmissionHandlerTests (no real Marten store
// needed for this handler).
public class GetPricedSubmissionViewTests
{
    [Fact]
    public async Task Existing_submission_returns_Ok_with_all_dto_fields_mapped()
    {
        var submissionId = Guid.NewGuid();
        var doc = new PricedSubmissionViewDoc
        {
            SubmissionId = submissionId,
            BrokerRequestedTerms = "{\"classOfBusiness\":\"Property\"}",
            BaselinePremium = 125_000m,
            RiskFactorSummary = "{\"windExposure\":\"High\"}",
            ModelVersion = "rating-model-v3",
        };
        var session = Substitute.For<IQuerySession>();
        session.LoadAsync<PricedSubmissionViewDoc>(submissionId, Arg.Any<CancellationToken>())
            .Returns(doc);

        var result = await GetPricedSubmissionView.Handle(
            submissionId, session, NullLogger<PricedSubmissionViewDoc>.Instance, CancellationToken.None);

        var ok = result.ShouldBeOfType<Results<Ok<PricedSubmissionViewResponse>, NotFound>>();
        var response = ok.Result.ShouldBeOfType<Ok<PricedSubmissionViewResponse>>().Value;
        response!.SubmissionId.ShouldBe(submissionId);
        response.BrokerRequestedTerms.ShouldBe("{\"classOfBusiness\":\"Property\"}");
        response.BaselinePremium.ShouldBe(125_000m);
        response.RiskFactorSummary.ShouldBe("{\"windExposure\":\"High\"}");
        response.ModelVersion.ShouldBe("rating-model-v3");
    }

    [Fact]
    public async Task Missing_submission_returns_NotFound()
    {
        var submissionId = Guid.NewGuid();
        var session = Substitute.For<IQuerySession>();
        session.LoadAsync<PricedSubmissionViewDoc>(submissionId, Arg.Any<CancellationToken>())
            .Returns((PricedSubmissionViewDoc?)null);

        var result = await GetPricedSubmissionView.Handle(
            submissionId, session, NullLogger<PricedSubmissionViewDoc>.Instance, CancellationToken.None);

        var ok = result.ShouldBeOfType<Results<Ok<PricedSubmissionViewResponse>, NotFound>>();
        ok.Result.ShouldBeOfType<NotFound>();
    }
}
