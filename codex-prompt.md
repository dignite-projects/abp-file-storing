# Codex 实施提示词 — abp-file-storing 重构剥离

## 任务
把已**冻结**的 `dignite-abp` 里的文件相关模块，剥离重构进新仓库 `abp-file-storing`，拆成三层：`Dignite.Abp.FileStoring`（纯基础设施）、`Dignite.Abp.FileStoring.Imaging`（可选装图像处理）、`Dignite.FileExplorer`（DDD 功能 + Angular UI）。核心动作：**删掉 `Dignite.Abp.Files` 这层泛型抽象**、命名空间统一为 `Dignite.Abp.FileStoring`、UI 从 Blazor 换成 Angular。

## 仓库与路径（Windows）
- **源仓库（只读、冻结、严禁写入）**：`D:\dignite-projects\dignite-abp`
  - 后端：`modules\files`（`Dignite.Abp.Files.*`）、`modules\file-explorer`（`Dignite.FileExplorer.*`）
  - Angular 库：`npm\ng-packs\packages\file-explorer`（npm 包 `@dignite-ng/expand.file-explorer`）
- **目标仓库（在此工作）**：`D:\dignite-projects\abp-file-storing`（当前近乎空）
- **参照骨架**：`D:\dignite-projects\abp-notifications`（同类已剥离仓库；照它的 `core/` + 功能模块目录 + `angular/` 工作区 + `Directory.*.props` + `.slnx` 的组织方式）
- **权威计划**：先运行 `gh issue view 1 --repo dignite-projects/abp-file-storing`，**issue #1 是完整分阶段清单，以它为准**；本文件是提要。

## 铁律（最高优先级，违反即失败）
1. **`dignite-abp` 冻结**：只读、复制其代码，**绝不**修改/删除/移动它的任何文件。所有产出只发生在 `abp-file-storing`。完成后 `dignite-abp` 的 `git status` 必须干净。
2. **删除 `Dignite.Abp.Files` 泛型抽象**：把 `FileManager<TFile,TFileStore>` / `IFile` / `FileBase` / `IFileStore<TFile>` 的逻辑**内联并入** `Dignite.FileExplorer` 的具体类型（`FileDescriptor` / `FileDescriptorManager` / `IFileDescriptorRepository`），而不是保留这层。
3. **命名空间统一 `Dignite.Abp.FileStoring`**（含所有扩展方法/配置类），**不**沿用 `Dignite.Abp.BlobStoring`；消费者需 `using Dignite.Abp.FileStoring;`。
4. **硬约束：`FileHandlerContext` 必须与 `IFile` 解耦** —— 现状它持有 `IFile File`，改为只携带 `string FileName` + `string MimeType` + `Stream BlobStream` + `BlobContainerConfiguration ContainerConfiguration`。否则 `IFile` 迁入 FileExplorer 后，FileStoring 反向依赖 FileExplorer → 循环依赖、编译不过。
5. **UI 用 Angular，不要 Blazor**：复制 Angular 库；**不复制** `Dignite.FileExplorer.Blazor` / `.Blazor.Server` / `.Blazor.WebAssembly` 及 `FileExplorerBlazorAutoMapperProfile` 等 Blazor 专属件。
6. **以真实源码为准**：本文件/issue 里的类名、文件名、成员若与实际代码不符，一律读 `dignite-abp` 实际源码确认，**不要臆造**。
7. **删抽象 ≠ 删功能**：内联泛型基类时务必保留 —— MD5 去重（`ReferBlobName`）、删除时引用计数、`IFileHandler` 上传管线派发、blob 保存/删除编排、`SaveFileInformationAsync`/`CheckFileAsync`。

## 目标架构
| 层 | 包 | 职责 |
|---|---|---|
| 纯基础设施 | `Dignite.Abp.FileStoring` | 上传处理管线（`IFileHandler`/`FileHandlerContext`）、大小/类型校验 handler、容器配置扩展、`ContainerNameValidator`、命名生成器（`IBlobNameGenerator`/`RandomBlobNameGenerator`）、`ImageFormatHelper`、`StreamExtensions`。**零 DDD、无 EF/Mongo**，仿 `Volo.Abp.BlobStoring`。单项目。 |
| 可选装 | `Dignite.Abp.FileStoring.Imaging` | 上传即缩放/压缩（`ImageResizeHandler` + 配置 + `AddImageResizeHandler` 扩展），依赖 `Dignite.Abp.FileStoring` + `Volo.Abp.Imaging`。 |
| DDD 功能 | `Dignite.FileExplorer.*` | `FileDescriptor` 聚合、目录、授权、`AppService`/`HttpApi`/`HttpApi.Client`/`Domain`/EF/Mongo、按需缩放；UI 为 Angular。 |

目标结构（照 `abp-notifications`）：
```
abp-file-storing/
├─ core/
│  ├─ Dignite.Abp.FileStoring/
│  └─ Dignite.Abp.FileStoring.Imaging/
├─ file-explorer/          # Dignite.FileExplorer.* 后端（.NET，无 Blazor）
├─ angular/                # Angular 工作区；projects/file-explorer 来自 ng-packs
├─ host/  test/  docs/
├─ Directory.Build.props / Directory.Packages.props
└─ Dignite.Abp.FileStoring.slnx
```

