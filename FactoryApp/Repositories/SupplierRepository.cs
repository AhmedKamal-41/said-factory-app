using FactoryApp.Models;
using FactoryApp.Services;
using Microsoft.Data.Sqlite;

namespace FactoryApp.Repositories;

public sealed class SupplierRepository
{
    public IReadOnlyList<Supplier> GetAllSuppliers()
    {
        using var connection = DatabaseInitializer.CreateConnection();
        var suppliers = new List<Supplier>();

        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT SupplierId, Name FROM Suppliers ORDER BY Name;";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                suppliers.Add(new Supplier
                {
                    SupplierId = reader.GetInt32(0),
                    Name = reader.GetString(1)
                });
            }
        }

        foreach (var supplier in suppliers)
        {
            foreach (var receipt in GetReceiptsBySupplier(connection, supplier.SupplierId))
                supplier.ReceiptImagePaths.Add(receipt);

            foreach (var entry in GetEntriesBySupplier(connection, supplier.SupplierId))
                supplier.Entries.Add(entry);
        }

        return suppliers;
    }

    public Supplier AddSupplier(string name)
    {
        var cleanName = (name ?? string.Empty).Trim();
        using var connection = DatabaseInitializer.CreateConnection();

        using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO Suppliers (Name) VALUES ($name);
SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$name", cleanName);
        var id = Convert.ToInt32((long)command.ExecuteScalar()!);
        return new Supplier { SupplierId = id, Name = cleanName };
    }

    public void DeleteSupplier(int supplierId)
    {
        using var connection = DatabaseInitializer.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Suppliers WHERE SupplierId = $supplierId;";
        command.Parameters.AddWithValue("$supplierId", supplierId);
        command.ExecuteNonQuery();
    }

    public void AddReceipt(int supplierId, string imagePath)
    {
        using var connection = DatabaseInitializer.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO SupplierReceipts (SupplierId, ImagePath) VALUES ($supplierId, $imagePath);";
        command.Parameters.AddWithValue("$supplierId", supplierId);
        command.Parameters.AddWithValue("$imagePath", imagePath);
        command.ExecuteNonQuery();
    }

    public void RemoveReceipt(int supplierId, string imagePath)
    {
        using var connection = DatabaseInitializer.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM SupplierReceipts WHERE SupplierId = $supplierId AND ImagePath = $imagePath;";
        command.Parameters.AddWithValue("$supplierId", supplierId);
        command.Parameters.AddWithValue("$imagePath", imagePath);
        command.ExecuteNonQuery();
    }

    public SupplierEntry AddEntry(int supplierId, SupplierEntry entry)
    {
        using var connection = DatabaseInitializer.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO SupplierEntries (SupplierId, PurchaseDate, Material, QuantityKg, PricePerKg, TotalPrice, Paid, Remaining)
VALUES ($supplierId, $purchaseDate, $material, $quantityKg, $pricePerKg, $totalPrice, $paid, $remaining);
SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$supplierId", supplierId);
        command.Parameters.AddWithValue("$purchaseDate", entry.PurchaseDate.ToString("o"));
        command.Parameters.AddWithValue("$material", entry.MaterialName ?? string.Empty);
        command.Parameters.AddWithValue("$quantityKg", (double)entry.QuantityKg);
        command.Parameters.AddWithValue("$pricePerKg", (double)entry.PricePerKg);
        command.Parameters.AddWithValue("$totalPrice", (double)entry.TotalPrice);
        command.Parameters.AddWithValue("$paid", (double)entry.Paid);
        command.Parameters.AddWithValue("$remaining", (double)entry.Remaining);
        entry.EntryId = Convert.ToInt32((long)command.ExecuteScalar()!);
        entry.SupplierId = supplierId;
        return entry;
    }

    public void UpdateEntry(SupplierEntry entry)
    {
        using var connection = DatabaseInitializer.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
UPDATE SupplierEntries
SET PurchaseDate = $purchaseDate,
    Material = $material,
    QuantityKg = $quantityKg,
    PricePerKg = $pricePerKg,
    TotalPrice = $totalPrice,
    Paid = $paid,
    Remaining = $remaining
WHERE EntryId = $entryId;";
        command.Parameters.AddWithValue("$purchaseDate", entry.PurchaseDate.ToString("o"));
        command.Parameters.AddWithValue("$material", entry.MaterialName ?? string.Empty);
        command.Parameters.AddWithValue("$quantityKg", (double)entry.QuantityKg);
        command.Parameters.AddWithValue("$pricePerKg", (double)entry.PricePerKg);
        command.Parameters.AddWithValue("$totalPrice", (double)entry.TotalPrice);
        command.Parameters.AddWithValue("$paid", (double)entry.Paid);
        command.Parameters.AddWithValue("$remaining", (double)entry.Remaining);
        command.Parameters.AddWithValue("$entryId", entry.EntryId);
        command.ExecuteNonQuery();
    }

    public void DeleteEntry(int entryId)
    {
        using var connection = DatabaseInitializer.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM SupplierEntries WHERE EntryId = $entryId;";
        command.Parameters.AddWithValue("$entryId", entryId);
        command.ExecuteNonQuery();
    }

    private static IReadOnlyList<string> GetReceiptsBySupplier(SqliteConnection connection, int supplierId)
    {
        var receipts = new List<string>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT ImagePath FROM SupplierReceipts WHERE SupplierId = $supplierId ORDER BY ReceiptId;";
        command.Parameters.AddWithValue("$supplierId", supplierId);
        using var reader = command.ExecuteReader();
        while (reader.Read())
            receipts.Add(reader.GetString(0));
        return receipts;
    }

    private static IReadOnlyList<SupplierEntry> GetEntriesBySupplier(SqliteConnection connection, int supplierId)
    {
        var entries = new List<SupplierEntry>();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT EntryId, SupplierId, PurchaseDate, Material, QuantityKg, PricePerKg, Paid
FROM SupplierEntries
WHERE SupplierId = $supplierId
ORDER BY EntryId;";
        command.Parameters.AddWithValue("$supplierId", supplierId);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var entry = new SupplierEntry
            {
                EntryId = reader.GetInt32(0),
                SupplierId = reader.GetInt32(1),
                PurchaseDate = reader.IsDBNull(2) ? DateTime.Now : DateTime.Parse(reader.GetString(2)),
                MaterialName = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                QuantityKg = Convert.ToDecimal(reader.GetDouble(4)),
                PricePerKg = Convert.ToDecimal(reader.GetDouble(5)),
                Paid = Convert.ToDecimal(reader.GetDouble(6))
            };
            entries.Add(entry);
        }
        return entries;
    }
}

