# Unity CLI 操作规程

以下命令从工程根目录运行；PowerShell 路径含空格时加引号。所有操作由本地 `Tools/UnityCLI/unity.exe` 执行，不调用同名 Editor。

## 查询与编译

`actionsequenceeditor.verification json_status` 只读诊断当前 JSON 往返差异字段路径，不输出字段值，不调用源图 OnBeforeSerialize，不修改会话/资产，仅在独立反序列化副本 Init。用于保存校验失败定位，不接受参数。

`actionsequenceeditor.verification window_status` 为固定只读现场诊断：返回当前 ActionSequenceEditor 窗口类型、标题、尺寸、焦点、原生 canvas 是否存在、欢迎页/时间轴分支和两个已加载菜单的声明绑定。不打开/聚焦/关闭窗口，不执行菜单，不读资产内容，不写文件或资源；不能接受代码、路径或操作参数。

```powershell
python .codex/scripts/workflow.py doctor
python .codex/scripts/workflow.py unity list
python .codex/scripts/workflow.py unity query "editor status"
python .codex/scripts/workflow.py unity query "scene list_open"
python .codex/scripts/workflow.py unity query "test list"
python .codex/scripts/workflow.py unity compile --timeout 300
```

编译会触发 Editor 生命周期，应在当前任务需要时执行，不要在纯文档修改时强制触发。脚本轮询编译终态并保留 console，不把请求已受理当作成功。
轮询中的暂时不可达会在调用者指定的总期限内继续观察，但不会重新提交原操作；PID变化、目标重复或工程不匹配立即阻断。总期限耗尽仍表示结果未知，应查询原操作终态而非重放。长套件需根据真实耗时设置期限，不能把轮询恢复当作操作已完成。
需要参数的查询将 JSON 对象保存到项目内文件，再传入 `--params-file`。例如 `asset find` 的参数 `{"name":"Example","search-in":"Assets","limit":20}`。参数名必须与注册描述一致，禁止注入工程、PID、runtime 或 confirm 等保留参数。

## 测试

先通过 `test list` 找到本工程实际用例，再将示例筛选器替换成真实程序集：

```powershell
python .codex/scripts/workflow.py verify --profile unity --test-filter "Your.Tests.Assembly" --filter-type assembly --test-mode editor --timeout 300
```

需要 PlayMode 时增加 `--test-mode playmode`；不强迫没有对应套件的项目运行两个模式。必须覆盖任务实际影响的模式，并报告未覆盖部分。没有匹配用例时 blocked；跳过、失败、旧身份或不一致计数不能通过。
没有自定义完整资源验证器，`--asset` 显式报告 blocked。`asset find` 和 `asset get_import_settings` 只证明查询到的内容，不代表丢失引用或业务规则已通过。

## 写入审批

`actionsequenceeditor.verification menu_layout_probe`为固定零参数Toolkit菜单布局探针：已有窗口则使用，没有则打开并保留窗口；临时设置16/20/24字号，经真实右键事件打开并取消轨道/保存菜单，测量Label字号、文字内容边界及行高，finally恢复字号与原焦点。不点击业务菜单、不编辑作者图、不写资产/偏好/文件，不接受路径/代码/字号覆盖。必须preview展示→真实验证授权→approve/apply，不是测试套件或人工视觉验收。

`actionsequenceeditor.verification document_lifecycle_probe`固定零参数隔离持久探针，只新建不存在的Assets/ActionSequenceEditorLifecycleVerification目录及IMGUI/Toolkit两组Source、Copy、Export JSON（实际来源名IMGUI.json/Toolkit.json）。两个真实窗口显式注入独立会话，验证属性、根混排、SaveChanges、Undo/Redo、磁盘重开、另存、Recovery、定时自动保存、导出读回及锁文件/导入失败/外部冲突。保留既有窗口和全局作者图，不写个人偏好；finally只关闭本次窗口并通过AssetDatabase删除本次目录。拒绝活动编辑/播放、路径/代码/force覆盖；独立preview展示六个目标、approve引用RF24–RF37连续授权、apply，超时先查证而非重放。

`actionsequenceeditor.verification tutorial_probe`为固定零参数双UI教程连续重放，只创建不存在的Assets/ActionSequenceEditorTutorialVerification下IMGUI.json、Toolkit.json及各自-Export.json；开始前拒绝已有产品/预览/导出窗口及活动编辑/播放，逐一调用真实菜单方法打开本次窗口，经创建弹窗共用确认动作创建预置样例，验证属性、各UI手势适配器移动/拉伸、Undo/Redo、播放状态窗口、窗口SaveChanges保存、关闭重开和独立导出读回。原生属性与指针事件走真实控件，IMGUI属性使用共享字段路径，手势调用实际窗口适配器；OS保存框/确认输入仅用固定批准新路径替代，不宣称全桌面点击。finally恢复原会话，仅关闭本次窗口并删除本次目录，不写个人偏好。独立preview/approve/apply，不接受路径/代码/force参数；超时先检查，不自动重放。

