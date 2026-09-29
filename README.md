# WordLens

> 鼠标指针指向哪个英文单词，就实时显示它的中文释义 —— 一块「透明放大镜」式的纯本地桌面工具。
> A transparent in-screen magnifier that shows the Chinese meaning of whichever English word your cursor is pointing at — a fully local desktop tool.

---

## 中文文档

### 项目简介

WordLens 是一个**纯本地**的 Windows 桌面应用，不依赖任何云端、大模型（LLM）或在线翻译 API。
完整的运行链路是：**鼠标位置 → 移动镜头 → 采集镜头附近屏幕 → 本地 OCR → 逐词命中测试 → 归一化 → 查本地 SQLite 词典 → 弹出中文释义**。

所有处理都在本机内存中瞬时完成，屏幕内容不会上传，也不会落盘。

### 功能特性

- **透明圆形放大镜**：220×220、半透明、`TopMost`、点击穿透（鼠标事件不阻塞底层窗口）。
- **本地 OCR**：使用 Windows 内置 `Windows.Media.Ocr`（en-US），逐词返回 `Word.Text` 与 `Word.BoundingRect`，而非整段文本。
- **精确命中**：三级命中测试 —— 严格包含 → 容差外扩 → 最近词（带垂直方向惩罚与最大距离阈值）。
- **本地词典**：SQLite 承载 ECDICT 词库（`entries` + `word_forms` 变形表），支持 `"jumps" → "jump"` 这类词形还原。
- **零干扰交互**：全局热键，无需焦点。`F8` 开关镜头、`F10` 循环放大档、`F9` 锁定当前词、`Esc` 退出。
- **多屏 / 高 DPI**：Per-Monitor DPI Awareness V2，物理像素与 WPF DIP 严格区分，支持负坐标的虚拟屏幕。
- **可替换组件**：`IScreenCapture` / `IOcrService` / `IDictionaryService` 接口化，GDI `BitBlt` 采集可平滑升级为 `Windows.Graphics.Capture`。

### 系统要求

| 项 | 要求 |
|---|---|
| 操作系统 | Windows 10 22H2 及以上 / Windows 11 |
| .NET | .NET 10（LTS）运行时或 SDK |
| Windows SDK | 10.0.26100.0（含 WinRT 投影，已通过 `EnableWindowsTargeting` 自动引用，无需手动安装包） |
| OCR 语言包 | 系统需安装 **English (United States)** 识别语言包（Windows 默认自带） |
| MSIX 打包 | Visual Studio 2022 + “Windows Application Packaging Project” 工作负载（仅供打包，非运行必需） |

### 安装与运行

#### 1. 安装 .NET 10 SDK（开发者构建前置条件）

本仓库**不内置** .NET SDK。请在本机安装 **.NET 10 SDK**（https://dot.net），并确保命令行中 `dotnet --version` 输出为 `10.x`。这是**构建/发布本项目**所需：你执行下面的 `dotnet build` / `dotnet publish` 需要它。若你只是运行别人打包好的自包含版本（方式 A），则无需自行安装。

#### 2. 准备词典

完整 ECDICT 词库 `Data/dictionary.db`（约 **76.8 万词条** + 12.8 万词形）以 **GitHub Release 资产** 形式提供，**不纳入 git 仓库**（避免大文件撑大仓库体积）。

请到本仓库的 **Releases** 页面下载 `dictionary.db`，放入 `Data/` 目录后，即可**开箱即译**绝大多数英文单词。

> 如需自行重建 / 更新词典，见下方「词典构建」。

#### 3. 还原与构建

```bash
dotnet restore WordLens.slnx
dotnet build  WordLens.slnx -c Release
```

#### 4. 运行

**方式 A（推荐，一次发布、双击即跑）：自包含发布**

```bash
dotnet publish WordLens/WordLens.csproj -c Release -r win-x64 --self-contained true
```

产物在 `WordLens/bin/Release/net10.0-windows10.0.26100.0/win-x64/publish/`，
其中的 `WordLens.exe` 已**自带 .NET 10 运行时**。将其分发给最终用户后，对方**无需安装 .NET** 即可双击运行（你本机执行上面的 `dotnet publish` 仍需第 1 步的 .NET 10 SDK）。

**方式 B（直接运行，需本机已装 .NET 10 运行时）**

```bash
dotnet run --project WordLens/WordLens.csproj -c Release
```

