namespace FactoryApp.Models;

/// <summary>
/// Master product row for DDID / الاسم / الصورة lookup. Replace InMemoryProductCatalog with DB-backed implementation later.
/// </summary>
public class ProductCatalogItem
{
    public string Ddid { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>Local file path to product image; empty if none.</summary>
    public string? PhotoPath { get; set; }
}
