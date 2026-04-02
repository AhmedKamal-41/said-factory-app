namespace FactoryApp.Services;

/// <summary>
/// Singleton access point for product records.
/// This keeps the app DB-ready later by having callers depend on the interface only.
/// </summary>
public static class ProductRepositoryProvider
{
    // Single shared instance across windows for consistent behavior now and easier DB swap later.
    public static IProductRepository Instance { get; } = CreateRepository();

    private static IProductRepository CreateRepository()
    {
        DatabaseInitializer.Initialize();
        return new SqliteProductRepository(DatabaseInitializer.ConnectionString);
    }
}

