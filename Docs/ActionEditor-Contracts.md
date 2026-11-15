# ActionEditor 时间、身份与扩展契约

本文定义当前产品合同。实施状态、验证证据和待验范围仅记录在[唯一任务清单](../.codex/tasks/action-editor-refactor.md)；入门操作只维护在[功能体验样例教程](../Assets/ActionEditor/Samples/FeatureSample/README.md)。

## 产品入口与状态归属

菜单为 `NBC/Action Editor/IMGUI` 和 `NBC/Action Editor/UI Toolkit`，分别打开 `NBC.ActionEditor.Views.IMGUI.ActionEditorWindow` 与 `NBC.ActionEditor.Views.UIToolkit.ActionEditorWindow`。两窗绑定同一作者会话、选择、剪贴板、播放头及Undo；每窗独立持有Presenter、工作区、焦点和捕获所有权。最后一个编辑窗口关闭时停止共享播放。

被动视图只绘制显示状态并上报意图。Application负责会话、事务和用例；Presentation负责Presenter、交互与几何；Infrastructure实现文件、Undo、时钟、偏好和AssetDatabase端口；Composition显式建立依赖并拥有Editor生命周期。Domain及Serialization不依赖UnityEditor，Application和Presentation不依赖具体视图。`ActionEditorHost`只作为组合根，不提供业务门面转发。

`EditorComposition`私有拥有共享会话、选择、剪贴板、播放与采样，只通过显式工厂创建逐窗绑定和Presenter，不公开服务查询包。`WindowBinding`拥有逐窗订阅和释放；Toolkit的`TimelineHeader`、`TimelineViewport`、`TimelineProperties`分别拥有原生控件和同步。默认设置会话由宿主私有持有，隔离宿主显式注入自己的`SettingsSession`，设置窗口不自行获取全局业务状态。

目录按职责对应：Runtime的`Domain`、`Metadata`、`Serialization`分别放作者规则、声明及格式；Editor的`Infrastructure/Documents`、`Serialization`、`Preview`、`Assets`、`Settings`分别实现文档外部能力、格式适配、预览、发现及偏好存储。属性输入状态与草稿位于`Presentation/Properties`，语言文本位于`Views/Localization`。旧`ActionEditorContext`、`Prefs`、`AssetPlayer`及`NBC.ActionEditor.Storage`不再作为业务入口。

组和独立轨道按 `ActionDocument.RootItems` 任意交错排列，组不嵌套；组持有轨道，轨道持有片段。结构拖动超过3px显示覆盖主体全宽的蓝/红插入线，不显示拖动文本。最左20px是独立根区，组移动携带完整子树，空组无折叠按钮但保留对齐占位。边缘24px按0–300px/s自动滚动。提交重验锁定、Attachable、Unique及版本，一次有效释放对应一步Undo，同位与相邻边界不产生空Undo。

两UI滚轮缩放以鼠标时间为锚点，最小跨度0.25秒、步进225；保持锚点时视域可为负，作者时间不得为负。中键平移保持跨度并在零点夹紧。片段最短可编辑区间0.01秒，点事件不可伸缩。Esc、失焦、捕获丢失及文档替换只取消本窗口拥有的操作。

创建、保存、另存与自动保存进入 `DocumentLifecycle`，经同一 `JsonDocumentRepository` 和 `AtomicFileCommit` 完成快照、校验、完整读回及revision保护。字节提交后先推进来源与基线再导入，导入失败明确报告已提交，禁止自动重放。导出使用同一格式目录和隔离快照，只写独立新目标，不切来源、不清脏、不改变Undo、选择或播放。新建/另存/导出拒绝现存目标及meta，取消不写文件。

属性字段由共享描述和Presenter私有的`PropertyPath`解释。原生控件只得到`PropertyInputState`、`PropertyTargetState`和不透明`PropertyFieldId`，不能拿路径或作者图；文档/选择换代使旧字段身份及草稿失效。集合增删先为每个目标构造隔离候选，整批预检通过后一次事务提交；后部目标失败不改变前部目标，Undo/Redo恢复完整批次。路径处理嵌套struct时逐层回写，陈旧对象、锁定和活动编辑边界在真实提交处重验。

业务Header和状态窗口通过`EditorExtensionContext.Capture()`得到`ExtensionDocumentState`显示帧，不接收可写文档。显示帧只包含基础值、独立曲线副本、时间、权重及循环值；复杂作者对象和集合不跨此边界。通用投影没有业务样例分支，属性扩展仍通过共享Presenter的字段身份提交。

播放只有一个Editor时钟驱动。预览会话拥有创建的预览实例，不拥有借入的作者图或Unity资源；回调引发替换/取消时，旧代次不能继续求值或清理新会话。逐窗订阅、原生捕获和选择器Editor缓存随各自所有者释放，关闭非最后窗口不停止共享播放。

