namespace EasyDo.WinForms;

public sealed record UiSkin
{
    public Color Bg { get; init; } = Color.FromArgb(13, 15, 24);
    public Color Sidebar { get; init; } = Color.FromArgb(18, 20, 32);
    public Color Card { get; init; } = Color.FromArgb(24, 27, 42);
    public Color CardBorder { get; init; } = Color.FromArgb(42, 46, 68);
    public Color Input { get; init; } = Color.FromArgb(32, 36, 55);
    public Color Selection { get; init; } = Color.FromArgb(52, 54, 92);
    public Color Accent { get; init; } = Color.FromArgb(108, 99, 255);
    public Color Text { get; init; } = Color.FromArgb(232, 234, 242);
    public Color Muted { get; init; } = Color.FromArgb(138, 144, 168);
    public Color Success { get; init; } = Color.FromArgb(34, 197, 94);
    public Color Danger { get; init; } = Color.FromArgb(239, 68, 68);
    public Color Warning { get; init; } = Color.FromArgb(245, 158, 11);
    public Color Info { get; init; } = Color.FromArgb(56, 189, 248);
    public Color Pink { get; init; } = Color.FromArgb(236, 72, 153);
    public string FontFamily { get; init; } = "Segoe UI";
    public string StrongFontFamily { get; init; } = "Segoe UI Semibold";
}
