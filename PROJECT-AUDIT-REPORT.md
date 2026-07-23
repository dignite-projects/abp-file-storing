# abp-file-storing 项目完整审查报告

审查日期：2026-07-22  
审查范围：当前工作区中的根目录配置、解决方案结构、.NET 代码、Angular 代码、EF Core/MongoDB 实现、Host、CI/CD、打包及测试。

> 本报告基于当前包含未提交迁移内容的工作区。审查过程没有修改原有源码；仅新增本报告文件。

## 一、结论摘要

当前迁移已经达到“各个包基本可编译、可打包”的阶段，但还没有达到“完整产品可以运行”的阶段。

结论：**暂不建议发布当前 `10.0.0-rc.1`。**

主要原因不是编译失败，而是以下几个产品级问题仍然存在：

- Host 没有真正接入 FileExplorer 模块、数据库模型和 API。
- 数据库迁移路径和容器启动流程存在错误。
- 目录可以构造自引用，可能导致进程 StackOverflow。
- 创建文件权限存在绕过，批量删除缺少逐资源授权。
- 文件 Blob 与数据库没有可靠的一致性策略。
- 上传和图片处理存在内存、内容伪造和资源消耗风险。
- .NET/Angular 客户端代理已经与服务端契约漂移。
- CI 没有执行当前已经失败的 Angular lint 和测试。

## 二、两条核心追问

### 1. 当前项目是否具备发布条件？

不具备。

目前可以完成 NuGet 和 Angular 包的构建与打包，但 Host 级别的产品链路没有闭合。启动 Host 后，FileExplorer 的模块、DbContext、Migration 和 Angular 路由并没有完整接入，因此“包能编译”不能等同于“产品能运行”。

### 2. 如果现在开始整改，应该先修什么？

建议按以下顺序处理：

1. 先完成 Host、数据库 Migration、Blob 容器和 Angular 路由的端到端接入。
2. 立即修复权限绕过、目录自引用和目录/文件完整性问题。
3. 再处理上传内存、图片解码、Blob/数据库一致性及并发问题。
4. 最后重新生成客户端代理、修复本地化、补齐测试和 CI 门禁。

## 三、阻断发布的问题（P0）

### P0-1：Host 没有真正接入 FileExplorer

- Host 项目没有引用 FileExplorer Application、HttpApi、EntityFrameworkCore 等项目：[Dignite.FileExplorer.Web.Host.csproj](host/Dignite.FileExplorer.Web.Host/Dignite.FileExplorer.Web.Host.csproj:12)
- Host 模块依赖列表没有 FileExplorer 模块：[HostModule.cs](host/Dignite.FileExplorer.Web.Host/HostModule.cs:64)
- HostDbContext 没有调用 `ConfigureFileExplorer()`：[HostDbContext.cs](host/Dignite.FileExplorer.Web.Host/Data/HostDbContext.cs:31)
- 仓库中没有实际 FileExplorer EF Core Migration。
- Angular 演示应用没有加载 FileExplorer 路由或模块：[app.routes.ts](angular/src/app/app.routes.ts:1)

影响：FileExplorer 各包虽然能够编译，但当前 Host 不会提供完整的 FileExplorer API，也不会创建对应数据库表；Angular 页面也没有真正验证该功能。

### P0-2：数据库自动迁移流程不可用

[HostDbMigrationService.cs](host/Dignite.FileExplorer.Web.Host/Data/HostDbMigrationService.cs:181) 将 EF 项目路径解析为解决方案根目录下的 `Dignite.FileExplorer.Web.Host`，而真实项目位于 `host/Dignite.FileExplorer.Web.Host`。

同时存在以下问题：

- 调用 ABP CLI 创建迁移后没有等待进程完成。
- 初始化脚本重复执行数据库迁移：[initialize-solution.ps1](host/etc/scripts/initialize-solution.ps1:33)
- Docker Compose 的健康检查地址使用 `http://host:8080`，实际服务名是 `host-api`：[docker-compose.yml](host/etc/docker/docker-compose.yml:28)
- API 与迁移器可能同时访问 SQLite 文件，存在初始化竞态和锁冲突。

### P0-3：目录自引用可导致进程崩溃

[DirectoryManager.cs](file-explorer/src/Dignite.FileExplorer.Domain/Dignite/FileExplorer/Directories/DirectoryManager.cs:41) 没有拒绝把目录移动到自身或自身子目录。

目录树构建代码会递归遍历子节点：[DirectoryListExtensions.cs](file-explorer/src/Dignite.FileExplorer.Application.Contracts/Dignite/FileExplorer/Directories/DirectoryListExtensions.cs:52)。一旦形成环，可能触发 `StackOverflowException`，终止整个服务进程。

## 四、高风险问题（P1）

### P1-1：创建文件权限被绕过

[FileDescriptorAppService.cs](file-explorer/src/Dignite.FileExplorer.Application/Dignite/FileExplorer/Files/FileDescriptorAppService.cs:40) 在鉴权前创建临时文件对象，并把 `CreatorId` 设置为当前用户。

