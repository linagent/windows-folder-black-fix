using System.Diagnostics;
using System.Drawing.Drawing2D;

namespace FolderBlackFix;

internal sealed class MainForm : Form
{
    private readonly BackupService _backupService = new();
    private readonly List<string> _folders = [];
    private readonly List<string> _logs = [];
    private readonly DropZonePanel _dropZone = new();
    private readonly FolderGlyph _folderGlyph = new();
    private readonly Label _dropTitle = new();
    private readonly Label _dropHint = new();
    private readonly ModernButton _selectButton = new();
    private readonly ModernButton _repairButton = new();
    private readonly LinkLabel _resetLink = new();
    private readonly LinkLabel _restoreLink = new();
    private readonly LinkLabel _backupLink = new();
    private readonly LinkLabel _detailsLink = new();
    private readonly Label _statusLabel = new();
    private readonly ProgressBar _progress = new();
    private readonly Label _headerTitle = new();
    private readonly Label _headerSubtitle = new();
    private bool _busy;
    private bool _finished;

    public MainForm()
    {
        Text = "文件夹黑块修复";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(780, 620);
        MinimumSize = new Size(720, 600);
        BackColor = Color.FromArgb(247, 249, 252);
        Font = new Font("Microsoft YaHei UI", 9F);
        AutoScaleMode = AutoScaleMode.Dpi;
        AllowDrop = true;
        BuildUi();
        WireEvents();
        AddLog("工具已就绪。 ");
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(38, 26, 38, 18), ColumnCount = 1, RowCount = 3 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        Controls.Add(root);

        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, Padding = Padding.Empty };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var badge = new LogoBadge { Size = new Size(56, 56), Anchor = AnchorStyles.Left, Margin = Padding.Empty };
        var titleStack = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, RowCount = 2, Anchor = AnchorStyles.Left, Margin = Padding.Empty, Padding = Padding.Empty };
        titleStack.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        titleStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        titleStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _headerTitle.Text = "文件夹黑块修复";
        _headerTitle.Font = new Font("Microsoft YaHei UI", 18F, FontStyle.Bold);
        _headerTitle.ForeColor = Color.FromArgb(24, 36, 59);
        _headerTitle.AutoSize = true;
        _headerTitle.Margin = Padding.Empty;
        _headerSubtitle.Text = "拖进来，点一下，恢复正常";
        _headerSubtitle.Font = new Font("Microsoft YaHei UI", 9.5F);
        _headerSubtitle.ForeColor = Color.FromArgb(104, 117, 139);
        _headerSubtitle.AutoSize = true;
        _headerSubtitle.Margin = new Padding(2, 4, 0, 0);
        titleStack.Controls.Add(_headerTitle, 0, 0);
        titleStack.Controls.Add(_headerSubtitle, 0, 1);
        header.Controls.Add(badge, 0, 0);
        header.Controls.Add(titleStack, 1, 0);
        root.Controls.Add(header, 0, 0);

        var card = new RoundedPanel { Dock = DockStyle.Fill, BackColor = Color.White, Radius = 22, Padding = new Padding(28, 26, 28, 22) };
        root.Controls.Add(card, 0, 1);
        var cardLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 };
        cardLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        cardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        cardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        cardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
        cardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        card.Controls.Add(cardLayout);

        _dropZone.Dock = DockStyle.Fill;
        _dropZone.Margin = new Padding(0, 0, 0, 14);
        _dropZone.AllowDrop = true;
        _folderGlyph.Size = new Size(72, 62);
        _dropTitle.Text = "把问题文件夹拖到这里";
        _dropTitle.Font = new Font("Microsoft YaHei UI", 14F, FontStyle.Bold);
        _dropTitle.ForeColor = Color.FromArgb(33, 49, 77);
        _dropTitle.TextAlign = ContentAlignment.MiddleCenter;
        _dropTitle.AutoSize = false;
        _dropHint.Text = "也可以直接选择文件夹";
        _dropHint.ForeColor = Color.FromArgb(116, 129, 150);
        _dropHint.TextAlign = ContentAlignment.MiddleCenter;
        _dropHint.AutoEllipsis = true;
        ConfigureSmallButton(_selectButton, "选择文件夹");
        _resetLink.Text = "清空重选";
        ConfigureLink(_resetLink);
        _resetLink.Visible = false;
        _dropZone.Controls.AddRange([_folderGlyph, _dropTitle, _dropHint, _selectButton, _resetLink]);
        foreach (Control child in _dropZone.Controls)
        {
            child.AllowDrop = true;
            child.DragEnter += OnDragEnter;
            child.DragLeave += (_, _) => _dropZone.Active = false;
            child.DragDrop += OnDragDrop;
        }
        _dropZone.Resize += (_, _) => LayoutDropZone();
        cardLayout.Controls.Add(_dropZone, 0, 0);

        _repairButton.Text = "一键修复";
        _repairButton.Dock = DockStyle.Fill;
        _repairButton.Margin = new Padding(0, 8, 0, 8);
        _repairButton.Radius = 12;
        _repairButton.Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold);
        _repairButton.BackColor = Color.FromArgb(47, 105, 224);
        _repairButton.HoverColor = Color.FromArgb(37, 87, 196);
        _repairButton.ForeColor = Color.White;
        _repairButton.Enabled = false;
        cardLayout.Controls.Add(_repairButton, 0, 1);

        _statusLabel.Text = "先拖入或选择一个文件夹";
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.TextAlign = ContentAlignment.MiddleCenter;
        _statusLabel.ForeColor = Color.FromArgb(119, 130, 149);
        cardLayout.Controls.Add(_statusLabel, 0, 2);
        _progress.Dock = DockStyle.Fill;
        _progress.Margin = new Padding(0, 4, 0, 4);
        _progress.Style = ProgressBarStyle.Marquee;
        _progress.MarqueeAnimationSpeed = 28;
        _progress.Visible = false;
        cardLayout.Controls.Add(_progress, 0, 3);

        var links = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = false, Padding = new Padding(0, 6, 0, 0) };
        _restoreLink.Text = "恢复上次修改";
        _backupLink.Text = "查看备份";
        _detailsLink.Text = "查看详情";
        _detailsLink.Visible = false;
        ConfigureLink(_restoreLink);
        ConfigureLink(_backupLink);
        ConfigureLink(_detailsLink);
        links.Controls.AddRange([_restoreLink, Separator(), _backupLink, Separator(), _detailsLink]);
        cardLayout.Controls.Add(links, 0, 4);

        root.Controls.Add(new Label { Text = "不联网 · 不删除个人文件 · 不重启电脑", Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomCenter, ForeColor = Color.FromArgb(137, 147, 164) }, 0, 2);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        BeginInvoke(() =>
        {
            PerformLayout();
            LayoutDropZone();
            _dropZone.Invalidate(true);
        });
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        BeginInvoke(LayoutDropZone);
    }

    private void WireEvents()
    {
        DragEnter += OnDragEnter;
        DragDrop += OnDragDrop;
        _dropZone.DragEnter += OnDragEnter;
        _dropZone.DragLeave += (_, _) => _dropZone.Active = false;
        _dropZone.DragDrop += OnDragDrop;
        _selectButton.Click += (_, _) => SelectFolder();
        _resetLink.LinkClicked += (_, _) => ResetSelection();
        _repairButton.Click += async (_, _) => await MainActionAsync();
        _restoreLink.LinkClicked += async (_, _) => await ChooseAndRestoreAsync();
        _backupLink.LinkClicked += (_, _) => OpenBackupLocation();
        _detailsLink.LinkClicked += (_, _) => ShowDetails();
    }

    private void LayoutDropZone()
    {
        if (_dropZone.ClientSize.Width <= 0 || _dropZone.ClientSize.Height <= 0) return;
        int center = _dropZone.ClientSize.Width / 2;
        int titleHeight = Math.Max(_dropTitle.PreferredHeight + 4, 36);
        int hintHeight = Math.Max(_dropHint.PreferredHeight + 2, 26);
        int gapAfterGlyph = 10;
        int gapAfterTitle = 2;
        int gapBeforeButton = 10;
        int gapBeforeReset = 8;
        bool showSelectButton = !_finished;
        bool showResetLink = _folders.Count > 0 && !_finished;
        int totalHeight = _folderGlyph.Height + gapAfterGlyph + titleHeight + gapAfterTitle + hintHeight;
        if (showSelectButton) totalHeight += gapBeforeButton + _selectButton.Height;
        if (showResetLink) totalHeight += gapBeforeReset + _resetLink.PreferredHeight;

        int y = Math.Max(4, (_dropZone.ClientSize.Height - totalHeight) / 2);
        _folderGlyph.Location = new Point(center - _folderGlyph.Width / 2, y);
        y = _folderGlyph.Bottom + gapAfterGlyph;
        _dropTitle.SetBounds(24, y, Math.Max(80, _dropZone.ClientSize.Width - 48), titleHeight);
        y = _dropTitle.Bottom + gapAfterTitle;
        _dropHint.SetBounds(36, y, Math.Max(80, _dropZone.ClientSize.Width - 72), hintHeight);
        y = _dropHint.Bottom;
        if (showSelectButton)
        {
            y += gapBeforeButton;
            _selectButton.Location = new Point(center - _selectButton.Width / 2, y);
            y = _selectButton.Bottom;
        }
        if (showResetLink)
        {
            y += gapBeforeReset;
            _resetLink.Location = new Point(center - _resetLink.PreferredWidth / 2, y);
        }
    }

    private static void ConfigureSmallButton(ModernButton button, string text)
    {
        button.Text = text;
        button.Size = new Size(150, 48);
        button.Radius = 10;
        button.BackColor = Color.FromArgb(235, 241, 253);
        button.HoverColor = Color.FromArgb(221, 231, 250);
        button.ForeColor = Color.FromArgb(42, 91, 190);
        button.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
    }

    private static void ConfigureLink(LinkLabel link)
    {
        link.AutoSize = true;
        link.LinkColor = Color.FromArgb(93, 108, 132);
        link.ActiveLinkColor = Color.FromArgb(47, 105, 224);
        link.VisitedLinkColor = link.LinkColor;
        link.LinkBehavior = LinkBehavior.HoverUnderline;
        link.Margin = new Padding(4, 0, 4, 0);
    }

    private static Label Separator() => new() { Text = "·", AutoSize = true, ForeColor = Color.FromArgb(190, 197, 208), Margin = new Padding(2, 0, 2, 0) };

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (_busy || e.Data?.GetDataPresent(DataFormats.FileDrop) != true) return;
        e.Effect = DragDropEffects.Copy;
        _dropZone.Active = true;
    }

    private void OnDragDrop(object? sender, DragEventArgs e)
    {
        _dropZone.Active = false;
        if (_busy || e.Data?.GetData(DataFormats.FileDrop) is not string[] paths) return;
        int added = 0;
        foreach (string path in paths.Where(Directory.Exists)) if (AddFolder(path)) added++;
        UpdateSelectionView();
        if (added == 0)
        {
            _statusLabel.Text = "请拖入文件夹，不要拖入单个文件";
            _statusLabel.ForeColor = Color.FromArgb(190, 91, 55);
        }
    }

    private void SelectFolder()
    {
        using var dialog = new FolderBrowserDialog { Description = "选择出现黑块的文件夹", UseDescriptionForTitle = true, ShowNewFolderButton = false, AutoUpgradeEnabled = true };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            AddFolder(dialog.SelectedPath);
            UpdateSelectionView();
        }
    }

    private bool AddFolder(string path)
    {
        try
        {
            string normalized = BackupService.NormalizeAndValidateFolder(path);
            if (_folders.Contains(normalized, StringComparer.OrdinalIgnoreCase)) return false;
            _folders.Add(normalized);
            AddLog("已选择：" + normalized);
            return true;
        }
        catch (Exception ex)
        {
            AddLog("无法加入：" + ex.Message);
            return false;
        }
    }

    private void UpdateSelectionView()
    {
        _finished = false;
        _folderGlyph.State = GlyphState.Folder;
        _dropZone.Completed = false;
        _selectButton.Visible = true;
        if (_folders.Count == 0)
        {
            _dropTitle.Text = "把问题文件夹拖到这里";
            _dropHint.Text = "也可以直接选择文件夹";
            _selectButton.Text = "选择文件夹";
            _resetLink.Visible = false;
            _repairButton.Enabled = false;
            _statusLabel.Text = "先拖入或选择一个文件夹";
        }
        else
        {
            _dropTitle.Text = _folders.Count == 1 ? "文件夹已选好" : $"已选好 {_folders.Count} 个文件夹";
            _dropHint.Text = _folders.Count == 1 ? _folders[0] : _folders[0] + $"  等 {_folders.Count} 个";
            _selectButton.Text = "继续添加";
            _resetLink.Visible = true;
            _repairButton.Text = "一键修复";
            _repairButton.Enabled = true;
            _statusLabel.Text = "修复时文件夹窗口会闪一下，不会重启电脑";
        }
        _statusLabel.ForeColor = Color.FromArgb(119, 130, 149);
        LayoutDropZone();
    }

    private void ResetSelection()
    {
        if (_busy) return;
        _folders.Clear();
        _logs.Clear();
        AddLog("已清空选择。 ");
        _detailsLink.Visible = false;
        UpdateSelectionView();
    }

    private async Task MainActionAsync()
    {
        if (_finished) { Close(); return; }
        if (_busy || _folders.Count == 0) return;
        SetBusy(true, "正在修复，请稍候……");
        int warnings = 0;
        try
        {
            AddLog("—— 开始修复 ——");
            foreach (string folder in _folders)
            {
                AddLog("处理：" + folder);
                FolderRepairResult result = await _backupService.BackupAndDisableCustomizationAsync(folder);
                AddLog((result.Success ? "成功：" : "提示：") + result.Message);
                if (!result.Success) warnings++;
                ExplorerRepairService.RefreshFolder(folder);
            }
            _statusLabel.Text = "正在重建缩略图，请不要操作文件夹窗口……";
            CacheRepairResult cache = await ExplorerRepairService.RebuildCachesAndRestartExplorerAsync(AddLog);
            AddLog(cache.Message);
            foreach (string failure in cache.FailedFiles) AddLog("提示：" + failure);
            if (!cache.Success) warnings++;
        }
        catch (Exception ex)
        {
            AddLog("未完全完成：" + ex.Message);
            warnings++;
        }
        finally
        {
            SetBusy(false, _statusLabel.Text);
            ShowFinished(warnings);
        }
    }

    private void ShowFinished(int warnings)
    {
        _finished = true;
        _progress.Visible = false;
        _dropZone.Completed = warnings == 0;
        _folderGlyph.State = warnings == 0 ? GlyphState.Success : GlyphState.Warning;
        _dropTitle.Text = warnings == 0 ? "修复完成" : "修复完成，有一项提示";
        _dropHint.Text = warnings == 0 ? "重新打开文件夹看看，缩略图会自动恢复" : "点击“查看详情”了解未完成的项目";
        _selectButton.Visible = false;
        _resetLink.Visible = false;
        _repairButton.Text = "完成";
        _repairButton.Enabled = true;
        _statusLabel.Text = warnings == 0 ? "已安全完成" : "个人文件未受影响";
        _statusLabel.ForeColor = warnings == 0 ? Color.FromArgb(42, 142, 91) : Color.FromArgb(187, 105, 36);
        _detailsLink.Visible = true;
        LayoutDropZone();
    }

    private async Task ChooseAndRestoreAsync()
    {
        if (_busy) return;
        using var dialog = new FolderBrowserDialog { Description = "选择要恢复的文件夹", UseDescriptionForTitle = true, ShowNewFolderButton = false };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        SetBusy(true, "正在恢复……");
        try
        {
            RestoreResult result = await _backupService.RestoreLatestAsync(dialog.SelectedPath);
            AddLog(result.Message);
            _detailsLink.Visible = true;
            if (result.Success) ExplorerRepairService.RefreshFolder(dialog.SelectedPath);
            MessageBox.Show(this, result.Message, result.Success ? "恢复完成" : "没有恢复", MessageBoxButtons.OK, result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
        finally { SetBusy(false, "就绪"); }
    }

    private void OpenBackupLocation()
    {
        try
        {
            Directory.CreateDirectory(_backupService.BackupRoot);
            Process.Start(new ProcessStartInfo { FileName = _backupService.BackupRoot, UseShellExecute = true });
        }
        catch (Exception ex) { AddLog("无法打开备份位置：" + ex.Message); _detailsLink.Visible = true; }
    }

    private void ShowDetails()
    {
        using var details = new Form { Text = "处理详情", StartPosition = FormStartPosition.CenterParent, Size = new Size(680, 440), MinimumSize = new Size(520, 340), BackColor = Color.White, Font = Font };
        var text = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.None, BackColor = Color.White, Font = new Font("Microsoft YaHei UI", 9F), Text = string.Join(Environment.NewLine, _logs) };
        var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(22) };
        host.Controls.Add(text);
        details.Controls.Add(host);
        details.ShowDialog(this);
    }

    private void SetBusy(bool busy, string status)
    {
        _busy = busy;
        _selectButton.Enabled = !busy;
        _repairButton.Enabled = !busy;
        _restoreLink.Enabled = !busy;
        _backupLink.Enabled = !busy;
        _resetLink.Enabled = !busy;
        AllowDrop = !busy;
        _dropZone.AllowDrop = !busy;
        _progress.Visible = busy;
        _statusLabel.Text = status;
        UseWaitCursor = busy;
    }

    private void AddLog(string message) => _logs.Add($"[{DateTime.Now:HH:mm:ss}] {message}");

    internal bool ValidateUiContract(out string problem)
    {
        PerformLayout();
        LayoutDropZone();
        if (_dropZone.Controls.Cast<Control>().Any(control => !control.AllowDrop))
        {
            problem = "拖放区域存在不能接收文件夹的子控件";
            return false;
        }
        if (_repairButton.Text != "一键修复" || _repairButton.Enabled)
        {
            problem = "初始主按钮状态不正确";
            return false;
        }
        if (_dropTitle.Text != "把问题文件夹拖到这里")
        {
            problem = "初始引导文字不正确";
            return false;
        }
        if (_headerTitle.Parent == _headerSubtitle.Parent && _headerTitle.Bounds.IntersectsWith(_headerSubtitle.Bounds))
        {
            problem = "标题与副标题发生重叠";
            return false;
        }
        if (_folderGlyph.Bounds.IntersectsWith(_dropTitle.Bounds) ||
            _dropTitle.Bounds.IntersectsWith(_dropHint.Bounds) ||
            (!_finished && (_selectButton.Top < _dropHint.Bottom || _selectButton.Bottom > _dropZone.ClientSize.Height ||
                Math.Abs((_selectButton.Left + _selectButton.Width / 2) - _dropZone.ClientSize.Width / 2) > 2)))
        {
            problem = "拖放区文字或按钮发生重叠";
            return false;
        }
        int buttonTextHeight = TextRenderer.MeasureText(_selectButton.Text, _selectButton.Font).Height;
        if (_selectButton.ClientSize.Height < buttonTextHeight + Math.Max(4, DeviceDpi / 12))
        {
            problem = "选择按钮高度不足，文字可能被裁切";
            return false;
        }
        problem = string.Empty;
        return true;
    }

    internal string GetLayoutDiagnostics() =>
        $"DPI={DeviceDpi}; DropZone={_dropZone.ClientRectangle}; Glyph={_folderGlyph.Bounds}; " +
        $"Title={_dropTitle.Bounds}; Hint={_dropHint.Bounds}; Select={_selectButton.Bounds}; " +
        $"SelectVisible={_selectButton.Visible}; Finished={_finished}";
}

