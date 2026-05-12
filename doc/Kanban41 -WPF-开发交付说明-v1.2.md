# Kanban41 个人看板 - WPF 开发交付说明文档 v1.2

> 本文档用于指导 WPF 开发人员或 AI 代码助手（如 Codex）实现 Kanban41 个人看板第一阶段版本。  
> 目标是明确产品边界、UI 结构、数据模型、SQLite 存储、文件附件保存、拖拽交互和第一阶段验收标准。

---

## 1. 产品定位

Kanban41 是一个 **Windows 本地个人看板应用**，用于管理个人任务、备忘、待办事项、简单日程和本地附件。

核心定位：

```text
本地
个人
轻量
简洁
无需登录
无团队协作
无云同步
无报告统计
```

视觉风格：

```text
新极简主义
明亮
低饱和度
现代桌面应用感
纸张便签感
```

存储原则：

```text
SQLite 是唯一主数据库。
JSON 仅作为导出、导入、备份交换格式。
附件文件不存入 SQLite BLOB，而是复制到本地附件目录。
SQLite 只保存附件元数据和相对路径。
```

---

## 2. 技术栈

推荐技术栈：

```text
WPF
.NET 8 或 .NET 9
MVVM
CommunityToolkit.Mvvm
SQLite
Microsoft.Data.Sqlite
原生 WPF 控件 + 自定义 ResourceDictionary
```

不建议第一版使用：

```text
WinForms
WebView 作为主界面
云数据库
登录系统
在线同步 SDK
团队协作 SDK
复杂第三方 UI 框架
```

---

## 3. 功能范围

### 3.1 包含功能

```text
任务创建
任务编辑
任务删除
任务状态流转
任务拖拽排序
五列看板
备忘管理
卡片本地附件
全局搜索
基础筛选
日历视图
完成记录
归档
SQLite 本地存储
本地数据备份
设置
```

### 3.2 明确不做

```text
登录注册
团队成员
任务分配
评论系统
用户头像
工作空间管理
云同步
实时协作
报告统计图表
组织架构
复杂后台
文件云上传
在线附件存储
```

---

## 4. 页面列表

| 页面 | 说明 |
|---|---|
| 看板 | 主任务流转页面，五列布局 |
| 日历 | 按日期查看任务 |
| 完成记录 | 查看已完成但未归档的任务 |
| 归档 | 查看已归档任务和备忘 |
| 数据备份 | 本地 SQLite 数据库和附件备份、导入、导出 |
| 设置 | 应用配置、主题、默认行为、数据库位置、附件目录 |

第一阶段重点实现：

```text
看板页面
SQLite 本地数据层
卡片组件
任务详情抽屉
备忘详情抽屉
基础搜索
基础拖拽
文件拖拽保存为附件
```

---

## 5. 主界面布局

### 5.1 整体结构

```text
MainWindow
├── SideNavControl
├── TopNavControl
└── MainContent
    ├── BoardView
    ├── CalendarView
    ├── HistoryView
    ├── ArchiveView
    ├── BackupView
    └── SettingsView
```

### 5.2 顶部栏

顶部栏包含：

```text
看板 / 日历 页面切换
搜索框
设置快捷入口
```

说明：

```text
顶部设置图标和左侧设置入口指向同一个设置页。
```

### 5.3 左侧栏

左侧栏包含：

```text
Kanban41 标题
+ 新建任务
全部任务
今日任务
高优先级
完成记录
归档
数据备份
设置
```

左侧栏是主导航区域，宽度固定。

---

## 6. 看板列规范

看板包含五列：

```text
待办
进行中
卡住
已完成
备忘
```

| 列名 | 数据类型 | 业务逻辑 | 视觉特征 |
|---|---|---|---|
| 待办 | TaskItem | 计划中但尚未开始的任务 | 奶油白卡片，灰褐色侧边条 |
| 进行中 | TaskItem | 当前正在处理的任务 | 浅蓝卡片，蓝色侧边条 |
| 卡住 | TaskItem | 遇到阻碍，暂时无法推进 | 浅杏色卡片，橙色侧边条 |
| 已完成 | TaskItem | 已完成但未归档的任务 | 浅绿灰卡片，文字弱化，标题删除线 |
| 备忘 | NoteItem | 灵感、备忘、随手记录 | 浅黄色便签，无状态条 |

