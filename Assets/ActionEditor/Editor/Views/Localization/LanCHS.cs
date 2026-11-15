namespace NBC.ActionEditor
{
    [Name("简体中文")]
    public class LanCHS : ILanguages
    {
        
        public static string Rename = "重命名";
        public static string FirstFrame = "跳转到开始处";
        public static string FeatureSample = "功能体验样例（预置轨道）";
        public static string WelcomeHint = "新建资源，或打开已有资源开始编辑。";
        public static string WelcomeHelp = "选择片段可查看属性，拖动边缘可调整长度。";
        public static string CreateSafety = "仅创建新文件，不覆盖已有资源。创建后在发起界面打开。";
        public static string NoResourceTypes = "没有可创建的资源类型，请确认业务扩展已编译。";
        public static string InvalidResourceName = "请输入有效资源名称，不要包含路径。";
        public static string NewPathHint = "选择新文件路径，不会覆盖已有资源。";
        public static string CreateConfirmation = "资源类型：{0}\n新文件：{1}\n仅创建此文件并在发起界面打开，不覆盖已有资源。";
        public static string SaveAs = "另存为…";
        public static string Export = "导出";
        public static string FileFormat = "文件格式";
        public static string ExportHint = "导出独立副本，不改变当前来源或未保存的修改。仅创建新文件。";
        public static string NoExportFormats = "没有已实现的导出格式。";
        public static string SaveMenuHint = "保存（右键：另存为、导出）";
        public static string UnsavedChanges = "未保存的修改";
        public static string SaveBeforeSwitch = "切换前是否保存当前资源？";
        public static string Discard = "放弃";
        public static string NoDocument = "没有打开的资源。";
        public static string FinishEditBeforeSave = "请先完成或取消当前编辑，再保存。";
        public static string FinishEditBeforeExport = "请先完成或取消当前编辑，再导出。";
        public static string UnsupportedExport = "文件格式不支持导出。";
        public static string ExportSourceTarget = "导出目标不能是当前源文件，请选择新文件名。";
        public static string ExportReadbackFailed = "导出读回内容不一致，未提交文件。";
        public static string PreviewTitle = "样例状态预览";
        public static string PreviewHint = "跟随播放头显示状态。不会创建场景对象，不播放音频，不执行战斗逻辑。";
        public static string OpenSampleFirst = "请先新建或打开“功能体验样例（预置轨道）”。";
        public static string Playhead = "播放头";
        public static string CurrentIntervals = "当前区间";
        public static string CurrentLoops = "当前循环";
        public static string PreviousMarkers = "播放头之前的标记（状态，不是触发记录）";
        public static string PreviewInterval = "进度 {0} · 偏移 {1}";
        public static string PreviewLoop = "第 {0} 次 · 相位 {1}";

        //**********  Welcome *********
        public static string Title = "行为时间轴编辑器";
        public static string CreateAsset = "新建资源";
        public static string SelectAsset = "打开资源";
        public static string Seeting = "编辑器配置";


        //**********  Crate Window *********
        public static string CrateAssetType = "资源类型";
        public static string CrateAssetName = "资源名称";
        public static string CreateAssetFileName = "资源名称";
        public static string CreateAssetConfirm = "创建并打开";
        public static string CreateAssetReset = "重置";
        public static string CreateAssetTipsNameNull = "名称不能为空";
        public static string CreateAssetTipsRepetitive = "已存在同名时间轴";


        //**********  Preferences Window *********
        public static string PreferencesTitle = "编辑器首选项";
        public static string PreferencesTimeStepMode = "时间步长模式";
        public static string PreferencesSnapInterval = "时间步长";
        public static string PreferencesFrameRate = "帧率";
        public static string PreferencesMagnetSnapping = "剪辑吸附";
        public static string PreferencesMagnetSnappingTips = "是否开启剪辑自动吸附前后其他剪辑";
        public static string PreferencesScrollWheelZooms = "滚轮缩放";
        public static string PreferencesScrollWheelZoomsTips = "是否开启滚轮缩放时间轴区域";
        public static string PreferencesSavePath = "配置保存地址";
        public static string PreferencesSavePathTips = "创建和选择时的默认地址";
        public static string PreferencesAutoSaveTime = "自动保存时间";
        public static string PreferencesAutoSaveTimeTips = "定时自动保存操作的间隔时间";
        public static string PreferencesHelpDoc = "帮助文档";


        //**********  Commom *********
        public static string Select = "选择";
        public static string SelectFile = "选择文件";
        public static string SelectFolder = "选择文件夹";
        public static string TipsTitle = "提示";
        public static string TipsConfirm = "确定";
        public static string TipsCancel = "取消";
        public static string CompilingTips = "编译中\n...请稍后...";
        public static string Disable = "禁用";
        public static string Locked = "锁定";
        public static string Save = "保存";

        //**********  Header *********
        public static string HeaderLastSaveTime = "最后保存时间：{0}";
        public static string HeaderSelectAsset = "选中：[{0}]";
        public static string OpenPreferencesTips = "打开首选项界面";
        public static string SelectAssetTips = "打开资源";
        public static string OpenMagnetSnappingTips = "开启剪辑磁性吸附";
        public static string NewAssetTips = "新建资源";
        public static string BackMenuTips = "返回主菜单";
        public static string PlayLoopTips = "循环播放";
        public static string PlayForwardTips = "跳转结尾处";
        public static string StepForwardTips = "跳转下一帧";
        public static string PauseTips = "点击暂停";
        public static string PlayTips = "点击播放";
        public static string StopTips = "点击停止播放";
        public static string StepBackwardTips = "跳转上一帧";

        //**********  Group Menu *********
        public static string MenuAddTrack = "添加轨道";
        public static string MenuPasteTrack = "粘贴轨道";
        public static string GroupAdd = "添加组";
        public static string GroupDisable = "禁用组";
        public static string GroupLocked = "锁定组";
        public static string GroupReplica = "复制组";
        public static string GroupDelete = "删除组";
        public static string GroupDeleteTips = "确定删除组吗?";

        //**********  Track Menu *********
        public static string TrackDisable = "禁用轨道";
        public static string TrackLocked = "锁定轨道";
        public static string TrackCopy = "拷贝轨道";
        public static string TrackReplica = "复制轨道";
        public static string TrackDelete = "删除轨道";
        public static string TrackDeleteTips = "确定删除改轨道吗?";

        //**********  Clip Menu *********
        public static string ClipCopy = "拷贝";
        public static string ClipCut = "剪切";
        public static string ClipDelete = "删除";
        public static string MatchClipLength = "匹配长度";
        public static string MatchPreviousLoop = "匹配上个循环";
        public static string MatchNextLoop = "匹配下个循环";
        public static string ClipPaste = "粘贴 ({0})";

        //**********  Inspector *********
        public static string NotSelectAsset = "当前没有选中的时间轴对象。";
        public static string InsBaseInfo = "{0} 基础信息";
        public static string OverflowInvalid = "剪辑超出有效范围";
        public static string EndTimeOverflowInvalid = "剪辑结束时间超出有效范围";
        public static string StartTimeOverflowInvalid = "剪辑开始时间超出可播放范围";
    }
}