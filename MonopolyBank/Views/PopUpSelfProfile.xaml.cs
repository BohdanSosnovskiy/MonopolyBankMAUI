using MonopolyBank.Models;
using MonopolyBank.Services;

namespace MonopolyBank.Views
{
    public partial class PopUpSelfProfile : ContentPage
    {
        private bool _isClosing = false;

        public PopUpSelfProfile()
        {
            InitializeComponent();
            Background = Colors.Transparent;
            if (Content != null) Content.TranslationY = 800;

            var user = GameManager.Instance.CurrentUser;
            PlayerNameLabel.Text = user.Name;
            PlayerCashLabel.Text = $"{user.Cash:N0} $";

            var companiesCount = user.Companys.Count(c => c.IsNoEmty);
            PlayerCompaniesCountLabel.Text = $"Предприятий во владении: {companiesCount}";

            HistoryList.ItemsSource = GameManager.Instance.Historys.ToList();

            if (user.isGameOver)
            {
                SurrenderBtn.IsEnabled = false;
                SurrenderBtn.Text = "Банкрот";
            }

            EndGameBtn.Text = GameManager.Instance.IsHost ? "Завершить игру (Сервер)" : "Покинуть игру";

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
            var windowHeight = 800;

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

        private async void OnCloseClicked(object sender, EventArgs e)
        {
            await CloseAsync();
        }

        private async void OnBackdropTapped(object sender, TappedEventArgs e)
        {
            await CloseAsync();
        }

        private async void OnSurrenderClicked(object sender, EventArgs e)
        {
            var confirm = await PopUpAlert.ShowConfirmAsync(
                Navigation,
                "Объявить банкротство?",
                "Вы действительно хотите сдаться и покинуть игру? Все ваши предприятия станут ничейными, постройки аннулируются, а баланс обнулится.",
                "Да, сдаться",
                "Отмена"
            );

            if (confirm)
            {
                var user = GameManager.Instance.CurrentUser;
                user.isGameOver = true;
                user.Cash = 0;

                // Reset all owned companies
                var myCompanies = GameManager.Instance.Companys.Where(c => c.UserName == user.Name).ToList();
                foreach (var c in myCompanies)
                {
                    c.Reset();
                }
                user.Companys.Clear();
                user.Companys.Add(new Company { IsNoEmty = false });

                // Broadcast GameOver
                await GameManager.Instance.BroadcastGameOverAsync(user.Id);
                await GameManager.Instance.AddHistoryAsync($"Игрок {user.Name} сдался и объявил себя банкротом", "bankrot.png", 0, false);

                await CloseAsync();
            }
        }

        private async void OnEndGameClicked(object sender, EventArgs e)
        {
            if (GameManager.Instance.IsHost)
            {
                var confirm = await PopUpAlert.ShowConfirmAsync(
                    Navigation,
                    "Завершение игры",
                    "Вы действительно хотите завершить игру и остановить сервер для всех участников?",
                    "Да, завершить",
                    "Отмена"
                );

                if (confirm)
                {
                    await GameManager.Instance.EndGameAsync();
                    await CloseAsync();
                    await Application.Current?.MainPage?.Navigation.PopToRootAsync();
                }
            }
            else
            {
                var confirm = await PopUpAlert.ShowConfirmAsync(
                    Navigation,
                    "Выход из игры",
                    "Покинуть текущую комнату?",
                    "Выйти",
                    "Отмена"
                );

                if (confirm)
                {
                    await GameManager.Instance.EndGameAsync();
                    await CloseAsync();
                    await Application.Current?.MainPage?.Navigation.PopToRootAsync();
                }
            }
        }
    }
}
