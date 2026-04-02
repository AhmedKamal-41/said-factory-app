using System.ComponentModel;

namespace FactoryApp.Models;

/// <summary>
/// One daily safe count row (الخزنة). Persistence-ready: plain properties, no UI logic.
/// </summary>
public class TreasuryEntry : INotifyPropertyChanged
{
    public int EntryId { get; set; }

    private DateTime? _date;
    private decimal _previousBalance;
    private decimal _actualAmountToday;
    private string _reason = string.Empty;

    /// <summary>التاريخ</summary>
    public DateTime? Date
    {
        get => _date;
        set
        {
            if (_date == value) return;
            _date = value;
            OnPropertyChanged(nameof(Date));
        }
    }

    /// <summary>رصيد اليوم السابق — set by chain recalculation from opening balance or prior row’s actual count.</summary>
    public decimal PreviousBalance => _previousBalance;

    internal void SetPreviousBalance(decimal value)
    {
        if (_previousBalance == value) return;
        _previousBalance = value;
        OnPropertyChanged(nameof(PreviousBalance));
        NotifyAmountDerived();
    }

    /// <summary>المبلغ الفعلي اليوم — user-entered counted cash in safe.</summary>
    public decimal ActualAmountToday
    {
        get => _actualAmountToday;
        set
        {
            if (_actualAmountToday == value) return;
            _actualAmountToday = value;
            OnPropertyChanged(nameof(ActualAmountToday));
            NotifyAmountDerived();
        }
    }

    /// <summary>Internal: المبلغ الفعلي اليوم − رصيد اليوم السابق (not persisted as a separate field).</summary>
    private decimal ComputeCashDifference() => _actualAmountToday - _previousBalance;

    /// <summary>المبلغ المضاف — derived from internal difference &gt; 0.</summary>
    public decimal AddedAmount
    {
        get
        {
            var d = ComputeCashDifference();
            return d > 0 ? d : 0;
        }
    }

    /// <summary>المبلغ المسحوب — derived from internal difference &lt; 0.</summary>
    public decimal TakenAmount
    {
        get
        {
            var d = ComputeCashDifference();
            return d < 0 ? Math.Abs(d) : 0;
        }
    }

    /// <summary>السبب</summary>
    public string Reason
    {
        get => _reason;
        set
        {
            var v = value ?? string.Empty;
            if (_reason == v) return;
            _reason = v;
            OnPropertyChanged(nameof(Reason));
        }
    }

    private void NotifyAmountDerived()
    {
        OnPropertyChanged(nameof(AddedAmount));
        OnPropertyChanged(nameof(TakenAmount));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
