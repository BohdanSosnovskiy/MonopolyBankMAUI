using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MonopolyBank.Models
{
    public class Sdelka : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public User currentUser { get; set; }
        public User targetUser { get; set; }
        public string currentUserName => currentUser.Name;
        public string targetUserName => targetUser.Name;

        public event Action? OnSendingMoneyChanged;

        public Sdelka(User current, User target)
        {
            currentUser = current;
            targetUser = target;
            textButton = "Внести сделку";
            UpdateUser_Info();
        }

        private int _currentSendingMoney;
        public int currentSendingMoney
        {
            get => _currentSendingMoney;
            set
            {
                if (_currentSendingMoney != value)
                {
                    _currentSendingMoney = value;
                    OnPropertyChanged();
                    UpdateUser_Info();
                    OnSendingMoneyChanged?.Invoke();
                }
            }
        }

        public void SetCurrentSendingMoneySilent(int value)
        {
            _currentSendingMoney = value;
            OnPropertyChanged(nameof(currentSendingMoney));
            UpdateUser_Info();
        }

        private int _targetSendingMoney;
        public int targetSendingMoney
        {
            get => _targetSendingMoney;
            set
            {
                if (_targetSendingMoney != value)
                {
                    _targetSendingMoney = value;
                    OnPropertyChanged();
                    UpdateUser_Info();
                    OnSendingMoneyChanged?.Invoke();
                }
            }
        }

        public void SetTargetSendingMoneySilent(int value)
        {
            _targetSendingMoney = value;
            OnPropertyChanged(nameof(targetSendingMoney));
            UpdateUser_Info();
        }

        private string _textButton = "Внести сделку";
        public string textButton
        {
            get => _textButton;
            set
            {
                _textButton = value;
                OnPropertyChanged();
            }
        }

        public List<Company> currentUserCompany { get; set; } = new();
        public List<Company> targetUserCompany { get; set; } = new();

        private int _currentUser_Sum;
        public int currentUser_Sum
        {
            get => _currentUser_Sum;
            set
            {
                _currentUser_Sum = value;
                OnPropertyChanged();
            }
        }

        private int _targetUser_Sum;
        public int targetUser_Sum
        {
            get => _targetUser_Sum;
            set
            {
                _targetUser_Sum = value;
                OnPropertyChanged();
            }
        }

        private bool _isRC;
        public bool _isReadyCurrent
        {
            get => _isRC;
            set
            {
                _isRC = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(isReadyCurrent));
            }
        }

        private bool _isRT;
        public bool _isReadyTarget
        {
            get => _isRT;
            set
            {
                _isRT = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(isReadyTarget));
            }
        }

        public string isReadyCurrent => _isReadyCurrent ? "Готов" : "Не готов";
        public string isReadyTarget => _isReadyTarget ? "Готов" : "Не готов";

        public void UpdateUser_Info()
        {
            currentUserCompany = new List<Company>();
            currentUser_Sum = 0;
            targetUser_Sum = 0;

            foreach (var company in currentUser.Companys)
            {
                if (company.Sdelka)
                {
                    currentUser_Sum += company.Price;
                    currentUserCompany.Add(company);
                }
            }
            currentUser_Sum += currentSendingMoney;

            targetUserCompany = new List<Company>();
            foreach (var company in targetUser.Companys)
            {
                if (company.Sdelka)
                {
                    targetUser_Sum += company.Price;
                    targetUserCompany.Add(company);
                }
            }
            targetUser_Sum += targetSendingMoney;
        }
    }
}
