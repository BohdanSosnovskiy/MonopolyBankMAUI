using MonopolyBank.Models;
using MonopolyBank.Services;

namespace MonopolyBank.Views
{
    public class PresetItemViewModel
    {
        public MonopolyPreset Preset { get; set; } = null!;
        public bool IsActive { get; set; }
        public string ActionButtonText => IsActive ? "✓ Активна" : "Выбрать";
        public Color ActionButtonColor => IsActive ? Color.FromArgb("#2e7d32") : Color.FromArgb("#0daaa3");
    }

    public partial class PagePresetList : ContentPage
    {
        public PagePresetList()
        {
            InitializeComponent();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            RefreshList();
            UpdateProUI();
        }

        private void RefreshList()
        {
            PresetManager.Instance.LoadAllPresets();
            var activePreset = PresetManager.Instance.ActivePreset;
            ActiveMonopolyTitle.Text = activePreset?.Name ?? "Не выбрана";

            var viewModels = PresetManager.Instance.Presets.Select(p => new PresetItemViewModel
            {
                Preset = p,
                IsActive = activePreset != null && p.Id == activePreset.Id
            }).ToList();

            PresetsCollection.ItemsSource = null;
            PresetsCollection.ItemsSource = viewModels;
        }

        private void UpdateProUI()
        {
            bool isPro = LicenseService.Instance.IsPro;
            ProBadgeText.Text = isPro ? "PRO АКТИВИРОВАН" : "FREE";
            BtnProStatus.Text = isPro ? "👑 У вас активен PRO-режим" : "👑 Активировать Pro ключ";
        }

        private async void OnSelectPresetClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is MonopolyPreset preset)
            {
                PresetManager.Instance.SetActivePreset(preset);
                GameManager.Instance.ResetGameData();
                RefreshList();
                await DisplayAlert("Монополия выбрана", $"Активная игра переключена на «{preset.Name}». Все последующие игры будут запускаться с этим набором карточек и правил.", "ОК");
            }
        }

        private async void OnEditPresetClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is MonopolyPreset preset)
            {
                if (preset.IsBuiltIn)
                {
                    bool clone = await DisplayAlert("Оригинальный шаблон", "Это защищенная оригинальная игра автора. Чтобы изменить её, создать редактируемую копию?", "Создать копию", "Отмена");
                    if (clone)
                    {
                        var copy = preset.Clone();
                        await PresetManager.Instance.SavePresetAsync(copy);
                        RefreshList();
                        await Navigation.PushAsync(new PagePresetEditor(copy));
                    }
                    return;
                }

                await Navigation.PushAsync(new PagePresetEditor(preset));
            }
        }

        private async void OnCreatePresetClicked(object sender, EventArgs e)
        {
            var customPresetsCount = PresetManager.Instance.Presets.Count(p => !p.IsBuiltIn);
            if (!LicenseService.Instance.IsPro && customPresetsCount >= 1)
            {
                bool buy = await DisplayAlert("Лимит бесплатной версии", "В бесплатной версии можно создать 1 свою монополию. Для создания неограниченного числа игр и доступа к каталогу требуется PRO.", "Ввести ключ", "Понятно");
                if (buy)
                {
                    OnProActivateClicked(sender, e);
                }
                return;
            }

            var choice = await DisplayActionSheet("Выберите тип монополии", "Отмена", null, 
                "Классическая сетка (40 клеток)", 
                "Свободный конструктор");

            if (choice == "Классическая сетка (40 клеток)")
            {
                string name = await DisplayPromptAsync("Новая игра", "Введите название вашей монополии:", initialValue: "Монополия Города");
                if (!string.IsNullOrWhiteSpace(name))
                {
                    var newPreset = PresetManager.Instance.CreateClassicGridTemplate(name.Trim());
                    await PresetManager.Instance.SavePresetAsync(newPreset);
                    RefreshList();
                    await Navigation.PushAsync(new PagePresetEditor(newPreset));
                }
            }
            else if (choice == "Свободный конструктор")
            {
                string name = await DisplayPromptAsync("Новая игра", "Введите название вашей монополии:", initialValue: "Кастомная Монополия");
                if (!string.IsNullOrWhiteSpace(name))
                {
                    var newPreset = new MonopolyPreset
                    {
                        Name = name.Trim(),
                        Description = "Свободная монополия",
                        IsBuiltIn = false
                    };
                    await PresetManager.Instance.SavePresetAsync(newPreset);
                    RefreshList();
                    await Navigation.PushAsync(new PagePresetEditor(newPreset));
                }
            }
        }

        private async void OnOpenStoreClicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new PageTemplateStore());
        }

        private async void OnProActivateClicked(object sender, EventArgs e)
        {
            if (LicenseService.Instance.IsPro)
            {
                await DisplayAlert("PRO Режим", "У вас уже активирован статус PRO! Доступны все функции редактора, генерация PDF и каталог шаблонов.", "Отлично");
                return;
            }

            string key = await DisplayPromptAsync("Активация PRO", "Введите ваш лицензионный ключ (формат MPRO-XXXX-XXXX-XXXX):", placeholder: "MPRO-...");
            if (!string.IsNullOrWhiteSpace(key))
            {
                bool success = await LicenseService.Instance.ActivateKeyAsync(key);
                if (success)
                {
                    await DisplayAlert("Успех!", "PRO-режим успешно активирован! Спасибо за поддержку проекта.", "Ура!");
                    UpdateProUI();
                }
                else
                {
                    await DisplayAlert("Ошибка", "Не удалось активировать ключ. Проверьте правильность ввода или подключение к серверу.", "ОК");
                }
            }
        }
    }
}
