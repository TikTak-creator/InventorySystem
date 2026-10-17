# Installing StockFlow · Установка StockFlow

## English

Download release **v2.0.0** from [GitHub Releases](https://github.com/TikTak-creator/InventorySystem/releases/tag/v2.0.0). The release packages the latest `redbox/server-api` version for Windows 10 2004+ / Windows 11 **x64**.

| File | Who needs it | Contents |
| :--- | :--- | :--- |
| `StockFlow-2.0.0-Setup-win-x64.exe` | Store staff | Windows client installer, bundled .NET, Start-menu shortcut, optional desktop shortcut and Windows uninstaller |
| `StockFlow-2.0.0-Server-win-x64.zip` | Server administrator | Standalone HTTPS API executable, bundled .NET, OpenAPI, foreground launcher and bilingual setup instructions |
| `StockFlow-2.0.0-Source.zip` | Developers | Source at the release commit, documentation and release build scripts |
| `SHA256SUMS.txt` | Anyone verifying a download | SHA-256 hashes of the three release packages |

### For end users

1. Download and run the **Setup EXE**. It installs for the current Windows user without requiring administrator elevation. Choose English or Russian for the installer.
2. Open **StockFlow** from the Start menu.
3. Enter the HTTPS server address, username and password supplied by your administrator. The server must already be configured and running.

The installer includes .NET. You do not need the SDK, MySQL or database credentials on the client computer. The application interface is in Russian. Uninstall through **Windows Settings → Apps → Installed apps → StockFlow**. Uninstalling the client does not remove server data; an existing `appsettings.json` is preserved during upgrades and uninstall.

The application and installer are **not code-signed** in this release, so Windows may show an unknown-publisher or SmartScreen notice. SHA-256 hashes verify file integrity; they do not replace a code-signing certificate. Download packages from this repository's release page.

### For server administrators

Extract the whole **Server ZIP**, then follow its `START-HERE.md`. The API includes .NET, while **MySQL 8, an HTTPS certificate, certificate trust and firewall configuration remain separate deployment requirements**. The launcher uses Production by default, prompts privately for secrets and runs the server in the foreground. It does not install a Windows service, change the firewall or provision a database server. Create the database schema and the first administrator before giving users access.

[Server setup](SETUP.md) · [Security](SECURITY.md) · [API](API.md).

### For developers: reproduce a release

On Windows x64, install .NET SDK 9.0.200+ (or a newer SDK supporting .NET 8 targets), Git and Windows PowerShell 5.1 or PowerShell 7. Commit all source changes, then run from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build-Release.ps1
```

The script builds untrimmed, self-contained `win-x64` client and server packages with **.NET 8.0.32**, creates the installer using **Inno Setup 6.7.3**, archives the exact source commit with `git archive`, and writes hashes. If needed, the official Inno Setup compiler is downloaded into `.artifacts/release-tools`, checked against a pinned SHA-256 and its publisher's Authenticode signature, and extracted in portable mode. It does not install the compiler globally. [Official Inno Setup downloads](https://jrsoftware.org/isdl.php).

Output: `.artifacts/releases/2.0.0/`. Existing nonempty output folders are rejected; choose a new `-OutputDirectory` for another build. `-InnoCompilerPath` accepts an existing `ISCC.exe`, and `-RuntimeVersion` selects a patched .NET 8 runtime. Update the runtime deliberately for future releases. Build dependencies require network access when absent from the local cache. Every binary package includes `release-manifest.json` with its source commit, runtime and architecture.

Before publishing, verify the installer, upgrade, uninstall, installed client startup, server startup and API against an isolated test database. Tag that exact commit and attach all four files to the GitHub Release. The source archive intentionally excludes untracked files, build output, local secrets and temporary test data.

## Русский

Готовые файлы версии **v2.0.0** находятся в [GitHub Releases](https://github.com/TikTak-creator/InventorySystem/releases/tag/v2.0.0). Релиз собирается из `redbox/server-api` для Windows 10 2004+ / Windows 11 **x64**.

| Файл | Для кого | Содержание |
| :--- | :--- | :--- |
| `StockFlow-2.0.0-Setup-win-x64.exe` | Сотрудник магазина | Установщик клиента со встроенной .NET, ярлыком в «Пуск», необязательным ярлыком на рабочем столе и удалением через Windows |
| `StockFlow-2.0.0-Server-win-x64.zip` | Администратор сервера | Готовый HTTPS API-сервер со встроенной .NET, OpenAPI, скриптом запуска и инструкциями EN/RU |
| `StockFlow-2.0.0-Source.zip` | Разработчик | Исходники релизного коммита, документация и скрипты сборки релиза |
| `SHA256SUMS.txt` | Для проверки загрузки | SHA-256 трёх пакетов |

### Обычному пользователю

1. Скачайте и запустите **Setup EXE**. Установка выполняется для текущего пользователя Windows без запроса прав администратора. Язык установщика — русский или английский.
2. Откройте **StockFlow** в меню «Пуск».
3. Введите HTTPS-адрес сервера, логин и пароль, которые выдал администратор. Сервер должен быть настроен и запущен заранее.

.NET уже включён. SDK, MySQL и пароль базы на клиентском компьютере не нужны. Интерфейс программы — русский. Удаление: **Параметры Windows → Приложения → Установленные приложения → StockFlow**. Удаление клиента не затрагивает данные сервера; существующий `appsettings.json` сохраняется при обновлении и удалении.

В этом релизе приложение и установщик **без цифровой подписи издателя**: Windows может показать предупреждение неизвестного издателя или SmartScreen. SHA-256 проверяет целостность файла, но не заменяет сертификат подписи кода. Загружайте файлы со страницы релиза этого репозитория.

### Администратору сервера

Полностью распакуйте **Server ZIP** и следуйте `START-HERE.md` внутри. .NET включён; **MySQL 8, HTTPS-сертификат, доверие сертификату и firewall настраиваются отдельно**. Скрипт по умолчанию использует Production, скрыто запрашивает секреты и держит сервер в открытом терминале. Он не создаёт Windows-службу, не меняет firewall и не устанавливает СУБД. Перед подключением пользователей создайте схему и первого администратора.

[Настройка сервера](SETUP.md) · [Безопасность](SECURITY.md) · [API](API.md).

### Разработчику: повторная сборка

Нужны Windows x64, SDK .NET 9.0.200+ или более новый с поддержкой целей .NET 8, Git и Windows PowerShell 5.1 либо PowerShell 7. Сохраните исходники в коммит и выполните команду из английского блока выше.

`Build-Release.ps1` собирает клиент и сервер для `win-x64` со встроенной **.NET 8.0.32**, создаёт установщик через **Inno Setup 6.7.3**, архивирует точный коммит через `git archive` и записывает хэши. При отсутствии компилятора он загружается с официального GitHub в `.artifacts/release-tools`, проверяется по закреплённому SHA-256 и подписи издателя и распаковывается в переносном режиме без глобальной установки.

Готовые файлы: `.artifacts/releases/2.0.0/`. Непустая выходная папка не перезаписывается; для повторной сборки укажите новый `-OutputDirectory`. `-InnoCompilerPath` задаёт существующий `ISCC.exe`, `-RuntimeVersion` — версию исправлений .NET 8. Для будущих релизов обновляйте её осознанно. Отсутствующие в кэше зависимости загружаются из сети. `release-manifest.json` внутри бинарных пакетов фиксирует коммит, runtime и архитектуру.

Перед публикацией проверьте установку, обновление, удаление, запуск установленного клиента, сервера и API на изолированной тестовой базе. Создайте тег точного коммита и приложите четыре файла к GitHub Release. ZIP исходников не включает неотслеживаемые файлы, результаты сборки, локальные секреты и временные тестовые данные.
