using MonopolyBank.Models;

namespace MonopolyBank.Views
{
    public partial class PageEditCard : ContentPage
    {
        private readonly Company _company;
        private readonly Action _onSaved;
        private string _currentColor;

        public PageEditCard(Company company, Action onSaved)
        {
            InitializeComponent();
            _company = company;
            _onSaved = onSaved;
            _currentColor = !string.IsNullOrEmpty(_company.color) ? _company.color : "#8B4513";

            NameEntry.Text = _company.Name;
            PriceEntry.Text = _company.Price.ToString();
            GroupCountEntry.Text = _company.CountColorGroup.ToString();
            NfcMarkerEntry.Text = _company.nfc_marker;

            DepositEntry.Text = _company.Deposit.ToString();

            if (_company.Orend != null && _company.Orend.Length >= 5)
            {
                Rent0Entry.Text = _company.Orend[0].ToString();
                Rent1Entry.Text = _company.Orend[1].ToString();
                Rent2Entry.Text = _company.Orend[2].ToString();
                Rent3Entry.Text = _company.Orend[3].ToString();
                Rent4Entry.Text = _company.Orend[4].ToString();
            }

            UpdatePreview();
        }

        private void UpdatePreview()
        {
            _company.Name = string.IsNullOrWhiteSpace(NameEntry.Text) ? "..." : NameEntry.Text.Trim();
            _company.color = _currentColor;
            CardPreview.BindingContext = null;
            CardPreview.BindingContext = _company;
        }

        private void OnFieldChanged(object? sender, TextChangedEventArgs e)
        {
            UpdatePreview();
        }

        private void OnPriceChanged(object? sender, TextChangedEventArgs e)
        {
            if (int.TryParse(PriceEntry.Text, out var pr) && pr > 0)
            {
                _company.Price = pr;
                _company.Deposit = pr / 2;
                DepositEntry.Text = _company.Deposit.ToString();

                // Автоматический расчет классической аренды
                Rent0Entry.Text = Math.Max(10, pr / 10).ToString();
                Rent1Entry.Text = (pr / 2).ToString();
                Rent2Entry.Text = pr.ToString();
                Rent3Entry.Text = (pr * 2).ToString();
                Rent4Entry.Text = (pr * 3).ToString();

                UpdatePreview();
            }
        }

        private void OnColorSelected(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is string hex)
            {
                _currentColor = hex;
                _company.color = hex;
                UpdatePreview();
            }
        }

        private async void OnScanNfcClicked(object sender, EventArgs e)
        {
            await Navigation.PushModalAsync(new PopUpNfcWriter(_company), false);
            NfcMarkerEntry.Text = _company.nfc_marker;
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            _company.Name = string.IsNullOrWhiteSpace(NameEntry.Text) ? "Без названия" : NameEntry.Text.Trim();

            if (int.TryParse(PriceEntry.Text, out var pr) && pr > 0)
            {
                _company.Price = pr;
                _company.PriceFilial = Math.Max(50, pr / 2);
                _company.PriceCorp = Math.Max(50, pr / 2);
                _company.Selling = pr;
            }

            if (int.TryParse(GroupCountEntry.Text, out var gc) && gc > 0)
            {
                _company.CountColorGroup = gc;
            }

            if (int.TryParse(DepositEntry.Text, out var dep))
            {
                _company.Deposit = dep;
            }

            if (_company.Orend == null || _company.Orend.Length < 6)
            {
                _company.Orend = new int[6];
            }

            if (int.TryParse(Rent0Entry.Text, out var r0)) _company.Orend[0] = r0;
            if (int.TryParse(Rent1Entry.Text, out var r1)) _company.Orend[1] = r1;
            if (int.TryParse(Rent2Entry.Text, out var r2)) _company.Orend[2] = r2;
            if (int.TryParse(Rent3Entry.Text, out var r3)) _company.Orend[3] = r3;
            if (int.TryParse(Rent4Entry.Text, out var r4)) _company.Orend[4] = r4;

            _company.color = _currentColor;
            _company.nfc_marker = NfcMarkerEntry.Text?.Trim() ?? "";

            _onSaved?.Invoke();
            await Navigation.PopAsync();
        }
    }
}
