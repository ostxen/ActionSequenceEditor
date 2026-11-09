# Unity CLI 操作规程

以下命令从工程根目录运行；PowerShell 路径含空格时加引号。所有操作由本地 `Tools/UnityCLI/unity.exe` 执行，不调用同名 Editor。

## 查询与编译

```powershell
python .codex/scripts/workflow.py doctor
python .codex/scripts/workflow.py unity list
python .codex/scripts/workflow.py unity query "editor status"
python .codex/scripts/workflow.py unity query "scene list_open"
python .codex/scripts/workflow.py unity query "test list"
python .codex/scripts/workflow.py unity compile --timeout 300
```

编译会触发 Editor 生命周期，应在当前任务需要时执行，不要在纯文档修改时强制触发。脚本轮询编译终态并保留 console，不把请求已受理当作成功。
需要参数的查询将 JSON 对象保存到项目内文件，再传入 `--params-file`。例如 `asset find` 的参数 `{"name":"Example","search-in":"Assets","limit":20}`。参数名必须与注册描述一致，禁止注入工程、PID、runtime 或 confirm 等保留参数。

## 测试

先通过 `test list` 找到本工程实际用例，再将示例筛选器替换成真实程序集：

```powershell
python .codex/scripts/workflow.py verify --profile unity --test-filter "Your.Tests.Assembly" --filter-type assembly --test-mode editor --timeout 300
```

需要 PlayMode 时增加 `--test-mode playmode`；不强迫没有对应套件的项目运行两个模式。必须覆盖任务实际影响的模式，并报告未覆盖部分。没有匹配用例时 blocked；跳过、失败、旧身份或不一致计数不能通过。
没有自定义完整资源验证器，`--asset` 显式报告 blocked。`asset find` 和 `asset get_import_settings` 只证明查询到的内容，不代表丢失引用或业务规则已通过。

## 写入审批

当前审计封装支持 `asset delete`、`settings.player set`、`build set_settings`、`build run`，以及项目专用 `actioneditor.validation persistence_probe`。其他写入需先核实包内实现并补充策略及离线测试，不直接运行原始 CLI 绕过封装。

`actioneditor.validation persistence_probe` 是 A01 的固定范围验证命令，不接受任意路径或代码。dry-run 只列目标；apply 要求真实确认。它拒绝已存在的 `Assets/ActionEditorValidationA01` 及对应 `.meta`，仅在本次新建文件夹内创建 `Document.asset` 和 `Resource.asset`，验证两个多态事件、时间、参数、Unity 资源引用、Undo/Redo 及保存/卸载/重载，然后在 finally 中仅清理本次创建的文件夹。不调用全工程 SaveAssets，不修改包、设置或 Combat。清理失败时报错，超时结果未知时先检查固定目标，不能重放。正式存储仍待 A01 定案，该命令不代表新平台存储已完成。

以下仅示范流程，不代表授权删除。先写一份参数 JSON，例如 `{"asset":"Assets/Example.asset"}`，随后：

```powershell
python .codex/scripts/workflow.py unity preview "asset delete" --params-file "params.json"
# 展示生成的 preview.json、目标和影响，等待用户真实确认。
python .codex/scripts/workflow.py approve --plan "实际运行目录/preview.json" --by "真实确认者" --reason "实际用户确认记录" --high-risk
python .codex/scripts/workflow.py unity apply --plan "实际运行目录/preview.json" --approval "审批运行目录/approval.json"
```

预览有效期一小时，绑定工程路径、命令、参数、工作区内容指纹和指定输入。apply 再次比较实时预览后消耗一次性审批。文件变化、过期或预览变化均要求重新预览与授权。审批不是防恶意篡改的安全边界。
工作区指纹不依赖 Git，覆盖 Assets、Packages、ProjectSettings、工作流、工具及文档输入，排除生成证据、缓存与 Pipeline 凭据目录。Unity 内存状态仍以二次预览为准。

`actioneditor.validation session_probe` 是 A05 的固定范围验证命令，不接受路径/代码参数。仅新建不存在的 `Assets/ActionEditorValidationA05/`，在其中创建 A/B 正式文档及资源，使用 DocumentSession/DocumentStore 实际保存、重复提交、关闭重开、失败切换、资源引用往返，最后清理本次新建目录。原始资产、包和设置不改；必须先 preview 并展示三个目标及清理范围，再取得真实确认，不能沿用 A01 的审批。

## Player 和证据

`actioneditor.validation workbench_probe` 是 B06 的固定范围命令，不接受路径/代码参数。仅新建不存在的 `Assets/ActionEditorValidationB06/` 中 Sample.asset 和 Resource.asset，打开本次专属原生工作台窗口，通过真实原生字段编辑点/区间、集合/null、Esc 取消、Undo/Redo、SaveDocument 保存、关闭并在新窗口重开，检查 ID/参数/引用及保存后撤销脏状态；最后关闭本次窗口，清理本次目录。不操作用户已打开窗口，不改包/设置/Combat，不调用全工程 SaveAssets。必须独立 preview、展示目标与清理范围、真实确认后 approve/apply，不沿用 A01/A05 授权。超时结果未知先只读检查，不能重放。

Development Player 查询必须显式使用 `unity runtime-status --runtime-path "Player工作目录绝对路径"`。返回工作目录必须匹配，不能借用另一个 Player。
每次执行生成 `.codex/runs/<运行ID>/report.json` 与 `report.md`。`report --run "实际运行目录"` 只重新呈现已有证据，不执行验证。
只读白名单不包含截图、文件读取、任意代码求值和菜单执行。截图会写文件，必须按具体任务范围处理，不代表视觉验收。