重要规则：

```text
备忘不是 TaskStatus 的一种。
备忘是独立数据类型 NoteItem。
备忘列只显示 NoteItem。
任务列只显示 TaskItem。
```

---

## 7. 卡片组件规范

### 7.1 任务卡片 TaskCard

适用于：

```text
待办
进行中
卡住
已完成
```

结构：

```text
TaskCard
├── 左侧状态色条 4px
├── Header
│   ├── 标签 Chip
│   └── 优先级圆点
├── Body
│   └── 任务标题
├── Summary
│   └── 任务描述摘要，可选
└── Footer
    ├── 日期 / 状态提示
    └── 附件图标 + 附件数量
```

字段：

```text
Title
Description
Status
Priority
Tags
DueDate
CreatedAt
UpdatedAt
CompletedAt
AttachmentCount
```

视觉规则：

```text
圆角：10px
左侧状态条：4px
内边距：16px
阴影：0 4px 12px rgba(0,0,0,0.05)
标题：14px Semi-Bold
正文：12px / 13px Regular
标签：11px Medium
日期：11px Regular
附件图标：12px / 14px
```

优先级颜色：

```text
High：红色
Medium：黄色 / 橙色
Low：蓝色 / 灰蓝色
```

#### 已完成卡片特殊规则

不要整张卡片使用 opacity 降低。

推荐：

```text
背景保持清楚
标题弱化并加删除线
标签弱化
日期弱化
保留绿色完成状态
附件图标仍保持可识别
```

### 7.2 备忘卡片 NoteCard

适用于：

```text
备忘列
```

结构：

```text
NoteCard
├── Header
│   └── 备忘标题
├── Content
│   └── 两行正文摘要
└── Footer
    ├── 可选标签
    └── 附件图标 + 附件数量
```

备忘卡片不显示：

```text
状态条
优先级
截止日期
完成状态
任务状态
```

视觉规则：

```text
背景：#FFF9DB
边框：浅黄色
文字：偏棕色或深灰色
可轻微旋转，但不要太拟物
整体像现代便签
```

---

## 8. 文件拖拽附件需求

### 8.1 需求说明

看板卡片需要支持从 Windows 文件资源管理器中拖拽文件到卡片上，并将文件保存为该卡片的本地附件。

这里的“上传”指：

```text
本地导入
本地复制
本地保存
本地关联到卡片
```

不指：

```text
上传到服务器
上传到云端
在线同步
网络传输
```

### 8.2 支持范围

支持拖拽附件到：

```text
任务卡片 TaskCard
备忘卡片 NoteCard
任务详情抽屉 TaskDrawer
备忘详情抽屉 NoteDrawer
```

第一版不支持：

```text
拖拽文件夹
拖拽网络路径
拖拽 Outlook 邮件附件
拖拽浏览器图片
直接粘贴截图
```

后续可扩展。

### 8.3 拖拽交互

#### 拖入卡片区域时

当用户把文件拖到卡片上方：

```text
卡片边框高亮
卡片轻微上浮
显示半透明提示：“释放文件以保存为附件”
鼠标效果显示 Copy
```

#### 释放文件时

用户释放文件后：

```text
复制文件到本地附件目录
写入 AttachmentItem 记录到 SQLite
更新卡片 AttachmentCount
显示轻量提示：“已保存 1 个附件”
```

#### 多文件拖拽

支持一次拖拽多个文件：

```text
单次最多 10 个文件
超过数量时提示用户
逐个复制
失败项显示错误提示
成功项正常保存
```

#### 文件大小限制

第一版建议默认限制：

```text
单个文件最大 200MB
单次拖拽总大小最大 1GB
```

超过限制时：

```text
不复制文件
显示错误提示
```

### 8.4 附件存储原则

附件文件不直接存入 SQLite。

推荐本地目录结构：

```text
%LocalAppData%\Kanban41\
├── data\
│   └── Kanban41.db
├── attachments\
│   ├── tasks\
│   │   └── {TaskId}\
│   │       └── {AttachmentId}_{SafeFileName}
│   └── notes\
│       └── {NoteId}\
│           └── {AttachmentId}_{SafeFileName}
└── backups\
```