## 唯一序列化契约

当前唯一可编辑和可导出格式为 `actioneditor.json` version 2。所有成员必需，完整类型身份精确匹配，禁止裸根回退、别名、迁移或双写。持久引用使用GUID/localFileId；有限float保存原始位，包含负零和次正规数。严格拒绝非有限数、错误图、失效引用和未知类型，失败候选不替换当前图。
### 最小边界与能力

- Runtime `Serialization` 定义 `IActionDocumentFormat`：不可变 `Id`、`DisplayName`、`Extension`，独立的 `CanRead`、`CanWrite`、`CanExport`，以及 `Serialize(ActionDocument, options, context) → byte[]` / `Deserialize(byte[], context) → ActionDocument`。不支持的方向明确失败；不要求未来只写导出器实现可用的读回。失败抛有非空错误的异常，包含字段路径/类型身份但不包含作者字段值；禁止返回空字符串、空字节或 null 冒充成功。`options` 首期仅压缩/美化，编码为 UTF-8 无 BOM，context 只提供受控类型解析及 Unity 引用解析，不提供路径写入或会话操作。
- 首个且唯一生产注册：`Id = actioneditor.json`，显示 `JSON (.json)`，扩展名 `.json`，读、写、导出均支持。Editor 格式目录显式注册实际实现，ID/扩展名冲突整体拒绝并列双方来源；创建/打开筛选 `CanRead && CanWrite`，导出筛选 `CanExport && CanWrite`，没有实现就不显示。文件扩展名选择候选，内容标识再次核实，大小写扩展名沿当前规则；不做旧格式探测或回退。
- 格式实现只做数据转换。Editor `Infrastructure/Serialization` 协调受信任格式上下文、图校验、资源引用、隔离快照和作者内容比较；`DocumentCopy`构造隔离副本与内容键，`SessionSnapshotCodec`实现会话快照端口，`JsonDocumentCodec`负责文档JSON入口。`Infrastructure/Documents` 通过 `JsonDocumentRepository` 负责路径、版本、`AtomicFileCommit` 和导入，导出用例消费相同格式与快照边界。Session 仍是唯一图、事务、Undo、Recovery 与保存基线的所有者。类型目录、磁盘格式、UI 各有职责，不新增第二份可编辑文档或万能注册框架。

### 唯一 JSON 结构与类型身份

顶层仅为 `format`、`version`、`document`，分别固定为 `"actioneditor.json"`、整数 `2`、非空 ActionDocument 节点。版本不同或缺失即拒绝，不读旧的裸根对象，不双写。版本号仅描述本契约，不表示对象稳定 ID。

`document` 及每个 Group/Track/Clip 使用 `$type` 加序列化字段。`$type` 为 **`Type.FullName + ", " + Assembly.GetName().Name`**，例如 `NBC.ActionEditor.Samples.FeatureSample.FeatureSampleDocument, NBC.ActionEditor.Domain`，ordinal 精确匹配。它不是 Name/MenuName、短类名、路径或带版本的 AssemblyQualifiedName；程序集或命名空间改名是显式格式变更，不建设别名层。根节点仅允许 Group/Track，唯一 `rootItems` 保持任意交错顺序；Group 仅有 Track，Track 仅有 Clip，不允许组嵌套，也无第二份 groups/rootTracks 存储。集合保持顺序；不持久化父引用、`directables`、Actor 场景绑定、预览/选择/Undo/缓存、计算属性或委托。`ActorId` 是既有业务字段，不提升为持久 Unity 引用身份。

序列化使用受控字段/类型策略，领域类型不依赖 FullSerializer：公共实例字段与 `[SerializeField]` 字段（含基类私有字段），排除 static、`[NonSerialized]`；不自动持久化属性。保留模型实际字段键，如 `rootItems`、`tracks`、`actionClips`、`startTime`、`length`、`blendIn`、`CrossBlendIn`。禁止框架元数据与业务字段重名、重复 JSON 键、未知字段静默丢弃、继承字段键冲突和 `$ref/$id` 对象图别名。序列化成员按 ordinal 名排序，数组/List 保持位置。所有契约成员必须存在，允许 null 的业务成员必须显式写 null；缺字段不使用样例构造器的预置数据补齐。

