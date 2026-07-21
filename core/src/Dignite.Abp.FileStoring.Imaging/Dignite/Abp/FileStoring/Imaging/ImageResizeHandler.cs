using System.Threading.Tasks;
using Dignite.Abp.FileStoring;
using SixLabors.ImageSharp;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Imaging;

namespace Dignite.Abp.FileStoring.Imaging;

public class ImageResizeHandler : IFileHandler, ITransientDependency
{
    private readonly IImageResizer _imageResizer;
    private readonly IImageCompressor _imageCompressor;

    public ImageResizeHandler(IImageResizer imageResizer, IImageCompressor imageCompressor)
    {
        _imageResizer = imageResizer;
        _imageCompressor = imageCompressor;
    }

    public async Task ExecuteAsync(FileHandlerContext context)
    {
        var configuration = context.ContainerConfiguration.GetImageResizeConfiguration();

        if (ImageFormatHelper.IsValidImage(context.MimeType, ImageFormatHelper.AllowedImageUploadFormats))
        {
            using (var image = await Image.LoadAsync(context.BlobStream))
            {
                context.BlobStream.Position = 0;
                if (configuration.ImageSizeMustBeLargerThanPreset)
                {
                    if (image.Width < configuration.ImageWidth || image.Height < configuration.ImageHeight)
                    {
                        throw new BusinessException(
                            code: FileStoringImagingErrorCodes.ImageSizeTooSmall,
                            message: "Image size must be larger than preset!",
                            details: "Uploaded image must be larger than: " + configuration.ImageWidth + "x" + configuration.ImageHeight
                        );
                    }
                }

                if (image.Width > configuration.ImageWidth || image.Height > configuration.ImageHeight)
                {
                    var resizeResult = await _imageResizer.ResizeAsync(
                        context.BlobStream,
                        new ImageResizeArgs((uint)configuration.ImageWidth, (uint)configuration.ImageHeight, ImageResizeMode.Max),
                        context.MimeType
                    );

                    if (resizeResult.State == ImageProcessState.Done)
                    {
                        context.BlobStream = resizeResult.Result;
                    }
                    else
                    {
                        if (resizeResult.Result is not null && context.BlobStream != resizeResult.Result && resizeResult.Result.CanRead)
                        {
                            context.BlobStream = resizeResult.Result;
                        }
                        else
                        {
                            throw new BusinessException(
                                code: FileStoringImagingErrorCodes.ImageResizeFailure,
                                message: resizeResult.State.ToString()
                            );
                        }
                    }
                }

                var compressResult = await _imageCompressor.CompressAsync(
                    context.BlobStream,
                    mimeType: context.MimeType
                );

                if (compressResult.State == ImageProcessState.Done)
                {
                    context.BlobStream = compressResult.Result;
                }
                else if (compressResult.Result is not null && context.BlobStream != compressResult.Result && compressResult.Result.CanRead)
                {
                    context.BlobStream = compressResult.Result;
                }
            }
        }
    }
}
