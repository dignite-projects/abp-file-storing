using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.FileStoring;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.Collections;
using Volo.Abp.Content;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Services;

namespace Dignite.FileExplorer.Files;

public class FileDescriptorManager : DomainService
{
    private readonly IFileDescriptorRepository _fileDescriptorRepository;
    private readonly IBlobContainerFactory _blobContainerFactory;
    private readonly IBlobContainerConfigurationProvider _blobContainerConfigurationProvider;
    private readonly ContainerNameValidator _containerNameValidator;

    public FileDescriptorManager(
        IFileDescriptorRepository fileDescriptorRepository,
        IBlobContainerFactory blobContainerFactory,
        IBlobContainerConfigurationProvider blobContainerConfigurationProvider,
        ContainerNameValidator containerNameValidator)
    {
        _fileDescriptorRepository = fileDescriptorRepository;
        _blobContainerFactory = blobContainerFactory;
        _blobContainerConfigurationProvider = blobContainerConfigurationProvider;
        _containerNameValidator = containerNameValidator;
    }

    public virtual async Task<FileDescriptor> CreateAsync<TContainer>(
        [NotNull] IRemoteStreamContent stream,
        [CanBeNull] string cellName,
        [CanBeNull] Guid? directoryId,
        [CanBeNull] IEntity entity)
        where TContainer : class
    {
        var containerName = BlobContainerNameAttribute.GetContainerName<TContainer>();
        return await CreateAsync(containerName, stream, cellName, directoryId, entity);
    }

    public virtual async Task<FileDescriptor> CreateAsync(
        [NotNull] string containerName,
        [NotNull] IRemoteStreamContent stream,
        [CanBeNull] string cellName,
        [CanBeNull] Guid? directoryId,
        [CanBeNull] IEntity entity)
    {
        return await CreateAsync(
            containerName,
            stream,
            cellName,
            directoryId,
            entity == null ? null : GetEntityKey(entity)
        );
    }

    public virtual async Task<FileDescriptor> CreateAsync(
        [NotNull] string containerName,
        [NotNull] IRemoteStreamContent stream,
        [CanBeNull] string cellName,
        [CanBeNull] Guid? directoryId,
        [CanBeNull] string entityId)
    {
        var configuration = _blobContainerConfigurationProvider.Get(containerName);
        var fileGrid = configuration.GetFileGridConfiguration();
        var fileCells = fileGrid.FileCells;

        if ((fileCells == null || !fileCells.Any()) && !cellName.IsNullOrEmpty())
        {
            throw new FileCellNameNotApplicableException();
        }

        if (fileCells != null && fileCells.Any())
        {
            if (cellName.IsNullOrEmpty())
            {
                throw new ArgumentNullException(nameof(cellName));
            }

            if (!fileCells.Any(c => c.Name.Equals(cellName, StringComparison.InvariantCultureIgnoreCase)))
            {
                throw new FileCellNameNotFoundException();
            }
        }

        var blobName = await GenerateBlobNameAsync(containerName);
        var fileDescriptor = new FileDescriptor(
            GuidGenerator.Create(),
            containerName,
            blobName,
            stream.FileName,
            stream.ContentType,
            cellName,
            directoryId,
            entityId,
            CurrentTenant.Id);

        return await CreateAsync(fileDescriptor, stream);
    }

    public virtual async Task<FileDescriptor> CreateAsync(
        [NotNull] FileDescriptor file,
        [NotNull] IRemoteStreamContent remoteStream)
    {
        using (var ms = new MemoryStream())
        {
            await remoteStream.GetStream().CopyToAsync(ms);
            ms.Position = 0;
            return await CreateAsync(file, ms, true);
        }
    }

    public virtual async Task<FileDescriptor> CreateAsync(
        [NotNull] FileDescriptor file,
        [NotNull] Stream stream,
        bool overrideExisting = false,
        CancellationToken cancellationToken = default)
    {
        _containerNameValidator.Validate(file.ContainerName);
        await CheckFileAsync(file);

        await OnCreatingEntityAsync(file);

        if (overrideExisting)
        {
            var existingFile = await _fileDescriptorRepository.FindByBlobNameAsync(file.ContainerName, file.BlobName, cancellationToken);
            if (existingFile != null)
            {
                await DeleteAsync(existingFile, cancellationToken);
            }
        }
        else if (await _fileDescriptorRepository.BlobNameExistsAsync(file.ContainerName, file.BlobName, cancellationToken))
        {
            throw new BlobAlreadyExistsException(
                $"Saving BLOB '{file.BlobName}' does already exists in the container '{file.ContainerName}'! Set {nameof(overrideExisting)} if it should be overwritten.");
        }

        stream = await FileHandlers(file, stream);

        file = await SaveFileInformationAsync(file, stream, cancellationToken);

        if (file.ReferBlobName.IsNullOrEmpty())
        {
            var blobContainer = _blobContainerFactory.Create(file.ContainerName);
            await blobContainer.SaveAsync(file.BlobName, stream, overrideExisting, cancellationToken);
        }

        await OnCreatedEntityAsync(file);

        return file;
    }

    public virtual async Task<FileDescriptor> GetOrNullAsync(
        [NotNull] string containerName,
        [NotNull] string blobName,
        CancellationToken cancellationToken = default)
    {
        return await _fileDescriptorRepository.FindByBlobNameAsync(containerName, blobName, cancellationToken);
    }

    public virtual async Task<FileDescriptor> GetOrNullAsync<TContainer>(
        [NotNull] string blobName,
        CancellationToken cancellationToken = default)
        where TContainer : class
    {
        var containerName = BlobContainerNameAttribute.GetContainerName<TContainer>();
        return await GetOrNullAsync(containerName, blobName, cancellationToken);
    }

