using System.Text.Json.Serialization;

namespace MonopolyBank.Models
{
    public class PresetSettings
    {
        public int DefaultStartCash { get; set; } = 15000;
        public int PassGoCash { get; set; } = 2000;
        public int JailFee { get; set; } = 50;
        public int RoadTaxAmount { get; set; } = 200;
        public int CarFineAmount { get; set; } = 100;
    }

    public class SpecialField
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public string Name { get; set; } = "Поле";
        public string NfcMarker { get; set; } = "";
        public string ActionType { get; set; } = "Penalty"; // "PassGo", "Penalty", "Jail", "Custom"
        public int Amount { get; set; } = 0;
        public string IconName { get; set; } = "payment.png";
        public string SoundName { get; set; } = "gopstop";
        public string Description { get; set; } = "";
    }

    public class MonopolyPreset
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public string Name { get; set; } = "Моя Монополия";
        public string Description { get; set; } = "Пользовательская версия игры";
        public string Author { get; set; } = "Игрок";
        public string Version { get; set; } = "1.0";
        public bool IsBuiltIn { get; set; } = false;
        public string CurrencySymbol { get; set; } = "$";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public PresetSettings Settings { get; set; } = new();
        public List<Company> Companies { get; set; } = new();
        public List<SpecialField> SpecialFields { get; set; } = new();
        public List<Card_kazna> TreasuryCards { get; set; } = new();
        public List<Card_Shans> ChanceCards { get; set; } = new();

        public MonopolyPreset Clone()
        {
            var json = System.Text.Json.JsonSerializer.Serialize(this);
            var copy = System.Text.Json.JsonSerializer.Deserialize<MonopolyPreset>(json) ?? new MonopolyPreset();
            copy.Id = Guid.NewGuid().ToString("N")[..8];
            copy.Name = $"{Name} (Копия)";
            copy.IsBuiltIn = false;
            copy.CreatedAt = DateTime.UtcNow;
            copy.UpdatedAt = DateTime.UtcNow;
            return copy;
        }
    }
}
