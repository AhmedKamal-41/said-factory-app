using FactoryApp.Models;

namespace FactoryApp.Services;

/// <summary>
/// Lookup products by DDID or display name. Swap implementation for SQLite/PostgreSQL later.
/// </summary>
public interface IProductCatalogLookup
{
    ProductCatalogItem? FindByDdid(string ddid);
    ProductCatalogItem? FindByName(string name);
}
