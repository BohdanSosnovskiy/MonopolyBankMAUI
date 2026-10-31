using MonopolyBank.Models;
using MonopolyBank.Networking;
using MonopolyBank.Services;
#if ANDROID || IOS
using Plugin.NFC;
#endif

namespace MonopolyBank.Views
{
    public partial class GameMainPage : ContentPage
    {
        public GameMainPage()
        {
            InitializeComponent();
            BindingContext = GameManager.Instance;

            Loaded += OnPageLoaded;
            Unloaded += OnPageUnloaded;

            GameManager.Instance.OnTradeRequested += HandleTradeRequested;
            GameManager.Instance.OnGameWon += HandleGameWon;
            GameManager.Instance.OnAuctionStarted += HandleAuctionStarted;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            try { DeviceDisplay.Current.KeepScreenOn = true; } catch { }

            if (!GameManager.Instance.IsHost && GameManager.Instance.Users.Count > 0)
            {
                await GameManager.Instance.EnsureConnectedAndSynchronizedAsync();
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            try { DeviceDisplay.Current.KeepScreenOn = false; } catch { }
        }

        private void OnPageLoaded(object? sender, EventArgs e)
        {
            InitNfc();
        }

        private void OnPageUnloaded(object? sender, EventArgs e)
        {
            StopNfc();
            GameManager.Instance.OnTradeRequested -= HandleTradeRequested;
            GameManager.Instance.OnGameWon -= HandleGameWon;
            GameManager.Instance.OnAuctionStarted -= HandleAuctionStarted;
        }

        private async void OnMyCompanyTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is Company company)
            {
                if (company.IsEmpty)
                {
                    // Если у игрока режим без NFC — сразу открываем окно выбора карточек и действий
                    if (GameManager.Instance.CurrentUser.IsNoNfc)
                    {
                        await Navigation.PushModalAsync(new PopUpCardPicker(this), false);
                        return;
                    }

                    var choice = await DisplayActionSheet(
                        "Действие на доске",
                        "Отмена",
                        null,
                        "🔍 Выбрать поле / карточку (Без NFC)",
                        "📱 Сканировать NFC метку (iOS)",
                        "Выбрать свободную фирму (Покупка / Аукцион)",
                        $"Поле ВПЕРЕД (+{GameManager.Instance.CountStartSum} $)",
                        "Случайная Казна / Шанс",
                        "Инструкция по NFC");

                    if (choice == "🔍 Выбрать поле / карточку (Без NFC)")
                    {
                        await Navigation.PushModalAsync(new PopUpCardPicker(this), false);
                        return;
                    }

                    if (choice == "📱 Сканировать NFC метку (iOS)")
                    {
#if ANDROID || IOS
                        try
                        {
                            if (CrossNFC.Current.IsAvailable)
                            {
                                CrossNFC.Current.StartListening();
                            }
                            else
                            {
                                await DisplayAlert("NFC", "NFC недоступен на этом устройстве.", "ОК");
                            }
                        }
                        catch (Exception ex)
                        {
                            await DisplayAlert("NFC", $"Ошибка запуска сессии NFC: {ex.Message}", "ОК");
                        }
#endif
                        return;
                    }

                    if (choice == "Выбрать свободную фирму (Покупка / Аукцион)")
                    {
                        var freeComps = GameManager.Instance.Companys.Where(c => string.IsNullOrEmpty(c.UserName)).ToList();
                        if (freeComps.Count == 0)
                        {
                            await DisplayAlert("Все фирмы заняты", "Все предприятия на поле уже выкуплены игроками!", "ОК");
                            return;
                        }

                        var names = freeComps.Select(c => $"{c.Name} ({c.Price} $)").ToArray();
                        var selectedName = await DisplayActionSheet("Выберите фирму", "Отмена", null, names);
                        if (!string.IsNullOrEmpty(selectedName) && selectedName != "Отмена")
                        {
                            var selectedComp = freeComps.FirstOrDefault(c => $"{c.Name} ({c.Price} $)" == selectedName);
                            if (selectedComp != null)
                            {
                                await Navigation.PushModalAsync(new PopUpCompany(selectedComp), false);
                            }
                        }
                    }
                    else if (choice == $"Поле ВПЕРЕД (+{GameManager.Instance.CountStartSum} $)")
                    {
                        await ProcessNfcTagAsync("start");
                    }
                    else if (choice == "Случайная Казна / Шанс")
                    {
                        var rnd = new Random();
                        if (rnd.Next(2) == 0)
                        {
                            var kIdx = rnd.Next(1, GameManager.Instance.TreasuryCards.Count + 1);
                            await ProcessNfcTagAsync($"Card_k_{kIdx}");
                        }
                        else
                        {
                            var sIdx = rnd.Next(1, GameManager.Instance.ChanceCards.Count + 1);
                            await ProcessNfcTagAsync($"Card_shans_{sIdx}");
                        }
                    }
                    else if (choice == "Инструкция по NFC")
                    {
                        await Navigation.PushModalAsync(new PopUpAlert("NFC", "Для покупки компании или действия приложите карту к задней панели телефона.", "Понятно"), false);
                    }
                    return;
                }
                await Navigation.PushModalAsync(new PopUpCompanyInfo(company), false);
            }
        }

