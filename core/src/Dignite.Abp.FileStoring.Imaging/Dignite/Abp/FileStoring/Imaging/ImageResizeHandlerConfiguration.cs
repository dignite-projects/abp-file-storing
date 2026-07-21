using Volo.Abp.BlobStoring;

namespace Dignite.Abp.FileStoring.Imaging;

public class ImageResizeHandlerConfiguration
{
    private readonly BlobContainerConfiguration _containerConfiguration;

    public ImageResizeHandlerConfiguration(BlobContainerConfiguration containerConfiguration)
    {
        _containerConfiguration = containerConfiguration;
    }

    public int ImageWidth
    {
        get => _containerConfiguration.GetConfigurationOrDefault<int>(ImageResizeHandlerConfigurationNames.ImageWidth);
        set => _containerConfiguration.SetConfiguration(ImageResizeHandlerConfigurationNames.ImageWidth, value);
    }

    public int ImageHeight
    {
        get => _containerConfiguration.GetConfigurationOrDefault<int>(ImageResizeHandlerConfigurationNames.ImageHeight);
        set => _containerConfiguration.SetConfiguration(ImageResizeHandlerConfigurationNames.ImageHeight, value);
    }

    public bool ImageSizeMustBeLargerThanPreset
    {
        get => _containerConfiguration.GetConfigurationOrDefault<bool>(ImageResizeHandlerConfigurationNames.ImageSizeMustBeLargerThanPreset, false);
        set => _containerConfiguration.SetConfiguration(ImageResizeHandlerConfigurationNames.ImageSizeMustBeLargerThanPreset, value);
    }
}
