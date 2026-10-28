using CompilerLab.Core;
using System.Diagnostics;
using System.Text;

namespace CompilerLab.UI;

/// <summary>
/// 界面统一使用的字体、颜色与尺寸。
/// 所有控件高度都按字体的实际行高动态计算（Font.Height 随显示缩放比例变化），
/// 因此在 100%、125%、150% 等不同缩放比例的显示器上文字都不会被裁切。
/// </summary>
internal static class Ui
{
    public static readonly Font UiFont = new("微软雅黑", 9f);
    public static readonly Font UiFontSmall = new("微软雅黑", 8.5f);
    public static readonly Font TitleFont = new("微软雅黑", 9.5f, FontStyle.Bold);
    public static readonly Font MonoFont = new("Consolas", 10.5f);
    public static readonly Font MonoFontSmall = new("Consolas", 9.5f);

    public static readonly Color HeaderBack = Color.FromArgb(238, 243, 250);
    public static readonly Color AccentColor = Color.FromArgb(30, 90, 160);
    public static readonly Color WarnColor = Color.FromArgb(192, 0, 0);
    public static readonly Color OkColor = Color.FromArgb(0, 120, 60);
    public static readonly Color SplitBack = Color.FromArgb(235, 238, 242);
    public static readonly Color PanelBack = Color.FromArgb(248, 250, 252);

    public static int ButtonHeight => UiFont.Height + 14;
    public static int InfoHeight => UiFontSmall.Height + 10;
    public static int SummaryHeight => TitleFont.Height + 10;
    public static int GridRowHeight(Font font) => font.Height + 8;

    public static Padding GroupPadding(Font titleFont)
        => new(8, titleFont.Height + 2, 8, 8);

    public static TextBox MakeCodeBox(bool readOnly = false)
    {
        return new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Font = MonoFont,
            ReadOnly = readOnly,
            BackColor = readOnly ? Color.FromArgb(250, 250, 250) : Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
    }

    public static Button MakeButton(string text)
    {
        return new Button
        {
            Text = text,
            Font = UiFont,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Height = ButtonHeight,
            MinimumSize = new Size(0, ButtonHeight),
            Padding = new Padding(10, 0, 10, 0),
            Margin = new Padding(0, 2, 6, 2),
            FlatStyle = FlatStyle.System,
            UseVisualStyleBackColor = true
        };
    }

    public static FlowLayoutPanel MakeToolbar()
    {
        return new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(6, 5, 6, 5),
            Margin = new Padding(0),
            BackColor = HeaderBack
        };
    }

    public static Label MakeLabel(string text, Font? font = null)
    {
        return new Label
        {
            Text = text,
            Font = font ?? UiFont,
            AutoSize = true,
            Height = ButtonHeight,
            Margin = new Padding(0, 2, 4, 2),
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    /// <summary>
    /// 设置分割容器的初始分割位置。SplitContainer 在构造时尺寸为 0，
    /// 此时赋值会抛异常，因此必须等控件真正拿到尺寸之后再设置，并且只设置一次。
    /// </summary>
    public static void SetupSplit(SplitContainer split, Orientation orientation,
        double ratio, int min1, int min2)
    {
        split.Orientation = orientation;
        bool done = false;

        void Apply()
        {
            if (done) return;
            int total = orientation == Orientation.Vertical ? split.Width : split.Height;
            if (total < min1 + min2 + split.SplitterWidth + 8) return;

            try
            {
                int max = total - min2 - split.SplitterWidth;
                split.SplitterDistance = Math.Clamp((int)(total * ratio), min1, Math.Max(min1, max));
                split.Panel1MinSize = min1;
                split.Panel2MinSize = min2;
                done = true;
            }
            catch (InvalidOperationException) { /* 窗口过小时忽略 */ }
        }

        split.HandleCreated += (_, _) => Apply();
        split.SizeChanged += (_, _) => Apply();
    }

    /// <summary>统一配置只读表格：表头高度自适应、行高按字体计算，避免文字被上下裁切。</summary>
    public static void StyleGrid(DataGridView grid, Font cellFont, Font headerFont)
    {
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.ReadOnly = true;
        grid.RowHeadersVisible = false;
        grid.EnableHeadersVisualStyles = false;
        grid.BorderStyle = BorderStyle.None;
        grid.BackgroundColor = Color.White;
        grid.GridColor = Color.FromArgb(228, 232, 236);
        grid.Font = cellFont;
        grid.ScrollBars = ScrollBars.Both;

        grid.ColumnHeadersDefaultCellStyle.BackColor = HeaderBack;
        grid.ColumnHeadersDefaultCellStyle.Font = headerFont;
        grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(3, 3, 3, 3);
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;

        grid.RowTemplate.Height = GridRowHeight(cellFont);
        grid.DefaultCellStyle.Padding = new Padding(3, 2, 3, 2);
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(210, 228, 250);
        grid.DefaultCellStyle.SelectionForeColor = Color.Black;
    }

    public static void WriteText(string path, string text)
    {
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, text, new UTF8Encoding(true));
    }
}

// =====================================================================
//                            通用表格显示控件
// =====================================================================

/// <summary>通用表格显示控件：列标题 + 行标题 + 单元格，供各阶段结果复用。</summary>
public sealed class GridPanel : UserControl
{
    private readonly Label _summary = new();
    private readonly DataGridView _grid = new();
    private const string EmptyText = "（尚未编译）";

    public List<string> Columns { get; } = new();
    public List<string[]> Rows { get; } = new();

