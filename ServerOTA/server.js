const http = require('http');
const fs = require('fs');
const path = require('path');
const os = require('os');

const PORT = 3000;
const APK_SRC = path.join(__dirname, '..', 'MonopolyBank', 'bin', 'Release', 'net10.0-android', 'publish', 'com.companyname.monopolybank-Signed.apk');
const APK_DEST = path.join(__dirname, 'MonopolyBank.apk');
const PRESETS_FILE = path.join(__dirname, 'presets_db.json');
const KEYS_FILE = path.join(__dirname, 'keys_db.json');

// MongoDB optional connection / fallback to JSON database
let mongoClient = null;
const MONGO_URI = process.env.MONGO_URI || 'mongodb://localhost:27017/monopoly_bank';

async function initMongo() {
    try {
        const { MongoClient } = require('mongodb');
        mongoClient = new MongoClient(MONGO_URI);
        await mongoClient.connect();
        console.log('[MongoDB] Подключено к MongoDB успешно:', MONGO_URI);
    } catch (e) {
        console.log('[MongoDB] Внешний MongoDB сервер не запущен, используется встроенная локальная JSON-база:', e.message);
    }
}
initMongo();

// Local JSON DB helpers
function getStoredPresets() {
    try {
        if (fs.existsSync(PRESETS_FILE)) {
            return JSON.parse(fs.readFileSync(PRESETS_FILE, 'utf-8'));
        }
    } catch (e) {}
    return [
        {
            Id: 'cyberpunk_2077',
            Name: 'Киберпанк Монополия (Night City)',
            Description: 'Неоновые улицы Найт-Сити, мегакорпорации Арасака и Милитех',
            Author: 'CyberBuilder',
            Rating: 4.9,
            Downloads: 142,
            Companies: [
                { Name: 'Уотсон', Price: 300, color: '#00FFFF', index: 0 },
                { Name: 'Уэстбрук', Price: 500, color: '#FF00FF', index: 1 },
                { Name: 'Арасака-Тауэр', Price: 1200, color: '#FF0000', index: 2 }
            ]
        },
        {
            Id: 'world_capitals',
            Name: 'Столицы Мира (Travel Edition)',
            Description: 'Путешествуйте по Лондону, Токио, Парижу и Нью-Йорку',
            Author: 'TravelerPro',
            Rating: 4.8,
            Downloads: 98,
            Companies: [
                { Name: 'Рим', Price: 400, color: '#FFA500', index: 0 },
                { Name: 'Париж', Price: 600, color: '#87CEEB', index: 1 },
                { Name: 'Токио', Price: 1000, color: '#0000CD', index: 2 }
            ]
        }
    ];
}

function savePresets(list) {
    try {
        fs.writeFileSync(PRESETS_FILE, JSON.stringify(list, null, 2), 'utf-8');
    } catch (e) {}
}

function syncApk() {
    try {
        if (fs.existsSync(APK_SRC)) {
            fs.copyFileSync(APK_SRC, APK_DEST);
            const stats = fs.statSync(APK_DEST);
            console.log(`[OTA Server] Synchronized latest APK (${(stats.size / 1024 / 1024).toFixed(1)} MB)`);
        }
    } catch (err) {
        console.error(`[OTA Server] Error copying APK:`, err.message);
    }
}

function getLocalIp() {
    const interfaces = os.networkInterfaces();
    let fallback = 'localhost';
    for (const name of Object.keys(interfaces)) {
        for (const net of interfaces[name]) {
            if (net.family === 'IPv4' && !net.internal) {
                if (net.address.startsWith('192.168.') || net.address.startsWith('10.') || (net.address.startsWith('172.') && !net.address.startsWith('172.17.'))) {
                    return net.address;
                }
                if (fallback === 'localhost') fallback = net.address;
            }
        }
    }
    return fallback;
}

const VERSION_INFO = {
    versionCode: 8,
    versionName: '1.0.8',
    apkUrl: '/MonopolyBank.apk',
    releaseNotes: '• Добавлен полноценный «Редактор Монополий»\n• Студия настройки карточек, цветов и аренды\n• Экспресс-привязка NFC меток прямо в приложении\n• Магазин авторских шаблонов монополий и PRO-система'
};

