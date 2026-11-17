# ActionSequenceEditor

基于 Unity 的可扩展行为时间轴编辑器，用于技能、Buff、剧情、战斗镜头与场景表现等内容的可视化编排。通过序列、分组、轨道和片段组织数据，将时间轴编辑、业务数据与预览逻辑结合起来。

## 项目来源

本项目基于 [NoBugCn/ActionEditor](https://github.com/NoBugCn/ActionEditor) 2.0 改造，沿用其行为时间轴思路与类型扩展机制，并围绕编辑器架构、UI Toolkit 界面、数据存储和交互体验进行了整理与扩展。感谢原作者提供的基础实现。

## 主要改造

- **统一产品与代码结构**：产品名、命名空间和插件目录统一为 `ActionSequenceEditor`，整理序列、分组、轨道和片段的命名与职责。
- **双界面工作台**：提供 IMGUI 和原生 UI Toolkit 两套编辑界面，共享当前序列、选择、剪贴板、撤销历史与播放头，各窗口独立保存视域、折叠、滚动和分栏状态。
- **分层编辑架构**：将模型规则、编辑事务、界面呈现、文件存储与预览分别组织，产品程序集收敛为 `ActionSequenceEditor.Runtime` 和 `ActionSequenceEditor.Editor`。
- **时间轴交互完善**：支持根级分组与独立轨道混排、跨组移动、插入位置提示、边缘自动滚动，以及移动后已有名称数字前缀的同步编号。
- **原生 Toolkit 交互**：创建、打开、导出、重命名和资源选择使用 Toolkit 窗口，分类菜单、属性控件与菜单布局适配编辑器字号。
- **统一数据读写**：以 JSON 保存序列，提供保存、另存为、自动保存和独立导出，配合快照、原子文件提交、读回校验与文件版本检查。
- **完整功能样例**：提供区间、点事件、交叉混合、内循环、字段编辑与状态预览示例，覆盖从创建到保存、重开和导出的常用操作。

## 编辑能力

### 时间轴与内容组织

- 通过 `ActionSequence → ActionGroup / ActionTrack → ActionClip` 组织内容。
- 拖动片段、调整区间长度、时间步长与吸附、滚轮缩放、中键平移。
- 分组和轨道排序、轨道归属调整、折叠、锁定与激活状态管理。
- 区间片段、零长度点事件、渐入渐出、交叉混合与内循环编辑。
- 复制粘贴、撤销与重做，以及按完整编辑事务恢复内容。

### 属性与预览

- 编辑文字、数值、布尔值、枚举、颜色、向量、曲线、数组、列表及嵌套业务字段。
- 通过字段声明组织显示名称、选项、条件显示和资源路径选择。
- 编辑器内播放、暂停、停止、范围播放与播放头定位。
- 通过业务预览扩展展示片段状态，并随时间轴位置更新。
- 中文与英文界面，以及字体、时间显示和自动保存等偏好设置。

## 扩展能力

| 扩展方向 | 接入方式 |
| --- | --- |
| 业务数据模型 | 继承 `ActionSequence`、`ActionGroup`、`ActionTrack` 和 `ActionClip`，为技能、Buff、剧情等业务定义专用结构与字段。 |
| 片段行为 | 使用 `PointClip`、`CrossBlendClip` 或 `ISubClipContainable` 表达点事件、混合区间及内循环。 |
| 类型与菜单声明 | 通过 `Name`、`MenuName`、`Category`、`Attachable`、`Unique`、`Color` 等特性配置名称、分类、挂载关系、唯一性与外观。 |
| 自定义工具栏 | IMGUI 使用 `SequenceHeaderExtension` 与 `CustomHeader`；UI Toolkit 使用 `ToolkitHeaderView` 与 `ToolkitHeader`。 |
| 自定义属性界面 | 通过 `CustomInspectors` 或 `ToolkitProperties` 绑定业务类型，结合共享属性描述与提交逻辑构建专用控件。 |
| 业务预览 | 继承 `PreviewBase<T>` 接入时间采样与预览生命周期；通过 `EditorExtensionContext.Capture()` 获取工具栏和状态窗口所需的显示快照。 |
| 数据格式 | 实现 `ISequenceFormat` 并注册到格式目录，声明读取、写入和导出能力。当前内置 JSON 格式可作为接入参考。 |

业务可以围绕同一份序列数据组合模型、属性界面、工具栏和预览，形成项目自己的内容编辑工具。

## 快速开始

1. 使用 **Unity 2022.3.62f3** 打开本工程，等待资源导入与脚本编译完成。
2. 从菜单打开 `ActionSequenceEditor > UI Toolkit` 或 `ActionSequenceEditor > IMGUI`。
3. 点击“新建资源”，选择“功能体验样例（预置轨道）”，创建序列 JSON 文件。
4. 选择片段编辑属性，体验拖动、伸缩、撤销与播放，并通过顶部“样例状态预览”查看时间轴状态。
5. 保存序列，或在保存按钮的右键菜单中选择另存为、导出。

完整操作说明见[功能体验样例教程](Assets/ActionSequenceEditor/Samples/FeatureSample/README.md)。

## 代码导航

插件源码位于 [`Assets/ActionSequenceEditor`](Assets/ActionSequenceEditor)。

| 目录 | 内容 |
| --- | --- |
| `Runtime/Model` | 序列、分组、轨道、片段及时间与结构规则。 |
| `Runtime/Metadata` | 类型声明、字段描述与扩展特性。 |
| `Runtime/Serialization` | 格式接口、JSON 转换与资源引用解析接口。 |
| `Runtime/Samples/FeatureSample` | 样例业务模型、字段与片段类型。 |
| `Editor/Application` | 编辑会话、事务、结构编辑、文档操作与自动保存。 |
| `Editor/Presentation` | 展示状态、属性编辑、时间轴交互与布局计算。 |
| `Editor/Infrastructure` | 文件、资源、预览、类型发现与偏好设置适配。 |
| `Editor/Views` | IMGUI、UI Toolkit 与共享界面资源。 |
| `Editor/Extensions` | 工具栏、属性界面与预览扩展入口。 |
| `Editor/Samples/FeatureSample` | 样例工具栏、状态展示与预览窗口。 |

## 参考资料

- [功能体验样例教程](Assets/ActionSequenceEditor/Samples/FeatureSample/README.md)
- [数据与扩展接口约定](Docs/ActionEditor-Contracts.md)
- [原项目](https://github.com/NoBugCn/ActionEditor)
- [原项目示例](https://github.com/NoBugCn/ActionEditorExample)
