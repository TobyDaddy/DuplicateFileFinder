using System;
using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Threading;

namespace DuplicateFileFinderWPF
{
    public class LocalizationManager : INotifyPropertyChanged
    {
        private static LocalizationManager? instance;
        private ResourceManager resourceManager;

        public static LocalizationManager Instance => instance ??= new LocalizationManager();

        public event PropertyChangedEventHandler? PropertyChanged;

        private LocalizationManager()
        {
            // 调整资源基名，确保与嵌入资源一致
            resourceManager = new ResourceManager("DuplicateFileFinderWPF.Resources.Strings", typeof(LocalizationManager).Assembly);
        }

        public string this[string key]
        {
            get
            {
                var value = resourceManager.GetString(key);
                return value ?? key;
            }
        }

        public void ChangeLanguage(string cultureCode)
        {
            var culture = new CultureInfo(cultureCode);
            Thread.CurrentThread.CurrentUICulture = culture;
            Thread.CurrentThread.CurrentCulture = culture;
            
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;

            // 通知所有绑定的UI元素更新
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            
            // 逐个通知每个属性更新（仅保留实际使用的键）
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AppTitle)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectFolder)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Browse)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StartSearch)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Cancel)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Delete)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FilePreview)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RotateRight)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ScannedFiles)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MarkedForDeletion)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Error)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(English)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Chinese)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Settings)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Help)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Minimize)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Maximize)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Close)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DeleteMarkedFiles)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowInExplorer)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Ready)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Scanning)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Processing)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Completed)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ScanningFiles)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ScanCancelled)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ScanError)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DuplicateGroup)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FoundDuplicatesStatus)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NoDuplicatesStatus)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CannotMarkAllFiles)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OperationRestriction)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OK)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HelpDialogTitle)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HelpContent)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectFolderDescription)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CancellingScan)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CancellingScanStatus)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ScanFiles)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FileList)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FilePreviewTitle)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PreviewWithZoomAndDrag)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CannotLoadImage)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VideoLoading)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CannotLoadVideo)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UnsupportedFileFormat)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectFileToPreview)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GetInfoFailed)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RemainingGroupsStatus)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TotalGroupsStatus)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AllDuplicatesDeleted)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ScanningStatus)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DuplicateGroupPrefix)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UpdateStatusFound)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VideoPlayError)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VideoStopError)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VideoPlaybackFailed)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VideoClickToPlay)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VideoInitError)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProgressSeekError)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PreviewWithZoom)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FileProperties)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CancelScan)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CreationDate)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ModificationDate)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FileSize)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FileLocation)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Confirm)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OpenLocationTooltip)));

            // 移动到文件夹流程相关
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ConfirmMoveMarkedFiles)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MoveCompletedMessage)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MoveCompletedMessageWithSpace)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectMoveTargetTitle)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectMoveTargetContent)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ChangeFolder)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FolderChangedTo)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EnableDebugLogging)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EnableDebugLoggingTip)));
        }

        // 便于在XAML中绑定的属性（仅保留实际使用的键）
        public string AppTitle => this["AppTitle"];
        public string Language => this["Language"];
        public string SelectFolder => this["SelectFolder"];
        public string Browse => this["Browse"];
        public string StartSearch => this["StartSearch"];
        public string Cancel => this["Cancel"];
        public string Delete => this["Delete"];
        public string FilePreview => this["FilePreview"];
        public string RotateRight => this["RotateRight"];
        public string ScannedFiles => this["ScannedFiles"];
        public string MarkedForDeletion => this["MarkedForDeletion"];
        public string Error => this["Error"];
        public string English => this["English"];
        public string Chinese => this["Chinese"];
        public string Settings => this["Settings"];
        public string Help => this["Help"];
        public string Minimize => this["Minimize"];
        public string Maximize => this["Maximize"];
        public string Close => this["Close"];
        public string DeleteMarkedFiles => this["DeleteMarkedFiles"];
        public string ShowInExplorer => this["ShowInExplorer"];
        public string Ready => this["Ready"];
        public string Scanning => this["Scanning"];
        public string Processing => this["Processing"];
        public string Completed => this["Completed"];
        public string ScanningFiles => this["ScanningFiles"];
        public string ScanCancelled => this["ScanCancelled"];
        public string ScanError => this["ScanError"];
        public string DuplicateGroup => this["DuplicateGroup"];
        public string FoundDuplicatesStatus => this["FoundDuplicatesStatus"];
        public string NoDuplicatesStatus => this["NoDuplicatesStatus"];
        public string CannotMarkAllFiles => this["CannotMarkAllFiles"];
        public string OperationRestriction => this["OperationRestriction"];
        public string OK => this["OK"];
        public string HelpDialogTitle => this["HelpDialogTitle"];
        public string HelpContent => this["HelpContent"];
        public string SelectFolderDescription => this["SelectFolderDescription"];
        public string CancellingScan => this["CancellingScan"];
        public string CancellingScanStatus => this["CancellingScanStatus"];
        public string ScanFiles => this["ScanFiles"];
        public string FileList => this["FileList"];
        public string FilePreviewTitle => this["FilePreviewTitle"];
        public string PreviewWithZoomAndDrag => this["PreviewWithZoomAndDrag"];
        public string CannotLoadImage => this["CannotLoadImage"];
        public string VideoLoading => this["VideoLoading"];
        public string CannotLoadVideo => this["CannotLoadVideo"];
        public string UnsupportedFileFormat => this["UnsupportedFileFormat"];
        public string SelectFileToPreview => this["SelectFileToPreview"];
        public string GetInfoFailed => this["GetInfoFailed"];
        public string RemainingGroupsStatus => this["RemainingGroupsStatus"];
        public string TotalGroupsStatus => this["TotalGroupsStatus"];
        public string AllDuplicatesDeleted => this["AllDuplicatesDeleted"];
        public string ScanningStatus => this["ScanningStatus"];
        public string DuplicateGroupPrefix => this["DuplicateGroupPrefix"];
        public string UpdateStatusFound => this["UpdateStatusFound"];
        public string VideoPlayError => this["VideoPlayError"];
        public string VideoStopError => this["VideoStopError"];
        public string VideoPlaybackFailed => this["VideoPlaybackFailed"];
        public string VideoClickToPlay => this["VideoClickToPlay"];
        public string VideoInitError => this["VideoInitError"];
        public string ProgressSeekError => this["ProgressSeekError"];
        public string PreviewWithZoom => this["PreviewWithZoom"];
        public string FileProperties => this["FileProperties"];
        public string CancelScan => this["CancelScan"];
        public string CreationDate => this["CreationDate"];
        public string ModificationDate => this["ModificationDate"];
        public string FileSize => this["FileSize"];
        public string FileLocation => this["FileLocation"];
        public string Confirm => this["Confirm"];
        public string OpenLocationTooltip => ShowInExplorer; // 复用“在资源管理器中显示”

        // 移动到文件夹流程相关
        public string ConfirmMoveMarkedFiles => this["ConfirmMoveMarkedFiles"];
        public string MoveCompletedMessage => this["MoveCompletedMessage"];
        public string SelectMoveTargetTitle => this["SelectMoveTargetTitle"];
        public string SelectMoveTargetContent => this["SelectMoveTargetContent"];
        public string ChangeFolder => this["ChangeFolder"];
        public string FolderChangedTo => this["FolderChangedTo"];
        public string MoveCompletedMessageWithSpace => this["MoveCompletedMessageWithSpace"];
    public string EnableDebugLogging => this["EnableDebugLogging"];
    public string EnableDebugLoggingTip => this["EnableDebugLoggingTip"];
    }
}
