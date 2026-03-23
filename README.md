# PasteTool

[中文](#中文说明) | [English](#english)

## 中文说明

### 项目简介

PasteTool 是一个 Windows 剪贴板历史工具。

它常驻系统托盘，持续监听剪贴板变化，并在你按下全局快捷键时弹出历史窗口，支持搜索、预览和一键粘贴。项目基于 .NET / WPF 开发，面向日常文本、富文本和图片复制场景。

### 功能特性

- 剪贴板历史记录：自动记录文本、富文本（RTF/HTML）和图片。
- 全局快捷键呼出：默认 `Ctrl+Shift+V`。
- 快速搜索：支持 SQLite FTS5 搜索，并对连续中文查询提供子串匹配。
- 内容预览：支持文本、RTF、HTML 表格文本和图片预览。
- 一键粘贴：选中记录后自动切回目标窗口执行粘贴。
- 去重与裁剪：对重复内容去重，并按设置自动清理旧记录和图片缓存。
- 系统托盘运行：关闭主窗口不会退出程序，可从托盘菜单退出。

### 下载可执行文件

如果你只想使用软件，不需要自己编译。

可执行文件已经上传到 GitHub Releases，请直接在仓库的 Releases 页面下载最新版本的 `PasteTool.exe`。

### 使用方式

1. 下载并运行 `PasteTool.exe`。
2. 首次启动后，程序会进入系统托盘并开始记录剪贴板历史。
3. 在任意应用中按 `Ctrl+Shift+V` 打开历史窗口。
4. 输入关键词搜索历史内容。
5. 使用 `↑/↓` 选择条目。
6. 按 `Enter` 粘贴，或双击条目直接粘贴。
7. 按 `Esc` 关闭窗口。

### 界面操作

- `Ctrl+Shift+V`：打开历史窗口
- `↑ / ↓`：移动选中项
- `Enter`：粘贴选中项
- `Esc`：关闭窗口
- `Tab`：将焦点移到列表
- 双击列表项：直接粘贴

### 项目结构

```text
pastetool/
├── src/
│   ├── PasteTool.App/     # WPF 前端
│   └── PasteTool.Core/    # 核心逻辑
├── tests/
│   └── PasteTool.Core.Tests/
└── publish/               # 本地发布输出目录
```

### 本地编译

环境要求：

- Windows
- .NET SDK 9.0 或更高版本

先执行测试：

```powershell
dotnet test PasteTool.sln
```

再构建 Release 单文件版本：

```powershell
dotnet publish src/PasteTool.App/PasteTool.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

输出文件：

```text
publish\PasteTool.exe
```

### 适用场景

- 频繁复制代码、命令或文本片段
- 在聊天工具、文档、浏览器之间反复粘贴
- 需要回找刚才复制过的内容
- 需要保留图片和富文本复制历史

---

## English

### Overview

PasteTool is a clipboard history utility for Windows.

It lives in the system tray, watches clipboard changes in the background, and opens a searchable history window with a global hotkey. The app supports text, rich text, and image clipboard content, with preview and one-click paste back into the target app.

### Features

- Clipboard history for plain text, rich text (RTF/HTML), and images
- Global hotkey popup, default `Ctrl+Shift+V`
- Fast search with SQLite FTS5 and substring matching for continuous CJK queries
- Preview for text, RTF, HTML table text, and images
- One-click paste back to the previously active window
- Deduplication and automatic trimming of old entries and image cache
- System tray workflow; closing the window hides the app instead of exiting

### Download the Executable

If you only want to use the app, you do not need to build it locally.

The executable has already been uploaded to GitHub Releases. Download the latest `PasteTool.exe` from the repository's Releases page.

### Usage

1. Download and run `PasteTool.exe`.
2. The app starts in the system tray and begins recording clipboard history.
3. Press `Ctrl+Shift+V` in any application to open the history window.
4. Type to search your clipboard history.
5. Use `Up` / `Down` to select an entry.
6. Press `Enter` to paste, or double-click an item to paste immediately.
7. Press `Esc` to close the window.

### Keyboard and UI Controls

- `Ctrl+Shift+V`: open the history window
- `Up / Down`: move selection
- `Enter`: paste selected item
- `Esc`: close the window
- `Tab`: move focus to the list
- Double-click an item: paste immediately

### Project Layout

```text
pastetool/
├── src/
│   ├── PasteTool.App/     # WPF frontend
│   └── PasteTool.Core/    # core logic
├── tests/
│   └── PasteTool.Core.Tests/
└── publish/               # local publish output
```

### Build From Source

Requirements:

- Windows
- .NET SDK 9.0 or newer

Run tests first:

```powershell
dotnet test PasteTool.sln
```

Then publish the self-contained single-file Release build:

```powershell
dotnet publish src/PasteTool.App/PasteTool.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

Build output:

```text
publish\PasteTool.exe
```

### Typical Use Cases

- Reusing copied code snippets, commands, and text fragments
- Pasting repeatedly across chat apps, documents, and browsers
- Recovering something you copied a few minutes ago
- Keeping image and rich text clipboard history
