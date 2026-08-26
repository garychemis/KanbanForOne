# WPF DataGrid Style Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将设计条件详情弹窗的图纸 DataGrid 改为柔和卡片式，并保持现有功能与可访问性。

**Architecture:** 使用设计条件模块现有 ResourceDictionary 扩展局部画刷和具名控件样式；编辑弹窗仅增加一个圆角裁剪容器和必要的样式引用。业务数据和命令保持原样。

**Tech Stack:** .NET 8、WPF XAML、xUnit、STA UI 冒烟测试

---

### Task 1: 添加失败的样式结构测试

**Files:**
- Modify: `KanbanForOne.Tests/UiSmokeTests.cs`

- [ ] **Step 1: 写入资源和 XAML 结构断言**

在 STA 测试中加载 `DesignConditionControlStyles.xaml`，断言存在 `ConditionDrawingGridChromeStyle`、`ConditionDrawingDeleteButtonStyle` 和 `ConditionDrawingAlternateRowBrush`；读取编辑弹窗 XAML 并断言图纸表格启用 `AlternationCount="2"`，删除按钮引用专用样式。

- [ ] **Step 2: 运行目标测试并确认失败**

Run: `dotnet test KanbanForOne.Tests/KanbanForOne.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~UiSmokeTests`

Expected: FAIL，提示缺少新增资源或 XAML 标记。

### Task 2: 实现方案 A 的 WPF 样式

**Files:**
- Modify: `Modules/DesignConditions/Views/DesignConditionControlStyles.xaml`
- Modify: `Modules/DesignConditions/Views/DesignConditionEditorDialog.xaml`

- [ ] **Step 1: 添加局部主题资源**

新增交替行画刷、删除按钮默认/悬停/按下画刷，以及圆角表格容器样式。

- [ ] **Step 2: 完善表头、单元格和数据行状态**

将表头高度设为 38；数据行按交替索引切换背景，并保留悬停、选中状态；单元格焦点使用主题色边框。

- [ ] **Step 3: 应用圆角容器和删除按钮样式**

用 `Border` 包裹 `DrawingGrid`，设置 `ClipToBounds="True"`；DataGrid 设置 `AlternationCount="2"`；删除按钮引用 `ConditionDrawingDeleteButtonStyle`。

- [ ] **Step 4: 运行目标测试并确认通过**

Run: `dotnet test KanbanForOne.Tests/KanbanForOne.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~UiSmokeTests`

Expected: PASS。

### Task 3: 回归与视觉验证

**Files:**
- Verify: `Modules/DesignConditions/Views/DesignConditionControlStyles.xaml`
- Verify: `Modules/DesignConditions/Views/DesignConditionEditorDialog.xaml`

- [ ] **Step 1: 运行完整测试**

Run: `dotnet test KanbanForOne.sln -c Release --no-restore --logger "console;verbosity=minimal"`

Expected: 全部测试通过。

- [ ] **Step 2: 执行格式与差异检查**

Run: `dotnet format KanbanForOne.sln --verify-no-changes --no-restore`

Expected: exit code 0。

Run: `git diff --check`

Expected: 无空白错误。

- [ ] **Step 3: 启动应用进行视觉检查**

检查图纸表格的圆角、交替行、悬停、选中、编辑态和删除按钮状态；确认内容仍全部居中且无裁剪。

本计划不单独创建提交，避免把当前工作区内尚未提交的多图幅功能拆散；提交由项目现有发布流程统一处理。

