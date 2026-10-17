# Запуск StockFlow 2.0

Готовые файлы без сборки исходников: [установщик клиента, ZIP сервера и исходники](RELEASES.md#русский). В бинарные пакеты включена .NET; MySQL и HTTPS-сертификат для сервера настраиваются отдельно.

## Состав

- `Inventory.Server` — самостоятельный ASP.NET Core HTTPS-сервер, аутентификация и API.
- `Inventory.Storage` — серверная работа с MySQL через EasyDo.
- `Inventory.Contracts` — общие модели и DTO протокола.
- `Inventory.Client` — переносимый C# HTTP-клиент для внешних программ и WinForms.
- `InventoryApp` — Windows-приложение: адрес сервера, логин, пароль и прежние рабочие разделы.

MySQL хранит данные; сервер управляет доступом к ним. Клиентам MySQL-драйвер, доступ к порту 3306 и пароль базы не нужны. Сервер можно запускать отдельно от настольной программы.

## Требования

Для сборки решения `.slnx`: **SDK .NET 9.0.200+**, Windows для WinForms-проектов, MySQL 8. Целевая платформа приложений — .NET 8. С SDK 8 можно собирать отдельные `.csproj`; поддержка SLNX добавлена [в SDK 9.0.200](https://devblogs.microsoft.com/dotnet/introducing-slnx-support-dotnet-cli/). Для запуска framework-dependent сервера нужен **ASP.NET Core 8 Runtime**, для клиента — **.NET Desktop 8 Runtime**. Устанавливайте актуальные исправления выбранной версии. Наличие одного .NET SDK 10 не означает наличие ASP.NET Core 8 Runtime; проверьте `dotnet --list-runtimes`.

Все команды ниже выполняются из `InventorySystem`.

```powershell
dotnet restore InventorySystem.slnx
dotnet build InventorySystem.slnx -c Release
```

Если нужен только сервер на Linux/macOS, собирайте `Inventory.Server/Inventory.Server.csproj`, не Windows-проекты решения.

## Локальная разработка

1. Запустите MySQL. Создайте отдельного пользователя и базу для приложения либо временно выдайте учётной записи инициализации право создания базы/таблиц. Не открывайте MySQL внешним клиентам.
2. Настройте локальный доверенный HTTPS-сертификат:

```powershell
dotnet dev-certs https --trust
```

3. Задайте параметры **в серверном терминале**. Строка подключения вводится локально; в репозиторий она не записывается:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ConnectionStrings__Default = Read-Host "Строка подключения MySQL"
$env:Database__CreateDatabase = "true"
```

Пример формы строки: `Server=localhost;Port=3306;Database=inventory_db;User ID=stockflow;Password=<ваш пароль>;SslMode=Required;`. `Required` шифрует соединение MySQL, но не проверяет идентичность сервера; такое послабление относится только к разработке. Если база уже создана и у пользователя нет права CREATE DATABASE, задайте `Database__CreateDatabase=false`.

4. Инициализируйте бизнес-таблицы и таблицы пользователей/сессий:

```powershell
dotnet run --project Inventory.Server -- init-db
```

Существующие товары и чеки не удаляются. При необходимости демо-данных задайте `Database__SeedDemoData=true` перед инициализацией; по умолчанию демо выключено.

5. Создайте первого администратора через локальную серверную команду:

```powershell
dotnet run --project Inventory.Server -- create-user
```

Команда спросит логин, роль (`Admin`, `Operator`, `Viewer`) и пароль (12–128 символов). В интерактивном терминале пароль не отображается. Нет общего стартового пароля или публичной регистрации. Создавайте первого пользователя с ролью `Admin`.

6. Уберите разрешение на создание базы и запустите сервер:

```powershell
$env:Database__CreateDatabase = "false"
dotnet run --project Inventory.Server
```

По умолчанию адрес — `https://localhost:7071`. После ручного `init-db` сервер не выполняет DDL при старте (`Database:InitializeSchema=false`). Для одноразового управляемого развёртывания можно включить InitializeSchema, затем снова отключить и вернуть ограниченную учётную запись БД.

7. В другом терминале запустите приложение:

```powershell
dotnet run --project InventoryApp
```

Введите адрес HTTPS-сервера, логин и пароль созданного пользователя. В `InventoryApp/appsettings.json` хранится только адрес по умолчанию. Пароль и токен не сохраняются на диске. «Выйти» закрывает окно и отзывает сессию; для нового входа запустите клиент снова. Истёкшая сессия требует повторного входа.

## Рабочий сервер

На сервере используйте `Production`, доверенный сертификат на фактическое DNS-имя и ограниченного MySQL-пользователя. Сервер в Production требует **`SslMode=VerifyFull`**: зашифрованное соединение с проверкой CA и имени MySQL-сервера. Укажите `SslCa` при использовании частного центра сертификации.

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:ConnectionStrings__Default = Read-Host "MySQL connection with SslMode=VerifyFull"
$env:Urls = "https://0.0.0.0:7071"
$env:AllowedHosts = "stockflow.example.com"
$env:Kestrel__Certificates__Default__Path = "C:/secure/stockflow.pfx"
$env:Kestrel__Certificates__Default__Password = Read-Host "Пароль сертификата"
dotnet Inventory.Server.dll
```

Используйте реальные домен и путь к сертификату. Пароли задавайте через хранилище секретов службы/платформы; переменные среды — минимальный пример конфигурации. Закройте файлы сертификата, секреты и резервные копии ACL-правами серверной учётной записи. Значение `AllowedHosts` задаётся отдельно от адресов прослушивания. Для исходящих к MySQL соединений используйте сетевой firewall и разрешайте только сервер приложения.

Установите часовой пояс бизнеса одинаково для сервера приложения и MySQL: существующие отчёты используют календарный день магазина. Сессии используют UTC и от этой настройки не зависят.

API не принимает HTTP и не включает CORS. Для настольных клиентов и серверных интеграций CORS не нужен. Если добавляется браузерный клиент, разрешите только его конкретный origin. При reverse proxy сохраняйте TLS до Kestrel; доверие произвольным Forwarded-заголовкам не включено. Распределённый rate limiter, несколько инстансов за прокси и внешняя идентификация требуют отдельной конфигурации.

## Публикация

```powershell
dotnet publish Inventory.Server -c Release -o publish/server
dotnet publish InventoryApp -c Release -r win-x64 --self-contained false -o publish/client
```

Серверная и клиентская папки развёртываются раздельно. Пароль БД не добавляется в клиентскую поставку. На сервере конфигурация с секретами задаётся после публикации. Локальные `.pfx`, `.pem`, `.key`, `.env` и `appsettings.*.local.json` исключены из Git.

## Обновление существующей базы

Остановите старые клиенты, сделайте резервную копию и запустите `init-db` новой версии с полномочиями DDL. Сохраняются таблицы `products`, `sales`, `sale_items`, `price_history`; добавляются `sale_requests`, `app_users`, `app_sessions`. Затем создайте пользователей и запускайте новые клиенты через API. Старый прямой доступ к MySQL нужно закрыть отдельно: старый клиент не проверяет серверные роли и версии карточек.

Схема создаётся идемпотентно через `CREATE TABLE IF NOT EXISTS`; это не универсальный механизм миграции произвольно изменённых таблиц. Не запускайте старую версию параллельно с API для записи в ту же базу.

## Проверки

```powershell
dotnet run --project EasyDo/Tests/EasyDo.Core.Specs
dotnet run --project EasyDo/Tests/StockFlow.Specs
./EasyDo/Tests/Run-IsolatedMySql.ps1 -IncludeApi
```

Последняя команда использует установленный MySQL Server из указанного `-MySqlBase`, создаёт отдельные файлы данных в `.artifacts` и поднимает HTTPS-сервер на случайном локальном порту с тестовым сертификатом. Временные базы удаляются, серверы останавливаются. Основная база не затрагивается. Нужны рабочие .NET 8 runtimes и свободный порт MySQL (по умолчанию 33317, меняется через `-Port`).

Тестовый клиент доверяет только точному отпечатку временного сертификата. Производственный клиент сохраняет обычную проверку TLS и не имеет переключателя «игнорировать сертификат».
