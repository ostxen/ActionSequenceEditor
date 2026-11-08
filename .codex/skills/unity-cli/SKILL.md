---
name: unity-cli
description: 使用本项目 Unity CLI 查询和验证 Unity Editor/Development Player、场景、Prefab、组件、资源数据库，执行编译与显式选定的 Unity 测试及构建预检。涉及真实 Unity 状态时使用；纯文本、普通算法和只读源码定位不必连接 Editor。
---

# Unity CLI

## 入口

从项目根目录运行 `python .codex/scripts/workflow.py doctor`，也可以从任意目录通过脚本绝对路径调用。路径从脚本位置推导，不借用其他工程。
CLI 位于 `Tools/UnityCLI/unity.exe`，不是 Unity Editor 自身的同名文件。实际 CLI/Pipeline 版本由 doctor 查询，Unity 版本以 `ProjectSettings/ProjectVersion.txt` 为准，不自动升级。

先运行 `python .codex/scripts/workflow.py unity list` 发现当前项目注册命令，再阅读 [操作规程](references/operations.md)。例如 `python .codex/scripts/workflow.py unity query "scene list_open"`。
仅使用完整分组命令，参数按注册的连字符名称传入，不转换旧命令或下划线参数。

## 不变量

- 开始操作前绑定指定项目唯一、可达且 ready 的 Editor/PID，轮询状态可在忙时执行。编译、测试、导入和写入串行。
- 源码用代码工具修改，不使用任意代码求值、菜单或 Pipeline 文本写入绕过授权。
- 查询与写入依据显式审核策略，不根据命令名字前缀猜测安全性。未知命令默认阻塞。
- Unity 写入依次 preview、展示范围、真实用户确认、approve、apply。高风险单独确认；CLI 的 `confirm` 不能替代用户授权。
- 内置命令的 `dry-run` 支持需要核实。Undo 不能恢复所有资源、包和设置变更。
- 写请求超时后结果未知，不重放。检查状态后再决定下一步。
- 测试先发现身份再执行，显式指定筛选器与模式；轮询终态，核对用例身份与计数。零用例、跳过、失败、取消或旧结果均不能通过。
- 缺少连接、命令、依赖或授权时报告 blocked，不安装、不升级、不连接另一工程兜底。
- 不读取和打印 Pipeline 鉴权描述符或凭据。

## 包内权威资料

读取 `Packages/com.unity.pipeline/Documentation~/` 中当前任务需要的文件：

| 场景 | 文件 |
|---|---|
| 连接与 Player | `connectivity.md`、`runtime-setup.md` |
| 安全与资源路径 | `safety-and-mutations.md`、`authoring-commands.md` |
| 编译、测试、构建 | `commands/build-and-compilation.md`、`testing.md` |
| 资源、对象、Prefab | `commands/assets-and-files.md`、`commands/gameobjects-and-components.md`、`commands/prefabs.md` |
| 视觉、场景、设置 | `commands/capture.md`、`commands/scenes.md`、`commands/project-settings.md` |
| 新增命令 | `creating-commands.md` |

实时注册能力及实现优先于示例，版本变化后重新核实契约。截图只是证据，不等于人工视觉验收通过。
