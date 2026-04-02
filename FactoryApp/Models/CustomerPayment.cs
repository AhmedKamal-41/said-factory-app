using System.ComponentModel;

namespace FactoryApp.Models;

/// <summary>
/// Single installment / payment row for a customer receipt.
/// </summary>
public class CustomerPayment : INotifyPropertyChanged
{
    public int PaymentId { get; set; }

    /// <summary>Parent receipt; set when the payment is added to <see cref="CustomerReceipt.Payments"/>.</summary>
    internal CustomerReceipt? OwnerReceipt { get; set; }

    private DateTime _paymentDate = DateTime.Today;
    private decimal _amount;
    private decimal _remainingAfterPayment;
    private string _note = string.Empty;
    private bool _isPaid;

    public DateTime PaymentDate
    {
        get => _paymentDate;
        set
        {
            var d = value.Date;
            if (_paymentDate == d) return;
            _paymentDate = d;
            OnPropertyChanged(nameof(PaymentDate));
            OnPropertyChanged(nameof(IsOverdue));
        }
    }

    public decimal Amount
    {
        get => _amount;
        set
        {
            if (_amount == value) return;
            _amount = value;
            OnPropertyChanged(nameof(Amount));
            OwnerReceipt?.RecalculateInstallments();
        }
    }

    /// <summary>Mark whether this scheduled payment has actually been paid.</summary>
    public bool IsPaid
    {
        get => _isPaid;
        set
        {
            if (_isPaid == value) return;
            _isPaid = value;
            OnPropertyChanged(nameof(IsPaid));
            OnPropertyChanged(nameof(IsOverdue));
            OwnerReceipt?.RecalculateInstallments();
        }
    }

    /// <summary>Auto-calculated running balance after this row.</summary>
    public decimal RemainingAfterPayment
    {
        get => _remainingAfterPayment;
        internal set
        {
            if (_remainingAfterPayment == value) return;
            _remainingAfterPayment = value;
            OnPropertyChanged(nameof(RemainingAfterPayment));
        }
    }

    public string Note
    {
        get => _note;
        set { _note = value ?? string.Empty; OnPropertyChanged(nameof(Note)); }
    }

    /// <summary>Overdue when due date has passed and payment is still not paid.</summary>
    public bool IsOverdue => !IsPaid && PaymentDate.Date < DateTime.Today;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
