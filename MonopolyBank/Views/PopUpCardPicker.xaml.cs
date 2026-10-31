using MonopolyBank.Models;
using MonopolyBank.Services;

namespace MonopolyBank.Views
{
    public class PickerItemViewModel
    {
        public string Title { get; set; } = "";
        public string Subtitle { get; set; } = "";
        public Color SubtitleColor { get; set; } = Colors.Gray;
        public Color ColorHex { get; set; } = Colors.Transparent;
        public string ButtonText { get; set; } = "Выбрать";
        public Color ButtonColor { get; set; } = Color.FromArgb("#0daaa3");
        public string ItemType { get; set; } = "Company"; // "Company", "Special", "Card"
        public Company? CompanyRef { get; set; }
        public string SpecialAction { get; set; } = "";
        public int SpecialAmount { get; set; }
    }

    public partial class PopUpCardPicker : ContentPage
    {
        private readonly GameMainPage _mainPage;
        private List<PickerItemViewModel> _allItems = new();
        private string _currentTab = "All";

        public PopUpCardPicker(GameMainPage mainPage)
        {
            InitializeComponent();
            _mainPage = mainPage;
            BuildItemsList();
            ApplyFilter();
        }

        private void BuildItemsList()
        {
            _allItems.Clear();
            var currentUser = GameManager.Instance.CurrentUser;

            // 1. Компании / Улицы
            foreach (var comp in GameManager.Instance.Companys)
            {
                if (!comp.IsNoEmty) continue;

                var item = new PickerItemViewModel
                {
                    Title = comp.Name,
                    ItemType = "Company",
                    CompanyRef = comp
                };

                try
                {
                    item.ColorHex = !string.IsNullOrEmpty(comp.color) && comp.color != "Transparent" 
                        ? Color.FromArgb(comp.color) 
                        : Color.FromArgb("#555555");
                }
                catch
                {
                    item.ColorHex = Color.FromArgb("#555555");
                }

                if (string.IsNullOrEmpty(comp.UserName))
                {
                    item.Subtitle = $"Свободно • {comp.Price} $";
                    item.SubtitleColor = Color.FromArgb("#55b155");
                    item.ButtonText = "Купить";
                    item.ButtonColor = Color.FromArgb("#0daaa3");
                }
                else if (comp.UserName == currentUser.Name)
                {
                    item.Subtitle = "Ваша фирма (Филиалы / Залог)";
                    item.SubtitleColor = Color.FromArgb("#0daaa3");
                    item.ButtonText = "Управлять";
                    item.ButtonColor = Color.FromArgb("#27283d");
                }
                else
                {
                    // Чужая фирма — расчет аренды
                    int rent = 0;
                    if (comp.IsCompany)
                    {
                        rent = comp.Orend[Math.Clamp(comp.CountBulds, 0, 5)];
                    }
                    else
                    {
                        var owner = GameManager.Instance.Users.FirstOrDefault(u => u.Name == comp.UserName);
                        int count = owner?.Companys.Count(c => c.color == comp.color) ?? 1;
                        rent = comp.Orend[Math.Clamp(count - 1, 0, 5)];
                    }

                    item.Subtitle = $"Владелец: {comp.UserName} • Рента: {rent} $";
                    item.SubtitleColor = Color.FromArgb("#e73d60");
                    item.ButtonText = "Оплатить";
                    item.ButtonColor = Color.FromArgb("#e73d60");
                }

                _allItems.Add(item);
            }

            // 2. Спецполя
            _allItems.Add(new PickerItemViewModel
            {
                Title = "Поле ВПЕРЕД (Старт)",
                Subtitle = $"Получить зарплату (+{GameManager.Instance.CountStartSum} $)",
                SubtitleColor = Color.FromArgb("#55b155"),
                ColorHex = Color.FromArgb("#0daaa3"),
                ButtonText = $"+{GameManager.Instance.CountStartSum} $",
                ButtonColor = Color.FromArgb("#55b155"),
                ItemType = "Special",
                SpecialAction = "start_mon",
                SpecialAmount = GameManager.Instance.CountStartSum
            });

            _allItems.Add(new PickerItemViewModel
            {
                Title = "Тюрьма (Выход под залог)",
                Subtitle = "Заплатить залог (-50 $)",
                SubtitleColor = Color.FromArgb("#e73d60"),
                ColorHex = Color.FromArgb("#27283d"),
                ButtonText = "50 $",
                ButtonColor = Color.FromArgb("#e73d60"),
                ItemType = "Special",
                SpecialAction = "turma",
                SpecialAmount = 50
            });

            _allItems.Add(new PickerItemViewModel
            {
                Title = "Поборы на дороге",
                Subtitle = "Штраф за проезд (-200 $)",
                SubtitleColor = Color.FromArgb("#e73d60"),
                ColorHex = Color.FromArgb("#fd6244"),
                ButtonText = "200 $",
                ButtonColor = Color.FromArgb("#fd6244"),
                ItemType = "Special",
                SpecialAction = "pobori",
                SpecialAmount = 200
            });

            _allItems.Add(new PickerItemViewModel
            {
                Title = "Штраф ТАЧКА",
                Subtitle = "Оплата штрафа за авто (-100 $)",
                SubtitleColor = Color.FromArgb("#e73d60"),
                ColorHex = Color.FromArgb("#e73d60"),
                ButtonText = "100 $",
                ButtonColor = Color.FromArgb("#e73d60"),
                ItemType = "Special",
                SpecialAction = "taska",
                SpecialAmount = 100
            });

            // 3. Карточки Шанс и Казна
            _allItems.Add(new PickerItemViewModel
            {
                Title = "Случайный ШАНС",
                Subtitle = "Вытянуть карточку удачи",
                SubtitleColor = Color.FromArgb("#ffe828"),
                ColorHex = Color.FromArgb("#ffe828"),
                ButtonText = "Тянуть",
                ButtonColor = Color.FromArgb("#8e2de2"),
                ItemType = "Card",
                SpecialAction = "random_chance"
            });

            _allItems.Add(new PickerItemViewModel
            {
                Title = "Случайная КАЗНА",
                Subtitle = "Вытянуть карточку городской казны",
                SubtitleColor = Color.FromArgb("#0daaa3"),
                ColorHex = Color.FromArgb("#0daaa3"),
                ButtonText = "Тянуть",
                ButtonColor = Color.FromArgb("#019dce"),
                ItemType = "Card",
                SpecialAction = "random_treasury"
            });
        }

