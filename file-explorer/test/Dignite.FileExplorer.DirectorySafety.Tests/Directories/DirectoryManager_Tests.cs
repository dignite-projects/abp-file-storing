using System;
using System.Threading.Tasks;
using Dignite.Abp.FileStoring;
using Dignite.FileExplorer.Directories;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace Dignite.FileExplorer.DirectorySafety.Tests.Directories;

public class DirectoryManager_Tests
{
    [Fact]
    public async Task MoveAsync_ShouldRejectMovingDirectoryIntoItself()
    {
        var directory = CreateDirectory(Guid.NewGuid(), null);
        var repository = Substitute.For<IDirectoryDescriptorRepository>();
        repository.GetAsync(directory.Id).Returns(directory);

        var manager = new DirectoryManager(repository, new ContainerNameValidator());

        var exception = await Should.ThrowAsync<BusinessException>(() =>
            manager.MoveAsync(directory, directory.Id, 0));

        exception.Code.ShouldBe(FileExplorerErrorCodes.Directories.ForbidMovingToChild);
    }

    [Fact]
    public async Task MoveAsync_ShouldRejectMovingDirectoryIntoDescendant()
    {
        var root = CreateDirectory(Guid.NewGuid(), null);
        var child = CreateDirectory(Guid.NewGuid(), root.Id);
        var repository = Substitute.For<IDirectoryDescriptorRepository>();
        repository.GetAsync(child.Id).Returns(child);
        repository.GetAsync(root.Id).Returns(root);

        var manager = new DirectoryManager(repository, new ContainerNameValidator());

        var exception = await Should.ThrowAsync<BusinessException>(() =>
            manager.MoveAsync(root, child.Id, 0));

        exception.Code.ShouldBe(FileExplorerErrorCodes.Directories.ForbidMovingToChild);
    }

    private static DirectoryDescriptor CreateDirectory(Guid id, Guid? parentId)
    {
        return new DirectoryDescriptor(id, "Default", id.ToString(), parentId, 0, null)
        {
            CreatorId = Guid.Parse("11111111-1111-1111-1111-111111111111")
        };
    }
}
