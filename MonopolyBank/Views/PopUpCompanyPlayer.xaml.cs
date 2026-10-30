using MonopolyBank.Models;
using MonopolyBank.Services;

namespace MonopolyBank.Views
{
    public partial class PopUpCompanyPlayer : ContentPage
    {
        private readonly Company _company;
        private readonly int _rent;
        private readonly User? _owner;
        private bool _isClosing = false;

        public PopUpCompanyPlayer(Company company)
        {
            InitializeComponent();
            _company = company;
            BindingContext = _company;

            _owner = GameManager.Instance.Users.FirstOrDefault(u => u.Name == _company.UserName);
            OwnerLabel.Text = $"Собственность игрока {_company.UserName}";

            if (_company.IsBan)
            {
                _rent = 0;
                PayRentBtn.Text = "В залоге (0 $)";
                PayRentBtn.IsEnabled = false;
            }
            else if (_company.IsCompany)
            {
                _rent = _company.Orend[Math.Clamp(_company.CountBulds, 0, 5)];
                PayRentBtn.Text = $"Оплатить аренду {_rent} $";
            }
            else
            {
                // Corporation logic
                int countOwned = _owner?.Companys.Count(c => c.color == _company.color) ?? 1;
                _rent = _company.Orend[Math.Clamp(countOwned - 1, 0, 5)];
                PayRentBtn.Text = $"Оплатить аренду {_rent} $";
            }

            Background = Colors.Transparent;
            if (Content != null) Content.TranslationY = 800;
            Loaded += Page_Loaded;
        }

        protected override bool OnBackButtonPressed()
        {
            CloseAsync();
            return true;
        }

        private void Page_Loaded(object? sender, EventArgs e)
        {
            Loaded -= Page_Loaded;
            PoppingIn();
        }

        public void PoppingIn()
        {
            var contentHeight = 800;
            this.Content.TranslationY = contentHeight;

            this.Animate("Background",
                callback: v => this.Background = new SolidColorBrush(Colors.Black.WithAlpha((float)v)),
                start: 0d,
                end: 0.65d,
                rate: 32,
                length: 350,
                easing: Easing.CubicOut,
                finished: (v, k) => this.Background = new SolidColorBrush(Colors.Black.WithAlpha(0.65f)));

            this.Animate("Content",
                callback: v => this.Content.TranslationY = (int)(contentHeight - v),
                start: 0,
                end: contentHeight,
                length: 450,
                easing: Easing.CubicOut,
                finished: (v, k) => this.Content.TranslationY = 0);
        }

        public Task PoppingOut()
        {
            var done = new TaskCompletionSource();
            var windowHeight = 700;

            this.Animate("Background",
                callback: v => this.Background = new SolidColorBrush(Colors.Black.WithAlpha((float)v)),
                start: 0.65d,
                end: 0d,
                rate: 32,
                length: 250,
                easing: Easing.CubicIn,
                finished: (v, k) => this.Background = new SolidColorBrush(Colors.Black.WithAlpha(0.0f)));

            this.Animate("Content",
                callback: v => this.Content.TranslationY = (int)(windowHeight - v),
                start: windowHeight,
                end: 0,
                length: 300,
                easing: Easing.CubicIn,
                finished: (v, k) =>
                {
                    this.Content.TranslationY = windowHeight;
                    done.TrySetResult();
                });

            return done.Task;
        }

        private async Task CloseAsync()
        {
            if (_isClosing) return;
            _isClosing = true;

            await PoppingOut();
            await Navigation.PopModalAsync(false);
        }

        private async void OnPayRentClicked(object sender, EventArgs e)
        {
            var currentUser = GameManager.Instance.CurrentUser;

            if (_rent <= 0)
            {
                await CloseAsync();
                return;
            }

            if (currentUser.Cash >= _rent)
            {
                // Pay rent
                await GameManager.Instance.UpdateBalanceAsync(currentUser.Id, currentUser.Cash - _rent, $"Оплата аренды {_company.Name}");

                if (_owner != null)
                {
                    await GameManager.Instance.UpdateBalanceAsync(_owner.Id, _owner.Cash + _rent, $"Получение аренды {_company.Name}");
                }

                _ = SoundService.Instance.PlaySoundAsync("kassa");
                await GameManager.Instance.AddHistoryAsync($"Игрок {currentUser.Name} оплатил аренду {_rent}$ игроку {_company.UserName}", "ippoteka_sell.png", _rent, true);

                await CloseAsync();
            }
            else
            {
                // Player cannot afford rent!
                var realCompaniesCount = currentUser.Companys.Count(c => c.IsNoEmty);
                if (realCompaniesCount < 2)
                {
                    // Bankruptcy!
                    await CloseAsync();
                    var mainNav = Application.Current?.MainPage?.Navigation ?? Navigation;
                    await mainNav.PushModalAsync(new PopUpGameOver(), false);
                }
                else
                {
                    await PopUpAlert.ShowAlertAsync(Navigation, "Недостаточно средств", $"Аренда составляет {_rent} $, а у вас {currentUser.Cash} $. Продайте филиалы, заложите фирмы или совершите сделку!", "Понятно");
                }
            }
        }

        private async void OnBackdropTapped(object sender, TappedEventArgs e)
        {
            await CloseAsync();
        }
    }
}
