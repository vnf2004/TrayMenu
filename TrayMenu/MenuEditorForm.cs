namespace TrayMenu;

public sealed class MenuEditorForm : Form
{
    private enum DropKind
    {
        Before,
        Into,
        After
    }

    private sealed class NodeData
    {
        public required string AbsolutePath { get; set; }
        public required bool IsDirectory { get; set; }
    }

    private readonly string _rootFolder;
    private readonly Dictionary<string, List<string>> _order;
    private readonly TreeView _tree = new();
    private readonly ImageList _images = new() { ImageSize = new Size(16, 16), ColorDepth = ColorDepth.Depth32Bit };
    private TreeNode? _dragNode;

    public MenuEditorForm(string rootFolder)
    {
        _rootFolder = Path.GetFullPath(rootFolder);
        _order = OrderStore.Load();

        Text = "Редактирование меню";
        StartPosition = FormStartPosition.CenterScreen;
        MinimizeBox = false;
        MaximizeBox = true;
        Width = 520;
        Height = 560;
        MinimumSize = new Size(400, 360);
        ShowInTaskbar = true;

        _images.Images.Add("folder", CreateFolderBitmap());
        _images.Images.Add("shortcut", CreateShortcutBitmap());

        _tree.Dock = DockStyle.Fill;
        _tree.AllowDrop = true;
        _tree.LabelEdit = true;
        _tree.HideSelection = false;
        _tree.ImageList = _images;
        _tree.ItemDrag += TreeOnItemDrag;
        _tree.DragEnter += (_, e) => e.Effect = DragDropEffects.Move;
        _tree.DragOver += TreeOnDragOver;
        _tree.DragDrop += TreeOnDragDrop;
        _tree.AfterLabelEdit += TreeOnAfterLabelEdit;
        _tree.KeyDown += TreeOnKeyDown;
        _tree.NodeMouseClick += TreeOnNodeMouseClick;

        var newFolderButton = new Button { Text = "Новая папка", AutoSize = true };
        newFolderButton.Click += (_, _) => CreateFolder();

        var renameButton = new Button { Text = "Переименовать", AutoSize = true };
        renameButton.Click += (_, _) => BeginRename();

        var deleteButton = new Button { Text = "Удалить", AutoSize = true };
        deleteButton.Click += (_, _) => DeleteSelected();

        var closeButton = new Button { Text = "Закрыть", AutoSize = true, DialogResult = DialogResult.OK };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            Padding = new Padding(8, 8, 8, 8),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        buttons.Controls.Add(newFolderButton);
        buttons.Controls.Add(renameButton);
        buttons.Controls.Add(deleteButton);
        buttons.Controls.Add(closeButton);

        var hint = new Label
        {
            Dock = DockStyle.Top,
            Height = 40,
            Padding = new Padding(8, 8, 8, 0),
            Text = "Перетаскивайте пункты: между соседями — порядок, на папку — внутрь. F2 — переименовать, Del — удалить."
        };

        Controls.Add(_tree);
        Controls.Add(hint);
        Controls.Add(buttons);

        AcceptButton = closeButton;
        LoadTree();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        OrderStore.Save(_order);
        base.OnFormClosed(e);
    }

    private void LoadTree()
    {
        _tree.BeginUpdate();
        _tree.Nodes.Clear();
        PopulateNodes(_tree.Nodes, _rootFolder);
        _tree.ExpandAll();
        _tree.EndUpdate();
    }

    private void PopulateNodes(TreeNodeCollection nodes, string directory)
    {
        foreach (var name in OrderStore.GetOrderedChildNames(_rootFolder, directory, _order))
        {
            var fullPath = Path.Combine(directory, name);
            if (Directory.Exists(fullPath))
            {
                var node = CreateNode(fullPath, isDirectory: true);
                nodes.Add(node);
                PopulateNodes(node.Nodes, fullPath);
            }
            else if (File.Exists(fullPath))
            {
                nodes.Add(CreateNode(fullPath, isDirectory: false));
            }
        }
    }