[FileDescriptorAuthorizationHandler.cs](file-explorer/src/Dignite.FileExplorer.Application/Dignite/FileExplorer/Files/FileDescriptorAuthorizationHandler.cs:45) 又允许资源创建者执行操作。因此上传者天然成为临时资源的所有者，配置的 `CreateFilePermissionName` 实际上不会成为必要条件。

另外，`DeleteByEntityIdAsync` 只做管理权限判断，没有对每个文件执行资源授权检查。

### P1-2：重命名可能清空文件元数据

后端更新接口会直接覆盖 `DirectoryId`、`Name` 和 `CellName`：[FileDescriptorAppService.cs](file-explorer/src/Dignite.FileExplorer.Application/Dignite/FileExplorer/Files/FileDescriptorAppService.cs:53)

Angular 重命名只发送 `{ name }`：[file-modal.component.ts](angular/projects/file-explorer/src/lib/components/file-modal/file-modal.component.ts:479)

结果是单纯重命名可能同时清空目录关系和 FileCell 信息。更新接口也没有重新验证目录、容器和网格约束。

### P1-3：上传大小限制发生在完整缓冲之后

[FileDescriptorManager.cs](file-explorer/src/Dignite.FileExplorer.Domain/Dignite/FileExplorer/Files/FileDescriptorManager.cs:108) 会先将远程流完整复制到内存，再执行大小限制处理器。

这会使大文件请求在被拒绝前已经完成大规模内存分配。当前 `FileSizeLimitHandler` 还是 KB 实现、MB 文案：[FileSizeLimitHandler.cs](core/src/Dignite.Abp.FileStoring/Dignite/Abp/FileStoring/FileSizeLimitHandler.cs:12)。

### P1-4：图片处理容易遭受内容伪造和解压炸弹

[ImageResizeHandler.cs](core/src/Dignite.Abp.FileStoring.Imaging/Dignite/Abp/FileStoring/Imaging/ImageResizeHandler.cs:25) 信任客户端 MIME，并在尺寸限制前解码完整图片。

缺少最大像素数、最大宽高、压缩比和解码超时限制。在线缩放接口也缺少宽高上限和缓存，可能被重复调用消耗 CPU 和内存。

### P1-5：Blob 与数据库没有原子一致性

创建流程先写元数据再写 Blob，删除和覆盖流程也缺少补偿机制。数据库提交失败可能留下孤儿 Blob；覆盖新文件失败可能已经删除旧文件。

当前 MD5 去重查询也不是并发安全的。建议采用 SHA-256、数据库唯一约束、补偿删除或 Outbox/后台清理策略。

### P1-6：容器名称边界没有生效

[ContainerNameValidator.cs](core/src/Dignite.Abp.FileStoring/Dignite/Abp/FileStoring/ContainerNameValidator.cs:5) 的校验方法为空实现。

结合未配置读取权限时的默认行为，未注册容器可能被访问，只要调用者知道或猜到 Blob 名称即可。

### P1-7：客户端代理与服务端契约漂移

- .NET 文件流代理把 `ImageResizeInput` 参数登记成 `string`：[FileDescriptorClientProxy.Generated.cs](file-explorer/src/Dignite.FileExplorer.HttpApi.Client/ClientProxies/Dignite/FileExplorer/Files/FileDescriptorClientProxy.Generated.cs:71)
- Directory 客户端仍保留已不存在的 `GetMyAsync`。
- Angular 代理把可选的 `imageResize` 无条件解引用：[file-descriptor.service.ts](angular/projects/file-explorer/src/lib/proxy/dignite/file-explorer/files/file-descriptor.service.ts:82)
- Angular 下载请求没有配置 Blob 响应类型。

现有打包冒烟测试只验证编译，不能证明真实 HTTP 调用成功。

## 五、领域模型与数据层问题（P1/P2）

- 目录创建没有验证父目录存在、租户、所有者和容器一致性。
- 目录删除没有非空检查、级联策略或孤儿清理。
- EF BlobName 索引不是唯一索引：[FileExplorerDbContextModelCreatingExtensions.cs](file-explorer/src/Dignite.FileExplorer.EntityFrameworkCore/Dignite/FileExplorer/EntityFrameworkCore/FileExplorerDbContextModelCreatingExtensions.cs:51)
- EF 和 Mongo 默认排序方向不一致。
- MongoDB 没有创建业务索引。
- MD5、ReferBlobName、EntityId 等高频查询缺少索引。
- Dynamic LINQ 排序字段没有白名单。
- 多处忽略 `CancellationToken`。
- 部分领域对象属性拥有公开 setter，聚合不变量容易被绕过。

## 六、前端功能问题