类型解析先检查已加载、受信任的类型目录（不 `Assembly.Load` 用户输入），核实具体/非泛型/可序列化类型及预期 ActionDocument/TimelineGroup/TimelineTrack/TimelineClip 或声明业务字段的可赋值性。预检整份 token 树的类型、成员和引用，再构造候选；不能调用 `ReflectionTools.GetType` 的短名兜底或让 FullSerializer 自由解析 `$type`。业务类型由现有 Attribute/发现形成目录，不要求另填一套显示名称身份；测试类型只在显式测试 context 中允许，生产目录排除测试、验证和 Editor-only 定义。目录发现不执行构造器；反序列化只允许已核实类型，结构集合由输入完整替换而非追加构造器默认集合。业务构造/序列化回调仍是受信任代码，不宣称这是任意第三方代码沙箱。

嵌套的具体业务类/struct使用声明字段类型，声明为基类/接口而出现不同具体类型时也用同一受控`$type`；不允许任意object反序列化入口。剪贴板Group/Track/Clip片段由内部同一受控值转换器按预期节点类型捕获/还原，不强塞成Asset，也不另建磁盘文档格式；`IActionDocumentFormat`的公开文档入口仍只接受ActionDocument。数组/List按声明元素类型递归，Dictionary及其他容器没有明确实现和用例时拒绝，不承诺任意.NET对象图可保存。

### 值、Unity 引用、快照及比较

- float 使用 JSON 数字，不固定小数位、不吸附、不转 decimal，沿现有 Single→double 数字输出的精度思路并实测打印器；读回必须恢复有限 Single 的原始位。**正零与负零均保留符号位**，输出负零须保留 `-0.0` 等可辨表示，内容比较也区分二者。NaN/Infinity（含嵌套业务字段）不能提交文档；数值溢出不夹紧。验证覆盖极值、次正规数、固定种子随机位模式及压缩/美化，而不是把本节定案当作已通过。
- null、空集合、数组/List 和嵌套业务对象分别保留。Vector2/3/4、Quaternion、Color 用其分量对象；AnimationCurve 保留全部 key 的时间、值、切线、权重、weightedMode 及前后 wrapMode，enum 保留整数并按声明类型校验；其他类型没有明确受控转换就报字段路径，不静默降级。字段的显示舍入不写回存储。
- Unity Object 的非空持久表示唯一为 `{"$unity":{"guid":"32位小写资源GUID","localFileId":整数}}`，null 仍为 null；解析后必须验证对象存在且可赋给声明字段。支持当前工程持久主资产及子资产，不按名字/路径/GetInstanceID 恢复；场景对象、临时对象、失效引用、无 GUID 的内建对象拒绝，字符串资源路径继续只是字符串。Editor context 用 AssetDatabase 将引用与 GUID/localFileId 双向映射；Runtime 只注入解析器，不引用 UnityEditor，缺解析器遇非空引用必须报错。该引用标识不引入文档节点稳定 ID。
- Editor 从当前图构造隔离快照，再在副本上执行既有序列化回调、派生时长重建与校验，不因保存/导出改变源图或视图。Undo/取消/Recovery 也走同一快照服务，仅携带作者图；视域、折叠、滚动与分栏由各窗口独立工作区持有，不进入文档或作者快照；Recovery 额外保存来源格式 ID/路径、保存快照、作者内容基线及磁盘 revision，不是第二种磁盘文档格式，不回退读取旧 Recovery。
- 作者内容比较基于同一格式无关的字段/类型/引用值树，而非任意未来格式输出的 JSON 字符串。工作区成员不存在于领域模型，不依靠序列化黑名单排除；范围、激活/锁定、业务字段、派生 length 仍纳入，重建派生值后比较。字典键序/美化不影响相等，集合顺序、null/empty、派生类型、float 位和引用二元组影响相等。存储读回还核对完整作者快照与根类型，不用“排除视图”掩盖字段丢失。
- 图校验必须在 `Init`/属性 setter 有机会夹紧或修复输入之前进行：结构集合非 null，无 null/共享/循环节点，类型与挂载/唯一性合法，片段时间有限非负、结束有限、点长度为零，业务字段所有 float 有限；保留现有允许的交叉混合与 `Blend=-1` 能力语义，不统一 UI 的最短长度/重叠策略。长度是现有激活片段的派生最大结束时间、空图最小 0.1 秒；不把实验 ActionDocument 的 double/文档边界规则套过来。失败候选不替换当前图。

### 完整最小样本与校验预期

以下两个完整样本是独立合同输入，回归测试直接读取本节并逐字段、逐位验证。空根样本不继承样例构造器的预置集合。

```json
{
  "format": "actioneditor.json",
  "version": 2,
  "document": {
    "$type": "NBC.ActionEditor.Samples.FeatureSample.FeatureSampleDocument, NBC.ActionEditor.Domain",
    "Background": { "a": 1, "b": 0.25, "g": 0.125, "r": 0.5 },
    "Notes": null,
    "Version": 1,
    "rootItems": [],
    "length": 0.10000000149011612,
    "rangeMax": 6,
    "rangeMin": 0
  }
}
```

