using FactoryApp.Models;
using FactoryApp.Services;
using Microsoft.Data.Sqlite;

namespace FactoryApp.Repositories;

public sealed class CustomerRepository
{
    public IReadOnlyList<Customer> GetAllCustomers()
    {
        using var connection = DatabaseInitializer.CreateConnection();
        var customers = new List<Customer>();

        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT CustomerId, Name FROM Customers ORDER BY Name;";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                customers.Add(new Customer
                {
                    CustomerId = reader.GetInt32(0),
                    Name = reader.GetString(1)
                });
            }
        }

        foreach (var customer in customers)
        {
            foreach (var receipt in GetReceiptsByCustomer(connection, customer.CustomerId))
                customer.Receipts.Add(receipt);
        }

        return customers;
    }

    public Customer AddCustomer(string name)
    {
        var cleanName = (name ?? string.Empty).Trim();
        using var connection = DatabaseInitializer.CreateConnection();

        using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO Customers (Name) VALUES ($name);
SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$name", cleanName);
        var id = Convert.ToInt32((long)command.ExecuteScalar()!);
        return new Customer { CustomerId = id, Name = cleanName };
    }

    public void DeleteCustomer(int customerId)
    {
        var inventory = RepositoryProvider.InventoryRepository;
        using var connection = DatabaseInitializer.CreateConnection();
        using var tx = connection.BeginTransaction();
        try
        {
            var lines = new List<(string? Ddid, decimal Qty)>();
            using (var q = connection.CreateCommand())
            {
                q.Transaction = tx;
                q.CommandText = @"
SELECT r.DDID, s.Quantity
FROM CustomerReceiptSections s
JOIN CustomerReceipts r ON r.ReceiptId = s.ReceiptId
WHERE r.CustomerId = $customerId;";
                q.Parameters.AddWithValue("$customerId", customerId);
                using var reader = q.ExecuteReader();
                while (reader.Read())
                {
                    var ddid = reader.IsDBNull(0) ? null : reader.GetString(0);
                    var qty = Convert.ToDecimal(reader.GetDouble(1));
                    lines.Add((ddid, qty));
                }
            }

            foreach (var line in lines)
            {
                var key = NormalizeDdid(line.Ddid);
                if (key != null && line.Qty != 0m)
                    inventory.AdjustStockByDdid(connection, tx, key, line.Qty);
            }

            using (var del = connection.CreateCommand())
            {
                del.Transaction = tx;
                del.CommandText = "DELETE FROM Customers WHERE CustomerId = $customerId;";
                del.Parameters.AddWithValue("$customerId", customerId);
                del.ExecuteNonQuery();
            }

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public CustomerReceipt AddReceipt(int customerId, CustomerReceipt receipt)
    {
        using var connection = DatabaseInitializer.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO CustomerReceipts
(CustomerId, Date, DDID, Kind, Quantity, PricePerPiece, Discount, Total, Deposit, Remaining)
VALUES
($customerId, $date, $ddid, $kind, $quantity, $pricePerPiece, $discount, $total, $deposit, $remaining);
SELECT last_insert_rowid();";
        BindReceipt(command, customerId, receipt);
        receipt.ReceiptId = Convert.ToInt32((long)command.ExecuteScalar()!);
        return receipt;
    }

    public void UpdateReceipt(int customerId, CustomerReceipt receipt)
    {
        var inventory = RepositoryProvider.InventoryRepository;
        using var connection = DatabaseInitializer.CreateConnection();
        using var tx = connection.BeginTransaction();
        try
        {
            string? oldDdid;
            using (var read = connection.CreateCommand())
            {
                read.Transaction = tx;
                read.CommandText = "SELECT DDID FROM CustomerReceipts WHERE ReceiptId = $receiptId;";
                read.Parameters.AddWithValue("$receiptId", receipt.ReceiptId);
                using var reader = read.ExecuteReader();
                if (!reader.Read())
                {
                    tx.Rollback();
                    return;
                }

                oldDdid = reader.IsDBNull(0) ? null : reader.GetString(0);
            }

            var sectionQuantities = GetSectionQuantities(connection, tx, receipt.ReceiptId);

            using (var command = connection.CreateCommand())
            {
                command.Transaction = tx;
                command.CommandText = @"
UPDATE CustomerReceipts
SET CustomerId = $customerId,
    Date = $date,
    DDID = $ddid,
    Kind = $kind,
    Quantity = $quantity,
    PricePerPiece = $pricePerPiece,
    Discount = $discount,
    Total = $total,
    Deposit = $deposit,
    Remaining = $remaining
WHERE ReceiptId = $receiptId;";
                BindReceipt(command, customerId, receipt);
                command.Parameters.AddWithValue("$receiptId", receipt.ReceiptId);
                command.ExecuteNonQuery();
            }

            var oldKey = NormalizeDdid(oldDdid);
            var newKey = NormalizeDdid(receipt.Ddid);
            if (!SameDdidKey(oldKey, newKey))
            {
                foreach (var qty in sectionQuantities)
                {
                    if (oldKey != null && qty != 0m)
                        inventory.AdjustStockByDdid(connection, tx, oldKey, qty);
                    if (newKey != null && qty != 0m)
                        inventory.AdjustStockByDdid(connection, tx, newKey, -qty);
                }
            }

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public void DeleteReceipt(int receiptId)
    {
        var inventory = RepositoryProvider.InventoryRepository;
        using var connection = DatabaseInitializer.CreateConnection();
        using var tx = connection.BeginTransaction();
        try
        {
            using (var read = connection.CreateCommand())
            {
                read.Transaction = tx;
                read.CommandText = @"
SELECT s.Quantity, r.DDID
FROM CustomerReceiptSections s
JOIN CustomerReceipts r ON r.ReceiptId = s.ReceiptId
WHERE s.ReceiptId = $receiptId;";
                read.Parameters.AddWithValue("$receiptId", receiptId);
                using var reader = read.ExecuteReader();
                while (reader.Read())
                {
                    var qty = Convert.ToDecimal(reader.GetDouble(0));
                    var ddid = reader.IsDBNull(1) ? null : reader.GetString(1);
                    var key = NormalizeDdid(ddid);
                    if (key != null && qty != 0m)
                        inventory.AdjustStockByDdid(connection, tx, key, qty);
                }
            }

            using (var del = connection.CreateCommand())
            {
                del.Transaction = tx;
                del.CommandText = "DELETE FROM CustomerReceipts WHERE ReceiptId = $receiptId;";
                del.Parameters.AddWithValue("$receiptId", receiptId);
                del.ExecuteNonQuery();
            }

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public ReceiptSection AddReceiptSection(int receiptId, string? receiptDdid, ReceiptSection section)
    {
        var inventory = RepositoryProvider.InventoryRepository;
        using var connection = DatabaseInitializer.CreateConnection();
        using var tx = connection.BeginTransaction();
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
INSERT INTO CustomerReceiptSections (ReceiptId, Quantity, DeliveryDate)
VALUES ($receiptId, $quantity, $deliveryDate);
SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("$receiptId", receiptId);
            cmd.Parameters.AddWithValue("$quantity", (double)section.Quantity);
            cmd.Parameters.AddWithValue("$deliveryDate", section.DeliveryDate.ToString("o"));
            section.SectionId = Convert.ToInt32((long)cmd.ExecuteScalar()!);
            section.ReceiptId = receiptId;

            var key = NormalizeDdid(receiptDdid);
            if (key != null && section.Quantity != 0m)
                inventory.AdjustStockByDdid(connection, tx, key, -section.Quantity);

            tx.Commit();
            return section;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public void UpdateReceiptSection(int receiptId, string? receiptDdid, ReceiptSection section)
    {
        var inventory = RepositoryProvider.InventoryRepository;
        using var connection = DatabaseInitializer.CreateConnection();
        using var tx = connection.BeginTransaction();
        try
        {
            decimal oldQty;
            using (var read = connection.CreateCommand())
            {
                read.Transaction = tx;
                read.CommandText = "SELECT Quantity FROM CustomerReceiptSections WHERE SectionId = $sid AND ReceiptId = $rid;";
                read.Parameters.AddWithValue("$sid", section.SectionId);
                read.Parameters.AddWithValue("$rid", receiptId);
                using var reader = read.ExecuteReader();
                if (!reader.Read())
                {
                    tx.Rollback();
                    return;
                }

                oldQty = Convert.ToDecimal(reader.GetDouble(0));
            }

            using (var upd = connection.CreateCommand())
            {
                upd.Transaction = tx;
                upd.CommandText = "UPDATE CustomerReceiptSections SET Quantity = $q, DeliveryDate = $deliveryDate WHERE SectionId = $sid AND ReceiptId = $rid;";
                upd.Parameters.AddWithValue("$q", (double)section.Quantity);
                upd.Parameters.AddWithValue("$deliveryDate", section.DeliveryDate.ToString("o"));
                upd.Parameters.AddWithValue("$sid", section.SectionId);
                upd.Parameters.AddWithValue("$rid", receiptId);
                upd.ExecuteNonQuery();
            }

            var key = NormalizeDdid(receiptDdid);
            if (key != null)
            {
                var delta = oldQty - section.Quantity;
                if (delta != 0m)
                    inventory.AdjustStockByDdid(connection, tx, key, delta);
            }

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public void DeleteReceiptSection(int receiptId, string? receiptDdid, int sectionId)
    {
        var inventory = RepositoryProvider.InventoryRepository;
        using var connection = DatabaseInitializer.CreateConnection();
        using var tx = connection.BeginTransaction();
        try
        {
            decimal qty;
            using (var read = connection.CreateCommand())
            {
                read.Transaction = tx;
                read.CommandText = "SELECT Quantity FROM CustomerReceiptSections WHERE SectionId = $sid AND ReceiptId = $rid;";
                read.Parameters.AddWithValue("$sid", sectionId);
                read.Parameters.AddWithValue("$rid", receiptId);
                using var reader = read.ExecuteReader();
                if (!reader.Read())
                {
                    tx.Rollback();
                    return;
                }

                qty = Convert.ToDecimal(reader.GetDouble(0));
            }

            using (var del = connection.CreateCommand())
            {
                del.Transaction = tx;
                del.CommandText = "DELETE FROM CustomerReceiptSections WHERE SectionId = $sid AND ReceiptId = $rid;";
                del.Parameters.AddWithValue("$sid", sectionId);
                del.Parameters.AddWithValue("$rid", receiptId);
                del.ExecuteNonQuery();
            }

            var key = NormalizeDdid(receiptDdid);
            if (key != null && qty != 0m)
                inventory.AdjustStockByDdid(connection, tx, key, qty);

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public CustomerPayment AddPayment(int receiptId, CustomerPayment payment)
    {
        using var connection = DatabaseInitializer.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO CustomerPayments (ReceiptId, PaymentDate, Amount, RemainingAfterPayment, Note, IsPaid)
VALUES ($receiptId, $paymentDate, $amount, $remainingAfterPayment, $note, $isPaid);
SELECT last_insert_rowid();";
        BindPayment(command, receiptId, payment);
        payment.PaymentId = Convert.ToInt32((long)command.ExecuteScalar()!);
        return payment;
    }

    public void UpdatePayment(int receiptId, CustomerPayment payment)
    {
        using var connection = DatabaseInitializer.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
UPDATE CustomerPayments
SET ReceiptId = $receiptId,
    PaymentDate = $paymentDate,
    Amount = $amount,
    RemainingAfterPayment = $remainingAfterPayment,
    Note = $note,
    IsPaid = $isPaid
WHERE PaymentId = $paymentId;";
        BindPayment(command, receiptId, payment);
        command.Parameters.AddWithValue("$paymentId", payment.PaymentId);
        command.ExecuteNonQuery();
    }

    public void DeletePayment(int paymentId)
    {
        using var connection = DatabaseInitializer.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM CustomerPayments WHERE PaymentId = $paymentId;";
        command.Parameters.AddWithValue("$paymentId", paymentId);
        command.ExecuteNonQuery();
    }

    private static IReadOnlyList<CustomerReceipt> GetReceiptsByCustomer(SqliteConnection connection, int customerId)
    {
        var receipts = new List<CustomerReceipt>();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT ReceiptId, Date, DDID, Kind, Quantity, PricePerPiece, Discount, Deposit
FROM CustomerReceipts
WHERE CustomerId = $customerId
ORDER BY ReceiptId;";
        command.Parameters.AddWithValue("$customerId", customerId);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var receipt = new CustomerReceipt
            {
                ReceiptId = reader.GetInt32(0),
                Date = reader.IsDBNull(1) ? DateTime.Today : DateTime.Parse(reader.GetString(1)),
                Ddid = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                Kind = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                Quantity = Convert.ToDecimal(reader.GetDouble(4)),
                PricePerPiece = Convert.ToDecimal(reader.GetDouble(5)),
                Discount = Convert.ToDecimal(reader.GetDouble(6)),
                Deposit = Convert.ToDecimal(reader.GetDouble(7))
            };

            foreach (var payment in GetPaymentsByReceipt(connection, receipt.ReceiptId))
                receipt.Payments.Add(payment);

            foreach (var section in GetSectionsByReceipt(connection, receipt.ReceiptId))
                receipt.Sections.Add(section);

            receipts.Add(receipt);
        }

        return receipts;
    }

    private static List<decimal> GetSectionQuantities(SqliteConnection connection, SqliteTransaction? tx, int receiptId)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT Quantity FROM CustomerReceiptSections WHERE ReceiptId = $rid ORDER BY SectionId;";
        cmd.Parameters.AddWithValue("$rid", receiptId);
        using var r = cmd.ExecuteReader();
        var list = new List<decimal>();
        while (r.Read())
            list.Add(Convert.ToDecimal(r.GetDouble(0)));
        return list;
    }

    private static IReadOnlyList<ReceiptSection> GetSectionsByReceipt(SqliteConnection connection, int receiptId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT SectionId, Quantity, DeliveryDate FROM CustomerReceiptSections WHERE ReceiptId = $receiptId ORDER BY SectionId;";
        command.Parameters.AddWithValue("$receiptId", receiptId);
        using var reader = command.ExecuteReader();
        var list = new List<ReceiptSection>();
        while (reader.Read())
        {
            var deliveryDate = reader.IsDBNull(2)
                ? DateTime.Today
                : DateTime.Parse(reader.GetString(2), null, System.Globalization.DateTimeStyles.RoundtripKind).Date;
            list.Add(new ReceiptSection
            {
                SectionId = reader.GetInt32(0),
                ReceiptId = receiptId,
                Quantity = Convert.ToDecimal(reader.GetDouble(1)),
                DeliveryDate = deliveryDate
            });
        }

        return list;
    }

    private static IReadOnlyList<CustomerPayment> GetPaymentsByReceipt(SqliteConnection connection, int receiptId)
    {
        var payments = new List<CustomerPayment>();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT PaymentId, PaymentDate, Amount, Note, IsPaid
FROM CustomerPayments
WHERE ReceiptId = $receiptId
ORDER BY PaymentId;";
        command.Parameters.AddWithValue("$receiptId", receiptId);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            payments.Add(new CustomerPayment
            {
                PaymentId = reader.GetInt32(0),
                PaymentDate = reader.IsDBNull(1) ? DateTime.Today : DateTime.Parse(reader.GetString(1)),
                Amount = Convert.ToDecimal(reader.GetDouble(2)),
                Note = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                IsPaid = !reader.IsDBNull(4) && reader.GetInt64(4) == 1
            });
        }

        return payments;
    }

    private static void BindReceipt(SqliteCommand command, int customerId, CustomerReceipt receipt)
    {
        command.Parameters.AddWithValue("$customerId", customerId);
        command.Parameters.AddWithValue("$date", receipt.Date.ToString("o"));
        command.Parameters.AddWithValue("$ddid", string.IsNullOrWhiteSpace(receipt.Ddid) ? (object)DBNull.Value : receipt.Ddid.Trim());
        command.Parameters.AddWithValue("$kind", string.IsNullOrWhiteSpace(receipt.Kind) ? (object)DBNull.Value : receipt.Kind.Trim());
        command.Parameters.AddWithValue("$quantity", (double)receipt.Quantity);
        command.Parameters.AddWithValue("$pricePerPiece", (double)receipt.PricePerPiece);
        command.Parameters.AddWithValue("$discount", (double)receipt.Discount);
        command.Parameters.AddWithValue("$total", (double)receipt.Total);
        command.Parameters.AddWithValue("$deposit", (double)receipt.Deposit);
        command.Parameters.AddWithValue("$remaining", (double)receipt.Remaining);
    }

    private static void BindPayment(SqliteCommand command, int receiptId, CustomerPayment payment)
    {
        command.Parameters.AddWithValue("$receiptId", receiptId);
        command.Parameters.AddWithValue("$paymentDate", payment.PaymentDate.ToString("o"));
        command.Parameters.AddWithValue("$amount", (double)payment.Amount);
        command.Parameters.AddWithValue("$remainingAfterPayment", (double)payment.RemainingAfterPayment);
        command.Parameters.AddWithValue("$note", string.IsNullOrWhiteSpace(payment.Note) ? (object)DBNull.Value : payment.Note.Trim());
        command.Parameters.AddWithValue("$isPaid", payment.IsPaid ? 1 : 0);
    }

    private static string? NormalizeDdid(string? ddid)
    {
        var t = (ddid ?? string.Empty).Trim();
        return string.IsNullOrEmpty(t) ? null : t;
    }

    private static bool SameDdidKey(string? a, string? b) =>
        string.Equals(a ?? string.Empty, b ?? string.Empty, StringComparison.Ordinal);
}
