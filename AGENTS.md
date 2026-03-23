# PasteTool 项目说明

## 开发规则

1. 每次做完代码修改，应该打一个 release 包 exe：
   ```
   dotnet publish src/PasteTool.App/PasteTool.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
   ```
   输出文件：`publish\PasteTool.exe`

---

## 项目结构

```
pastetool/
├── src/
│   ├── PasteTool.App/          # WPF 前端应用层
│   │   ├── Windows/
│   │   │   ├── HistoryWindow.xaml/.cs   # 主界面（历史记录 + 预览）
│   │   │   └── SettingsWindow.xaml/.cs  # 设置界面
│   │   ├── Controls/
│   │   │   └── HighlightedTextBlock.cs  # 搜索高亮文本控件
│   │   ├── Models/
│   │   │   └── HistoryListItem.cs       # 列表项 ViewModel（含高亮分段）
│   │   ├── Infrastructure/
│   │   │   ├── GlobalHotkeyManager.cs   # 全局热键注册
│   │   │   ├── TrayIconHost.cs          # 系统托盘图标
│   │   │   ├── AutoStartService.cs      # 开机自启
│   │   │   └── AppPaths.cs             # 应用路径管理
│   │   ├── AppController.cs            # 应用主控制器（组装所有服务）
│   │   └── App.xaml/.cs               # 应用入口
│   │
│   └── PasteTool.Core/         # 核心业务逻辑层（无 UI 依赖）
│       ├── Models/
│       │   ├── ClipEntry.cs             # 剪贴板条目数据模型
│       │   ├── ClipKind.cs              # 条目类型枚举（Text/RichText/Image）
│       │   ├── CapturedClipboardPayload.cs  # 原始剪贴板内容
│       │   ├── AppSettings.cs           # 应用设置模型
│       │   └── HotkeyGesture.cs        # 热键描述模型
│       ├── Services/
│       │   ├── ClipboardHistoryManager.cs  # 历史管理（监听→存储→通知）
│       │   ├── ClipboardMonitor.cs         # 系统剪贴板变化监听
│       │   ├── PasteService.cs             # 粘贴执行（写剪贴板 + 发送 Ctrl+V）
│       │   ├── SqliteClipRepository.cs     # SQLite 持久化存储
│       │   ├── SearchService.cs            # 内存模糊搜索（fallback）
│       │   ├── SettingsStore.cs            # 设置读写（JSON）
│       │   └── FileLogger.cs              # 文件日志
│       ├── Utilities/
│       │   ├── ClipboardPayloadReader.cs   # 从系统剪贴板读取数据
│       │   ├── ClipboardPayloadWriter.cs   # 将数据写回系统剪贴板
│       │   ├── HtmlTextExtractor.cs        # HTML 纯文本提取 + 表格结构提取
│       │   ├── RichTextUtilities.cs        # RTF 纯文本提取
│       │   ├── ImageUtilities.cs           # 图片编解码 + 缩略图生成
│       │   ├── ContentHasher.cs            # 内容哈希（去重用）
│       │   ├── SearchNormalizer.cs         # 搜索文本规范化
│       │   └── StaDispatcher.cs           # STA 线程调度器（剪贴板操作需要）
│       └── Native/
│           └── NativeMethods.cs            # Win32 API 调用（SendInput、GetForegroundWindow 等）
│
└── tests/
    └── PasteTool.Core.Tests/   # 单元测试（xUnit）
```

---

## 功能概述

PasteTool 是一个 Windows 剪贴板历史管理工具，通过全局热键呼出，支持搜索、预览、一键粘贴。

### 核心功能
- **剪贴板监听**：实时捕获系统剪贴板变化，支持文本、富文本（RTF/HTML）、图片。
- **历史持久化**：使用 SQLite 存储历史记录，文本以 JSON blob 保存，图片以 PNG 保存（含缩略图），支持按条目数量和图片缓存大小自动裁剪。
- **全局热键呼出**：默认 `Ctrl+Shift+V`，可在设置中修改。呼出时自动记录前台窗口句柄，粘贴后回到该窗口执行 Ctrl+V。
- **搜索**：SQLite FTS5 全文搜索（主路径）+ 内存模糊搜索（fallback），300ms 防抖。
- **预览**：
  - 文本：ScrollViewer + TextBox
  - 富文本（RTF）：RichTextBox 渲染
  - Excel 等 HTML 表格数据：解析 `<tr>/<td>` 结构，以制表符对齐方式展示
  - 图片：Image 控件展示
- **粘贴**：将选中条目写回系统剪贴板，激活目标窗口，发送 Ctrl+V 键盘输入。
- **去重**：对相同内容（哈希匹配）只保留最新一条（更新时间戳）。
- **自动抑制**：粘贴时设置抑制 hash，避免将粘贴动作自身再次记录进历史。

### UI 交互
- `↑/↓`：在列表中移动选择
- `Enter`：粘贴选中项并关闭窗口
- `双击列表项`：粘贴选中项并关闭窗口
- `Esc`：关闭窗口（不粘贴）
- `Tab`：焦点移至列表
- 失焦自动隐藏（`OnDeactivated`）
- 关闭按钮仅隐藏窗口，不退出程序（从系统托盘退出）
