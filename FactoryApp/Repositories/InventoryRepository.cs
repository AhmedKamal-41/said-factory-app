using System.Globalization;
using FactoryApp.Models;
using FactoryApp.Services;
using Microsoft.Data.Sqlite;

namespace FactoryApp.Repositories;

public sealed class InventoryRepository
{
    private sealed class DeliveryAgg
    {
        public decimal Total;
        public readonly List<(string Name, decimal Qty)> Lines = new();
    }

    /// <summary>Products with recorded production and available stock (orders reduce available only).</summary>
    public IReadOnlyList<InventoryItem> GetInventoryItems()
    {
        using var connection = DatabaseInitializer.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT p.DDID, COALESCE(p.Name, ''), p.PhotoPath, COALESCE(i.Produced, 0), COALESCE(i.AvailableInStorage, 0)
FROM Products p
LEFT JOIN InventoryControl i ON i.DDID = p.DDID
ORDER BY p.DDID;";

        var items = new List<InventoryItem>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                items.Add(new InventoryItem
                {
                    Ddid = reader.GetString(0),
                    Name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    PhotoPath = reader.IsDBNull(2) ? null : reader.GetString(2),
                    Produced = Convert.ToDecimal(reader.GetDouble(3)),
                    AvailableInStorage = Convert.ToDecimal(reader.GetDouble(4))
                });
            }
        }

        var byDdid = LoadCustomerDeliveryAggregates(connection);
        var orderedTotals = LoadCustomerReceiptOrderedTotals(connection);
        foreach (var item in items)
        {
            var key = item.Ddid.Trim();
            item.TotalOrderedOnCustomerReceipts = orderedTotals.TryGetValue(key, out var req) ? req : 0m;
            if (byDdid.TryGetValue(key, out var agg))
                item.TotalDeliveredToCustomers = agg.Total;
            else
                item.TotalDeliveredToCustomers = 0m;
        }

        return items;
    }

    private static Dictionary<string, decimal> LoadCustomerReceiptOrderedTotals(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT TRIM(r.DDID) AS DdidKey, SUM(r.Quantity) AS TotalQty
FROM CustomerReceipts r
WHERE COALESCE(TRIM(r.DDID), '') != ''
GROUP BY TRIM(r.DDID);";

        var map = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var key = reader.GetString(0);
            var qty = Convert.ToDecimal(reader.GetDouble(1), CultureInfo.InvariantCulture);
            map[key] = qty;
        }

        return map;
    }

    private static Dictionary<string, DeliveryAgg> LoadCustomerDeliveryAggregates(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT TRIM(r.DDID) AS DdidKey, c.Name, SUM(s.Quantity) AS Qty
FROM CustomerReceiptSections s
JOIN CustomerReceipts r ON r.ReceiptId = s.ReceiptId
JOIN Customers c ON c.CustomerId = r.CustomerId
WHERE COALESCE(TRIM(r.DDID), '') != ''
GROUP BY TRIM(r.DDID), c.CustomerId, c.Name
ORDER BY TRIM(r.DDID), c.Name;";

        var map = new Dictionary<string, DeliveryAgg>(StringComparer.OrdinalIgnoreCase);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var ddidKey = reader.GetString(0);
            var name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            var qty = Convert.ToDecimal(reader.GetDouble(2), CultureInfo.InvariantCulture);
            if (qty == 0m) continue;

            if (!map.TryGetValue(ddidKey, out var agg))
            {
                agg = new DeliveryAgg();
                map[ddidKey] = agg;
            }

            agg.Lines.Add((name, qty));
            agg.Total += qty;
        }

        return map;
    }

    /// <summary>Sets absolute available stock (manual correction). Does not change Produced.</summary>
    public void UpsertAvailableInStorage(string ddid, decimal availableInStorage)
    {
        var key = (ddid ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(key))
            return;

        using var connection = DatabaseInitializer.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO InventoryControl (DDID, AvailableInStorage, Produced)
VALUES ($ddid, $available, 0)
ON CONFLICT(DDID) DO UPDATE SET
    AvailableInStorage = excluded.AvailableInStorage;";
        command.Parameters.AddWithValue("$ddid", key);
        command.Parameters.AddWithValue("$available", (double)availableInStorage);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Records cumulative produced quantity. The difference vs. the previous value is added to available stock.
    /// </summary>
    public void SetProduced(string ddid, decimal newProduced)
    {
        var key = (ddid ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(key))
            return;

        using var connection = DatabaseInitializer.CreateConnection();
        using var tx = connection.BeginTransaction();
        try
        {
            using (var check = connection.CreateCommand())
            {
                check.Transaction = tx;
                check.CommandText = "SELECT 1 FROM Products WHERE DDID = $ddid LIMIT 1;";
                check.Parameters.AddWithValue("$ddid", key);
                if (check.ExecuteScalar() == null)
                {
                    tx.Rollback();
                    return;
                }
            }

            var oldProduced = GetProduced(connection, tx, key);
            var delta = newProduced - oldProduced;
            if (delta == 0m)
            {
                tx.Commit();
                return;
            }

            AdjustStockByDdid(connection, tx, key, delta);

            using (var merge = connection.CreateCommand())
            {
                merge.Transaction = tx;
                merge.CommandText = @"
INSERT INTO InventoryControl (DDID, AvailableInStorage, Produced)
VALUES ($ddid, 0, $p)
ON CONFLICT(DDID) DO UPDATE SET Produced = $p;";
                merge.Parameters.AddWithValue("$ddid", key);
                merge.Parameters.AddWithValue("$p", (double)newProduced);
                merge.ExecuteNonQuery();
            }

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    private static decimal GetProduced(SqliteConnection connection, SqliteTransaction? tx, string ddid)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT COALESCE(Produced, 0) FROM InventoryControl WHERE DDID = $ddid;";
        cmd.Parameters.AddWithValue("$ddid", ddid);
        var o = cmd.ExecuteScalar();
        return o == null || o == DBNull.Value ? 0m : Convert.ToDecimal(Convert.ToDouble(o));
    }

    /// <summary>
    /// Adds <paramref name="delta"/> to stored available quantity (negative = deduct from warehouse).
    /// Only runs for DDIDs that exist in <c>Products</c> (InventoryControl FK).
    /// </summary>
    public void AdjustStockByDdid(string? ddid, decimal delta)
    {
        if (delta == 0m) return;
        using var connection = DatabaseInitializer.CreateConnection();
        AdjustStockByDdid(connection, null, ddid, delta);
    }

    /// <summary>Same as <see cref="AdjustStockByDdid(string?, decimal)"/> but participates in an open transaction.</summary>
    public void AdjustStockByDdid(SqliteConnection connection, SqliteTransaction? tx, string? ddid, decimal delta)
    {
        var key = (ddid ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(key) || delta == 0m) return;

        using var check = connection.CreateCommand();
        check.Transaction = tx;
        check.CommandText = "SELECT 1 FROM Products WHERE DDID = $ddid LIMIT 1;";
        check.Parameters.AddWithValue("$ddid", key);
        if (check.ExecuteScalar() == null) return;

        using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = @"
INSERT INTO InventoryControl (DDID, AvailableInStorage, Produced)
VALUES ($ddid, $delta, 0)
ON CONFLICT(DDID) DO UPDATE SET
    AvailableInStorage = AvailableInStorage + $delta;";
        command.Parameters.AddWithValue("$ddid", key);
        command.Parameters.AddWithValue("$delta", (double)delta);
        command.ExecuteNonQuery();
    }
}
