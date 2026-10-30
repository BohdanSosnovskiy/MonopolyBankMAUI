using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Windows.Input;

namespace MonopolyBank.Models
{
    public class Company : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public int index { get; set; }
        private bool _isNoEmty = true;
        public bool IsNoEmty
        {
            get => _isNoEmty;
            set
            {
                _isNoEmty = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsEmpty));
            }
        }
        public bool IsEmpty => !IsNoEmty;
        public string Name { get; set; } = string.Empty;
        public string nfc_marker { get; set; } = string.Empty;
        public string color { get; set; } = "Transparent";
        public int PriceFilial { get; set; }
        public int PriceCorp { get; set; }
        public int CountColorGroup { get; set; }
        
        private string _userName = string.Empty;
        public string UserName
        {
            get => _userName;
            set
            {
                _userName = value;
                OnPropertyChanged();
            }
        }

        public bool IsCompany { get; set; } = true;
        public bool IsCorp { get; set; } = false;

        private ObservableCollection<bool> _companyBuldVisible = new() { false, false, false, false };
        public ObservableCollection<bool> CompanyBuldVisible
        {
            get => _companyBuldVisible;
            set
            {
                _companyBuldVisible = value;
                OnPropertyChanged();
            }
        }

        public int[] Orend { get; set; } = new int[6];
        public int Price { get; set; }
        public int Selling { get; set; }
        public int Deposit { get; set; }

        private int _countBulds;
        public int CountBulds
        {
            get => _countBulds;
            set
            {
                _countBulds = value;
                OnPropertyChanged();
                UpdateBuldsVisual();
            }
        }

        private bool _isBan;
        public bool IsBan
        {
            get => _isBan;
            set
            {
                _isBan = value;
                OnPropertyChanged();
            }
        }

        private bool _isSdelka;
        public bool Sdelka
        {
            get => _isSdelka;
            set
            {
                _isSdelka = value;
                OnPropertyChanged();
            }
        }

        [JsonIgnore]
        public ICommand? SelectCompany { get; set; }

        public Company()
        {
            CompanyBuldVisible = new ObservableCollection<bool> { false, false, false, false };
        }

        public void UpdateBuldsVisual()
        {
            for (int i = 0; i < CompanyBuldVisible.Count; i++)
            {
                CompanyBuldVisible[i] = i < CountBulds;
            }
            OnPropertyChanged(nameof(CompanyBuldVisible));
        }

        public void Reset()
        {
            UserName = string.Empty;
            CountBulds = 0;
            IsBan = false;
            Sdelka = false;
            UpdateBuldsVisual();
        }
    }
}