示例：

```text
%LocalAppData%\Kanban41\attachments\tasks\4f2a...\9c1b_design-spec.pdf
```

SQLite 中只保存：

```text
原始文件名
存储文件名
相对路径
文件大小
文件类型
所属卡片 ID
所属卡片类型
创建时间
```

### 8.5 文件名处理规则

拖拽保存时必须处理文件名：

```text
移除非法字符
保留原始扩展名
前缀使用 AttachmentId 防止重名
数据库保存 OriginalFileName
本地文件保存 StoredFileName
```

示例：

```text
OriginalFileName: 设计稿 v1.0?.png
StoredFileName: 9c1b7f2a_设计稿 v1.0.png
```

### 8.6 附件展示规则

卡片底部显示：

```text
附件图标
附件数量
```

例如：

```text
📎 3
```

任务详情抽屉中显示附件列表：

```text
文件图标
原始文件名
文件大小
创建时间
操作按钮：打开 / 定位 / 删除
```

附件操作：

```text
打开：使用系统默认程序打开
定位：在资源管理器中显示文件
删除：删除附件记录，并可选择删除本地文件
```

第一版删除行为建议：

```text
删除附件时，同时删除本地附件文件。
删除失败时保留数据库记录，并提示用户。
```

### 8.7 附件安全规则

```text
不要自动执行附件文件。
打开附件必须由用户主动点击。
不要把附件路径暴露在主卡片上。
不要允许路径穿越。
不要直接使用原始文件名作为存储文件名。
不要把附件保存到程序安装目录。
```

---

## 9. 交互规则

### 9.1 点击卡片

点击任务卡片：

```text
打开右侧任务详情抽屉
```

点击备忘卡片：

```text
打开右侧备忘详情抽屉
```

### 9.2 拖拽任务规则

任务卡片：

```text
可以在 待办 / 进行中 / 卡住 / 已完成 之间拖拽
拖拽后更新 Status
同列拖拽更新 SortOrder
```

备忘卡片：

```text
只能在备忘列内部排序
不能拖到任务列
任务卡片也不能拖到备忘列
```

任务拖拽视觉反馈：

```text
Scale 1.02
Rotate 0.5° ~ 0.8°
Shadow 提升到 Level 3
原位置显示虚线占位符
鼠标样式为 grabbing
```

注意：

```text
任务拖拽和文件拖拽是两种不同交互。
从应用内拖拽卡片 = 移动任务状态或排序。
从 Windows 文件资源管理器拖拽文件 = 保存附件。
需要通过 DataObject 类型区分。
```

### 9.3 Hover 状态

```text
卡片轻微上浮 1px ~ 2px
阴影增强
边框略微加深
```

### 9.4 Selected 状态

```text
边框加深
左侧状态条加深
不使用过强外圈 ring
保持桌面应用克制感
```

### 9.5 新建任务

入口：

```text
左侧 + 新建任务
各任务列顶部 + 添加
```

规则：

```text
左侧 + 新建任务：默认创建到待办
待办列 +：创建待办任务
进行中列 +：创建进行中任务
卡住列 +：创建卡住任务
已完成列 +：可选，第一版可以不提供
备忘列 +：创建备忘
```

第一版新建任务方式：

```text
在对应列顶部生成一个空白输入态卡片
输入标题
回车保存
Esc 取消
```

---

## 10. 任务详情抽屉

任务详情抽屉从主区域右侧滑出，覆盖约 1/3 屏幕。

字段：

```text
任务标题
状态
优先级
标签
截止日期
详细描述
附件列表
创建时间
更新时间
完成时间
```

按钮：

```text
保存
取消
删除
归档
添加附件
```

附件区域支持：

```text
拖拽文件到附件区域
点击“添加附件”选择文件
打开附件
定位附件
删除附件
```

第一阶段说明：

```text
详情抽屉实现基础展示和编辑。
附件列表必须实现。
Markdown 编辑器暂不实现。
复杂附件预览暂不实现。
```

---

## 11. 备忘详情抽屉

备忘详情抽屉从主区域右侧滑出，覆盖约 1/3 屏幕。

字段：

```text
备忘标题
备忘正文
标签
附件列表
创建时间
更新时间
```

按钮：

