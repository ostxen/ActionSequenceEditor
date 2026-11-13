# Unity CLI 操作规程

以下命令从工程根目录运行；PowerShell 路径含空格时加引号。所有操作由本地 `Tools/UnityCLI/unity.exe` 执行，不调用同名 Editor。

## 查询与编译

`actioneditor.validation json_status` 只读诊断当前 JSON 往返差异字段路径，不输出字段值，不调用源图 OnBeforeSerialize，不修改会话/资产，仅在独立反序列化副本 Init。用于保存校验失败定位，不接受参数。

`actioneditor.validation window_status` 为固定只读现场诊断：返回新旧 ActionEditor 窗口类型、标题、尺寸、焦点、原生 canvas 是否存在、旧欢迎页/时间轴分支和两个已加载菜单的声明绑定。不打开/聚焦/关闭窗口，不执行菜单，不读资产内容，不写文件或资源；不能接受代码、路径或操作参数。

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

`actioneditor.validation document_lifecycle_probe`固定零参数隔离持久探针，只新建不存在的Assets/ActionEditorLifecycleVerification目录及IMGUI/Toolkit两组Source、Copy、Export JSON（实际来源名IMGUI.json/Toolkit.json）。两个真实窗口显式注入独立会话，验证属性、根混排、SaveChanges、Undo/Redo、磁盘重开、另存、Recovery、定时自动保存、导出读回及锁文件/导入失败/外部冲突。保留既有窗口和全局作者图，不写个人偏好；finally只关闭本次窗口并通过AssetDatabase删除本次目录。拒绝活动编辑/播放、路径/代码/force覆盖；独立preview展示六个目标、approve引用RF24–RF37连续授权、apply，超时先查证而非重放。

`actioneditor.validation arrange_feature_layout`固定移动四项：Runtime/Samples/LegacyExperience/LegacyExperienceAsset.cs→Runtime/Samples/FeatureSample/FeatureSampleDocument.cs、Editor/Samples/LegacyExperiencePreview.cs→FeatureSamplePreview.cs、Samples/LegacyExperience/README.md→Samples/FeatureSample/README.md、Editor/Core/Properties/JsonFieldDescription.cs→Runtime/Metadata/PropertyDescription.cs。固定GUID和源码/meta哈希，拒绝覆盖，AssetDatabase移动后复核身份/字节并SyncAll；无用户数据写入、无删除、无参数覆盖。独立preview/展示/approve/apply，引用RF24–RF37连续授权。

`actioneditor.validation retire_imgui_timeline_sources`固定删除十一份已替代的IMGUI时间轴源码及meta：TimelineView/Middle/Pointer/TrackItem/Bottom、ClipDragger、ClipDrawer/Basic/Simple/Signal与SplitterView。保留Toolbar、资源及用户数据；固定GUID和源码/meta哈希、拒绝参数覆盖，AssetDatabase批量删除并SyncAll。调用者迁移后独立preview/展示/approve/apply，引用RF24–RF37连续授权。

`actioneditor.validation retire_editing_sources`固定删除Core/Editing中的JsonEditCommand、JsonEditCommands、JsonPropertyEditing三份源码及meta。调用者已直接接入Application用例后才允许执行；固定GUID与源码/meta哈希，preview列清单，apply经AssetDatabase删除并核实不存在、SyncAll。不接受参数/任意路径，不触及用户文档；按RF24–RF37连续授权独立preview/approve/apply。

`actioneditor.validation arrange_application_layout`为固定零参数四项源码移动：Core/Documents/JsonDocumentSession→Application/Documents/DocumentSession，Storage/JsonDocumentStore→Infrastructure/Documents/JsonDocumentRepository，Storage/ReliableFileCommit→Infrastructure/Documents/AtomicFileCommit，Serialization/DocumentExportService→Infrastructure/Documents/DocumentExportService。固定GUID、目标不存在、源码/meta哈希、移动后身份与字节复核并SyncAll；无删除/覆盖/用户文档写入，拒绝参数覆盖。必须独立preview/approve/apply，引用本系列连续授权。

`actioneditor.validation arrange_domain_layout`为固定零参数领域归位命令：只移动Runtime/Assets的Asset、Group、Track、Clip至Runtime/Domain的ActionDocument、TimelineGroup、TimelineTrack、TimelineClip；Runtime根asmdef改名NBC.ActionEditor.Domain；CreateUtilities移入Editor/Infrastructure/Assets、InspectorPreviewAsset移入Editor/Composition；Serialization/FullSerializerExtensions改名DocumentNumber。八项固定GUID，拒绝目标存在及未登记目录；preview列源码/meta哈希和缺失目录，AssetDatabase移动后复核身份与字节并SyncAll。无删除/覆盖/用户文档写入，不接受任意路径或代码；独立preview/approve/apply，引用第4.3节连续授权，超时先核查。

`actioneditor.validation rename_toolkit_ui`是本轮UI命名整理的固定零参数命令：只将UI/UIToolkit中的JsonActionWorkbench、JsonTimelineCanvas、JsonPropertyView、JsonPreferencesWindow四个cs依次改为UIToolkitActionEditorWindow、UIToolkitTimelineCanvas、UIToolkitPropertyView、UIToolkitPreferencesWindow。核对固定GUID、目标不存在，preview绑定源码/meta哈希；AssetDatabase批量移动后验证GUID和字节保留，并SyncAll生成真实项目。不接受路径/代码覆盖，不写用户文档，不移动样式资源；独立preview/approve/apply，超时先检查。

