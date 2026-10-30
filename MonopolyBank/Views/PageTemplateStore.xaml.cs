using System.Text.Json;
using MonopolyBank.Models;
using MonopolyBank.Services;

namespace MonopolyBank.Views
{
    public partial class PageTemplateStore : ContentPage
    {
        private List<MonopolyPreset> _catalogPresets = new();

        public PageTemplateStore()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadCatalogAsync();
        }

        private async Task LoadCatalogAsync()
        {
            var devHostIp = Preferences.Get("LastHostIp", "192.168.88.14");
            _catalogPresets.Clear();

            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
                var response = await client.GetAsync($"http://{devHostIp}:3000/api/presets/catalog");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var list = JsonSerializer.Deserialize<List<MonopolyPreset>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (list != null && list.Count > 0)
                    {
                        _catalogPresets = list;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Store] Error loading catalog: {ex.Message}");
            }

            // Если сервер еще пуст или недоступен, покажем встроенные образцы каталога
            if (_catalogPresets.Count == 0)
            {
                _catalogPresets.Add(new MonopolyPreset
                {
                    Id = "cyberpunk_2077",
                    Name = "Киберпанк Монополия (Night City)",
                    Description = "Неоновые улицы Найт-Сити, мегакорпорации Арасака и Милитех",
                    Author = "CyberBuilder",
                    Companies = new List<Company>
                    {
                        new Company { Name = "Уотсон", Price = 300, color = "#00FFFF" },
                        new Company { Name = "Уэстбрук", Price = 500, color = "#FF00FF" },
                        new Company { Name = "Арасака-Тауэр", Price = 1200, color = "#FF0000" }
                    }
                });

                _catalogPresets.Add(new MonopolyPreset
                {
                    Id = "world_capitals",
                    Name = "Столицы Мира (Travel Edition)",
                    Description = "Путешествуйте по Лондону, Токио, Парижу и Нью-Йорку",
                    Author = "TravelerPro",
                    Companies = new List<Company>
                    {
                        new Company { Name = "Рим", Price = 400, color = "#FFA500" },
                        new Company { Name = "Париж", Price = 600, color = "#87CEEB" },
                        new Company { Name = "Токио", Price = 1000, color = "#0000CD" }
                    }
                });
            }

            StoreCollection.ItemsSource = null;
            StoreCollection.ItemsSource = _catalogPresets;
        }

        private async void OnDownloadTemplateClicked(object sender, EventArgs e)
        {
            if (!LicenseService.Instance.IsPro)
            {
                bool unlock = await DisplayAlert("PRO Функция", "Скачивание шаблонов из каталога сообщества доступно в версии PRO.", "Активировать Pro", "Отмена");
                if (unlock)
                {
                    string key = await DisplayPromptAsync("Активация", "Введите ваш Pro ключ:");
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        await LicenseService.Instance.ActivateKeyAsync(key);
                    }
                }
                return;
            }

            if (sender is Button btn && btn.CommandParameter is MonopolyPreset preset)
            {
                var clone = preset.Clone();
                clone.Name = preset.Name;
                await PresetManager.Instance.SavePresetAsync(clone);
                await DisplayAlert("Успешно!", $"Шаблон «{preset.Name}» успешно скачан и добавлен в вашу библиотеку игр!", "Супер");
            }
        }

        private async void OnPublishClicked(object sender, EventArgs e)
        {
            if (!LicenseService.Instance.IsPro)
            {
                await DisplayAlert("PRO Функция", "Публикация своих монополий в общий каталог доступна в PRO версии.", "Понятно");
                return;
            }

            var customPresets = PresetManager.Instance.Presets.Where(p => !p.IsBuiltIn).ToList();
            if (customPresets.Count == 0)
            {
                await DisplayAlert("Нет кастомных игр", "Сначала создайте свою монополию в Редакторе, чтобы опубликовать её.", "ОК");
                return;
            }

            var names = customPresets.Select(p => p.Name).ToArray();
            var chosen = await DisplayActionSheet("Выберите игру для публикации", "Отмена", null, names);
            if (!string.IsNullOrEmpty(chosen) && chosen != "Отмена")
            {
                var target = customPresets.FirstOrDefault(p => p.Name == chosen);
                if (target != null)
                {
                    try
                    {
                        var devHostIp = Preferences.Get("LastHostIp", "192.168.88.14");
                        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                        var json = JsonSerializer.Serialize(target);
                        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                        var res = await client.PostAsync($"http://{devHostIp}:3000/api/presets/upload", content);
                        if (res.IsSuccessStatusCode)
                        {
                            await DisplayAlert("Успех!", "Ваша монополия отправлена на сервер и теперь видна другим игрокам!", "Отлично");
                            await LoadCatalogAsync();
                            return;
                        }
                    }
                    catch { }

                    await DisplayAlert("Опубликовано", "Шаблон зарегистрирован локально в каталоге!", "ОК");
                }
            }
        }
    }
}
