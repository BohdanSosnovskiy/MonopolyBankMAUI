using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using MonopolyBank.Models;

namespace MonopolyBank.Services
{
    public class PresetManager
    {
        private static PresetManager? _instance;
        public static PresetManager Instance => _instance ??= new PresetManager();

        private readonly string _presetsDirectory;
        private readonly JsonSerializerOptions _jsonOptions;

        public List<MonopolyPreset> Presets { get; private set; } = new();
        public MonopolyPreset ActivePreset { get; private set; } = null!;

        public event Action? OnPresetsChanged;
        public event Action<MonopolyPreset>? OnActivePresetChanged;

        private PresetManager()
        {
            _presetsDirectory = Path.Combine(FileSystem.AppDataDirectory, "presets");
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                PropertyNameCaseInsensitive = true
            };

            EnsureDirectoryExists();
            LoadAllPresets();
        }

        private void EnsureDirectoryExists()
        {
            try
            {
                if (!Directory.Exists(_presetsDirectory))
                {
                    Directory.CreateDirectory(_presetsDirectory);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PresetManager] Error creating dir: {ex.Message}");
            }
        }

        public void LoadAllPresets()
        {
            Presets.Clear();

            // 1. Создаем и добавляем защищенный дефолтный оригинальный пресет
            var originalDefault = CreateOriginalDefaultPreset();
            Presets.Add(originalDefault);

            // 2. Загружаем пользовательские пресеты из папки
            try
            {
                if (Directory.Exists(_presetsDirectory))
                {
                    var files = Directory.GetFiles(_presetsDirectory, "*.json");
                    foreach (var file in files)
                    {
                        try
                        {
                            var json = File.ReadAllText(file);
                            var preset = JsonSerializer.Deserialize<MonopolyPreset>(json, _jsonOptions);
                            if (preset != null && !preset.IsBuiltIn)
                            {
                                Presets.Add(preset);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[PresetManager] Failed to load preset from {file}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PresetManager] Error loading presets: {ex.Message}");
            }

            // 3. Восстанавливаем активный пресет по сохраненному ID
            var activeId = Preferences.Get("ActivePresetId", originalDefault.Id);
            ActivePreset = Presets.FirstOrDefault(p => p.Id == activeId) ?? originalDefault;
        }

        public void SetActivePreset(MonopolyPreset preset)
        {
            if (preset == null) return;
            ActivePreset = preset;
            Preferences.Set("ActivePresetId", preset.Id);
            OnActivePresetChanged?.Invoke(preset);
        }

        public async Task SavePresetAsync(MonopolyPreset preset)
        {
            if (preset.IsBuiltIn)
            {
                throw new InvalidOperationException("Встроенный оригинальный пресет защищен от перезаписи.");
            }

            preset.UpdatedAt = DateTime.UtcNow;
            var filePath = Path.Combine(_presetsDirectory, $"{preset.Id}.json");
            var json = JsonSerializer.Serialize(preset, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json);

            var existingIndex = Presets.FindIndex(p => p.Id == preset.Id);
            if (existingIndex >= 0)
            {
                Presets[existingIndex] = preset;
            }
            else
            {
                Presets.Add(preset);
            }

            if (ActivePreset.Id == preset.Id)
            {
                ActivePreset = preset;
            }

            OnPresetsChanged?.Invoke();
        }

        public bool DeletePreset(string presetId)
        {
            var target = Presets.FirstOrDefault(p => p.Id == presetId);
            if (target == null || target.IsBuiltIn) return false;

            try
            {
                var filePath = Path.Combine(_presetsDirectory, $"{presetId}.json");
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                Presets.Remove(target);

                if (ActivePreset.Id == presetId)
                {
                    SetActivePreset(Presets[0]);
                }

                OnPresetsChanged?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PresetManager] Delete error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Создание новой пустой монополии по классической сетке (28 предприятий, 8 групп, 4 спецполя)
        /// </summary>
        public MonopolyPreset CreateClassicGridTemplate(string name, string author = "Игрок")
        {
            var template = new MonopolyPreset
            {
                Id = Guid.NewGuid().ToString("N")[..8],
                Name = name,
                Author = author,
                Description = "Классическая монополия (40 клеток)",
                IsBuiltIn = false,
                Settings = new PresetSettings()
            };

            var colors = new[]
            {
                ("#8B4513", 2, "Коричневая"),
                ("#87CEEB", 3, "Голубая"),
                ("#FF69B4", 3, "Розовая"),
                ("#FFA500", 3, "Оранжевая"),
                ("#FF0000", 3, "Красная"),
                ("#FFD700", 3, "Желтая"),
                ("#228B22", 3, "Зеленая"),
                ("#0000CD", 2, "Синяя"),
            };

            int compIdx = 0;
            int groupNum = 1;
            foreach (var (hex, count, groupName) in colors)
            {
                for (int i = 1; i <= count; i++)
                {
                    int basePrice = groupNum * 50 + i * 20;
                    template.Companies.Add(new Company
                    {
                        index = compIdx++,
                        Name = $"Улица {compIdx}",
                        nfc_marker = $"street_{compIdx}",
                        color = hex,
                        CountColorGroup = count,
                        Price = basePrice,
                        PriceFilial = Math.Max(50, (basePrice / 2)),
                        PriceCorp = Math.Max(50, (basePrice / 2)),
                        Deposit = basePrice / 2,
                        Selling = basePrice,
                        Orend = new int[]
                        {
                            Math.Max(10, basePrice / 10),
                            basePrice / 2,
                            basePrice,
                            basePrice * 2,
                            basePrice * 3,
                            0
                        },
                        IsCompany = true,
                        IsCorp = false
                    });
                }
                groupNum++;
            }

            // 4 станции / транспорт
            for (int i = 1; i <= 4; i++)
            {
                template.Companies.Add(new Company
                {
                    index = compIdx++,
                    Name = $"Станция {i}",
                    nfc_marker = $"station_{i}",
                    color = "#555555",
                    CountColorGroup = 4,
                    Price = 200,
                    PriceFilial = 0,
                    PriceCorp = 0,
                    Deposit = 100,
                    Selling = 200,
                    Orend = new int[] { 25, 50, 100, 200, 0, 0 },
                    IsCompany = false,
                    IsCorp = true
                });
            }

            // Спецполя
            template.SpecialFields = new List<SpecialField>
            {
                new SpecialField { Id = "start", Name = "Старт", NfcMarker = "start_mon", ActionType = "PassGo", Amount = 2000, IconName = "payment.png" },
                new SpecialField { Id = "pobori", Name = "Поборы на дороге", NfcMarker = "pobori", ActionType = "Penalty", Amount = 200, IconName = "payment.png", SoundName = "gopstop" },
                new SpecialField { Id = "taska", Name = "Штраф авто", NfcMarker = "taska", ActionType = "Penalty", Amount = 100, IconName = "pay_bank.png", SoundName = "policay" },
                new SpecialField { Id = "turma", Name = "Тюрьма", NfcMarker = "turma", ActionType = "Jail", Amount = 50, IconName = "payment.png", SoundName = "turma" },
            };

            // Стандартные колоды
            template.TreasuryCards = PresetData.CreateTreasuryCards();
            template.ChanceCards = PresetData.CreateChanceCards();

            return template;
        }

        /// <summary>
        /// Базовый защищенный пресет текущей игры автора (Харьковская монополия)
        /// </summary>
        public MonopolyPreset CreateOriginalDefaultPreset()
        {
            var preset = new MonopolyPreset
            {
                Id = "original_kharkiv_builtin",
                Name = "Харьковская Монополия (Оригинал)",
                Description = "Оригинальная физическая версия с настроенными NFC метками",
                Author = "Алексей (Автор)",
                IsBuiltIn = true,
                CurrencySymbol = "$",
                Settings = new PresetSettings
                {
                    DefaultStartCash = 15000,
                    PassGoCash = 2000,
                    JailFee = 50,
                    RoadTaxAmount = 200,
                    CarFineAmount = 100
                },
                Companies = PresetData.CreateDefaultCompanys(),
                SpecialFields = new List<SpecialField>
                {
                    new SpecialField { Id = "start", Name = "Поле ВПЕРЕД (Старт)", NfcMarker = "start_mon", ActionType = "PassGo", Amount = 2000, IconName = "payment.png", SoundName = "coindrop" },
                    new SpecialField { Id = "pobori", Name = "Поборы на дороге", NfcMarker = "pobori", ActionType = "Penalty", Amount = 200, IconName = "payment.png", SoundName = "gopstop" },
                    new SpecialField { Id = "taska", Name = "Штраф ТАЧКА", NfcMarker = "taska", ActionType = "Penalty", Amount = 100, IconName = "pay_bank.png", SoundName = "policay" },
                    new SpecialField { Id = "turma", Name = "Тюрьма под залог", NfcMarker = "turma", ActionType = "Jail", Amount = 50, IconName = "payment.png", SoundName = "turma" },
                },
                TreasuryCards = PresetData.CreateTreasuryCards(),
                ChanceCards = PresetData.CreateChanceCards()
            };

            return preset;
        }
    }
}
