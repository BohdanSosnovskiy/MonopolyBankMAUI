using Microsoft.Extensions.DependencyInjection;

namespace MonopolyBank
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(new AppShell());
            window.Resumed += async (s, e) =>
            {
                if (!Services.GameManager.Instance.IsHost && Services.GameManager.Instance.Users.Count > 0)
                {
                    await Services.GameManager.Instance.EnsureConnectedAndSynchronizedAsync();
                }
            };
            return window;
        }
    }
}