        private async void OnPlayerTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is User player)
            {
                await Navigation.PushModalAsync(new PopUpInfoUser(player), false);
            }
        }

        private async void OnOpenCardPickerClicked(object? sender, EventArgs e)
        {
            if (Navigation.ModalStack.OfType<PopUpCardPicker>().Any()) return;
            await Navigation.PushModalAsync(new PopUpCardPicker(this), false);
        }

        private void HandleTradeRequested(string senderId, string senderName)
        {
            var senderUser = GameManager.Instance.Users.FirstOrDefault(u => u.Id == senderId || u.Name == senderName);
            if (senderUser != null)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    if (Navigation.ModalStack.OfType<PageObmenCompanyUser>().Any())
                    {
                        return;
                    }

                    _ = SoundService.Instance.PlaySoundAsync("policay");
                    bool accept = await PopUpAlert.ShowConfirmAsync(Navigation, "Предложение сделки", $"Игрок {senderName} предлагает обмен карточками и деньгами. Открыть окно сделки?", "Да", "Отклонить");
                    if (accept)
                    {
                        try
                        {
                            await Task.Delay(100);
                            var mainNav = Application.Current?.MainPage?.Navigation ?? Navigation;
                            while (mainNav.ModalStack.Count > 0)
                            {
                                await mainNav.PopModalAsync(false);
                                await Task.Delay(50);
                            }
                            await mainNav.PushModalAsync(new PageObmenCompanyUser(senderUser), false);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[Open PageObmen error] {ex.Message}");
                        }
                    }
                    else
                    {
                        await GameManager.Instance.SendTradeCloseAsync(senderId);
                    }
                });
            }
        }

        private void HandleGameWon(User winner)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                if (winner.Id == GameManager.Instance.CurrentUser.Id && !GameManager.Instance.CurrentUser.isGameOver)
                {
                    await Navigation.PushModalAsync(new WinnerUser(), false);
                }
            });
        }

        private void HandleAuctionStarted(AuctionStartPayload payload)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                if (Navigation.ModalStack.OfType<PopUpAuction>().Any()) return;

                try
                {
                    var mainNav = Application.Current?.MainPage?.Navigation ?? Navigation;
                    while (mainNav.ModalStack.Count > 0)
                    {
                        await mainNav.PopModalAsync(false);
                        await Task.Delay(50);
                    }

                    _ = SoundService.Instance.PlaySoundAsync("policay");
                    await mainNav.PushModalAsync(new PopUpAuction(payload), false);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Open PopUpAuction error] {ex.Message}");
                }
            });
        }

        #region NFC Обработка

        private void InitNfc()
        {
            if (GameManager.Instance.CurrentUser.IsNoNfc) return;
#if ANDROID || IOS
            try
            {
                if (CrossNFC.Current.IsAvailable && CrossNFC.Current.IsEnabled)
                {
                    CrossNFC.Current.OnMessageReceived -= OnNfcMessageReceived;
                    CrossNFC.Current.OnMessageReceived += OnNfcMessageReceived;
                    CrossNFC.Current.StartListening();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NFC Init] {ex.Message}");
            }
#endif
        }

        private void StopNfc()
        {
#if ANDROID || IOS
            try
            {
                if (CrossNFC.Current.IsAvailable)
                {
                    CrossNFC.Current.OnMessageReceived -= OnNfcMessageReceived;
                    CrossNFC.Current.StopListening();
                }
            }
            catch { }
#endif
        }

        private async void OnMyCardTapped(object sender, TappedEventArgs e)
        {
            if (Navigation.ModalStack.OfType<PopUpSelfProfile>().Any()) return;
            await Navigation.PushModalAsync(new PopUpSelfProfile(), false);
        }

