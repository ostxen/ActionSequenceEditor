# RF15 双 UI 交付与人工验收

本文件记录验收证据和待验操作，不维护第二份任务状态。唯一任务状态见 `.codex/tasks/action-editor-refactor.md`。本轮只覆盖 ActionSequenceEditor，未改 Combat、包/设置、用户 JSON，未执行 Player Build 或推送。

## 当前阅读入口

两个正式菜单为 `ActionSequenceEditor/IMGUI` 和 `ActionSequenceEditor/UI Toolkit`，窗口分别位于 `ActionSequenceEditor.Views.IMGUI` 与 `.UIToolkit`，均名为 `ActionSequenceEditorWindow`。两窗共享单文档、选择、播放头及Undo，逐窗工作区与捕获独立。

入门操作正文只维护在[功能体验样例教程](../Assets/ActionSequenceEditor/Samples/FeatureSample/README.md)。当前[存储合同](ActionEditor-Contracts.md)为严格 `actioneditor.json` version 2，根组和独立轨道按RootContainers混排。保存、另存、自动保存和独立导出由共享用例与可靠提交实现，导出不改变来源或基线。

最终工程状态与证据只见[唯一任务清单](../.codex/tasks/action-editor-refactor.md)。H01–H09仍为待用户体验的范围，不以自动化测试或截图代签。

以下均为RF15当日记录，保留原始身份、计数与限制，不定义当前产品合同。
## 两个入口与数据合同

- 原版：`NBC/Action Editor/Open Action Editor`，GUILayout/Event 绘制。
- 新版：`JsonActionWorkbench` 原生 UI Toolkit，时间轴为 VisualElement/Painter2D，不用 IMGUIContainer 包裹业务。
- 两窗编辑同一份 FullSerializer JSON、共用文档会话、选择、播放头和 Undo；不是各自加载一份独立文档。捕获、悬停和活动手势归发起窗口。
- 新版缩放按用户选择，滚轮符号×225/时间轴宽×当前跨度，左边固定。其他已记录差异不擅自统一。

## 连续序列证据

`ExtensionEndToEndTests.BothUiSequencesKeepOneGraphThroughCopyDragResizeCancelReopenAndPlayback`：同样例在两 UI 分别创建区间、复制到9秒、多选整体移动0.5秒并保持间距、一次Undo/Redo、右边拉伸0.25秒、属性路径写入后取消、再次提交及Undo/Redo、失败打开保留当前图、保存快照编码、关闭两窗、重新加载编码并打开两窗、验证作者内容完全相同、播放及停止释放。

原版创建执行真实GenericMenu构建结果的回调；原生创建走ContextualMenuPopulateEvent。原版移动/拉伸通过专用IMGUI事件窗口路由到真实ActionEditorWindow.Gesture，Toolkit使用实际Pointer事件。这不是OS弹出菜单点击或原版整窗命中路线的完整模拟。属性路径验证取消/Undo语义；真实原版字符串控件及Toolkit延迟文本控件由同套件的`BothMenusAndPropertyControlsProduceEquivalentPointAndIntervalGraphs`覆盖，输入中间态由JsonPropertyTests覆盖。

该连续用例内“保存重开”为正式Codec快照/Decode与两窗生命周期，**不是磁盘保存**。磁盘原子提交、导入、锁定/外部修改保护及会话恢复由独立授权的固定 `Assets/ActionEditorValidationR10/Shared.json` 探针验证。不新建额外磁盘目标，不把内存证据替代持久证据。系统新建/另存/保存对话框仍需人工。

首次130912Z-5545f8b7连接在编译前中断，未执行测试；doctor131006Z-6b7776e6恢复。131031Z-45673fbe实际1失败，原因为故意失败打开产生预期FullSerializer错误日志未声明；仅补LogAssert.Expect，没有改产品或放宽数据断言。131240Z-8e4135c1该连续用例1/1通过。

最终验证：131504Z-00abb773真实Unity编译及NBC.ActionEditor.Editor.Tests EditMode **164/164**，0失败/跳过/不确定；131504Z-10f4691a工作流**42/42**、真实18项目656源码方案**0错误/43既有警告**。完整code profile文档解析缺依赖仍blocked。固定持久探针preview132023Z-e4f911e3→approve132118Z-7c09ce61→apply132134Z-fc1a3b6e，**14项通过、31287bytes**；原会话恢复，Glob确认自有目录/meta无残留。