        private void ApplyFilter()
        {
            var query = SearchEntry?.Text?.Trim().ToLowerInvariant() ?? "";
            var filtered = _allItems.AsEnumerable();

            if (_currentTab == "Streets")
            {
                filtered = filtered.Where(i => i.ItemType == "Company");
            }
            else if (_currentTab == "Special")
            {
                filtered = filtered.Where(i => i.ItemType == "Special");
            }
            else if (_currentTab == "Cards")
            {
                filtered = filtered.Where(i => i.ItemType == "Card");
            }

            if (!string.IsNullOrEmpty(query))
            {
                filtered = filtered.Where(i => i.Title.ToLowerInvariant().Contains(query) || i.Subtitle.ToLowerInvariant().Contains(query));
            }

            ItemsCollection.ItemsSource = filtered.ToList();
        }

        private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void SetTabActive(string tab, Button activeBtn)
        {
            _currentTab = tab;
            TabAllBtn.BackgroundColor = Color.FromArgb("#222533");
            TabAllBtn.TextColor = Color.FromArgb("#aaa");
            TabStreetsBtn.BackgroundColor = Color.FromArgb("#222533");
            TabStreetsBtn.TextColor = Color.FromArgb("#aaa");
            TabSpecialBtn.BackgroundColor = Color.FromArgb("#222533");
            TabSpecialBtn.TextColor = Color.FromArgb("#aaa");
            TabCardsBtn.BackgroundColor = Color.FromArgb("#222533");
            TabCardsBtn.TextColor = Color.FromArgb("#aaa");

            activeBtn.BackgroundColor = Color.FromArgb("#0daaa3");
            activeBtn.TextColor = Colors.White;

            ApplyFilter();
        }

        private void OnTabAllClicked(object sender, EventArgs e) => SetTabActive("All", TabAllBtn);
        private void OnTabStreetsClicked(object sender, EventArgs e) => SetTabActive("Streets", TabStreetsBtn);
        private void OnTabSpecialClicked(object sender, EventArgs e) => SetTabActive("Special", TabSpecialBtn);
        private void OnTabCardsClicked(object sender, EventArgs e) => SetTabActive("Cards", TabCardsBtn);

        private async void OnItemActionClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is PickerItemViewModel item)
            {
                // Закрываем текущий диалог выбора карточки
                await Navigation.PopModalAsync(false);
                await Task.Delay(100);

                if (item.ItemType == "Company" && item.CompanyRef != null)
                {
                    await _mainPage.ProcessNfcTagAsync(item.CompanyRef.nfc_marker);
                }
                else if (item.ItemType == "Special")
                {
                    await _mainPage.ProcessNfcTagAsync(item.SpecialAction);
                }
                else if (item.ItemType == "Card")
                {
                    if (item.SpecialAction == "random_chance")
                    {
                        var rnd = new Random();
                        int idx = rnd.Next(1, GameManager.Instance.ChanceCards.Count + 1);
                        await _mainPage.ProcessNfcTagAsync($"Card_shans_{idx}");
                    }
                    else if (item.SpecialAction == "random_treasury")
                    {
                        var rnd = new Random();
                        int idx = rnd.Next(1, GameManager.Instance.TreasuryCards.Count + 1);
                        await _mainPage.ProcessNfcTagAsync($"Card_k_{idx}");
                    }
                }
            }
        }

        private async void OnCloseClicked(object sender, EventArgs e)
        {
            await Navigation.PopModalAsync(false);
        }
    }
}