internal sealed class RoundedPanel : Panel
{
    public int Radius { get; set; } = 18;
    public RoundedPanel() => SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using GraphicsPath path = UiDrawing.RoundedRect(ClientRectangle, Radius);
        using var brush = new SolidBrush(BackColor);
        e.Graphics.FillPath(brush, path);
    }
}

internal sealed class DropZonePanel : Panel
{
    private bool _active;
    private bool _completed;
    public bool Active { get => _active; set { _active = value; Invalidate(); } }
    public bool Completed { get => _completed; set { _completed = value; Invalidate(); } }
    public DropZonePanel()
    {
        BackColor = Color.FromArgb(249, 251, 255);
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle rect = ClientRectangle; rect.Inflate(-2, -2);
        using GraphicsPath path = UiDrawing.RoundedRect(rect, 16);
        Color fill = Completed ? Color.FromArgb(245, 252, 248) : Active ? Color.FromArgb(240, 246, 255) : BackColor;
        using var brush = new SolidBrush(fill);
        using var pen = new Pen(Active ? Color.FromArgb(47, 105, 224) : Color.FromArgb(190, 204, 226), Active ? 2.2F : 1.4F) { DashStyle = DashStyle.Dash };
        e.Graphics.FillPath(brush, path);
        e.Graphics.DrawPath(pen, path);
    }
}