`actionsequenceeditor.verification export_probe`为固定零参数新格式导出验证，只创建不存在的Assets/ActionSequenceEditorExportVerification中的Source/Clean/Dirty.json，从共享保存菜单打开导出窗口，验证干净/脏文档输出、取消/目标存在/源别名/活动手势拒绝及源对象/来源/基线/Undo/选择/暂停播放不变；finally关闭本次导出窗口、恢复原会话、仅删本次目录。不修改已有数据，活动编辑/播放时拒绝开始；必须独立preview/approve/apply。

`actionsequenceeditor.verification cancel_save_dialog`是Windows固定模态恢复，不是Pipeline代码执行：只读CLI实例列表绑定本工程唯一PID，EnumWindows只筛该PID、#32770、保存/Save/Save As/另存为及IDCANCEL取消按钮；preview记录句柄与按钮，approve/apply复核PID/句柄/指纹后仅PostMessage WM_COMMAND IDCANCEL。无参数、不选文件、不保存/覆盖、不退出或强杀；其他模态/多个匹配拒绝。只用于本系列测试已明确触发的保存框；返回cancel_requested后重新查询实际状态，不冒充取消已完成。

两个固定零参数审核命令均按独立preview/approve/apply并引用本系列真实连续授权：`actionsequenceeditor.verification reference_probe`只新建不存在的Assets/ActionSequenceEditorReferenceVerification，创建References.asset主/子Texture2D和Document.json，用唯一新格式写盘、卸载后按GUID/localFileId解析，验证类型不符/资源失效拒绝，finally仅删除本次目录，不接触当前会话或全工程SaveAssets；`actionsequenceeditor.verification sync_projects`只调用Unity当前IDE的SyncAll生成sln/csproj，不写源码/配置。禁止任意路径/代码/force覆盖。超时先检查目标，不重放。

`actionsequenceeditor.verification controlled_quit` 是正常重开的固定退出封装，不接受参数覆盖。用户于2026-11-11明确允许正常重开及最小workflow策略/测试扩展，禁止强杀，后续明确无关Shader等资源不必处理并可忽略。先preview/展示/approve/apply；发现活动或未保存JSON、无磁盘源文档、脏场景、打开的PrefabStage、未保存窗口或Assets/ActionSequenceEditor/下脏持久资源时拒绝退出。无关Combat/包/内建资源脏标记不作为任务阻塞，不保存、不清脏、不修改。无阻塞时仅调用已核实Pipeline正常退出方法，save=false/discard=false/exitCode=0，不保存、不放弃、不启动第二实例。退出回复只表示排程，必须观察旧PID正常退出后才启动同版本Editor。此命令不自动重开、不证明冷启动测量完成；未知结果先观察，不重放。

`actionsequenceeditor.verification persistence_probe` 为固定隔离文档持久探针，不接受路径/代码参数。只新建不存在的 `Assets/ActionSequenceEditorPersistenceVerification/Shared.json`，使用共用 JSON 会话修改、原子保存、Undo/Redo、导入重载及拒绝外部修改；finally 恢复原会话快照（含未保存内容）并清理本次目录。无窗口操作，不改既有 JSON/Combat/设置。必须 preview/approve/apply。任务4.3.1连续执行授权涵盖该固定范围验证，审批原因应引用这条真实授权，不伪造新的确认。超时结果未知时先检查目标和报告，不重放。

当前审计封装支持 `asset delete`、`settings.player set`、`build set_settings`、`build run`及上述固定工程验证命令。其他写入需先核实包内实现并补充策略及离线测试，不直接运行原始 CLI 绕过封装。

以下仅示范流程，不代表授权删除。先写一份参数 JSON，例如 `{"asset":"Assets/Example.asset"}`，随后：

```powershell
python .codex/scripts/workflow.py unity preview "asset delete" --params-file "params.json"
# 展示生成的 preview.json、目标和影响，等待用户真实确认。
python .codex/scripts/workflow.py approve --plan "实际运行目录/preview.json" --by "真实确认者" --reason "实际用户确认记录" --high-risk
python .codex/scripts/workflow.py unity apply --plan "实际运行目录/preview.json" --approval "审批运行目录/approval.json"
```

预览有效期一小时，绑定工程路径、命令、参数、工作区内容指纹和指定输入。apply 再次比较实时预览后消耗一次性审批。文件变化、过期或预览变化均要求重新预览与授权。审批不是防恶意篡改的安全边界。
工作区指纹不依赖 Git，覆盖 Assets、Packages、ProjectSettings、工作流、工具及文档输入，排除生成证据、缓存与 Pipeline 凭据目录。Unity 内存状态仍以二次预览为准。

## Player 和证据

Development Player 查询必须显式使用 `unity runtime-status --runtime-path "Player工作目录绝对路径"`。返回工作目录必须匹配，不能借用另一个 Player。
每次执行生成 `.codex/runs/<运行ID>/report.json` 与 `report.md`。`report --run "实际运行目录"` 只重新呈现已有证据，不执行验证。
只读白名单不包含截图、文件读取、任意代码求值和菜单执行。截图会写文件，必须按具体任务范围处理，不代表视觉验收。