```text
保存
取消
删除
归档
添加附件
```

备忘不显示：

```text
状态
优先级
截止日期
完成时间
```

---

## 12. 搜索与筛选规则

### 12.1 搜索

搜索范围：

```text
任务标题
任务描述
任务标签
备忘标题
备忘正文
备忘标签
附件原始文件名
```

搜索行为：

```text
输入即时过滤
清空搜索恢复全部
搜索无结果时显示空状态
```

### 12.2 筛选

第一阶段只做基础筛选：

```text
按状态
按优先级
按日期
是否有附件
```

复杂组合筛选可以后置。

---

## 13. 视图过滤规则

| 入口 | 显示内容 |
|---|---|
| 全部任务 | 所有未归档 TaskItem，不包含备忘 |
| 今日任务 | DueDate 为今天的未归档 TaskItem |
| 高优先级 | Priority = High 的未归档 TaskItem |
| 完成记录 | Status = Done 且 IsArchived = false 的 TaskItem |
| 归档 | IsArchived = true 的 TaskItem 和 NoteItem |
| 看板 | 未归档 TaskItem + 未归档 NoteItem |
| 日历 | 有 DueDate 的 TaskItem |
| 备忘列 | 未归档 NoteItem |

---

## 14. 响应式与窗口适配

推荐窗口尺寸：

```text
1440 × 900
1600 × 900
1920 × 1080
```

最小窗口尺寸建议：

```text
1100 × 720
```

布局规则：

```text
左侧栏固定宽度：256px
顶部栏高度：64px
看板列最小宽度：280px
看板列最大宽度：360px
列间距：24px
卡片间距：16px
主区域内边距：32px
详情抽屉宽度：420px ~ 520px
```

横向滚动规则：

```text
当窗口宽度不足以完整显示五列时
主看板区域启用横向 ScrollViewer
列宽不应被压缩到 280px 以下
```

---

## 15. 样式 Tokens

### 15.1 颜色

| 用途 | HEX |
|---|---|
| 应用背景 | `#F8F9FA` |
| 主内容背景 | `#FCFCFD` |
| 左侧栏背景 | `#F1F3F5` |
| 顶部栏背景 | `#FFFFFF` |
| 主文字 | `#212529` |
| 辅助文字 | `#868E96` |
| 边框 | `#DEE2E6` |
| 选中背景 | `#DDE3EB` |

### 15.2 卡片颜色

| 类型 | 背景 | 状态条 |
|---|---|---|
| 待办 | `#FFFBF0` | `#8B8682` |
| 进行中 | `#E7F5FF` | `#3B82F6` |
| 卡住 | `#FFF4E6` | `#F97316` |
| 已完成 | `#EBFBEE` | `#22C55E` |
| 备忘 | `#FFF9DB` | 无 |

### 15.3 字体

WPF 字体栈建议：

```text
Segoe UI Variable
Segoe UI
Microsoft YaHei UI
Microsoft YaHei
```

字号：

```text
页面标题：18px / 20px
列标题：18px / 20px
卡片标题：14px
卡片正文：12px / 13px
标签：11px
辅助文字：11px
导航文字：14px
```

### 15.4 圆角

```text
菜单项：12px
按钮：12px
输入框：10px
卡片：10px
看板列容器：12px
详情抽屉：16px
```

### 15.5 阴影

普通卡片：

```text
0 4px 12px rgba(0,0,0,0.05)
```

Hover 卡片：

```text
0 6px 16px rgba(0,0,0,0.08)
```

Dragging 卡片：

```text
0 12px 24px rgba(0,0,0,0.12)
```

详情抽屉：

```text
-8px 0 24px rgba(0,0,0,0.08)
```

---

## 16. 数据模型

### 16.1 TaskItem

```csharp
public class TaskItem
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public TaskStatus Status { get; set; }
    public TaskPriority Priority { get; set; }

    public ObservableCollection<string> Tags { get; set; } = new();

    public DateTime? DueDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public bool IsArchived { get; set; }
    public int SortOrder { get; set; }

    public ObservableCollection<AttachmentItem> Attachments { get; set; } = new();
}
```

### 16.2 NoteItem

