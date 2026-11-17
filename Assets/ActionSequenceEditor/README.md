# ActionSequenceEditor

可扩展的 Unity 行为时间轴编辑器，用于技能、Buff、剧情、战斗镜头与场景表现等内容的可视化编排。

## 来源与改造

基于 [NoBugCn/ActionEditor](https://github.com/NoBugCn/ActionEditor) 2.0 改造，沿用原项目的时间轴模型与类型扩展机制。当前版本统一了产品命名与目录，整理为 Runtime、Editor 两个产品程序集，并加入原生 UI Toolkit 工作台、共享编辑会话、事务式撤销、JSON 存储与独立导出，以及更完整的结构拖动、属性编辑和业务状态预览。

## 核心能力

- IMGUI 与 UI Toolkit 两套界面共享序列、选择、剪贴板、播放头和撤销历史。
- 分组、独立轨道和片段编排，支持排序、跨组移动、锁定、激活和已有名称编号同步。
- 片段拖动与伸缩、时间吸附、渐入渐出、交叉混合、点事件与内循环。
- 常用字段、数组、列表、嵌套数据与条件属性编辑。
- 编辑器内播放、时间定位、业务预览、保存、另存为、自动保存和 JSON 导出。

## 扩展入口

- **业务模型**：继承 `ActionSequence`、`ActionGroup`、`ActionTrack` 和 `ActionClip`。
- **类型声明**：通过名称、分类、挂载关系、唯一性、颜色等特性组织业务类型。
- **工具栏与属性**：为 IMGUI 和 UI Toolkit 分别绑定自定义工具栏与属性控件。
- **预览**：通过 `PreviewBase<T>` 接入时间采样与生命周期，通过显示快照构建业务状态窗口。
- **数据格式**：实现 `ISequenceFormat`，注册读取、写入或导出能力。

## 开始使用

在 Unity 菜单中打开 `ActionSequenceEditor > UI Toolkit` 或 `ActionSequenceEditor > IMGUI`，新建“功能体验样例（预置轨道）”即可体验编辑、播放、保存与导出。

- [完整项目介绍与代码导航](../../README.md)
- [功能体验样例教程](Samples/FeatureSample/README.md)
- [数据与扩展接口约定](../../Docs/ActionEditor-Contracts.md)

样例模型位于 `Runtime/Samples/FeatureSample`，工具栏和预览示例位于 `Editor/Samples/FeatureSample`。
