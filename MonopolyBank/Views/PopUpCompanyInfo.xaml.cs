using MonopolyBank.Models;
using MonopolyBank.Services;

namespace MonopolyBank.Views
{
    public partial class PopUpCompanyInfo : ContentPage
    {
        private readonly Company _company;
        private readonly User _currentUser;
        private bool _isClosing = false;

        public PopUpCompanyInfo(Company company)
        {
            InitializeComponent();
            _company = company;
            _currentUser = GameManager.Instance.CurrentUser;
            BindingContext = _company;

            UpdateUI();
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

        private void UpdateUI()
        {
            // Check if player owns entire color group
            int ownedInColor = _currentUser.Companys.Count(c => c.color == _company.color);
            bool hasMonopoly = ownedInColor >= _company.CountColorGroup && _company.IsCompany;

            BtnBuildBranch.Text = $"Построить филиал (+{_company.PriceFilial} $)";
            BtnBuildCorp.Text = $"Построить корпорацию (+{_company.PriceCorp} $)";
            BtnDestroyBranch.Text = $"Продать филиал (+{_company.PriceFilial} $)";
            BtnPledge.Text = $"Заложить (+{_company.Deposit} $)";
            BtnRedeem.Text = $"Выкупить (-{_company.Deposit} $)";
            BtnSellBank.Text = $"Продать банку (+{_company.Selling} $)";

            // Visibility conditions
            BtnBuildBranch.IsVisible = hasMonopoly && !_company.IsBan && _company.CountBulds < 3;
            BtnBuildCorp.IsVisible = hasMonopoly && !_company.IsBan && _company.CountBulds == 3;
            BtnDestroyBranch.IsVisible = !_company.IsBan && _company.CountBulds > 0;

            BtnPledge.IsVisible = !_company.IsBan && _company.CountBulds == 0;
            BtnRedeem.IsVisible = _company.IsBan;
            BtnSellBank.IsVisible = !_company.IsBan && _company.CountBulds == 0;
        }

        private async void OnBuildBranchClicked(object sender, EventArgs e)
        {
            if (_currentUser.Cash < _company.PriceFilial)
            {
                await PopUpAlert.ShowAlertAsync(Navigation, "Недостаточно средств", $"Нужно {_company.PriceFilial} $", "OK");
                return;
            }

            await GameManager.Instance.UpdateBalanceAsync(_currentUser.Id, _currentUser.Cash - _company.PriceFilial, $"Постройка филиала {_company.Name}");
            await GameManager.Instance.BuildBranchAsync(_company.index, _company.CountBulds + 1);
            _ = SoundService.Instance.PlaySoundAsync("buld");
            await GameManager.Instance.AddHistoryAsync($"Игрок {_currentUser.Name} построил филиал в {_company.Name}", "buy_buld.png", _company.PriceFilial, true);

            UpdateUI();
        }

        private async void OnBuildCorpClicked(object sender, EventArgs e)
        {
            if (_currentUser.Cash < _company.PriceCorp)
            {
                await PopUpAlert.ShowAlertAsync(Navigation, "Недостаточно средств", $"Нужно {_company.PriceCorp} $", "OK");
                return;
            }

            await GameManager.Instance.UpdateBalanceAsync(_currentUser.Id, _currentUser.Cash - _company.PriceCorp, $"Постройка корпорации {_company.Name}");
            await GameManager.Instance.BuildBranchAsync(_company.index, 4);
            _ = SoundService.Instance.PlaySoundAsync("buld");
            await GameManager.Instance.AddHistoryAsync($"Игрок {_currentUser.Name} построил корпорацию в {_company.Name}", "buy_buld.png", _company.PriceCorp, true);

            UpdateUI();
        }

        private async void OnDestroyBranchClicked(object sender, EventArgs e)
        {
            if (_company.CountBulds <= 0) return;

            int newCount = _company.CountBulds - 1;
            await GameManager.Instance.UpdateBalanceAsync(_currentUser.Id, _currentUser.Cash + _company.PriceFilial, $"Продажа филиала {_company.Name}");
            await GameManager.Instance.BuildBranchAsync(_company.index, newCount);
            _ = SoundService.Instance.PlaySoundAsync("destroy_buld");
            await GameManager.Instance.AddHistoryAsync($"Игрок {_currentUser.Name} снес филиал в {_company.Name}", "destroy_buld.png", _company.PriceFilial, true);

            UpdateUI();
        }

        private async void OnPledgeClicked(object sender, EventArgs e)
        {
            await GameManager.Instance.UpdateBalanceAsync(_currentUser.Id, _currentUser.Cash + _company.Deposit, $"Залог {_company.Name}");
            await GameManager.Instance.SetCompanyBanAsync(_company.index, true);
            await GameManager.Instance.AddHistoryAsync($"Игрок {_currentUser.Name} заложил {_company.Name}", "ipoteka.png", _company.Deposit, true);

            UpdateUI();
        }

        private async void OnRedeemClicked(object sender, EventArgs e)
        {
            if (_currentUser.Cash < _company.Deposit)
            {
                await PopUpAlert.ShowAlertAsync(Navigation, "Недостаточно средств", $"Нужно {_company.Deposit} $", "OK");
                return;
            }

            await GameManager.Instance.UpdateBalanceAsync(_currentUser.Id, _currentUser.Cash - _company.Deposit, $"Выкуп из залога {_company.Name}");
            await GameManager.Instance.SetCompanyBanAsync(_company.index, false);
            await GameManager.Instance.AddHistoryAsync($"Игрок {_currentUser.Name} выкупил из залога {_company.Name}", "payment.png", _company.Deposit, true);

            UpdateUI();
        }

        private async void OnSellBankClicked(object sender, EventArgs e)
        {
            await GameManager.Instance.UpdateBalanceAsync(_currentUser.Id, _currentUser.Cash + _company.Selling, $"Продажа банку {_company.Name}");
            await GameManager.Instance.SellCompanyAsync(_currentUser.Id, _company.index);
            await GameManager.Instance.AddHistoryAsync($"Игрок {_currentUser.Name} продал банку {_company.Name}", "pay_bank.png", _company.Selling, true);

            await CloseAsync();
        }

        private async void OnBackdropTapped(object sender, TappedEventArgs e)
        {
            await CloseAsync();
        }
    }
}