| 样本/变体 | 必须断言的预期 |
|---|---|
| 上述完整样本 | 根为 FeatureSampleDocument，0组而非构造器预置3组；Notes=null；length 位为 `0x3dcccccd`，不因显示0.1改变；范围[0,6]；工作区不在文档中 |
| 按现有 FeatureSampleDocument 构造完整点/区间/循环样例后用新实现编码 | 3组、7轨、13片段；Marker零长度、Interval参数列表/Offset/Curve、Loop周期/速度逐字段相等；原样例业务类型身份不因产品文案改变 |
| 在测试专用派生 Clip 增加 `float[]`、null List、嵌套 struct、持久 Texture2D/子资产引用 | 位值/数组顺序/null与空分别保留；引用二元组及实际对象相等，不能用实验 `.asset` 引用测试冒充新格式测试 |
| format错误、version=1、缺document、裸旧根、未知/错误基类 `$type`、未知/重复字段 | 非空错误，整份预检失败，不调用未知类型构造器、不切换会话、不写文件 |
| null组/轨/片段、重复节点、负/非有限时间、嵌套非有限业务float、缺引用解析器/失效或临时引用 | 显式失败，不修复为可保存的半图，不用null替换失败引用 |
| 只改视域/折叠；改业务字段；美化与压缩 | 前者不标作者脏且完整作者快照不变；业务修改标脏且一步Undo；两种输出同作者内容和精确位值 |

下面是同一封套内非空点事件样本，根length取真实末点0.5，片段基类存储length保留其默认1（Signal有效Length仍为0）。派生字段及基类私有字段同时存在；回归测试直接消费此块，而非仅验证自己的编码输出。

```json
{
  "format": "actioneditor.json",
  "version": 2,
  "document": {
    "$type": "NBC.ActionEditor.Samples.FeatureSample.FeatureSampleDocument, NBC.ActionEditor.Domain",
    "Background": { "a": 1, "b": 0.25, "g": 0.125, "r": 0.5 },
    "Notes": "Point contract fixture",
    "Version": 1,
    "rootItems": [{
      "$type": "NBC.ActionEditor.Samples.FeatureSample.FeatureSampleGroup, NBC.ActionEditor.Domain",
      "ActorId": 0,
      "active": true,
      "isLocked": false,
      "name": "Markers",
      "tracks": [{
        "$type": "NBC.ActionEditor.Samples.FeatureSample.FeatureSampleMarkerTrack, NBC.ActionEditor.Domain",
        "actionClips": [{
          "$type": "NBC.ActionEditor.Samples.FeatureSample.FeatureSampleMarker, NBC.ActionEditor.Domain",
          "EventCode": 7,
          "Message": "Marker",
          "Notes": null,
          "length": 1,
          "name": "Marker",
          "startTime": 0.5
        }],
        "active": true,
        "color": { "a": 1, "b": 1, "g": 1, "r": 1 },
        "isLocked": false,
        "name": "Marker track"
      }]
    }],
    "length": 0.5,
    "rangeMax": 6,
    "rangeMin": 0
  }
}
```

还原后Signal有效`Length==0`、基类存储length仍为1、`StartTime`位为`0x3f000000`、EventCode=7，1组1轨1片段且父引用正确。实现通过受控字段赋值而非setter回写，不能把0.5吸附或被预置构造器追加片段。此规则也要求区间blend和跨混合缓存读回一致；不能以字段隐藏或暂未使用为由丢弃/规范化其原始float位值。

## 历史合同记录

以下保留原日期与事实，仅用于追溯，不定义当前产品合同。

## R01–R10 当前入口契约（覆盖下文的正式存储与窗口取舍）

用户后续明确要求先完善原版，再以原生 UI Toolkit 重绘原有编辑体验，并共用 JSON。当前两个菜单分别进入 `ActionEditorWindow` 与 `JsonActionWorkbench`，均使用 `App.Session : JsonDocumentSession`、原有 `Asset → Group → Track → Clip` 和 FullSerializer JSON。只有一个当前文档/选择/播放头；不是下文实验平台的每窗口独立会话。旧 `.asset` 实验代码、验证和历史契约保留，但不作为两个主菜单的数据格式，不自动迁移任何资产。

共用模型沿用 float 秒和原有类型扩展，不宣称获得下文的稳定 ID、double 时间或确定性运行时语义。JSON 编辑快照提供整图 Undo；嵌套命令归外层事务，失败回滚。缩放与折叠不计入作者数据脏状态。保存采用暂存读回、内容一致性校验、原子替换及磁盘版本比较；外部修改时拒绝覆盖。切换先成功读取候选，再释放旧会话。未知类型读取失败保持原会话，不自动删未知数据后保存。

