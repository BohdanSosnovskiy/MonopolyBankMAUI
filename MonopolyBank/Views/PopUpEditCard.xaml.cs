using MonopolyBank.Models;

namespace MonopolyBank.Views
{
    public partial class PopUpEditCard : ContentPage
    {
        private readonly Company _company;
        private readonly Action _onSaved;
        private string _currentColor;

        public PopUpEditCard(Company company, Action onSaved)
        {
            InitializeComponent();
            _company = company;
            _onSaved = onSaved;
            _currentColor = _company.color;

            NameEntry.Text = _company.Name;
            PriceEntry.Text = _company.Price.ToString();
            GroupCountEntry.Text = _company.CountColorGroup.ToString();
            NfcMarkerEntry.Text = _company.nfc_marker;

            CardPreview.BindingContext = _company;
        }

        private void OnColorSelected(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is string hex)
            {
                _currentColor = hex;
                _company.color = hex;
                CardPreview.BindingContext = null;
                CardPreview.BindingContext = _company;
            }
        }

        private async void OnSaveCardClicked(object sender, EventArgs e)
        {
            _company.Name = string.IsNullOrWhiteSpace(NameEntry.Text) ? "Без названия" : NameEntry.Text.Trim();

            if (int.TryParse(PriceEntry.Text, out var pr) && pr > 0)
            {
                _company.Price = pr;
                _company.PriceFilial = Math.Max(50, pr / 2);
                _company.PriceCorp = Math.Max(50, pr / 2);
                _company.Deposit = pr / 2;
                _company.Selling = pr;

                // Авто-пересчет базовой аренды
                if (_company.Orend == null || _company.Orend.Length < 6)
                {
                    _company.Orend = new int[6];
                }
                _company.Orend[0] = Math.Max(10, pr / 10);
                _company.Orend[1] = pr / 2;
                _company.Orend[2] = pr;
                _company.Orend[3] = pr * 2;
                _company.Orend[4] = pr * 3;
            }

            if (int.TryParse(GroupCountEntry.Text, out var gc) && gc > 0)
            {
                _company.CountColorGroup = gc;
            }

            _company.color = _currentColor;
            _company.nfc_marker = NfcMarkerEntry.Text?.Trim() ?? "";

            _onSaved?.Invoke();
            await Navigation.PopModalAsync(false);
        }

        private async void OnCloseClicked(object sender, EventArgs e)
        {
            await Navigation.PopModalAsync(false);
        }
    }
}
