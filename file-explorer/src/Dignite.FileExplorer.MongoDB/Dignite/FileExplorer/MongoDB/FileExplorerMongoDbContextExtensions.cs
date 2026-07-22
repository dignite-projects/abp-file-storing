using Dignite.FileExplorer.Directories;
using Dignite.FileExplorer.Files;
using MongoDB.Bson;
using MongoDB.Driver;
using Volo.Abp;
using Volo.Abp.MongoDB;

namespace Dignite.FileExplorer.MongoDB;

public static class FileExplorerMongoDbContextExtensions
{
    public static void ConfigureFileExplorer(
        this IMongoModelBuilder builder)
    {
        Check.NotNull(builder, nameof(builder));

        builder.Entity<DirectoryDescriptor>(x =>
        {
            x.CollectionName = FileExplorerDbProperties.DbTablePrefix + "DirectoryDescriptors";
        });

        builder.Entity<FileDescriptor>(x =>
        {
            x.CollectionName = FileExplorerDbProperties.DbTablePrefix + "FileDescriptors";
            x.ConfigureIndexes(indexes =>
            {
                var keys = Builders<BsonDocument>.IndexKeys;

                indexes.CreateOne(new CreateIndexModel<BsonDocument>(
                    keys.Ascending(nameof(FileDescriptor.TenantId))
                        .Ascending(nameof(FileDescriptor.ContainerName))
                        .Ascending(nameof(FileDescriptor.Md5))));
                indexes.CreateOne(new CreateIndexModel<BsonDocument>(
                    keys.Ascending(nameof(FileDescriptor.TenantId))
                        .Ascending(nameof(FileDescriptor.ContainerName))
                        .Ascending(nameof(FileDescriptor.ReferBlobName))));
                indexes.CreateOne(new CreateIndexModel<BsonDocument>(
                    keys.Ascending(nameof(FileDescriptor.TenantId))
                        .Ascending(nameof(FileDescriptor.ContainerName))
                        .Ascending(nameof(FileDescriptor.EntityId))));
            });
        });
    }
}