原生属性视图覆盖公共实例字段、条件/选项/资源路径、常见 Unity 值类型和集合；不嵌入旧 IMGUI 业务 Inspector/Header。样例状态窗口仅只读求值，不证明 Combat 或任意外部预览副作用安全。任意 Unity Object 引用的跨重启保存仍未验收，字符串路径字段不是 GUID 映射。以下 A02 章节属于保留的 `.asset` 实验方案，不能用其测试通过替代当前 JSON 界面的验证。

原版布局还原：原生 Header 顺序为文档选择、类型自定义内容、弹性空白、最后保存时间、保存/磁吸/设置图标，播放区不包含分隔条。`HeaderBase.CreateGUI()` 提供原生扩展入口，样例已实现；不调用 `OnGUI()` 代绘。欢迎页与设置保留原版内容顺序，属性时间和混合控件保留横排结构。时间变换采用整个窗口的时间区域宽度，不因垂直滚动条出现而改变。截图和自动测试不代替用户最终布局验收。

JSON 的 Single 值通过工程局部 FullSerializer converter 直接提升为 double 数字写出，避免原 primitive converter 的 float→decimal 舍入改变编辑值及重算总时长；不升级或修改第三方 DLL。旧 JSON 数字仍可读取。保存保持严格内容比较，失败仅记录差异字段路径、不记录值。原子替换失败保留底层异常和 HRESULT，不删除目标作为降级、不自动重放结果未知的提交；文件占用须先核查再由用户重试。

共用 JSON 窗口的“放弃修改”恢复最近一次打开/成功提交时的完整基线，清除未完成编辑及该会话 Undo/Redo，不读写磁盘、不改变磁盘版本基线；另一视图同步看到已回退内容。恢复快照同时携带该基线。关闭原版窗口会停止共用播放、清除暂停与播放头、释放预览，即使 Toolkit 窗口仍开着也不继续播放。

RF02明确的纠错边界：文档选择只列`.json`（扩展名不区分大小写），回调在解码前再次拒绝有路径的非JSON资源；通用资源选择器不默认限制扩展名，无路径内存TextAsset仍可用于创建及测试。原版多选移动从起拖快照应用同一位移，最早片段至零点后不压缩其他片段间距。每次拉伸重建非选中片段磁吸参考；折叠组内片段不参与单击、包含或框选查询。原版集合折叠状态按owner与字段保存，null不串状态且仅显式添加才创建内容。中键区域外释放、Esc和失焦取消平移，其他按钮不能续用中键状态。此轮不统一两UI最短长度、选择阈值、快捷键或缩放手感，相关人工差异及后续任务见唯一清单。

RF03所有权：`App.Context`为产品内唯一`ActionEditorContext`，拥有JsonDocumentSession、源TextAsset、选择、剪贴板和播放状态/AssetPlayer。App原有入口与AssetPlayer.Inst均转发同一状态，不另存副本。上下文服务不持有具体窗口或GUI事件；Unity Selection、对话框和域重载恢复属于宿主适配。两窗口注册至`App.Views`，重复注册替换原回调，关闭解除注册且更新资格转交下一注册窗口；原版关闭仍停止共用播放。视图尺寸/焦点/原生捕获仍各UI持有，旧App.Window/Width和静态手势留待后续迁移，当前不声称跨窗口事务令牌或完整预览资源重构已完成。

RF04当前JSON编辑生命周期：BeginEdit(owner)返回发起者令牌，同owner复用、其他owner返回null。Update异常、Commit校验失败回滚整图，取消/重复提交/陈旧令牌不能结束新事务；无参legacy入口只结束自身legacy事务。两窗口、原版Inspector和拖动器持有自己的令牌，失焦/关闭只取消本方；全局放弃、打开、Undo/Redo使旧令牌失效。嵌套Session.Edit仍归活动外层，不是安全隔离。错误鼠标按钮释放不提交，活动Toolkit手势不弹上下文菜单。自身失焦仍按原习惯回滚；关闭另一窗口若导致owner自身失焦，也属于该规则。换图后清理旧选择引用，不承诺恢复对象身份。显式保存的活动事务策略与持久验证归RF05。

RF05存储边界：JsonDocumentCodec集中现有FullSerializer格式、图校验、作者内容比较与严格读回路径；JsonDocumentStore集中JSON路径、字节版本、原子提交和Unity导入。Session保留事务/Undo/基线/Recovery，旧静态入口转发。先拒绝覆盖既有另存目标再序列化，成功提交后先记录基线再导入，导入失败不得自动重放。既有自动保存跳过活动事务，显式保存不隐式结束令牌；保存焦点/快捷键仍由UI适配处理。隔离持久样例已覆盖占用失败不损字节、解锁后保存、恢复、保存后撤销和外部保护；不代表系统文件对话框、跨重启Unity引用或真实导入失败已验收。

