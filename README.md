# ActionEditor

基于 Unity 2022.3.62f3 的通用行为时间轴编辑平台。当前保留 NBC ActionEditor 2.0 源码作为改造基线，正在按任务清单建设原生 UI Toolkit 工作台。

- 唯一实施源码：`Assets/ActionEditor/`。
- 执行状态、验收与决策：[改造任务清单](.codex/tasks/action-editor-refactor.md)。
- 设计依据：[架构分析](Docs/ActionEditor与CombatEditor架构分析及改造建议.md)。
- Unity 操作入口：[AI 工作流](Docs/AI-Development-Workflow.md)。

## 仓库范围

本轮仅实施 A00–B06。Combat 专属实施与验收暂缓，不修改其源码及原始资产。
`Assets/CombatEditor/` 和目录外的 Combat 示例资产 `Assets/ScriptableObjects/` 保留在本机，但不纳入 Git。
因此这个仓库暂不提供 Combat 示例运行能力，缺少这些本地资料不应成为 ActionEditor 核心的依赖。

Unity 缓存、生成的解决方案、用户配置、工作流运行证据和签名材料不提交。`Tools/UnityCLI/` 是工作流实际使用的 CLI，`Assets/Tools/` 中的重复副本不提交。

## 来源与发布边界

原始实现来自 [NoBugCn/ActionEditor](https://github.com/NoBugCn/ActionEditor)，本地包声明版本为 2.0.0。保留原始作者与来源说明。
截至本轮基线调查，上游根目录及本地 ActionEditor 副本未发现独立许可证文件；不能据此推定允许以某个开源许可证重新发布。
远程公开发布前需要确认原始实现及 FullSerializer 二进制的许可与再分发条件。当前不添加未经授权的许可证声明。

Git 提交先保留在本地，远程地址与目标分支由用户后续提供。
