using MonopolyBank.Services;

namespace MonopolyBank.Views
{
    public partial class PopUpAlert : ContentPage
    {
        public event Action? OnConfirmed;
        public event Action? OnCancelled;

        private TaskCompletionSource<bool>? _tcs;
        private bool _isClosing = false;

        public PopUpAlert(string title, string description, string buttonText = "OK", TaskCompletionSource<bool>? tcs = null)
        {
            InitializeComponent();
            TitleLabel.Text = title;
            DescLabel.Text = description;
            ConfirmBtn.Text = buttonText;
            ConfirmBtn.IsVisible = true;
            ConfirmGrid.IsVisible = false;
            _tcs = tcs;
            PrepareInitialState();
            Loaded += Page_Loaded;
        }

        public PopUpAlert(string title, string description, string acceptText, string cancelText, TaskCompletionSource<bool> tcs)
        {
            InitializeComponent();
            TitleLabel.Text = title;
            DescLabel.Text = description;
            AcceptDualBtn.Text = acceptText;
            CancelDualBtn.Text = cancelText;
            ConfirmBtn.IsVisible = false;
            ConfirmGrid.IsVisible = true;
            _tcs = tcs;
            PrepareInitialState();
            Loaded += Page_Loaded;
        }

        private void PrepareInitialState()
        {
            Background = Colors.Transparent;
            if (Content != null)
            {
                Content.TranslationY = 800;
            }
        }

        public static async Task ShowAlertAsync(INavigation? navigation, string title, string description, string buttonText = "OK")
        {
            var tcs = new TaskCompletionSource<bool>();
            var alert = new PopUpAlert(title, description, buttonText, tcs);

            var nav = navigation ?? Application.Current?.MainPage?.Navigation;
            if (nav != null)
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await nav.PushModalAsync(alert, false);
                });
                await tcs.Task;
            }
        }

        public static async Task<bool> ShowConfirmAsync(INavigation? navigation, string title, string description, string acceptText = "Да", string cancelText = "Отклонить")
        {
            var tcs = new TaskCompletionSource<bool>();
            var alert = new PopUpAlert(title, description, acceptText, cancelText, tcs);

            var nav = navigation ?? Application.Current?.MainPage?.Navigation;
            if (nav != null)
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await nav.PushModalAsync(alert, false);
                });
                return await tcs.Task;
            }
            return false;
        }

        protected override bool OnBackButtonPressed()
        {
            _ = CloseAsync(false);
            return true;
        }

        private void Page_Loaded(object? sender, EventArgs e)
        {
            Loaded -= Page_Loaded;
            PoppingIn();
        }

        public void PoppingIn()
        {
            var contentHeight = 600;
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

        private async Task CloseAsync(bool confirmed)
        {
            if (_isClosing) return;
            _isClosing = true;

            try
            {
                await PoppingOut();
                await Navigation.PopModalAsync(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PopUpAlert CloseAsync] {ex.Message}");
            }
            finally
            {
                _tcs?.TrySetResult(confirmed);
            }
        }

        private async void OnConfirmClicked(object sender, EventArgs e)
        {
            OnConfirmed?.Invoke();
            await CloseAsync(true);
        }

        private async void OnAcceptDualClicked(object sender, EventArgs e)
        {
            OnConfirmed?.Invoke();
            await CloseAsync(true);
        }

        private async void OnCancelDualClicked(object sender, EventArgs e)
        {
            OnCancelled?.Invoke();
            await CloseAsync(false);
        }

        private async void OnBackdropTapped(object sender, TappedEventArgs e)
        {
            OnCancelled?.Invoke();
            await CloseAsync(false);
        }
    }
}
