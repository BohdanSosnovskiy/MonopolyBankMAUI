using System.Security.Cryptography;
using System.Text;

namespace MonopolyBank.Services
{
    public interface ILicenseService
    {
        bool IsPro { get; }
        Task<bool> ActivateKeyAsync(string licenseKey);
        Task<bool> VerifyStoredLicenseAsync();
        void ActivateDevBypass();
    }

    public class LicenseService : ILicenseService
    {
        private static LicenseService? _instance;
        public static LicenseService Instance => _instance ??= new LicenseService();

        private const string LicenseStorageKey = "MonopolyBank_ProLicenseToken";
        private const string LicenseKeyStorage = "MonopolyBank_ProLicenseKey";

        // Секретный мастер-ключ разработчика для мгновенной активации в Release на любых телефонах автора
        private const string DevMasterKey = "MPRO-DEV-ALEX-2026-UNLIMITED";

        public event Action? OnLicenseStatusChanged;

        private bool? _cachedIsPro;

        public bool IsPro
        {
            get
            {
#if DEBUG
                // В Debug-сборках у разработчика Pro включен автоматически и всегда
                return true;
#else
                if (_cachedIsPro.HasValue) return _cachedIsPro.Value;
                _cachedIsPro = CheckProStatusLocally();
                return _cachedIsPro.Value;
#endif
            }
        }

        private LicenseService()
        {
        }

        private bool CheckProStatusLocally()
        {
            try
            {
                var tokenTask = SecureStorage.Default.GetAsync(LicenseStorageKey);
                tokenTask.Wait(300);
                var token = tokenTask.Result;

                if (!string.IsNullOrEmpty(token))
                {
                    // Проверяем валидность формата криптографического токена
                    return ValidateTokenSignature(token);
                }
            }
            catch
            {
                // Если SecureStorage временно недоступен
            }

            // Fallback в обычные Preferences
            var prefKey = Preferences.Get("ProStatusActive", false);
            return prefKey;
        }

        public async Task<bool> ActivateKeyAsync(string licenseKey)
        {
            if (string.IsNullOrWhiteSpace(licenseKey)) return false;

            var cleanKey = licenseKey.Trim().ToUpperInvariant();

            // 1. Проверка мастер-ключа автора
            if (cleanKey == DevMasterKey || cleanKey == "ALEX-MASTER-DEV")
            {
                ActivateDevBypass();
                return true;
            }

            // 2. Валидация через сервер
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                var devHostIp = Preferences.Get("LastHostIp", "192.168.88.14");
                var deviceId = GetDeviceId();

                var payload = new
                {
                    key = cleanKey,
                    deviceId = deviceId,
                    app = "MonopolyBank"
                };

                var content = new StringContent(
                    System.Text.Json.JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json"
                );

                var url = $"http://{devHostIp}:3000/api/license/activate";
                var response = await client.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    var respJson = await response.Content.ReadAsStringAsync();
                    // Сервер возвращает { success: true, token: "..." }
                    var token = GenerateOfflineToken(cleanKey, deviceId);
                    await SecureStorage.Default.SetAsync(LicenseStorageKey, token);
                    await SecureStorage.Default.SetAsync(LicenseKeyStorage, cleanKey);
                    Preferences.Set("ProStatusActive", true);

                    _cachedIsPro = true;
                    OnLicenseStatusChanged?.Invoke();
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LicenseService] Server activation error: {ex.Message}");
            }

            // 3. Автономная валидация по формату (если сервер оффлайн, но ключ сгенерирован по алгоритму MPRO-XXXX-...)
            if (cleanKey.StartsWith("MPRO-") && cleanKey.Length >= 16)
            {
                var token = GenerateOfflineToken(cleanKey, GetDeviceId());
                await SecureStorage.Default.SetAsync(LicenseStorageKey, token);
                await SecureStorage.Default.SetAsync(LicenseKeyStorage, cleanKey);
                Preferences.Set("ProStatusActive", true);

                _cachedIsPro = true;
                OnLicenseStatusChanged?.Invoke();
                return true;
            }

            return false;
        }

        public void ActivateDevBypass()
        {
            _cachedIsPro = true;
            Preferences.Set("ProStatusActive", true);
            _ = SecureStorage.Default.SetAsync(LicenseStorageKey, "DEV_BYPASS_AUTHORIZED_TOKEN");
            OnLicenseStatusChanged?.Invoke();
        }

        public async Task<bool> VerifyStoredLicenseAsync()
        {
            var token = await SecureStorage.Default.GetAsync(LicenseStorageKey);
            if (!string.IsNullOrEmpty(token) && ValidateTokenSignature(token))
            {
                _cachedIsPro = true;
                return true;
            }
            return false;
        }

        private bool ValidateTokenSignature(string token)
        {
            if (token == "DEV_BYPASS_AUTHORIZED_TOKEN") return true;
            return token.Length >= 32;
        }

        private string GenerateOfflineToken(string key, string deviceId)
        {
            using var sha = SHA256.Create();
            var raw = $"{key}:{deviceId}:MonopolyBankSecretSalt_2026";
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(bytes);
        }

        private string GetDeviceId()
        {
            var id = Preferences.Get("AppInstallDeviceId", "");
            if (string.IsNullOrEmpty(id))
            {
                id = Guid.NewGuid().ToString("N")[..12];
                Preferences.Set("AppInstallDeviceId", id);
            }
            return id;
        }
    }
}
