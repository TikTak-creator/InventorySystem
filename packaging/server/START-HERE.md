# StockFlow server · Windows x64

## English

This archive contains the self-contained HTTPS API server. .NET is included; **MySQL 8 and an HTTPS certificate are not**. End users should install the separate client EXE and get an account from the server administrator.

1. Extract the entire ZIP to a folder owned by the account that will run the server.
2. Prepare MySQL 8, a database account with initialization permissions, and an HTTPS `.pfx` certificate valid for your server's DNS name. Clients must trust its issuing CA. Production MySQL connections require `SslMode=VerifyFull`; specify `SslCa` for a private CA.
3. Open PowerShell in this folder. Initialize the schema, then create the first administrator:

   ```powershell
   # Only if the database must be created and this account has permission:
   $env:Database__CreateDatabase = 'true'
   powershell -NoProfile -ExecutionPolicy Bypass -File .\Start-Server.ps1 -Action init-db
   powershell -NoProfile -ExecutionPolicy Bypass -File .\Start-Server.ps1 -Action create-user
   $env:Database__CreateDatabase = 'false'
   ```

   Each command prompts for the MySQL connection string without displaying it. Example format: `Server=mysql.example.com;Database=inventory_db;User ID=stockflow;Password=<local-secret>;SslMode=VerifyFull;`. `create-user` prompts for the username, role and password; choose `Admin` for the first user. Application passwords require 12–128 characters. There is no default account. Leave demo data disabled for real deployments.
4. Double-click `Start-Server.cmd`, or run `powershell -NoProfile -ExecutionPolicy Bypass -File .\Start-Server.ps1`. Enter the connection string, HTTPS listen address, allowed DNS hostnames and certificate details. For remote access, bind to an appropriate network interface and allow the HTTPS port in your firewall. Do not expose MySQL to clients.
5. Give users the public HTTPS address and their individual accounts. Keep the server terminal open; press Ctrl+C to stop it. The launcher runs in the foreground and does not install a Windows service or configure your firewall.

Existing environment settings can be supplied by a deployment secret manager instead of entering them at every start. The launcher defaults to Production, restores the caller's environment after exit, writes no passwords to disk and does not import certificates or disable TLS checks. Configure a restricted database account for normal operation after initialization. Certificate renewal, backups, operating-system patching and service hosting belong to the administrator.

For upgrades, stop the old server and clients, back up MySQL and configuration, extract the new package into a new folder, run `init-db` with appropriate DDL permissions and then start the new version. Do not run the old direct-database application against the API database for writes.

Detailed guides: [Setup](https://github.com/TikTak-creator/InventorySystem/blob/redbox/server-api/docs/SETUP.md) · [Security](https://github.com/TikTak-creator/InventorySystem/blob/redbox/server-api/docs/SECURITY.md) · [API](https://github.com/TikTak-creator/InventorySystem/blob/redbox/server-api/docs/API.md).

## Русский

Архив содержит готовый HTTPS API-сервер со встроенной .NET. **MySQL 8 и HTTPS-сертификат настраиваются отдельно.** Обычному пользователю нужен отдельный EXE-установщик клиента и учётная запись от администратора.

1. Распакуйте весь ZIP в папку учётной записи, от которой будет работать сервер.
2. Подготовьте MySQL 8, пользователя БД с правами инициализации и HTTPS-сертификат `.pfx` на DNS-имя сервера. Клиенты должны доверять его центру сертификации. В Production подключение MySQL требует `SslMode=VerifyFull`; для частного CA укажите `SslCa`.
3. Откройте PowerShell в этой папке. Создайте схему и первого администратора командами из английского блока выше. `Database__CreateDatabase=true` нужен только при создании самой базы. Строка подключения вводится скрыто для каждой команды. В `create-user` выберите роль `Admin`; пароль — 12–128 символов. Общего стартового пароля нет. Для рабочего развёртывания оставьте демо-данные выключенными.
4. Запустите `Start-Server.cmd` или PowerShell-команду выше. Укажите строку MySQL, HTTPS-адрес прослушивания, разрешённые DNS-имена и параметры сертификата. Для удалённого доступа выберите подходящий сетевой интерфейс и разрешите HTTPS-порт в firewall. Порт MySQL клиентам не открывайте.
5. Выдайте пользователям внешний HTTPS-адрес и отдельные учётные записи. Окно сервера должно оставаться открытым; Ctrl+C останавливает сервер. Этот запуск не создаёт Windows-службу и не меняет firewall.

Вместо ручного ввода можно передавать переменные через хранилище секретов развёртывания. По умолчанию используется Production. Скрипт восстанавливает переменные после завершения, не сохраняет пароли на диск, не импортирует сертификаты и не отключает проверку TLS. После инициализации используйте ограниченную учётную запись БД. Продление сертификата, резервные копии, обновления ОС и запуск как службы настраивает администратор.

Для обновления остановите сервер и старые клиенты, сохраните резервную копию MySQL и конфигурации, распакуйте новый пакет в новую папку, выполните `init-db` с DDL-правами и запустите новую версию. Не допускайте запись старого приложения с прямым доступом к MySQL в базу API.

[Подробный запуск](https://github.com/TikTak-creator/InventorySystem/blob/redbox/server-api/docs/SETUP.md) · [Безопасность](https://github.com/TikTak-creator/InventorySystem/blob/redbox/server-api/docs/SECURITY.md) · [API](https://github.com/TikTak-creator/InventorySystem/blob/redbox/server-api/docs/API.md).
