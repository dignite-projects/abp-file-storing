using Volo.Abp.BlobStoring;

namespace Dignite.Abp.FileStoring;

public class FileSizeLimitHandlerConfiguration
{
    private readonly BlobContainerConfiguration _containerConfiguration;

    public FileSizeLimitHandlerConfiguration(BlobContainerConfiguration containerConfiguration)
    {
        _containerConfiguration = containerConfiguration;
    }

    public int MaxFileSize
    {
        get => _containerConfiguration.GetConfigurationOrDefault<int>(FileSizeLimitHandlerConfigurationNames.MaxFileSize);
        set => _containerConfiguration.SetConfiguration(FileSizeLimitHandlerConfigurationNames.MaxFileSize, value);
    }
}