RF06类型目录：DirectableTypeInfo缓存现有Attribute与点/伸缩/混合属性事实，锁定/激活与Blend=-1仍为实例状态。菜单保持原名称、分类排序并返回可独立修改的副本。Header/Inspector精确匹配优先，仅显式允许子类时回退到最近基类；同目标派生适配器覆盖父适配器，无关冲突列出双方完整来源。Toolkit缺自定义适配显式提示，不代绘IMGUI。两套属性邻居界限共用ClipTimingRules，保留半长度交叉混合与严格边缘拖动的既有差异，不据此统一最短长度或手感。

RF07结构编辑：两UI创建、批量删除和轨道移动经窄JsonEditCommand统一CanExecute(reason)/Execute，菜单打开后状态变化仍在执行时重验；失败回滚、不留部分创建/删除。复制/剪切/粘贴保留序列化参数，成功后才完成剪切和选择，剪切源新锁定拒绝，Duplicate失败保留旧剪贴板。原版创建轨道后选择等已有UI习惯保留，不强行添加新版选择变化；新建文档仍共用CreateLegacyAsset，不产生另一套存储。

RF08几何：两UI消费JsonTimelineTransform的float时间变换及TimelineGeometry可见行、矩形、折叠可见性、框选与手柄优先级；尺寸由各UI提供，各自绘制。Toolkit极短区间最少4px、点8px，原版点图标仍由原绘制器扩展，不强制统一外观。完整时间区宽不随垂直滚动条变化；几何计算不改作者数据，反向框选归一，短片段手柄相交左侧优先。

## JSON 业务扩展示例（RF13）

现有 `Runtime/Samples/FeatureSample/FeatureSampleDocument.cs` 是完整的非Combat扩展例子：`FeatureSampleMarker : ClipSignal` 长度为零，`FeatureSampleInterval : ClipCrossBlend` 有区间及混合；`Name/Category/Attachable/Color` 决定菜单名称、分类、允许父轨道和外观。公共字段及已有属性决定通用属性控件，首个片段分类仍限制轨道的创建菜单。新增此类业务类型不修改核心switch或分支。

自定义绘制属于各UI适配：`Editor/Samples/FeatureSamplePreview.cs` 的Header同时实现 `OnGUI()` 与原生 `CreateGUI()`，后者不嵌入前者；点/区间继承现有 `ActionClipInspector` 的共用属性编辑。可选预览没有绑定时不创建实例，样例状态窗口只读当前播放头，不触发游戏逻辑。Header/Inspector的精确、显式子类回退及冲突规则见RF06，预览的精确绑定/释放和异常规则见RF12。字段字符串资源路径不等同于Unity对象跨重启引用证明。

`ExtensionEndToEndTests` 通过原版实际菜单构建产生的回调及Toolkit菜单事件，分别创建同样的点/区间；通过真实IMGUI文本输入及Toolkit延迟文本输入修改字段，并验证事务取消、共用路径提交、Undo/Redo、FullSerializer重开得到完全相同作者内容。菜单构建提取为私有方法供原版原入口调用，没有新增业务API；测试不声称自动操作系统弹出菜单。实际磁盘保存由固定授权Shared.json探针单独验证，不扩大到两个窗口的保存对话框或任意路径。

## 时间坐标和数据草案

RF12播放：App只注册一个EditorApplication.update共享更新入口，有注册视图时驱动自动保存及播放；两窗口自己的update仅更新视图，不再调用共享播放。AssetPlayer只持有播放头/前帧与ActionEditorPreviewSession，后者独占预览实例和指针，重复释放幂等；初始化部分失败、求值回调异常都会释放已拥有资源并传播异常，播放故障停机，不再吞异常继续预览。类型目录缓存可选预览工厂，保持精确绑定及派生适配器覆盖，无关冲突报双方来源，无绑定无预览。停止回调异常也在finally释放；原版关闭仍停止全局播放，最后窗口退出同样释放。文档变化/Undo/放弃和重载钩子沿用Reset，不因缩放/重绘重建。没有引入Combat工厂或场景副作用；真实域重载和任意第三方预览资源仍非已验收范围。

