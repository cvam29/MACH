using Mach.Infrastructure.Algolia;

using Shouldly;

using static Mach.Infrastructure.Algolia.AlgoliaRecordMapper;

namespace Mach.Infrastructure.Algolia.Tests;

public class AlgoliaIndexSettingsTests
{
    [Fact]
    public void Build_SetsSearchableAttributesInWeightOrder()
    {
        var settings = AlgoliaIndexSettings.Build();

        settings.SearchableAttributes.ShouldNotBeNull();
        settings.SearchableAttributes![0].ShouldBe(Attributes.Name);
        settings.SearchableAttributes.ShouldContain(Attributes.Categories);
        settings.SearchableAttributes.ShouldContain($"unordered({Attributes.Description})");
    }

    [Fact]
    public void Build_ConfiguresFacetingAttributes()
    {
        var settings = AlgoliaIndexSettings.Build();

        settings.AttributesForFaceting.ShouldNotBeNull();
        settings.AttributesForFaceting!.ShouldContain("searchable(brand)");
        settings.AttributesForFaceting.ShouldContain("category");
        settings.AttributesForFaceting.ShouldContain($"filterOnly({Attributes.InStock})");
    }

    [Fact]
    public void Build_CustomRankingPrefersInStockThenPrice()
    {
        var settings = AlgoliaIndexSettings.Build();

        settings.CustomRanking.ShouldNotBeNull();
        settings.CustomRanking!.ShouldBe([$"desc({Attributes.InStock})", $"asc({Attributes.Price})"]);
    }

    [Fact]
    public void Build_RetrievesCoreAttributes()
    {
        var settings = AlgoliaIndexSettings.Build();

        settings.AttributesToRetrieve.ShouldNotBeNull();
        settings.AttributesToRetrieve!.ShouldContain(Attributes.ObjectId);
        settings.AttributesToRetrieve.ShouldContain(Attributes.Price);
    }
}
