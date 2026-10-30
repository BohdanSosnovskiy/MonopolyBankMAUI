using MonopolyBank.Models;
using MonopolyBank.Services;

namespace MonopolyBank.Views
{
    public partial class PopUpCompany : ContentPage
    {
        private readonly Company _company;
        private bool _isClosing = false;

        public PopUpCompany(Company company)
        {
            InitializeComponent();
            _company = company;
            BindingContext = _company;
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

        private async void OnBuyClicked(object sender, EventArgs e)
        {
            var user = GameManager.Instance.CurrentUser;
            if (user.Cash < _company.Price)
            {
                await PopUpAlert.ShowAlertAsync(Navigation, "Недостаточно средств", $"У вас {user.Cash} $, а стоимость фирмы {_company.Price} $", "OK");
                return;
            }

            // Deduct cash
            await GameManager.Instance.UpdateBalanceAsync(user.Id, user.Cash - _company.Price, $"Покупка {_company.Name}");
            // Buy company
            await GameManager.Instance.BuyCompanyAsync(user.Id, _company.index);
            // Play sound
            _ = SoundService.Instance.PlaySoundAsync("kassa");
            // Add history
            await GameManager.Instance.AddHistoryAsync($"Игрок {user.Name} купил {_company.Name}", "pay_bank.png", _company.Price, true);

            await CloseAsync();
        }

        private async void OnAuctionClicked(object sender, EventArgs e)
        {
            var compIndex = _company.index;
            int startingBid = Math.Max(1, _company.Price / 2);

            var user = GameManager.Instance.CurrentUser;
            if (user.Cash < startingBid)
            {
                await DisplayAlert("Недостаточно средств", $"Для выставления на аукцион со стартовой ценой {startingBid} $ (половина номинала) на балансе должно быть не менее {startingBid} $ (сейчас: {user.Cash} $).", "ОК");
                return;
            }

            await CloseAsync();
            await GameManager.Instance.StartAuctionAsync(compIndex, startingBid);
        }

        private async void OnBackdropTapped(object sender, TappedEventArgs e)
        {
            await CloseAsync();
        }
    }
}
