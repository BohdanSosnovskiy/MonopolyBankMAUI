using MonopolyBank.Models;
using MonopolyBank.Networking;
using MonopolyBank.Services;

namespace MonopolyBank.Views
{
    public partial class PageObmenCompanyUser : ContentPage
    {
        private readonly User _targetUser;
        private readonly User _currentUser;

        private bool _isMyReady = false;
        private bool _isTargetReady = false;
        private bool _isUpdatingNetwork = false;
        private bool _isClosing = false;

        public PageObmenCompanyUser(User targetUser)
        {
            InitializeComponent();
            _targetUser = targetUser;
            _currentUser = GameManager.Instance.CurrentUser;

            MyNameLabel.Text = _currentUser.Name;
            TargetNameLabel.Text = _targetUser.Name;
            TargetSectionTitle.Text = $"Активы игрока {_targetUser.Name}";

            // Reset Sdelka flags
            ResetSdelkaFlags();

            MyCompaniesList.ItemsSource = _currentUser.Companys.Where(c => c.IsNoEmty).ToList();
            TargetCompaniesList.ItemsSource = _targetUser.Companys.Where(c => c.IsNoEmty).ToList();

            // Subscribe to trade network events
            GameManager.Instance.OnTradeUpdated += HandleTradeUpdated;
            GameManager.Instance.OnTradeClosed += HandleTradeClosed;
            GameManager.Instance.OnTradeConfirmed += HandleTradeConfirmed;

            UpdateReadyVisuals();
            UpdateTradeSums();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            UnsubscribeEvents();
        }

        protected override bool OnBackButtonPressed()
        {
            OnCancelClicked(this, EventArgs.Empty);
            return true;
        }

        private void UnsubscribeEvents()
        {
            GameManager.Instance.OnTradeUpdated -= HandleTradeUpdated;
            GameManager.Instance.OnTradeClosed -= HandleTradeClosed;
            GameManager.Instance.OnTradeConfirmed -= HandleTradeConfirmed;
        }

        private void ResetSdelkaFlags()
        {
            foreach (var c in _currentUser.Companys) c.Sdelka = false;
            foreach (var c in _targetUser.Companys) c.Sdelka = false;
        }

        private async void OnMyCompanySelected(object sender, TappedEventArgs e)
        {
            if (_isClosing) return;
            if (e.Parameter is Company company)
            {
                company.Sdelka = !company.Sdelka;
                _isMyReady = false;
                UpdateReadyVisuals();
                UpdateTradeSums();
                await BroadcastTradeUpdateAsync();
            }
        }

        private async void OnTargetCompanySelected(object sender, TappedEventArgs e)
        {
            if (_isClosing) return;
            if (e.Parameter is Company company)
            {
                company.Sdelka = !company.Sdelka;
                _isMyReady = false;
                UpdateReadyVisuals();
                UpdateTradeSums();
                await BroadcastTradeUpdateAsync();
            }
        }