## U01–U14 工程证据矩阵

“通过”仅指列明的可执行检查，不代表所有子细节或人工体验已验。下列SharedJsonTests名称可在源码搜索定位；最终完整报告及持久报告见唯一任务执行记录。

| ID | GUILayout结果及证据 | Toolkit结果及证据 | 不适用/差异/人工剩余 |
|---|---|---|---|
| U01 布局 | 工程检查：SharedLayoutEvidenceCapturesBothOwnedWindows、OriginalInspectorWelcomeAndSettingsHaveNativeCounterparts、BaselineWindowNavigation… | 同轮真实窗口布局、欢迎页、独立属性面板、最小尺寸/滚动/分栏及原生控件断言 | H01逐像素文字/图标/颜色/提示、最小窗口外观与其他DPI待用户，不以截图代签 |
| U02 文档/菜单 | JSON选择器过滤、非JSON拒绝；OriginalCreate真实菜单回调；失败打开保留图 | 同选择器；NativeCreate原生上下文事件及LastError检查 | 系统新建/保存/另存/关闭弹窗取消、搜索/键盘/双击真人路径H07；磁盘边界共用且独立探针 |
| U03 选择/焦点 | SelectionCandidates…、双窗口开序/同步、原版多选真实事件 | 同步、NativeTextFocus…、多选拖动保留间距 | 原版替换/Toolkit Ctrl追加及保留选中差异保留；真人框选正反/当前项/跨窗焦点H02 |
| U04 鼠标路由 | OriginalInputNeedsPressAndEndsOutside、OriginalMiddlePanReleaseOutside…、每窗Gesture隔离 | NativeWrongButtonReleaseAndLostFocus…、无按下移动、捕获丢失、当前手势阻断菜单 | 原版起拖阈值与Toolkit即时拖动、分隔条最小值未统一；OS右键菜单H02/H04/H08 |
| U05 移动/排序 | OriginalMultiMoveClampsOneDeltaAndKeepsSpacingAtZero、OriginalInvalidStretchRestoresLengthAndTrackReorderIsUndoable | NativeMultiClipDragGuides…、连续序列创建复制后多选移动 | 共享结构命令锁定/挂载/唯一性/批量失败回滚通过；原版静态几何仍是过渡边界，跨组落点真人反馈H02 |
| U06 伸缩/能力 | StretchRefreshesMagnetReferences…、连续序列真实StretchEnd、邻居真值表 | NativeDragResizeCancelAndCaptureLoss…、NativeClipEdgesShowResizeCursor…、短片段左右优先级 | 原版最小0.1/严格邻居，Toolkit0.01/原有重叠；点不可伸缩、Blend=-1能力保留；光标/交叉混合真人H03 |
| U07 吸附/反馈 | 候选阈值/平局、拉伸前参考刷新、原版移动和Shift事件 | NativeMultiClipDragGuides…验证实/虚线、边界、辅助线取消/捕获清理 | 原版含阈值最近起点优先，Toolkit严格阈值遍历优先；网格/临时绕过策略仍有既有差异，H03 |
| U08 导航 | BaselineWindowNavigation…实窗滚轮/中键平移/尺寸，取消框选状态清理 | 同序列；轮滚取消手势、几何不因滚动条改变 | 共同225步进；原版忽略wheel偏好、过零缩短视域，Toolkit尊重偏好且保跨度；底范围/真人手感H04 |
| U09 播放 | ClosingOriginalStopsSharedPlaybackAndReopeningDoesNotResume、双窗共享唯一驱动 | RulerContinuousSeekAndEscapeRestoreTime、BothWindowsKeepExactlyOneSharedEditorDriver | 暂停/步进/循环/异常释放共用Playback测试；原版标尺完整真人路径及实时节奏H05，未以600次同步循环冒充600真实帧 |
| U10 快捷键 | 原版现有菜单共用Copy/Paste/Delete/Duplicate命令；原版真实属性输入覆盖 | NativeTextFocusDoesNotRunTimelineShortcuts…、原生延迟输入Return/Esc/Blur | 不新增原版原先没有的快捷键；文本撤销/系统保存快捷键完整桌面焦点链H06 |
| U11 属性/集合 | OriginalCollectionDeleteClicks…、NullCollectionFoldouts…、实际字符串输入 | NativeFieldsUseSharedDescriptions…、NativeDelayedInput… | 条件/选项真实7/20、嵌套struct、数组/null首项/边界共享测试通过；批量Mixed UI和集合重排按钮原先未提供，非本轮新增功能；第三方自定义控件H07 |
| U12 终止/资源 | OriginalWindowsOwnIndependentGestureSnapshots…、另一窗口token不能被取消、窗口关闭停止 | InactiveOriginalLifecycleDoesNotCancelNativeOwnedDrag、InactiveNativeCancellationDoesNotTouchOriginalWindowToken、Esc/失焦/Detach/捕获丢失 | 真实退出已在RF14受控执行；预览资源部分初始化/异常/重复释放通过；任意业务资源及完整域重载恢复H09 |
| U13 一致性 | 一手势一次Undo、多次相反操作、取消/失败整图恢复 | 同共用Session及真实拖动/延迟输入/连续序列 | 探针覆盖保存后Undo/Redo、放弃不复活、浮点、占用/外部版本保护；导入失败现场/跨进程Unity引用仍缺，不修改格式 |
| U14 扩展/复杂内容 | TypeCatalogTests元数据/继承/冲突、原版菜单/继承Inspector/隐藏几何 | 同目录原生缺适配提示、点/区间真实菜单/字段创建；无IMGUI业务代绘 | RF13无Combat扩展证明通过；旧实验Workbench的dense测试不是当前JSON密集真实业务证明，未知Header/第三方控件/最大文档仍缺 |