internal enum GlyphState { Folder, Success, Warning }

internal sealed class FolderGlyph : Control
{
    private GlyphState _state;
    public GlyphState State { get => _state; set { _state = value; Invalidate(); } }
    public FolderGlyph()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        if (State == GlyphState.Folder)
        {
            using var brush = new SolidBrush(Color.FromArgb(224, 234, 253));
            using var pen = new Pen(Color.FromArgb(47, 105, 224), 2.5F);
            using var path = new GraphicsPath();
            path.AddLine(9, 16, 27, 16); path.AddLine(27, 16, 33, 23); path.AddLine(33, 23, 63, 23);
            path.AddArc(59, 21, 8, 8, 270, 90); path.AddLine(67, 25, 67, 50); path.AddArc(59, 46, 8, 8, 0, 90);
            path.AddLine(63, 54, 12, 54); path.AddArc(5, 46, 8, 8, 90, 90); path.AddLine(5, 50, 5, 20); path.AddArc(5, 16, 8, 8, 180, 90); path.CloseFigure();
            e.Graphics.FillPath(brush, path); e.Graphics.DrawPath(pen, path);
        }
        else
        {
            Color color = State == GlyphState.Success ? Color.FromArgb(50, 165, 105) : Color.FromArgb(220, 139, 55);
            using var brush = new SolidBrush(Color.FromArgb(28, color));
            using var pen = new Pen(color, 3F) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            e.Graphics.FillEllipse(brush, 7, 3, 58, 58); e.Graphics.DrawEllipse(pen, 8, 4, 56, 56);
            if (State == GlyphState.Success) e.Graphics.DrawLines(pen, new Point[] { new(22, 32), new(30, 40), new(49, 21) });
            else { e.Graphics.DrawLine(pen, 36, 18, 36, 39); e.Graphics.FillEllipse(Brushes.Orange, 33, 46, 6, 6); }
        }
    }
}

