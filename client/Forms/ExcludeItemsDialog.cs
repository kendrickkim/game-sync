using System.Runtime.InteropServices;
using GameSync.Services;

namespace GameSync.Forms;

public sealed class ExcludeItemsDialog : Form
{
    private const int FolderImage = 0;
    private const int FileImage = 1;

    private readonly string _root;
    private readonly ImageList _icons;
    private readonly ImageList _checkStates;
    private readonly TreeView _tree;
    private readonly ListView _list;
    private readonly HashSet<string> _excludes = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> ExcludeRelativePaths =>
        _excludes.OrderBy(v => v, StringComparer.OrdinalIgnoreCase).ToList();

    public ExcludeItemsDialog(string rootDirectory, IEnumerable<string>? currentExcludes)
    {
        _root = Path.GetFullPath(rootDirectory);
        _icons = ShellFileIcons.CreateSmallImageList();
        _checkStates = ShellFileIcons.CreateCheckStateImageList();

        Text = "백업 제외 항목";
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = false;
        MaximizeBox = true;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(860, 680);
        ClientSize = new Size(920, 680);
        Icon = AppIcon.Value;

        foreach (var item in BackupExclude.NormalizeList(_root, currentExcludes))
        {
            _excludes.Add(item);
        }

        var lblRoot = new Label
        {
            Text = $"게임 디렉토리: {_root}",
            Location = new Point(16, 12),
            Size = new Size(888, 28),
            AutoSize = false,
            AutoEllipsis = true,
            UseCompatibleTextRendering = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        };

        var lblTree = new Label
        {
            Text = "디렉토리에서 선택",
            Location = new Point(16, 46),
            AutoSize = true,
            UseCompatibleTextRendering = true,
        };

        _tree = new TreeView
        {
            Location = new Point(16, 86),
            Size = new Size(520, 416),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            CheckBoxes = false,
            HideSelection = false,
            ShowLines = true,
            ShowPlusMinus = true,
            ShowRootLines = true,
            FullRowSelect = false,
            ItemHeight = 36,
            Indent = 28,
            ImageList = _icons,
            StateImageList = _checkStates,
            DrawMode = TreeViewDrawMode.OwnerDrawText,
        };
        _tree.ItemHeight = 36;
        _tree.DrawNode += Tree_DrawNode;
        _tree.BeforeExpand += Tree_BeforeExpand;
        _tree.NodeMouseClick += Tree_NodeMouseClick;
        _tree.KeyDown += Tree_KeyDown;

        var lblList = new Label
        {
            Text = "제외할 파일 / 폴더",
            Location = new Point(552, 46),
            AutoSize = true,
            UseCompatibleTextRendering = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };

        _list = new ListView
        {
            Location = new Point(552, 86),
            Size = new Size(352, 266),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right,
            View = View.Details,
            HeaderStyle = ColumnHeaderStyle.None,
            FullRowSelect = true,
            HideSelection = false,
            MultiSelect = true,
            SmallImageList = _icons,
        };
        _list.Columns.Add("항목", 320);

        const int buttonWidth = 352;
        const int buttonHeight = 40;
        var btnAddFiles = new Button
        {
            Text = "파일 추가",
            Location = new Point(552, 364),
            Size = new Size(buttonWidth, buttonHeight),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
        };
        btnAddFiles.Click += (_, _) => AddFiles();

        var btnAddFolder = new Button
        {
            Text = "폴더 추가",
            Location = new Point(552, 412),
            Size = new Size(buttonWidth, buttonHeight),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
        };
        btnAddFolder.Click += (_, _) => AddFolder();

        var btnRemove = new Button
        {
            Text = "선택 제거",
            Location = new Point(552, 460),
            Size = new Size(buttonWidth, buttonHeight),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
        };
        btnRemove.Click += (_, _) => RemoveSelected();

        var lblHint = new Label
        {
            Text = "체크하거나 파일/폴더 추가로 백업에서 빼둘 항목을 지정합니다." +
                   Environment.NewLine +
                   "폴더를 제외하면 그 안의 모든 파일이 빠집니다.",
            Location = new Point(16, 508),
            Size = new Size(650, 60),
            AutoSize = false,
            UseCompatibleTextRendering = true,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
        };

        var btnOk = new Button
        {
            Text = "확인",
            Location = new Point(680, 588),
            Size = new Size(108, 40),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            DialogResult = DialogResult.OK,
        };

        var btnCancel = new Button
        {
            Text = "취소",
            Location = new Point(796, 588),
            Size = new Size(108, 40),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            DialogResult = DialogResult.Cancel,
        };

        Controls.AddRange(new Control[]
        {
            lblRoot, lblTree, _tree, lblList, _list,
            btnAddFiles, btnAddFolder, btnRemove, lblHint, btnOk, btnCancel,
        });

        AcceptButton = btnOk;
        CancelButton = btnCancel;
        FormClosed += (_, _) =>
        {
            _icons.Dispose();
            _checkStates.Dispose();
        };

        LoadRoot();
        RefreshList();
    }

