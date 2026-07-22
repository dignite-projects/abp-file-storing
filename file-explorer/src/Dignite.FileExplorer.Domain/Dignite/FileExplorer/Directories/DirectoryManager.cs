using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.FileStoring;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace Dignite.FileExplorer.Directories;

public class DirectoryManager : DomainService
{
    protected ContainerNameValidator ContainerNameValidator { get; }
    protected IDirectoryDescriptorRepository DirectoryDescriptorRepository { get; }

    public DirectoryManager(IDirectoryDescriptorRepository directoryDescriptorRepository, ContainerNameValidator containerNameValidator)
    {
        DirectoryDescriptorRepository = directoryDescriptorRepository;
        ContainerNameValidator = containerNameValidator;
    }

    public virtual async Task<DirectoryDescriptor> CreateAsync(Guid userId, string containerName, string name, Guid? parentId = null)
    {
        ContainerNameValidator.Validate(containerName);

        if (parentId.HasValue)
        {
            var parent = await DirectoryDescriptorRepository.FindAsync(parentId.Value, false);
            if (parent == null ||
                !parent.ContainerName.Equals(containerName, StringComparison.OrdinalIgnoreCase) ||
                parent.TenantId != CurrentTenant.Id ||
                parent.CreatorId != userId)
            {
                throw new BusinessException(FileExplorerErrorCodes.Directories.DirectoryNotExist);
            }
        }

        //
        if (await DirectoryDescriptorRepository.NameExistsAsync(userId, containerName, name, parentId))
        {
            throw new DirectoryAlreadyExistException(name);
        }

        //
        var order = await DirectoryDescriptorRepository.GetMaxOrderAsync(userId, containerName, parentId);
        var directory = new DirectoryDescriptor(
            GuidGenerator.Create(),
            containerName, name, parentId,
            order + 1,
            CurrentTenant.Id
            );
        return await DirectoryDescriptorRepository.InsertAsync(directory);
    }

    public virtual async Task<DirectoryDescriptor> MoveAsync(DirectoryDescriptor directory, Guid? parentId,int order)
    {
        if (parentId.HasValue)
        {
            var parent = await DirectoryDescriptorRepository.GetAsync(parentId.Value);
            if (!parent.ContainerName.Equals(directory.ContainerName, StringComparison.CurrentCultureIgnoreCase))
            {
                throw new DirectoryInvalidMoveException();
            }

            if (await IsDescendantOrSelfAsync(directory.Id, parent))
            {
                throw new BusinessException(FileExplorerErrorCodes.Directories.ForbidMovingToChild);
            }
        }

        var children = await DirectoryDescriptorRepository.GetListAsync(directory.CreatorId.Value, directory.ContainerName, parentId);
        foreach (var item in children.Where(d=>d.Order>=order && d.Id!=directory.Id))
        {
            item.Order=item.Order+1;
            await DirectoryDescriptorRepository.UpdateAsync(item);
        }

        directory.ParentId = parentId;
        directory.Order = order;
        return await DirectoryDescriptorRepository.UpdateAsync(directory);
    }

    private async Task<bool> IsDescendantOrSelfAsync(Guid directoryId, DirectoryDescriptor parent)
    {
        var visited = new HashSet<Guid>();
        var current = parent;

        while (true)
        {
            if (current.Id == directoryId || !visited.Add(current.Id))
            {
                return true;
            }

            if (!current.ParentId.HasValue)
            {
                return false;
            }

            current = await DirectoryDescriptorRepository.GetAsync(current.ParentId.Value);
        }
    }

    public virtual async Task<DirectoryDescriptor> UpdateAsync(DirectoryDescriptor directory, string name)
    {
        //
        if (!directory.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase))
        {
            if (await DirectoryDescriptorRepository.NameExistsAsync(directory.CreatorId.Value, directory.ContainerName, name, directory.ParentId))
            {
                throw new DirectoryAlreadyExistException(name);
            }
        }

        directory.Name = name;
        return await DirectoryDescriptorRepository.UpdateAsync(directory);
    }
}