#if ANDROID || IOS
        private async void OnNfcMessageReceived(ITagInfo tagInfo)
        {
            try
            {
                if (tagInfo?.Records == null || tagInfo.Records.Length == 0) return;

                var message = tagInfo.Records[0].Message?.Trim();
                if (string.IsNullOrEmpty(message)) return;

                await ProcessNfcTagAsync(message);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NFC Tag Process] {ex.Message}");
            }
        }
#endif

        public async Task ProcessNfcTagAsync(string message)
        {
            var user = GameManager.Instance.CurrentUser;

            if (message == "start_mon")
            {
                if (user.isGameOver) return;
                _ = SoundService.Instance.PlaySoundAsync("coindrop");

                int bonus = GameManager.Instance.CountStartSum;
                await GameManager.Instance.UpdateBalanceAsync(user.Id, user.Cash + bonus, "Проход Старта");
                await GameManager.Instance.AddHistoryAsync($"Игрок {user.Name} прошел поле Старт (+{bonus}$)", "payment.png", bonus, true);
            }
            else if (message == "pobori")
            {
                if (user.isGameOver) return;
                _ = SoundService.Instance.PlaySoundAsync("gopstop");

                if (user.Cash < 200)
                {
                    if (user.Companys.Count(c => c.IsNoEmty) < 2)
                    {
                        await Navigation.PushModalAsync(new PopUpGameOver(), false);
                    }
                    else
                    {
                        var alert = new PopUpAlert("ПОБОРЫ НА ДОРОГЕ", "У вас недостаточно денег (200$). Продайте филиалы, заложите фирмы или совершите сделку!", "ОК");
                        await Navigation.PushModalAsync(alert, false);
                    }
                }
                else
                {
                    await GameManager.Instance.UpdateBalanceAsync(user.Id, user.Cash - 200, "Поборы 200$");
                    await GameManager.Instance.AddHistoryAsync($"Игрок {user.Name} заплатил поборы 200$", "payment.png", 200, true);
                }
            }
            else if (message == "taska")
            {
                if (user.isGameOver) return;
                _ = SoundService.Instance.PlaySoundAsync("policay");

                if (user.Cash < 100)
                {
                    if (user.Companys.Count(c => c.IsNoEmty) < 2)
                    {
                        await Navigation.PushModalAsync(new PopUpGameOver(), false);
                    }
                    else
                    {
                        var alert = new PopUpAlert("ТАЧКА", "У вас недостаточно денег (100$). Продайте филиалы, заложите фирмы или совершите сделку!", "ОК");
                        await Navigation.PushModalAsync(alert, false);
                    }
                }
                else
                {
                    await GameManager.Instance.UpdateBalanceAsync(user.Id, user.Cash - 100, "Штраф 100$");
                    await GameManager.Instance.AddHistoryAsync($"Игрок {user.Name} оплатил штраф 100$", "pay_bank.png", 100, true);
                }
            }
            else if (message == "turma")
            {
                if (user.isGameOver) return;
                _ = SoundService.Instance.PlaySoundAsync("turma");

                if (user.Cash < 50)
                {
                    if (user.Companys.Count(c => c.IsNoEmty) < 2)
                    {
                        await Navigation.PushModalAsync(new PopUpGameOver(), false);
                    }
                    else
                    {
                        var alert = new PopUpAlert("ТЮРЬМА", "У вас недостаточно денег (50$). Продайте филиалы, заложите фирмы или совершите сделку!", "ОК");
                        await Navigation.PushModalAsync(alert, false);
                    }
                }
                else
                {
                    await GameManager.Instance.UpdateBalanceAsync(user.Id, user.Cash - 50, "Тюрьма 50$");
                    await GameManager.Instance.AddHistoryAsync($"Игрок {user.Name} вышел из тюрьмы под залог 50$", "payment.png", 50, true);
                }
            }
            else if (message.StartsWith("Card_k_"))
            {
                if (user.isGameOver) return;
                if (int.TryParse(message.Replace("Card_k_", ""), out var idx) && idx >= 1 && idx <= GameManager.Instance.TreasuryCards.Count)
                {
                    var card = GameManager.Instance.TreasuryCards[idx - 1];
                    if (user.Cash < card.Sum)
                    {
                        if (user.Companys.Count(c => c.IsNoEmty) < 2)
                        {
                            await Navigation.PushModalAsync(new PopUpGameOver(), false);
                        }
                        else
                        {
                            var alert = new PopUpAlert("КАЗНА", $"У вас недостаточно денег ({card.Sum}$). Продайте филиалы, заложите фирмы или совершите сделку!", "ОК");
                            await Navigation.PushModalAsync(alert, false);
                        }
                    }
                    else
                    {
                        var alert = new PopUpAlert("КАЗНА", card.Description, $"Заплатить {card.Sum}$");
                        alert.OnConfirmed += async () =>
                        {
                            await GameManager.Instance.UpdateBalanceAsync(user.Id, user.Cash - card.Sum, "Казна");
                            await GameManager.Instance.AddHistoryAsync($"Игрок {user.Name}: {card.Description}", "kazna.png", card.Sum, true);
                        };
                        await Navigation.PushModalAsync(alert, false);
                    }
                }
            }
            else if (message.StartsWith("Card_shans_"))
            {
                if (user.isGameOver) return;
                if (int.TryParse(message.Replace("Card_shans_", ""), out var idx) && idx >= 1 && idx <= GameManager.Instance.ChanceCards.Count)
                {
                    var card = GameManager.Instance.ChanceCards[idx - 1];
                    var alert = new PopUpAlert("ШАНС", card.Description, $"Получить {card.Sum}$");
                    alert.OnConfirmed += async () =>
                    {
                        await GameManager.Instance.UpdateBalanceAsync(user.Id, user.Cash + card.Sum, "Шанс");
                        await GameManager.Instance.AddHistoryAsync($"Игрок {user.Name}: {card.Description}", "shans.png", card.Sum, true);
                    };
                    await Navigation.PushModalAsync(alert, false);
                }
            }
            else
            {
                // Поиск среди предприятий по nfc_marker
                var company = GameManager.Instance.Companys.FirstOrDefault(c => c.nfc_marker == message);
                if (company != null)
                {
                    if (string.IsNullOrEmpty(company.UserName))
                    {
                        // Свободное предприятие — покупка
                        await Navigation.PushModalAsync(new PopUpCompany(company), false);
                    }
                    else if (company.UserName == user.Name)
                    {
                        // Свое предприятие — управление филиалами и залогами
                        await Navigation.PushModalAsync(new PopUpCompanyInfo(company), false);
                    }
                    else
                    {
                        // Чужое предприятие — оплата аренды
                        await Navigation.PushModalAsync(new PopUpCompanyPlayer(company), false);
                    }
                }
                else
                {
                    var alert = new PopUpAlert("NFC", $"Неизвестная метка: {message}", "Закрыть");
                    await Navigation.PushModalAsync(alert, false);
                }
            }
        }

        #endregion
    }
}
