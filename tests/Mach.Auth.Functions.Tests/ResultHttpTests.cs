using Mach.Auth.Functions;
using Mach.Domain;

using Microsoft.AspNetCore.Http;

using Shouldly;

namespace Mach.Auth.Functions.Tests;

public sealed class ResultHttpTests
{
    [Theory]
    [InlineData("validation", StatusCodes.Status400BadRequest)]
    [InlineData("auth_failed", StatusCodes.Status401Unauthorized)]
    [InlineData("not_found", StatusCodes.Status404NotFound)]
    [InlineData("conflict", StatusCodes.Status409Conflict)]
    [InlineData("unexpected", StatusCodes.Status500InternalServerError)]
    [InlineData("something_unmapped", StatusCodes.Status500InternalServerError)]
    public void StatusCodeFor_maps_error_codes(string code, int expected)
    {
        ResultHttp.StatusCodeFor(new Error(code, "msg")).ShouldBe(expected);
    }

    [Fact]
    public void Validation_error_maps_to_400()
    {
        ResultHttp.StatusCodeFor(Error.Validation("bad")).ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public void Conflict_error_maps_to_409()
    {
        ResultHttp.StatusCodeFor(Error.Conflict("dup")).ShouldBe(StatusCodes.Status409Conflict);
    }

    [Fact]
    public void NotFound_error_maps_to_404()
    {
        ResultHttp.StatusCodeFor(Error.NotFound("gone")).ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public void Unexpected_error_maps_to_500()
    {
        ResultHttp.StatusCodeFor(Error.Unexpected("boom"))
            .ShouldBe(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public void Problem_carries_status_code_and_error_code_in_body()
    {
        var result = ResultHttp.Problem(Error.Conflict("already exists"));

        var json = result.ShouldBeOfType<Microsoft.AspNetCore.Http.HttpResults.JsonHttpResult<ResultHttp.ProblemBody>>();
        json.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
        json.Value.ShouldNotBeNull();
        json.Value!.Status.ShouldBe(StatusCodes.Status409Conflict);
        json.Value.Code.ShouldBe("conflict");
        json.Value.Detail.ShouldBe("already exists");
    }

    [Fact]
    public void Unauthorized_helper_builds_401_problem()
    {
        var result = ResultHttp.Unauthorized("no session");

        var json = result.ShouldBeOfType<Microsoft.AspNetCore.Http.HttpResults.JsonHttpResult<ResultHttp.ProblemBody>>();
        json.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
        json.Value!.Code.ShouldBe(ResultHttp.AuthFailureCode);
    }
}