```csharp
public class NoteItem
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;

    public ObservableCollection<string> Tags { get; set; } = new();

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public bool IsArchived { get; set; }
    public int SortOrder { get; set; }

    public ObservableCollection<AttachmentItem> Attachments { get; set; } = new();
}
```

### 16.3 AttachmentItem

```csharp
public class AttachmentItem
{
    public Guid Id { get; set; }

    public AttachmentOwnerType OwnerType { get; set; }
    public Guid OwnerId { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;

    public string FileExtension { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }

    public DateTime CreatedAt { get; set; }
    public int SortOrder { get; set; }
}
```

### 16.4 Enums

```csharp
public enum TaskStatus
{
    Todo,
    Doing,
    Blocked,
    Done
}

public enum TaskPriority
{
    Low,
    Medium,
    High
}

public enum AttachmentOwnerType
{
    Task,
    Note
}
```

---

## 17. SQLite 数据库设计

### 17.1 数据库路径

推荐路径：

```text
%LocalAppData%\Kanban41\data\Kanban41.db
```

不要放在：

```text
程序安装目录
桌面
临时目录
```

### 17.2 表结构建议

#### Tasks

```sql
CREATE TABLE IF NOT EXISTS Tasks (
    Id TEXT PRIMARY KEY,
    Title TEXT NOT NULL,
    Description TEXT NOT NULL DEFAULT '',
    Status TEXT NOT NULL,
    Priority TEXT NOT NULL,
    TagsJson TEXT NOT NULL DEFAULT '[]',
    DueDate TEXT NULL,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    CompletedAt TEXT NULL,
    IsArchived INTEGER NOT NULL DEFAULT 0,
    SortOrder INTEGER NOT NULL DEFAULT 0
);
```

#### Notes

```sql
CREATE TABLE IF NOT EXISTS Notes (
    Id TEXT PRIMARY KEY,
    Title TEXT NOT NULL,
    Content TEXT NOT NULL DEFAULT '',
    TagsJson TEXT NOT NULL DEFAULT '[]',
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    IsArchived INTEGER NOT NULL DEFAULT 0,
    SortOrder INTEGER NOT NULL DEFAULT 0
);
```

#### Attachments

```sql
CREATE TABLE IF NOT EXISTS Attachments (
    Id TEXT PRIMARY KEY,
    OwnerType TEXT NOT NULL,
    OwnerId TEXT NOT NULL,
    OriginalFileName TEXT NOT NULL,
    StoredFileName TEXT NOT NULL,
    RelativePath TEXT NOT NULL,
    FileExtension TEXT NOT NULL,
    FileSizeBytes INTEGER NOT NULL,
    CreatedAt TEXT NOT NULL,
    SortOrder INTEGER NOT NULL DEFAULT 0
);
```

#### AppSettings

```sql
CREATE TABLE IF NOT EXISTS AppSettings (
    Key TEXT PRIMARY KEY,
    Value TEXT NOT NULL
);
```

#### 推荐索引

```sql
CREATE INDEX IF NOT EXISTS IX_Tasks_Status ON Tasks(Status);
CREATE INDEX IF NOT EXISTS IX_Tasks_DueDate ON Tasks(DueDate);
CREATE INDEX IF NOT EXISTS IX_Tasks_IsArchived ON Tasks(IsArchived);
CREATE INDEX IF NOT EXISTS IX_Notes_IsArchived ON Notes(IsArchived);
CREATE INDEX IF NOT EXISTS IX_Attachments_Owner ON Attachments(OwnerType, OwnerId);
```

---

## 18. 附件保存流程

### 18.1 添加附件流程

```text
1. 用户拖拽文件到卡片或详情抽屉。
2. WPF 判断 DataObject 是否包含 FileDrop。
3. 校验文件数量和大小。
4. 生成 AttachmentId。
5. 生成安全文件名 StoredFileName。
6. 根据 OwnerType 和 OwnerId 创建附件目录。
7. 将文件复制到附件目录。
8. 写入 Attachments 表。
9. 刷新对应卡片附件数量。
10. 显示成功提示。
```

### 18.2 异常处理

如果复制文件失败：

```text
不写入 SQLite
显示错误提示
```

如果 SQLite 写入失败：

```text
删除已经复制的本地文件
显示错误提示
```

如果删除附件文件失败：

