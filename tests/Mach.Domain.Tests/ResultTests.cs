using Mach.Domain;
using Shouldly;

namespace Mach.Domain.Tests;

public class ResultTests
{
    [Fact]
    public void Success_IsSuccess()
    {
        var result = Result.Success();
        result.IsSuccess.ShouldBeTrue();
        result.IsFailure.ShouldBeFalse();
    }

    [Fact]
    public void Failure_CarriesError()
    {
        var result = Result.Failure(Error.NotFound("missing"));
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("not_found");
    }

    [Fact]
    public void GenericSuccess_ExposesValue()
    {
        Result<int> result = 42;
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(42);
    }

    [Fact]
    public void GenericFailure_AccessingValueThrows()
    {
        Result<int> result = Error.Validation("bad");
        Should.Throw<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Match_RoutesByOutcome()
    {
        Result<int> ok = 10;
        Result<int> bad = Error.Unexpected("boom");

        ok.Match(v => v * 2, _ => -1).ShouldBe(20);
        bad.Match(v => v * 2, _ => -1).ShouldBe(-1);
    }
}