## 执行顺序（每个 Phase 的勾选项以 issue #1 为准）
- **Phase 0 骨架**：建目录、`Directory.*.props`、`.slnx`、`angular/` 工作区（照 abp-notifications）。
- **Phase 1 `Dignite.Abp.FileStoring`**：从 `Dignite.Abp.Files.Domain` 中 `Dignite.Abp.BlobStoring` 命名空间那批（`IFileHandler`/`FileHandlerContext`/`FileSizeLimitHandler`/`FileTypeCheckHandler` + 各自 Configuration/Names、`BlobContainerConfigurationExtensions`/`BlobContainerConfigurationNames`、`ContainerNameValidator`、`StreamExtensions`）+ 从 `Dignite.FileExplorer.*` 迁 `IBlobNameGenerator`/`RandomBlobNameGenerator`/`ImageFormatHelper`。**执行铁律 #4 解耦**。namespace 全改 `Dignite.Abp.FileStoring`。加模块类 `DigniteAbpFileStoringModule : AbpModule`（`DependsOn(AbpBlobStoringModule)`）。单项目，无 Domain/EF/Mongo 拆分。
- **Phase 2 `.Imaging`**：迁 `ImageResizeHandler` + `ImageResizeHandlerConfiguration`/Names + `AddImageResizeHandler`/`GetImageResizeConfiguration`；`ImageResizeHandler` 改用 `context.MimeType`（不再 `context.File.MimeType`）；依赖 FileStoring + `Volo.Abp.Imaging`；模块 `DigniteAbpFileStoringImagingModule`。
- **Phase 3 并入 FileExplorer（.NET 后端）**：内联泛型（铁律 #2/#7）；`FileDescriptor` 由 `: FileBase` 改为直接 `: AggregateRoot<Guid>` 并内联 FileBase 成员；`FileDescriptorManager` 去泛型；**删 `FileDescriptorStore`**（它只是 100% 转发给 `IFileDescriptorRepository` 的适配器，为满足 `IFileStore<TFile>` 而存在）；`AbpFilesDbContextModelCreatingExtensions` 的 EF 建模并入 FileExplorer 的 DbContext，Mongo 同理；`[DependsOn]` 移除 `AbpFiles*Module`、加 `DigniteAbpFileStoringModule`（Application 需上传缩放时加 `.Imaging`）；**不复制 Blazor**。
- **Phase 4 Angular UI**：把 `@dignite-ng/expand.file-explorer` 复制到 `angular/projects/file-explorer`，脱离原 ng-packs monorepo，核对包名/peer deps/路径别名/`project.json`（Nx）；确认 API 代理指向 `Dignite.FileExplorer.HttpApi`。
- **Phase 5 引用与 using 修复**：所有 csproj 的 `Dignite.Abp.Files.*` 引用改到 `Dignite.Abp.FileStoring`；给用到扩展的文件补 `using Dignite.Abp.FileStoring;`；按需缩放（`FileDescriptorAppService.GetStreamAsync` + `ImageResizeInput`）留 FileExplorer，用 `Volo.Abp.Imaging.IImageResizer` + 基座 `ImageFormatHelper`，**不引 `.Imaging`**。
- **Phase 6 验证**：见验收标准。

## 工作方式
- `git init`（若无），**每个 Phase 一个 commit**，message 写 `Phase N: ...`。
- 每个 .NET Phase 结束跑 `dotnet build`（至少 core + file-explorer），过了再往下。
- **重点核对无 `Dignite.Abp.FileStoring → Dignite.FileExplorer` 反向依赖**（这是铁律 #4 没做好的信号）。
- 与本文件不符处：停下读源码，按实际来。
- 不确定的取舍（如 `Dignite.Abp.DynamicForms.FileExplorer` 要不要随 Blazor 去掉）：**记录到最终报告，不要自作主张删关键件**。

## 验收标准
- [ ] `dotnet build` 整个 `.slnx` 全绿。
- [ ] 全仓库无残留 `Dignite.Abp.Files` 命名空间、无旧自定义 `Dignite.Abp.BlobStoring` 扩展命名空间（都应为 `Dignite.Abp.FileStoring`）。
- [ ] `Dignite.Abp.FileStoring` 无任何 DDD/EF/Mongo 引用；无 FileStoring→FileExplorer 反向依赖。
- [ ] `angular/projects/file-explorer` 能 `nx build`（或 `ng build`）通过。
- [ ] `D:\dignite-projects\dignite-abp` 的 `git status` 干净（证明源仓库零改动）。
- [ ] 最小闭环：公开容器匿名读、上传自动封顶缩放、取图 `?width=&height=` 按需缩放。

## 交付
分阶段 commit 的 `abp-file-storing` 仓库 + 一份 Markdown 报告：做了什么、每层最终文件清单、未决取舍（含 DynamicForms/Blazor 处理）、遗留 TODO。
