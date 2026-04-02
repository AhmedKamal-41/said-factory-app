namespace FactoryApp.Models;

/// <summary>
/// Master product row for DDID / Name / PhotoPath lookup inside factory accounts.
/// Designed to be database-ready later (fields only; no UI logic).
/// </summary>
public class Product
{
    public string DDID { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>Local file path to product image; null/empty if none.</summary>
    public string? PhotoPath { get; set; }
}