    private void Tree_DrawNode(object? sender, DrawTreeNodeEventArgs e)
    {
        if (e.Node is null)
        {
            return;
        }

        var selected = (e.State & TreeNodeStates.Selected) != 0 && e.Node.TreeView?.Focused == true;
        if (selected)
        {
            using var fill = new SolidBrush(SystemColors.Highlight);
            e.Graphics.FillRectangle(fill, e.Bounds);
        }

        TextRenderer.DrawText(
            e.Graphics,
            e.Node.Text,
            _tree.Font,
            e.Bounds,
            selected ? SystemColors.HighlightText : _tree.ForeColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix |
            TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.NoClipping);
    }

    private void LoadRoot()
    {
        _tree.BeginUpdate();
        _tree.Nodes.Clear();
        try
        {
            foreach (var entry in EnumerateChildren(_root))
            {
                _tree.Nodes.Add(CreateNode(entry));
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "디렉토리 읽기 실패", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        _tree.EndUpdate();
    }

    private TreeNode CreateNode(FileSystemInfo info)
    {
        BackupExclude.TryMakeRelative(_root, info.FullName, out var relative);
        var isFolder = info is DirectoryInfo;
        var imageIndex = isFolder ? FolderImage : FileImage;
        var node = new TreeNode(info.Name)
        {
            Tag = relative,
            ImageIndex = imageIndex,
            SelectedImageIndex = imageIndex,
            StateImageIndex = _excludes.Contains(relative) ? 1 : 0,
        };

        if (isFolder)
        {
            node.Nodes.Add(new TreeNode("…") { Tag = null });
        }

        return node;
    }

    private static IEnumerable<FileSystemInfo> EnumerateChildren(string directory)
    {
        var dir = new DirectoryInfo(directory);
        IEnumerable<FileSystemInfo> dirs = Array.Empty<FileSystemInfo>();
        IEnumerable<FileSystemInfo> files = Array.Empty<FileSystemInfo>();

        try
        {
            dirs = dir.GetDirectories().OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            // skip unreadable directories
        }

        try
        {
            files = dir.GetFiles().OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            // skip unreadable files
        }

        return dirs.Concat(files);
    }

    private void Tree_BeforeExpand(object? sender, TreeViewCancelEventArgs e)
    {
        if (e.Node is null || e.Node.Nodes.Count != 1 || e.Node.Nodes[0].Tag is not null)
        {
            return;
        }

        e.Node.Nodes.Clear();
        if (e.Node.Tag is not string relative)
        {
            return;
        }

        var fullPath = Path.Combine(_root, relative);
        if (!Directory.Exists(fullPath))
        {
            return;
        }

        foreach (var entry in EnumerateChildren(fullPath))
        {
            e.Node.Nodes.Add(CreateNode(entry));
        }
    }

    private void Tree_NodeMouseClick(object? sender, TreeNodeMouseClickEventArgs e)
    {
        if (e.Button != MouseButtons.Left || e.Node is null)
        {
            return;
        }

        var hit = _tree.HitTest(e.Location);
        if (hit.Location is TreeViewHitTestLocations.StateImage or TreeViewHitTestLocations.Label or TreeViewHitTestLocations.Image)
        {
            ToggleNode(e.Node);
        }
    }

    private void Tree_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space && _tree.SelectedNode is not null)
        {
            ToggleNode(_tree.SelectedNode);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

    private void ToggleNode(TreeNode node)
    {
        if (node.Tag is not string relative || string.IsNullOrWhiteSpace(relative))
        {
            return;
        }

        if (_excludes.Contains(relative))
        {
            _excludes.Remove(relative);
        }
        else
        {
            _excludes.Add(relative);
        }

        RefreshList();
        SyncVisibleChecks();
    }

    private void AddFiles()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "백업에서 제외할 파일 선택",
            InitialDirectory = _root,
            Multiselect = true,
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var added = 0;
        foreach (var file in dialog.FileNames)
        {
            if (TryAddPath(file))
            {
                added++;
            }
        }

        if (added == 0)
        {
            MessageBox.Show(
                this,
                "선택한 파일이 게임 디렉토리 안에 없습니다.",
                "제외 항목",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        RefreshList();
        SyncVisibleChecks();
    }

    private void AddFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "백업에서 제외할 폴더 선택",
            UseDescriptionForTitle = true,
            SelectedPath = _root,
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        if (!TryAddPath(dialog.SelectedPath))
        {
            MessageBox.Show(
                this,
                "게임 디렉토리 안의 하위 폴더만 제외할 수 있습니다.",
                "제외 항목",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        RefreshList();
        SyncVisibleChecks();
    }

    private bool TryAddPath(string fullPath)
    {
        if (!BackupExclude.TryMakeRelative(_root, fullPath, out var relative))
        {
            return false;
        }

        _excludes.Add(relative);
        return true;
    }

    private void RemoveSelected()
    {
        if (_list.SelectedItems.Count == 0)
        {
            return;
        }

        foreach (ListViewItem item in _list.SelectedItems)
        {
            if (item.Tag is string relative)
            {
                _excludes.Remove(relative);
            }
        }

        RefreshList();
        SyncVisibleChecks();
    }

    private void RefreshList()
    {
        var items = BackupExclude.NormalizeList(_root, _excludes);
        _excludes.Clear();
        foreach (var item in items)
        {
            _excludes.Add(item);
        }

        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var item in items)
        {
            var fullPath = Path.Combine(_root, item);
            var isFolder = Directory.Exists(fullPath);
            var viewItem = new ListViewItem(item)
            {
                Tag = item,
                ImageIndex = isFolder ? FolderImage : FileImage,
            };
            _list.Items.Add(viewItem);
        }

        if (_list.Columns.Count > 0)
        {
            _list.Columns[0].Width = _list.ClientSize.Width - 4;
        }

        _list.EndUpdate();
    }

    private void SyncVisibleChecks()
    {
        foreach (TreeNode node in _tree.Nodes)
        {
            SyncNode(node);
        }
    }

    private void SyncNode(TreeNode node)
    {
        if (node.Tag is string relative)
        {
            node.StateImageIndex = _excludes.Contains(relative) ? 1 : 0;
        }

        foreach (TreeNode child in node.Nodes)
        {
            SyncNode(child);
        }
    }
}

internal static class ShellFileIcons
{
    private const int TileSize = 32;
    private const int GlyphSize = 16;
    private const uint FileAttributeDirectory = 0x10;
    private const uint FileAttributeNormal = 0x80;
    private const uint ShgfiIcon = 0x100;
    private const uint ShgfiSmallIcon = 0x1;
    private const uint ShgfiUseFileAttributes = 0x10;

    public static ImageList CreateSmallImageList()
    {
        var list = new ImageList
        {
            ColorDepth = ColorDepth.Depth32Bit,
            ImageSize = new Size(TileSize, TileSize),
        };

        using var folder = GetIcon(isFolder: true);
        using var file = GetIcon(isFolder: false);
        list.Images.Add(PadIcon(folder));
        list.Images.Add(PadIcon(file));
        return list;
    }

    public static ImageList CreateCheckStateImageList()
    {
        var list = new ImageList
        {
            ColorDepth = ColorDepth.Depth32Bit,
            ImageSize = new Size(TileSize, TileSize),
        };
        list.Images.Add(DrawCheckBox(false));
        list.Images.Add(DrawCheckBox(true));
        return list;
    }

    private static Bitmap PadIcon(Icon icon)
    {
        var bitmap = new Bitmap(TileSize, TileSize);
        using var g = Graphics.FromImage(bitmap);
        g.Clear(Color.Transparent);
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
        var offset = (TileSize - GlyphSize) / 2;
        g.DrawIcon(icon, new Rectangle(offset, offset, GlyphSize, GlyphSize));
        return bitmap;
    }

    private static Bitmap DrawCheckBox(bool isChecked)
    {
        var bitmap = new Bitmap(TileSize, TileSize);
        using var g = Graphics.FromImage(bitmap);
        g.Clear(Color.Transparent);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

        var box = new Rectangle((TileSize - 18) / 2, (TileSize - 18) / 2, 17, 17);
        using (var fill = new SolidBrush(Color.White))
        using (var border = new Pen(Color.FromArgb(110, 118, 128), 1.5f))
        {
            g.FillRectangle(fill, box);
            g.DrawRectangle(border, box);
        }

        if (isChecked)
        {
            using var check = new Pen(Color.FromArgb(32, 132, 90), 2.2f)
            {
                StartCap = System.Drawing.Drawing2D.LineCap.Round,
                EndCap = System.Drawing.Drawing2D.LineCap.Round,
                LineJoin = System.Drawing.Drawing2D.LineJoin.Round,
            };
            var cx = box.Left;
            var cy = box.Top;
            g.DrawLines(check, new[]
            {
                new Point(cx + 4, cy + 9),
                new Point(cx + 7, cy + 13),
                new Point(cx + 13, cy + 5),
            });
        }

        return bitmap;
    }

    private static Icon GetIcon(bool isFolder)
    {
        var info = new ShFileInfo();
        var attributes = isFolder ? FileAttributeDirectory : FileAttributeNormal;
        SHGetFileInfo(
            isFolder ? "folder" : "file",
            attributes,
            ref info,
            (uint)Marshal.SizeOf<ShFileInfo>(),
            ShgfiIcon | ShgfiSmallIcon | ShgfiUseFileAttributes);

        if (info.hIcon == IntPtr.Zero)
        {
            return isFolder ? SystemIcons.WinLogo : SystemIcons.Application;
        }

        try
        {
            using var source = Icon.FromHandle(info.hIcon);
            return (Icon)source.Clone();
        }
        finally
        {
            DestroyIcon(info.hIcon);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        ref ShFileInfo psfi,
        uint cbFileInfo,
        uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileInfo
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }
}