    public GridPanel()
    {
        BackColor = Color.White;

        _summary.Dock = DockStyle.Top;
        _summary.Height = Ui.SummaryHeight;
        _summary.TextAlign = ContentAlignment.MiddleLeft;
        _summary.Font = Ui.UiFontSmall;
        _summary.ForeColor = Ui.AccentColor;
        _summary.BackColor = Ui.HeaderBack;
        _summary.Padding = new Padding(6, 0, 0, 0);
        _summary.Text = EmptyText;
        _summary.AutoEllipsis = true;

        _grid.Dock = DockStyle.Fill;
        Ui.StyleGrid(_grid, Ui.UiFont, Ui.UiFontSmall);
        _grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
        _grid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

        Controls.Add(_grid);
        Controls.Add(_summary);
    }

    public void ShowRows(IEnumerable<string> columns, IEnumerable<string[]> rows, string? summary = null)
    {
        Columns.Clear(); Columns.AddRange(columns);
        Rows.Clear(); Rows.AddRange(rows);

        _grid.Rows.Clear();
        _grid.Columns.Clear();
        foreach (string c in Columns)
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = c,
                MinimumWidth = 54,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
        foreach (var r in Rows) _grid.Rows.Add(r);
        if (_grid.Columns.Count > 0) _grid.Columns[0].Frozen = true;

        _summary.Text = summary ?? $"共 {Rows.Count} 行";
    }

    /// <summary>显示一张二维分析表（第一列是行标题）。</summary>
    public void ShowTable(ParseTable table, string rowHeader, string? summary = null)
    {
        var columns = new List<string> { rowHeader };
        columns.AddRange(table.Columns);
        var rows = new List<string[]>();
        for (int i = 0; i < table.Rows.Count; i++)
        {
            var line = new string[table.Columns.Count + 1];
            line[0] = table.Rows[i];
            Array.Copy(table.Cells[i], 0, line, 1, table.Columns.Count);
            rows.Add(line);
        }
        ShowRows(columns, rows, summary ?? table.Title);
    }

    public void Clear(string? text = null)
    {
        Columns.Clear(); Rows.Clear();
        _grid.Rows.Clear();
        _grid.Columns.Clear();
        _summary.Text = text ?? EmptyText;
    }

    /// <summary>导出成列对齐的文本（中文按 2 个字符宽计算）。</summary>
    public List<string> ToTextLines()
    {
        var lines = new List<string>();
        if (Columns.Count == 0) return lines;
        int n = Columns.Count;
        var w = new int[n];
        for (int c = 0; c < n; c++) w[c] = DisplayWidth(Columns[c]);
        foreach (var r in Rows)
            for (int c = 0; c < n && c < r.Length; c++) w[c] = Math.Max(w[c], DisplayWidth(r[c]));

        string Line(IReadOnlyList<string> cells)
        {
            var sb = new StringBuilder();
            for (int c = 0; c < n; c++)
            {
                string v = c < cells.Count ? cells[c] : "";
                sb.Append(v);
                if (c < n - 1) sb.Append(new string(' ', Math.Max(1, w[c] - DisplayWidth(v) + 2)));
            }
            return sb.ToString().TrimEnd();
        }

        lines.Add(Line(Columns));
        lines.Add(new string('-', w.Sum() + 2 * (n - 1)));
        foreach (var r in Rows) lines.Add(Line(r));
        return lines;
    }

    public static int DisplayWidth(string s)
    {
        int w = 0;
        foreach (char ch in s ?? "") w += ch >= 0x1100 && IsWide(ch) ? 2 : 1;
        return w;
    }

    private static bool IsWide(char c)
        => (c >= 0x1100 && c <= 0x115F) || (c >= 0x2E80 && c <= 0xA4CF) ||
           (c >= 0xAC00 && c <= 0xD7A3) || (c >= 0xF900 && c <= 0xFAFF) ||
           (c >= 0xFE30 && c <= 0xFE6F) || (c >= 0xFF00 && c <= 0xFF60) ||
           (c >= 0xFFE0 && c <= 0xFFE6);
}

/// <summary>标签页小工具。</summary>
public static class TextPanel
{
    public static TabPage MakeTab(string title, Control content, string? tip = null)
    {
        var page = new TabPage(title) { BackColor = Color.White, Padding = new Padding(3) };
        content.Dock = DockStyle.Fill;
        page.Controls.Add(content);
        if (tip is not null)
            page.Controls.Add(new Label
            {
                Dock = DockStyle.Top,
                Height = Ui.InfoHeight,
                Font = Ui.UiFontSmall,
                ForeColor = Color.DimGray,
                BackColor = Ui.PanelBack,
                Padding = new Padding(6, 0, 0, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = tip
            });
        return page;
    }
}

// =====================================================================
//                              程序入口
// =====================================================================

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.ThreadException += (_, e) =>
            MessageBox.Show(e.Exception.ToString(), "程序异常",
                MessageBoxButtons.OK, MessageBoxIcon.Error);

        Application.Run(new MainForm());
    }
}

// =====================================================================
//                               主窗口
// =====================================================================

/// <summary>
/// 主窗口。《编译原理》实验三“综合实验”——一个小型语言的编译系统：
///   ① 词法分析  ② 语法分析（SLR(1) 自动生成分析表）  ③ 语义分析（符号表、类型检查）
///   ④ 中间代码（四元式）  ⑤ 解释执行
/// </summary>
public sealed class MainForm : Form
{
    private readonly TextBox _source = Ui.MakeCodeBox();
    private readonly ComboBox _sampleBox = new();
    private readonly TextBox _inputData = new();
    private readonly ToolStripStatusLabel _status = new();

