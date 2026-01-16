using System.Threading.Tasks;

namespace EasyScreenRecord.Services
{
    public interface IScreenRecorder
    {
        bool IsRecording { get; }
        Task StartRecordingToFileAsync(object captureTarget, Windows.Storage.StorageFile file, System.Windows.Rect? region = null);
        Task StopRecordingAsync();
    }
}
