namespace MonopolyBank.Models
{
    public class History
    {
        public string name_img { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public bool isVisibleCash { get; set; }
        public int Cash { get; set; }
    }
}
