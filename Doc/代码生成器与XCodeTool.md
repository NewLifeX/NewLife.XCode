# 代码生成器与 XCodeTool（Code）

XCode 的代码生成由 `XCodeTool` 驱动，核心生成器在 `XCode.Code` 命名空间。理解这套流程后，你可以稳定地扩展实体、模型、接口、控制器与自定义业务模板。

## 1）总体流程

`XCodeTool.Program.Build(modelFile, log, manager)` 的主流程：

1. 加载 `Model.xml`（得到 `tables + option + attributes`）
2. 插件 `FixTables` 扩展模型
3. `EntityBuilder.FixModelFile` 修正模型与版本信息
4. 生成实体类：`EntityBuilder.BuildTables`
5. 生成模型类：`ModelBuilder.BuildModels`
6. 生成接口：`InterfaceBuilder.BuildInterfaces`
7. 生成数据字典：`HtmlBuilder.BuildDataDictionary`
8. 生成 Cube 区域/控制器：`CubeBuilder.BuildArea / BuildControllers`

## 2）关键选项（BuilderOption / EntityBuilderOption）

高频配置：

- `Namespace`：命名空间
- `Output`：输出目录
- `ChineseFileName`：中文文件名
- `ClassNameTemplate`：类名模板（`{name}` 占位）
- `ModelClass / ModelInterface`：模型与接口生成模板
- `ModelsOutput / InterfacesOutput`
- `Nullable`：生成可空引用类型
- `ExtendNameSpace`：额外 using
- `Excludes`：排除项

## 3）EntityBuilder 生成重点

### 数据类 + 业务类双文件

每个表会生成：
- `xxx.cs`（数据映射类）
- `xxx.Biz.cs`（业务扩展类）

其中 `.Biz.cs` 支持合并策略：
- `MergeBusiness=true` 时，合并新增片段（扩展属性/扩展查询）

### 自动缓存策略代码

生成器会根据表规模与索引特征生成缓存相关代码：
- `MaxCacheCount`
- `Meta.Cache`
- `Meta.SingleCache` 从键配置

### 自动拦截器

根据字段名自动生成拦截器配置：
- `UserInterceptor`
- `TimeInterceptor`
- `IPInterceptor`
- `TraceInterceptor`
- `TenantInterceptor`

### 自动分片策略

当字段 `DataScale` 是 `timeShard:*` 时，自动生成 `Meta.ShardPolicy = new TimeShardPolicy(...)`。

## 4）模型文件自动修正（FixModelFile）

`EntityBuilder.FixModelFile` 会执行：

- 默认值补齐
- 旧配置项清理
- 名称格式规范化（`NameFormat`）
- 雪花主键自动标记 `DataScale=time`
- `UsingCache` 迁移为 `MaxCacheCount`
- 更新 xsd 到 `Model202509`
- 写入 `ModelVersion`

这是“让旧模型自动升级”的关键环节。

## 5）版本感知升级（Module -> Interceptor）

`EntityBuilder` 会检测引用的 `NewLife.XCode` 版本：

- 版本 `>= 11.23.2026.127-beta0417` 时启用升级
- 将旧类名：`TimeModule/UserModule/IPModule/TraceModule`
- 升级为：`TimeInterceptor/UserInterceptor/IPInterceptor/TraceInterceptor`
- 同时升级 `Meta.Modules -> Meta.Interceptors`

避免老模板在新版本编译失败。

## 6）XCodeTool 的扫描与执行行为

- 支持 `xcode model.xml`
- 无参数时递归扫描当前目录 `*.xml`（跳过 `XCode.xml`）
- 自动识别 `<Tables>` / `<EntityModel>`
- 若没找到模型，会释放默认 `Model.xml`

此外会检测全局工具版本并尝试更新。

## 7）自定义生成器（XCodeTool 扩展）

项目中已有：

- `CustomBuilder`：可额外生成 `.CusBiz.cs`（仅首次）
- `CubeBuilder`：生成区域类与控制器模板；搜索参数中的枚举类型自动生成 using 命名空间并使用 global:: 前缀，Double/Single 字段生成 ToDouble 解析

`CustomBuilder` 适合沉淀“永不覆盖”的业务代码骨架。

## 8）推荐团队工作流

1. 改 `Model.xml`，不手改 `*.cs` 数据映射文件。
2. 执行 `xcode` 生成。
3. 只在 `.Biz.cs` / `.CusBiz.cs` 写业务逻辑。
4. 升级 XCode 版本后，先跑一次生成器做自动迁移。

## 9）常见问题

- **生成后业务代码丢失？**
  - 检查是否误改了 `*.cs` 而非 `.Biz.cs`。
- **旧项目升级后编译报 Module 不存在？**
  - 运行新版生成器触发 Module→Interceptor 升级。
- **分片策略没生成？**
  - 检查模型字段 `DataScale` 是否 `timeShard:*`。

## 10）与其它文档联动

- 分片策略：`Shards分配策略与路由.md`
- 元数据：`元数据管理总览.md`
- 缓存：`实体缓存EntityCache.md` / `单对象缓存SingleCache.md` / `字段缓存FieldCache.md`