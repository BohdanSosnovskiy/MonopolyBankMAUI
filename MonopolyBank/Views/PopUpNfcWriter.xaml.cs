using MonopolyBank.Models;
using MonopolyBank.Services;
#if ANDROID || IOS
using Plugin.NFC;
#endif

namespace MonopolyBank.Views
{
    public partial class PopUpNfcWriter : ContentPage
    {
        private readonly Company _company;
        private bool _isListening = false;

        public PopUpNfcWriter(Company company)
        {
            InitializeComponent();
            _company = company;
            CardNameLabel.Text = $"Карточка: {_company.Name}";
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
                else
                {
                    StatusLabel.Text = "NFC недоступен или выключен в настройках телефона.";
                    ProgressIndicator.IsRunning = false;
                }
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Ошибка NFC: {ex.Message}";
                ProgressIndicator.IsRunning = false;
            }
#else
            StatusLabel.Text = "NFC поддерживается только на мобильных устройствах.";
            ProgressIndicator.IsRunning = false;
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
        private async void OnNfcReceived(ITagInfo tagInfo)
        {
            try
            {
                // 1. Пытаемся взять текст из NDEF, либо аппаратный UID
                string detectedToken = "";
                if (tagInfo.Records != null && tagInfo.Records.Length > 0 && !string.IsNullOrEmpty(tagInfo.Records[0].Message))
                {
                    detectedToken = tagInfo.Records[0].Message.Trim();
                }

                if (string.IsNullOrEmpty(detectedToken))
                {
                    // Используем аппаратный серийный номер метки (UID)
                    detectedToken = tagInfo.Identifier != null ? Convert.ToHexString(tagInfo.Identifier) : $"tag_{Guid.NewGuid():N}";
                }

                _company.nfc_marker = detectedToken;

                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    ProgressIndicator.IsRunning = false;
                    StatusLabel.Text = $"✅ Успешно привязано!\nМетка: [{detectedToken}]";
                    StatusLabel.TextColor = Color.FromArgb("#0daaa3");

                    _ = SoundService.Instance.PlaySoundAsync("kassa");

                    await Task.Delay(1400);
                    await Navigation.PopModalAsync(false);
                });
            }
            catch (Exception ex)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    StatusLabel.Text = $"Ошибка чтения метки: {ex.Message}";
                });
            }
        }
#endif

        private async void OnCancelClicked(object sender, EventArgs e)
        {
            await Navigation.PopModalAsync(false);
        }
    }
}