`actioneditor.validation rf22_tutorial_probe`为固定零参数双UI教程连续重放，只创建不存在的Assets/ActionEditorValidationRF22下IMGUI.json、Toolkit.json及各自-Export.json；开始前拒绝已有产品/预览/导出窗口及活动编辑/播放，逐一调用真实菜单方法打开本次窗口，经创建弹窗共用确认动作创建预置样例，验证属性、各UI手势适配器移动/拉伸、Undo/Redo、播放状态窗口、窗口SaveChanges保存、关闭重开和独立导出读回。原生属性与指针事件走真实控件，IMGUI属性使用共享字段路径，手势调用实际窗口适配器；OS保存框/确认输入仅用固定批准新路径替代，不宣称全桌面点击。finally恢复原会话，仅关闭本次窗口并删除本次目录，不写个人偏好。独立preview/approve/apply，不接受路径/代码/force参数；超时先检查，不自动重放。

`actioneditor.validation rf20_export_probe`为固定零参数新格式导出验证，只创建不存在的Assets/ActionEditorValidationRF20中的Source/Clean/Dirty.json，从共享保存菜单打开导出窗口，验证干净/脏文档输出、取消/目标存在/源别名/活动手势拒绝及源对象/来源/基线/Undo/选择/暂停播放不变；finally关闭本次导出窗口、恢复原会话、仅删本次目录。不修改已有数据，活动编辑/播放时拒绝开始；必须独立preview/approve/apply。

`actioneditor.validation cancel_save_dialog`是Windows固定模态恢复，不是Pipeline代码执行：只读CLI实例列表绑定本工程唯一PID，EnumWindows只筛该PID、#32770、保存/Save/Save As/另存为及IDCANCEL取消按钮；preview记录句柄与按钮，approve/apply复核PID/句柄/指纹后仅PostMessage WM_COMMAND IDCANCEL。无参数、不选文件、不保存/覆盖、不退出或强杀；其他模态/多个匹配拒绝。只用于本系列测试已明确触发的保存框；返回cancel_requested后重新查询实际状态，不冒充取消已完成。

RF18增加三个固定零参数审核命令，均按独立preview/approve/apply并引用本系列真实连续授权：`actioneditor.validation rf18_move`只移动RF16-C指定的FullSerializerExtensions.cs至Runtime/Serialization，核对固定GUID、源码/meta哈希及目标不存在，再由Unity生成项目；`actioneditor.validation rf18_reference_probe`只新建不存在的Assets/ActionEditorValidationRF18，创建References.asset主/子Texture2D和Document.json，用唯一新格式写盘、卸载后按GUID/localFileId解析，验证类型不符/资源失效拒绝，finally仅删除本次目录，不接触当前会话或全工程SaveAssets；`actioneditor.validation sync_projects`只调用Unity当前IDE的SyncAll生成sln/csproj，不写源码/配置。禁止任意路径/代码/force覆盖。超时先检查目标，不重放。

`actioneditor.validation rf17_move` 为RF17固定79个源码移动命令，只接受confirm/dry-run，不接受路径或代码。清单固化自任务记录RF16-C并与原GUID逐项复核；预览列出精确源/目标/GUID/源码与meta哈希、新目录及Unity项目重新生成。拒绝源身份变化、既有目标或未登记目录；apply通过AssetDatabase创建所需目录并批量移动，保留原空目录，逐项核对GUID及源码/meta字节后使用Unity当前IDE集成重新生成真实项目。不写JSON、Combat、包或设置、不删除/覆盖、不声称Unity Undo可恢复。部分失败不自动回滚或重放，先检查已完成清单。2026-11-11用户“各种授权我都允许了，在这个系列任务中，不要等我回复而中断操作”覆盖本系列最小审核扩展及该精确移动，但仍独立preview/approve/apply，审批引用真实授权。

`actioneditor.validation controlled_quit` 是RF14受控重开的固定退出封装，不接受参数覆盖。用户于2026-11-11明确允许正常重开及最小workflow策略/测试扩展，禁止强杀，后续明确无关Shader等资源不必处理并可忽略。先preview/展示/approve/apply；发现活动或未保存JSON、无磁盘源文档、脏场景、打开的PrefabStage、未保存窗口或Assets/ActionEditor/下脏持久资源时拒绝退出。无关Combat/包/内建资源脏标记不作为任务阻塞，不保存、不清脏、不修改。无阻塞时仅调用已核实Pipeline正常退出方法，save=false/discard=false/exitCode=0，不保存、不放弃、不启动第二实例。退出回复只表示排程，必须观察旧PID正常退出后才启动同版本Editor。此命令不自动重开、不证明冷启动测量完成；未知结果先观察，不重放。

`actioneditor.validation shared_json_probe` 为 R10 固定隔离 JSON 持久探针，不接受路径/代码参数。只新建不存在的 `Assets/ActionEditorValidationR10/Shared.json`，使用共用 JSON 会话修改、原子保存、Undo/Redo、导入重载及拒绝外部修改；finally 恢复原会话快照（含未保存内容）并清理本次目录。无窗口操作，不改既有 JSON/Combat/设置。必须 preview/approve/apply。R01–R10 最新用户明确无人值守授权涵盖该固定范围验证，审批原因应引用这条真实授权，不伪造新的确认。超时结果未知时先检查目标和报告，不重放。

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
