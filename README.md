# Claude Code Launcher

**RU** | Десктоп-лаунчер для [Claude Code](https://docs.anthropic.com/en/docs/claude-code) с бесплатным провайдером Kiro (OmniRoute) и поддержкой любого OpenAI-совместимого API.

**EN** | Desktop launcher for [Claude Code](https://docs.anthropic.com/en/docs/claude-code) with free Kiro (OmniRoute) provider and any OpenAI-compatible API.

![.NET 10](https://img.shields.io/badge/.NET-10.0-blueviolet)
![WPF](https://img.shields.io/badge/UI-WPF-blue)
![Version](https://img.shields.io/badge/version-1.2.0-green)

---

## 📥 Скачать / Download

Скачай последний релиз → [Releases](../../releases) → `ClaudeLauncher-v1.2.0-win-x64.zip`

Download the latest release → [Releases](../../releases) → `ClaudeLauncher-v1.2.0-win-x64.zip`

---

## ⚡ Быстрый старт / Quick Start

### Что нужно / Requirements

| Требование | Зачем | Установка |
|---|---|---|
| **Windows 10/11** (x64) | Платформа | — |
| **Node.js 22** | OmniRoute + прокси | [nodejs.org](https://nodejs.org) |
| **OmniRoute** | Шлюз для Kiro | `npm install -g omniroute` |
| **Claude Code** | CLI-инструмент | `npm install -g @anthropic-ai/claude-code` |

### Шаги / Steps

1. Установи Node.js 22, OmniRoute, Claude Code (см. таблицу выше)
2. Скачай и распакуй [последний релиз](../../releases)
3. Запусти `ClaudeLauncher.exe`
4. При первом запуске введи токен OmniRoute и URL шлюза
5. Выбери модель, папку проекта → **Launch** 🚀

---

## 🔧 Полная установка / Full Setup Guide

<details>
<summary><b>🇷🇺 Инструкция на русском</b></summary>

### 1. Node.js 22

> ⚠️ Нужна именно **версия 22**

Скачай с [nodejs.org](https://nodejs.org) → **Node.js 22 LTS**. Установи с настройками по умолчанию.

```bash
node --version   # должно быть v22.x.x
```

### 2. OmniRoute

```bash
npm install -g omniroute
omniroute --version
```

### 3. Настроить OmniRoute + Kiro

Запусти OmniRoute:
```bash
omniroute
```
Откроется браузер с UI OmniRoute.

**Добавь провайдер:**
1. Зайди в **Провайдеры** (Providers)
2. Нажми **Kiro AI** → **Добавить соединение** (Add Connection) → **Builder ID**
3. Войди через Google аккаунт

**Создай API-ключ:**
1. Зайди в **Менеджер API** (API Manager)
2. **Создать ключ** (Create Key) → придумай название
3. **Скопируй ключ сразу!** Он показывается только один раз

**Скопируй URL шлюза:**
- Раздел **Конечные точки** (Endpoints)
- Скопируй URL (обычно `http://localhost:20128`)

### 4. Claude Code

```bash
npm install -g @anthropic-ai/claude-code
claude --version
```

### 5. Запуск лаунчера

1. Скачай из [Releases](../../releases), распакуй
2. Запусти `ClaudeLauncher.exe`
3. **Первый запуск** — окно настроек:
   - **Claude Code** → нажми **Detect** (или путь вручную)
   - **OmniRoute API Key** — вставь ключ из шага 3
   - **OmniRoute Gateway URL** — вставь URL из шага 3
   - Нажми **Get Started**
4. Выбери **Kiro**, модель, папку проекта → **Launch** 🚀

</details>

<details>
<summary><b>🇬🇧 Instructions in English</b></summary>

### 1. Node.js 22

> ⚠️ Must be **version 22**

Download from [nodejs.org](https://nodejs.org) → **Node.js 22 LTS**. Install with default settings.

```bash
node --version   # should be v22.x.x
```

### 2. OmniRoute

```bash
npm install -g omniroute
omniroute --version
```

### 3. Set up OmniRoute + Kiro

Start OmniRoute:
```bash
omniroute
```
A browser window opens with the OmniRoute UI.

**Add a provider:**
1. Go to **Providers**
2. Click **Kiro AI** → **Add Connection** → **Builder ID**
3. Sign in with your Google account

**Create an API key:**
1. Go to **API Manager**
2. **Create Key** → pick a name
3. **Copy the key immediately!** It's shown only once

**Copy the gateway URL:**
- Go to **Endpoints** section
- Copy the URL (usually `http://localhost:20128`)

### 4. Claude Code

```bash
npm install -g @anthropic-ai/claude-code
claude --version
```

### 5. Launch

1. Download from [Releases](../../releases), extract
2. Run `ClaudeLauncher.exe`
3. **First run** — Settings screen:
   - **Claude Code** → click **Detect** (or enter path manually)
   - **OmniRoute API Key** — paste key from step 3
   - **OmniRoute Gateway URL** — paste URL from step 3
   - Click **Get Started**
4. Select **Kiro**, pick a model, choose project folder → **Launch** 🚀

</details>

---

## 🔄 Мульти-аккаунт / Multi-Account (больше бесплатных кредитов)

Кредиты Kiro привязаны к Builder ID аккаунту. Чтобы получить в 2–3 раза больше бесплатных кредитов, добавь несколько аккаунтов в OmniRoute — он автоматически переключится на следующий когда на текущем закончатся кредиты.

Kiro credits are tied to your Builder ID account. To get 2–3× more free credits, add multiple accounts in OmniRoute — it automatically switches to the next one when the current runs out.

<details>
<summary><b>🇷🇺 Гайд по мульти-аккаунту</b></summary>

### Как это работает

OmniRoute поддерживает несколько Kiro-подключений одновременно. Каждое подключение — это отдельный Builder ID (отдельный Google аккаунт). Когда на одном аккаунте заканчиваются кредиты (429/quota), OmniRoute автоматически маршрутизирует запросы на следующий. Никаких изменений в лаунчере не нужно.

### Пошаговая настройка (2–3 аккаунта)

**Шаг 1. Подготовь Google аккаунты**

Тебе нужны отдельные Google аккаунты (разные email). Каждый используется для отдельного Builder ID.

| # | Google аккаунт | Builder ID |
|---|---|---|
| 1 | `твой.основной@gmail.com` | Основной |
| 2 | `второй.аккаунт@gmail.com` | Запасной 1 |
| 3 | `третий.аккаунт@gmail.com` | Запасной 2 |

**Шаг 2. Добавь подключения в OmniRoute**

1. Открой OmniRoute UI (`omniroute` в терминале)
2. Перейди в **Провайдеры** → **Kiro AI**
3. Нажми **Добавить соединение** (Add Connection) → **Builder ID**
4. Войди через **второй** Google аккаунт
5. Повтори для **третьего** аккаунта

Теперь у тебя 3 подключения под одним провайдером Kiro:
```
Kiro AI
├── Connection 1 (основной@gmail.com) ✅
├── Connection 2 (второй@gmail.com)   ✅
└── Connection 3 (третий@gmail.com)    ✅
```

**Шаг 3. Проверь**

Ничего менять в лаунчере не нужно. Тот же API-ключ OmniRoute работает для всех подключений.

Когда на аккаунте 1 заканчиваются кредиты:
1. OmniRoute получает ошибку 429/quota от Kiro
2. Помечает аккаунт 1 как `credits_exhausted`
3. Автоматически маршрутизирует следующий запрос на аккаунт 2
4. Лаунчер не знает и не должен знать об этом — всё прозрачно

### Безопасность

- ✅ **Несколько Builder ID с разными email — это нормально.** Amazon Builder ID привязан к email; разные email = полностью независимые аккаунты
- ✅ **OmniRoute поддерживает это нативно** — каждое подключение имеет изолированную OAuth-сессию (добавлено в OmniRoute v3.8.0)
- ⚠️ **Не используй один email для нескольких аккаунтов** — система может создать дублирующиеся аккаунты и взимать двойную плату
- ⚠️ **Не злоупотребляй** — 2–3 аккаунта для личного использования это нормально, массовое создание аккаунтов может привести к блокировке

### Когда кредиты обновляются?

Кредиты Kiro Free Tier обновляются ежемесячно. Точная дата зависит от момента создания аккаунта.

</details>

<details>
<summary><b>🇬🇧 Multi-Account Guide</b></summary>

### How It Works

OmniRoute supports multiple Kiro connections simultaneously. Each connection is a separate Builder ID (separate Google account). When one account's credits run out (429/quota), OmniRoute automatically routes requests to the next one. No launcher changes needed.

### Setup (2–3 Accounts)

**Step 1. Prepare Google accounts**

You need separate Google accounts (different emails). Each is used for a separate Builder ID.

| # | Google account | Builder ID |
|---|---|---|
| 1 | `your.main@gmail.com` | Primary |
| 2 | `second.account@gmail.com` | Backup 1 |
| 3 | `third.account@gmail.com` | Backup 2 |

**Step 2. Add connections in OmniRoute**

1. Open OmniRoute UI (`omniroute` in terminal)
2. Go to **Providers** → **Kiro AI**
3. Click **Add Connection** → **Builder ID**
4. Sign in with your **second** Google account
5. Repeat for the **third** account

You now have 3 connections under one Kiro provider:
```
Kiro AI
├── Connection 1 (main@gmail.com)    ✅
├── Connection 2 (second@gmail.com)  ✅
└── Connection 3 (third@gmail.com)   ✅
```

**Step 3. Verify**

No changes in the launcher needed. The same OmniRoute API key works for all connections.

When account 1 runs out of credits:
1. OmniRoute gets a 429/quota error from Kiro
2. Marks account 1 as `credits_exhausted`
3. Automatically routes the next request to account 2
4. The launcher doesn't know and doesn't need to — it's transparent

### Safety

- ✅ **Multiple Builder IDs with different emails is fine.** Amazon Builder ID is tied to an email; different emails = fully independent accounts
- ✅ **OmniRoute supports this natively** — each connection has an isolated OAuth session (added in OmniRoute v3.8.0)
- ⚠️ **Don't use the same email for multiple accounts** — the system may create duplicate accounts and charge twice
- ⚠️ **Don't abuse it** — 2–3 accounts for personal use is normal, mass account creation may lead to suspension

### When Do Credits Refresh?

Kiro Free Tier credits refresh monthly. The exact date depends on when the account was created.

</details>

---

## 🔌 Провайдеры / Providers

| Provider | Type | Price | How to get |
|---|---|---|---|
| **Kiro** (OmniRoute) ✅ | Anthropic gateway | Free | Token via [kiro.dev](https://kiro.dev) + OmniRoute |
| **Custom API** (optional) | OpenAI-compatible | Varies | Any OpenAI-compatible endpoint |
| — OpenRouter | OpenAI-compatible | $1 free, then paid | Key at [openrouter.ai](https://openrouter.ai) |

> 💡 Кiro — основной бесплатный метод. Custom API нужен только если хочешь использовать платный провайдер вроде OpenRouter.
>
> Kiro is the primary free method. Custom API is only needed for paid providers like OpenRouter.

---

## 🖥️ Интерфейс / Interface

**Главное окно / Main Window:**
- **PROJECT** — рабочая папка / workspace folder for Claude Code
- **PROVIDER** — Kiro или Custom API / switch between providers
- **MODEL** — выбор модели из каталога / model from provider's catalog
- **CREDENTIALS** — проверка ключа / pre-launch key check
- **ROUTE** — маршрут запросов / request routing path
- **Launch without project** — запуск без привязки к папке / clean session

**Settings** — настройка подключений, URL, ключей, пути к Claude.

---

## 🛠️ Сборка из исходников / Build from Source

Требуется [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
# Клонируй / Clone
git clone https://github.com/YOUR-USERNAME/claude-code-launcher.git
cd claude-code-launcher

# Собери / Build (Windows)
publish.bat

# Или вручную / Or manually
dotnet publish Claude\ClaudeLauncher.csproj -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true -o publish\ClaudeLauncher
```

> ⚠️ `publish.bat` автоматически очищает `bin/` и `obj/` перед сборкой. Если собираешь вручную и видишь старый UI — удали эти папки: `rmdir /s /q Claude\bin Claude\obj`

---

## 📁 Структура / Structure

```
claude-code-launcher/
├── README.md                         ← you are here
├── ClaudeLauncher.slnx               ← solution file
├── publish.bat                       ← build script (auto-cleans)
├── .github/workflows/release.yml     ← GitHub Actions CI
└── Claude/                           ← project folder
    ├── ClaudeLauncher.csproj
    ├── config.json                   ← default settings (no keys)
    ├── Views/                        ← WPF windows (Main, Settings)
    ├── ViewModels/                   ← MVVM view models
    ├── Services/                     ← Business logic
    │   ├── Providers/                ← Kiro, Custom API
    │   ├── Gateway/                  ← Gateway management
    │   └── Launcher/                 ← Launch engine
    ├── Resources/CompletionsProxy/   ← Node.js API translator proxy
    ├── Assets/Icons/                 ← App icons
    └── Styles/                       ← WPF styles & themes
```

---

## ❓ FAQ

**Q: OmniRoute не запускается / OmniRoute won't start**
A: Проверь Node.js 22: `node --version`. Перезапусти терминал после установки. / Check Node.js 22. Restart terminal after install.

**Q: Claude Code не находится / Claude Code not found**
A: `npm install -g @anthropic-ai/claude-code`, перезапусти лаунчер. / Restart the launcher.

**Q: Закончились кредиты / Credits exhausted**
A: Добавь ещё один Builder ID в OmniRoute (см. раздел Мульти-аккаунт). / Add another Builder ID in OmniRoute (see Multi-Account section).

**Q: Ошибка "Key rejected"**
A: Проверь ключ в OmniRoute → API Manager. Создай новый если нужно. / Check key in OmniRoute → API Manager. Create a new one if needed.

**Q: Модель не найдена / Model not found**
A: Нажми 🔄 на главном экране. / Click 🔄 on the main screen.

**Q: При сборке UI показывает старые надписи / Old labels after rebuild**
A: Удали `bin/` и `obj/` перед сборкой, или используй `publish.bat` (он делает это автоматически). / Delete `bin/` and `obj/` before building, or use `publish.bat` (it cleans automatically).

---

## License

MIT

---

*Built by seth · [Telegram @sethosint](https://t.me/sethosint)*
