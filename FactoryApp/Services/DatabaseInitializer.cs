using Microsoft.Data.Sqlite;
using System.IO;

namespace FactoryApp.Services;

public static class DatabaseInitializer
{
    private static readonly object SyncLock = new();
    private static bool _initialized;
    private static string? _databaseFilePath;

    /// <summary>Live SQLite path. Default until <see cref="SetDatabaseFilePath"/> runs: %LocalAppData%\FactoryApp\factory.db</summary>
    public static string DatabaseFilePath =>
        _databaseFilePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FactoryApp",
            "factory.db");

    public static string ConnectionString => $"Data Source={DatabaseFilePath}";

    /// <summary>Call once from <see cref="AppConfigurationLoader.Load"/> before <see cref="Initialize"/>.</summary>
    public static void SetDatabaseFilePath(string absolutePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(absolutePath);
        lock (SyncLock)
        {
            if (_initialized)
                return;
            _databaseFilePath = Path.GetFullPath(absolutePath);
        }
    }

    public static void Initialize()
    {
        if (_initialized) return;

        lock (SyncLock)
        {
            if (_initialized) return;

            var dbDirectory = Path.GetDirectoryName(DatabaseFilePath);
            if (!string.IsNullOrWhiteSpace(dbDirectory))
                Directory.CreateDirectory(dbDirectory);

            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            EnableForeignKeys(connection);

            using var command = connection.CreateCommand();
            command.CommandText = @"
CREATE TABLE IF NOT EXISTS Products (
    DDID TEXT PRIMARY KEY,
    Name TEXT NOT NULL,
    PhotoPath TEXT NULL
);

CREATE TABLE IF NOT EXISTS Suppliers (
    SupplierId INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS SupplierReceipts (
    ReceiptId INTEGER PRIMARY KEY AUTOINCREMENT,
    SupplierId INTEGER NOT NULL,
    ImagePath TEXT NOT NULL,
    FOREIGN KEY (SupplierId) REFERENCES Suppliers(SupplierId) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS SupplierEntries (
    EntryId INTEGER PRIMARY KEY AUTOINCREMENT,
    SupplierId INTEGER NOT NULL,
    PurchaseDate TEXT NULL,
    Material TEXT NULL,
    QuantityKg REAL NOT NULL DEFAULT 0,
    PricePerKg REAL NOT NULL DEFAULT 0,
    TotalPrice REAL NOT NULL DEFAULT 0,
    Paid REAL NOT NULL DEFAULT 0,
    Remaining REAL NOT NULL DEFAULT 0,
    FOREIGN KEY (SupplierId) REFERENCES Suppliers(SupplierId) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS FactoryEntries (
    EntryId INTEGER PRIMARY KEY AUTOINCREMENT,
    DDID TEXT NULL,
    Name TEXT NULL,
    PhotoPath TEXT NULL,
    Kilo REAL NOT NULL DEFAULT 0,
    PricePerKilo REAL NOT NULL DEFAULT 0,
    TotalPrice REAL NOT NULL DEFAULT 0,
    ShirtCount REAL NOT NULL DEFAULT 0,
    PricePerShirt REAL NOT NULL DEFAULT 0,
    ManufacturingCost REAL NOT NULL DEFAULT 0,
    Waste REAL NOT NULL DEFAULT 0,
    PrintingCost REAL NOT NULL DEFAULT 0,
    CostPerShirt REAL NOT NULL DEFAULT 0,
    TotalCostAll REAL NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS TreasuryEntries (
    EntryId INTEGER PRIMARY KEY AUTOINCREMENT,
    Date TEXT NULL,
    PreviousBalance REAL NOT NULL DEFAULT 0,
    ActualAmountToday REAL NOT NULL DEFAULT 0,
    AddedAmount REAL NOT NULL DEFAULT 0,
    TakenAmount REAL NOT NULL DEFAULT 0,
    Reason TEXT NULL
);

CREATE TABLE IF NOT EXISTS Customers (
    CustomerId INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS CustomerReceipts (
    ReceiptId INTEGER PRIMARY KEY AUTOINCREMENT,
    CustomerId INTEGER NOT NULL,
    Date TEXT NULL,
    DDID TEXT NULL,
    Kind TEXT NULL,
    Quantity REAL NOT NULL DEFAULT 0,
    PricePerPiece REAL NOT NULL DEFAULT 0,
    Discount REAL NOT NULL DEFAULT 0,
    Total REAL NOT NULL DEFAULT 0,
    Deposit REAL NOT NULL DEFAULT 0,
    Remaining REAL NOT NULL DEFAULT 0,
    FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS CustomerPayments (
    PaymentId INTEGER PRIMARY KEY AUTOINCREMENT,
    ReceiptId INTEGER NOT NULL,
    PaymentDate TEXT NULL,
    Amount REAL NOT NULL DEFAULT 0,
    RemainingAfterPayment REAL NOT NULL DEFAULT 0,
    Note TEXT NULL,
    IsPaid INTEGER NOT NULL DEFAULT 0,
    FOREIGN KEY (ReceiptId) REFERENCES CustomerReceipts(ReceiptId) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS CustomerReceiptSections (
    SectionId INTEGER PRIMARY KEY AUTOINCREMENT,
    ReceiptId INTEGER NOT NULL,
    Quantity REAL NOT NULL DEFAULT 0,
    DeliveryDate TEXT NULL,
    FOREIGN KEY (ReceiptId) REFERENCES CustomerReceipts(ReceiptId) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS InventoryControl (
    DDID TEXT PRIMARY KEY,
    AvailableInStorage REAL NOT NULL DEFAULT 0,
    Produced REAL NOT NULL DEFAULT 0,
    FOREIGN KEY (DDID) REFERENCES Products(DDID) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS AppSettings (
    [Key] TEXT PRIMARY KEY,
    [Value] TEXT NULL
);";
            command.ExecuteNonQuery();

            // Safe migration for existing databases: add IsPaid column if missing.
            try
            {
                using var alter = connection.CreateCommand();
                alter.CommandText = "ALTER TABLE CustomerPayments ADD COLUMN IsPaid INTEGER NOT NULL DEFAULT 0;";
                alter.ExecuteNonQuery();
            }
            catch (SqliteException)
            {
                // Column already exists.
            }

            try
            {
                using var alterProduced = connection.CreateCommand();
                alterProduced.CommandText = "ALTER TABLE InventoryControl ADD COLUMN Produced REAL NOT NULL DEFAULT 0;";
                alterProduced.ExecuteNonQuery();
            }
            catch (SqliteException)
            {
                // Column already exists.
            }

            try
            {
                using var createSections = connection.CreateCommand();
                createSections.CommandText = @"
CREATE TABLE IF NOT EXISTS CustomerReceiptSections (
    SectionId INTEGER PRIMARY KEY AUTOINCREMENT,
    ReceiptId INTEGER NOT NULL,
    Quantity REAL NOT NULL DEFAULT 0,
    DeliveryDate TEXT NULL,
    FOREIGN KEY (ReceiptId) REFERENCES CustomerReceipts(ReceiptId) ON DELETE CASCADE
);";
                createSections.ExecuteNonQuery();
            }
            catch (SqliteException)
            {
                // Table may already exist from main DDL.
            }

            try
            {
                using var alterSectionDate = connection.CreateCommand();
                alterSectionDate.CommandText = "ALTER TABLE CustomerReceiptSections ADD COLUMN DeliveryDate TEXT NULL;";
                alterSectionDate.ExecuteNonQuery();
            }
            catch (SqliteException)
            {
                // Column already exists.
            }

            _initialized = true;
        }
    }

    public static SqliteConnection CreateConnection()
    {
        Initialize();
        var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        EnableForeignKeys(connection);
        return connection;
    }

    private static void EnableForeignKeys(SqliteConnection connection)
    {
        using var pragmaCommand = connection.CreateCommand();
        pragmaCommand.CommandText = "PRAGMA foreign_keys = ON;";
        pragmaCommand.ExecuteNonQuery();
    }
}

