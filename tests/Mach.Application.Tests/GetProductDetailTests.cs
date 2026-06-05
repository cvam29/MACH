using Mach.Application.Dtos;
using Mach.Application.Features.Products;
using Mach.Application.Ports;
using Mach.Domain;
using Mach.Domain.ValueObjects;
using Shouldly;

namespace Mach.Application.Tests;

public class GetProductDetailTests
{
    private static readonly ProductDto Product = new(
        new ProductId("p1"), "shirt", "Shirt", "A shirt", [], []);

    [Fact]
    public async Task Handle_MergesCommerceAndMarketing()
    {
        var handler = new GetProductDetailHandler(
            new FakeCommerce(Result.Success(Product)),
            new FakeCms(Result.Success(new ContentEntryDto(
                "pdp_marketing_block", "shirt", "Marketing", new Dictionary<string, object?>()))));

        var result = await handler.Handle(new GetProductDetailQuery("shirt"), default);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Product.Slug.ShouldBe("shirt");
        result.Value.Marketing.ShouldNotBeNull();
    }

    [Fact]
    public async Task Handle_MissingMarketing_IsNonFatal()
    {
        var handler = new GetProductDetailHandler(
            new FakeCommerce(Result.Success(Product)),
            new FakeCms(Result.Failure<ContentEntryDto>(Error.NotFound("none"))));

        var result = await handler.Handle(new GetProductDetailQuery("shirt"), default);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Marketing.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_CommerceFailure_PropagatesError()
    {
        var handler = new GetProductDetailHandler(
            new FakeCommerce(Result.Failure<ProductDto>(Error.NotFound("gone"))),
            new FakeCms(Result.Failure<ContentEntryDto>(Error.NotFound("none"))));

        var result = await handler.Handle(new GetProductDetailQuery("shirt"), default);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("not_found");
    }

    private sealed class FakeCommerce(Result<ProductDto> product) : StubCommerceClient
    {
        public override Task<Result<ProductDto>> GetProductBySlugAsync(string slug, CancellationToken ct)
            => Task.FromResult(product);
    }

    private sealed class FakeCms(Result<ContentEntryDto> entry) : ICmsClient
    {
        public Task<Result<ContentEntryDto>> GetEntryAsync(string contentType, string slug, CancellationToken ct)
            => Task.FromResult(entry);

        public Task<Result<NavigationNodeDto>> GetNavigationAsync(CancellationToken ct)
            => throw new NotSupportedException();

        public Task<Result<EmailTemplateDto>> GetEmailTemplateAsync(NotificationAudience audience, CancellationToken ct)
            => throw new NotSupportedException();
    }
}
