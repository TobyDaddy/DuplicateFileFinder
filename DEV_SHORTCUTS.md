# 开发快捷方式说明

如果工作区内 `.vscode/keybindings.json` 未生效，可能你的 VS Code 使用的是“用户级”快捷键文件（Preferences: Open Keyboard Shortcuts (JSON) 打开后为空表示未自定义）。

可以在用户级 `keybindings.json` 中加入：

```jsonc
[
  { "key": "f6", "command": "workbench.action.tasks.runTask", "args": "build-and-run" },
  { "key": "ctrl+shift+w", "command": "workbench.action.tasks.runTask", "args": "watch-run" }
]
```

或通过 GUI 搜索 `runTask` 添加。

> 若 F6 被其它扩展占用，可改用：`ctrl+f9` / `alt+f6` / `ctrl+shift+r`。

## 当前提供的 tasks
| 任务 | 作用 |
|------|------|
| build | Debug 编译（默认 Ctrl+Shift+B） |
| build-and-run | 编译并运行 Debug（推荐快捷键 F6） |
| run | 仅运行（依赖 build） |
| watch-run | watch 模式（保存自动重启） |
| build-release | Release 编译（供发布用） |

## Launch 配置
F5: .NET WPF Debug (F5)
调试无符号错误时使用 Release: 选择 .NET WPF Release (Ctrl+F5)

---
常见问题：
1. 快捷键不触发：焦点不相关 / 被系统占用 / 未加载工作区 keybindings。
2. 终端不出现：任务输出设置被隐藏，可在 tasks.json 中将 presentation.reveal 改为 always。
3. 路径不对：查看 launch.json program 字段是否包含 `win10-x64` 子目录。
