using System.Security.Cryptography;
using System.Text;

namespace FactoryApp.Services;

public static class AppPasswordService
{
    private const string SettingsKey = "StartupPasswordHash";
    private const int Iterations = 210_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;

    public static bool HasPassword()
    {
        var stored = GetSetting(SettingsKey);
        return !string.IsNullOrEmpty(stored);
    }

    public static void SetPassword(string plaintext)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(plaintext),
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            KeySize);
        var encoded = $"{Iterations}:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
        UpsertSetting(SettingsKey, encoded);
    }

    public static bool Verify(string plaintext)
    {
        var stored = GetSetting(SettingsKey);
        if (string.IsNullOrEmpty(stored))
            return false;

        var parts = stored.Split(':', 3);
        if (parts.Length != 3 || !int.TryParse(parts[0], out var iter) || iter < 1)
            return false;

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[1]);
            expected = Convert.FromBase64String(parts[2]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(plaintext),
            salt,
            iter,
            HashAlgorithmName.SHA256,
            expected.Length);

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static string? GetSetting(string key)
    {
        DatabaseInitializer.Initialize();
        using var connection = DatabaseInitializer.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT [Value] FROM AppSettings WHERE [Key] = $k LIMIT 1;";
        cmd.Parameters.AddWithValue("$k", key);
        return cmd.ExecuteScalar() as string;
    }

    private static void UpsertSetting(string key, string value)
    {
        DatabaseInitializer.Initialize();
        using var connection = DatabaseInitializer.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO AppSettings ([Key], [Value]) VALUES ($k, $v)
            ON CONFLICT([Key]) DO UPDATE SET [Value] = excluded.[Value];
            """;
        cmd.Parameters.AddWithValue("$k", key);
        cmd.Parameters.AddWithValue("$v", value);
        cmd.ExecuteNonQuery();
    }
}
