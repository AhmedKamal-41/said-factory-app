using FactoryApp.Models;

namespace FactoryApp.Services;

/// <summary>
/// Temporary in-memory catalog. Replace with database-backed class implementing <see cref="IProductCatalogLookup"/>.
/// </summary>
public sealed class InMemoryProductCatalog : IProductCatalogLookup
{
    private readonly List<ProductCatalogItem> _items;

    public InMemoryProductCatalog(IEnumerable<ProductCatalogItem>? seedData = null)
    {
        _items = seedData?.ToList() ?? CreateDefaultSeed();
    }

    private static List<ProductCatalogItem> CreateDefaultSeed()
    {
        return
        [
            new ProductCatalogItem { Ddid = "P-001", Name = "قماش قطني أبيض", PhotoPath = null },
            new ProductCatalogItem { Ddid = "P-002", Name = "خيط بوليستر", PhotoPath = null },
            new ProductCatalogItem { Ddid = "P-003", Name = "أزرار بلاستيك", PhotoPath = null }
        ];
    }

    public ProductCatalogItem? FindByDdid(string ddid)
    {
        if (string.IsNullOrWhiteSpace(ddid)) return null;
        var key = ddid.Trim();
        return _items.FirstOrDefault(x => string.Equals(x.Ddid, key, StringComparison.OrdinalIgnoreCase));
    }

    public ProductCatalogItem? FindByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var key = name.Trim();
        return _items.FirstOrDefault(x => string.Equals(x.Name, key, StringComparison.OrdinalIgnoreCase));
    }
}
