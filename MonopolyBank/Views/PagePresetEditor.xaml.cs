using MonopolyBank.Models;
using MonopolyBank.Services;

namespace MonopolyBank.Views
{
    public partial class PagePresetEditor : ContentPage
    {
        private readonly MonopolyPreset _preset;

        public PagePresetEditor(MonopolyPreset preset)
        {
            InitializeComponent();
            _preset = preset;

            PresetNameEntry.Text = _preset.Name;
            StartCashEntry.Text = _preset.Settings.DefaultStartCash.ToString();
            PassGoEntry.Text = _preset.Settings.PassGoCash.ToString();
            JailFeeEntry.Text = _preset.Settings.JailFee.ToString();

            RefreshCompaniesList();
        }

        private void RefreshCompaniesList()
        {
            CompaniesCollection.ItemsSource = null;
            CompaniesCollection.ItemsSource = _preset.Companies;
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            _preset.Name = string.IsNullOrWhiteSpace(PresetNameEntry.Text) ? "Моя Монополия" : PresetNameEntry.Text.Trim();

            if (int.TryParse(StartCashEntry.Text, out var sc)) _preset.Settings.DefaultStartCash = sc;
            if (int.TryParse(PassGoEntry.Text, out var pg)) _preset.Settings.PassGoCash = pg;
            if (int.TryParse(JailFeeEntry.Text, out var jf)) _preset.Settings.JailFee = jf;

            await PresetManager.Instance.SavePresetAsync(_preset);
            await DisplayAlert("Сохранено", $"Шаблон «{_preset.Name}» успешно сохранен!", "ОК");
            await Navigation.PopAsync();
        }

        private async void OnAddCompanyClicked(object sender, EventArgs e)
        {
            string name = await DisplayPromptAsync("Новая карточка", "Введите название предприятия:", initialValue: $"Предприятие {_preset.Companies.Count + 1}");
            if (string.IsNullOrWhiteSpace(name)) return;

            string priceStr = await DisplayPromptAsync("Цена", "Стоимость предприятия:", initialValue: "500", keyboard: Keyboard.Numeric);
            int.TryParse(priceStr, out var price);
            if (price <= 0) price = 500;

            var newComp = new Company
            {
                index = _preset.Companies.Count,
                Name = name.Trim(),
                Price = price,
                PriceFilial = Math.Max(50, price / 2),
                PriceCorp = Math.Max(50, price / 2),
                Deposit = price / 2,
                Selling = price,
                color = "#3b7d3b",
                CountColorGroup = 3,
                nfc_marker = $"street_{_preset.Companies.Count + 1}",
                Orend = new int[] { Math.Max(10, price / 10), price / 2, price, price * 2, price * 3, 0 },
                IsCompany = true,
                IsCorp = false
            };

            _preset.Companies.Add(newComp);
            RefreshCompaniesList();
        }

        private async void OnEditCompanyClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is Company company)
            {
                await Navigation.PushAsync(new PageEditCard(company, () =>
                {
                    RefreshCompaniesList();
                }));
            }
        }

        private async void OnWriteNfcClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is Company company)
            {
                await Navigation.PushModalAsync(new PopUpNfcWriter(company), false);
            }
        }

        private async void OnBatchNfcClicked(object sender, EventArgs e)
        {
            if (_preset.Companies.Count == 0)
            {
                await DisplayAlert("Внимание", "В монополии пока нет карточек для прошивки", "ОК");
                return;
            }

            bool start = await DisplayAlert("Экспресс-прошивка NFC", 
                $"Мастер поможет быстро привязать NFC метки ко всем {_preset.Companies.Count} карточкам по очереди.\n\nПрикладывайте карточки к телефону одну за другой.", 
                "Начать прошивку", "Отмена");

            if (start)
            {
                await Navigation.PushModalAsync(new PopUpBatchNfcWriter(_preset.Companies), false);
            }
        }
    }
}
