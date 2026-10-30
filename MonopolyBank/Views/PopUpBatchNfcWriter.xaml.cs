using MonopolyBank.Models;
using MonopolyBank.Services;
#if ANDROID || IOS
using Plugin.NFC;
#endif

namespace MonopolyBank.Views
{
    public partial class PopUpBatchNfcWriter : ContentPage
    {
        private readonly List<Company> _companies;
        private int _currentIndex = 0;
        private bool _isListening = false;

        public PopUpBatchNfcWriter(List<Company> companies)
        {
            InitializeComponent();
            _companies = companies;
            ShowCurrentCompany();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            StartListening();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            StopListening();
        }

        private void ShowCurrentCompany()
        {
            if (_currentIndex < 0 || _currentIndex >= _companies.Count)
            {
                FinishBatch();
                return;
            }

            var comp = _companies[_currentIndex];
            StepLabel.Text = $"Карточка {_currentIndex + 1} из {_companies.Count}";
            CompanyNameLabel.Text = comp.Name;
            CompanyPriceLabel.Text = $"Цена: {comp.Price} $";
            InstructionLabel.Text = "👉 Приложите метку для этой карточки";
            InstructionLabel.TextColor = Color.FromArgb("#0daaa3");

            try
            {
                if (!string.IsNullOrEmpty(comp.color) && comp.color != "Transparent")
                {
                    CompanyBorder.BackgroundColor = Color.FromArgb(comp.color);
                }
            }
            catch { }

            BatchProgress.Progress = (double)(_currentIndex + 1) / _companies.Count;
        }

        private void StartListening()
        {
#if ANDROID || IOS
            try
            {
                if (CrossNFC.Current.IsAvailable && CrossNFC.Current.IsEnabled)
                {
                    CrossNFC.Current.OnMessageReceived += OnNfcReceived;
                    CrossNFC.Current.StartListening();
                    _isListening = true;
                }
            }
            catch { }
#endif
        }

        private void StopListening()
        {
#if ANDROID || IOS
            try
            {
                if (_isListening)
                {
                    CrossNFC.Current.OnMessageReceived -= OnNfcReceived;
                    CrossNFC.Current.StopListening();
                    _isListening = false;
                }
            }
            catch { }
#endif
        }

#if ANDROID || IOS
        private void OnNfcReceived(ITagInfo tagInfo)
        {
            if (_currentIndex >= _companies.Count) return;

            string detectedToken = "";
            if (tagInfo.Records != null && tagInfo.Records.Length > 0 && !string.IsNullOrEmpty(tagInfo.Records[0].Message))
            {
                detectedToken = tagInfo.Records[0].Message.Trim();
            }

            if (string.IsNullOrEmpty(detectedToken))
            {
                detectedToken = tagInfo.Identifier != null ? Convert.ToHexString(tagInfo.Identifier) : $"tag_{Guid.NewGuid():N}";
            }

            var currentComp = _companies[_currentIndex];
            currentComp.nfc_marker = detectedToken;

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                InstructionLabel.Text = $"✅ Записано: [{detectedToken}]";
                InstructionLabel.TextColor = Color.FromArgb("#55b155");

                _ = SoundService.Instance.PlaySoundAsync("kassa");

                await Task.Delay(800);
                _currentIndex++;
                ShowCurrentCompany();
            });
        }
#endif

        private void OnSkipClicked(object sender, EventArgs e)
        {
            _currentIndex++;
            ShowCurrentCompany();
        }

        private async void FinishBatch()
        {
            StopListening();
            await DisplayAlert("Готово!", "Все карточки успешно обработаны и привязаны к NFC меткам!", "Ура!");
            await Navigation.PopModalAsync(false);
        }

        private async void OnCancelClicked(object sender, EventArgs e)
        {
            await Navigation.PopModalAsync(false);
        }
    }
}