```text
保留数据库记录
显示“文件删除失败”提示
```

### 18.3 推荐服务拆分

```text
IAttachmentStorageService
├── CopyFilesToAttachmentFolderAsync(...)
├── DeleteAttachmentFileAsync(...)
├── OpenAttachmentAsync(...)
└── RevealAttachmentInExplorerAsync(...)

IAttachmentRepository
├── AddAsync(...)
├── DeleteAsync(...)
├── GetByOwnerAsync(...)
└── CountByOwnerAsync(...)
```

---

## 19. 推荐 WPF 控件拆分

```text
MainWindow.xaml
SideNavControl.xaml
TopNavControl.xaml
BoardView.xaml
CalendarView.xaml
HistoryView.xaml
ArchiveView.xaml
BackupView.xaml
SettingsView.xaml

KanbanColumnControl.xaml
TaskCardControl.xaml
NoteCardControl.xaml
TaskDrawerControl.xaml
NoteDrawerControl.xaml
AttachmentListControl.xaml
AttachmentDropZoneControl.xaml
SearchBoxControl.xaml
EmptyStateControl.xaml
```

---

## 20. 推荐 ViewModel 拆分

```text
MainWindowViewModel
SideNavViewModel
TopNavViewModel
BoardViewModel
KanbanColumnViewModel
TaskCardViewModel
NoteCardViewModel
TaskDrawerViewModel
NoteDrawerViewModel
AttachmentItemViewModel
CalendarViewModel
BackupViewModel
SettingsViewModel
```

---

## 21. 推荐服务层

```text
IDatabaseService
ITaskRepository
INoteRepository
IAttachmentRepository
IAttachmentStorageService
ISearchService
IBackupService
ISettingsService
IFileDialogService
IToastService
```

---

## 22. Commands 建议

```text
CreateTaskCommand
CreateNoteCommand
UpdateTaskCommand
UpdateNoteCommand
DeleteTaskCommand
DeleteNoteCommand
ArchiveTaskCommand
ArchiveNoteCommand

OpenTaskDrawerCommand
OpenNoteDrawerCommand
CloseDrawerCommand

MoveTaskCommand
ReorderTaskCommand
ReorderNoteCommand

AttachFilesCommand
OpenAttachmentCommand
RevealAttachmentCommand
DeleteAttachmentCommand

SearchCommand
ClearSearchCommand
ChangeViewCommand
BackupCommand
RestoreBackupCommand
```

---

## 23. 数据备份规则

因为附件文件不存入 SQLite，所以备份必须同时包含：

```text
Kanban41.db
attachments 文件夹
```

推荐备份格式：

```text
Kanban41_Backup_yyyyMMdd_HHmmss.zip
```

备份包结构：

```text
Kanban41_Backup_20260512_153000.zip
├── Kanban41.db
└── attachments\
```

导出 JSON 可以作为辅助功能，但不能替代完整备份。

---

## 24. 第一阶段开发目标

第一阶段必须实现：

```text
1. WPF 主窗口 Shell
2. SQLite 数据库初始化
3. Tasks / Notes / Attachments 表结构
4. 左侧导航
5. 顶部栏
6. 五列看板
7. TaskCardControl
8. NoteCardControl
9. 基础数据绑定
10. 任务详情抽屉
11. 备忘详情抽屉
12. 基础搜索
13. 任务卡片跨列拖拽
14. 备忘卡片仅在备忘列内排序
15. 从文件资源管理器拖拽文件到卡片并保存为本地附件
16. 附件列表展示
17. 打开附件
18. 删除附件
19. 窄窗口横向滚动
```

第一阶段暂不实现：

```text
复杂日历逻辑
Markdown 编辑器
附件预览
图片缩略图
自动备份
复杂设置页
深色模式
全文索引
OCR
云同步
```

---

## 25. 第一阶段验收标准

完成后应满足：

