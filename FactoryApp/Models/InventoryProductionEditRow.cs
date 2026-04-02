namespace FactoryApp.Models;

/// <summary>One row in the «تحديث الإنتاج» dialog: current recorded total and optional add amount (as typed text).</summary>
public sealed class InventoryProductionEditRow
{
    public string Ddid { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal CurrentProduced { get; set; }

    /// <summary>User input for quantity to add; empty or zero means no change for this row.</summary>
    public string AddAmountText { get; set; } = string.Empty;
}
