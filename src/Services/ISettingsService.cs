using EasyScreenRecord.Models;

namespace EasyScreenRecord.Services
{
    public interface ISettingsService
    {
        AppSettings CurrentSettings { get; }
        void Save();
    }
}