    private readonly GridPanel _lexGrid = new();
    private readonly TextBox _lexSeq = Ui.MakeCodeBox(readOnly: true);
    private readonly TextBox _lexErr = Ui.MakeCodeBox(readOnly: true);

    private readonly GridPanel _slrTable = new();
    private readonly GridPanel _slrProcess = new();
    private readonly GridPanel _slrStates = new();
    private readonly TextBox _slrResult = Ui.MakeCodeBox(readOnly: true);

    private readonly GridPanel _symGrid = new();
    private readonly TextBox _errText = Ui.MakeCodeBox(readOnly: true);

    private readonly GridPanel _quadGrid = new();
    private readonly GridPanel _optGrid = new();
    private readonly TextBox _optLog = Ui.MakeCodeBox(readOnly: true);
    private readonly TextBox _runOutput = Ui.MakeCodeBox(readOnly: true);
    private readonly GridPanel _varGrid = new();

    private CompileResult? _result;

    /// <summary>内置示例源程序。</summary>
    public static readonly (string Name, string Code, string Input)[] Samples =
    {
        ("① 综合示例：循环求和 + 分支判断",
            "// 求 1 到 n 的和，并判断和是否大于 50\n" +
            "{\n" +
            "    int n;\n" +
            "    int i;\n" +
            "    int sum;\n\n" +
            "    n = 10;\n" +
            "    i = 1;\n" +
            "    sum = 0;\n" +
            "    while (i <= n) {\n" +
            "        sum = sum + i;\n" +
            "        i = i + 1;\n" +
            "    }\n" +
            "    write(sum);\n\n" +
            "    if (sum > 50) {\n" +
            "        write(1);\n" +
            "    } else {\n" +
            "        write(0);\n" +
            "    }\n" +
            "}\n", ""),

        ("② 实型运算与类型提升",
            "{\n" +
            "    real x;\n" +
            "    real y;\n" +
            "    int n;\n\n" +
            "    x = 1.5;\n" +
            "    y = 2.0;\n" +
            "    n = 3;\n" +
            "    write(x * y + n);   // 实型：6.0\n" +
            "    write(n / 2);       // 整除：1\n" +
            "    write(n / 2.0);     // 实型：1.5\n" +
            "}\n", ""),

        ("③ read 输入并计算",
            "// read 依次从“输入数据”中取值\n" +
            "{\n" +
            "    int a;\n" +
            "    int b;\n" +
            "    int c;\n\n" +
            "    read(a);\n" +
            "    read(b);\n" +
            "    read(c);\n" +
            "    write(a + b + c);\n" +
            "    write(a * (b + c));\n" +
            "}\n", "7 8 9"),

        ("④ 嵌套块与作用域",
            "{\n" +
            "    int a;\n\n" +
            "    a = 1;\n" +
            "    {\n" +
            "        int a;      // 内层重新声明，与外层互不影响\n" +
            "        a = 100;\n" +
            "        write(a);\n" +
            "    }\n" +
            "    write(a);\n" +
            "}\n", ""),

        ("⑤ 语义错误示例",
            "{\n" +
            "    int a;\n" +
            "    int a;          // 重复声明\n" +
            "    real r;\n\n" +
            "    a = 1;\n" +
            "    r = 2.5;\n" +
            "    a = r;          // 实型赋给整型\n" +
            "    b = a + 1;      // 未声明\n" +
            "    write(z);       // 未声明\n" +
            "}\n", ""),

        ("⑥ 语法错误示例",
            "{\n" +
            "    int a;\n" +
            "    a = 1       // 缺少分号\n" +
            "    write(a);\n" +
            "}\n", ""),

        ("⑦ 词法错误示例",
            "{\n" +
            "    int a;\n" +
            "    a = 12abc;  // 非法数值\n" +
            "    a = 1 @ 2;  // 非法字符\n" +
            "}\n", "")
    };

    public MainForm()
    {
        Text = "CompilerLab 小型语言编译系统 —— 《编译原理》实验三";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(960, 660);
        Font = Ui.UiFont;
        BackColor = Color.White;

        var work = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 800);
        Size = new Size(
            Math.Min(1400, Math.Max(960, (int)(work.Width * 0.94))),
            Math.Min(920, Math.Max(660, (int)(work.Height * 0.94))));

