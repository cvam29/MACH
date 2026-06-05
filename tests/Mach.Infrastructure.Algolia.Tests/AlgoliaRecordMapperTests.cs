using Mach.Application.Dtos;
using Mach.Domain.ValueObjects;
using Mach.Infrastructure.Algolia;

using Shouldly;

using static Mach.Infrastructure.Algolia.AlgoliaRecordMapper;

namespace Mach.Infrastructure.Algolia.Tests;

public class AlgoliaRecordMapperTests
{
    private static SearchRecord SampleRecord(
        IReadOnlyDictionary<string, string>? facets = null,
        bool inStock = true)
        => new(
            ObjectId: "prod-123",
            Sku: new Sku("SKU-123"),
            Slug: "running-shoe",
            Name: "Running Shoe",
            Description: "A lightweight running shoe.",
            Price: new Money(89.95m, "EUR"),
            Categories: ["footwear", "running"],
            Facets: facets ?? new Dictionary<string, string> { ["brand"] = "Acme", ["color"] = "Red" },
            InStock: inStock);

    [Fact]
    public void ToRecord_MapsAllCoreAttributes()
    {
        var result = ToRecord(SampleRecord());

        result[Attributes.ObjectId].ShouldBe("prod-123");
        result[Attributes.Sku].ShouldBe("SKU-123");
        result[Attributes.Slug].ShouldBe("running-shoe");
        result[Attributes.Name].ShouldBe("Running Shoe");
        result[Attributes.Description].ShouldBe("A lightweight running shoe.");
        result[Attributes.Price].ShouldBe(89.95m);
        result[Attributes.Currency].ShouldBe("EUR");
        result[Attributes.InStock].ShouldBe(true);
    }

    [Fact]
    public void ToRecord_MapsCategoriesAsList()
    {
        var result = ToRecord(SampleRecord());

        var categories = result[Attributes.Categories].ShouldBeAssignableTo<IReadOnlyList<string>>();
        categories!.ShouldBe(["footwear", "running"]);
    }

    [Fact]
    public void ToRecord_HoistsFacetsToTopLevelAttributes()
    {
        var result = ToRecord(SampleRecord());

        result["brand"].ShouldBe("Acme");
        result["color"].ShouldBe("Red");
    }

    [Fact]
    public void ToRecord_UsesAlgoliaObjectIdKeyCasing()
    {
        var result = ToRecord(SampleRecord());

        // Algolia requires the exact "objectID" casing.
        result.Keys.ShouldContain("objectID");
    }

    [Fact]
    public void ToRecord_ProducesIndependentCategoryCopy()
    {
        var source = new List<string> { "a", "b" };
        var record = SampleRecord() with { Categories = source };

        var result = ToRecord(record);
        source.Add("mutated");

        var categories = (IReadOnlyList<string>)result[Attributes.Categories]!;
        categories.Count.ShouldBe(2);
    }

    [Fact]
    public void ToRecord_WithEmptyFacets_StillMapsCore()
    {
        var result = ToRecord(SampleRecord(facets: new Dictionary<string, string>()));

        result.Count.ShouldBe(9); // 9 core attributes, no facets
        result[Attributes.Name].ShouldBe("Running Shoe");
    }

    [Fact]
    public void ToRecord_OutOfStock_MapsInStockFalse()
    {
        var result = ToRecord(SampleRecord(inStock: false));

        result[Attributes.InStock].ShouldBe(false);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ToRecord_BlankObjectId_Throws(string objectId)
    {
        var record = SampleRecord() with { ObjectId = objectId };

        Should.Throw<ArgumentException>(() => ToRecord(record));
    }

    [Fact]
    public void ToRecord_FacetCollidingWithReservedAttribute_Throws()
    {
        var record = SampleRecord(facets: new Dictionary<string, string> { [Attributes.Name] = "boom" });

        var ex = Should.Throw<ArgumentException>(() => ToRecord(record));
        ex.Message.ShouldContain("collides");
    }

    [Fact]
    public void ToRecord_BlankFacetKey_Throws()
    {
        var record = SampleRecord(facets: new Dictionary<string, string> { ["  "] = "x" });

        Should.Throw<ArgumentException>(() => ToRecord(record));
    }

    [Fact]
    public void ToRecord_NullRecord_Throws()
        => Should.Throw<ArgumentNullException>(() => ToRecord(null!));
}