RF11属性：PropertyDescription缓存字段、名称、条件、选项真实常量及资源/范围说明；两UI保留既有字段顺序、无名称时的显示差异、横排时间和原生控件。JsonPropertyPath按字段/集合索引重新读取并回写嵌套值类型，当前文档对象的旧引用/锁定在写前拒绝；集合变更先验证所有者再修改。JsonPropertyEditing提供事务、元素创建和数组/列表增删重排操作，Toolkit不再调用InspectorsBase。原版选项原先把常量当索引，改为真实常量映射，非连续值不再被误写为索引；未知值在没有用户选择时保留。原版既有Inspector外层令牌继续归并本轮写入，Toolkit延迟字段仍由Unity控件处理回车/失焦/Esc。当前JSON入口仍只显示首个选择的属性，没有新增批量混合编辑或集合重排按钮；Mixed与Move为共享操作能力，其单测不代表UI已提供这些功能。缺自定义Toolkit适配仍提示，不代绘IMGUI。

RF10导航：TimelineNavigation统一225×滚轮符号/窗口宽×视域跨度的缩放候选，左端固定，是用户明确选择的行为变化；240秒上限检查、最小视域仍沿用原模型。两UI平移共用候选计算，但保留原版过零时缩短视域、Toolkit过零保留跨度的既有差异。原版滚轮原本不受scrollWheelZooms控制，Toolkit仍受其控制，不借统一步进改变偏好作用域。ActionEditorSelection提供无副作用点击候选，UI保留原版替换/Toolkit Ctrl追加与已选集合保留策略。Toolkit文本控件的Delete/Ctrl快捷键和Esc不被时间轴父节点抢占；原版没有新增全窗口快捷键。原版Esc/失焦同步清理待提交框选矩形，取消后不会残留旧框选。标尺、分栏与底栏的输入适配、现有阈值及布局仍各UI持有，人工H02/H04/H05/H06待确认。

RF09手势：原版OriginalTimelineGesture由每个ActionEditorWindow独立持有快照、事务、磁吸参考及轨道落点，不再以ItemDragger静态状态跨窗口共享。Toolkit仍由每个canvas持有PointerCapture。TimelineCandidates共用快照位移零点约束、拉伸候选、非选中磁吸参考构造及吸附计算；调用者显式保留原策略。原版最短0.1秒、Toolkit0.01秒；原版阈值含边界且起点优先，Toolkit严格小于阈值且参考顺序优先。原版提交时用CanValidTime整体恢复不合法候选，Toolkit原有重叠容许及锁定集合过滤不在此次重构中暗改。原版网格始终量化，Toolkit网格跟随磁吸偏好；Shift只绕过磁吸，保留网格策略。上述差异需人工体验决策，不能把共用计算理解为已统一操作语义。

平台时间存储为有限、非负的 double 秒。文档长度必须大于 0；点事件 duration=0，区间 duration>0，start+duration 不得超出文档长度。所有非法数值在提交前拒绝，不在绘制时修正。界面帧吸附是显式编辑操作，不反向量化已有数据。

正式模型为 `ActionDocument : ScriptableObject`，包含 `FormatVersion`、`Id`、`Duration`、Group 列表；Group 拥有 Track 列表，Track 拥有 `[SerializeReference]` Event 列表。每一层有稳定 ID。Event 具有 start、duration、kind、role 与显式排序值，业务定义继承 Event 保存自己的参数。工作区（折叠/缩放/滚动/分栏）、选择、脏状态和播放实例不属于此定义。

业务可以选择逻辑 Tick，但通过适配器转换到平台秒，不把平台存储强制成 60 FPS。`seconds=tick/rate`；界面转最近帧使用中点远离零取整，源动画采样率与业务逻辑率分别传入，不推断为同一个值。首版不交付 Combat 调度或确定性模拟，也不声称整数 Tick 能使物理/浮点/Animator 确定。

## 边界与真值表

游戏向前推进跨点规则为 `(previous,current]`；启动第一次允许 `[0,current]`，包括首次 current=0。重复求值同一时间不再触发。倒退和随机跳转不是向前执行，不能调用此入口触发副作用。

区间活动范围为 `[start,end)`。一次跨过短区间仍按边界时间执行一次 Enter、最终求值和一次 Exit，不因当前时刻已越过而漏掉整个事件。点事件 at=duration 在正常结束前触发。事件排序先绝对时间，再循环号，再 BoundaryPhase（Exit、Point、Enter），再 Group 顺序、Track 顺序、Event 显式排序值、Event ID ordinal。此排序是语义契约，不依赖字典枚举或绘制顺序。

