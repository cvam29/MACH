using Mach.Application.Dtos;
using Mach.Application.Features.Products;
using Mach.Application.Ports;
using Mach.Domain;

using MediatR;

using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;

namespace Mach.Bff.Functions;

/// <summary>
/// SSR catalog reads for the storefront grid + PDP. All reads are cache-aside via
/// <see cref="ICacheStore"/>. The rich client search runs browser-side against Algolia, so
/// <c>GET /search</c> here is a commercetools-backed listing for the server-rendered grid only.
/// Effective routes are <c>/api/catalog/categories</c>, <c>/api/search</c>, <c>/api/products/{slug}</c>.
/// </summary>
public sealed class CatalogFunctions(
    ICommerceClient commerce,
    IMediator mediator,
    ICacheStore cache)
{
    private static readonly TimeSpan CategoriesTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan SearchTtl = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ProductTtl = TimeSpan.FromMinutes(2);

    [Function("GetCategories")]
    public async Task<IResult> GetCategories(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "catalog/categories")] HttpRequest request,
        CancellationToken ct)
    {
        var result = await cache.GetOrSetAsync(
            "catalog:categories",
            innerCt => commerce.GetCategoriesAsync(innerCt),
            CategoriesTtl,
            ct);

        return ResultHttp.Ok(result);
    }

    [Function("Search")]
    public async Task<IResult> Search(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "search")] HttpRequest request,
        CancellationToken ct)
    {
        var q = request.Query("q");
        var category = request.Query("category");
        var page = Math.Max(1, request.QueryInt("page", 1));
        const int pageSize = 24;

        var cacheKey = $"search:q={q}:cat={category}:p={page}";

        var result = await cache.GetOrSetAsync(
            cacheKey,
            async innerCt =>
            {
                // NOTE: ICommerceClient currently exposes no product LISTING/query method — only
                // GetProductBySlugAsync. For the SSR grid we resolve a single product when the query
                // looks like an exact slug; otherwise we return an empty page. See the host README /
                // status notes: a commercetools product-projection search method should be added to
                // ICommerceClient to make this a true paged listing.
                if (!string.IsNullOrWhiteSpace(q))
                {
                    var bySlug = await commerce.GetProductBySlugAsync(q, innerCt).ConfigureAwait(false);
                    if (bySlug.IsSuccess)
                    {
                        var item = ProductSummaryDto.From(bySlug.Value);
                        var matchesCategory = category is null
                            || bySlug.Value.CategoryIds.Contains(category);

                        var items = matchesCategory ? new[] { item } : [];
                        return Result.Success<ProductPageDto>(
                            new ProductPageDto(items, page, pageSize, items.Length));
                    }
                }

                return Result.Success<ProductPageDto>(
                    new ProductPageDto([], page, pageSize, 0));
            },
            SearchTtl,
            ct);

        return ResultHttp.Ok(result);
    }

    [Function("GetProductBySlug")]
    public async Task<IResult> GetProductBySlug(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "products/{slug}")] HttpRequest request,
        string slug,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return ResultHttp.BadRequest("A product slug is required.");
        }

        var result = await cache.GetOrSetAsync(
            $"product:{slug}",
            innerCt => mediator.Send(new GetProductDetailQuery(slug), innerCt),
            ProductTtl,
            ct);

        return ResultHttp.Ok(result);
    }
}
