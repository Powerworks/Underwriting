using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BrokerConnect.Modules.AuthorityAdministration.Api;

/// <summary>
/// Constitution Principle VII: all error responses are ProblemDetails, never an
/// ad-hoc shape. Every rejection-event handler in this module builds its 409
/// response through here rather than returning a bespoke DTO.
/// </summary>
internal static class AuthorityRejectionProblem
{
    public static ProblemHttpResult Conflict(Guid authorityLimitId, string rejectionReason, string detail) =>
        TypedResults.Problem(
            title: "Authority limit request rejected",
            detail: detail,
            statusCode: StatusCodes.Status409Conflict,
            extensions: new Dictionary<string, object?>
            {
                ["authorityLimitId"] = authorityLimitId,
                ["rejectionReason"] = rejectionReason,
            });
}
