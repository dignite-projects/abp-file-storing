using System;
using Volo.Abp.Auditing;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace Dignite.FileExplorer.Files;

public class FileDescriptor : AggregateRoot<Guid>, ICreationAuditedObject, IDeletionAuditedObject, IMultiTenant
{
    protected FileDescriptor()
    { }

    public FileDescriptor(Guid id, string containerName, string blobName, string name, string mimeType, string cellName, Guid? directoryId, string entityId, Guid? tenantId)
        : base(id)
    {
        ContainerName = containerName;
        BlobName = blobName;
        Name = name;
        MimeType = mimeType;
        CellName = cellName;
        DirectoryId = directoryId;
        EntityId = entityId;
        TenantId = tenantId;
    }

    public string ContainerName { get; protected set; } = default!;

    public string BlobName { get; protected set; } = default!;

    public long Size { get; protected set; }

    public string Name { get; set; } = default!;

    public string MimeType { get; protected set; } = default!;

    public string Md5 { get; protected set; } = default!;

    public string ReferBlobName { get; protected set; } = default!;

    public string CellName { get; set; }

    /// <summary>
    /// Directory in container
    /// </summary>
    public Guid? DirectoryId { get; set; }

    /// <summary>
    /// Associated Entity Id
    /// </summary>
    public string EntityId { get; protected set; }

    public DateTime CreationTime { get; set; }

    public Guid? CreatorId { get; set; }

    public Guid? DeleterId { get; set; }
    public DateTime? DeletionTime { get; set; }
    public bool IsDeleted { get; set; }

    public Guid? TenantId { get; protected set; }

    public void SetMd5(string md5)
    {
        Md5 = md5;
    }

    public void SetReferBlobName(string blobName)
    {
        ReferBlobName = blobName;
    }

    public void SetSize(long size)
    {
        Size = size;
    }
}
