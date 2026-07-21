using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.FileStoring;

public class FileSizeLimitHandler : IFileHandler, ITransientDependency
{
    public Task ExecuteAsync(FileHandlerContext context)
    {
        var configuration = context.ContainerConfiguration.GetFileSizeLimitConfiguration();
        if (configuration.MaxFileSize * 1024 < context.BlobStream.Length)
        {
            throw new BusinessException(
                code: FileErrorCodes.Files.FileTooLarge,
                message: "File object is too large",
                details: $"The file object size cannot exceed {configuration.MaxFileSize}M!"
            );
        }

        return Task.CompletedTask;
    }
}