> 若双击 `bin/.../WordLens.exe` 报 “You must install or update .NET to run this application.”，通常是 PATH 中的旧版全局 `dotnet`（如 8.0.8）与本项目所需的 .NET 10 冲突。请安装 .NET 10 运行时/SDK 并用 `dotnet --version` 确认是 `10.x`。

### 使用说明

启动后按 **F8** 唤出放大镜，把鼠标移到任意英文文本（网页、PDF、IDE……）上，即可看到中心词的中文释义。

| 快捷键 | 作用 |
|---|---|
| `F8` | 开关放大镜（焦点无关）；**默认不放大（1.0× 原比例）** |
| `F10` | 循环放大档位：1.0× → 2.0× → 3.0× → 回到 1.0×（按一次才放大） |
| `F9` | 锁定 / 解锁当前词（锁定后镜头不再跟随） |
| `Esc` | 退出程序 |

### 词典构建（可选）

将 [ECDICT](https://github.com/skywind3000/ECDICT) 导出的 `ecdict.csv` 转换为 `dictionary.db`：

```bash
dotnet run --project DictionaryBuilder/DictionaryBuilder.csproj -- \
  --input ecdict.csv \
  --output Data/dictionary.db
```

- 按**表头列名**映射（`word` / `phonetic` / `translation` / `definition` / `pos` / `exchange`），与列顺序无关。
- 必填列：`word`、`translation`；其余可选。
- 解析 ECDICT 的 `exchange` 字段（`p:/d:/i:/3:/0:/s:`），写入 `word_forms(form, lemma)` 以支持词形还原；`0:`（原形自指）跳过。
- 可重复运行：每次重建（先删后建），不过滤词。
- 支持带引号的字段（内嵌逗号、换行），符合 RFC-4180 基本约定。

> 完整 `ecdict.csv` 约 62 MB、77 万行，普通机器上构建约十几秒即可完成。若不想自行构建，可直接从本仓库 **Releases** 页面下载已构建好的 `Data/dictionary.db`。
>
> ⚠️ 下载地址用 `https://raw.githubusercontent.com/skywind3000/ECDICT/master/ecdict.csv`（仓库根目录即 `ecdict.csv`）。**不要使用 `stardict.csv`** —— 该文件名不存在，会下到一个 404 页面。

### 工作原理（流水线）

```
GetCursorPos
   │
   ▼
DispatcherTimer(16ms): 移动 LensWindow + LensOverlay，更新 Magnifier 源窗口
   │
   ▼
System.Threading.Timer(~110ms, 9–12 FPS):
   ├─ 计算采集矩形（默认 480×180 物理像素，镜头中心附近）
   ├─ GdiScreenCapture.CaptureRegion → Bitmap
   ├─ WindowsOcrService.RecognizeWordsAsync → OcrWordInfo[]（逐词）
   ├─ 光标坐标转换到采集图局部坐标
   ├─ WordHitTester.FindWord → 命中的词（或 null）
   ├─ TextNormalizer.Normalize + IsEnglishWord 过滤
   ├─ CacheService 命中则跳过；否则 SqliteDictionaryService.LookupAsync
   └─ 触发 TranslationUpdated / NoWord 事件 → TranslationPopup 显示
```

关键设计点：

- **单飞（single-flight）OCR**：`SemaphoreSlim(1,1)` + 递增请求 ID + 每轮 `CancellationTokenSource`，丢弃过期结果，避免积压。
- **缓存**：`Dictionary<string, TranslationResult?>`，相同词不重复查库。
- **去抖 + 去重**：110ms 去抖；与上一词相同时不重复更新 UI。
- **永不崩溃**：OCR / 采集 / SQLite / 词典缺失任一失败都降级为空结果，仅写日志。

### 坐标与 DPI

- 进程启动即设置 **Per-Monitor DPI Awareness V2**（`SetProcessDpiAwareness`）。
- 采集、命中测试、镜头定位一律使用**物理像素**；与 WPF 的 DIP 通过 `DpiHelper.PhysicalToDip` / `GetScaleAtPoint` 互转。
- `GetCursorPos` 返回虚拟屏幕坐标（可含负偏移），采集矩形按 `SM_XVIRTUALSCREEN` 等裁剪到合法范围。

### MSIX 打包（为何需要 Package Identity）

`Windows.Media.Ocr` 在 Windows 10+ 的**解包（unpackaged）桌面应用**下通常可用，但要让应用具备**完整、可靠的能力边界**，需要 MSIX 赋予的 **Package Identity**：

1. **后续升级采集方式**：计划用 `Windows.Graphics.Capture`（现代屏幕采集，支持 WDDM 显示）替换 GDI `BitBlt`，该 API 与受限能力（restricted capabilities）**必须有 Package Identity** 才能初始化。
2. **系统级集成与分发**：MSIX 提供干净的安装/卸载、自动更新通道，以及企业侧策略合规。
3. **OCR 初始化更稳健**：在带身份的包内，in-box OCR 引擎的加载与语言包匹配更可靠。

> 当前 MVP 用 GDI `BitBlt` + Magnification API 采集，两者均为经典 Win32，**解包运行即可工作**；MSIX 是推荐的分发形态，并非运行前置条件。

**构建 / 安装 / 调试（Visual Studio）**

1. 用 Visual Studio 2022 打开 `WordLens.slnx`（或单独打开 `WordLens.Package\WordLens.Package.wapproj`）。
2. 确认已安装工作负载：**“Windows Application Packaging Project”** 与 **“.NET 桌面开发”**。
3. 将 `WordLens.Package` 设为启动项目，目标选 `x64`（或 `x86`，需与 WordLens 目标一致）。
4. 按需修改 `Package.appxmanifest` 中的 `Identity Publisher`（当前为示例 `CN=WordLens`，正式发布需替换为你的证书主体）。
5. **生成 → 部署**（Deploy）即可在本机安装并启动；**F5** 进入调试（包内运行，带 Package Identity）。
6. 导出可分发包：右键项目 → **Store → Create App Packages**，生成 `.msix` / `.msixbundle`。

> ⚠️ `WordLens.Package.wapproj` 依赖 Visual Studio 的打包 MSBuild 目标，**不能**用 `dotnet build` 命令行直接构建——因此该工程**未加入** `WordLens.slnx` 的 CLI 构建链路，以保证 `dotnet build WordLens.slnx` 始终为绿。请用 Visual Studio 打开它。

### 目录结构

```
WordLens/
├─ WordLens.slnx                 # 解决方案（app + builder + tests）
├─ WordLens/
│  ├─ WordLens.csproj
│  ├─ App.xaml / App.xaml.cs      # 启动：ComWrappers + DPI + 组装各服务
│  ├─ AppController.cs            # 串联 UI / 流水线 / 热键 / 定时器
│  ├─ Models/                     # TranslationResult
│  ├─ OCR/                        # OcrWordInfo, IOcrService, WindowsOcrService
│  ├─ Capture/                    # IScreenCapture, GdiScreenCapture
│  ├─ Detection/                  # WordHitTester, HitTestOptions
│  ├─ Dictionary/                 # IDictionaryService, SqliteDictionaryService
│  ├─ Services/                   # CacheService, LoggingService
│  ├─ Interop/                    # Win32 P/Invoke, DpiHelper
│  ├─ Magnification/              # MagnificationApi, MagnifierController
│  ├─ Windows/                    # LensWindow, LensOverlay, TranslationPopup, MagnifierHost
│  ├─ Input/                      # GlobalHotkeyService
│  ├─ Pipeline/                   # TranslationPipeline（单飞 + 缓存 + 去重）
│  ├─ Util/                       # TextNormalizer
│  └─ app.manifest                # DPI / 支持的系统版本
├─ DictionaryBuilder/             # ECDICT CSV → dictionary.db 命令行工具
│  ├─ DictionaryBuilder.csproj
│  ├─ Program.cs
│  └─ ecdict-sample.csv           # 内置样本（88 词），可用于快速验证构建链路
├─ WordLens.Tests/                # xUnit：WordHitTester + TextNormalizer（30 用例）
├─ WordLens.Package/              # MSIX 打包工程（用 Visual Studio 打开）
│  ├─ WordLens.Package.wapproj
│  ├─ Package.appxmanifest
│  └─ Images/                     # StoreLogo / 磁贴 / 闪屏占位图
└─ Data/
   ├─ .gitkeep                    # 保留目录（dictionary.db 通过 Releases 获取，不入库）
   └─ dictionary.db               # 完整 ECDICT 词典（约 76.8 万词条）— 以 Release 资产提供
```

### 已知限制 / 后续路线

- 默认采集区 480×180 物理像素，`CaptureWidth/Height` 可在 `TranslationPipeline` 构造处调整。
- 屏幕采集当前为 GDI `BitBlt`；后续可切换到 `Windows.Graphics.Capture`（需 MSIX 身份）。
- 词典查询为单步（`entries → word_forms → entries`），未做同义词 / 短语扩展。

### 许可证

- **本项目代码**：采用 **MIT 许可证**，版权人 **Gylhc2000**，见仓库根目录 `LICENSE` 文件。
- **词典数据（ECDICT）**：由 skywind3000（Linwei）以 **MIT License** 发布（Copyright (c) 2025 Linwei）。可自由使用、修改与再分发（含商业用途），唯须保留其 MIT 版权与许可声明。随附 / 提供的 `dictionary.db` 均遵循该 MIT 许可，完整声明见 `THIRD_PARTY_LICENSES.md`。

---

## English Documentation

### Introduction

WordLens is a **fully local** Windows desktop application. It does **not** rely on any cloud service, large language model (LLM), or online translation API.
The complete pipeline is: **cursor position → move lens → capture screen around the lens → local OCR → per-word hit testing → normalization → look up a local SQLite dictionary → pop up the Chinese translation**.

All processing happens in memory on your machine in real time. Screen content is never uploaded and never written to disk.

### Features

- **Transparent circular magnifier**: 220×220, semi-transparent, `TopMost`, click-through (mouse events do not block the window underneath).
- **Local OCR**: uses the built-in Windows `Windows.Media.Ocr` (en-US), returning `Word.Text` and `Word.BoundingRect` per word rather than a whole block of text.
- **Precise hit testing**: a three-level test — strict containment → tolerance expansion → nearest word (with vertical penalty and a max-distance threshold).
- **Local dictionary**: a SQLite-backed ECDICT database (`entries` + `word_forms` inflection table) supports lemma resolution such as `"jumps" → "jump"`.
- **Zero-interference interaction**: global hotkeys that work without focus. `F8` toggles the lens, `F10` cycles the zoom level, `F9` locks the current word, `Esc` quits.
- **Multi-monitor / High DPI**: Per-Monitor DPI Awareness V2, with a strict separation between physical pixels and WPF DIP, and support for virtual screens with negative coordinates.
- **Replaceable components**: `IScreenCapture` / `IOcrService` / `IDictionaryService` are interfaces, so the GDI `BitBlt` capture can be smoothly upgraded to `Windows.Graphics.Capture`.

### System Requirements

| Item | Requirement |
|---|---|
| OS | Windows 10 22H2 or later / Windows 11 |
| .NET | .NET 10 (LTS) runtime or SDK |
| Windows SDK | 10.0.26100.0 (WinRT projection is auto-referenced via `EnableWindowsTargeting`; no manual package needed) |
| OCR language pack | The **English (United States)** recognition language pack must be installed (ships by default with Windows) |
| MSIX packaging | Visual Studio 2022 with the “Windows Application Packaging Project” workload (packaging only, not required to run) |

### Install & Run

#### 1. Install the .NET 10 SDK (developer build prerequisite)

This repository **does not bundle** the .NET SDK. Install the **.NET 10 SDK** (https://dot.net) on your machine, and make sure `dotnet --version` prints `10.x` in your terminal. This is required to **build/publish the project**: running the `dotnet build` / `dotnet publish` commands below needs it. If you only run someone else's self-contained package (Option A), you do not need to install it yourself.

#### 2. Prepare the dictionary

The full ECDICT database `Data/dictionary.db` (~**768k entries** + 128k inflections) is distributed as a **GitHub Release asset** and is **not part of the git repository** (to avoid bloating the repo with a large file).

Download `dictionary.db` from the **Releases** page of this repository, place it into the `Data/` directory, and you can **translate out of the box** for the vast majority of English words.

> To rebuild / update the dictionary yourself, see “Build the Dictionary” below.

#### 3. Restore & Build

```bash
dotnet restore WordLens.slnx
dotnet build  WordLens.slnx -c Release
```

#### 4. Run

**Option A (recommended, publish once, double-click to run): self-contained publish**

```bash
dotnet publish WordLens/WordLens.csproj -c Release -r win-x64 --self-contained true
```

The output is under `WordLens/bin/Release/net10.0-windows10.0.26100.0/win-x64/publish/`.
The `WordLens.exe` there **ships with the .NET 10 runtime**. Once distributed to end users, they can **run it without installing .NET** — just double-click it. (You still need the .NET 10 SDK from step 1 to run the `dotnet publish` command above.)

**Option B (run directly, requires the .NET 10 runtime installed locally)**

```bash
dotnet run --project WordLens/WordLens.csproj -c Release
```

> If double-clicking `bin/.../WordLens.exe` shows “You must install or update .NET to run this application.”, it is usually because an older global `dotnet` (e.g. 8.0.8) on your PATH conflicts with the required .NET 10. Install the .NET 10 runtime/SDK and confirm `dotnet --version` is `10.x`.

### Usage

After launch, press **F8** to bring up the magnifier, then hover your cursor over any English text (web page, PDF, IDE, …) to see the Chinese meaning of the centered word.

| Hotkey | Action |
|---|---|
| `F8` | Toggle the magnifier (focus-independent); **no zoom by default (1.0× original scale)** |
| `F10` | Cycle zoom levels: 1.0× → 2.0× → 3.0× → back to 1.0× (zoom applies on press) |
| `F9` | Lock / unlock the current word (lens stops following once locked) |
| `Esc` | Quit the application |

### Build the Dictionary (Optional)

Convert the `ecdict.csv` exported from [ECDICT](https://github.com/skywind3000/ECDICT) into `dictionary.db`:

```bash
dotnet run --project DictionaryBuilder/DictionaryBuilder.csproj -- \
  --input ecdict.csv \
  --output Data/dictionary.db
```

- Maps by **header column names** (`word` / `phonetic` / `translation` / `definition` / `pos` / `exchange`), independent of column order.
- Required columns: `word`, `translation`; the rest are optional.
- Parses ECDICT’s `exchange` field (`p:/d:/i:/3:/0:/s:`), writing `word_forms(form, lemma)` for lemma resolution; `0:` (self-reference to the base form) is skipped.
- Re-runnable: rebuilt each time (drop then create), with no word filtering.
- Supports quoted fields (embedded commas, newlines) per the basic RFC-4180 convention.

> A full `ecdict.csv` is ~62 MB / 770k rows and builds in roughly ten-plus seconds on an ordinary machine. If you prefer not to build it yourself, download the prebuilt `Data/dictionary.db` from this repo’s **Releases** page.
>
> ⚠️ Use `https://raw.githubusercontent.com/skywind3000/ECDICT/master/ecdict.csv` (the repo root *is* `ecdict.csv`). **Do not use `stardict.csv`** — that file name does not exist and will return a 404 page.

### How It Works (Pipeline)

```
GetCursorPos
   │
   ▼
DispatcherTimer(16ms): move LensWindow + LensOverlay, update the Magnifier source window
   │
   ▼
System.Threading.Timer(~110ms, 9–12 FPS):
   ├─ compute the capture rectangle (default 480×180 physical pixels, near the lens center)
   ├─ GdiScreenCapture.CaptureRegion → Bitmap
   ├─ WindowsOcrService.RecognizeWordsAsync → OcrWordInfo[] (per word)
   ├─ convert cursor coordinates into the capture image's local space
   ├─ WordHitTester.FindWord → the hit word (or null)
   ├─ TextNormalizer.Normalize + IsEnglishWord filter
   ├─ skip on CacheService hit; otherwise SqliteDictionaryService.LookupAsync
   └─ raise TranslationUpdated / NoWord → TranslationPopup displays
```

Key design points:

- **Single-flight OCR**: `SemaphoreSlim(1,1)` + an incrementing request ID + a per-round `CancellationTokenSource` discard stale results and prevent backlog.
- **Cache**: `Dictionary<string, TranslationResult?>`, so the same word is never looked up twice.
- **Debounce + dedupe**: 110ms debounce; no UI update when the word is unchanged from the previous one.
- **Never crashes**: any failure in OCR / capture / SQLite / missing dictionary degrades gracefully to an empty result and only logs.

### Coordinates & DPI

- **Per-Monitor DPI Awareness V2** (`SetProcessDpiAwareness`) is set at process startup.
- Capture, hit testing, and lens positioning all use **physical pixels**; they are converted to/from WPF DIP via `DpiHelper.PhysicalToDip` / `GetScaleAtPoint`.
- `GetCursorPos` returns virtual-screen coordinates (which may be negative); the capture rectangle is clipped to the valid range using `SM_XVIRTUALSCREEN`, etc.

### MSIX Packaging (Why a Package Identity Is Needed)

`Windows.Media.Ocr` usually works in an **unpackaged** desktop app on Windows 10+, but to give the app a **complete, reliable capability boundary**, the **Package Identity** granted by MSIX is required:

1. **Future capture upgrade**: `Windows.Graphics.Capture` (modern screen capture, WDDM display support) — planned to replace GDI `BitBlt` — and its restricted capabilities **require a Package Identity** to initialize.
2. **System-level integration & distribution**: MSIX provides clean install/uninstall, an auto-update channel, and enterprise policy compliance.
3. **More robust OCR init**: inside an identity-bearing package, the in-box OCR engine loads and matches language packs more reliably.

> The current MVP uses GDI `BitBlt` + the Magnification API — both classic Win32 — and **runs fine unpackaged**; MSIX is the recommended distribution form, not a runtime prerequisite.

**Build / Install / Debug (Visual Studio)**

1. Open `WordLens.slnx` in Visual Studio 2022 (or open `WordLens.Package\WordLens.Package.wapproj` on its own).
2. Make sure the **“Windows Application Packaging Project”** and **“.NET Desktop Development”** workloads are installed.
3. Set `WordLens.Package` as the startup project, with the target `x64` (or `x86`, matching WordLens’s target).
4. Edit `Identity Publisher` in `Package.appxmanifest` as needed (currently the placeholder `CN=WordLens`; replace with your certificate subject for a real release).
5. **Build → Deploy** installs and launches it locally; **F5** debugs inside the package (with Package Identity).
6. To export a distributable: right-click the project → **Store → Create App Packages**, producing `.msix` / `.msixbundle`.

> ⚠️ `WordLens.Package.wapproj` depends on Visual Studio’s packaging MSBuild targets and **cannot** be built with `dotnet build` from the command line — therefore it is **excluded** from the `WordLens.slnx` CLI build chain, so `dotnet build WordLens.slnx` always stays green. Open it in Visual Studio.

### Project Structure

```
WordLens/
├─ WordLens.slnx                 # Solution (app + builder + tests)
├─ WordLens/
│  ├─ WordLens.csproj
│  ├─ App.xaml / App.xaml.cs      # Startup: ComWrappers + DPI + wiring services
│  ├─ AppController.cs            # Orchestrates UI / pipeline / hotkeys / timers
│  ├─ Models/                     # TranslationResult
│  ├─ OCR/                        # OcrWordInfo, IOcrService, WindowsOcrService
│  ├─ Capture/                    # IScreenCapture, GdiScreenCapture
│  ├─ Detection/                  # WordHitTester, HitTestOptions
│  ├─ Dictionary/                 # IDictionaryService, SqliteDictionaryService
│  ├─ Services/                   # CacheService, LoggingService
│  ├─ Interop/                    # Win32 P/Invoke, DpiHelper
│  ├─ Magnification/              # MagnificationApi, MagnifierController
│  ├─ Windows/                    # LensWindow, LensOverlay, TranslationPopup, MagnifierHost
│  ├─ Input/                      # GlobalHotkeyService
│  ├─ Pipeline/                   # TranslationPipeline (single-flight + cache + dedup)
│  ├─ Util/                       # TextNormalizer
│  └─ app.manifest                # DPI / supported OS versions
├─ DictionaryBuilder/             # ECDICT CSV → dictionary.db CLI tool
│  ├─ DictionaryBuilder.csproj
│  ├─ Program.cs
│  └─ ecdict-sample.csv           # Bundled sample (88 words) for quick build validation
├─ WordLens.Tests/                # xUnit: WordHitTester + TextNormalizer (30 cases)
├─ WordLens.Package/              # MSIX packaging project (open in Visual Studio)
│  ├─ WordLens.Package.wapproj
│  ├─ Package.appxmanifest
│  └─ Images/                     # StoreLogo / tile / splash placeholders
└─ Data/
   ├─ .gitkeep                    # Keeps the directory (dictionary.db comes from Releases, not git)
   └─ dictionary.db               # Full ECDICT database (~768k entries) — provided as a Release asset
```

### Known Limitations / Roadmap

- The default capture area is 480×180 physical pixels; `CaptureWidth/Height` can be adjusted at the `TranslationPipeline` constructor.
- Screen capture currently uses GDI `BitBlt`; it can later switch to `Windows.Graphics.Capture` (requires MSIX identity).
- Dictionary lookup is single-step (`entries → word_forms → entries`); no synonym / phrase expansion yet.

### License

- **Project code**: licensed under the **MIT License**, Copyright (c) 2026 **Gylhc2000** — see the `LICENSE` file at the repository root.
- **Dictionary data (ECDICT)**: published by skywind3000 (Linwei) under the **MIT License** (Copyright (c) 2025 Linwei). Free to use, modify, and redistribute (including commercial use), provided its MIT copyright and license notice are retained. The bundled / provided `dictionary.db` follows that MIT license; the full notice is in `THIRD_PARTY_LICENSES.md`.
