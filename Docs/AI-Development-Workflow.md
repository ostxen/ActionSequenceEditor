# AI 开发工作流

## 范围与入口

本工程维护一套项目内、项目绑定的 Unity AI 工作流：

- [Agent 规则](../AGENTS.md)：通用协作、授权与验证边界。
- [Unity CLI skill](../.codex/skills/unity-cli/SKILL.md)：Unity 实际状态操作路由。
- [操作规程](../.codex/skills/unity-cli/references/operations.md)：查询、编译、测试、审批与报告示例。
- [任务模板](../.codex/templates/task.md)：多阶段任务的单份记录。
- `.codex/scripts/workflow.py`：Python 3.11+ 命令入口。
- `Tools/UnityCLI/unity.exe`：本地 CLI，区别于 Unity Editor。

只迁移 AI 与通用 Unity 流程，不引入业务框架规则、业务代码、配置生成器、网页烘焙或源工程运行记录。用户已复制的 Pipeline 保持不变。没有 Git 时不自动初始化或弱化授权，使用项目输入的内容指纹绑定预览。

## 常用命令

```powershell
python .codex/scripts/workflow.py --help
python .codex/scripts/workflow.py doctor
python .codex/scripts/workflow.py check
python .codex/scripts/workflow.py verify --profile docs
python .codex/scripts/workflow.py unity list
python .codex/scripts/workflow.py unity query "scene list_open"
python .codex/scripts/workflow.py unity compile --timeout 300
```

脚本从自身位置确定项目，默认生成的解决方案名称跟随当前项目名；仅有一个其他 `.sln` 时使用它。多个候选且无同名文件时不猜测、不合成解决方案。显式覆盖工程可用全局 `--project`，必须是含 Assets、Packages、ProjectSettings 的 Unity 根目录。

## 验证层级

| Profile | 实际范围 |
|---|---|
| docs | skill/Agent/指南链接与元数据、Python 单元测试 |
| code | docs + 真实生成解决方案的源文件覆盖检查和 dotnet build |
| unity | 项目与 PID 绑定、命令发现、编译终态、显式选定的 Unity 测试 |
| full | docs + code + unity，不包含任何外部业务工具 |

`unity` 和 `full` 必须提供 `--test-filter` 以及至少一个 `--test-mode editor/playmode`，不默认选择无关测试。`--filter-type` 支持 assembly、testName、category。实际测试是否充分仍需按变更范围判断，profile 不是一键宣布整个工程通过。

```powershell
python .codex/scripts/workflow.py verify --profile unity --test-filter "Your.Tests.Assembly" --filter-type assembly --test-mode editor --timeout 300
```

上面的程序集名称必须替换。零测试或全跳过不是通过；部分跳过同样不能声称完整验收。请求 `--asset` 时由于没有通用完整资源验证器，显式 blocked，不用导入设置查询冒充资源验收。

## 环境与安装

- Python 3.11+；`python` 与实际路径以当前环境为准，不假定存在 `py`。
- 文档结构检查需要 `.codex/scripts/requirements.txt` 中的 PyYAML 与 markdown-it-py，离线单元测试与命令帮助只依赖标准库。
- code profile 需要 .NET SDK 和 Unity 生成的 `.sln`/`.csproj`，不得手工合成项目文件来掩盖未覆盖源码。
- Unity 操作需要已打开的本项目 Editor 与可达 Pipeline。版本取自实际本地工具与项目，不自行升级。

仅在用户明确同意安装后执行：

```powershell
python -m pip install -r .codex/scripts/requirements.txt
```

脚本不自动安装、升级或修改全局配置。缺依赖时结构检查为 blocked，仍可运行标准库单元测试定位逻辑问题。doctor 展示可用依赖，不等于所有 profile 已通过。

## 写入与证据

采用 preview → 展示目标/范围 → 用户真实确认 → approve → apply。删除、覆盖、设置、平台切换、包变更和实际构建必须明确授权。确认参数、审批 JSON 与 CLI 能力本身不是授权。审批有时效、工程与内容绑定及一次性消耗机制，写请求结果未知时不得重放。

工作区指纹覆盖项目输入目录，排除 Library、Temp、Logs、生成证据与 Python 缓存，不依赖 Git，不扫描其他工程。外部输入必须显式绑定；其他写入契约默认拒绝，需先实现和验证策略。

`.codex/runs/` 保存脱敏命令、时间、退出码、结构化结果与报告，不迁移旧运行记录，不提交鉴权描述符。返回码：0 passed，1 failed，2 blocked。必要步骤失败或阻塞不会被可选步骤覆盖；被中断的报告保持 blocked。

先读真实实现与现状，改动后按影响验证，最终分别列出已通过、阻塞、未运行和人工验收项。纯文档与脚本迁移不要求擅自触发 Unity 编译或游戏测试。
