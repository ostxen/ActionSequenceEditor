# Agent 工作约定

## 语言与工作方式

- 默认使用中文沟通、记录决策和报告结果；代码标识符遵循现有风格。
- 先读相关实现和更具体的 Agent 规则。有 Git 时先查看状态及差异；没有 Git 时不得擅自初始化，仍需保留现有及未跟踪文件。
- 小任务直接处理。跨模块、高风险、多阶段任务使用一份任务记录，按 [.codex/templates/task.md](.codex/templates/task.md) 记录目标、验收、决策、授权和证据，不制造重复计划文档。
- 仅实现明确范围，避免顺手重构、覆盖用户工作或静默删减要求。疑问影响安全或验收时先确认。
- 最终说明改了什么、实际运行了哪些检查、剩余风险和阻塞。未执行、零用例、跳过、过期结果和截图不能冒充验证通过。

## AI 工作流唯一入口

- 项目维护的 Unity skill 是 [.codex/skills/unity-cli/SKILL.md](.codex/skills/unity-cli/SKILL.md)，涉及真实 Unity 状态时先读取它和所需引用。源码定位、纯文本修改和 Python 离线检查不必连接 Editor。
- 脚本入口为 `python .codex/scripts/workflow.py`，说明见 [AI 工作流指南](Docs/AI-Development-Workflow.md)。路径从脚本所在工程定位，不依赖当前目录或源工程布局。
- 保持一份项目内 skill，不安装用户级副本，不引入另一套 Unity 自动化入口。
- 不自动安装依赖、更新 CLI/Pipeline/Editor、修改全局配置或安装第三方扩展。需要时说明原因并请求授权。
- 不擅自 commit、push、上传、签名、发布或连接外部平台。

## Unity 边界

- 源码由代码工具编辑。场景、Prefab、组件及资源数据库行为通过当前项目的 Unity CLI 验证；不要直接编辑 Scene/Prefab YAML、GUID 或 `.meta` 来替代 AssetDatabase。新增源码的 `.meta` 由 Unity 生成。
- 每个操作绑定指定项目唯一的 Editor 与 PID；确认 ready 后开始。编译、导入、测试和写入串行，轮询忙状态除外。不得连接其他项目兜底。
- 查询使用已审计白名单。未知命令、任意代码求值、菜单调用、文本写入不能绕过编辑与授权边界。
- Unity 写入先 preview，展示具体目标、输入和范围，得到用户真实确认后才可 approve/apply。删除、覆盖、包/设置变更、平台切换和实际构建必须明确授权；当前封装只支持少量已核实契约，不因 Pipeline 暴露命令就自动允许。
- `confirm` 和审批文件只是防误操作机制，不代表人类已经授权，也不是安全沙箱。不得自行编造审批人或确认记录。
- 请求发出后超时意味着结果未知。先检查状态和目标，不自动重放；已消耗审批不能再次使用。
- 不读取或输出 `Library/Pipeline` 鉴权描述符、token、密码等敏感内容。证据日志需要脱敏。
- 测试显式选择实际存在的测试及模式，发现用例后再运行，检查终态、身份、计数和结果。编译请求被接受不等于编译成功。
- UniTask 等异步代码需明确取消来源、异常处理、资源归属与销毁生命周期；不添加任何业务框架假设。

## 验证范围

- 文档与工作流修改先做离线检查；C# 修改检查真实生成的解决方案并按影响验证 Editor。
- `verify --profile unity/full` 必须显式给出测试筛选器与模式。没有对应测试就报告覆盖缺口，不跑无关包测试代替。
- 项目未提供通用完整资源验证命令；资源查询不等于序列化引用或业务规则验收。`--asset` 请求不能假报通过。
- 无连接、无命令、无依赖或无授权时报告 blocked，不把缺少证据当作成功。

## 编辑器改造任务恢复

- ActionSequenceEditor 平台化与 Combat 接入的唯一执行清单是 [.codex/tasks/action-editor-refactor.md](.codex/tasks/action-editor-refactor.md)。涉及该改造时先读取其任务状态、依赖、验收和执行记录，再按用户指定编号执行，不自动批量推进。
- 工程改名或重新打开后，以新根目录和工程内 `Assets/ActionSequenceEditor/`、`Assets/CombatEditor/` 为准，不使用旧绝对路径或外部 ActionSequenceEditor 副本；环境恢复步骤见该任务文档。