internal sealed class LogoBadge : Control
{
    public LogoBadge()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var brush = new LinearGradientBrush(ClientRectangle, Color.FromArgb(69, 127, 240), Color.FromArgb(92, 92, 225), 45F);
        e.Graphics.FillEllipse(brush, 1, 1, Width - 2, Height - 2);
        using var pen = new Pen(Color.White, 2.4F) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        e.Graphics.DrawLines(pen, new Point[] { new(14, 27), new(22, 27), new(26, 22), new(42, 22), new(43, 38), new(14, 38), new(14, 27) });
        e.Graphics.DrawLine(pen, 21, 33, 36, 33);
    }
}

internal sealed class ModernButton : Control
{
    public int Radius { get; set; } = 10;
    public Color HoverColor { get; set; } = Color.Empty;
    private bool _hovered;
    public ModernButton()
    {
        Cursor = Cursors.Hand;
        TabStop = true;
        AccessibleRole = AccessibleRole.PushButton;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.StandardClick | ControlStyles.Selectable, true);
    }
    protected override void OnMouseEnter(EventArgs e) { _hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hovered = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Space or Keys.Enter || base.IsInputKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (Enabled && e.KeyCode is Keys.Space or Keys.Enter)
        {
            OnClick(EventArgs.Empty);
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent?.BackColor ?? SystemColors.Control);
        Rectangle rect = ClientRectangle; rect.Inflate(-1, -1);
        using GraphicsPath path = UiDrawing.RoundedRect(rect, Radius);
        Color fill = !Enabled ? Color.FromArgb(207, 214, 226) : _hovered && HoverColor != Color.Empty ? HoverColor : BackColor;
        using var brush = new SolidBrush(fill); e.Graphics.FillPath(brush, path);
        TextRenderer.DrawText(e.Graphics, Text, Font, rect, Enabled ? ForeColor : Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        if (Focused && ShowFocusCues)
        {
            Rectangle focusRect = rect;
            focusRect.Inflate(-4, -4);
            ControlPaint.DrawFocusRectangle(e.Graphics, focusRect, ForeColor, fill);
        }
    }
}

internal static class UiDrawing
{
    public static GraphicsPath RoundedRect(Rectangle rect, int radius)
    {
        int diameter = Math.Max(2, radius * 2);
        var path = new GraphicsPath();
        path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
