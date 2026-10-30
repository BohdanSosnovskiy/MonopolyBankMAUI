using MonopolyBank.Networking;
using MonopolyBank.Services;

namespace MonopolyBank.Views
{
    public partial class JoinGamePage : ContentPage
    {
        private bool _isScanning = false;

        public JoinGamePage()
        {
            InitializeComponent();
            var savedIp = Preferences.Get("LastHostIp", "");
            if (!string.IsNullOrEmpty(savedIp))
            {
                Ipserver.Text = savedIp;
            }
            var savedName = Preferences.Get("ClientPlayerName", "Игрок 2");
            Username.Text = savedName;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _ = ScanLocalNetworkAsync();
        }

        private async void OnRefreshDiscoveryClicked(object sender, EventArgs e)
        {
            await ScanLocalNetworkAsync();
        }

        private async Task ScanLocalNetworkAsync()
        {
            if (_isScanning) return;
            _isScanning = true;

            ScanningLayout.IsVisible = true;
            DiscoveredList.IsVisible = false;
            BtnRefreshDiscovery.IsEnabled = false;

            try
            {
                var servers = await UdpDiscoveryClient.DiscoverServersAsync(2000);
                DiscoveredList.ItemsSource = servers;
                DiscoveredList.IsVisible = true;

                // If only 1 server discovered and manual IP is empty, auto-fill it
                if (servers.Count == 1 && string.IsNullOrEmpty(Ipserver.Text))
                {
                    Ipserver.Text = servers[0].IpAddress;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ScanLocalNetworkAsync] {ex.Message}");
            }
            finally
            {
                ScanningLayout.IsVisible = false;
                BtnRefreshDiscovery.IsEnabled = true;
                _isScanning = false;
            }
        }

        private async void OnJoinDiscoveredClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is DiscoveredServer server)
            {
                Ipserver.Text = server.IpAddress;
                await ConnectToServerAsync(server.IpAddress);
            }
        }

        private async void OnStartJoinGame(object sender, EventArgs e)
        {
            var ip = Ipserver.Text?.Trim();
            if (string.IsNullOrEmpty(ip))
            {
                await PopUpAlert.ShowAlertAsync(Navigation, "Ошибка", "Пожалуйста, выберите найденный сервер или введите IP адрес", "OK");
                return;
            }

            await ConnectToServerAsync(ip);
        }

        private async Task ConnectToServerAsync(string ip)
        {
            var name = string.IsNullOrWhiteSpace(Username.Text) ? "Игрок" : Username.Text.Trim();

            Preferences.Set("LastHostIp", ip);
            Preferences.Set("ClientPlayerName", name);

            BtnJoin.IsEnabled = false;
            BtnJoin.Text = "Подключение...";

            bool success = await GameManager.Instance.JoinGameAsync(ip, name);
            if (success)
            {
                await Navigation.PushAsync(new GameMainPage());
            }
            else
            {
                await PopUpAlert.ShowAlertAsync(Navigation, "Ошибка подключения", $"Не удалось подключиться к серверу по адресу {ip}. Убедитесь, что оба устройства в одной Wi-Fi сети.", "OK");
                BtnJoin.IsEnabled = true;
                BtnJoin.Text = "Присоединиться к банку";
            }
        }
    }
}
