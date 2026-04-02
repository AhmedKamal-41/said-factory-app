using FactoryApp.Models;
using FactoryApp.Services;

namespace FactoryApp.Repositories;

public sealed class FactoryRepository
{
    public IReadOnlyList<FactoryEntry> GetAllEntries()
    {
        using var connection = DatabaseInitializer.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT EntryId, DDID, Name, PhotoPath, Kilo, PricePerKilo, ShirtCount, PricePerShirt, ManufacturingCost, Waste, PrintingCost
FROM FactoryEntries
ORDER BY EntryId;";

        using var reader = command.ExecuteReader();
        var result = new List<FactoryEntry>();
        while (reader.Read())
        {
            var entry = new FactoryEntry
            {
                EntryId = reader.GetInt32(0),
                Ddid = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                Name = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                PhotoPath = reader.IsDBNull(3) ? null : reader.GetString(3),
                Kilo = Convert.ToDecimal(reader.GetDouble(4)),
                PricePerKilo = Convert.ToDecimal(reader.GetDouble(5)),
                ShirtCount = Convert.ToDecimal(reader.GetDouble(6)),
                ManufacturingCost = Convert.ToDecimal(reader.GetDouble(8)),
                Waste = Convert.ToDecimal(reader.GetDouble(9)),
                PrintingCost = Convert.ToDecimal(reader.GetDouble(10))
            };
            result.Add(entry);
        }
        return result;
    }

    public FactoryEntry AddEntry(FactoryEntry entry)
    {
        using var connection = DatabaseInitializer.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO FactoryEntries
(DDID, Name, PhotoPath, Kilo, PricePerKilo, TotalPrice, ShirtCount, PricePerShirt, ManufacturingCost, Waste, PrintingCost, CostPerShirt, TotalCostAll)
VALUES
($ddid, $name, $photoPath, $kilo, $pricePerKilo, $totalPrice, $shirtCount, $pricePerShirt, $manufacturingCost, $waste, $printingCost, $costPerShirt, $totalCostAll);
SELECT last_insert_rowid();";
        BindEntryParameters(command, entry);
        entry.EntryId = Convert.ToInt32((long)command.ExecuteScalar()!);
        return entry;
    }

    public void UpdateEntry(FactoryEntry entry)
    {
        using var connection = DatabaseInitializer.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
UPDATE FactoryEntries
SET DDID = $ddid,
    Name = $name,
    PhotoPath = $photoPath,
    Kilo = $kilo,
    PricePerKilo = $pricePerKilo,
    TotalPrice = $totalPrice,
    ShirtCount = $shirtCount,
    PricePerShirt = $pricePerShirt,
    ManufacturingCost = $manufacturingCost,
    Waste = $waste,
    PrintingCost = $printingCost,
    CostPerShirt = $costPerShirt,
    TotalCostAll = $totalCostAll
WHERE EntryId = $entryId;";
        BindEntryParameters(command, entry);
        command.Parameters.AddWithValue("$entryId", entry.EntryId);
        command.ExecuteNonQuery();
    }

    public void DeleteEntry(int entryId)
    {
        using var connection = DatabaseInitializer.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM FactoryEntries WHERE EntryId = $entryId;";
        command.Parameters.AddWithValue("$entryId", entryId);
        command.ExecuteNonQuery();
    }

    private static void BindEntryParameters(Microsoft.Data.Sqlite.SqliteCommand command, FactoryEntry entry)
    {
        command.Parameters.AddWithValue("$ddid", string.IsNullOrWhiteSpace(entry.Ddid) ? (object)DBNull.Value : entry.Ddid);
        command.Parameters.AddWithValue("$name", string.IsNullOrWhiteSpace(entry.Name) ? (object)DBNull.Value : entry.Name);
        command.Parameters.AddWithValue("$photoPath", string.IsNullOrWhiteSpace(entry.PhotoPath) ? (object)DBNull.Value : entry.PhotoPath);
        command.Parameters.AddWithValue("$kilo", (double)entry.Kilo);
        command.Parameters.AddWithValue("$pricePerKilo", (double)entry.PricePerKilo);
        command.Parameters.AddWithValue("$totalPrice", (double)entry.TotalPrice);
        command.Parameters.AddWithValue("$shirtCount", (double)entry.ShirtCount);
        command.Parameters.AddWithValue("$pricePerShirt", (double)entry.PricePerShirt);
        command.Parameters.AddWithValue("$manufacturingCost", (double)entry.ManufacturingCost);
        command.Parameters.AddWithValue("$waste", (double)entry.Waste);
        command.Parameters.AddWithValue("$printingCost", (double)entry.PrintingCost);
        command.Parameters.AddWithValue("$costPerShirt", (double)entry.CostPerShirt);
        command.Parameters.AddWithValue("$totalCostAll", (double)entry.TotalCostAll);
    }
}

