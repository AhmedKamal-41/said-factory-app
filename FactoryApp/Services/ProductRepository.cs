using FactoryApp.Models;
using Microsoft.Data.Sqlite;

namespace FactoryApp.Services;

/// <summary>
/// Shared product lookup source for factory accounts (DDID / Name / PhotoPath).
/// Database-ready: UI should depend on the interface, not the in-memory implementation.
/// </summary>
public interface IProductRepository
{
    IReadOnlyList<Product> GetAllProducts();
    Product? GetByDDID(string ddid);
    Product? GetByName(string name);

    // Optional methods (for future database readiness)
    void AddProduct(Product product);
    void UpdateProduct(Product product);
    void DeleteProduct(string ddid);
}

public sealed class SqliteProductRepository : IProductRepository
{
    public SqliteProductRepository(string connectionString)
    {
        _ = connectionString;
    }

    public IReadOnlyList<Product> GetAllProducts()
    {
        var result = new List<Product>();
        using var connection = DatabaseInitializer.CreateConnection();

        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT DDID, Name, PhotoPath
FROM Products
ORDER BY DDID;";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new Product
            {
                DDID = reader.GetString(0),
                Name = reader.GetString(1),
                PhotoPath = reader.IsDBNull(2) ? null : reader.GetString(2)
            });
        }

        return result;
    }

    public Product? GetByDDID(string ddid)
    {
        if (string.IsNullOrWhiteSpace(ddid)) return null;
        var key = ddid.Trim();

        using var connection = DatabaseInitializer.CreateConnection();

        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT DDID, Name, PhotoPath
FROM Products
WHERE DDID = $ddid COLLATE NOCASE
LIMIT 1;";
        command.Parameters.AddWithValue("$ddid", key);

        using var reader = command.ExecuteReader();
        if (!reader.Read()) return null;

        return new Product
        {
            DDID = reader.GetString(0),
            Name = reader.GetString(1),
            PhotoPath = reader.IsDBNull(2) ? null : reader.GetString(2)
        };
    }

    public Product? GetByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var key = name.Trim();

        using var connection = DatabaseInitializer.CreateConnection();

        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT DDID, Name, PhotoPath
FROM Products
WHERE Name = $name COLLATE NOCASE
LIMIT 1;";
        command.Parameters.AddWithValue("$name", key);

        using var reader = command.ExecuteReader();
        if (!reader.Read()) return null;

        return new Product
        {
            DDID = reader.GetString(0),
            Name = reader.GetString(1),
            PhotoPath = reader.IsDBNull(2) ? null : reader.GetString(2)
        };
    }

    public void AddProduct(Product product)
    {
        if (product == null) return;
        var ddid = (product.DDID ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(ddid)) return;

        var name = (product.Name ?? string.Empty).Trim();
        var photoPath = string.IsNullOrWhiteSpace(product.PhotoPath) ? null : product.PhotoPath.Trim();

        using var connection = DatabaseInitializer.CreateConnection();

        using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO Products (DDID, Name, PhotoPath)
VALUES ($ddid, $name, $photoPath);";
        command.Parameters.AddWithValue("$ddid", ddid);
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$photoPath", (object?)photoPath ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    public void UpdateProduct(Product product)
    {
        if (product == null) return;
        var ddid = (product.DDID ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(ddid)) return;

        var name = (product.Name ?? string.Empty).Trim();
        var photoPath = string.IsNullOrWhiteSpace(product.PhotoPath) ? null : product.PhotoPath.Trim();

        using var connection = DatabaseInitializer.CreateConnection();

        using var command = connection.CreateCommand();
        command.CommandText = @"
UPDATE Products
SET Name = $name,
    PhotoPath = $photoPath
WHERE DDID = $ddid COLLATE NOCASE;";
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$photoPath", (object?)photoPath ?? DBNull.Value);
        command.Parameters.AddWithValue("$ddid", ddid);
        command.ExecuteNonQuery();
    }

    public void DeleteProduct(string ddid)
    {
        if (string.IsNullOrWhiteSpace(ddid)) return;
        var key = ddid.Trim();

        using var connection = DatabaseInitializer.CreateConnection();

        using var command = connection.CreateCommand();
        command.CommandText = @"
DELETE FROM Products
WHERE DDID = $ddid COLLATE NOCASE;";
        command.Parameters.AddWithValue("$ddid", key);
        command.ExecuteNonQuery();
    }
}

