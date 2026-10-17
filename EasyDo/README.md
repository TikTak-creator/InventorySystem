# EasyDo

EasyDo — библиотека C#, которая убирает повторяющуюся техническую работу из приложения. Экран собирается из таблиц, карточек, полей и областей; операция — из проверок и шагов; работа с БД — из запроса и транзакционного блока.

В StockFlow 2.0 SQL и сценарии перенесены в `Inventory.Storage`, общие модели/правила — в `Inventory.Contracts`. `Inventory.Server/Security/UserStore.cs` также использует Store/Session. В WinForms остались блоки интерфейса; сетевое взаимодействие выполняет `Inventory.Client`. Актуальная карта использования: [README проекта](../README.md#easydo). Описание ниже также сохраняет контекст исходной EasyDo-версии, доступной в `codex/easydo-baseline`.

Она уже используется в StockFlow и отдельно в примере TaskBoard. Ядро не зависит от магазина, MySQL или Windows. Модуль интерфейса использует WinForms и работает на Windows.

## Быстрый старт

Для сценариев и БД подключите `EasyDo.Core/EasyDo.Core.csproj` через `ProjectReference`. Для Windows-интерфейса — `EasyDo.WinForms/EasyDo.WinForms.csproj`: он автоматически подключает ядро. Дополнительные NuGet-пакеты EasyDo не нужны. Драйвер БД выбирает приложение.

```xml
<ItemGroup>
  <ProjectReference Include="../EasyDo/EasyDo.WinForms/EasyDo.WinForms.csproj" />
</ItemGroup>
```

Путь зависит от положения вашего проекта. Для WinForms нужны `net8.0-windows` и `<UseWindowsForms>true</UseWindowsForms>`. Для ядра достаточно `net8.0`; его можно использовать в консоли, сервисе или другом интерфейсе.

```csharp
using EasyDo;
using EasyDo.Data;
using EasyDo.WinForms;
```

## Приложение из блоков

Вот рабочая часть экрана из `Examples/TaskBoard/Program.cs`. `TaskItem` содержит `Title` и `Done`, `Tasks` — `BindingList<TaskItem>`.

```csharp
var grid = ui.Grid().Define<TaskItem>()
    .Column(task => task.Title, "Задача", 80)
    .Column(task => task.Done, "Готово", 20).Apply();
grid.DataSource = Tasks;

var add = ui.Btn("Добавить").OnClick(() => ui.Safe(() => AddTask(input.Text)));
var done = ui.Btn("Завершить", ui.Success).OnClick(() =>
{
    if (grid.Selected<TaskItem>() is { } task)
    {
        task.Done = true;
        Tasks.ResetBindings();
    }
});

ui.Page(ui.TitledCard("Мои задачи", grid))
    .Above(ui.Toolbar([input], [done, add]))
    .Above(ui.PageHeader("Задачи", "Приложение из блоков EasyDo"))
    .Into(this);
```

Полный пример можно запустить из папки InventorySystem:

```powershell
dotnet run --project EasyDo/Examples/TaskBoard
```

Это отдельная доска задач в памяти, без зависимости от StockFlow. Для самостоятельного хранения добавьте репозиторий через `Store` или сохранение в файл.

## Какие блоки есть

| Блок | Что делает |
|---|---|
| `Rules<T>` | Именованные проверки и список ошибок |
| `Flow<T>` | Последовательность действий, проверок и условных шагов |
| `AsyncFlow<T>` | Асинхронные шаги с CancellationToken |
| `Sql` | Неизменяемый запрос с параметрами |
| `Store` | Время жизни соединения, транзакции, commit и rollback |
| `Session` | Execute, Scalar, Query и их async-варианты |
| `UiKit` | Стилизованные контролы, карточки, график и сообщения |
| `UiSkin`, `UiTexts` | Цвета, шрифты и язык конкретного приложения |
| `GridRecipe<T>` | Таблицы с проверяемыми компилятором свойствами |
| `SurfaceRecipe` | Основная область и панели сверху/снизу |
| `MatrixRecipe` | Ячейки, проценты, пиксели и объединение строк/колонок |
| `FormBinding<T>` | Явная загрузка и сохранение полей формы |
| `ViewDeck` | Ленивая навигация, кэш страниц, обновление и освобождение |
| `DebouncedAction` | Отложенное действие с освобождением при закрытии владельца |
| `DesktopApp` | Запуск приложения, культура, инициализация и ошибки |

## Проверки и сценарии

```csharp
var valid = Rules<Order>.Create()
    .Require("customer", order => !string.IsNullOrWhiteSpace(order.Customer), "Нужен клиент.")
    .Require("quantity", order => order.Quantity > 0, "Количество должно быть положительным.");

var submit = Flow<Order>.Start()
    .Check(valid)
    .Then(CalculateTotal)
    .When(order => order.NeedsApproval, RequestApproval)
    .Then(SaveOrder);

submit.Run(order);
```

Имена и функции здесь относятся к вашему приложению. EasyDo выполняет их в указанном порядке. Исключение сразу останавливает оставшиеся шаги. `Validate` возвращает все нарушения; `Ensure` выбрасывает `RuleException`, у которого есть `Failures` с ключами полей.

`Flow` и `Rules` неизменяемы: `Then`, `Check`, `When`, `Require` возвращают новую цепочку. Одну цепочку можно переиспользовать с разными объектами состояния. Сами объекты и функции не становятся автоматически потокобезопасными.

`Flow` сам по себе не откатывает внешние действия. Для изменений БД запускайте его **внутри** `Store.Transaction`. Письмо, сетевой вызов или запись в файл не входят в транзакцию БД. Для внешних эффектов нужно отдельно проектировать повторы и компенсацию.

```csharp
var process = AsyncFlow<Order>.Start()
    .Then(async (state, cancellation) =>
        await ValidateRemotely(state, cancellation))
    .Then(async (state, cancellation) =>
        await SaveAsync(state, cancellation));

await process.RunAsync(order, cancellationToken);
```

Асинхронные шаги последовательны. Отмена проверяется перед каждым шагом; переданный токен также нужно использовать внутри операций. Это не механизм фонового выполнения WinForms: обращаться к контролам следует из потока интерфейса.

## База данных

`Store` принимает фабрику **нового** `DbConnection`. Например, адаптер StockFlow:

```csharp
var store = new Store(() => new MySqlConnection(connectionString));
var query = Sql.TextOnly("SELECT name FROM products WHERE category = @category")
    .Bind("category", selectedCategory);
var names = store.Read(session => session.Query(query, reader => reader.GetString(0)));
```

Подключите драйвер и его namespace в своем приложении. Для другого провайдера замените фабрику соединения и SQL-диалект. EasyDo не переводит MySQL SQL в SQLite/PostgreSQL и не создаёт схему за вас.

Значения передавайте через `Bind`, включая `null` — он преобразуется в `DBNull.Value`. Можно задать `DbType`, если драйверу нужна явная подсказка. Имена таблиц, колонок и SQL-фрагменты параметрами не являются: используйте только доверенные имена или список допустимых значений. Никогда не собирайте SQL из пользовательского текста.

```csharp
store.Transaction(session =>
{
    session.Execute(Sql.TextOnly("UPDATE accounts SET balance = balance - @sum WHERE id = @id")
        .Bind("sum", amount).Bind("id", fromAccount));
    session.Execute(Sql.TextOnly("UPDATE accounts SET balance = balance + @sum WHERE id = @id")
        .Bind("sum", amount).Bind("id", toAccount));
});
```

При успехе вызывается commit; при ошибке — rollback и повторное выбрасывание исходного исключения. Ошибка rollback сохраняется в `exception.Data["EasyDo.RollbackError"]`. Ошибка commit может означать неопределённый результат на сервере: библиотека не делает опасных автоматических повторов.

В одной транзакции все запросы должны использовать **переданную session**. Вызов другого `store.Read`/`Transaction` внутри блока создаст отдельное соединение и отдельную операцию. Вложенных транзакций и savepoint-магии нет.

`Session` действует только внутри callback. `Query` полностью читает данные и закрывает reader до следующего запроса. Соединения, команды и транзакции освобождаются также при исключениях. Нельзя сохранять session, запускать на ней параллельные запросы, возвращать ленивый reader или использовать async callback с синхронным `Read`.

По умолчанию изоляция транзакции — `ReadCommitted`, таймаут команд — 30 секунд. Изоляция передаётся дополнительным аргументом `Transaction`; таймаут — конструктору `Store` (0 означает таймаут, определяемый ADO.NET как бесконечный).

```csharp
await store.TransactionAsync(async (session, ct) =>
{
    await session.ExecuteAsync(Sql.TextOnly("UPDATE jobs SET status = @status WHERE id = @id")
        .Bind("status", "done").Bind("id", jobId), ct);
    return jobId;
}, cancellationToken);
```

`Scalar<T>` преобразует тип провайдера к `T`. SQL NULL разрешён для nullable/reference типа, но для `int`, `decimal` и других non-nullable типов выдаёт понятную ошибку. Идентификатор вставленной строки получается SQL самого провайдера; StockFlow использует `SELECT LAST_INSERT_ID()` на той же session.

## Интерфейс и формы

Тема хранится в экземпляре `UiKit`, поэтому разные приложения могут использовать разные цвета и тексты без изменения библиотеки:

```csharp
using var ui = new UiKit(
    new UiSkin { Accent = Color.SteelBlue },
    new UiTexts { ApplicationTitle = "Мой проект", ErrorTitle = "Ошибка" },
    amount => amount.ToString("N2") + " €");
```

Освобождайте `UiKit` **после закрытия всех окон**, использующих его шрифты. Не освобождайте эти шрифты отдельно. Параметры палитры неизменяемы; для другой темы создайте новый kit.

`Column(row => row.Price, ...)` принимает прямое свойство модели. Переименование свойства проверяет компилятор. Для вычислений добавьте свойство в модель; выражения вида `row => row.Price * 2` запрещены. `weight` — относительный вес ширины; `format` использует стандартный формат .NET; `right` выравнивает числовые колонки.

`BarChart.SetData` принимает пары `(подпись, неотрицательное значение)`. Для отрицательных величин нужен другой график; такие данные отвергаются до изменения текущего набора.

```csharp
grid.BindRows(products, product => product.Id, focusColumn: 1);
var selected = grid.Selected<Product>();
```

Это сохраняет выбранную строку по ключу после обновления данных. Ключи должны быть уникальны в вашем наборе.

```csharp
var layout = ui.Matrix(new[] { 60f, 40f }, new[] { 50f, 50f })
    .Cell(mainTable, 0, 0, rowSpan: 2)
    .Cell(chart, 1, 0)
    .Cell(details, 1, 1).Build();
```

Для фиксированных размеров используйте `Track.Pixels(40)` вместе с `Track.Percent(100)`. `Cell` проверяет пересечения, границы и повторное использование контролов. Рецепты компоновки и таблиц одноразовые: каждый Control принадлежит одному родителю. Создавайте новые рецепты для новых экранов.

`Page(body).Above(toolbar).Above(header)` сохраняет порядок WinForms Dock: последнее добавленное верхнее поле находится выше. `Surface(body).Bottom(footer)` создаёт панель с основной областью и подвалом. `Field(form, label, input, x, y)` полезен для диалогов с точной геометрией.

```csharp
var binding = new FormBinding<Customer>()
    .Text(nameBox, model => model.Name, (model, text) => model.Name = text.Trim())
    .Choice(categoryBox, model => model.Category, (model, text) => model.Category = text);

binding.Load(existingCustomer);
var edited = binding.Save(new Customer());
```

Во время ввода исходная модель не меняется. `Save` записывает в переданный объект — используйте новый объект, затем проверяйте `Rules` и сохраняйте через репозиторий. Для других типов полей есть общий `Bind`. Валидация, диапазоны чисел и правила подтверждения принадлежат приложению.

```csharp
var deck = new ViewDeck(contentPanel, page => ui.Safe(page.RefreshData));
deck.Register("tasks", () => new TasksView());
deck.Register("reports", () => new ReportsView());
deck.Show("tasks");
```

Фабрика вызывается при первом переходе, страница кэшируется. `IRefreshable.RefreshData` вызывается при посещении. Все страницы, включая скрытые, закрываются вместе с владельцем; можно также вызвать `Dispose` вручную. Не отдавайте ViewDeck контролы, принадлежащие другому окну.

## Как EasyDo применён в StockFlow

| Код приложения | Используемые блоки |
|---|---|
| `Program` | DesktopApp и освобождение UiKit |
| `UI/AppStyle` | Палитра, русские тексты, прежнее округление суммы до целых сомов |
| `MainForm` | ViewDeck, кнопки действий, прежний sidebar |
| Все 4 раздела | Typed grids, Page, Surface, Columns, Matrix |
| `ProductEditForm` | Field и FormBinding, проверки товара |
| `SaleForm` | Typed grids, Surface, receipt с сохранённой суммой |
| `ProductRepository` | Sql, Session, Store, Rules |
| `SaleRepository` | Flow внутри одной транзакции, блокировки товаров |
| `Database`, `DemoData` | Адаптер MySQL, прежняя схема и генератор демо-данных |

Свойства модели, формулы прибыли, состав показателей, оформление и горячие клавиши сохранены. Добавлены проверки некорректного количества на уровне репозиториев, объединение одинаковых товаров перед проверкой остатков, одинаковый порядок блокировок и корректная сумма уведомления при изменившейся цене. Форма по-прежнему показывает цены загруженной корзины; продажа использует актуальные цены БД.

EasyDo не скрывает правила магазина: оформление чека видно как `LockAndCheckStock → SaveReceipt → SaveItemsAndDeductStock`. Для новой идеи заменяются модели, SQL и шаги, а работа с ресурсами и компоновкой остаётся той же.

## Проверка

Из папки InventorySystem:

```powershell
dotnet build InventorySystem.slnx
dotnet run --project EasyDo/Tests/EasyDo.Core.Specs
dotnet run --project EasyDo/Tests/StockFlow.Specs
dotnet run --project EasyDo/Examples/TaskBoard -- --smoke
```

У тестов нет зависимости от xUnit/NUnit и загрузки дополнительных пакетов. Это исполняемые regression suites: при нарушении проверки процесс возвращает ненулевой код. Core.Specs можно запускать без Windows. StockFlow.Specs проверяет настоящий WinForms, поэтому требует Windows и desktop runtime.

Для MySQL-интеграции:

```powershell
dotnet run --project EasyDo/Tests/StockFlow.Specs -- --mysql --settings InventoryApp/appsettings.json
```

Тест создаёт уникальную базу `easydo_test_<guid>`, проверяет репозитории, откат при ошибке записи позиции, конкурентные продажи, удаление товара и демо-данные, затем удаляет **только свою** временную БД в finally. Нужны права создания/удаления БД и триггера. Основная база и appsettings не изменяются.

Если MySQL установлен, но основной сервер выключен, можно запустить независимый временный сервер:

```powershell
./EasyDo/Tests/Run-IsolatedMySql.ps1 -MySqlBase "C:/Program Files/MySQL/MySQL Server 8.4"
```

Он использует новые файлы данных в `.artifacts`, слушает только `127.0.0.1:33317`, выполняет suite и выключается в finally. Порт можно изменить через `-Port`. Системная служба и данные установленного сервера не используются. Временная учётная запись root без пароля относится только к этому тестовому серверу. Логи и файлы данных остаются для диагностики.

Для сравнения со старой версией сохраните старую сборку вне текущей output-папки, затем:

```powershell
dotnet run --project EasyDo/Tests/StockFlow.Specs -- --baseline PATH/StockFlow.dll --output .artifacts/visual-parity
```

Сравниваются дерево контролов, геометрия, цвета, шрифты, колонки и пиксели восьми окон/разделов. PNG и текстовые снимки сохраняются для проверки. Сравнение конструирует окна без запуска приложения и чтения основной БД; оно проверяет фиксированные размеры и тестовые показатели, а не все возможные размеры экрана и состояния данных.

## Границы инструмента

EasyDo — набор совместимых блоков, а не язык, автоматически понимающий любую идею. Сложные правила всё ещё нужно выражать функциями, SQL или новым блоком. При этом новый блок можно писать один раз и переиспользовать в следующем сценарии.

WinForms-модуль привязан к Windows; для веба или мобильного интерфейса понадобится другой адаптер над тем же Core. Библиотека не добавляет авторизацию, миграции схемы, резервное копирование, аудит операций или универсальную ORM. Синхронные запросы StockFlow остаются синхронными; переход UI на async требует отдельной проработки состояний загрузки. Прямое редактирование остатка в карточке сохраняет прежнее поведение последнего сохранения при одновременном редактировании — это отдельное правило магазина, а не гарантия Store.

Подробности устройства и добавления своих блоков: [ARCHITECTURE.md](ARCHITECTURE.md).
