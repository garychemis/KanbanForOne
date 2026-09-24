# 亮暗主题与卡片悬停展开 Review 报告

> 以下保留修复前的审查记录；后续修复验收及最新卡片展示规则见文末。

- 审查日期：2026-09-24
- 审查基线：Git `3693bc5` 与当前工作区之间的未提交变更，包含 43 个已跟踪文件变更、7 个新增文件；不包含本报告。
- 需求范围：保留原亮色主题，新增暗色与切换；业务卡片默认仅显示标题，悬停时自动增高展示前四行详情，移开后恢复。
- 审查方式：主审复核，加上主题与持久化、卡片与布局、通用控件与测试三个独立子审查。
- 本轮仅审查并输出报告，未修改产品实现。

## 审查结论

**建议修复下述 1 项 P2 问题，并补齐关键交互验证后再合入。** 未发现有充分证据支持的 P0/P1 问题；主题初始化、偏好保存、资源切换及卡片四行预览的主要实现未发现其他已确认缺陷。

本轮重新执行 Release 测试，**106 项通过、0 项失败、0 项跳过**，`git diff --check` 通过。但现有测试不能证明所有键盘、真实鼠标和动画完成后的视觉状态均正确。下文将已确认的产品缺陷与验证缺口分开记录。

## 已确认问题

### F-01 · P2：月份／年份选择网格失去键盘焦点提示