## 保留的差异与边界

1. 全部窗口仍共用一个文档，切换一窗会切换另一窗；需要多文档不是本轮目标。
2. 除225缩放步进外，选择追加、网格、磁吸边界、最短长度、交叉重叠、过零平移和起拖阈值等沿既定基线分别保留。是否进一步统一由用户体验验收决定。
3. 不声称任意第三方Inspector/Header自动适配Toolkit。缺适配必须显示提示，不嵌入旧IMGUI业务。当前只首选属性，无批量混合输入或凭空增加集合重排UI。
4. RF14证据是当前13片段样例、受控新进程及本轮RF13→RF14对照，不是全系列性能提升证明，也不是最大真实业务文档/峰值内存认证。
5. Unity2022.3.62f3、Windows、DPI1.5为本轮实际环境。其他平台/DPI、完整域重载状态恢复、跨进程Unity对象引用、任意外部业务副作用和Player Build未验。
6. 完整code profile文档解析缺yaml/markdown_it，未授权安装，仍标blocked；工作流测试和真实方案构建分别列结果。

## 用户最后验收清单（H01–H09，全部待验）

使用自己的可丢弃JSON副本，先保存其他工作。分别打开两个入口，检查以下操作；发现差异记录“窗口、初态、按键/鼠标、预期、实际”，不需要改Shader：

- H01：欢迎页、顶部/文档栏、标尺、轨道、底范围、独立Inspector；拖动分隔条及最小窗口，比较文字/图标/颜色/提示。
- H02：空白/片段/轨道/组选择，Ctrl、反向框选、跨轨多选，双窗交替，复制后再移动和排序提示。
- H03：单/多选过零与邻居、左右拉伸、短区间/点、磁吸开关及Shift、辅助线/边缘光标、取消后反向重拖。
- H04：两窗分别滚轮、轨道头滚动、中键拖出后释放、底部范围拖动/双击；确认225缩放步进手感和保留差异。
- H05：标尺定位/连续拖动/Esc、播放/暂停/停止/循环/步进，双窗刷新，关闭原版后不继续播放。
- H06：属性文本编辑中Delete/复制/剪切/粘贴/Undo/Esc不能误操作时间轴，保存快捷键与失焦行为。
- H07：真实创建/打开/保存/另存/关闭弹窗和取消；null集合首项/增删/条件/选项/资源字段，第三方自定义控件。
- H08：OS右键菜单、未知已有窗口和辅助弹窗，不强关不认识的窗口。
- H09：活动手势中切窗/关闭、关闭重开、切文档、Undo/放弃、按需要人工域重载，检查事务/播放/临时资源无残留。

工程验收不会自动勾选上述人工项。数据安全、编译或自动测试失败不能用“人工待验”豁免。
