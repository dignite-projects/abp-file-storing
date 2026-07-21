# Migration Report

## Summary

The frozen `dignite-abp` file modules were copied into this repository and refactored into:

- `core/src/Dignite.Abp.FileStoring`: upload pipeline, file handlers, container configuration helpers, blob name generation, MIME helper, and stream MD5 helper.
- `core/src/Dignite.Abp.FileStoring.Imaging`: optional upload-time image resize/compress handler.
- `file-explorer/src/Dignite.FileExplorer.*`: FileExplorer DDD backend, application services, HTTP API, EF Core, MongoDB, and HTTP client.
- `angular/projects/file-explorer`: Angular package `@dignite-ng/expand.file-explorer`.

The old Files abstraction layer was not retained. `FileDescriptor` now owns the former file base fields directly, and `FileDescriptorManager` contains the former generic manager flow for handler dispatch, MD5 de-duplication via `ReferBlobName`, blob save/delete orchestration, and reference-count aware deletion.

## Core Layer

- `Dignite.Abp.FileStoring`
  - `IFileHandler`
  - `FileHandlerContext`
  - `FileSizeLimitHandler`
  - `FileTypeCheckHandler`
  - `BlobContainerConfigurationExtensions`
  - `ContainerNameValidator`
  - `IBlobNameGenerator`
  - `RandomBlobNameGenerator`
  - `ImageFormatHelper`
  - `StreamExtensions`
- `Dignite.Abp.FileStoring.Imaging`
  - `ImageResizeHandler`
  - `ImageResizeHandlerConfiguration`
  - `AddImageResizeHandler` / `GetImageResizeConfiguration`

`FileHandlerContext` carries only file name, MIME type, blob stream, and container configuration. There is no FileStoring to FileExplorer reference.

## FileExplorer Layer

Copied backend projects:

- `Dignite.FileExplorer.Domain.Shared`
- `Dignite.FileExplorer.Domain`
- `Dignite.FileExplorer.Application.Contracts`
- `Dignite.FileExplorer.Application`
- `Dignite.FileExplorer.HttpApi`
- `Dignite.FileExplorer.HttpApi.Client`
- `Dignite.FileExplorer.EntityFrameworkCore`
- `Dignite.FileExplorer.MongoDB`

Not copied:

- `Dignite.FileExplorer.Blazor`
- `Dignite.FileExplorer.Blazor.Server`
- `Dignite.FileExplorer.Blazor.WebAssembly`
- Blazor-only DynamicForms components

`Dignite.Abp.DynamicForms.FileExplorer` remains a follow-up decision because Angular-side DynamicForms integration needs a separate contract review.

## Validation Notes

- `.NET` solution builds.
- `Dignite.Abp.FileStoring.Tests` passes.
- Angular package builds with `npm run build:lib`.
- Source repository `D:\dignite-projects\dignite-abp` remained clean.

Known residual work:

- No runnable `host/` app was created in this pass, so the public-container/upload-resize/on-demand-resize runtime loop is not automated yet.
- FileExplorer integration tests were not fully ported; the obsolete copied tests were removed to avoid reintroducing the deleted generic abstraction.
- MongoDB repositories compile on ABP 10.5 / MongoDB Driver 3.x after removing explicit `IMongoQueryable<>` casts, but still use ABP's obsolete `GetMongoQueryableAsync` API.