- 上传失败回调中的 `this` 指向错误，错误处理本身会再次抛异常：[file-modal.component.ts](angular/projects/file-explorer/src/lib/components/file-modal/file-modal.component.ts:205)
- 文件选择器在任意输入变化时假设 `selectFormFile` 一定存在，可能触发异常。
- 文件预览使用异步 `forEach`，外层流程会提前完成；同时会将所有文件转成 Data URL，存在内存放大。
- 树组件使用模块级全局 `that`，多个组件实例会互相覆盖。
- `@swimlane/ngx-datatable` 被代码使用，但没有在库的 peerDependencies 中声明。
- 生产环境 Angular 配置仍写死 localhost。

## 七、本地化问题

错误码定义、JSON 资源键和异常命名空间不一致：

- 定义使用 `FileExplorer.Directory:0001`：[FileExplorerErrorCodes.cs](file-explorer/src/Dignite.FileExplorer.Domain.Shared/Dignite/FileExplorer/FileExplorerErrorCodes.cs:7)
- 资源使用 `FileExplorer:Directory:0001`：[en.json](file-explorer/src/Dignite.FileExplorer.Domain.Shared/Dignite/FileExplorer/Localization/Resources/en.json:4)
- 模块映射使用 `Dignite.FileExplorer`：[FileExplorerDomainSharedModule.cs](file-explorer/src/Dignite.FileExplorer.Domain.Shared/Dignite/FileExplorer/FileExplorerDomainSharedModule.cs:31)

因此业务异常很可能不能正确显示本地化消息。Angular 还存在多个代码引用但资源文件未定义的键。

## 八、配置、安全与部署问题

- 基础 `appsettings.json` 包含证书口令和字符串加密口令：[appsettings.json](host/Dignite.FileExplorer.Web.Host/appsettings.json:47)
- Host 关闭了 NuGet 审计：[common.props](common.props:6)
- Data Protection 密钥没有持久化，重启或多实例部署会使认证状态失效。
- 非生产环境统一开启详细 PII 日志，可能把敏感数据带入测试或预发布日志。
- OpenIddict 种子中启用了密码授权和客户端凭据授权，需要确认是否符合实际安全模型。
- 没有 `global.json`，也没有固定 Node 版本。
- 同时存在 npm 和 yarn 锁文件，CI 和本地启动工具链不一致。

## 九、验证结果

| 检查项 | 结果 |
|---|---|
| .NET Release Build | 成功，0 error，126 warning |
| .NET Tests | 15/15 通过 |
| Angular Library Build | 成功 |
| Angular Production Build | 成功，有 SCSS budget warning |
| NuGet 打包与消费端编译 | 成功 |
| Angular tgz 打包与消费端编译 | 成功 |
| `npm run lint` | 失败，缺少 `@nx/eslint-plugin` |
| `npm test -- --watch=false` | 失败，Jest/Vitest 配置混用 |
| npm production audit | 2 个 low，无 high/critical |

测试覆盖仍明显不足：授权测试使用 AlwaysAllow，缺少真实 Host/API、端到端上传下载、并发、事务失败、目录环、多租户和代理调用测试。

## 十、整改路线

### 第一阶段：恢复完整运行链路

1. 给 Host 添加 FileExplorer 项目引用和模块依赖。
2. 在 HostDbContext 中配置 FileExplorer 实体。
3. 创建并提交正式 EF Migration。
4. 修正 MigrationService 的路径、进程等待和容器启动顺序。
5. 接入 Blob 容器配置、权限配置、Angular 路由和 API 地址。
6. 增加真实上传、下载、缩放、删除端到端测试。

### 第二阶段：修复安全和数据完整性

1. 重写创建文件授权，不以临时实体所有权代替创建权限。
2. 批量删除逐文件执行资源鉴权。
3. 禁止目录自引用和子孙循环。
4. 校验父目录、容器、租户和所有者关系。
5. 明确非空目录删除和级联策略。
6. 把更新接口区分为完整更新和 Patch 更新，避免字段被隐式清空。

### 第三阶段：修复文件管线和并发一致性

1. 在 HTTP 层和流复制层双重限制请求体大小。
2. 统一大小单位并校验配置范围。
3. 使用真实内容检测文件类型和图片格式。
4. 增加图片像素、宽高、压缩比和处理超时限制。
5. 使用 SHA-256 和唯一约束解决并发去重。
6. 增加 Blob 失败补偿、孤儿清理和覆盖回滚策略。

### 第四阶段：客户端、测试和发布门禁

1. 重新生成 .NET 和 Angular 客户端代理。
2. 统一错误码、本地化资源和前端资源键。
3. 修复 Angular lint/test 配置并纳入 CI。
4. 固定 .NET SDK、Node、npm/yarn 工具链。
5. 开启 NuGet 审计，升级高危依赖。
6. 将警告、漏洞、端到端测试失败纳入发布阻断条件。

## 最终判断

当前项目的“模块拆分和包迁移”方向基本正确，但“宿主集成、领域约束、安全授权和生产运行链路”还没有完成。

综合判断：

- 包迁移完成度：约 70%
- 产品级运行链路完成度：约 40%
- 当前发布建议：不发布
- 首要工作：先完成 Host/数据库/Angular 的端到端接入，再处理权限和数据一致性问题
