# Kanban41（KanbanForOne）

一个面向个人工作流的 Windows 桌面看板应用。它以「单人使用」为设计前提，在一个本地窗口内整合了**任务看板、备注、日历、人工时记录与汇总、设计条件归档**以及整体的**数据备份与恢复**，所有数据存储在本地，无需联网、无需服务器。

> 工程名为 `KanbanForOne`，产品显示名为 `Kanban41`。

---

## 功能概览

### 外观与卡片预览
- 保留原亮色主题，新增暗色主题；顶部太阳/月亮按钮与设置页均可切换，并自动记住选择。
- 待办、进行中、卡住卡片按原布局常驻显示内容与元数据；仅完成任务和备忘录默认只显示标题，悬停展示前四行详情，移开后恢复标题高度。日历和归档同步遵循对应状态的展示规则。

### 看板 / 任务
- 四列看板（待办、进行中、卡住、完成）+ 备注列，卡片支持拖拽移动与排序。
- 任务具备：标题、描述、状态、优先级、标签、日期区间、截止/开始日期、附件。
- 标签芯片编辑、附件拖放上传、附件打开与在资源管理器中定位。
- 快速筛选：全部任务、今日任务、高优先级、超期未完成、有附件、归档。
- 任务在完成时自动记录完成时间，并根据开始/截止日期实时判断是否超期并显示「超期」标注。

### 备注（Notes）
- 独立的备忘卡片，支持标记与附件，与任务并列展示。
- 内置 Markdown 编辑 / 预览 / 查看。

### 日历
- 按月/日期范围的日历视图，任务与人工时按日期分区展示。
- 点击日期查看当天任务与人工时，支持新增、查看、编辑、删除当天记录。

### 人工时（Work Hours）
- 人工时录入：项目号、专业、工作内容、工时、可选备注。
- 项目号自动转大写并去除空格；专业与工作内容用 `data` 目录下的 JSON 配置维护，新增选项自动同步。
- 人工时汇总页：按月自动查询或按日期区间查询，按项目号、专业、工作内容展示工时分布与汇总明细，支持组合筛选与级联选项。
- 导出 Excel：包含「汇总」与「原始明细」两个工作表，并记录查询日期范围。

### 设计条件归档（Design Conditions）— 独立模块
- 管理不同专业之间提交/接收的设计条件，字段包含项目、提出专业、接收专业、接收人、提出日期、条件名称、版次、图幅与图纸数量。
- 支持在同一记录中维护多种图幅及对应数量。
- 提出日期作为唯一日期口径，在日历中展示。
- 条件文件上传、打开、定位、删除。
- 按项目 / 提出专业 / 接收专业 / 条件名称分级汇总图纸量，并导出汇总及明细 Excel。
- 使用独立的 `DesignConditions.db` 与独立附件目录，不修改主数据库；数据库升级至 V3，兼容迁移旧数据与备份。

### 归档（Archive）
- 创建与选择归档分区，归档页顶部为分区选择器，新建与管理分区通过弹窗完成。
- 任务可归档到指定分区，并在归档视图内浏览。

### 备份与恢复
- 完整备份与整体恢复：一次包含看板、任务、人工时、所有附件及设计条件数据。
- 提供恢复前的完整保护备份，以及恢复失败时的整体数据回滚。
- 附件保存采用事务化编排，备份包带校验、解压限制与异步错误处理。

---

## 技术栈

| 类别 | 技术 |
|------|------|
| 平台 | .NET 8（`net8.0-windows`） |
| UI | WPF（设定 `UseWindowsForms`，用于系统托盘图标 `NotifyIcon`） |
| 架构 | MVVM + 依赖注入（`Microsoft.Extensions.DependencyInjection`） |
| 数据存储 | SQLite（`Microsoft.Data.Sqlite`，启用 WAL 日志模式） |
| Excel 导出 | ClosedXML |
| 测试 | xUnit v3 + Microsoft.NET.Test.Sdk |

---

## 项目结构

```
KanbanForOne/
├─ App.xaml / App.xaml.cs        # 应用入口、DI 容器、单实例限制
├─ MainWindow.xaml(.cs)          # 主窗口（无边框窗口 + 资源主题）
├─ ViewModels/                   # 页面级 ViewModel（按页拆分）
│  └─ MainWindowViewModel        # 主窗口导航与页面切换中枢
├─ Models/                       # 领域/数据模型（任务、备注、附件、人工时、归档等）
├─ Services/                     # 数据访问、存储、备份、对话框/文件选择/剪贴板等基础服务
├─ Repositories（部分在 Services/） # 数据库仓储
├─ Controls/                     # 自定义 WPF 控件与视图
├─ Converters/                   # 绑定转换器
├─ Styles/                       # 全局主题与样式（KanbanTheme、DrawerStyles）
├─ Modules/
│  └─ DesignConditions/          # 设计条件归档独立模块（Data/Models/Repositories/Services/ViewModels/Views）
├─ KanbanForOne.Tests/           # 单元与集成测试
├─ icon/                         # 应用图标
└─ data/                         # 运行时数据（见下）
```

> 运行期生成的数据目录 `data/`：`Kanban41.db` 主数据库、`db/`、`attachments/`（任务/备注附件）、`backups/`（完整备份包）、`design-conditions/`（设计条件独立库与附件）、`workhour-options.json`（人工时配置）。这些目录已在 `.gitignore` 中排除。

---

## 数据与本地化

- 数据根目录默认取可执行文件所在目录下的 `data/`（`AppPaths`）。
- 主数据库 `Kanban41.db` 采用 **WAL 日志模式**，读不阻塞写、写不阻塞读；备份/复制前会自动执行 `wal_checkpoint(TRUNCATE)` 将 WAL 数据合并回主文件，保证主文件自包含。
- 数据库带版本号（`PRAGMA user_version`）与幂等迁移，升级时自动迁移旧数据结构。
- 应用通过 `SingleInstanceManager` 保证单实例运行（重复启动会激活已有窗口）。

---

## 构建与运行

前置：安装 [.NET 8 SDK](https://dotnet.microsoft.com/)（含 Windows 桌面开发能力）。

```bash
# 构建（Debug）
dotnet build KanbanForOne.sln -c Debug

# 构建（Release）
dotnet build KanbanForOne.sln -c Release

# 运行（在 Visual Studio 中也可直接 F5）
dotnet run --project KanbanForOne.csproj
```

也可使用 Visual Studio 打开 `KanbanForOne.sln` 直接生成并运行。

### 运行测试

```bash
dotnet test KanbanForOne.sln -c Debug
```

测试工程覆盖：归档集成与仓储、备份安全（路径穿越/解压限制/校验）、备份服务、设计条件模块（数据库迁移、多图幅、导出、概览）、任务超期判断、人工时仓储/导出/汇总、发布说明解析、单实例、WPF UI 冒烟等。

---

## 主要依赖

- `Microsoft.Data.Sqlite` + `SQLitePCLRaw.bundle_e_sqlite3`
- `ClosedXML`
- `Microsoft.Extensions.DependencyInjection`
- 测试：`xunit.v3`、`Microsoft.NET.Test.Sdk`、`coverlet.collector`

---

## 许可证与说明

© 2026 闫飞（Copyright © 2026 闫飞）。本项目为个人工作流工具，仅供个人使用。
