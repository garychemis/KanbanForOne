# KanbanForOne / Kanban41

Kanban41 是一个 Windows 本地个人看板应用，用来管理个人任务、备忘、简单日程和本地附件。它不包含登录、团队协作或云同步逻辑，所有数据都保存在本机。

当前版本：`v0.1`

## 功能特性

- 五列看板：待办、进行中、卡住、完成、备忘录
- 任务管理：创建、编辑、删除、状态流转、优先级、标签、起止日期
- 备忘管理：创建、编辑、删除、标签和正文记录
- 拖拽排序：任务可在四个任务列之间移动，备忘只在备忘列内排序
- 全局搜索：按标题、描述/正文、标签和附件原始文件名过滤
- 基础筛选：全部任务、今日任务、高优先级、有附件
- 日历视图：展示设置了起始日期或终止日期的任务
- 本地附件：支持从文件资源管理器拖入卡片或详情抽屉，也支持手动选择文件
- 附件操作：打开、在资源管理器中定位、删除
- 本地备份：将 `Kanban41.db` 和 `attachments/` 打包为 zip，并支持从备份恢复

## 技术栈

- .NET 8
- WPF
- MVVM 风格的数据绑定
- SQLite
- `Microsoft.Data.Sqlite`
- 原生 WPF 控件和 `ResourceDictionary` 样式

## 环境要求

- Windows 10/11
- .NET 8 SDK
- Visual Studio 2022、Rider，或任意支持 .NET/WPF 的编辑器

## 快速开始

在项目根目录运行：

```powershell
dotnet restore
dotnet build
dotnet run --project KanbanForOne.csproj
```

应用启动后会自动初始化 SQLite 数据库和本地数据目录。

## 发布

项目内置了文件夹发布配置：

```powershell
dotnet publish KanbanForOne.csproj /p:PublishProfile=FolderProfile
```

默认输出目录：

```text
bin\Release\net8.0-windows\publish\win-x64\
```

发布配置使用：

- `Release`
- `win-x64`
- 自包含发布
- 单文件发布
- ReadyToRun

也可以直接使用命令行参数发布：

```powershell
dotnet publish KanbanForOne.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishReadyToRun=true
```

## 制作安装包

项目提供了一个基于 Windows IExpress 的安装包构建脚本：

```powershell
powershell -ExecutionPolicy Bypass -File scripts\build-installer.ps1
```

脚本会先发布 `win-x64` 自包含版本，再生成安装器：

```text
artifacts\installer\Kanban41_Setup_v0.1.0.exe
```

安装器会将应用安装到当前用户目录：

```text
%LocalAppData%\Programs\Kanban41
```

安装后会创建开始菜单快捷方式和桌面快捷方式，并在 Windows“应用和功能”中注册卸载入口。卸载时会移除程序文件和快捷方式，但会保留 `Kanban41.db`、`attachments/` 和 `backups/`，避免误删本地看板数据。

## 数据存储

当前实现会把数据保存在应用 EXE 所在目录，便于连同程序目录一起迁移或备份。

运行后会生成：

```text
Kanban41.db
attachments\
backups\
```

在开发模式下，数据通常位于：

```text
bin\Debug\net8.0-windows\
```

在发布版本中，数据通常位于：

```text
bin\Release\net8.0-windows\publish\win-x64\
```

附件不会以 BLOB 形式写入 SQLite。应用会把附件文件复制到 `attachments/`，并在 SQLite 中保存原始文件名、存储文件名、相对路径、文件大小、所属任务/备忘等元数据。

附件目录结构示例：

```text
attachments\
  tasks\
    {TaskId}\
      {AttachmentId}_{SafeFileName}
  notes\
    {NoteId}\
      {AttachmentId}_{SafeFileName}
```

附件限制：

- 单次最多保存 10 个文件
- 单个文件最大 200 MB
- 单次拖入总大小最大 1 GB

## 备份与恢复

在“数据备份”页面可以创建完整备份。备份文件保存在 `backups/`，命名格式类似：

```text
Kanban41_Backup_yyyyMMdd_HHmmss.zip
```

备份包包含：

```text
Kanban41.db
attachments/
```

恢复备份会覆盖当前数据库和附件目录。恢复前应用会自动创建一份保护备份：

```text
Kanban41_PreRestore_yyyyMMdd_HHmmss.zip
```

## 项目结构

```text
KanbanForOne/
  Controls/       WPF 用户控件：看板、列、卡片、抽屉、附件列表等
  Converters/     XAML 绑定转换器
  Models/         TaskItem、NoteItem、AttachmentItem 和拖拽 payload
  Services/       SQLite、仓储、附件存储、备份恢复、路径管理
  Styles/         主题样式和抽屉样式
  ViewModels/     MainWindowViewModel、命令和通知基类
  doc/            产品和交付说明
  MainWindow.*    应用主窗口
  App.xaml        应用资源入口
```

## 数据模型概览

核心实体：

- `TaskItem`：任务标题、描述、状态、优先级、标签、起止日期、完成时间、附件集合
- `NoteItem`：备忘标题、正文、标签、附件集合
- `AttachmentItem`：附件所有者、原始文件名、存储文件名、相对路径、大小、创建时间

SQLite 表：

- `Tasks`
- `Notes`
- `Attachments`
- `AppSettings`

数据库当前 schema 版本为 `2`，启动时会自动创建表、索引，并补齐 `StartDate` / `EndDate` 等字段。

## 开发说明

- 入口 ViewModel 是 `ViewModels/MainWindowViewModel.cs`
- 数据路径由 `Services/AppPaths.cs` 决定
- 数据库初始化和迁移在 `Services/DatabaseService.cs`
- 附件复制、打开、定位和删除在 `Services/AttachmentStorageService.cs`
- 备份和恢复逻辑在 `Services/BackupService.cs`
- 看板 UI 入口是 `Controls/BoardView.xaml`

设计边界：

- 不添加登录、账号、团队协作、评论、云同步或网络上传
- 任务和备忘是两种不同实体，备忘不是任务状态
- SQLite 是主数据库，附件文件保存在本地目录
- 备份必须同时包含数据库和附件目录

## 常用命令

```powershell
dotnet restore
dotnet build
dotnet run --project KanbanForOne.csproj
dotnet publish KanbanForOne.csproj /p:PublishProfile=FolderProfile
```
