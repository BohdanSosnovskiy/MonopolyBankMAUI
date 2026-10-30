using MonopolyBank.Models;
using MonopolyBank.Services;

namespace MonopolyBank.Views
{
    public partial class PopUpGameOver : ContentPage
    {
        private bool _isClosing = false;

        public PopUpGameOver()
        {
            InitializeComponent();
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
            _ = SoundService.Instance.PlaySoundAsync("GameOver");
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

        private async void OnBackdropTapped(object sender, TappedEventArgs e)
        {
            await CloseAsync();
        }

        private async Task CloseAsync()
        {
            if (_isClosing) return;
            _isClosing = true;

            await PoppingOut();
            await Navigation.PopModalAsync(false);
        }

        private async void OnGameOverClicked(object sender, EventArgs e)
        {
            if (_isClosing) return;
            _isClosing = true;

            var user = GameManager.Instance.CurrentUser;
            user.isGameOver = true;
            user.Cash = 0;

            // Reset all companies owned by this player
            var myCompanies = GameManager.Instance.Companys.Where(c => c.UserName == user.Name).ToList();
            foreach (var comp in myCompanies)
            {
                comp.Reset();
            }
            user.Companys.Clear();
            user.Companys.Add(new Company { IsNoEmty = false });

            // Broadcast GameOver to other players
            await GameManager.Instance.BroadcastGameOverAsync(user.Id);

            // Add history
            await GameManager.Instance.AddHistoryAsync($"Игрок {user.Name} объявил себя банкротом", "bankrot.png", 0, false);

            await PoppingOut();
            await Navigation.PopModalAsync(false);
        }
    }
}
