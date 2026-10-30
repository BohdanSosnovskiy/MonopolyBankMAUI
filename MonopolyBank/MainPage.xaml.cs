using MonopolyBank.Services;
using MonopolyBank.Views;

namespace MonopolyBank
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _ = Task.Run(async () =>
            {
                await Task.Delay(800);
                await MainThread.InvokeOnMainThreadAsync(() => UpdateService.CheckForUpdatesAsync(Navigation));
            });
        }

        private async void OnStartNewGame(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new NewGamePage());
        }

        private async void OnJoinNewGame(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new JoinGamePage());
        }

        private async void OnOpenEditorClicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new PagePresetList());
        }
    }
}
