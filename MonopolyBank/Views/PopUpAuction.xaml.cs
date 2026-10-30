using MonopolyBank.Models;
using MonopolyBank.Networking;
using MonopolyBank.Services;

namespace MonopolyBank.Views
{
    public partial class PopUpAuction : ContentPage
    {
        private readonly int _companyIndex;
        private readonly Company? _company;
        private readonly string _initiatorId;
        private readonly string _initiatorName;
        private int _currentBid;
        private string? _leadingUserId;
        private string? _leadingUserName;
        private int _remainingSeconds = 15;
        private const int MaxSeconds = 15;
        private IDispatcherTimer? _timer;
        private bool _hasPassed = false;
        private bool _isEnding = false;
        private bool _hasHandledEnd = false;
        private bool _isBiddingBusy = false;

        public PopUpAuction(AuctionStartPayload startPayload)
        {
            InitializeComponent();

            Content.TranslationY = 800;
            Background = Colors.Transparent;

            _companyIndex = startPayload.CompanyIndex;
            _initiatorId = startPayload.InitiatorUserId;
            _initiatorName = startPayload.InitiatorName;

            if (_companyIndex >= 0 && _companyIndex < GameManager.Instance.Companys.Count)
            {
                _company = GameManager.Instance.Companys[_companyIndex];
            }

            // Starting bid is half the company price if not specified
            _currentBid = startPayload.StartingBid > 0 
                ? startPayload.StartingBid 
                : (_company != null ? Math.Max(1, _company.Price / 2) : 10);

            // Initiator is the leader by default
            _leadingUserId = startPayload.InitiatorUserId;
            _leadingUserName = startPayload.InitiatorName;

            SetupInitialView(startPayload);
        }

        private void SetupInitialView(AuctionStartPayload startPayload)
        {
            if (_company != null)
            {
                CompanyNameLabel.Text = _company.Name;
                CompanyNominalLabel.Text = $"Номинал: {_company.Price} $";

                try
                {
                    if (!string.IsNullOrEmpty(_company.color) && _company.color != "Transparent")
                    {
                        CompanyColorBorder.BackgroundColor = Color.FromArgb(_company.color);
                    }
                    else
                    {
                        CompanyColorBorder.BackgroundColor = Color.FromArgb("#27283d");
                    }
                }
                catch
                {
                    CompanyColorBorder.BackgroundColor = Color.FromArgb("#27283d");
                }
            }

            InitiatorLabel.Text = $"Организатор: {_initiatorName}";
            CurrentBidLabel.Text = $"{_currentBid} $";
            LeaderLabel.Text = $"Лидер: {_leadingUserName}";

            FinishBtn.IsVisible = GameManager.Instance.IsHost || GameManager.Instance.CurrentUser.Id == _initiatorId;

            UpdateBidButtonsState();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            GameManager.Instance.OnAuctionBidReceived += HandleBidReceived;
            GameManager.Instance.OnAuctionEnded += HandleAuctionEnded;

            _timer = Dispatcher.CreateTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += OnTimerTick;
            _timer.Start();

            await Task.WhenAll(
                this.FadeTo(1, 200),
                Content.TranslateTo(0, 0, 300, Easing.CubicOut)
            );
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            StopTimer();
            GameManager.Instance.OnAuctionBidReceived -= HandleBidReceived;
            GameManager.Instance.OnAuctionEnded -= HandleAuctionEnded;
        }

        private void StopTimer()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Tick -= OnTimerTick;
                _timer = null;
            }
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            if (_isEnding) return;

            _remainingSeconds--;
            if (_remainingSeconds < 0) _remainingSeconds = 0;

            TimerLabel.Text = $"⏱ Ожидание ставок: {_remainingSeconds} сек";
            TimerProgress.Progress = (double)_remainingSeconds / MaxSeconds;

