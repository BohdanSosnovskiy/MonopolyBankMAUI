using MonopolyBank.Models;
using MonopolyBank.Services;

namespace MonopolyBank.Views
{
    public partial class NewGamePage : ContentPage
    {
        private List<MonopolyPreset> _availablePresets = new();

        public NewGamePage()
        {
            InitializeComponent();
            var savedName = Preferences.Get("HostPlayerName", "Игрок 1");
            Username.Text = savedName;
            NoNfcCheckBox.IsChecked = Preferences.Get("PlayWithoutNfc", false);

            LoadPresets();
        }

        private void LoadPresets()
        {
            PresetManager.Instance.LoadAllPresets();
            _availablePresets = PresetManager.Instance.Presets.ToList();

            PresetPicker.ItemsSource = _availablePresets.Select(p => p.Name).ToList();

            var active = PresetManager.Instance.ActivePreset;
            if (active != null)
            {
                var index = _availablePresets.FindIndex(p => p.Id == active.Id);
                if (index >= 0)
                {
                    PresetPicker.SelectedIndex = index;
                }
                else if (_availablePresets.Count > 0)
                {
                    PresetPicker.SelectedIndex = 0;
                }
            }
            else if (_availablePresets.Count > 0)
            {
                PresetPicker.SelectedIndex = 0;
            }
        }

        private void OnPresetPickerSelectedIndexChanged(object? sender, EventArgs e)
        {
            if (PresetPicker.SelectedIndex >= 0 && PresetPicker.SelectedIndex < _availablePresets.Count)
            {
                var selected = _availablePresets[PresetPicker.SelectedIndex];
                PresetManager.Instance.SetActivePreset(selected);
            }
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

            if (PresetPicker.SelectedIndex >= 0 && PresetPicker.SelectedIndex < _availablePresets.Count)
            {
                PresetManager.Instance.SetActivePreset(_availablePresets[PresetPicker.SelectedIndex]);
            }

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