        private async void OnMoneyChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingNetwork || _isClosing) return;
            _isMyReady = false;
            UpdateReadyVisuals();
            UpdateTradeSums();
            await BroadcastTradeUpdateAsync();
        }

        private void UpdateTradeSums()
        {
            int.TryParse(MyMoneyEntry.Text, out var myMoney);
            int.TryParse(TargetMoneyEntry.Text, out var targetMoney);

            var myCompaniesSum = _currentUser.Companys.Where(c => c.Sdelka).Sum(c => c.Price);
            var targetCompaniesSum = _targetUser.Companys.Where(c => c.Sdelka).Sum(c => c.Price);

            MySumLabel.Text = $"Сумма сделки: {myCompaniesSum + myMoney} $";
            TargetSumLabel.Text = $"Сумма сделки: {targetCompaniesSum + targetMoney} $";
        }

        private void UpdateReadyVisuals()
        {
            MyReadyLabel.Text = _isMyReady ? "Готов" : "Не готов";
            TargetReadyLabel.Text = _isTargetReady ? "Готов" : "Не готов";

            if (_isMyReady && !_isTargetReady)
            {
                BtnConfirmTrade.Text = $"Ожидание игрока {_targetUser.Name}...";
                BtnConfirmTrade.BackgroundColor = Color.FromArgb("#FF9800"); // Orange
            }
            else if (_isMyReady && _isTargetReady)
            {
                BtnConfirmTrade.Text = "Сделка подтверждена!";
                BtnConfirmTrade.BackgroundColor = Color.FromArgb("#4CAF50"); // Green
            }
            else
            {
                BtnConfirmTrade.Text = "Заключить сделку";
                BtnConfirmTrade.BackgroundColor = Color.FromArgb("#3b7d3b");
            }
        }

        private async Task BroadcastTradeUpdateAsync()
        {
            if (_isUpdatingNetwork || _isClosing) return;

            try
            {
                int.TryParse(MyMoneyEntry.Text, out var myMoney);
                int.TryParse(TargetMoneyEntry.Text, out var targetMoney);

                var mySelected = _currentUser.Companys.Where(c => c.Sdelka).Select(c => c.index).ToList();
                var targetSelected = _targetUser.Companys.Where(c => c.Sdelka).Select(c => c.index).ToList();

                var payload = new TradeUpdatePayload
                {
                    TargetUserId = _targetUser.Id,
                    SendingMoney = myMoney,
                    TargetMoney = targetMoney,
                    OfferedCompanyIndexes = mySelected,
                    RequestedCompanyIndexes = targetSelected,
                    IsReadySender = _isMyReady
                };

                await GameManager.Instance.SendTradeUpdateAsync(payload);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BroadcastTradeUpdate Error] {ex.Message}");
            }
        }

        private void HandleTradeUpdated(TradeUpdatePayload payload)
        {
            if (payload.TargetUserId != _currentUser.Id || _isClosing) return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                _isUpdatingNetwork = true;
                try
                {
                    // The sender offered these companies (they belong to _targetUser on our side)
                    foreach (var c in _targetUser.Companys)
                    {
                        c.Sdelka = payload.OfferedCompanyIndexes.Contains(c.index);
                    }

                    // The sender requested these companies (they belong to _currentUser on our side)
                    foreach (var c in _currentUser.Companys)
                    {
                        c.Sdelka = payload.RequestedCompanyIndexes.Contains(c.index);
                    }

                    // What sender offers to give us appears in TargetMoneyEntry
                    TargetMoneyEntry.Text = payload.SendingMoney.ToString();

                    // What sender requests from us appears in MyMoneyEntry
                    MyMoneyEntry.Text = payload.TargetMoney.ToString();

                    _isTargetReady = payload.IsReadySender;
                    UpdateReadyVisuals();
                    UpdateTradeSums();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[HandleTradeUpdated Error] {ex.Message}");
                }
                finally
                {
                    _isUpdatingNetwork = false;
                }
            });
        }

        private void HandleTradeClosed()
        {
            if (_isClosing) return;
            _isClosing = true;

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                UnsubscribeEvents();
                ResetSdelkaFlags();
                try
                {
                    await Navigation.PopModalAsync();
                    await PopUpAlert.ShowAlertAsync(Application.Current?.MainPage?.Navigation, "Сделка", $"Игрок {_targetUser.Name} отменил сделку", "OK");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[TradeClosed Error] {ex.Message}");
                }
            });
        }

        private void HandleTradeConfirmed(TradeExecutedPayload payload)
        {
            if (_isClosing) return;
            if (payload.User1Id == _currentUser.Id || payload.User2Id == _currentUser.Id)
            {
                _isClosing = true;
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    UnsubscribeEvents();
                    ResetSdelkaFlags();
                    _ = SoundService.Instance.PlaySoundAsync("kassa");
                    try
                    {
                        var mainNav = Application.Current?.MainPage?.Navigation ?? Navigation;
                        await Navigation.PopModalAsync(false);
                        await Task.Delay(50);
                        await mainNav.PushModalAsync(new PopUpConfirmSdelka(_targetUser.Name), false);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[TradeConfirmed Error] {ex.Message}");
                    }
                });
            }
        }

        private async void OnCancelClicked(object sender, EventArgs e)
        {
            if (_isClosing) return;
            _isClosing = true;

            UnsubscribeEvents();
            ResetSdelkaFlags();
            await GameManager.Instance.SendTradeCloseAsync(_targetUser.Id);
            try
            {
                await Navigation.PopModalAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OnCancelClicked Error] {ex.Message}");
            }
        }

        private async void OnConfirmTradeClicked(object sender, EventArgs e)
        {
            if (_isClosing) return;

            int.TryParse(MyMoneyEntry.Text, out var myMoney);
            int.TryParse(TargetMoneyEntry.Text, out var targetMoney);

            if (myMoney > _currentUser.Cash)
            {
                await PopUpAlert.ShowAlertAsync(Navigation, "Ошибка", $"У вас недостаточно денег для доплаты ({_currentUser.Cash} $)", "OK");
                return;
            }

            if (targetMoney > _targetUser.Cash)
            {
                await PopUpAlert.ShowAlertAsync(Navigation, "Ошибка", $"У игрока {_targetUser.Name} недостаточно денег ({_targetUser.Cash} $)", "OK");
                return;
            }

            var toTarget = _currentUser.Companys.Where(c => c.Sdelka).Select(c => c.index).ToList();
            var toCurrent = _targetUser.Companys.Where(c => c.Sdelka).Select(c => c.index).ToList();

            if (toTarget.Count == 0 && toCurrent.Count == 0 && myMoney == 0 && targetMoney == 0)
            {
                await PopUpAlert.ShowAlertAsync(Navigation, "Внимание", "Вы не выбрали ни одного предприятия и не указали сумму", "OK");
                return;
            }

            // Toggle ready state
            _isMyReady = !_isMyReady;
            UpdateReadyVisuals();
            await BroadcastTradeUpdateAsync();

            // If both players are ready, finalize trade
            if (_isMyReady && _isTargetReady)
            {
                _isClosing = true;

                var tradePayload = new TradeExecutedPayload
                {
                    User1Id = _currentUser.Id,
                    User1NewCash = _currentUser.Cash - myMoney + targetMoney,
                    User2Id = _targetUser.Id,
                    User2NewCash = _targetUser.Cash - targetMoney + myMoney,
                    TransferredToUser1 = toCurrent,
                    TransferredToUser2 = toTarget
                };

                await GameManager.Instance.ExecuteTradeAsync(tradePayload);
                _ = SoundService.Instance.PlaySoundAsync("kassa");
                await GameManager.Instance.AddHistoryAsync($"Игроки {_currentUser.Name} и {_targetUser.Name} заключили сделку", "sdelka_logo.png", 0, false);

                UnsubscribeEvents();
                ResetSdelkaFlags();
                try
                {
                    var mainNav = Application.Current?.MainPage?.Navigation ?? Navigation;
                    await Navigation.PopModalAsync(false);
                    await Task.Delay(50);
                    await mainNav.PushModalAsync(new PopUpConfirmSdelka(_targetUser.Name), false);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ExecuteTrade Error] {ex.Message}");
                }
            }
        }
    }
}