        BuildUi();
        LoadSample(0);
    }

    // ==================================================================
    //                              界面搭建
    // ==================================================================

    private void BuildUi()
    {
        var menu = BuildMenu();
        MainMenuStrip = menu;
        Controls.Add(BuildSplit());
        Controls.Add(BuildStatusStrip());
        Controls.Add(menu);
        KeyPreview = true;
        KeyDown += (_, e) => { if (e.KeyCode == Keys.F5) Compile(andRun: true); };
    }

    private MenuStrip BuildMenu()
    {
        var menu = new MenuStrip { Font = Ui.UiFont, BackColor = Ui.PanelBack };

        var file = new ToolStripMenuItem("文件(&F)");
        file.DropDownItems.Add(new ToolStripMenuItem("打开源程序…", null, (_, _) => OpenSource()));
        file.DropDownItems.Add(new ToolStripMenuItem("导出全部结果…", null, (_, _) => ExportAll()));
        file.DropDownItems.Add(new ToolStripMenuItem("导出到程序目录的 output 文件夹", null,
            (_, _) => ExportToOutputFolder()));
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add(new ToolStripMenuItem("退出", null, (_, _) => Close()));

        var build = new ToolStripMenuItem("编译(&B)");
        build.DropDownItems.Add(new ToolStripMenuItem("编译并执行（F5）", null, (_, _) => Compile(andRun: true)));
        build.DropDownItems.Add(new ToolStripMenuItem("只编译不执行", null, (_, _) => Compile(andRun: false)));
        build.DropDownItems.Add(new ToolStripSeparator());
        build.DropDownItems.Add(new ToolStripMenuItem("清空结果", null, (_, _) => ClearResults()));

        var help = new ToolStripMenuItem("帮助(&H)");
        help.DropDownItems.Add(new ToolStripMenuItem("本语言说明", null, (_, _) => ShowLanguageHelp()));
        help.DropDownItems.Add(new ToolStripMenuItem("关于本程序", null, (_, _) => ShowAbout()));

        menu.Items.Add(file);
        menu.Items.Add(build);
        menu.Items.Add(help);
        return menu;
    }

    private StatusStrip BuildStatusStrip()
    {
        var strip = new StatusStrip { Font = Ui.UiFontSmall, BackColor = Ui.PanelBack };
        _status.Text = "请选择或编辑源程序，然后按 F5 编译并执行。";
        _status.Spring = true;
        _status.TextAlign = ContentAlignment.MiddleLeft;
        strip.Items.Add(_status);
        return strip;
    }

    private Control BuildSplit()
    {
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterWidth = 6,
            BackColor = Ui.SplitBack
        };
        split.Panel1.Controls.Add(BuildSourceSide());
        split.Panel2.Controls.Add(BuildResultSide());
        Ui.SetupSplit(split, Orientation.Vertical, 0.32,
            Ui.ButtonHeight * 8, Ui.ButtonHeight * 9);
        return split;
    }

    // ------------------------- 左侧：源程序 -------------------------

    private Control BuildSourceSide()
    {
        var toolbar = Ui.MakeToolbar();

        toolbar.Controls.Add(Ui.MakeLabel("内置示例："));
        _sampleBox.Font = Ui.UiFont;
        _sampleBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _sampleBox.Width = Ui.UiFont.Height * 24;
        _sampleBox.Margin = new Padding(0, 2, 8, 2);
        foreach (var s in Samples) _sampleBox.Items.Add(s.Name);
        _sampleBox.SelectedIndex = 0;
        _sampleBox.SelectedIndexChanged += (_, _) =>
        {
            if (_sampleBox.SelectedIndex >= 0) LoadSample(_sampleBox.SelectedIndex);
        };

        var btnOpen = Ui.MakeButton("打开源程序…");
        btnOpen.Click += (_, _) => OpenSource();

        var btnClear = Ui.MakeButton("清空");
        btnClear.Click += (_, _) =>
        {
            _source.Clear();
            _inputData.Text = "";
            ClearResults();
            _status.Text = "已清空。";
        };

        toolbar.Controls.AddRange(new Control[] { _sampleBox, btnOpen, btnClear });

        _source.Dock = DockStyle.Fill;
        var group = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = "源程序（小语言：声明、赋值、if/else、while、read/write）",
            Font = Ui.TitleFont,
            Padding = Ui.GroupPadding(Ui.TitleFont),
            BackColor = Color.White
        };
        group.Controls.Add(_source);

        var host = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        host.Controls.Add(group);
        host.Controls.Add(toolbar);
        return host;
    }

    // ------------------------- 右侧：五级结果 -------------------------

    private Control BuildResultSide()
    {
        var toolbar = Ui.MakeToolbar();
        toolbar.Controls.Add(Ui.MakeLabel("read 输入数据：", Ui.TitleFont));

        _inputData.Font = Ui.MonoFont;
        _inputData.Width = Ui.UiFont.Height * 12;
        _inputData.Margin = new Padding(0, 2, 8, 2);
        _inputData.Text = "7 8 9";

        var btnCompile = Ui.MakeButton("编译并执行（F5）");
        btnCompile.Font = new Font(Ui.UiFont, FontStyle.Bold);
        btnCompile.Click += (_, _) => Compile(andRun: true);

        var btnOnlyCompile = Ui.MakeButton("只编译");
        btnOnlyCompile.Click += (_, _) => Compile(andRun: false);

        var btnExport = Ui.MakeButton("导出全部结果…");
        btnExport.Click += (_, _) => ExportAll();

        toolbar.Controls.AddRange(new Control[]
        {
            _inputData, btnCompile, btnOnlyCompile, btnExport
        });

        var tabs = new TabControl { Dock = DockStyle.Fill, Font = Ui.UiFont, Padding = new Point(16, 5) };
        tabs.Controls.Add(BuildLexTab());
        tabs.Controls.Add(BuildSyntaxTab());
        tabs.Controls.Add(BuildSemanticTab());
        tabs.Controls.Add(BuildQuadTab());
        tabs.Controls.Add(BuildRunTab());

        var host = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        host.Controls.Add(tabs);
        host.Controls.Add(toolbar);
        return host;
    }

    private TabPage BuildLexTab()
    {
        var inner = new TabControl { Dock = DockStyle.Fill, Font = Ui.UiFont, Padding = new Point(14, 5) };
        inner.Controls.Add(TextPanel.MakeTab("① 单词符号明细", _lexGrid));
        inner.Controls.Add(TextPanel.MakeTab("② 二元组序列 (syn, token)", _lexSeq));
        inner.Controls.Add(TextPanel.MakeTab("③ 词法错误", _lexErr));

        var page = new TabPage("① 词法分析") { BackColor = Color.White, Padding = new Padding(3) };
        page.Controls.Add(inner);
        return page;
    }

    private TabPage BuildSyntaxTab()
    {
        var inner = new TabControl { Dock = DockStyle.Fill, Font = Ui.UiFont, Padding = new Point(14, 5) };
        inner.Controls.Add(TextPanel.MakeTab("① SLR(1) 分析表", _slrTable));
        inner.Controls.Add(TextPanel.MakeTab("② 分析过程表", _slrProcess));
        inner.Controls.Add(TextPanel.MakeTab("③ 项目集规范族", _slrStates));
        inner.Controls.Add(TextPanel.MakeTab("④ 分析结果", _slrResult));

        var page = new TabPage("② 语法分析（SLR(1)）") { BackColor = Color.White, Padding = new Padding(3) };
        page.Controls.Add(inner);
        return page;
    }

    private TabPage BuildSemanticTab()
    {
        var inner = new TabControl { Dock = DockStyle.Fill, Font = Ui.UiFont, Padding = new Point(14, 5) };
        inner.Controls.Add(TextPanel.MakeTab("① 符号表", _symGrid));
        inner.Controls.Add(TextPanel.MakeTab("② 全部错误清单", _errText));

        var page = new TabPage("③ 语义分析") { BackColor = Color.White, Padding = new Padding(3) };
        page.Controls.Add(inner);
        return page;
    }

    private TabPage BuildQuadTab()
    {
        var page = new TabPage("④ 中间代码（四元式）") { BackColor = Color.White, Padding = new Padding(3) };
        var inner = new TabControl { Dock = DockStyle.Fill, Font = Ui.UiFont };
        inner.Controls.Add(TextPanel.MakeTab("① 优化前（原始四元式）", _quadGrid));
        inner.Controls.Add(TextPanel.MakeTab("② 优化后（用于执行）", _optGrid));
        inner.Controls.Add(TextPanel.MakeTab("③ 优化说明", _optLog));
        page.Controls.Add(inner);
        return page;
    }

    private TabPage BuildRunTab()
    {
        var runHeader = Ui.MakeToolbar();
        var btnRun = Ui.MakeButton("重新执行");
        btnRun.Click += (_, _) => Run();
        runHeader.Controls.Add(btnRun);
        runHeader.Controls.Add(Ui.MakeLabel("（read 的数据取自上方输入框，用空格分隔）", Ui.UiFontSmall));

        var outputGroup = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = "程序输出",
            Font = Ui.TitleFont,
            Padding = Ui.GroupPadding(Ui.TitleFont),
            BackColor = Color.White
        };
        outputGroup.Controls.Add(_runOutput);

        var varGroup = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = "运行结束时各变量的取值",
            Font = Ui.TitleFont,
            Padding = Ui.GroupPadding(Ui.TitleFont),
            BackColor = Color.White
        };
        varGroup.Controls.Add(_varGrid);

        var inner = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterWidth = 6,
            Orientation = Orientation.Horizontal,
            BackColor = Ui.SplitBack
        };
        inner.Panel1.Controls.Add(outputGroup);
        inner.Panel2.Controls.Add(varGroup);
        Ui.SetupSplit(inner, Orientation.Horizontal, 0.5,
            Ui.ButtonHeight * 3, Ui.ButtonHeight * 3);

        var page = new TabPage("⑤ 解释执行结果") { BackColor = Color.White, Padding = new Padding(3) };
        page.Controls.Add(inner);
        page.Controls.Add(runHeader);
        return page;
    }

    // ==================================================================
    //                              功能实现
    // ==================================================================

    private void LoadSample(int index)
    {
        int i = Math.Clamp(index, 0, Samples.Length - 1);
        var s = Samples[i];
        _source.Text = s.Code.Replace("\r\n", "\n").Replace("\n", "\r\n");
        _source.SelectionStart = 0;
        _inputData.Text = s.Input;
        ClearResults();
        _status.Text = "已载入示例：" + s.Name;
    }

    private void OpenSource()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "打开源程序文件",
            Filter = "源程序文件 (*.txt;*.src;*.c;*.lang)|*.txt;*.src;*.c;*.lang|所有文件 (*.*)|*.*"
        };
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;

        try
        {
            string text;
            try { text = File.ReadAllText(dlg.FileName, new UTF8Encoding(false, true)); }
            catch (DecoderFallbackException) { text = File.ReadAllText(dlg.FileName, Encoding.Default); }

            _source.Text = text.Replace("\r\n", "\n").Replace("\n", "\r\n");
            _status.Text = "已打开：" + dlg.FileName;
        }
        catch (Exception ex)
        {
            MessageBox.Show("读取源文件失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ClearResults()
    {
        _result = null;
        _lexGrid.Clear(); _lexSeq.Clear(); _lexErr.Clear();
        _slrTable.Clear(); _slrProcess.Clear(); _slrStates.Clear(); _slrResult.Clear();
        _symGrid.Clear(); _errText.Clear();
        _quadGrid.Clear(); _optGrid.Clear(); _optLog.Clear();
        _runOutput.Clear(); _varGrid.Clear();
    }

    /// <summary>编译整份源程序并刷新全部标签页。</summary>
    private void Compile(bool andRun)
    {
        string code = _source.Text;
        if (code.Trim().Length == 0)
        {
            MessageBox.Show("请先输入或载入源程序。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var sw = Stopwatch.StartNew();
        CompileResult r;
        try
        {
            r = Compiler.Compile(code);
        }
        catch (Exception ex)
        {
            MessageBox.Show("编译过程中出现异常：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        sw.Stop();

        _result = r;
        ShowLex(r);
        ShowSyntax(r);
        ShowSemantic(r);
        ShowQuads(r);

        if (andRun && r.SyntaxAccepted && r.Errors.Count == 0) Run();
        else if (!andRun) { _runOutput.Clear(); _varGrid.Clear(); }

        _status.Text =
            $"编译完成：单词 {r.Lex.Tokens.Count} 个；SLR(1) 分析 {r.Process.Count} 步（{(r.SyntaxAccepted ? "接受" : "拒绝")}）；" +
            $"符号 {r.Symbols.Count(s => !s.IsTemp)} 个；四元式 {r.Quads.Count} 条；错误 {r.AllErrors.Count()} 个；" +
            $"耗时 {sw.Elapsed.TotalMilliseconds:0.0} ms。";
    }

    private void ShowLex(CompileResult r)
    {
        var rows = new List<string[]>();
        foreach (var t in r.Lex.Tokens)
            rows.Add(new[]
            {
                t.Index.ToString(), t.Syn.ToString(), t.Lexeme, t.KindText,
                t.Line.ToString(), t.Column.ToString()
            });
        _lexGrid.ShowRows(
            new[] { "序号", "种别码 syn", "单词符号", "种类", "行", "列" },
            rows, $"共 {r.Lex.Tokens.Count} 个单词");

        _lexSeq.Text = r.Lex.ToSequenceString().Length == 0
            ? "（没有识别出任何单词）"
            : r.Lex.ToSequenceString();

        if (r.Lex.Errors.Count == 0)
        {
            _lexErr.ForeColor = Ui.OkColor;
            _lexErr.Text = "未发现词法错误。";
        }
        else
        {
            _lexErr.ForeColor = Ui.WarnColor;
            _lexErr.Text = string.Join(Environment.NewLine,
                r.Lex.Errors.Select((e, i) => $"[{i + 1}] {e.Text}"));
        }
    }

    private void ShowSyntax(CompileResult r)
    {
        _slrTable.ShowTable(r.Slr.Table, "状态",
            $"SLR(1) 分析表：{r.Slr.States.Count} 个状态，{r.Slr.Table.Columns.Count} 列" +
            (r.Slr.IsSlr ? "，无冲突" : $"，{r.Slr.Conflicts.Count} 处冲突"));

        var rows = new List<string[]>();
        foreach (var s in r.Process)
            rows.Add(new[] { s.Index.ToString(), s.StateStack, s.SymbolStack, s.CurrentInput, s.Action });
        _slrProcess.ShowRows(
            new[] { "步骤", "状态栈", "符号栈", "当前输入", "动作" },
            rows, $"语法分析过程：共 {r.Process.Count} 步");

        var stateRows = new List<string[]>();
        foreach (var st in r.Slr.States)
        {
            var trans = new List<string>();
            foreach (string sym in r.Slr.Augmented.Terminals.Concat(r.Slr.Augmented.NonTerminals))
                if (r.Slr.Transitions.TryGetValue((st.Id, sym), out int to))
                    trans.Add($"{sym} → {to}");
            stateRows.Add(new[] { st.Id.ToString(), st.Render(r.Slr.Augmented), string.Join("　", trans) });
        }
        _slrStates.ShowRows(
            new[] { "状态", "LR(0) 项目集", "转移（读入符号 → 后继状态）" },
            stateRows, $"LR(0) 项目集规范族：共 {r.Slr.States.Count} 个状态");

        var sb = new StringBuilder();
        sb.AppendLine("========== 一、文法（四元组） ==========");
        sb.AppendLine(r.Grammar.Quadruple());
        sb.AppendLine();
        sb.AppendLine("========== 二、SLR(1) 分析表 ==========");
        if (r.Slr.IsSlr)
            sb.AppendLine("分析表中没有任何多重入口，该文法是无冲突的 SLR(1) 文法。");
        else
        {
            sb.AppendLine($"分析表中有 {r.Slr.Conflicts.Count} 处冲突：");
            foreach (string c in r.Slr.Conflicts) sb.AppendLine("  · " + c);
        }
        sb.AppendLine($"项目集个数：{r.Slr.States.Count}");
        sb.AppendLine();
        sb.AppendLine("========== 三、FIRST / FOLLOW 集 ==========");
        foreach (string nt in r.Grammar.NonTerminals)
            sb.AppendLine($"FIRST({nt}) = {{ {string.Join(", ", r.Slr.Sets.First[nt])} }}");
        sb.AppendLine();
        foreach (string nt in r.Grammar.NonTerminals)
            sb.AppendLine($"FOLLOW({nt}) = {{ {string.Join(", ", r.Slr.Sets.Follow[nt])} }}");
        sb.AppendLine();
        sb.AppendLine("========== 四、分析结果 ==========");
        sb.AppendLine($"分析步数：{r.Process.Count} 步");
        sb.AppendLine("分析结论：" + (r.SyntaxResult.Length == 0 ? "（尚未分析）" : r.SyntaxResult));
        _slrResult.Text = sb.ToString();
    }

    private void ShowSemantic(CompileResult r)
    {
        var rows = new List<string[]>();
        foreach (var s in r.Symbols.Where(x => !x.IsTemp))
            rows.Add(new[]
            {
                s.Name, s.Type.Text(), s.Store, s.ScopeLevel.ToString(),
                s.Address.ToString(), s.Line.ToString()
            });
        _symGrid.ShowRows(
            new[] { "名字", "类型", "存储名", "作用域层次", "起始地址", "声明行" },
            rows,
            $"符号表：共 {rows.Count} 个变量" +
            (r.Scopes.Count > 0 ? $"；作用域 {r.Scopes.Count} 个（层次 0 为最外层）" : ""));

        var all = r.AllErrors.ToList();
        if (all.Count == 0)
        {
            _errText.ForeColor = Ui.OkColor;
            _errText.Text = "未发现词法、语法或语义错误。";
        }
        else
        {
            _errText.ForeColor = Ui.WarnColor;
            var sb = new StringBuilder();
            int n = 0;
            foreach (var e in all) sb.AppendLine($"[{++n}] {e.Text}");
            _errText.Text = sb.ToString();
        }
    }

    private void ShowQuads(CompileResult r)
    {
        var rows = new List<string[]>();
        foreach (var q in r.Quads)
            rows.Add(new[] { q.Index.ToString(), q.Text, q.Meaning });
        _quadGrid.ShowRows(
            new[] { "序号", "四元式 (op, arg1, arg2, result)", "含义" },
            rows,
            $"中间代码：共 {r.Quads.Count} 条四元式" +
            (r.Quads.Count > 0 ? $"；临时变量 {r.Quads.Count(q => q.Result.StartsWith('t'))} 个" : ""));

        var optRows = new List<string[]>();
        foreach (var q in r.OptimizedQuads)
            optRows.Add(new[] { q.Index.ToString(), q.Text, q.Meaning });
        _optGrid.ShowRows(
            new[] { "序号", "四元式 (op, arg1, arg2, result)", "含义" },
            optRows,
            $"优化后：共 {r.OptimizedQuads.Count} 条（减少 {r.Quads.Count - r.OptimizedQuads.Count} 条），" +
            $"优化 {r.OptimizeLog.Count} 项");

        _optLog.Text = r.OptimizeLog.Count == 0
            ? "本程序没有可优化的四元式。"
            : string.Join(Environment.NewLine, r.OptimizeLog.Select((s, i) => $"{i + 1}. {s}"));
    }

    /// <summary>解释执行四元式。</summary>
    private void Run()
    {
        if (_result is null) { MessageBox.Show("请先编译。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        if (!_result.SyntaxAccepted)
        {
            _runOutput.ForeColor = Ui.WarnColor;
            _runOutput.Text = "源程序未通过语法分析，无法执行。\r\n\r\n" +
                              string.Join("\r\n", _result.Errors.Select(e => e.Text));
            _varGrid.Clear("（未执行）");
            return;
        }
        if (_result.Errors.Count > 0)
        {
            _runOutput.ForeColor = Ui.WarnColor;
            _runOutput.Text = "源程序存在语义错误，已跳过执行。\r\n\r\n" +
                              string.Join("\r\n", _result.Errors.Select(e => e.Text));
            _varGrid.Clear("（未执行）");
            return;
        }

        var inputs = _inputData.Text
            .Split(new[] { ' ', ',', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        var run = Interpreter.Run(_result, inputs);
        _result.Run = run;

        _runOutput.ForeColor = run.Ok ? Ui.OkColor : Ui.WarnColor;
        var sb = new StringBuilder();
        sb.AppendLine(run.Ok ? "执行成功。" : "执行失败。");
        sb.AppendLine("说明：" + run.Message);
        sb.AppendLine($"执行步数：{run.Steps}");
        sb.AppendLine();
        sb.AppendLine("程序输出：");
        if (run.Output.Count == 0) sb.AppendLine("（程序没有任何输出）");
        else foreach (string line in run.Output) sb.AppendLine("  " + line);
        _runOutput.Text = sb.ToString();

        _varGrid.ShowRows(
            new[] { "变量", "类型", "最终值" },
            run.Variables.Select(v => new[] { v.Name, v.Type, v.Value }),
            $"运行结束时共 {run.Variables.Count} 个变量");

        _status.Text = $"执行{(run.Ok ? "成功" : "失败")}：{run.Steps} 步，输出 {run.Output.Count} 行。{run.Message}";
    }

    // ------------------------------ 导出 ------------------------------

    private string BuildExportText()
    {
        if (_result is null) return "";
        var r = _result;
        var sb = new StringBuilder();

        sb.AppendLine("《编译原理》实验三  综合实验（小型语言编译系统）运行结果");
        sb.AppendLine("生成时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine();
        sb.AppendLine("################ 源程序 ################");
        sb.AppendLine(r.Source.TrimEnd());
        sb.AppendLine();
        sb.AppendLine("read 输入数据：" + _inputData.Text);

        sb.AppendLine();
        sb.AppendLine("################ 一、词法分析 ################");
        foreach (string line in _lexGrid.ToTextLines()) sb.AppendLine(line);
        sb.AppendLine();
        sb.AppendLine("二元组序列：");
        sb.AppendLine(_lexSeq.Text);
        sb.AppendLine();
        sb.AppendLine("词法错误：");
        sb.AppendLine(_lexErr.Text);

        sb.AppendLine();
        sb.AppendLine("################ 二、语法分析 ################");
        sb.AppendLine("-------------------- SLR(1) 分析表 --------------------");
        foreach (string line in _slrTable.ToTextLines()) sb.AppendLine(line);
        sb.AppendLine();
        sb.AppendLine("-------------------- 分析过程表 --------------------");
        foreach (string line in _slrProcess.ToTextLines()) sb.AppendLine(line);
        sb.AppendLine();
        sb.AppendLine("-------------------- 项目集规范族 --------------------");
        foreach (string line in _slrStates.ToTextLines()) sb.AppendLine(line);
        sb.AppendLine();
        sb.AppendLine(_slrResult.Text);

        sb.AppendLine();
        sb.AppendLine("################ 三、语义分析 ################");
        foreach (string line in _symGrid.ToTextLines()) sb.AppendLine(line);
        sb.AppendLine();
        sb.AppendLine(_errText.Text);

        sb.AppendLine();
        sb.AppendLine("################ 四、中间代码（四元式） ################");
        sb.AppendLine("-------------------- 优化前 --------------------");
        foreach (string line in _quadGrid.ToTextLines()) sb.AppendLine(line);
        sb.AppendLine();
        sb.AppendLine("-------------------- 优化后 --------------------");
        foreach (string line in _optGrid.ToTextLines()) sb.AppendLine(line);
        sb.AppendLine();
        sb.AppendLine("-------------------- 优化说明 --------------------");
        sb.AppendLine(_optLog.Text);

        sb.AppendLine();
        sb.AppendLine("################ 五、解释执行结果 ################");
        sb.AppendLine(_runOutput.Text);
        sb.AppendLine();
        foreach (string line in _varGrid.ToTextLines()) sb.AppendLine(line);
        return sb.ToString();
    }

    private void ExportAll()
    {
        if (_result is null) { MessageBox.Show("请先编译。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }

        using var dlg = new SaveFileDialog
        {
            Title = "导出编译运行结果",
            Filter = "文本文件 (*.txt)|*.txt",
            FileName = "编译运行结果.txt"
        };
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;

        try
        {
            Ui.WriteText(dlg.FileName, BuildExportText());
            _status.Text = "结果已导出到：" + dlg.FileName;
        }
        catch (Exception ex)
        {
            MessageBox.Show("导出失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ExportToOutputFolder()
    {
        if (_result is null) { MessageBox.Show("请先编译。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }

        try
        {
            string dir = Path.Combine(AppContext.BaseDirectory, "output");
            Ui.WriteText(Path.Combine(dir, "编译运行结果.txt"), BuildExportText());
            Ui.WriteText(Path.Combine(dir, "词法分析.txt"),
                string.Join(Environment.NewLine, _lexGrid.ToTextLines()) +
                Environment.NewLine + Environment.NewLine + _lexSeq.Text);
            Ui.WriteText(Path.Combine(dir, "SLR1_分析表.txt"), string.Join(Environment.NewLine, _slrTable.ToTextLines()));
            Ui.WriteText(Path.Combine(dir, "语法分析过程表.txt"), string.Join(Environment.NewLine, _slrProcess.ToTextLines()));
            Ui.WriteText(Path.Combine(dir, "符号表.txt"), string.Join(Environment.NewLine, _symGrid.ToTextLines()));
            Ui.WriteText(Path.Combine(dir, "四元式.txt"), string.Join(Environment.NewLine, _quadGrid.ToTextLines()));
            Ui.WriteText(Path.Combine(dir, "四元式_优化后.txt"), string.Join(Environment.NewLine, _optGrid.ToTextLines()));
            Ui.WriteText(Path.Combine(dir, "优化说明.txt"), _optLog.Text);
            Ui.WriteText(Path.Combine(dir, "执行结果.txt"), _runOutput.Text);

            _status.Text = "结果已写入：" + dir;
            MessageBox.Show("全部结果已写入程序目录下的 output 文件夹：\r\n" + dir,
                "导出完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("导出失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ------------------------------ 帮助 ------------------------------

    private static void ShowLanguageHelp()
    {
        MessageBox.Show(
            "本语言（一个小型的过程式语言）说明：\r\n\r\n" +
            "1) 程序整体是一个 { } 块，块内先写声明、后写语句。\r\n" +
            "2) 类型只有 int（整型）与 real（实型）两种；\r\n" +
            "   整型常量如 12，实型常量如 1.5、1.2e3。\r\n" +
            "3) 语句支持：\r\n" +
            "     变量 = 表达式 ;\r\n" +
            "     if ( 条件 ) { … }\r\n" +
            "     if ( 条件 ) { … } else { … }\r\n" +
            "     while ( 条件 ) { … }\r\n" +
            "     read ( 变量 ) ;     write ( 表达式 ) ;\r\n" +
            "     { … }               —— 嵌套块，可重新声明同名变量\r\n" +
            "4) 表达式支持 + - * /、一元负号与括号，乘除优先级高于加减；\r\n" +
            "   条件支持 <  <=  >  >=  ==  != 六种关系运算。\r\n" +
            "5) 类型规则：整型与实型混合运算按实型进行；\r\n" +
            "   把实型表达式赋给整型变量会给出错误提示；\r\n" +
            "   两个整数相除是整除（如 3/2 得 1）。\r\n" +
            "6) 注释支持 // 行注释与 /* */ 块注释。\r\n\r\n" +
            "说明：if / while 的语句体必须用 { } 括起来，这样文法就不存在\r\n" +
            "悬挂 else 的二义性，可以做成无冲突的 SLR(1) 文法。",
            "本语言说明", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static void ShowAbout()
    {
        MessageBox.Show(
            "CompilerLab 小型语言编译系统\r\n《编译原理》实验三  综合实验\r\n\r\n" +
            "把一个源程序完整地走完编译的全过程：\r\n" +
            "  ① 词法分析：扫描出 (种别码, 单词符号) 二元组序列，报告词法错误；\r\n" +
            "  ② 语法分析：自动构造 SLR(1) 分析表（项目集规范族 + FOLLOW 集），\r\n" +
            "     输出分析过程表与分析结果；\r\n" +
            "  ③ 语义分析：建立符号表（含作用域层次与存储分配），\r\n" +
            "     完成类型检查与未声明/重复声明检查；\r\n" +
            "  ④ 中间代码：以语法制导翻译的方式生成四元式；\r\n" +
            "  ⑤ 解释执行：直接执行四元式，输出运行结果与变量终值。\r\n\r\n" +
            "运行环境：Windows 10/11 x64，单文件发布，无需安装任何运行环境。",
            "关于 CompilerLab", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