```text
1. 项目可以正常编译和启动。
2. SQLite 数据库可以自动创建。
3. 主界面布局与设计稿基本一致。
4. 左侧栏、顶部栏、五列看板显示正常。
5. TaskItem 正确显示在待办、进行中、卡住、已完成四列。
6. NoteItem 正确显示在备忘列。
7. 任务卡片和备忘卡片视觉区分明显。
8. 点击任务卡片可以打开右侧任务详情抽屉。
9. 点击备忘卡片可以打开右侧备忘详情抽屉。
10. 搜索框可以按标题、描述、标签过滤任务和备忘。
11. 任务卡片可以在四个任务列之间拖拽。
12. 备忘卡片不能拖入任务列。
13. 任务卡片不能拖入备忘列。
14. 从 Windows 文件资源管理器拖拽文件到任务卡片，可以保存为附件。
15. 从 Windows 文件资源管理器拖拽文件到备忘卡片，可以保存为附件。
16. 附件文件被复制到本地 attachments 目录。
17. SQLite Attachments 表记录附件元数据。
18. 重启软件后，附件仍然可见。
19. 原始文件删除后，应用内附件仍然可打开。
20. 删除附件后，数据库记录和本地附件文件同步删除。
21. 看板区域在窄窗口下可以横向滚动。
22. 不出现登录、团队、评论、头像、报告统计等功能。
23. 不出现任何网络上传行为。
```

---

## 26. Codex 执行提示词

可以把下面这段直接交给 Codex：

```text
请根据 docs/Kanban41-WPF-开发交付说明.md 实现 Kanban41 个人看板第一阶段版本。

技术要求：
- 使用 WPF。
- 使用 MVVM。
- 推荐使用 CommunityToolkit.Mvvm。
- 使用 SQLite 作为本地主数据库。
- 推荐使用 Microsoft.Data.Sqlite。
- 不使用 WinForms。
- 不使用 WebView 实现主界面。
- 不实现登录、团队、云同步、评论、报告统计。
- 所有数据保存在本地。
- 附件文件复制到本地 attachments 目录，SQLite 只保存附件元数据。

第一阶段必须实现：
1. MainWindow Shell。
2. SideNavControl。
3. TopNavControl。
4. BoardView 五列看板。
5. TaskCardControl。
6. NoteCardControl。
7. TaskDrawerControl。
8. NoteDrawerControl。
9. SQLite 初始化。
10. Tasks / Notes / Attachments 表。
11. 基础数据读取和保存。
12. 任务卡片跨列拖拽。
13. 备忘卡片仅在备忘列排序。
14. 从 Windows 文件资源管理器拖拽文件到卡片，保存为本地附件。
15. 附件列表展示、打开、删除。
16. 基础搜索。
17. 横向滚动适配。

请优先保证：
- 项目可以编译运行。
- 主界面结构正确。
- SQLite 数据可以持久化。
- 文件附件在重启后仍然存在。
- 不引入任何云同步或网络上传逻辑。
```

---

## 27. AGENTS.md 建议内容

可以在项目根目录创建：

```text
AGENTS.md
```

内容如下：

```text
# Kanban41 Development Rules

This is a Windows local personal kanban application.

Core stack:
- WPF
- MVVM
- SQLite
- CommunityToolkit.Mvvm
- Microsoft.Data.Sqlite

Do:
- Keep all data local.
- Store the main database in SQLite.
- Store attachment files in the local attachments folder.
- Store attachment metadata in SQLite.
- Use native WPF controls and ResourceDictionary styles.
- Keep the UI minimal, bright, and note-like.
- Separate TaskItem and NoteItem.
- Treat Notes as a separate entity, not a TaskStatus.

Do not:
- Add login.
- Add user accounts.
- Add team collaboration.
- Add comments.
- Add task assignment.
- Add cloud sync.
- Add reports or dashboards.
- Add WebView as the main UI.
- Store attachment binary files as SQLite BLOBs.
- Upload files to any server.

First milestone:
- MainWindow shell.
- Board view.
- Five columns.
- SQLite persistence.
- Task and note cards.
- Right drawer editor.
- Drag task cards between task columns.
- Drag files from Windows Explorer onto cards and save them as local attachments.
```

---

## 28. 备注

当前版本重点是完成第一阶段最小可用闭环：

```text
SQLite 本地持久化
任务/备忘分离
文件拖拽保存为本地附件
无网络上传
无团队协作
主看板可用
```

后续可在稳定版本基础上逐步扩展：

```text
Markdown 编辑器
图片缩略图
附件预览
自动备份
全文搜索
深色模式
快捷键
标签管理
日历增强
```
