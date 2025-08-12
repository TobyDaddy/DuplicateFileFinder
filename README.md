# DuplicateFileFinder

一个基于 .NET 6 的 Windows WPF 应用，用于扫描并管理重复文件，支持图片/视频预览与多语言界面。

## 特性
- 快速扫描重复文件，按组展示，支持一键批量标记/删除
- 图片缩放/拖拽/旋转预览，视频预览与进度拖拽
- 简洁现代的 UI（ModernWpfUI）
- 多语言（简体中文/英文）
- 应用图标显示优化：任务栏使用高分辨率 PNG，标题栏左上角使用 ICO 小图标

## 环境要求
- Windows 10/11
- .NET 6 SDK

## 构建与运行
```powershell
# 在仓库根目录
cd .\DuplicateFileFinder

# 构建
 dotnet build .\DuplicateFileFinderWPF.csproj -c Release

# 运行（调试构建）
 dotnet build .\DuplicateFileFinderWPF.csproj
 .\bin\Debug\net6.0-windows\DuplicateFileFinderWPF.exe
```

## 目录结构
```
DuplicateFileFinder/
  App.xaml(.cs)
  MainWindow.xaml(.cs)
  FileUtils.cs
  LocalizationManager.cs
  Resources/Strings*.resx
  Assets/Icons/*
  DuplicateFileFinderWPF.csproj
```

## 本地化
- 默认根据系统语言选择 zh-CN 或 en-US
- 文案位于 `DuplicateFileFinder/Resources/Strings*.resx`

## 应用图标策略
- 任务栏/Alt-Tab：高分辨率 PNG（`Assets/Icons/app-icon-256.png` 等）
- 标题栏左上角：使用 `WM_SETICON(ICON_SMALL)` 设置的 ICO 帧（`Assets/Icons/app-icon.ico`）

## 许可证
本项目使用 MIT 许可证，见 `LICENSE`。

## 贡献
欢迎 PR / Issue。