| 输入 | 明确预期 |
|---|---|
| 首次 0→0，点 0 | 触发一次；重复 0→0 不再触发 |
| 0→1，点 0.25 / 0.75 / 1 | 全部按时间执行一次 |
| 1→2，点 1 | 不再触发 |
| 区间 [0.25,0.75)，求值 0.25 / 0.75 | 活动 / 不活动 |
| 0→1 跨过区间 [0.25,0.250001) | Enter、最终求值、Exit，各一次 |
| 同刻区间结束、点、区间开始 | Exit→Point→Enter，再按稳定序号 |
| 长度 1，首次 0→3，点 0 | 循环 0、1、2、3 各一次，共 4 次 |
| 长度 1，首次 0→3，点 1 | 循环 0、1、2 的末端，共 3 次 |
| 长度 1，0→3.5，点 0.25 | 0.25、1.25、2.25、3.25，共 4 次 |
| 长度 1，0.25→3.5，点 0.25 | 1.25、2.25、3.25，共 3 次 |
| 循环边界 t=1，旧循环末端与新循环 0 | 旧循环末端先完整结束，然后新循环 0；两个事件身份不同 |
| 随机跳转/倒拖 | 重建预览状态，不执行逻辑伤害或无条件重播点事件音效 |
| Completed | 向前推进到文档末端，执行末端事件，再一次释放活动效果 |
| Stopped | 不补发未来点事件，结束当前活动效果，预览游标按工作区约定归零 |
| Cancelled | 不补发未来事件，通知业务取消原因后一次释放；不同于正常 Exit |
| Faulted | 记录故障与业务上下文，取消本实例并一次释放，不能吞异常假装完成 |

循环展开不丢弃中间循环，不用任意步数上限悄悄跳事件。极大推进可以显式拒绝并报告资源限制，但不能返回“已全部执行”。这里只交付边界辅助函数与真值表，不把它们称为完整调度器。短区间、停止/取消、排序的真实播放实例集成在相应业务任务继续验收。

## 身份、复制、分类与重叠

- 持久 ID 为非空、规范小写的 32 位 GUID N 字符串，不用 GetInstanceID。加载时遇到重复/缺失 ID 报错，不自动改写未知用户数据。
- 移动/重排不改 ID；复制事件生成新 ID 并深拷贝参数集合，保留持久 Unity 资源引用；复制文档为文档及全部内部对象生成新 ID。Undo/Redo 恢复原 ID，不能每次求值都生成。
- 执行状态以播放实例 ID、事件 ID、循环号/触发序号定位，不能放在定义或按 AnimationClip ID 合并。该身份仅提供边界，不实现网络去重协议。
- EventRole 首版为 Logic / Presentation。每个事件类型在注册中固定默认分类，业务校验可限制用途，不把分类仅用于颜色。
- Track 的 OverlapPolicy 首版为 Allow / Reject。Reject 仅禁止正长度区间相交，邻接合法，点事件允许同刻；业务可以通过校验扩展互斥、混合或数量限制，不在核心增加 Combat 类型判断。

## 扩展接口草案

类型描述符以不可变字符串 TypeId 注册，持有定义类型、工厂、原生属性视图工厂、时间轴摘要/绘制描述、校验器及可选预览/导出工厂。核心提供 `IEventDescriptor`、`IEventValidator`、`IEventPreview`、`IEventExporter` 职责，具体参数在 B05 随真实 UI 固化，不制造空程序集。

注册先校验全部输入再发布。相同 TypeId 或定义类型发生冲突时拒绝整个候选注册并提供双方身份，不允许“后注册覆盖”。工厂返回独立事件，核心验证类型、时间和 ID。未知 TypeId、缺失 managed-reference 类型、失效资源引用保留可诊断状态并禁止保存，不静默删事件。

预览只返回可释放的会话资源，必须明确取消来源和所有权，不约定 UniTask/业务框架。每个窗口持有自己的文档会话、注册上下文和选择，所有属性修改进入同一事务。

### B05 已固化接口

Editor 的 `NBC.ActionEditor.Extensibility.IEventRegistration.Describe()` 返回 `EventDescriptor`；由 TypeCache 发现无参构造 provider。每个描述符指定 TypeId、DefinitionType、Owner、Factory、Summary、Properties、Validate 及可选 Preview/Exporter 工厂。注册先验证整个批次，包括类型/ID 唯一性、工厂类型和独立实例/ID，再发布；不允许覆盖已有 owner。

`EventPropertyContext` 提供原生字段与事务修改入口，业务属性不直接绑定正式文档的 SerializedObject。文档会话、事务和正式存储调用同一注册校验。未知类型可保留诊断，禁止保存；未知 managed-reference 类型不加载为可保存的空事件。资源允许 null，非空资源必须为有效持久引用。预览/导出工厂为可选能力，当前没有默认业务预览或导出实现，不把接口存在当作业务已接入。

Unity 序列化会规范化 null 集合/字符串。平台示例为可空 Tags/Message/Name 保存显式空值标记，Undo/Redo 和保存不把用户 null 选择默认为 empty。业务新增其他可空值类型时同样需要给出序列化表示与往返测试。
