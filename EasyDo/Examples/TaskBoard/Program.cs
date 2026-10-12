using System.ComponentModel;
using EasyDo;
using EasyDo.WinForms;

namespace TaskBoard;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        using var ui = new UiKit(texts: new UiTexts { ApplicationTitle = "EasyDo · Tasks" });
        if (args.Contains("--smoke"))
        {
            using var window = new Board(ui);
            window.AddTask("Build a reusable block");
            if (window.Tasks.Count != 1) throw new Exception("Task was not added.");
            var emptyRejected = false;
            try { window.AddTask(" "); } catch (RuleException) { emptyRejected = true; }
            if (!emptyRejected || window.Tasks.Count != 1) throw new Exception("Validation failed.");
            window.Tasks[0].Done = true;
            Console.WriteLine("PASS reusable task board: composition, add, validation, complete");
            return;
        }
        DesktopApp.Run(ui, "ru-RU", () => { }, () => new Board(ui), error => ui.Safe(() => throw error));
    }
}

public sealed class TaskItem
{
    public string Title { get; set; } = "";
    public bool Done { get; set; }
}

public sealed class Board : Form
{
    public BindingList<TaskItem> Tasks { get; } = new();
    private readonly Flow<TaskItem> _add;
    public Board(UiKit ui)
    {
        Text = "EasyDo · Tasks"; Size = new Size(900, 620); Padding = new Padding(24);
        BackColor = ui.Bg; ForeColor = ui.Text; Font = ui.Body;
        var input = ui.TextBox("Новая задача", 350);
        var grid = ui.Grid().Define<TaskItem>()
            .Column(task => task.Title, "Задача", 80)
            .Column(task => task.Done, "Готово", 20).Apply();
        grid.DataSource = Tasks;
        _add = Flow<TaskItem>.Start()
            .Check(Rules<TaskItem>.Create().Require("title", task => !string.IsNullOrWhiteSpace(task.Title), "Введите задачу."))
            .Then(task => Tasks.Add(task));
        var add = ui.Btn("Добавить").OnClick(() => ui.Safe(() => { AddTask(input.Text); input.Clear(); }));
        var done = ui.Btn("Завершить", ui.Success).OnClick(() =>
        { if (grid.Selected<TaskItem>() is { } task) { task.Done = true; Tasks.ResetBindings(); } });
        ui.Page(ui.TitledCard("Мои задачи", grid))
            .Above(ui.Toolbar([input], [done, add]))
            .Above(ui.PageHeader("Задачи", "Приложение из блоков EasyDo, без зависимости от StockFlow"))
            .Into(this);
    }
    public void AddTask(string title) => _add.Run(new TaskItem { Title = title.Trim() });
}
