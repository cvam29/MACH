using FluentValidation;
using Mach.Application.Dtos;
using Mach.Application.Ports;
using Mach.Domain;
using MediatR;

namespace Mach.Application.Features.Products;

/// <summary>
/// The merged PDP view: commerce data (commercetools) enriched with marketing copy (Contentstack).
/// </summary>
public sealed record ProductDetailDto(ProductDto Product, ContentEntryDto? Marketing);

/// <summary>Query for a product detail page by slug.</summary>
public sealed record GetProductDetailQuery(string Slug) : IRequest<Result<ProductDetailDto>>;

/// <summary>Validates the <see cref="GetProductDetailQuery"/>.</summary>
public sealed class GetProductDetailQueryValidator : AbstractValidator<GetProductDetailQuery>
{
    public GetProductDetailQueryValidator()
    {
        RuleFor(q => q.Slug).NotEmpty();
    }
}

/// <summary>
/// Handler proving the wiring: depends on <see cref="ICommerceClient"/> + <see cref="ICmsClient"/>.
/// Missing marketing content is non-fatal (the PDP still renders commerce data).
/// </summary>
public sealed class GetProductDetailHandler
    : IRequestHandler<GetProductDetailQuery, Result<ProductDetailDto>>
{
    private const string PdpMarketingContentType = "pdp_marketing_block";

    private readonly ICommerceClient _commerce;
    private readonly ICmsClient _cms;

    public GetProductDetailHandler(ICommerceClient commerce, ICmsClient cms)
    {
        _commerce = commerce;
        _cms = cms;
    }

    public async Task<Result<ProductDetailDto>> Handle(
        GetProductDetailQuery request, CancellationToken cancellationToken)
    {
        var product = await _commerce.GetProductBySlugAsync(request.Slug, cancellationToken)
            .ConfigureAwait(false);
        if (product.IsFailure)
        {
            return Result.Failure<ProductDetailDto>(product.Error);
        }

        var marketing = await _cms
            .GetEntryAsync(PdpMarketingContentType, request.Slug, cancellationToken)
            .ConfigureAwait(false);

        return new ProductDetailDto(
            product.Value,
            marketing.IsSuccess ? marketing.Value : null);
    }
}