const server = http.createServer((req, res) => {
    const url = req.url.split('?')[0];

    // CORS
    res.setHeader('Access-Control-Allow-Origin', '*');
    res.setHeader('Access-Control-Allow-Methods', 'GET, POST, OPTIONS');
    res.setHeader('Access-Control-Allow-Headers', 'Content-Type, Authorization');

    if (req.method === 'OPTIONS') {
        res.writeHead(204);
        res.end();
        return;
    }

    if (url === '/version.json') {
        res.writeHead(200, { 'Content-Type': 'application/json; charset=utf-8' });
        res.end(JSON.stringify(VERSION_INFO, null, 2));
        return;
    }

    // 1. API Каталога пресетов
    if (url === '/api/presets/catalog' && req.method === 'GET') {
        const presets = getStoredPresets();
        res.writeHead(200, { 'Content-Type': 'application/json; charset=utf-8' });
        res.end(JSON.stringify(presets, null, 2));
        return;
    }

    // 2. API Загрузки нового пресета (Upload)
    if (url === '/api/presets/upload' && req.method === 'POST') {
        let body = '';
        req.on('data', chunk => body += chunk);
        req.on('end', () => {
            try {
                const newPreset = JSON.parse(body);
                if (newPreset && newPreset.Name) {
                    const presets = getStoredPresets();
                    presets.unshift(newPreset);
                    savePresets(presets);
                    res.writeHead(200, { 'Content-Type': 'application/json' });
                    res.end(JSON.stringify({ success: true, message: 'Шаблон опубликован!' }));
                    return;
                }
            } catch (e) {}
            res.writeHead(400, { 'Content-Type': 'application/json' });
            res.end(JSON.stringify({ success: false, error: 'Некорректный формат' }));
        });
        return;
    }

    // 3. API Активации ключа Pro
    if (url === '/api/license/activate' && req.method === 'POST') {
        let body = '';
        req.on('data', chunk => body += chunk);
        req.on('end', () => {
            try {
                const data = JSON.parse(body);
                const key = data.key ? data.key.trim().toUpperCase() : '';
                // Разрешаем ключи формата MPRO-...
                if (key.startsWith('MPRO-')) {
                    res.writeHead(200, { 'Content-Type': 'application/json' });
                    res.end(JSON.stringify({ success: true, token: `TOKEN_${Date.now()}` }));
                    return;
                }
            } catch (e) {}
            res.writeHead(400, { 'Content-Type': 'application/json' });
            res.end(JSON.stringify({ success: false, error: 'Недействительный ключ' }));
        });
        return;
    }

    // 4. Скачивание APK
    if (url === '/MonopolyBank.apk' || url === '/download') {
        syncApk();
        if (!fs.existsSync(APK_DEST)) {
            res.writeHead(404, { 'Content-Type': 'text/plain; charset=utf-8' });
            res.end('APK файл формируется...');
            return;
        }

        const stat = fs.statSync(APK_DEST);
        res.writeHead(200, {
            'Content-Type': 'application/vnd.android.package-archive',
            'Content-Length': stat.size,
            'Content-Disposition': 'attachment; filename="MonopolyBank.apk"'
        });
        fs.createReadStream(APK_DEST).pipe(res);
        return;
    }

    // 5. Красивый неоновый лендинг в стиле приложения
    syncApk();
    const localIp = getLocalIp();
    const downloadUrl = `http://${localIp}:${PORT}/MonopolyBank.apk`;
    const apkSize = fs.existsSync(APK_DEST) ? `${(fs.statSync(APK_DEST).size / 1024 / 1024).toFixed(1)} MB` : '34 MB';

    const html = `<!DOCTYPE html>
<html lang="ru">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Monopoly Bank — Умный банк и Редактор Монополий</title>
    <link rel="preconnect" href="https://fonts.googleapis.com">
    <link href="https://fonts.googleapis.com/css2?family=Montserrat:wght@400;600;700;800;900&display=swap" rel="stylesheet">
    <style>
        :root {
            --bg: #0b0d14;
            --card-bg: rgba(22, 25, 37, 0.85);
            --accent-orange: #fd6244;
            --accent-pink: #e73d60;
            --accent-cyan: #0daaa3;
            --accent-purple: #8e2de2;
            --text-main: #f0f2f8;
            --text-muted: #8b92a5;
        }
        * { box-sizing: border-box; margin: 0; padding: 0; }
        body {
            font-family: 'Montserrat', sans-serif;
            background-color: var(--bg);
            color: var(--text-main);
            min-height: 100vh;
            overflow-x: hidden;
            background-image: 
                radial-gradient(circle at 15% 20%, rgba(253, 98, 68, 0.15) 0%, transparent 40%),
                radial-gradient(circle at 85% 80%, rgba(14, 170, 163, 0.15) 0%, transparent 40%),
                radial-gradient(circle at 50% 50%, rgba(142, 45, 226, 0.1) 0%, transparent 50%);
        }
        .container {
            max-width: 1080px;
            margin: 0 auto;
            padding: 40px 20px;
        }
        /* Hero */
        .hero {
            text-align: center;
            padding: 40px 0 60px;
        }
        .logo-badge {
            display: inline-flex;
            align-items: center;
            gap: 8px;
            padding: 8px 18px;
            background: rgba(255, 255, 255, 0.06);
            border: 1px solid rgba(255, 255, 255, 0.12);
            border-radius: 30px;
            font-size: 13px;
            font-weight: 700;
            color: #0daaa3;
            margin-bottom: 24px;
            backdrop-filter: blur(10px);
        }
        h1 {
            font-size: clamp(34px, 6vw, 56px);
            font-weight: 900;
            letter-spacing: -1px;
            line-height: 1.15;
            margin-bottom: 18px;
            background: linear-gradient(135deg, #ffffff 30%, #a5b0c9 100%);
            -webkit-background-clip: text;
            -webkit-text-fill-color: transparent;
        }
        h1 span {
            background: linear-gradient(135deg, var(--accent-orange) 0%, var(--accent-pink) 100%);
            -webkit-background-clip: text;
            -webkit-text-fill-color: transparent;
        }
        .subtitle {
            font-size: 18px;
            color: var(--text-muted);
            max-width: 640px;
            margin: 0 auto 36px;
            line-height: 1.6;
        }
        .cta-group {
            display: flex;
            gap: 16px;
            justify-content: center;
            flex-wrap: wrap;
        }
        .btn-download {
            background: linear-gradient(135deg, var(--accent-orange) 0%, var(--accent-pink) 100%);
            color: white;
            text-decoration: none;
            font-weight: 800;
            font-size: 16px;
            padding: 16px 36px;
            border-radius: 16px;
            box-shadow: 0 10px 25px rgba(231, 61, 96, 0.4);
            transition: all 0.25s ease;
            display: inline-flex;
            align-items: center;
            gap: 10px;
        }
        .btn-download:hover {
            transform: translateY(-2px);
            box-shadow: 0 14px 30px rgba(231, 61, 96, 0.55);
        }
        .btn-pro {
            background: linear-gradient(135deg, #8e2de2 0%, #4a00e0 100%);
            color: white;
            text-decoration: none;
            font-weight: 800;
            font-size: 16px;
            padding: 16px 32px;
            border-radius: 16px;
            box-shadow: 0 10px 25px rgba(74, 0, 224, 0.35);
            transition: all 0.25s ease;
        }
        .btn-pro:hover {
            transform: translateY(-2px);
            box-shadow: 0 14px 30px rgba(74, 0, 224, 0.5);
        }
        /* Features Grid */
        .features {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
            gap: 24px;
            margin: 60px 0;
        }
        .f-card {
            background: var(--card-bg);
            border: 1px solid rgba(255, 255, 255, 0.08);
            border-radius: 20px;
            padding: 28px;
            backdrop-filter: blur(14px);
            transition: border-color 0.3s;
        }
        .f-card:hover {
            border-color: rgba(255, 255, 255, 0.2);
        }
        .f-icon {
            font-size: 32px;
            margin-bottom: 16px;
        }
        .f-card h3 {
            font-size: 20px;
            font-weight: 700;
            margin-bottom: 10px;
        }
        .f-card p {
            color: var(--text-muted);
            font-size: 14px;
            line-height: 1.6;
        }
        /* Instructions */
        .box {
            background: rgba(13, 170, 163, 0.08);
            border: 1px solid rgba(13, 170, 163, 0.25);
            border-radius: 18px;
            padding: 24px 28px;
            margin-top: 40px;
        }
        .box h4 {
            color: var(--accent-cyan);
            font-size: 16px;
            margin-bottom: 10px;
        }
        .box ol {
            padding-left: 20px;
            color: #c0c6d8;
            font-size: 14px;
            line-height: 1.8;
        }
        footer {
            text-align: center;
            margin-top: 80px;
            padding-top: 24px;
            border-top: 1px solid rgba(255, 255, 255, 0.08);
            color: var(--text-muted);
            font-size: 13px;
        }
    </style>
</head>
<body>
    <div class="container">
        <!-- Hero -->
        <div class="hero">
            <div class="logo-badge">⚡ Новое в v${VERSION_INFO.versionName}: Студия Редактора & NFC</div>
            <h1>МОНОПОЛИЯ <span>БАНК</span></h1>
            <p class="subtitle">Умный банкир для физической Монополии с поддержкой NFC, сетевого мультиплеера и визуального редактора собственных версий игры.</p>
            
            <div class="cta-group">
                <a href="${downloadUrl}" class="btn-download">
                    <span>📥 Скачать APK (${apkSize})</span>
                </a>
                <a href="#pro" class="btn-pro">
                    <span>👑 Получить PRO ключ</span>
                </a>
            </div>
        </div>

        <!-- Features -->
        <div class="features">
            <div class="f-card">
                <div class="f-icon">🎨</div>
                <h3>Редактор Монополий</h3>
                <p>Создавайте свои версии игры: задавайте названия улиц, настраивайте градиентные цвета, авто-баланс цен и аренды с живым 3D-предпросмотром карточек.</p>
            </div>

            <div class="f-card">
                <div class="f-icon">📱</div>
                <h3>Экспресс-прошивка NFC</h3>
                <p>Никаких сторонних программ! Привязывайте и прошивайте самоклеящиеся NFC метки прямо из приложения за 2 минуты в конвейерном режиме.</p>
            </div>

            <div class="f-card">
                <div class="f-icon">🌐</div>
                <h3>Сетевой Мультиплеер</h3>
                <p>Один телефон выступает сервером, а остальные игроки подключаются по локальной сети. Пресет хоста мгновенно передается по воздуху.</p>
            </div>

            <div class="f-card">
                <div class="f-icon">⚖️</div>
                <h3>Сетевые Аукционы и Сделки</h3>
                <p>Интерактивные торги за активы с передачей лидерства и обратным отсчетом, а также удобный экран обмена активами и доплатой.</p>
            </div>

            <div class="f-card">
                <div class="f-icon">🔋</div>
                <h3>Фоновая служба хоста</h3>
                <p>Сервер игры работает в фоне с блокировкой засыпания (KeepScreenOn + Foreground Service) — соединение не прерывается при блокировке экрана.</p>
            </div>

            <div class="f-card" id="pro">
                <div class="f-icon">👑</div>
                <h3>PRO Каталог Шаблонов</h3>
                <p>Скачивайте готовые монополии других пользователей в 1 клик (Киберпанк, Столицы мира) и делитесь своими шедеврами с сообществом.</p>
            </div>
        </div>

        <!-- How to Install -->
        <div class="box">
            <h4>📲 Как установить на Android:</h4>
            <ol>
                <li>Нажмите большую оранжевую кнопку <b>«Скачать APK»</b>.</li>
                <li>Если появится предупреждение «Файл может быть опасным», нажмите <b>«Все равно скачать»</b>.</li>
                <li>Откройте загруженный файл и подтвердите установку (разрешите установку неизвестных приложений для браузера, если потребуется).</li>
                <li>Приложение будет автоматически проверять и устанавливать новые обновления при каждом запуске!</li>
            </ol>
        </div>

        <footer>
            <p>Monopoly Bank Studio • 2026. Разработано для комфортной игры с друзьями.</p>
        </footer>
    </div>
</body>
</html>`;

    res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' });
    res.end(html);
});

server.listen(PORT, '0.0.0.0', () => {
    const localIp = getLocalIp();
    console.log(`[OTA Server] Запущен на http://${localIp}:${PORT}`);
    console.log(`[OTA Server] Страница загрузки: http://${localIp}:${PORT}/`);
    console.log(`[OTA Server] Прямая ссылка: http://${localIp}:${PORT}/MonopolyBank.apk`);
    console.log(`[OTA Server] Каталог шаблонов: http://${localIp}:${PORT}/api/presets/catalog`);
    syncApk();
});
