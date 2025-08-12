using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using ModernWpf.Controls;
using System.Globalization;
using System.Runtime.InteropServices; // + P/Invoke
using System.Windows.Interop;        // + WindowInteropHelper
using ModernWpf;

namespace DuplicateFileFinderWPF
{
    public partial class MainWindow : Window
    {
        private BackgroundWorker? backgroundWorker;
        private List<List<string>>? currentDuplicates;
        private int totalScannedFiles = 0;
        private string? _currentPreviewFile;
        
        // 图片缩放和拖拽相关字段
        private double zoomFactor = 1.0;
        private const double minZoom = 0.1;
        private const double maxZoom = 10.0;
        private const double zoomStep = 0.2;
        private bool isDragging = false;
        private System.Windows.Point lastMousePosition;
        private System.Windows.Point imageOffset = new System.Windows.Point(0, 0);
        private TransformGroup? imageTransform;
        private bool isUpdatingTransform = false;

        // 视频播放相关字段
        private System.Windows.Threading.DispatcherTimer? videoTimer;
        private bool isVideoPlaying = false;
        private bool isDraggingProgress = false;
        private bool isVideoEnded = false;

        // 图片旋转相关字段
        private double currentRotationAngle = 0;

        // 保持小图标句柄存活，避免被GC释放导致标题栏图标丢失
        private System.Drawing.Icon? _smallCaptionIcon;

        // Win32 设置窗口图标
        private const int WM_SETICON = 0x0080;
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        public MainWindow()
        {
            InitializeComponent();
            
            // 在InitializeComponent之后初始化语言设置
            InitializeLanguage();
            
            // 删除：this.Loaded += MainWindow_Loaded;  // XAML 已绑定 Loaded 事件
            
            InitializeBackgroundWorker();
            InitializeVideoTimer();
            UpdateDeleteButtonState();
            UpdatePreviewVisibility(); // 初始化预览区域可见性
            LoadInitialData();
        }

        private void InitializeLanguage()
        {
            try
            {
                // 暂时先使用中文作为默认语言
                var systemCulture = System.Globalization.CultureInfo.CurrentUICulture;
                string defaultLanguage = systemCulture.Name.StartsWith("zh") ? "zh-CN" : "en-US";
                
                LocalizationManager.Instance.ChangeLanguage(defaultLanguage);
            }
            catch (Exception ex)
            {
                // 如果语言初始化失败，使用默认英文，但不影响程序启动
                System.Diagnostics.Debug.WriteLine($"Language initialization failed: {ex.Message}");
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 设置程序图标（混合方案：任务栏用PNG，标题栏左上角用ICO小图标）
            LoadApplicationIcon();
            
            // 初始化语言菜单状态
            UpdateLanguageMenuItems();

            // 取消旧的主题菜单初始化（改为设置弹窗）
            // UpdateThemeMenuItems();
        }

        private void LoadApplicationIcon()
        {
            try
            {
                // 路径准备
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string icoPath = Path.Combine(baseDir, "Assets", "Icons", "app-icon.ico");
                string[] pngCandidates = new[]
                {
                    Path.Combine(baseDir, "Assets", "Icons", "app-icon-256.png"),
                    Path.Combine(baseDir, "Assets", "Icons", "appicon-1024.png"),
                    Path.Combine(baseDir, "Assets", "Icons", "Square310x310Logo.png"),
                };

                // 先尝试用 PNG 作为 Window.Icon（任务栏更饱满）
                string? pngPath = pngCandidates.FirstOrDefault(File.Exists);
                if (!string.IsNullOrEmpty(pngPath))
                {
                    try
                    {
                        var pngBitmap = new BitmapImage();
                        pngBitmap.BeginInit();
                        pngBitmap.UriSource = new Uri(pngPath, UriKind.Absolute);
                        pngBitmap.CacheOption = BitmapCacheOption.OnLoad;
                        pngBitmap.EndInit();
                        pngBitmap.Freeze();
                        this.Icon = pngBitmap; // 影响任务栏/Alt-Tab 大图标
                        Debug.WriteLine($"✓ 使用PNG作为任务栏图标: {pngPath}");
                        Console.WriteLine($"✓ 使用PNG作为任务栏图标: {pngPath}");
                    }
                    catch (Exception exPng)
                    {
                        Debug.WriteLine($"加载PNG失败: {exPng.Message}");
                        Console.WriteLine($"加载PNG失败: {exPng.Message}");
                    }
                }

                // 再用 ICO 仅设置标题栏左上角小图标（不设置 ICON_BIG，避免覆盖任务栏PNG）
                if (File.Exists(icoPath))
                {
                    TrySetSmallCaptionIconFromIco(icoPath);
                }
                else
                {
                    // 尝试备用路径
                    string altIco1 = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Icons", "app-icon.ico");
                    string altIco2 = Path.Combine("Assets", "Icons", "app-icon.ico");
                    string altIco3 = Path.Combine("Assets/Icons/app-icon.ico");
                    string? found = new[] { icoPath, altIco1, altIco2, altIco3 }.FirstOrDefault(File.Exists);
                    if (!string.IsNullOrEmpty(found))
                        TrySetSmallCaptionIconFromIco(found);
                }

                // 如果PNG也没成功，至少保证窗口和任务栏有ICO图标
                if (this.Icon == null && File.Exists(icoPath))
                {
                    try
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(icoPath, UriKind.Absolute);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        bitmap.Freeze();
                        this.Icon = bitmap;
                        Debug.WriteLine($"✓ 回退为ICO作为窗口图标: {icoPath}");
                    }
                    catch { /* 忽略 */ }
                }

                if (this.Icon == null)
                {
                    Debug.WriteLine("未找到任何图标资源，使用内置默认图标");
                    this.Icon = CreateProgramIcon();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载图标失败: {ex.Message}");
                Console.WriteLine($"加载图标失败: {ex.Message}");
                this.Icon = CreateProgramIcon();
            }
            finally
            {
                // 刷新
                this.InvalidateVisual();
                this.Dispatcher.BeginInvoke(new Action(() => this.UpdateLayout()), System.Windows.Threading.DispatcherPriority.Render);
            }
        }

        private void TrySetSmallCaptionIconFromIco(string icoPath)
        {
            try
            {
                var helper = new WindowInteropHelper(this);
                var hwnd = helper.Handle;
                if (hwnd == IntPtr.Zero) return; // 句柄尚未创建

                // 生成 16x16 小图标（若ICO包含更小帧，系统会按需缩放）；保留引用避免被GC回收
                using var fs = new FileStream(icoPath, FileMode.Open, FileAccess.Read);
                using var ico = new System.Drawing.Icon(fs);
                _smallCaptionIcon?.Dispose();
                _smallCaptionIcon = new System.Drawing.Icon(ico, new System.Drawing.Size(16, 16));

                // 只设置 ICON_SMALL，不触碰 ICON_BIG，确保任务栏仍使用PNG的“大图标”
                SendMessage(hwnd, WM_SETICON, new IntPtr(0), _smallCaptionIcon.Handle);
                Debug.WriteLine($"✓ 已设置标题栏小图标(ICO): {icoPath}");
                Console.WriteLine($"✓ 已设置标题栏小图标(ICO): {icoPath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"设置小图标失败: {ex.Message}");
                Console.WriteLine($"设置小图标失败: {ex.Message}");
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            // 释放我们创建的小图标
            try { _smallCaptionIcon?.Dispose(); } catch { }
            _smallCaptionIcon = null;
        }

        private ImageSource CreateProgramIcon()
        {
            // 创建文件夹样式的图标
            var drawingVisual = new DrawingVisual();
            using (var context = drawingVisual.RenderOpen())
            {
                // 文件夹背景
                var folderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xD7, 0x00)); // 黄色文件夹
                var folderPen = new System.Windows.Media.Pen(new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xCC, 0xAA, 0x00)), 1);
                
                // 文件夹主体
                context.DrawRectangle(folderBrush, folderPen, new Rect(4, 10, 24, 16));
                
                // 文件夹标签
                context.DrawRectangle(folderBrush, folderPen, new Rect(4, 8, 10, 4));
                
                // 添加小的重复符号表示重复文件
                var textBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x80, 0x60, 0x00));
                var typeface = new Typeface(new System.Windows.Media.FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
                var formattedText = new FormattedText("=", 
                    CultureInfo.CurrentCulture,
                    System.Windows.FlowDirection.LeftToRight, 
                    typeface, 10, textBrush, 1.0);
                context.DrawText(formattedText, new System.Windows.Point(14, 16));
            }
            
