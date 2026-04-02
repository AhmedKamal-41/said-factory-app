namespace FactoryApp.Services;

/// <summary>Fixed access code for opening الخزنة (no user setup).</summary>
public static class TreasuryAccess
{
    private const string StaticPassword = "nofoodforramadan";

    public static bool Verify(string? entered) =>
        string.Equals(entered ?? string.Empty, StaticPassword, StringComparison.Ordinal);
}
