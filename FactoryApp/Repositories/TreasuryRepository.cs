using FactoryApp.Models;
using FactoryApp.Services;

namespace FactoryApp.Repositories;

public sealed class TreasuryRepository
{
    private const string OpeningBalanceKey = "TreasuryOpeningBalance";

    public decimal GetOpeningBalance()
    {
        using var connection = DatabaseInitializer.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM AppSettings WHERE [Key] = $key LIMIT 1;";
        command.Parameters.AddWithValue("$key", OpeningBalanceKey);
        var value = command.ExecuteScalar() as string;
        return decimal.TryParse(value, out var balance) ? balance : 0m;
    }

    /// <summary>True if opening balance was saved at least once (field must stay read-only).</summary>
    public bool HasOpeningBalanceBeenSaved()
    {
        using var connection = DatabaseInitializer.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM AppSettings WHERE [Key] = $key LIMIT 1;";
        command.Parameters.AddWithValue("$key", OpeningBalanceKey);
        return command.ExecuteScalar() != null;
    }

    public void SetOpeningBalance(decimal openingBalance)
    {
        using var connection = DatabaseInitializer.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO AppSettings ([Key], [Value]) VALUES ($key, $value)
ON CONFLICT([Key]) DO UPDATE SET [Value] = excluded.[Value];";
        command.Parameters.AddWithValue("$key", OpeningBalanceKey);
        command.Parameters.AddWithValue("$value", openingBalance.ToString(System.Globalization.CultureInfo.InvariantCulture));
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<TreasuryEntry> GetAllEntries()
    {
        using var connection = DatabaseInitializer.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT EntryId, Date, PreviousBalance, ActualAmountToday, Reason
FROM TreasuryEntries
ORDER BY EntryId;";
        using var reader = command.ExecuteReader();
        var list = new List<TreasuryEntry>();
        while (reader.Read())
        {
            list.Add(new TreasuryEntry
            {
                EntryId = reader.GetInt32(0),
                Date = reader.IsDBNull(1) ? null : DateTime.Parse(reader.GetString(1)),
                ActualAmountToday = Convert.ToDecimal(reader.GetDouble(3)),
                Reason = reader.IsDBNull(4) ? string.Empty : reader.GetString(4)
            });
        }
        return list;
    }

    public TreasuryEntry AddEntry(TreasuryEntry entry)
    {
        using var connection = DatabaseInitializer.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO TreasuryEntries (Date, PreviousBalance, ActualAmountToday, AddedAmount, TakenAmount, Reason)
VALUES ($date, $previousBalance, $actualAmountToday, $addedAmount, $takenAmount, $reason);
SELECT last_insert_rowid();";
        BindEntry(command, entry);
        entry.EntryId = Convert.ToInt32((long)command.ExecuteScalar()!);
        return entry;
    }

    public void UpdateEntry(TreasuryEntry entry)
    {
        using var connection = DatabaseInitializer.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
UPDATE TreasuryEntries
SET Date = $date,
    PreviousBalance = $previousBalance,
    ActualAmountToday = $actualAmountToday,
    AddedAmount = $addedAmount,
    TakenAmount = $takenAmount,
    Reason = $reason
WHERE EntryId = $entryId;";
        BindEntry(command, entry);
        command.Parameters.AddWithValue("$entryId", entry.EntryId);
        command.ExecuteNonQuery();
    }

    public void DeleteEntry(int entryId)
    {
        using var connection = DatabaseInitializer.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM TreasuryEntries WHERE EntryId = $entryId;";
        command.Parameters.AddWithValue("$entryId", entryId);
        command.ExecuteNonQuery();
    }

    private static void BindEntry(Microsoft.Data.Sqlite.SqliteCommand command, TreasuryEntry entry)
    {
        command.Parameters.AddWithValue("$date", entry.Date?.ToString("o") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$previousBalance", (double)entry.PreviousBalance);
        command.Parameters.AddWithValue("$actualAmountToday", (double)entry.ActualAmountToday);
        command.Parameters.AddWithValue("$addedAmount", (double)entry.AddedAmount);
        command.Parameters.AddWithValue("$takenAmount", (double)entry.TakenAmount);
        command.Parameters.AddWithValue("$reason", entry.Reason ?? string.Empty);
    }
}