            var renderBitmap = new RenderTargetBitmap(32, 32, 96, 96, PixelFormats.Pbgra32);
            renderBitmap.Render(drawingVisual);
            return renderBitmap;
        }

        private void InitializeVideoTimer()
        {
            videoTimer = new System.Windows.Threading.DispatcherTimer();
            videoTimer.Interval = TimeSpan.FromMilliseconds(500);
            videoTimer.Tick += VideoTimer_Tick;
        }

        private void VideoTimer_Tick(object? sender, EventArgs e)
        {
            if (VideoPlayer.Source != null && !isDraggingProgress)
            {
                var current = VideoPlayer.Position;
                var total = VideoPlayer.NaturalDuration.HasTimeSpan ? VideoPlayer.NaturalDuration.TimeSpan : TimeSpan.Zero;
                VideoTimeText.Text = $"{current:mm\\:ss} / {total:mm\\:ss}";
                
                // 更新进度条
                if (total.TotalSeconds > 0)
                {
                    VideoProgressSlider.Value = (current.TotalSeconds / total.TotalSeconds) * 100;
                }
            }
        }

        private void LoadInitialData()
        {
            // 设置默认扫描路径为用户的Documents文件夹
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            FolderPathTextBox.Text = documentsPath;
            
            // 使用本地化的状态文本
            var localizer = LocalizationManager.Instance;
            StatusText.Text = localizer.Ready;
        }

        private void InitializeBackgroundWorker()
        {
            backgroundWorker = new BackgroundWorker
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };
            backgroundWorker.DoWork += BackgroundWorker_DoWork;
            backgroundWorker.ProgressChanged += BackgroundWorker_ProgressChanged;
            backgroundWorker.RunWorkerCompleted += BackgroundWorker_RunWorkerCompleted;
        }

        private void BackgroundWorker_DoWork(object? sender, DoWorkEventArgs e)
        {
            if (sender is BackgroundWorker worker)
            {
                string folderPath = e.Argument as string ?? "";
                e.Result = FileUtils.FindDuplicates(folderPath, worker);
            }
        }

        private void BackgroundWorker_ProgressChanged(object? sender, ProgressChangedEventArgs e)
        {
            ScanProgressBar.Value = e.ProgressPercentage;
        }

        private void BackgroundWorker_RunWorkerCompleted(object? sender, RunWorkerCompletedEventArgs e)
        {
            var localizer = LocalizationManager.Instance;
            
            ScanProgressBar.Value = 0;
            ScanProgressBar.Visibility = Visibility.Collapsed;
            
            // 恢复扫描按钮状态
            ScanButton.Content = "⚡";
            ScanButton.ToolTip = localizer.StartSearch;
            ScanButton.Style = (Style)FindResource("PrimaryButtonStyle");
            ScanButton.IsEnabled = true;

            if (e.Cancelled)
            {
                StatusText.Text = localizer.ScanCancelled;
                return;
            }

            if (e.Error != null)
            {
                StatusText.Text = $"{localizer.ScanError}: {e.Error.Message}";
                return;
            }

            if (e.Result != null)
            {
                var result = (Tuple<List<List<string>>, int>)e.Result;
                currentDuplicates = result.Item1;
                var totalFilesScanned = result.Item2;

                DisplayDuplicateFiles(currentDuplicates, totalFilesScanned);
                UpdateDeleteButtonState();
            }
        }

        private void DisplayDuplicateFiles(List<List<string>> duplicates, int totalFilesScanned)
        {
            var localizer = LocalizationManager.Instance;
            
            FileTreeView.Items.Clear();
            currentDuplicates = duplicates;
            totalScannedFiles = totalFilesScanned;

            if (duplicates != null && duplicates.Count > 0)
            {
                foreach (var fileList in duplicates)
                {
                    // 按路径深度排序，路径最深的排在前面
                    var sortedFileList = fileList.OrderByDescending(file => 
                        Path.GetDirectoryName(file)?.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length ?? 0)
                        .ToList();
                    
                    // 检查是否为零字节文件组
                    bool isZeroByteGroup = sortedFileList.All(file => 
                    {
                        try { return new FileInfo(file).Length == 0; }
                        catch { return false; }
                    });
                    
                    var rootItem = new TreeViewItem
                    {
                        IsExpanded = false,
                        Tag = "group",
                        Foreground = System.Windows.Media.Brushes.Black,
                        FontWeight = FontWeights.Normal,
                        FontSize = 14
                    };

                    // 创建自定义的Header Grid
                    var headerGrid = new Grid();
                    headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    // 重复组文本
                    var groupText = new TextBlock
                    {
                        Text = localizer.DuplicateGroupPrefix,
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                        VerticalAlignment = System.Windows.VerticalAlignment.Center,
                        FontWeight = FontWeights.Normal
                    };
                    Grid.SetColumn(groupText, 0);
                    headerGrid.Children.Add(groupText);

                    // 计数文本 [1 / 2] - 粗体显示
                    var countText = new TextBlock
                    {
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                        VerticalAlignment = System.Windows.VerticalAlignment.Center,
                        FontWeight = FontWeights.Bold,
                        Margin = new Thickness(0, 0, 5, 0) // 右边距
                    };
                    Grid.SetColumn(countText, 1);
                    headerGrid.Children.Add(countText);

                    // 文件名文本
                    var titleText = new TextBlock
                    {
                        Text = $"- {Path.GetFileName(sortedFileList[0])}",
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                        VerticalAlignment = System.Windows.VerticalAlignment.Center,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        FontWeight = FontWeights.Normal
                    };
                    Grid.SetColumn(titleText, 2);
                    headerGrid.Children.Add(titleText);

                    rootItem.Header = headerGrid;
                    
                    // 保存计数TextBlock的引用，用于后续更新
                    rootItem.Tag = new { Type = "group", CountText = countText, FileCount = sortedFileList.Count };

                    // 第一个文件作为保留文件（对于零字节文件组也标记删除）
                    var firstFileCheckBox = new System.Windows.Controls.CheckBox
                    {
                        Content = $"{Path.GetFileName(sortedFileList[0])} [{Path.GetDirectoryName(sortedFileList[0])}]",
                        IsChecked = isZeroByteGroup, // 零字节文件组全部标记删除
                        Tag = sortedFileList[0],
                        Style = (Style)FindResource("CustomCheckBoxStyle"),
                        Margin = new Thickness(5, 2, 5, 2)
                    };
                    firstFileCheckBox.Checked += FileCheckBox_CheckedChanged;
                    firstFileCheckBox.Unchecked += FileCheckBox_CheckedChanged;
                    firstFileCheckBox.PreviewMouseDown += CheckBox_PreviewMouseDown;

                    var firstFileItem = new TreeViewItem
                    {
                        Header = firstFileCheckBox,
                        Tag = sortedFileList[0]
                    };
                    rootItem.Items.Add(firstFileItem);

                    // 其余文件默认选中（表示标记删除）
                    for (int i = 1; i < sortedFileList.Count; i++)
                    {
                        var duplicateCheckBox = new System.Windows.Controls.CheckBox
                        {
                            Content = $"{Path.GetFileName(sortedFileList[i])} [{Path.GetDirectoryName(sortedFileList[i])}]",
                            IsChecked = true, // 选中表示标记删除
                            Tag = sortedFileList[i],
                            Style = (Style)FindResource("CustomCheckBoxStyle"),
                            Margin = new Thickness(5, 2, 5, 2)
                        };
                        duplicateCheckBox.Checked += FileCheckBox_CheckedChanged;
                        duplicateCheckBox.Unchecked += FileCheckBox_CheckedChanged;
                        duplicateCheckBox.PreviewMouseDown += CheckBox_PreviewMouseDown;

                        var duplicateItem = new TreeViewItem
                        {
                            Header = duplicateCheckBox,
                            Tag = sortedFileList[i]
                        };
                        rootItem.Items.Add(duplicateItem);
                    }

                    FileTreeView.Items.Add(rootItem);
                    
                    // 初始更新计数显示
                    UpdateGroupCount(rootItem);
                }

                int totalDuplicates = duplicates.Sum(fileList => fileList.Count - 1);
                int markedForDeletion = duplicates.Sum(fileList => fileList.Count - 1);
                long spaceToSave = GetSpaceToSave();
                string spaceText = FormatFileSize(spaceToSave);
                StatusText.Text = string.Format(localizer.FoundDuplicatesStatus, totalDuplicates, totalFilesScanned, markedForDeletion, spaceText);
            }
            else
            {
                StatusText.Text = string.Format(localizer.NoDuplicatesStatus, totalFilesScanned);
            }

            UpdatePreviewVisibility(); // 更新预览区域可见性
            UpdateDeleteButtonState();
        }

        private void FileCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.CheckBox checkBox)
            {
                // 更新所在组的计数显示
                TreeViewItem? parentGroup = FindParentGroup(checkBox);
                if (parentGroup != null)
                {
                    UpdateGroupCount(parentGroup);
                }
                
                // 更新状态统计
                UpdateStatusText();
                UpdateDeleteButtonState();
            }
        }

        private void CheckBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var localizer = LocalizationManager.Instance;
            
            // 检查是否可以切换状态（不能让一个组内所有文件都被标记删除）
            if (sender is System.Windows.Controls.CheckBox checkBox)
            {
                if (!CanToggleCheckBox(checkBox))
                {
                    e.Handled = true; // 阻止状态改变
                    System.Windows.MessageBox.Show(localizer.CannotMarkAllFiles, 
                        localizer.OperationRestriction, MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void UpdateStatusText()
        {
            if (currentDuplicates == null) return;

            int totalDuplicates = currentDuplicates.Sum(fileList => fileList.Count - 1);
            int markedForDeletion = GetMarkedForDeletionCount();
            long spaceToSave = GetSpaceToSave();
            int totalScanned = GetTotalScannedCount();
            
            string spaceText = FormatFileSize(spaceToSave);
            var localizer = LocalizationManager.Instance;
            StatusText.Text = string.Format(localizer.UpdateStatusFound, totalDuplicates, totalScanned, markedForDeletion, spaceText);
        }

        private int GetMarkedForDeletionCount()
        {
            int count = 0;
            foreach (TreeViewItem groupItem in FileTreeView.Items)
            {
                foreach (TreeViewItem fileItem in groupItem.Items)
                {
                    if (fileItem.Header is System.Windows.Controls.CheckBox checkBox && checkBox.IsChecked == true)
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        private long GetSpaceToSave()
        {
            long totalSpace = 0;
            foreach (TreeViewItem groupItem in FileTreeView.Items)
            {
                foreach (TreeViewItem fileItem in groupItem.Items)
                {
                    if (fileItem.Header is System.Windows.Controls.CheckBox checkBox && 
                        checkBox.IsChecked == true && 
                        fileItem.Tag is string filePath)
                    {
                        try
                        {
                            if (File.Exists(filePath))
                            {
                                var fileInfo = new FileInfo(filePath);
                                totalSpace += fileInfo.Length;
                            }
                        }
                        catch
                        {
                            // 忽略无法访问的文件
                        }
                    }
                }
            }
            return totalSpace;
        }

        private bool CanToggleCheckBox(System.Windows.Controls.CheckBox targetCheckBox)
        {
            // 检查是否为零字节文件，零字节文件允许被标记
            if (targetCheckBox.Tag is string filePath)
            {
                try
                {
                    var fileInfo = new FileInfo(filePath);
                    if (fileInfo.Length == 0)
                    {
                        return true; // 零字节文件允许被标记
                    }
                }
                catch
                {
                    // 文件信息获取失败，继续原逻辑
                }
            }
            
            // 如果当前是未选中状态，要变为选中，需要检查是否会导致组内所有文件都被标记删除
            if (targetCheckBox.IsChecked == false)
            {
                // 找到目标复选框所在的重复组
                TreeViewItem? parentGroup = FindParentGroup(targetCheckBox);
                if (parentGroup != null)
                {
                    int uncheckedCount = 0;
                    
                    // 统计组内未选中的文件数量
                    foreach (TreeViewItem fileItem in parentGroup.Items)
                    {
                        if (fileItem.Header is System.Windows.Controls.CheckBox checkBox)
                        {
                            if (checkBox == targetCheckBox)
                            {
                                // 这是目标复选框，假设它会被选中
                                continue;
                            }
                            else if (checkBox.IsChecked == false)
                            {
                                uncheckedCount++;
                            }
                        }
                    }
                    
                    // 如果选中目标复选框后，没有未选中的文件了，则不允许操作
                    return uncheckedCount > 0;
                }
            }
            
            // 其他情况（从选中变为未选中，或者找不到父组）都允许
            return true;
        }

        private TreeViewItem? FindParentGroup(System.Windows.Controls.CheckBox checkBox)
        {
            // 遍历所有重复组，找到包含这个复选框的组
            foreach (TreeViewItem groupItem in FileTreeView.Items)
            {
                foreach (TreeViewItem fileItem in groupItem.Items)
                {
                    if (fileItem.Header == checkBox)
                    {
                        return groupItem;
                    }
                }
            }
            return null;
        }

        private void UpdateGroupCount(TreeViewItem groupItem)
        {
            if (groupItem.Tag is { } tagObj)
            {
                var tagType = tagObj.GetType();
                if (tagType.GetProperty("Type")?.GetValue(tagObj)?.ToString() == "group")
                {
                    var countText = tagType.GetProperty("CountText")?.GetValue(tagObj) as TextBlock;
                    var fileCount = (int)(tagType.GetProperty("FileCount")?.GetValue(tagObj) ?? 0);
                    
                    if (countText != null)
                    {
                        // 统计选中的文件数量
                        int checkedCount = 0;
                        foreach (TreeViewItem fileItem in groupItem.Items)
                        {
                            if (fileItem.Header is System.Windows.Controls.CheckBox checkBox && checkBox.IsChecked == true)
                            {
                                checkedCount++;
                            }
                        }
                        
                        // 创建格式化的文本 [1 / 2]
                        var openBracketRun = new System.Windows.Documents.Run("[");
                        
                        var checkedRun = new System.Windows.Documents.Run(checkedCount.ToString())
                        {
                            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2C, 0x5A, 0xA0)) // 与扫描按钮相同的蓝色
                        };
                        
                        // 分隔符（正常颜色）
                        var separatorRun = new System.Windows.Documents.Run(" / ");
                        
                        // 总数量（正常颜色）
                        var totalRun = new System.Windows.Documents.Run(fileCount.ToString());
                        
                        var closeBracketRun = new System.Windows.Documents.Run("]");
                        
                        countText.Inlines.Clear();
                        countText.Inlines.Add(openBracketRun);
                        countText.Inlines.Add(checkedRun);
                        countText.Inlines.Add(separatorRun);
                        countText.Inlines.Add(totalRun);
                        countText.Inlines.Add(closeBracketRun);
                    }
                }
            }
        }

        private string GetTruncatedTitle(int fileCount, string fileName)
        {
            var localizer = LocalizationManager.Instance;
            string baseTitle = localizer.DuplicateGroupPrefix;
            const int maxTitleLength = 60; // 最大标题长度
            
            if (baseTitle.Length + fileName.Length <= maxTitleLength)
            {
                return baseTitle + fileName;
            }
            
            // 文件名太长，需要截断
            int availableLength = maxTitleLength - baseTitle.Length - 3; // 3个字符用于"..."
            if (availableLength <= 6) // 至少显示6个字符（前3个+后3个）
            {
                return baseTitle + fileName.Substring(0, Math.Min(fileName.Length, 6)) + "...";
            }
            
            int prefixLength = availableLength / 2;
            int suffixLength = availableLength - prefixLength;
            
            if (fileName.Length <= prefixLength + suffixLength)
            {
                return baseTitle + fileName;
            }
            
            string prefix = fileName.Substring(0, prefixLength);
            string suffix = fileName.Substring(fileName.Length - suffixLength);
            
            return baseTitle + prefix + "..." + suffix;
        }

        private void UpdatePreviewVisibility()
        {
            bool hasSelectedFile = FileTreeView.SelectedItem != null;
            bool hasPreviewContent = PreviewImage.Visibility == Visibility.Visible || 
                                   VideoPreviewGrid.Visibility == Visibility.Visible;
            
            if (FileTreeView.Items.Count == 0 || (!hasSelectedFile && !hasPreviewContent))
            {
                // 没有文件或（没有选中文件且没有预览内容）时，隐藏预览区域并扩展文件列表
                PreviewColumn.Width = new GridLength(0);
                SplitterColumn.Width = new GridLength(0);
                PreviewBorder.Visibility = Visibility.Collapsed;
            }
            else
            {
                // 有选中文件或有预览内容时，显示预览区域
                PreviewColumn.Width = new GridLength(2, GridUnitType.Star);
                SplitterColumn.Width = new GridLength(8);
                PreviewBorder.Visibility = Visibility.Visible;
            }
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            
            if (order == 0)
                return $"{len:0} {sizes[order]}";
            else
                return $"{len:0.##} {sizes[order]}";
        }

        private int GetTotalScannedCount()
        {
            return totalScannedFiles;
        }

        #region 标题栏按钮事件
        private async void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var localizer = LocalizationManager.Instance;

            var dialog = new ContentDialog
            {
                Title = localizer.Settings,
                CloseButtonText = localizer.OK
            };

            // 构建设置面板（包含主题切换）
            var panel = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Vertical, Margin = new Thickness(8) };
            panel.Children.Add(new System.Windows.Controls.TextBlock { Text = "Theme", Margin = new Thickness(0,0,0,8), Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush") });

            var themeRow = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
            var btnSystem = new System.Windows.Controls.Button { Content = "System", Margin = new Thickness(0,0,8,0), Style = (Style)FindResource("SecondaryButtonStyle") };
            var btnLight  = new System.Windows.Controls.Button { Content = "Light",  Margin = new Thickness(0,0,8,0), Style = (Style)FindResource("SecondaryButtonStyle") };
            var btnDark   = new System.Windows.Controls.Button { Content = "Dark",   Style = (Style)FindResource("SecondaryButtonStyle") };

            btnSystem.Click += (s, _)=>
            {
                ThemeManager.Current.ApplicationTheme = null; // 跟随系统
                DuplicateFileFinderWPF.Properties.Settings.Default.Theme = string.Empty;
                DuplicateFileFinderWPF.Properties.Settings.Default.Save();
            };
            btnLight.Click += (s, _)=>
            {
                ThemeManager.Current.ApplicationTheme = ApplicationTheme.Light;
                DuplicateFileFinderWPF.Properties.Settings.Default.Theme = "Light";
                DuplicateFileFinderWPF.Properties.Settings.Default.Save();
            };
            btnDark.Click += (s, _)=>
            {
                ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark;
                DuplicateFileFinderWPF.Properties.Settings.Default.Theme = "Dark";
                DuplicateFileFinderWPF.Properties.Settings.Default.Save();
            };

            themeRow.Children.Add(btnSystem);
            themeRow.Children.Add(btnLight);
            themeRow.Children.Add(btnDark);

            panel.Children.Add(themeRow);
            dialog.Content = panel;

            await dialog.ShowAsync();
        }

        private void LanguageButton_Click(object sender, RoutedEventArgs e)
        {
            // 显示下拉菜单
            if (sender is System.Windows.Controls.Button button && button.ContextMenu != null)
            {
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                button.ContextMenu.IsOpen = true;
            }
        }

        private void ChineseMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ChangeLanguage("zh-CN");
            UpdateLanguageMenuItems();
        }

        private void EnglishMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ChangeLanguage("en-US");
            UpdateLanguageMenuItems();
        }

        private void UpdateLanguageMenuItems()
        {
            var currentCulture = System.Threading.Thread.CurrentThread.CurrentUICulture.Name;
            
            if (currentCulture == "zh-CN")
            {
                ChineseMenuItem.Header = "中文 ✓";
                EnglishMenuItem.Header = "English";
            }
            else
            {
                ChineseMenuItem.Header = "中文";
                EnglishMenuItem.Header = "English ✓";
            }
        }

        private void ChangeLanguage(string cultureCode)
        {
            try
            {
                LocalizationManager.Instance.ChangeLanguage(cultureCode);
                
                // 更新UI - 由于绑定会自动更新大部分元素，这里处理一些特殊情况
                UpdateUIAfterLanguageChange();
            }
            catch (Exception ex)
            {
                var dialog = new ContentDialog
                {
                    Title = "Error",
                    Content = $"Language change failed: {ex.Message}",
                    CloseButtonText = "OK"
                };
                _ = dialog.ShowAsync();
            }
        }

        private void UpdateUIAfterLanguageChange()
        {
            var localizer = LocalizationManager.Instance;
            
            // 更新扫描按钮的ToolTip
            if (backgroundWorker?.IsBusy == true)
            {
                ScanButton.ToolTip = localizer.CancelScan;
            }
            else
            {
                ScanButton.ToolTip = localizer.StartSearch;
            }
            
            // 更新现有重复组的标题文本
            UpdateExistingDuplicateGroupTitles();
            
            // 更新文件属性区域的标签文本
            UpdateFilePropertiesLabels();
            
            // 更新状态栏中的计数文本等动态内容
            UpdateStatusText();
            
            // 可以在这里添加其他需要语言切换后更新的UI元素
        }

        private void UpdateExistingDuplicateGroupTitles()
        {
            var localizer = LocalizationManager.Instance;
            
            // 遍历TreeView中的所有项目，更新重复组的标题
            foreach (TreeViewItem rootItem in FileTreeView.Items)
            {
                if (rootItem.Header is Grid headerGrid)
                {
                    // 找到重复组文本的TextBlock
                    foreach (var child in headerGrid.Children)
                    {
                        if (child is TextBlock textBlock && textBlock.FontWeight == FontWeights.Normal)
                        {
                            textBlock.Text = localizer.DuplicateGroupPrefix;
                            break;
                        }
                    }
                }
            }
        }

        private void UpdateFileProperties(string filePath)
        {
            var localizer = LocalizationManager.Instance;
            try
            {
                var fileInfo = new FileInfo(filePath);
                
                CreationDateText.Text = fileInfo.CreationTime.ToString("yyyy-MM-dd HH:mm:ss");
                ModificationDateText.Text = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");
                FileSizeText.Text = FormatFileSize(fileInfo.Length);
                FileLocationText.Text = Path.GetDirectoryName(filePath) ?? "";
                
                // 保存当前文件路径用于打开位置功能
                OpenLocationButton.Tag = filePath;
            }
            catch
            {
                CreationDateText.Text = localizer.GetInfoFailed;
                ModificationDateText.Text = localizer.GetInfoFailed;
                FileSizeText.Text = localizer.GetInfoFailed;
                FileLocationText.Text = localizer.GetInfoFailed;
            }
        }

        private void UpdateFilePropertiesLabels()
        {
            // 此方法现在可以为空，因为绑定是自动的
        }

        private async void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            var localizer = LocalizationManager.Instance;
            var dialog = new ContentDialog
            {
                Title = localizer.HelpDialogTitle,
                Content = localizer.HelpContent,
                CloseButtonText = localizer.OK
            };
            await dialog.ShowAsync();
        }
        
        #region 窗口控制按钮事件
        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
                MaximizeButton.Content = "\uE922"; // 最大化图标
            }
            else
            {
                WindowState = WindowState.Maximized;
                MaximizeButton.Content = "\uE923"; // 还原图标
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        #endregion
        #endregion

        #region 主要功能
        private void FolderPathTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // 按回车键时触发扫描
                ScanButton_Click(ScanButton, new RoutedEventArgs());
                e.Handled = true;
            }
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var localizer = LocalizationManager.Instance;
            var dialog = new System.Windows.Forms.FolderBrowserDialog()
            {
                Description = localizer.SelectFolderDescription,
                UseDescriptionForTitle = true
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                FolderPathTextBox.Text = dialog.SelectedPath;
            }
        }

        private void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            var localizer = LocalizationManager.Instance;
            
            // 如果当前正在扫描，则取消扫描
            if (backgroundWorker?.IsBusy == true)
            {
                backgroundWorker.CancelAsync();
                ScanButton.Content = "⏹️";
                ScanButton.ToolTip = localizer.CancellingScan;
                ScanButton.IsEnabled = false;
                StatusText.Text = localizer.CancellingScanStatus;
                return;
            }

            // 开始扫描
            string folderPath = FolderPathTextBox.Text;
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
            {
                System.Windows.MessageBox.Show("请选择一个有效的文件夹路径", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 清空之前的结果
            FileTreeView.Items.Clear();
            
            // 切换按钮状态为取消模式
            ScanButton.Content = "⏹️";
            ScanButton.ToolTip = localizer.CancelScan;
            ScanButton.Style = (Style)FindResource("WarningButtonStyle");
            
            ScanProgressBar.Value = 0;
            ScanProgressBar.Visibility = Visibility.Visible;
            StatusText.Text = localizer.ScanningStatus;

            backgroundWorker?.RunWorkerAsync(folderPath);
        }

        private void DeleteMarkedButton_Click(object sender, RoutedEventArgs e)
        {
            var result = System.Windows.MessageBox.Show("确认删除所有标记的重复文件吗？此操作不可撤销！", 
                "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                
            if (result == MessageBoxResult.Yes)
            {
                DeleteMarkedFiles();
                RemoveEmptyGroups();
                UpdateDeleteButtonState();
                UpdateStatusAfterDeletion();
            }
        }

        private void DeleteMarkedFiles()
        {
            var itemsToRemove = new List<TreeViewItem>();

            foreach (TreeViewItem groupItem in FileTreeView.Items)
            {
                var filesToRemove = new List<TreeViewItem>();
                
                foreach (TreeViewItem fileItem in groupItem.Items)
                {
                    if (fileItem.Header is System.Windows.Controls.CheckBox checkBox && checkBox.IsChecked == true && fileItem.Tag is string filePath)
                    {
                        try
                        {
                            if (File.Exists(filePath))
                            {
                                File.Delete(filePath);
                                filesToRemove.Add(fileItem);
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Windows.MessageBox.Show($"删除文件失败: {filePath}\n错误: {ex.Message}", 
                                "删除错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }

                foreach (var item in filesToRemove)
                {
                    groupItem.Items.Remove(item);
                }
            }
        }

        private void RemoveEmptyGroups()
        {
            var groupsToRemove = new List<TreeViewItem>();

            foreach (TreeViewItem groupItem in FileTreeView.Items)
            {
                if (groupItem.Items.Count <= 1)
                {
                    groupsToRemove.Add(groupItem);
                }
            }

            foreach (var group in groupsToRemove)
            {
                FileTreeView.Items.Remove(group);
            }
        }

        private void UpdateDeleteButtonState()
        {
            DeleteMarkedButton.IsEnabled = HasMarkedFiles();
        }

        private bool HasMarkedFiles()
        {
            foreach (TreeViewItem groupItem in FileTreeView.Items)
            {
                foreach (TreeViewItem fileItem in groupItem.Items)
                {
                    if (fileItem.Header is System.Windows.Controls.CheckBox checkBox && checkBox.IsChecked == true)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private void UpdateStatusAfterDeletion()
        {
            var localizer = LocalizationManager.Instance;
            if (FileTreeView.Items.Count == 0)
            {
                StatusText.Text = localizer.AllDuplicatesDeleted;
            }
            else
            {
                int remainingGroups = FileTreeView.Items.Count;
                int markedForDeletion = GetMarkedForDeletionCount();
                
                StatusText.Text = string.Format(localizer.RemainingGroupsStatus, remainingGroups, markedForDeletion);
            }
        }

        private void FileTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            UpdatePreviewVisibility(); // 更新预览区域可见性
            
            if (e.NewValue is TreeViewItem item)
            {
                // 如果是叶节点（文件），直接预览
                if (item.Tag is string filePath && File.Exists(filePath))
                {
                    PreviewFile(filePath);
                }
                // 如果是根节点（重复组），预览第一个文件
                else if (item.Tag != null && item.Tag.GetType().GetProperty("Type")?.GetValue(item.Tag)?.ToString() == "group" && item.Items.Count > 0)
                {
                    // 找到第一个文件进行预览
                    foreach (TreeViewItem childItem in item.Items)
                    {
                        if (childItem.Tag is string childFilePath && File.Exists(childFilePath))
                        {
                            PreviewFile(childFilePath);
                            break;
                        }
                    }
                }
            }
        }

        private void PreviewFile(string filePath)
        {
            var localizer = LocalizationManager.Instance;
            _currentPreviewFile = filePath;
            var extension = Path.GetExtension(filePath).ToLower();

            // 更新文件属性信息
            UpdateFileProperties(filePath);

            // 停止当前视频播放
            if (VideoPlayer.Source != null)
            {
                VideoPlayer.Stop();
                videoTimer?.Stop();
                isVideoPlaying = false;
            }

            // 隐藏所有预览控件
            PreviewImage.Visibility = Visibility.Collapsed;
            VideoPreviewGrid.Visibility = Visibility.Collapsed;
            NoPreviewText.Visibility = Visibility.Collapsed;

            if (IsImageFile(extension))
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(filePath);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();

                    PreviewImage.Source = bitmap;
                    PreviewImage.Visibility = Visibility.Visible;
                    
                    // 显示旋转按钮
                    RotateButton.Visibility = Visibility.Visible;
                    
                    // 重置缩放参数和旋转角度
                    ResetZoom();
                    currentRotationAngle = 0;
                    // 设置事件处理
                    SetupImageEvents();
                    
                    StatusText.Text = string.Format(localizer.PreviewWithZoomAndDrag, Path.GetFileName(filePath));
                    
                    StatusText.Text = string.Format(localizer.PreviewWithZoomAndDrag, Path.GetFileName(filePath));
                }
                catch (Exception ex)
                {
                    ShowNoPreview(string.Format(localizer.CannotLoadImage, ex.Message));
                }
            }
            else if (IsVideoFile(extension))
            {
                try
                {
                    // 停止之前的视频
                    VideoPlayer.Stop();
                    videoTimer?.Stop();
                    
                    // 隐藏旋转按钮（视频不需要旋转功能）
                    RotateButton.Visibility = Visibility.Collapsed;
                    
                    VideoPlayer.Source = new Uri(filePath);
                    VideoPreviewGrid.Visibility = Visibility.Visible;
                    isVideoEnded = false;
                    VideoProgressSlider.Value = 0;
                    VideoTimeText.Text = "00:00 / 00:00";
                    StatusText.Text = string.Format(localizer.VideoLoading, Path.GetFileName(filePath));
                    
                    // 改为不自动播放，等待用户手动点击播放
                    PlayPauseButton.Content = "▶️";
                    isVideoPlaying = false; // 初始状态为暂停
                }
                catch (Exception ex)
                {
                    ShowNoPreview(string.Format(localizer.CannotLoadVideo, ex.Message));
                }
            }
            else
            {
                ShowNoPreview(localizer.UnsupportedFileFormat);
            }
        }

        private void ShowNoPreview(string? message = null)
        {
            var localizer = LocalizationManager.Instance;
            // 隐藏旋转按钮
            RotateButton.Visibility = Visibility.Collapsed;
            
            NoPreviewText.Text = message ?? localizer.SelectFileToPreview;
            NoPreviewText.Visibility = Visibility.Visible;
        }

        #region 视频播放控制
        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (VideoPlayer.Source == null) return;

            try
            {
                if (isVideoPlaying)
                {
                    VideoPlayer.Pause();
                    PlayPauseButton.Content = "▶️";
                    videoTimer?.Stop();
                    isVideoPlaying = false;
                }
                else
                {
                    // 如果视频已播放到结尾，从头开始播放
                    if (isVideoEnded)
                    {
                        VideoPlayer.Position = TimeSpan.Zero;
                        isVideoEnded = false;
                    }
                    
                    VideoPlayer.Play();
                    PlayPauseButton.Content = "⏸️";
                    videoTimer?.Start();
                    isVideoPlaying = true;
                }
            }
            catch (Exception ex)
            {
                var localizer = LocalizationManager.Instance;
                StatusText.Text = string.Format(localizer.VideoPlayError, ex.Message);
            }
        }

        private async void StopButton_Click(object sender, RoutedEventArgs e)
        {
            if (VideoPlayer.Source == null) return;

            try
            {
                // 重新加载视频到第一帧并暂停（不是停止，这样画面不会变黑）
                var currentSource = VideoPlayer.Source;
                VideoPlayer.Source = null;
                VideoPlayer.Source = currentSource;
                VideoPlayer.Position = TimeSpan.Zero;
                
                // 加载第一帧但不播放
                VideoPlayer.Play();
                await Task.Delay(100); // 给一点时间加载第一帧
                VideoPlayer.Pause();
                
                PlayPauseButton.Content = "▶️";
                videoTimer?.Stop();
                isVideoPlaying = false;
                isVideoEnded = false;
                VideoProgressSlider.Value = 0;
                VideoTimeText.Text = "00:00 / 00:00";
            }
            catch (Exception ex)
            {
                var localizer = LocalizationManager.Instance;
                StatusText.Text = string.Format(localizer.VideoStopError, ex.Message);
            }
        }

        private void VideoPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            PlayPauseButton.Content = "▶️";
            videoTimer?.Stop();
            isVideoPlaying = false;
            isVideoEnded = true;
            VideoProgressSlider.Value = 100; // 设置进度条到100%
        }

        private void VideoPlayer_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            var localizer = LocalizationManager.Instance;
            StatusText.Text = string.Format(localizer.VideoPlaybackFailed, e.ErrorException?.Message);
            PlayPauseButton.Content = "▶️";
            videoTimer?.Stop();
            isVideoPlaying = false;
        }

        private async void VideoPlayer_MediaOpened(object sender, RoutedEventArgs e)
        {
            try
            {
                // 视频已完全加载，显示第一帧但不播放
                var localizer = LocalizationManager.Instance;
                StatusText.Text = string.Format(localizer.VideoClickToPlay, Path.GetFileName(_currentPreviewFile ?? ""));
                
                // 播放一小段时间然后暂停，确保显示第一帧
                VideoPlayer.Play();
                await Task.Delay(100);
                VideoPlayer.Pause();
                
                // 确保按钮状态正确
                PlayPauseButton.Content = "▶️";
                isVideoPlaying = false;
                
                // 更新时间显示
                if (VideoPlayer.NaturalDuration.HasTimeSpan)
                {
                    var total = VideoPlayer.NaturalDuration.TimeSpan;
                    VideoTimeText.Text = $"00:00 / {total:mm\\:ss}";
                }
            }
            catch (Exception ex)
            {
                var localizer = LocalizationManager.Instance;
                StatusText.Text = string.Format(localizer.VideoInitError, ex.Message);
            }
        }

        private void VideoPlayer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 点击视频画面切换播放/暂停状态
            PlayPauseButton_Click(PlayPauseButton, new RoutedEventArgs());
            e.Handled = true;
        }

        private void VideoProgressSlider_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            isDraggingProgress = true;
        }

        private void VideoProgressSlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            isDraggingProgress = false;
            // ValueChanged已经实时更新了位置，这里只需要重置拖拽状态
        }

        private async void VideoProgressSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // 在用户拖拽时实时更新位置和画面
            if (isDraggingProgress && VideoPlayer.Source != null && VideoPlayer.NaturalDuration.HasTimeSpan)
            {
                var total = VideoPlayer.NaturalDuration.TimeSpan;
                var newPosition = TimeSpan.FromSeconds((e.NewValue / 100.0) * total.TotalSeconds);
                
                // 实时更新画面位置
                VideoPlayer.Position = newPosition;
                isVideoEnded = false;
                
                // 给一点时间让画面更新
                await Task.Delay(50);
                
                // 如果当前不在播放状态，强制刷新一帧
                if (!isVideoPlaying)
                {
                    VideoPlayer.Play();
                    await Task.Delay(50);
                    VideoPlayer.Pause();
                }
                
                // 更新时间显示
                VideoTimeText.Text = $"{newPosition:mm\\:ss} / {total:mm\\:ss}";
            }
        }

        private async void VideoProgressArea_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (VideoPlayer.Source == null || !VideoPlayer.NaturalDuration.HasTimeSpan) return;

            try
            {
                // 获取点击位置相对于进度条区域的比例
                var border = sender as Border;
                if (border == null) return;

                var position = e.GetPosition(border);
                var percentage = position.X / border.ActualWidth;
                
                // 确保在有效范围内
                percentage = Math.Max(0, Math.Min(1, percentage));
                
                // 计算新的播放位置
                var total = VideoPlayer.NaturalDuration.TimeSpan;
                var newPosition = TimeSpan.FromSeconds(percentage * total.TotalSeconds);
                
                // 记住当前播放状态
                var wasPlaying = isVideoPlaying;
                
                // 立即设置新位置
                VideoPlayer.Position = newPosition;
                VideoProgressSlider.Value = percentage * 100;
                isVideoEnded = false;
                
                // 强制刷新画面到新位置
                if (!wasPlaying)
                {
                    // 如果之前是暂停状态，播放一小段时间然后暂停来刷新画面
                    VideoPlayer.Play();
                    await Task.Delay(100);
                    VideoPlayer.Pause();
                }
                
                // 更新时间显示
                VideoTimeText.Text = $"{newPosition:mm\\:ss} / {total:mm\\:ss}";
                
                // 恢复播放状态
                if (wasPlaying)
                {
                    VideoPlayer.Play();
                    PlayPauseButton.Content = "⏸️";
                    videoTimer?.Start();
                    isVideoPlaying = true;
                }
                else
                {
                    PlayPauseButton.Content = "▶️";
                    videoTimer?.Stop();
                    isVideoPlaying = false;
                }
                
                // 标记为已处理，避免默认行为
                e.Handled = true;
            }
            catch (Exception ex)
            {
                var localizer = LocalizationManager.Instance;
                StatusText.Text = string.Format(localizer.ProgressSeekError, ex.Message);
            }
        }
        #endregion

        private void UpdateStatusAfterMarkingChange()
        {
            var localizer = LocalizationManager.Instance;
            int totalGroups = FileTreeView.Items.Count;
            int markedForDeletion = GetMarkedForDeletionCount();
            
            StatusText.Text = string.Format(localizer.TotalGroupsStatus, totalGroups, markedForDeletion);
        }

        private bool IsImageFile(string extension)
        {
            return extension == ".jpg" || extension == ".jpeg" ||
                   extension == ".jfif" || extension == ".png" ||
                   extension == ".gif" || extension == ".bmp";
        }

        private bool IsVideoFile(string extension)
        {
            return extension == ".mp4" || extension == ".avi" ||
                   extension == ".wmv" || extension == ".mov" ||
                   extension == ".mkv";
        }

        private async Task ShowErrorDialog(string message)
        {
            var dialog = new ContentDialog
            {
                Title = "错误",
                Content = message,
                CloseButtonText = "确定"
            };
            await dialog.ShowAsync();
        }

        #region 图片缩放和拖拽功能
        private void ResetZoom()
        {
            zoomFactor = 1.0;
            imageOffset = new System.Windows.Point(0, 0);
            isDragging = false;
            ApplyImageTransform();
        }

        private void SetupImageEvents()
        {
            // 移除之前的事件处理
            PreviewImage.MouseWheel -= PreviewImage_MouseWheel;
            PreviewImage.MouseDown -= PreviewImage_MouseDown;
            PreviewImage.MouseMove -= PreviewImage_MouseMove;
            PreviewImage.MouseUp -= PreviewImage_MouseUp;
            PreviewImage.MouseLeave -= PreviewImage_MouseLeave;
            ImagePreviewBorder.MouseWheel -= PreviewImage_MouseWheel;

            // 添加事件处理 - 将MouseWheel绑定到容器，其他事件仍绑定到图片
            ImagePreviewBorder.MouseWheel += PreviewImage_MouseWheel;
            PreviewImage.MouseDown += PreviewImage_MouseDown;
            PreviewImage.MouseMove += PreviewImage_MouseMove;
            PreviewImage.MouseUp += PreviewImage_MouseUp;
            PreviewImage.MouseLeave += PreviewImage_MouseLeave;
        }

        private void ApplyImageTransform()
        {
            if (PreviewImage.Source == null || isUpdatingTransform) return;
            
            isUpdatingTransform = true;
            
            try
            {
                // 创建变换组
                imageTransform = new TransformGroup();
                
                // 先旋转
                if (currentRotationAngle != 0)
                {
                    var rotateTransform = new RotateTransform(currentRotationAngle);
                    imageTransform.Children.Add(rotateTransform);
                }
                
                // 然后缩放
                var scaleTransform = new ScaleTransform(zoomFactor, zoomFactor);
                imageTransform.Children.Add(scaleTransform);
                
                // 最后平移
                var translateTransform = new TranslateTransform(imageOffset.X, imageOffset.Y);
                imageTransform.Children.Add(translateTransform);
                
                // 应用变换
                PreviewImage.RenderTransform = imageTransform;
            }
            finally
            {
                isUpdatingTransform = false;
            }
        }

        private void PreviewImage_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (PreviewImage.Source == null) return;

            // 获取鼠标相对于容器的位置
            var mousePos = e.GetPosition(ImagePreviewBorder);
            
            // 保存缩放前鼠标在图片坐标系中的位置
            var oldZoom = zoomFactor;
            
            // 计算新的缩放值
            if (e.Delta > 0)
                zoomFactor *= (1.0 + zoomStep);
            else
                zoomFactor /= (1.0 + zoomStep);

            // 限制缩放范围
            zoomFactor = Math.Max(minZoom, Math.Min(maxZoom, zoomFactor));

            if (Math.Abs(zoomFactor - oldZoom) > 0.001)
            {
                // 计算缩放比例变化
                var scaleChange = zoomFactor / oldZoom;
                
                // 计算容器中心
                var containerCenterX = ImagePreviewBorder.ActualWidth / 2;
                var containerCenterY = ImagePreviewBorder.ActualHeight / 2;
                
                // 计算鼠标相对于容器中心的偏移
                var mouseOffsetX = mousePos.X - containerCenterX;
                var mouseOffsetY = mousePos.Y - containerCenterY;
                
                // 更新偏移量以实现以鼠标为中心的缩放
                imageOffset.X = imageOffset.X * scaleChange + mouseOffsetX * (1 - scaleChange);
                imageOffset.Y = imageOffset.Y * scaleChange + mouseOffsetY * (1 - scaleChange);
                
                ApplyImageTransform();

                // 更新状态显示
                var localizer = LocalizationManager.Instance;
                StatusText.Text = string.Format(localizer.PreviewWithZoom, Path.GetFileName(_currentPreviewFile), zoomFactor.ToString("P0"));
            }

            e.Handled = true;
        }

        private void PreviewImage_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (PreviewImage.Source == null || e.LeftButton != MouseButtonState.Pressed) return;

            isDragging = true;
            lastMousePosition = e.GetPosition(ImagePreviewBorder); // 使用容器坐标系
            PreviewImage.CaptureMouse();
            PreviewImage.Cursor = System.Windows.Input.Cursors.SizeAll;
            e.Handled = true;
        }

        private void PreviewImage_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!isDragging || PreviewImage.Source == null) return;

            var currentPos = e.GetPosition(ImagePreviewBorder); // 使用容器坐标系
            var deltaX = currentPos.X - lastMousePosition.X;
            var deltaY = currentPos.Y - lastMousePosition.Y;

            // 更新偏移量
            imageOffset.X += deltaX;
            imageOffset.Y += deltaY;
            lastMousePosition = currentPos;

            // 直接更新变换，避免重新创建
            if (imageTransform != null && imageTransform.Children.Count >= 2)
            {
                var translateTransform = imageTransform.Children[1] as TranslateTransform;
                if (translateTransform != null)
                {
                    translateTransform.X = imageOffset.X;
                    translateTransform.Y = imageOffset.Y;
                }
            }
            else
            {
                ApplyImageTransform();
            }
            
            e.Handled = true;
        }

        private void PreviewImage_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (isDragging)
            {
                isDragging = false;
                PreviewImage.ReleaseMouseCapture();
                PreviewImage.Cursor = System.Windows.Input.Cursors.Hand;
                e.Handled = true;
            }
        }

        private void PreviewImage_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (isDragging)
            {
                isDragging = false;
                PreviewImage.ReleaseMouseCapture();
                PreviewImage.Cursor = System.Windows.Input.Cursors.Arrow;
            }
        }
        
        private void OpenLocationButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.Tag is string filePath && File.Exists(filePath))
            {
                try
                {
                    // 在资源管理器中选中文件
                    Process.Start("explorer.exe", $"/select,\"{filePath}\"");
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"无法打开文件位置: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void RotateButton_Click(object sender, RoutedEventArgs e)
        {
            if (PreviewImage.Visibility == Visibility.Visible)
            {
                // 向右旋转90度
                currentRotationAngle = (currentRotationAngle + 90) % 360;
                
                // 应用旋转变换
                ApplyImageTransform();
            }
        }
        #endregion
        #endregion

        private void UpdateThemeMenuItems()
        {
            // Legacy theme menu removed; no-op
        }
        
        // 删除 ThemeSystemMenuItem_Click / ThemeLightMenuItem_Click / ThemeDarkMenuItem_Click
        // 主题切换逻辑已在 SettingsButton_Click 中实现
    }
}

