using MonopolyBank.Models;
using MonopolyBank.Services;

namespace MonopolyBank.Views
{
    public partial class PopUpInfoUser : ContentPage
    {
        private readonly User _targetUser;
        private bool _isClosing = false;

        public PopUpInfoUser(User targetUser)
        {
            InitializeComponent();
            _targetUser = targetUser;
            BindingContext = _targetUser;

            if (_targetUser.Id == GameManager.Instance.CurrentUser.Id)
            {
                BtnStartTrade.IsVisible = false;
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

        private async void OnStartTradeClicked(object sender, EventArgs e)
        {
            if (_isClosing) return;
            BtnStartTrade.IsEnabled = false;
            try
            {
                await GameManager.Instance.SendTradeOpenAsync(_targetUser.Id);

                _isClosing = true;
                var mainNav = Application.Current?.MainPage?.Navigation ?? Navigation;
                await PoppingOut();
                await Navigation.PopModalAsync(false);
                await Task.Delay(100);
                await mainNav.PushModalAsync(new PageObmenCompanyUser(_targetUser), false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StartTrade Error] {ex.Message}");
                BtnStartTrade.IsEnabled = true;
            }
        }

        private async void OnBackdropTapped(object sender, TappedEventArgs e)
        {
            await CloseAsync();
        }
    }
}