**位置**：[Styles/ModernControls.xaml:111](<D:/coding/c#/c#_projects/KanbanForOne/Styles/ModernControls.xaml:111>)，`ThemedCalendarMonthStyle` 的 `CalendarButton` 模板及其 114–118 行状态触发器。

**触发方式**：

1. 打开使用 `DrawerDatePickerStyle` 的日期弹窗，例如人工时录入中的日期选择器。
2. 点击弹窗顶部的月份标题，进入月份选择网格。
3. 使用方向键移动键盘焦点；进入年份选择网格后执行相同操作。

**实际行为与影响**：新模板只有背景和内容，未保留原生 `CalendarButton` 的焦点视觉状态及焦点边框。`HasSelectedDays` 只标记已选日期所在的月份／年份，不能表示键盘当前移动到的目标。因此方向键移动时，用户无法从界面确认下一次确认操作将选中哪一项。这是本轮替换原生模板引入的键盘交互回归，亮暗主题均受影响。

**证据**：静态代码未定义焦点状态；定向单控件探针对比了当前源 XAML 与原生 WPF 模板：原生模板在焦点状态下显示 `CalendarButtonFocusVisual`，当前模板没有等效元素／状态，最终 `FocusVisualStyle` 也没有提供有效的焦点绘制。现有 UI 测试通过直接切换 `DisplayMode`、设置 `SelectedDate` 来验证日期控件，因此不会捕获这一问题。

探针通过 `FocusManager.SetFocusedElement` 和反射设置只读键盘焦点属性来构造同一状态，未操作真实桌面键盘。关键结果如下，可用 [review-controls-probe.ps1](<D:/coding/c#/c#_projects/KanbanForOne/artifacts/ui-refresh/review-controls-probe.ps1>) 复核：

```text
Native: IsFocused=True, IsKeyboardFocused=True, HasSelectedDays=False, FocusVisual=Visible
Themed: IsFocused=True, IsKeyboardFocused=True, HasSelectedDays=False, FocusVisual=absent
```

**修复建议**：恢复原生 CalendarButton 的焦点状态及可见边框，或者提供跟随该控件实际焦点状态的等效样式。补充进入月份／年份网格后，方向键移动、确认选择和退出弹窗的交互验证；同时在亮暗主题下检查焦点与选中态能明确区分。

## 已确认的验证缺口

### G-01：任务详情截图没有等待内容真正显示

**位置**：[KanbanForOne.Tests/UiSmokeTests.cs:193](<D:/coding/c#/c#_projects/KanbanForOne/KanbanForOne.Tests/UiSmokeTests.cs:193>) 和 [UiSmokeTests.cs:442](<D:/coding/c#/c#_projects/KanbanForOne/KanbanForOne.Tests/UiSmokeTests.cs:442>)。

`SavePreview` 固定等待 300 ms；`VerifyEditors` 只等待 `IsSpotlightOpen`，没有断言详情标题、正文、操作按钮已经显示。现有弹窗先执行 250 ms 外壳动画，再经 Dispatcher 执行 120 ms 内容动画，固定 300 ms 不足以保证详情已完整渲染。

本轮 106 项测试全部通过时生成的 [亮色任务详情截图](<D:/coding/c#/c#_projects/KanbanForOne/artifacts/ui-refresh/theme-hover/Task-details.png>) 和 [暗色任务详情截图](<D:/coding/c#/c#_projects/KanbanForOne/artifacts/ui-refresh/theme-hover/Dark-Task-details.png>) 都只显示面板外壳，没有实际详情内容。因此，这两张截图不能作为任务详情已完成亮暗主题视觉验证的证据。

**建议**：等待实际详情容器可见、透明度完成，并断言任务标题及关键操作控件可见之后再截图。使用含正文、Markdown、标签和附件的样本覆盖完整详情。此项是已经观察到的测试／证据缺口，**不等同于已确认正常使用时详情永久空白**；弹窗动画代码本轮没有改动。

### G-02：反射悬停测试不能覆盖真实鼠标命中与滚动反馈

**位置**：[KanbanForOne.Tests/CardHoverAssertions.cs:226](<D:/coding/c#/c#_projects/KanbanForOne/KanbanForOne.Tests/CardHoverAssertions.cs:226>)。

测试通过反射同时设置 WPF 内部鼠标状态缓存与 `IsMouseOver` 依赖属性，再主动更新绑定和布局。这能够验证四行上限、展开及收起高度、空详情以及月份容器的尺寸，但没有经过真实输入管理器的命中测试。

卡片审查另运行了独立视觉树命中探针：42 天、每日至 3 张任务、1050×480 视口；最后一周展开后滚动范围从 480 增至 603.85，滚到底部偏移为 123.85。在第 1／2／3 张卡与日期空白处之间切换命中点，布局均能稳定，未发现循环展开／收起。此探针仍以视觉树和反射模拟状态，不能替代真实输入验证。

**建议**：补充真实指针场景：同一天多张卡片间移动、最后一周展开后滚动、跨卡片快速移动、展开状态下开始拖拽及取消。验证卡片不会因布局和滚动改变而反复展开／收起，且点击和拖拽目标仍正确。目前这些属于待验证场景，不能直接认定存在抖动或误操作缺陷。

### G-03：暗色冷启动与部分原生交互尚未端到端覆盖

主题偏好测试验证了配置读写、损坏回退、保存失败以及服务重建，但没有覆盖“保存暗色 → 退出进程 → 再次启动 → 首屏显示暗色”的完整路径。

日期控件和菜单模板也缺少真实输入下的完整回归，例如方向键选日／选月、确认与取消、文本框原生上下文菜单、子菜单和访问键。建议优先补齐应用现有入口涉及的交互，无需为未使用功能扩大实现范围。

## 已通过核查的内容

| 领域 | 核查结果 |
| --- | --- |
| 色板完整性 | Light/Dark 各 247 个资源键，包含 213 个画刷和 34 个 Color；键集一致，未发现资源引用缺失。 |
| 亮色保留 | 原有命名颜色及迁出的局部命名画刷与亮色色板对比，未发现颜色值不一致。 |
| 主题切换 | 初始化在主窗口创建前完成；共享画刷通过颜色绑定避免冻结；现有资源身份及颜色往返切换测试通过。 |
| 偏好持久化 | 新安装默认亮色；损坏／未知配置回退；临时文件替换写入及写入失败反馈均有覆盖。 |
| 卡片预览 | 普通及归档任务、便签、日历任务、人工时、设计条件卡片的折叠／展开、四行上限和恢复高度测试通过。 |
| 月份布局 | 实际 42 格容器的默认高度、最后一周增高、滚动范围、其他周高度和收起恢复测试通过。 |
| 原有行为 | 打开命令、完成状态删除线及既有业务测试通过；真实指针拖拽的验证边界见 G-02。 |
| 视觉抽查 | 亮暗设置页、卡片三态、月份展开及设计条件编辑弹窗已抽查；任务详情截图的限制见 G-01。 |

## 验证记录

```powershell
$env:KANBAN_UI_PREVIEW_DIR = 'D:\coding\c#\c#_projects\KanbanForOne\artifacts\ui-refresh\theme-hover'
dotnet test KanbanForOne.Tests -c Release --no-restore
git diff --check
```

- Release 构建成功；本轮测试结果：106 通过、0 失败、0 跳过，测试耗时约 34 秒。
- 界面截图位于 `artifacts/ui-refresh/theme-hover/`，使用测试数据库和测试样本。
- 本报告不将测试通过等同于交互无缺陷；F-01 是现有测试之外确认的产品问题，G-01 至 G-03 是需要补齐的验证边界。

## 建议处理顺序

1. 修复 F-01 的键盘焦点提示，并验证月份／年份选择。
2. 按 G-01 等待详情内容完成显示后重新生成并审阅亮暗截图。
3. 完成 G-02 的真实鼠标／滚动／拖拽验证及 G-03 的暗色冷启动验证，再进行发布验收。

## 2026-09-24 后续修复与验收

本次按报告修复，并将暗色主题改为中性石墨深灰。亮色配色保持原值。

| 项目 | 修复与验证结果 |
| --- | --- |
| F-01：键盘焦点 | 日、月、年按钮新增独立的 2 DIP 焦点内描边，跟随实际 `IsKeyboardFocused`。亮暗两套主题均通过 WPF 键盘焦点、方向键导航、Enter 进入下一层以及焦点离开后隐藏的验证。未选中按钮也能独立显示焦点。 |
| G-01：详情截图 | 移除统一的 300 ms 固定等待；任务和便签详情等待内容绑定就绪、容器可见、透明度为 1、动画结束，并断言标题、Markdown、标签、附件和编辑／删除操作存在。新的亮暗截图均已人工复核，详情内容完整显示。 |
| G-02：真实鼠标 | 已在隔离窗口通过实际 OS 指针输入验证任务展开、四行高度、点击和收起；同日三张日历卡片往返；最后一周展开后实际滚轮滚动及静止指针稳定性；展开状态进入真实拖拽循环后 Esc 取消。未发现循环抖动、误打开、拖拽残影或无法恢复高度。 |
| G-03：暗色冷启动 | 两个独立 apphost 进程通过真实 `App.OnStartup` 和原 `StartupUri` 启动 `MainWindow`。首进程默认亮色并保存暗色；第二进程在主窗口创建前加载暗色，首个 `ContentRendered` 的主题及窗口底色均为 `#FF181A1F`。所有数据位于隔离探针输出目录。 |
| G-03：原生交互 | 使用生产日期选择器样式，经真实鼠标打开、Right 改日、Esc 取消并恢复原日期，以及再次 Right／Enter 确认次日；原生 TextBox 通过真实 Ctrl+A 全选、右键打开菜单、Down 高亮菜单项和 Esc 关闭，Copy 命令正确启用，文本与选择保持不变。验证未读写剪贴板。应用未使用的子菜单／访问键不扩大验证范围。 |

### 深灰配色

- 工作区 `#181A1F`，侧栏／列背景 `#202329`，弹窗 `#24272D`，卡片／输入表面 `#292D34`。
- 主文字 `#E9E7E2`，次文字 `#B1B5BE`，雾蓝强调 `#A2B4CF`，主按钮 `#586B85`。
- 任务状态和模块保留少量低饱和灰蓝、灰绿、香槟金及灰玫瑰，避免大面积绿色基底。
- 两份色板各 247 个同名同类型资源；主文字与卡片表面对比 11.19:1，次文字 6.73:1，主按钮白字在普通／悬停／按下状态均高于 4.5:1。

### 本次验收证据

- Release 全量测试：**106 通过、0 失败、0 跳过**，本次约 17 秒；`git diff --check` 通过。
- 独立探针构建：0 警告、0 错误。新增的探针与父测试项目编译隔离，不进入产品程序集。
- 冷启动进程：PID 17648 保存暗色，PID 2504 在首次渲染时恢复暗色。见 [save-dark.json](<D:/coding/c#/c#_projects/KanbanForOne/artifacts/ui-refresh/theme-hover/cold-start/save-dark.json>)、[verify-dark.json](<D:/coding/c#/c#_projects/KanbanForOne/artifacts/ui-refresh/theme-hover/cold-start/verify-dark.json>)。
- 真实指针探针：末周滚轮偏移 48 DIP，`dragStarted=true`、`escapeObserved=true`，任务打开次数保持 1。见 [verify-pointer.json](<D:/coding/c#/c#_projects/KanbanForOne/artifacts/ui-refresh/theme-hover/cold-start/verify-pointer.json>)。
- 已复核 [深灰设置页](<D:/coding/c#/c#_projects/KanbanForOne/artifacts/ui-refresh/theme-hover/Dark-Settings.png>)、[深灰任务详情](<D:/coding/c#/c#_projects/KanbanForOne/artifacts/ui-refresh/theme-hover/Dark-Task-details.png>)、[卡片三态](<D:/coding/c#/c#_projects/KanbanForOne/artifacts/ui-refresh/theme-hover/Dark-Card-hover.png>)、[月份焦点](<D:/coding/c#/c#_projects/KanbanForOne/artifacts/ui-refresh/theme-hover/Dark-Date-picker-month-focus.png>)、便签和设计条件详情。

复现隔离冷启动与真实鼠标检查：在 Windows 桌面会话中运行 `KanbanForOne.Tests/Probes/Run-ThemeColdStart.ps1 -VerifyPointer`。脚本为每次运行新建临时 apphost 目录；真实鼠标检查只操作合成卡片，结束后恢复指针与前台窗口。若已有正式应用实例则拒绝启动探针。UI 冒烟中的月份／年份按键测试使用真实 WPF 焦点及路由按键；真实 OS 指针与拖拽检查另由上述探针覆盖。

**修复后结论：F-01 已修复，G-01／G-02 及 G-03 的当前应用入口验证已补齐；本轮未发现新的待修复产品缺陷。**

## 后续需求调整：仅完成与备忘录悬停展开

按用户最新要求，待办／进行中／卡住恢复最初的常驻展示：看板正文预览、日期、优先级、超期提示、附件数和标签重新显示，归档沿用原紧凑规则；日历中相应状态恢复原有字段布局，人工时与设计条件恢复常驻信息。仅完成任务与备忘录继续默认仅标题、悬停前四行、移开收起。任务状态往返切换时即时更新，不需重新打开页面。

月格高度仅在完成任务的预览实际展开时增高，未完成任务与日期空白处的悬停不会触发展开。月格展示直接跟随 `Task.Status`，避免包装对象不转发通知导致状态切换滞后。

此次 Release 全量测试 **106 通过、0 失败、0 跳过**，约 19 秒；真实 OS 指针探针新增 Todo／Doing／Blocked 内容常驻与高度不变检查，连同完成任务展开、最后一周滚动、拖拽取消、日期及菜单回归均通过。`git diff --check` 通过。

更新后的效果见 [深灰卡片三态](<D:/coding/c#/c#_projects/KanbanForOne/artifacts/ui-refresh/theme-hover/Dark-Card-hover.png>)；最新真实输入结果见 [verify-pointer.json](<D:/coding/c#/c#_projects/KanbanForOne/artifacts/ui-refresh/theme-hover/cold-start/verify-pointer.json>)。