    private TreeNode CreateNode(string absolutePath, bool isDirectory)
    {
        var text = isDirectory
            ? Path.GetFileName(absolutePath)
            : Path.GetFileNameWithoutExtension(absolutePath);

        return new TreeNode(text)
        {
            Tag = new NodeData { AbsolutePath = absolutePath, IsDirectory = isDirectory },
            ImageKey = isDirectory ? "folder" : "shortcut",
            SelectedImageKey = isDirectory ? "folder" : "shortcut"
        };
    }

    private static NodeData Data(TreeNode node) => (NodeData)node.Tag!;

    private void TreeOnItemDrag(object? sender, ItemDragEventArgs e)
    {
        if (e.Item is TreeNode node)
        {
            _dragNode = node;
            DoDragDrop(node, DragDropEffects.Move);
        }
    }

    private void TreeOnDragOver(object? sender, DragEventArgs e)
    {
        e.Effect = DragDropEffects.None;
        if (_dragNode is null || !e.Data!.GetDataPresent(typeof(TreeNode)))
        {
            return;
        }

        var target = GetNodeAt(e);
        if (target is null)
        {
            // Drop at root end
            e.Effect = DragDropEffects.Move;
            return;
        }

        if (ReferenceEquals(target, _dragNode) || IsDescendant(target, _dragNode))
        {
            return;
        }

        e.Effect = DragDropEffects.Move;
    }