            if (_remainingSeconds == 0)
            {
                StopTimer();
                FinalizeAuction();
            }
        }

        private async void FinalizeAuction()
        {
            if (_isEnding) return;
            _isEnding = true;
            StopTimer();

            // Host or Initiator finalizes the auction
            if (GameManager.Instance.IsHost || GameManager.Instance.CurrentUser.Id == _initiatorId || GameManager.Instance.Users.Count <= 1)
            {
                await GameManager.Instance.SendAuctionEndAsync(_companyIndex, _leadingUserId, _currentBid);
            }
            else
            {
                // Fallback for client: if after 2s host didn't end it, client ensures finish
                await Task.Delay(2000);
                if (GameManager.Instance.IsAuctionRunning)
                {
                    await GameManager.Instance.SendAuctionEndAsync(_companyIndex, _leadingUserId, _currentBid);
                }
            }
        }

        private void HandleBidReceived(AuctionBidPayload bid)
        {
            if (bid.CompanyIndex != _companyIndex || _isEnding) return;
            if (bid.BidAmount <= _currentBid) return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                _currentBid = bid.BidAmount;
                _leadingUserId = bid.BidderUserId;
                _leadingUserName = bid.BidderName;

                CurrentBidLabel.Text = $"{_currentBid} $";
                LeaderLabel.Text = $"Лидер: {_leadingUserName}";

                // Reset timer for another 15 seconds so other players have time to respond
                _remainingSeconds = MaxSeconds;
                TimerLabel.Text = $"⏱ Ожидание ставок: {_remainingSeconds} сек";
                TimerProgress.Progress = 1.0;

                // Sound notification
                _ = SoundService.Instance.PlaySoundAsync("coindrop");

                UpdateBidButtonsState();
            });
        }

        private void HandleAuctionEnded(AuctionEndPayload endPayload)
        {
            if (endPayload.CompanyIndex != _companyIndex) return;
            if (_hasHandledEnd) return;
            _hasHandledEnd = true;
            _isEnding = true;
            StopTimer();

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                // Disable bidding buttons
                Bid10Btn.IsEnabled = false;
                Bid50Btn.IsEnabled = false;
                Bid100Btn.IsEnabled = false;
                Bid500Btn.IsEnabled = false;
                PassBtn.IsEnabled = false;
                FinishBtn.IsEnabled = false;

                Bid10Btn.Opacity = 0.45;
                Bid50Btn.Opacity = 0.45;
                Bid100Btn.Opacity = 0.45;
                Bid500Btn.Opacity = 0.45;

                if (!string.IsNullOrEmpty(endPayload.WinnerUserId))
                {
                    LeaderLabel.Text = $"🏆 ПРОДАНО: {endPayload.WinnerName}";
                    CurrentBidLabel.Text = $"{endPayload.FinalPrice} $";
                    TimerLabel.Text = "Поздравляем победителя!";
                    TimerLabel.TextColor = Color.FromArgb("#3b7d3b");

                    StatusBadgeBorder.BackgroundColor = Color.FromArgb("#eefbee");
                    StatusBadgeLabel.TextColor = Color.FromArgb("#2e7d32");
                    StatusBadgeLabel.Text = $"Фирма достаётся: {endPayload.WinnerName}";

                    _ = SoundService.Instance.PlaySoundAsync("kassa");
                }
                else
                {
                    LeaderLabel.Text = "Торги завершены без покупателя";
                    TimerLabel.Text = "Фирма остаётся в банке";
                    TimerLabel.TextColor = Color.FromArgb("#e73d60");
                }

                await Task.Delay(2000);
                await CloseAsync();
            });
        }

        private async void MakeBid(int increment)
        {
            if (_hasPassed || _isEnding || _isBiddingBusy) return;

            var user = GameManager.Instance.CurrentUser;
            if (_leadingUserId == user.Id)
            {
                await DisplayAlert("Внимание", "Ваша ставка уже лидирует!", "ОК");
                return;
            }

            var newBid = _currentBid + increment;

            if (user.Cash < newBid)
            {
                await DisplayAlert("Недостаточно средств", $"У вас на балансе {user.Cash} $, а для ставки требуется {newBid} $", "ОК");
                return;
            }

            _isBiddingBusy = true;
            UpdateBidButtonsState();

            try
            {
                await GameManager.Instance.SendAuctionBidAsync(_companyIndex, newBid);
            }
            finally
            {
                _isBiddingBusy = false;
                UpdateBidButtonsState();
            }
        }

        private void OnBid10Clicked(object sender, EventArgs e) => MakeBid(10);
        private void OnBid50Clicked(object sender, EventArgs e) => MakeBid(50);
        private void OnBid100Clicked(object sender, EventArgs e) => MakeBid(100);
        private void OnBid500Clicked(object sender, EventArgs e) => MakeBid(500);

        private void OnPassClicked(object sender, EventArgs e)
        {
            var user = GameManager.Instance.CurrentUser;
            if (_leadingUserId == user.Id && !_isEnding)
            {
                DisplayAlert("Внимание", "Вы в данный момент лидируете в торгах и не можете спасовать!", "ОК");
                return;
            }

            _hasPassed = true;
            PassBtn.IsEnabled = false;
            PassBtn.Text = "Вы спасовали";
            UpdateBidButtonsState();
        }

        private async void OnFinishClicked(object sender, EventArgs e)
        {
            if (_isEnding) return;
            _isEnding = true;
            StopTimer();

            await GameManager.Instance.SendAuctionEndAsync(_companyIndex, _leadingUserId, _currentBid);
        }

        private void UpdateBidButtonsState()
        {
            var user = GameManager.Instance.CurrentUser;
            bool isLeader = (_leadingUserId == user.Id);
            bool canBid = !_hasPassed && !_isEnding && !isLeader && !_isBiddingBusy;

            MyBalanceLabel.Text = $"Ваш баланс: {user.Cash} $";

            Bid10Btn.IsEnabled = canBid && (user.Cash >= _currentBid + 10);
            Bid50Btn.IsEnabled = canBid && (user.Cash >= _currentBid + 50);
            Bid100Btn.IsEnabled = canBid && (user.Cash >= _currentBid + 100);
            Bid500Btn.IsEnabled = canBid && (user.Cash >= _currentBid + 500);

            Bid10Btn.Opacity = Bid10Btn.IsEnabled ? 1.0 : 0.45;
            Bid50Btn.Opacity = Bid50Btn.IsEnabled ? 1.0 : 0.45;
            Bid100Btn.Opacity = Bid100Btn.IsEnabled ? 1.0 : 0.45;
            Bid500Btn.Opacity = Bid500Btn.IsEnabled ? 1.0 : 0.45;

            PassBtn.IsEnabled = !_hasPassed && !_isEnding && !isLeader;

            if (_hasPassed)
            {
                StatusBadgeBorder.BackgroundColor = Color.FromArgb("#f1f3f5");
                StatusBadgeLabel.TextColor = Color.FromArgb("#6c757d");
                StatusBadgeLabel.Text = "Вы спасовали (вне торгов)";
                ActionPromptLabel.Text = "Вы спасовали:";
            }
            else if (isLeader)
            {
                StatusBadgeBorder.BackgroundColor = Color.FromArgb("#eefbee");
                StatusBadgeLabel.TextColor = Color.FromArgb("#2e7d32");
                StatusBadgeLabel.Text = $"🟢 Ваша ставка {_currentBid} $ лидирует!";
                ActionPromptLabel.Text = "Ожидание ответов игроков...";
            }
            else
            {
                StatusBadgeBorder.BackgroundColor = Color.FromArgb("#fff8e1");
                StatusBadgeLabel.TextColor = Color.FromArgb("#b26a00");
                StatusBadgeLabel.Text = $"⚠️ {_leadingUserName} лидирует ({_currentBid} $)";
                ActionPromptLabel.Text = "Повысить ставку:";
            }
        }

        private async void OnBackdropTapped(object sender, TappedEventArgs e)
        {
            // Do not dismiss backdrop during active auction if user is leading
            var user = GameManager.Instance.CurrentUser;
            if (_leadingUserId == user.Id || _isEnding) return;

            // Otherwise, treat as pass
            OnPassClicked(this, EventArgs.Empty);
        }

        private async Task CloseAsync()
        {
            try
            {
                await Task.WhenAll(
                    this.FadeTo(0, 150),
                    Content.TranslateTo(0, 800, 200, Easing.CubicIn)
                );
            }
            catch { }
            finally
            {
                try
                {
                    var nav = Navigation?.ModalStack?.Contains(this) == true 
                        ? Navigation 
                        : Application.Current?.MainPage?.Navigation;

                    if (nav != null && nav.ModalStack.Contains(this))
                    {
                        await nav.PopModalAsync(false);
                    }
                }
                catch { }
            }
        }
    }
}
