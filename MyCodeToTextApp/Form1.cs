using System;
using System.Windows.Forms;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Drawing;
using System.Linq;

namespace MyCodeToTextApp
{
    public partial class Form1 : Form
    {
        private Label lblProjectDir;
        private TextBox txtProjectDir;
        private Button btnBrowseProjectDir;
        private Label lblOutputDir;
        private TextBox txtOutputDir;
        private Button btnBrowseOutputDir;
        private Button btnGenerate;
        private CheckBox chkAutoGenerate;
        private GroupBox groupBoxInclude;
        private CheckedListBox chklstInclude;
        private GroupBox groupBoxExclude;
        private TreeView treeViewExclude;
        private CheckBox chkCustomExclude;
        private TextBox txtCustomExcludePatterns;
        private TextBox txtStatus;
        private Label lblCustomExclude;
        private TableLayoutPanel mainTableLayout;
        private SplitContainer splitContainer;

        private CodeConverter converter;
        private CodeDetector codeDetector;
        private List<string> customExcludePatterns = new List<string>();

        public Form1()
        {
            InitializeComponent();
            converter = new CodeConverter();
            codeDetector = new CodeDetector();
            converter.StatusUpdated += Converter_StatusUpdated;
        }

        private void InitializeComponent()
        {
            // 设置窗口属性
            this.Text = "代码转文本工具";
            this.Width = 1000;
            this.Height = 800;
            this.MinimumSize = new Size(800, 600);
            this.Font = new Font("微软雅黑", 9F, FontStyle.Regular, GraphicsUnit.Point, ((byte)(134)));

            // 创建主布局面板
            mainTableLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 3,
                Padding = new Padding(10)
            };

            mainTableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));
            mainTableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            mainTableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80F));

            mainTableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));
            mainTableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));
            mainTableLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            // 创建目录选择控件
            lblProjectDir = new Label { Text = "项目目录:", Anchor = AnchorStyles.Left, TextAlign = ContentAlignment.MiddleLeft };
            txtProjectDir = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(3) };
            btnBrowseProjectDir = new Button { Text = "浏览", Dock = DockStyle.Fill, Margin = new Padding(3) };
            btnBrowseProjectDir.Click += BtnBrowseProjectDir_Click;

            lblOutputDir = new Label { Text = "输出目录:", Anchor = AnchorStyles.Left, TextAlign = ContentAlignment.MiddleLeft };
            txtOutputDir = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(3) };
            btnBrowseOutputDir = new Button { Text = "浏览", Dock = DockStyle.Fill, Margin = new Padding(3) };
            btnBrowseOutputDir.Click += BtnBrowseOutputDir_Click;

            // 创建分割面板
            splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                Panel1MinSize = 100,  // 减小最小值
                Panel2MinSize = 100,  // 减小最小值
            };

            this.Load += (s, e) => UpdateSplitterDistance();
            this.SizeChanged += (s, e) => UpdateSplitterDistance();

            // 处理分割面板尺寸变化
            void UpdateSplitterDistance()
            {
                if (splitContainer.Width > (splitContainer.Panel1MinSize + splitContainer.Panel2MinSize))
                {
                    splitContainer.SplitterDistance = (splitContainer.Width - splitContainer.Panel2MinSize) / 2;
                }
            }

            // 左侧面板：包含和排除规则
            var leftPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1
            };

            leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 40F));
            leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 40F));
            leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));

            // 包含规则组
            groupBoxInclude = new GroupBox { Text = "包含文件类型", Dock = DockStyle.Fill, Margin = new Padding(3) };
            chklstInclude = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true };
            groupBoxInclude.Controls.Add(chklstInclude);

            // 排除规则组
            groupBoxExclude = new GroupBox { Text = "排除文件/文件夹", Dock = DockStyle.Fill, Margin = new Padding(3) };
            treeViewExclude = new TreeView { Dock = DockStyle.Fill, CheckBoxes = true };
            groupBoxExclude.Controls.Add(treeViewExclude);

            // 自定义排除模式
            var customExcludePanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                Margin = new Padding(3)
            };

            chkCustomExclude = new CheckBox { Text = "启用自定义排除模式", Dock = DockStyle.Top, AutoSize = true };
            txtCustomExcludePatterns = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                PlaceholderText = "每行输入一个模式，支持通配符 * 和 ?\n例如：\n*.designer.cs\n*.generated.cs\nobj/*\nbin/*"
            };

            customExcludePanel.Controls.Add(chkCustomExclude);
            customExcludePanel.Controls.Add(txtCustomExcludePatterns);

            // 添加到左侧面板
            leftPanel.Controls.Add(groupBoxInclude);
            leftPanel.Controls.Add(groupBoxExclude);
            leftPanel.Controls.Add(customExcludePanel);

            // 右侧面板：状态显示
            txtStatus = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Margin = new Padding(3)
            };

            // 添加自动检测按钮
            var detectButton = new Button
            {
                Text = "检测生成代码",
                AutoSize = true,
                Margin = new Padding(3),
                Dock = DockStyle.Top
            };
            detectButton.Click += DetectGeneratedCode_Click;
            groupBoxExclude.Controls.Add(detectButton);

            // 为TreeView添加勾选事件
            treeViewExclude.AfterCheck += TreeViewExclude_AfterCheck;

            // 配置分割面板
            splitContainer.Panel1.Controls.Add(leftPanel);
            splitContainer.Panel2.Controls.Add(txtStatus);

            // 控制按钮面板
            var controlPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = new Padding(3)
            };

            btnGenerate = new Button { Text = "生成", AutoSize = true, Margin = new Padding(3) };
            chkAutoGenerate = new CheckBox { Text = "自动生成", AutoSize = true, Margin = new Padding(10, 3, 3, 3) };

            btnGenerate.Click += BtnGenerate_Click;
            chkAutoGenerate.CheckedChanged += ChkAutoGenerate_CheckedChanged;

            controlPanel.Controls.Add(btnGenerate);
            controlPanel.Controls.Add(chkAutoGenerate);

            // 添加所有控件到主布局
            mainTableLayout.Controls.Add(lblProjectDir, 0, 0);
            mainTableLayout.Controls.Add(txtProjectDir, 1, 0);
            mainTableLayout.Controls.Add(btnBrowseProjectDir, 2, 0);

            mainTableLayout.Controls.Add(lblOutputDir, 0, 1);
            mainTableLayout.Controls.Add(txtOutputDir, 1, 1);
            mainTableLayout.Controls.Add(btnBrowseOutputDir, 2, 1);

            // 添加控制按钮和分割面板
            var bottomPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1
            };
            bottomPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));
            bottomPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            bottomPanel.Controls.Add(controlPanel, 0, 0);
            bottomPanel.Controls.Add(splitContainer, 0, 1);

            mainTableLayout.Controls.Add(bottomPanel, 0, 2);
            mainTableLayout.SetColumnSpan(bottomPanel, 3);

            this.Controls.Add(mainTableLayout);
        }
        private void BtnBrowseProjectDir_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    txtProjectDir.Text = dialog.SelectedPath;
                    UpdateIncludeExcludeLists(dialog.SelectedPath);
                }
            }
        }

        private void UpdateIncludeExcludeLists(string projectPath)
        {
            treeViewExclude.Nodes.Clear();
            chklstInclude.Items.Clear();

            try
            {
                var includedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    ".cs", ".xaml", ".txt", ".html", ".js", ".css", ".json", ".xml",
                    ".csproj", ".config", ".md", ".sql", ".py", ".java", ".cpp", ".h", ".ts"
                };

                var fileExtensions = Directory.GetFiles(projectPath, "*.*", SearchOption.AllDirectories)
                    .Select(f => Path.GetExtension(f).ToLower())
                    .Where(ext => includedExtensions.Contains(ext))
                    .Distinct()
                    .OrderBy(ext => ext);

                foreach (var ext in fileExtensions)
                {
                    chklstInclude.Items.Add("*" + ext, true);
                }

                PopulateTreeView(projectPath, projectPath, treeViewExclude.Nodes);
            }
            catch (Exception ex)
            {
                MessageBox.Show("扫描目录时出错：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void PopulateTreeView(string rootPath, string currentPath, TreeNodeCollection nodes)
        {
            string relativePath = Path.GetRelativePath(rootPath, currentPath);
            if (relativePath == ".")
            {
                relativePath = Path.GetFileName(rootPath);
            }

            TreeNode currentNode = new TreeNode(relativePath)
            {
                Tag = currentPath
            };
            nodes.Add(currentNode);

            foreach (string subDir in Directory.GetDirectories(currentPath))
            {
                PopulateTreeView(rootPath, subDir, currentNode.Nodes);
            }

            foreach (string file in Directory.GetFiles(currentPath))
            {
                TreeNode fileNode = new TreeNode(Path.GetFileName(file))
                {
                    Tag = file
                };
                currentNode.Nodes.Add(fileNode);
            }
        }

        private void BtnBrowseOutputDir_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    txtOutputDir.Text = dialog.SelectedPath;
                }
            }
        }

        private void Converter_StatusUpdated(object sender, string status)
        {
            if (txtStatus.InvokeRequired)
            {
                txtStatus.Invoke(new Action(() => txtStatus.AppendText(status + Environment.NewLine)));
            }
            else
            {
                txtStatus.AppendText(status + Environment.NewLine);
            }
        }

        private async void BtnGenerate_Click(object sender, EventArgs e)
        {
            await GenerateCodeText();
        }

        private void ChkAutoGenerate_CheckedChanged(object sender, EventArgs e)
        {
            if (chkAutoGenerate.Checked)
            {
                if (!string.IsNullOrEmpty(txtProjectDir.Text) && Directory.Exists(txtProjectDir.Text))
                {
                    converter.StartWatching(txtProjectDir.Text);
                    Converter_StatusUpdated(this, "开始监控: " + txtProjectDir.Text);
                }
                else
                {
                    MessageBox.Show("请先选择一个有效的项目目录。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    chkAutoGenerate.Checked = false;
                }
            }
            else
            {
                converter.StopWatching();
                Converter_StatusUpdated(this, "停止监控。");
            }
        }

        public async Task GenerateCodeText()
        {
            string projectDir = txtProjectDir.Text;
            string outputDir = string.IsNullOrEmpty(txtOutputDir.Text) ? projectDir : txtOutputDir.Text;

            var includeRules = new List<string>();
            foreach (var item in chklstInclude.CheckedItems)
            {
                includeRules.Add(item.ToString());
            }

            var excludeRules = new List<string>();

            // 从TreeView获取排除规则
            foreach (TreeNode node in GetCheckedNodes(treeViewExclude.Nodes))
            {
                string relativePath = GetNodePath(node, projectDir);
                if (Directory.Exists(node.Tag.ToString()))
                {
                    relativePath += "/*";
                }
                excludeRules.Add(relativePath);
            }

            // 添加自定义排除模式 - 修复此部分
            if (chkCustomExclude.Checked && !string.IsNullOrWhiteSpace(txtCustomExcludePatterns.Text))
            {
                var customPatterns = txtCustomExcludePatterns.Text
                    .Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .ToList();

                LogToStatus($"添加自定义排除模式: {string.Join(", ", customPatterns)}");
                excludeRules.AddRange(customPatterns);
            }

            // 基本验证
            if (string.IsNullOrEmpty(projectDir) || !Directory.Exists(projectDir))
            {
                MessageBox.Show("请选择一个有效的项目目录。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (string.IsNullOrEmpty(outputDir) || !Directory.Exists(outputDir))
            {
                MessageBox.Show("请选择一个有效的输出目录。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                btnGenerate.Enabled = false;

                // 记录排除规则到状态框
                txtStatus.AppendText("使用的排除规则：\r\n");
                foreach (var rule in excludeRules)
                {
                    txtStatus.AppendText($"- {rule}\r\n");
                }

                await Task.Run(() => converter.ConvertCodeToText(projectDir, outputDir, includeRules, excludeRules));
            }
            catch (Exception ex)
            {
                MessageBox.Show("发生错误: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnGenerate.Enabled = true;
            }
        }

        private List<TreeNode> GetCheckedNodes(TreeNodeCollection nodes)
        {
            var checkedNodes = new List<TreeNode>();
            foreach (TreeNode node in nodes)
            {
                if (node.Checked)
                {
                    checkedNodes.Add(node);
                }
                checkedNodes.AddRange(GetCheckedNodes(node.Nodes));
            }
            return checkedNodes;
        }

        private string GetNodePath(TreeNode node, string rootDir)
        {
            return Path.GetRelativePath(rootDir, node.Tag.ToString());
        }

        private void TreeViewExclude_AfterCheck(object sender, TreeViewEventArgs e)
        {
            // 阻止事件重复触发
            treeViewExclude.AfterCheck -= TreeViewExclude_AfterCheck;

            try
            {
                // 设置所有子节点的勾选状态
                SetChildNodesCheckState(e.Node, e.Node.Checked);

                // 更新父节点的勾选状态
                UpdateParentNodeCheckState(e.Node);
            }
            finally
            {
                // 重新添加事件处理
                treeViewExclude.AfterCheck += TreeViewExclude_AfterCheck;
            }
        }

        private void SetChildNodesCheckState(TreeNode node, bool isChecked)
        {
            if (node == null) return;

            foreach (TreeNode childNode in node.Nodes)
            {
                childNode.Checked = isChecked;
                SetChildNodesCheckState(childNode, isChecked);
            }
        }

        private void UpdateParentNodeCheckState(TreeNode node)
        {
            if (node == null || node.Parent == null) return;

            TreeNode parent = node.Parent;
            bool shouldCheckParent = true;
            bool hasCheckedChild = false;

            // 检查所有子节点
            foreach (TreeNode sibling in parent.Nodes)
            {
                if (sibling.Checked)
                {
                    hasCheckedChild = true;
                }
                else
                {
                    shouldCheckParent = false;
                }
            }

            // 仅当所有子节点都被勾选时，才勾选父节点
            parent.Checked = shouldCheckParent;

            // 继续向上更新父节点状态
            UpdateParentNodeCheckState(parent);
        }

        private async void DetectGeneratedCode_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txtProjectDir.Text) || !Directory.Exists(txtProjectDir.Text))
            {
                MessageBox.Show("请先选择有效的项目目录。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 禁用按钮，显示进度
            var detectButton = (Button)sender;
            detectButton.Enabled = false;
            txtStatus.Clear();
            txtStatus.AppendText("开始检测代码...\r\n");

            try
            {
                // 清除现有选中状态
                ClearAllCheckedNodes(treeViewExclude.Nodes);

                // 先勾选常见的生成目录
                var commonDirs = new[] { "obj", "bin", ".vs", "TempPE", "Driver", "Dirver", "SDK", "API" };
                foreach (var dir in commonDirs)
                {
                    LogToStatus($"正在查找目录: {dir}");
                    AutoCheckCommonDirectory(treeViewExclude.Nodes, dir);
                }

                // 使用CodeDetector检测需要排除的文件
                var filesToExclude = codeDetector.GetFilesToExclude(txtProjectDir.Text);
                foreach (var file in filesToExclude)
                {
                    string relativePath = Path.GetRelativePath(txtProjectDir.Text, file);
                    if (File.Exists(file))
                    {
                        LogToStatus($"检测到需要排除的文件：{relativePath}");
                        FindAndCheckNode(treeViewExclude.Nodes, file);
                    }
                }

                txtStatus.AppendText($"检测完成，找到 {filesToExclude.Count} 个需要排除的文件。\r\n");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"检测过程中发生错误：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                detectButton.Enabled = true;
            }
        }

        private void AutoCheckCommonDirectory(TreeNodeCollection nodes, string dirName)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.Text.Equals(dirName, StringComparison.OrdinalIgnoreCase))
                {
                    LogToStatus($"找到目录: {dirName}");

                    var parent = node.Parent;
                    while (parent != null)
                    {
                        parent.Expand();
                        parent = parent.Parent;
                    }

                    node.Checked = true;
                    SetChildNodesCheckState(node, true);
                    node.EnsureVisible();

                    LogToStatus($"已自动勾选目录及其子目录: {dirName}");
                }

                if (node.Nodes.Count > 0)
                {
                    AutoCheckCommonDirectory(node.Nodes, dirName);
                }
            }
        }

        private void ClearAllCheckedNodes(TreeNodeCollection nodes)
        {
            foreach (TreeNode node in nodes)
            {
                node.Checked = false;
                if (node.Nodes.Count > 0)
                {
                    ClearAllCheckedNodes(node.Nodes);
                }
            }
        }

        private void FindAndCheckNode(TreeNodeCollection nodes, string targetPath)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.Tag != null)
                {
                    string nodePath = node.Tag.ToString();

                    // 如果是文件节点且路径完全匹配
                    if (!Directory.Exists(nodePath))
                    {
                        if (string.Equals(nodePath, targetPath, StringComparison.OrdinalIgnoreCase))
                        {
                            if (treeViewExclude.InvokeRequired)
                            {
                                treeViewExclude.Invoke(new Action(() =>
                                {
                                    node.Checked = true;
                                    node.EnsureVisible();

                                    TreeNode parent = node.Parent;
                                    while (parent != null)
                                    {
                                        parent.Expand();
                                        parent = parent.Parent;
                                    }

                                    TreeViewExclude_AfterCheck(treeViewExclude, new TreeViewEventArgs(node, TreeViewAction.Unknown));
                                    treeViewExclude.Refresh();
                                }));
                            }
                            else
                            {
                                node.Checked = true;
                                node.EnsureVisible();

                                TreeNode parent = node.Parent;
                                while (parent != null)
                                {
                                    parent.Expand();
                                    parent = parent.Parent;
                                }

                                TreeViewExclude_AfterCheck(treeViewExclude, new TreeViewEventArgs(node, TreeViewAction.Unknown));
                                treeViewExclude.Refresh();
                            }
                            return;
                        }
                    }
                    else if (targetPath.StartsWith(nodePath, StringComparison.OrdinalIgnoreCase))
                    {
                        if (node.Nodes.Count > 0)
                        {
                            FindAndCheckNode(node.Nodes, targetPath);
                        }
                    }
                }
            }
        }

        private void LogToStatus(string message)
        {
            if (txtStatus.InvokeRequired)
            {
                txtStatus.Invoke(new Action(() => txtStatus.AppendText($"[调试] {message}\r\n")));
            }
            else
            {
                txtStatus.AppendText($"[调试] {message}\r\n");
            }
        }
    }
}