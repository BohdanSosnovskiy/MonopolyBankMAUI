using MonopolyBank.Services;

namespace MonopolyBank.Views
{
    public partial class NewGamePage : ContentPage
    {
        public NewGamePage()
        {
            InitializeComponent();
            var savedName = Preferences.Get("HostPlayerName", "Игрок 1");
            Username.Text = savedName;
            NoNfcCheckBox.IsChecked = Preferences.Get("PlayWithoutNfc", false);
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            HostIpLabel.Text = GameManager.GetLocalIpAddress();
        }

        private async void OnStartNewGame(object sender, EventArgs e)
        {
            var name = string.IsNullOrWhiteSpace(Username.Text) ? "Игрок 1" : Username.Text.Trim();
            Preferences.Set("HostPlayerName", name);

            int.TryParse(CountCashStart.Text, out var startCash);
            if (startCash <= 0) startCash = 15000;

            int.TryParse(CountStartSum.Text, out var passGoCash);
            if (passGoCash <= 0) passGoCash = 200;

            bool isNoNfc = NoNfcCheckBox.IsChecked;
            Preferences.Set("PlayWithoutNfc", isNoNfc);

            BtnStartGame.IsEnabled = false;
            BtnStartGame.Text = "Запуск сервера...";
            StartingLayout.IsVisible = true;

            try
            {
                await GameManager.Instance.StartHostGameAsync(name, startCash, passGoCash, isNoNfc);
                await Navigation.PushAsync(new GameMainPage());
            }
            finally
            {
                BtnStartGame.IsEnabled = true;
                BtnStartGame.Text = "Создать и войти в банк";
                StartingLayout.IsVisible = false;
            }
        }
    }
}