    public virtual async Task<Stream> GetStreamOrNullAsync(
        [NotNull] string containerName,
        [NotNull] string blobName,
        CancellationToken cancellationToken = default)
    {
        var blobContainer = _blobContainerFactory.Create(containerName);

        return await blobContainer.GetOrNullAsync(blobName, cancellationToken);
    }

    public virtual async Task<Stream> GetStreamOrNullAsync<TContainer>(
        [NotNull] string blobName,
        CancellationToken cancellationToken = default)
        where TContainer : class
    {
        var blobContainer = _blobContainerFactory.Create<TContainer>();

        return await blobContainer.GetOrNullAsync(blobName, cancellationToken);
    }

    public virtual async Task<bool> DeleteAsync(
        [NotNull] FileDescriptor file,
        CancellationToken cancellationToken = default)
    {
        await OnDeletingEntityAsync(file);

        await _fileDescriptorRepository.DeleteAsync(file, false, cancellationToken);

        if (file.ReferBlobName.IsNullOrEmpty())
        {
            if (!await _fileDescriptorRepository.ReferencingAnyAsync(file.ContainerName, file.BlobName, cancellationToken))
            {
                var blobContainer = _blobContainerFactory.Create(file.ContainerName);
                return await blobContainer.DeleteAsync(file.BlobName, cancellationToken);
            }
        }
        else
        {
            if (!await _fileDescriptorRepository.ReferencingAnyAsync(file.ContainerName, file.ReferBlobName, cancellationToken) &&
                !await _fileDescriptorRepository.BlobNameExistsAsync(file.ContainerName, file.ReferBlobName, cancellationToken))
            {
                var blobContainer = _blobContainerFactory.Create(file.ContainerName);
                return await blobContainer.DeleteAsync(file.ReferBlobName, cancellationToken);
            }
        }

        await OnDeletedEntityAsync(file);

        return true;
    }

    protected virtual Task OnCreatingEntityAsync([NotNull] FileDescriptor file)
    {
        return Task.CompletedTask;
    }

    protected virtual Task OnCreatedEntityAsync([NotNull] FileDescriptor file)
    {
        return Task.CompletedTask;
    }

    protected virtual Task OnDeletingEntityAsync([NotNull] FileDescriptor file)
    {
        return Task.CompletedTask;
    }

    protected virtual Task OnDeletedEntityAsync([NotNull] FileDescriptor file)
    {
        return Task.CompletedTask;
    }

    protected virtual Task CheckFileAsync([NotNull] FileDescriptor file)
    {
        Check.NotNullOrWhiteSpace(file.ContainerName, nameof(FileDescriptor.ContainerName), FileConsts.MaxContainerNameLength);
        Check.NotNullOrWhiteSpace(file.BlobName, nameof(FileDescriptor.BlobName), FileConsts.MaxBlobNameLength);
        Check.Length(file.Name, nameof(FileDescriptor.Name), FileConsts.MaxNameLength);
        Check.Length(file.MimeType, nameof(FileDescriptor.MimeType), FileConsts.MaxMimeTypeLength);

        return Task.CompletedTask;
    }

    protected virtual string GetEntityKey(IEntity entity)
    {
        var keys = entity.GetKeys();
        if (keys.All(k => k == null))
        {
            return null;
        }

        return string.Join(",", keys);
    }

    private async Task<Stream> FileHandlers(FileDescriptor file, Stream stream)
    {
        var configuration = _blobContainerConfigurationProvider.Get(file.ContainerName);
        var processHandlers = configuration.GetConfigurationOrDefault<ITypeList<IFileHandler>>(BlobContainerConfigurationNames.FileHandlers, null);
        if (processHandlers != null && processHandlers.Any())
        {
            var serviceProvider = LazyServiceProvider.LazyGetRequiredService<IServiceProvider>();
            var context = new FileHandlerContext(file.Name, file.MimeType, stream, configuration);
            using (var scope = serviceProvider.CreateScope())
            {
                foreach (var handlerType in processHandlers)
                {
                    var handler = scope.ServiceProvider
                        .GetRequiredService(handlerType)
                        .As<IFileHandler>();

                    await handler.ExecuteAsync(context);
                }

                return context.BlobStream;
            }
        }

        return stream;
    }

    private async Task<FileDescriptor> SaveFileInformationAsync(
        [NotNull] FileDescriptor file,
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        var md5 = stream.Md5();
        var md5ExistingFile = await _fileDescriptorRepository.FindByMd5Async(file.ContainerName, md5, cancellationToken);
        if (md5ExistingFile == null || md5ExistingFile.BlobName == file.BlobName)
        {
            file.SetSize(stream.Length);
            file.SetMd5(md5);
        }
        else
        {
            file.SetSize(md5ExistingFile.Size);
            file.SetReferBlobName(md5ExistingFile.BlobName);
        }

        await _fileDescriptorRepository.InsertAsync(file, false, cancellationToken);

        return file;
    }

    private async Task<string> GenerateBlobNameAsync(string containerName)
    {
        var configuration = _blobContainerConfigurationProvider.Get(containerName);
        var namingGeneratorType = configuration.GetConfigurationOrDefault(
            FileExplorerBlobContainerConfigurationNames.BlobNamingGenerator,
            typeof(RandomBlobNameGenerator)
        );

        var generator = LazyServiceProvider.LazyGetRequiredService(namingGeneratorType)
            .As<IBlobNameGenerator>();

        return await generator.Create();
    }
}
