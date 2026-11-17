namespace ActionSequenceEditor
{
    [Name("English")]
    public class EnglishText : ILanguageCatalog
    {
        
        public static string Rename = "Rename";
        public static string FirstFrame = "Go to start";
        public static string FeatureSample = "Feature sample (preset tracks)";
        public static string WelcomeHint = "Create a resource or open an existing one to start editing.";
        public static string WelcomeHelp = "Select a clip to inspect properties. Drag its edges to resize.";
        public static string CreateSafety = "Creates a new file only, without overwriting resources. Opens in the initiating view.";
        public static string NoResourceTypes = "No resource types are available. Check that business extensions compile.";
        public static string InvalidResourceName = "Enter a valid resource name without a path.";
        public static string NewPathHint = "Choose a new file path. Existing resources will not be overwritten.";
        public static string CreateConfirmation = "Resource type: {0}\nNew file: {1}\nCreates only this file and opens it in the initiating view. No overwrites.";
        public static string SaveAs = "Save as…";
        public static string Export = "Export";
        public static string FileFormat = "File format";
        public static string ExportHint = "Exports an independent copy without changing the source or unsaved changes. Creates a new file only.";
        public static string NoExportFormats = "No export formats are available.";
        public static string SaveMenuHint = "Save (right-click: Save as, Export)";
        public static string UnsavedChanges = "Unsaved changes";
        public static string SaveBeforeSwitch = "Save the current resource before switching?";
        public static string Discard = "Discard";
        public static string NoSequence = "No resource is open.";
        public static string FinishEditBeforeSave = "Finish or cancel the current edit before saving.";
        public static string FinishEditBeforeExport = "Finish or cancel the current edit before exporting.";
        public static string UnsupportedExport = "This file format does not support export.";
        public static string ExportSourceTarget = "The export target cannot be the source file. Choose a new file name.";
        public static string ExportReadbackFailed = "Export readback differs. No file was committed.";
        public static string PreviewTitle = "Sample state preview";
        public static string PreviewHint = "Shows state at the playhead. Creates no scene objects and plays no audio or combat logic.";
        public static string OpenSampleFirst = "Create or open the Feature sample (preset tracks) first.";
        public static string Playhead = "Playhead";
        public static string CurrentIntervals = "Active intervals";
        public static string CurrentLoops = "Active loops";
        public static string PreviousMarkers = "Markers before the playhead (state, not trigger history)";
        public static string PreviewInterval = "Progress {0} · Offset {1}";
        public static string PreviewLoop = "Loop {0} · Phase {1}";

        //**********  Welcome *********
        public static string Title = "ActionSequenceEditor";
        public static string CreateSequence = "New resource";
        public static string OpenSequence = "Open resource";
        public static string Settings = "Preferences";

        //**********  Crate Window *********
        public static string CreationTypeLabel = "Resource type";
        public static string CreationNameLabel = "Resource name";
        public static string CreateAssetFileName = "Resource name";
        public static string CreateAssetConfirm = "Create and open";
        public static string CreateAssetReset = "Reset";
        public static string CreateAssetTipsNameNull = "Name cannot be empty!";
        public static string CreateAssetTipsRepetitive = "Duplicate name!";

        //**********  Preferences Window *********
        public static string SettingsTitle = "Editor Preferences";
        public static string SettingsTimeStepMode = "Time Step Mode";
        public static string SettingsSnapInterval = "Snap Interval";
        public static string SettingsFrameRate = "Frame Rate";
        public static string SettingsMagnetSnapping = "Magnet Snapping";

        public static string SettingsMagnetSnappingTooltip =
            "Turn on other clips before and after the clip is automatically attached";

        public static string SettingsScrollWheelZooms = "Scroll Wheel Zooms";
        public static string SettingsScrollWheelZoomsTooltip = "Turn on the scroll wheel to zoom the timeline area";
        public static string SettingsSavePath = "Asset save path";
        public static string SettingsSavePathTooltip = "Default path on creation and selection";
        public static string SettingsAutoSaveInterval = "auto save time";
        public static string SettingsAutoSaveIntervalTooltip = "Auto save interval";
        public static string SettingsHelpDocument = "Help doc";


        //**********  Commom *********
        public static string Select = "Select";
        public static string SelectFile = "Select File";
        public static string SelectFolder = "Select Folder";
        public static string TipsTitle = "Tips";
        public static string TipsConfirm = "Confirm";
        public static string TipsCancel = "Cancel";
        public static string CompilingTips = "Compiling\n... please wait...";
        public static string Disable = "Disable";
        public static string Locked = "Locked";
        public static string Save = "Save";

        //**********  Header *********
        public static string HeaderLastSaveTime = "Last save time：{0}";
        public static string HeaderSelectAsset = "Select：[{0}]";
        public static string OpenSettingsTooltip = "Open Preferences";
        public static string OpenSequenceTooltip = "Open resource";
        public static string OpenMagnetSnappingTips = "Open Magnet Snapping";
        public static string NewAssetTips = "New resource";
        public static string BackMenuTips = "Back menu";
        public static string PlayLoopTips = "Loop Play";
        public static string PlayForwardTips = "Jump to the end";
        public static string StepForwardTips = "Jump to next frame";
        public static string PauseTips = "Pause";
        public static string PlayTips = "Play";
        public static string StopTips = "Stop";
        public static string StepBackwardTips = "Jump to previous frame";

        //**********  Group Menu *********
        public static string MenuAddTrack = "Add Track";
        public static string MenuPasteTrack = "Paste Track";
        public static string GroupAdd = "Add Group";
        public static string GroupDisable = "Disable Group";
        public static string GroupLocked = "Locked Group";
        public static string GroupReplica = "Replica Group";
        public static string GroupDelete = "Delete Group";
        public static string GroupDeleteTips = "confirm delete group?";

        //**********  Track Menu *********
        public static string TrackDisable = "Disable Track";
        public static string TrackLocked = "Locked Track";
        public static string TrackCopy = "Copy Track";
        public static string TrackReplica = "Replica Track";
        public static string TrackDelete = "Delete Track";
        public static string TrackDeleteTips = "confirm delete track?";

        //**********  Clip Menu *********
        public static string ClipCopy = "Copy";
        public static string ClipCut = "Cut";
        public static string ClipDelete = "Delete";
        public static string MatchClipLength = "Match Clip Length";
        public static string MatchPreviousLoop = "Match Previous Loop";
        public static string MatchNextLoop = "Match Next Loop";
        public static string ClipPaste = "Paste ({0})";

        //**********  Inspector *********
        public static string NoSelection = "not selected asset。";
        public static string InsBaseInfo = "{0} base info";
        public static string OverflowInvalid = "Clip is outside of playable range";
        public static string EndTimeOverflowInvalid = "Clip end time is outside of playable range";
        public static string StartTimeOverflowInvalid = "Clip start time is outside of playable range";
    }
}