    private void TreeOnDragDrop(object? sender, DragEventArgs e)
    {
        if (_dragNode is null)
        {
            return;
        }

        var source = _dragNode;
        _dragNode = null;

        var target = GetNodeAt(e);
        try
        {
            if (target is null)
            {
                MoveNode(source, parentDirectory: _rootFolder, insertIndex: CountRootChildren());
                return;
            }

            if (ReferenceEquals(target, source) || IsDescendant(target, source))
            {
                return;
            }

            var kind = GetDropKind(target, e);
            var targetData = Data(target);

            if (kind == DropKind.Into && targetData.IsDirectory)
            {
                MoveNode(source, parentDirectory: targetData.AbsolutePath, insertIndex: target.Nodes.Count);
                return;
            }

            var parentDir = target.Parent is null
                ? _rootFolder
                : Data(target.Parent).AbsolutePath;
            var index = target.Index;
            if (kind == DropKind.After)
            {
                index++;
            }

            MoveNode(source, parentDir, index);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "TrayMenu", MessageBoxButtons.OK, MessageBoxIcon.Error);
            LoadTree();
        }
    }

    private void MoveNode(TreeNode source, string parentDirectory, int insertIndex)
    {
        var data = Data(source);
        var sourcePath = data.AbsolutePath;
        var fileName = Path.GetFileName(sourcePath);
        var destPath = Path.Combine(parentDirectory, fileName);

        var sourceParent = Path.GetDirectoryName(sourcePath)!;
        var sameParent = string.Equals(
            Path.GetFullPath(sourceParent),
            Path.GetFullPath(parentDirectory),
            StringComparison.OrdinalIgnoreCase);

        if (sameParent)
        {
            ReorderInParent(source, parentDirectory, insertIndex);
            return;
        }

        if (File.Exists(destPath) || Directory.Exists(destPath))
        {
            throw new IOException($"Уже существует:\n{destPath}");
        }

        var oldRel = data.IsDirectory
            ? OrderStore.ToRelativeDir(_rootFolder, sourcePath)
            : OrderStore.NormalizeKey(Path.GetRelativePath(_rootFolder, sourcePath));
        var oldName = Path.GetFileName(sourcePath);

        if (data.IsDirectory)
        {
            Directory.Move(sourcePath, destPath);
            var newRel = OrderStore.ToRelativeDir(_rootFolder, destPath);
            OrderStore.RewritePathPrefix(_order, oldRel, newRel, updateParentList: false);
        }
        else
        {
            File.Move(sourcePath, destPath);
        }

        var oldParentKey = OrderStore.ToRelativeDir(_rootFolder, sourceParent);
        if (_order.TryGetValue(oldParentKey, out var oldSiblings))
        {
            oldSiblings.RemoveAll(s => string.Equals(s, oldName, StringComparison.OrdinalIgnoreCase));
        }

        InsertIntoOrder(parentDirectory, Path.GetFileName(destPath), insertIndex);
        data.AbsolutePath = destPath;
        RefreshTreeKeepingExpansion();
    }

    private void ReorderInParent(TreeNode source, string parentDirectory, int insertIndex)
    {
        var parentNodes = source.Parent?.Nodes ?? _tree.Nodes;
        var currentIndex = source.Index;
        if (insertIndex > currentIndex)
        {
            insertIndex--;
        }

        if (insertIndex == currentIndex)
        {
            return;
        }

        parentNodes.Remove(source);
        parentNodes.Insert(insertIndex, source);
        PersistLevelOrder(parentDirectory, parentNodes);
    }

    private void InsertIntoOrder(string parentDirectory, string childName, int insertIndex)
    {
        var names = OrderStore.GetOrderedChildNames(_rootFolder, parentDirectory, _order)
            .Where(n => !string.Equals(n, childName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (insertIndex < 0)
        {
            insertIndex = 0;
        }

        if (insertIndex > names.Count)
        {
            insertIndex = names.Count;
        }

        names.Insert(insertIndex, childName);
        OrderStore.SetLevelOrder(_order, _rootFolder, parentDirectory, names);
    }

    private void PersistLevelOrder(string parentDirectory, TreeNodeCollection nodes)
    {
        var names = nodes.Cast<TreeNode>().Select(n => Path.GetFileName(Data(n).AbsolutePath)).ToList();
        OrderStore.SetLevelOrder(_order, _rootFolder, parentDirectory, names);
    }

    private void RefreshTreeKeepingExpansion()
    {
        var selected = _tree.SelectedNode is null ? null : Data(_tree.SelectedNode).AbsolutePath;
        var expanded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectExpanded(_tree.Nodes, expanded);

        LoadTree();

        foreach (TreeNode node in _tree.Nodes)
        {
            RestoreExpanded(node, expanded);
        }

        if (selected is not null)
        {
            _tree.SelectedNode = FindNode(_tree.Nodes, selected);
        }
    }

    private static void CollectExpanded(TreeNodeCollection nodes, HashSet<string> expanded)
    {
        foreach (TreeNode node in nodes)
        {
            if (node.IsExpanded && Data(node).IsDirectory)
            {
                expanded.Add(Data(node).AbsolutePath);
            }

            CollectExpanded(node.Nodes, expanded);
        }
    }

    private static void RestoreExpanded(TreeNode node, HashSet<string> expanded)
    {
        if (Data(node).IsDirectory && expanded.Contains(Data(node).AbsolutePath))
        {
            node.Expand();
        }

        foreach (TreeNode child in node.Nodes)
        {
            RestoreExpanded(child, expanded);
        }
    }

    private static TreeNode? FindNode(TreeNodeCollection nodes, string absolutePath)
    {
        foreach (TreeNode node in nodes)
        {
            if (string.Equals(Data(node).AbsolutePath, absolutePath, StringComparison.OrdinalIgnoreCase))
            {
                return node;
            }

            var found = FindNode(node.Nodes, absolutePath);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private TreeNode? GetNodeAt(DragEventArgs e)
    {
        var point = _tree.PointToClient(new Point(e.X, e.Y));
        return _tree.GetNodeAt(point);
    }

    private DropKind GetDropKind(TreeNode target, DragEventArgs e)
    {
        var point = _tree.PointToClient(new Point(e.X, e.Y));
        var bounds = target.Bounds;
        var y = point.Y - bounds.Top;
        var third = Math.Max(bounds.Height / 3, 1);

        if (Data(target).IsDirectory)
        {
            if (y < third)
            {
                return DropKind.Before;
            }

            if (y > bounds.Height - third)
            {
                return DropKind.After;
            }

            return DropKind.Into;
        }

        return y < bounds.Height / 2 ? DropKind.Before : DropKind.After;
    }

    private static bool IsDescendant(TreeNode possibleDescendant, TreeNode ancestor)
    {
        var current = possibleDescendant.Parent;
        while (current is not null)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }

            current = current.Parent;
        }

        return false;
    }

    private int CountRootChildren() => _tree.Nodes.Count;

    private void CreateFolder()
    {
        var parentNode = _tree.SelectedNode;
        string parentDir;
        TreeNodeCollection collection;

        if (parentNode is not null && Data(parentNode).IsDirectory)
        {
            parentDir = Data(parentNode).AbsolutePath;
            collection = parentNode.Nodes;
        }
        else if (parentNode?.Parent is not null)
        {
            parentDir = Data(parentNode.Parent).AbsolutePath;
            collection = parentNode.Parent.Nodes;
        }
        else
        {
            parentDir = _rootFolder;
            collection = _tree.Nodes;
        }

        var name = NextAvailableName(parentDir, "Новая папка", isDirectory: true);
        var path = Path.Combine(parentDir, name);
        try
        {
            Directory.CreateDirectory(path);
            InsertIntoOrder(parentDir, name, collection.Count);
            var node = CreateNode(path, isDirectory: true);
            collection.Add(node);
            parentNode?.Expand();
            _tree.SelectedNode = node;
            BeginRename(node);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "TrayMenu", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string NextAvailableName(string parentDir, string baseName, bool isDirectory)
    {
        var candidate = baseName;
        var i = 2;
        while (true)
        {
            var full = Path.Combine(parentDir, candidate);
            var exists = isDirectory ? Directory.Exists(full) : File.Exists(full);
            if (!exists)
            {
                return candidate;
            }

            candidate = $"{baseName} ({i})";
            i++;
        }
    }

    private void BeginRename(TreeNode? node = null)
    {
        node ??= _tree.SelectedNode;
        if (node is null)
        {
            return;
        }

        _tree.SelectedNode = node;
        node.BeginEdit();
    }

    private void TreeOnAfterLabelEdit(object? sender, NodeLabelEditEventArgs e)
    {
        if (e.CancelEdit || e.Node is null)
        {
            return;
        }

        // LabelEdit fires with null label when cancelled
        if (e.Label is null)
        {
            return;
        }

        var node = e.Node;
        var data = Data(node);
        var newLabel = e.Label.Trim();
        if (string.IsNullOrWhiteSpace(newLabel))
        {
            e.CancelEdit = true;
            return;
        }

        // Invalid path chars
        if (newLabel.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            e.CancelEdit = true;
            MessageBox.Show(this, "Имя содержит недопустимые символы.", "TrayMenu",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var parentDir = Path.GetDirectoryName(data.AbsolutePath)!;
        string newPath;
        if (data.IsDirectory)
        {
            newPath = Path.Combine(parentDir, newLabel);
        }
        else
        {
            if (!newLabel.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                newLabel += ".lnk";
            }

            newPath = Path.Combine(parentDir, newLabel);
            // Display without extension — cancel default text apply; we'll set manually
        }

        if (string.Equals(data.AbsolutePath, newPath, StringComparison.OrdinalIgnoreCase))
        {
            if (!data.IsDirectory)
            {
                e.CancelEdit = true;
                node.Text = Path.GetFileNameWithoutExtension(data.AbsolutePath);
            }

            return;
        }

        if (File.Exists(newPath) || Directory.Exists(newPath))
        {
            e.CancelEdit = true;
            MessageBox.Show(this, $"Уже существует:\n{newPath}", "TrayMenu",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            var oldName = Path.GetFileName(data.AbsolutePath);
            var parentKey = OrderStore.ToRelativeDir(_rootFolder, parentDir);

            if (data.IsDirectory)
            {
                var oldRel = OrderStore.ToRelativeDir(_rootFolder, data.AbsolutePath);
                Directory.Move(data.AbsolutePath, newPath);
                var newRel = OrderStore.ToRelativeDir(_rootFolder, newPath);
                OrderStore.RewritePathPrefix(_order, oldRel, newRel, updateParentList: true);
            }
            else
            {
                File.Move(data.AbsolutePath, newPath);
                OrderStore.ReplaceChildName(_order, parentKey, oldName, Path.GetFileName(newPath));
                // If parent had no explicit order yet, persist current tree order with new name
                if (!_order.ContainsKey(parentKey))
                {
                    var siblings = (node.Parent?.Nodes ?? _tree.Nodes).Cast<TreeNode>()
                        .Select(n => ReferenceEquals(n, node) ? Path.GetFileName(newPath) : Path.GetFileName(Data(n).AbsolutePath))
                        .ToList();
                    OrderStore.SetLevelOrder(_order, _rootFolder, parentDir, siblings);
                }
            }

            data.AbsolutePath = newPath;
            e.CancelEdit = true;
            node.Text = data.IsDirectory ? Path.GetFileName(newPath) : Path.GetFileNameWithoutExtension(newPath);

            if (data.IsDirectory)
            {
                UpdateChildPaths(node, newPath);
            }
        }
        catch (Exception ex)
        {
            e.CancelEdit = true;
            MessageBox.Show(this, ex.Message, "TrayMenu", MessageBoxButtons.OK, MessageBoxIcon.Error);
            LoadTree();
        }
    }

    private static void UpdateChildPaths(TreeNode folderNode, string newFolderPath)
    {
        foreach (TreeNode child in folderNode.Nodes)
        {
            var childData = Data(child);
            var name = Path.GetFileName(childData.AbsolutePath);
            childData.AbsolutePath = Path.Combine(newFolderPath, name);
            if (childData.IsDirectory)
            {
                UpdateChildPaths(child, childData.AbsolutePath);
            }
        }
    }

    private void DeleteSelected()
    {
        var node = _tree.SelectedNode;
        if (node is null)
        {
            return;
        }

        var data = Data(node);
        var label = data.IsDirectory
            ? $"Удалить папку «{node.Text}» и всё её содержимое?"
            : $"Удалить ярлык «{node.Text}»?";

        if (MessageBox.Show(this, label, "TrayMenu", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        try
        {
            var rel = data.IsDirectory
                ? OrderStore.ToRelativeDir(_rootFolder, data.AbsolutePath)
                : OrderStore.NormalizeKey(Path.GetRelativePath(_rootFolder, data.AbsolutePath));

            if (data.IsDirectory)
            {
                Directory.Delete(data.AbsolutePath, recursive: true);
            }
            else
            {
                File.Delete(data.AbsolutePath);
            }

            OrderStore.RemovePath(_order, rel, data.IsDirectory);
            node.Remove();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "TrayMenu", MessageBoxButtons.OK, MessageBoxIcon.Error);
            LoadTree();
        }
    }

    private void TreeOnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.F2)
        {
            BeginRename();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Delete)
        {
            DeleteSelected();
            e.Handled = true;
        }
    }

    private void TreeOnNodeMouseClick(object? sender, TreeNodeMouseClickEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            _tree.SelectedNode = e.Node;
            var menu = new ContextMenuStrip();
            menu.Items.Add("Новая папка", null, (_, _) => CreateFolder());
            menu.Items.Add("Переименовать", null, (_, _) => BeginRename());
            menu.Items.Add("Удалить", null, (_, _) => DeleteSelected());
            menu.Show(_tree, e.Location);
        }
    }

    private static Bitmap CreateFolderBitmap()
    {
        var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Transparent);
        using var brush = new SolidBrush(Color.FromArgb(230, 190, 80));
        g.FillRectangle(brush, 2, 5, 12, 9);
        g.FillRectangle(brush, 2, 3, 6, 3);
        return bmp;
    }

    private static Bitmap CreateShortcutBitmap()
    {
        var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Transparent);
        using var brush = new SolidBrush(Color.FromArgb(70, 130, 200));
        g.FillRectangle(brush, 3, 2, 10, 12);
        using var pen = new Pen(Color.White);
        g.DrawLine(pen, 5, 5, 11, 5);
        g.DrawLine(pen, 5, 8, 11, 8);
        return bmp;
    }
}
