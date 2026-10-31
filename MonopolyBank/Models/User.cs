using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Windows.Input;

namespace MonopolyBank.Models
{
    public class User : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
        
        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged();
            }
        }

        private int _cash;
        public int Cash
        {
            get => _cash;
            set
            {
                _cash = value;
                OnPropertyChanged();
            }
        }

        private bool _isGameOver;
        public bool isGameOver
        {
            get => _isGameOver;
            set
            {
                _isGameOver = value;
                OnPropertyChanged();
            }
        }

        private bool _isNoNfc;
        public bool IsNoNfc
        {
            get => _isNoNfc;
            set
            {
                _isNoNfc = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(NfcBadgeText));
                OnPropertyChanged(nameof(NfcBadgeColor));
            }
        }

        [JsonIgnore]
        public string NfcBadgeText => IsNoNfc ? "📵 Без NFC" : "📱 NFC";

        [JsonIgnore]
        public Color NfcBadgeColor => IsNoNfc ? Color.FromArgb("#8e2de2") : Color.FromArgb("#0daaa3");

        public ObservableCollection<Company> Companys { get; set; } = new();
        public string ImgPath { get; set; } = "user.png";

        [JsonIgnore]
        public ICommand? SelectUser { get; set; }

        public User()
        {
            Companys = new ObservableCollection<Company>
            {
                new Company { IsNoEmty = false }
            };
        }

        public User(string username, int initialCash = 15000)
        {
            Id = Guid.NewGuid().ToString("N")[..8];
            Name = username;
            Cash = initialCash;
            isGameOver = false;
            Companys = new ObservableCollection<Company>
            {
                new Company { IsNoEmty = false }
            };
        }
    }
}
