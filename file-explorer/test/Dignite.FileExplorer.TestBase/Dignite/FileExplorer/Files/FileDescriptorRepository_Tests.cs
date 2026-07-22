using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Modularity;
using Xunit;

namespace Dignite.FileExplorer.Files;

/* Write your custom repository tests like that, in this project, as abstract classes.
 * Then inherit these abstract classes from EF Core & MongoDB test projects.
 * In this way, both database providers are tests with the same set tests.
 */

public abstract class FileDescriptorRepository_Tests<TStartupModule> : FileExplorerTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly FileExplorerTestData testData;
    private readonly IFileDescriptorRepository _fileDescriptorRepository;
    private readonly ICurrentTenant _currentTenant;

    protected FileDescriptorRepository_Tests()
    {
        _fileDescriptorRepository = GetRequiredService<IFileDescriptorRepository>();
        testData = GetRequiredService<FileExplorerTestData>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task BlobNameExistsAsync_ShouldReturnTrue_WithExistingBlobName()
    {
        var result = await _fileDescriptorRepository.BlobNameExistsAsync(testData.ContainerName1, testData.BlobName1);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task BlobNameExistsAsync_ShouldBeTenantScoped()
    {
        var tenantId = Guid.NewGuid();
        var blobName = "tenant-scope-" + Guid.NewGuid().ToString("N");
        using (_currentTenant.Change(tenantId))
        {
            var file = new FileDescriptor(
                Guid.NewGuid(),
                testData.ContainerName1,
                blobName,
                "tenant-scope.txt",
                "text/plain",
                string.Empty,
                null,
                string.Empty,
                tenantId);
            file.SetMd5(new string('a', 64));
            file.SetReferBlobName(string.Empty);
            await _fileDescriptorRepository.InsertAsync(file, autoSave: true);

            (await _fileDescriptorRepository.BlobNameExistsAsync(testData.ContainerName1, blobName))
                .ShouldBeTrue();
        }

        using (_currentTenant.Change(Guid.NewGuid()))
        {
            (await _fileDescriptorRepository.BlobNameExistsAsync(testData.ContainerName1, blobName))
                .ShouldBeFalse();
        }
    }

    [Fact]
    public async Task BlobNameExistsAsync_ShouldSupportConcurrentReads()
    {
        var results = await Task.WhenAll(
            Enumerable.Range(0, 8)
                .Select(_ => _fileDescriptorRepository.BlobNameExistsAsync(
                    testData.ContainerName1,
                    testData.BlobName1)));

        results.All(result => result).ShouldBeTrue();
    }
}
