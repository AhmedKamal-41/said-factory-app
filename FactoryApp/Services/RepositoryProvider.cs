using FactoryApp.Repositories;

namespace FactoryApp.Services;

public static class RepositoryProvider
{
    public static SupplierRepository SupplierRepository { get; } = new();
    public static FactoryRepository FactoryRepository { get; } = new();
    public static TreasuryRepository TreasuryRepository { get; } = new();
    public static CustomerRepository CustomerRepository { get; } = new();
    public static InventoryRepository InventoryRepository { get; } = new();
}

