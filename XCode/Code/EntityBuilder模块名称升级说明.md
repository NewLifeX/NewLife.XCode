# EntityBuilder 模块名称升级功能说明

## 功能概述

从 XCode 11.23.2026.127-beta0417 版本开始，旧的模块类名（如 `TimeModule`、`UserModule` 等）已升级为拦截器类名（如 `TimeInterceptor`、`UserInterceptor` 等）。

`EntityBuilder` 新增了自动检测 XCode 版本并升级模块名称的功能。

## 工作原理

### 1. 版本检测

`EntityBuilder.DetectXCodeVersion(String startPath, Int32 maxLevels = 5)` 方法：
- 从指定路径开始，向上查找最多 5 层目录中的 `.csproj` 文件
- 解析 csproj 文件中的 `PackageReference`，获取 `NewLife.XCode` 的版本号
- 返回检测到的版本号，如果未找到则返回 `null`

支持的 csproj 格式：
```xml
<!-- 格式1：单行属性 -->
<PackageReference Include="NewLife.XCode" Version="11.23.2026.127-beta0417" />

<!-- 格式2：嵌套元素 -->
<PackageReference Include="NewLife.XCode">
  <Version>11.23.2026.127-beta0417</Version>
</PackageReference>
```

### 2. 版本判断

`EntityBuilder.NeedUpgradeModuleNames(String? version)` 方法：
- 判断版本号是否 >= 11.23.2026.127-beta0417
- 支持正式版和 beta 版本的比较
- 返回 `true` 表示需要升级，`false` 表示不需要

### 3. 自动升级

在 `EntityBuilder.BuildTables()` 方法中：
- 自动检测当前项目的 XCode 版本
- 根据版本号自动设置 `EnableUpgradeModuleNames` 属性
- 生成业务类时，如果 `EnableUpgradeModuleNames = true`，则执行名称升级

升级内容包括：
- `TimeModule` → `TimeInterceptor`
- `UserModule` → `UserInterceptor`
- `IPModule` → `IPInterceptor`
- `TraceModule` → `TraceInterceptor`
- `Meta.Modules` → `Meta.Interceptors`
- `EntityModules` → `EntityInterceptors`

## 使用方式

### 自动模式（推荐）

使用 `EntityBuilder.BuildTables()` 时，会自动检测版本并决定是否升级：

```csharp
// 自动检测 XCode 版本并执行升级
EntityBuilder.BuildTables(tables, option, log);
```

输出日志示例：
```
生成实体类 D:\MyProject\Entity
检测到XCode版本 11.23.2026.127-beta0417，启用模块名称升级
```

### 手动模式

如果需要手动控制，可以创建 `EntityBuilder` 实例并设置属性：

```csharp
var builder = new EntityBuilder
{
    AllTables = tables,
    Option = option,
    Log = log,
    EnableUpgradeModuleNames = true  // 手动启用升级
};

builder.Load(table);
builder.Business = true;
builder.Execute();
builder.Save(null, false, chineseFileName);
```

### 外部版本检测

外部工具（如 xcodetool）可以单独使用版本检测方法：

```csharp
// 检测当前目录的 XCode 版本
var version = EntityBuilder.DetectXCodeVersion(Environment.CurrentDirectory);
if (EntityBuilder.NeedUpgradeModuleNames(version))
{
    Console.WriteLine($"当前 XCode 版本 {version} 需要升级模块名称");
}
```

## 注意事项

1. **自动检测范围**：从输出目录开始，向上最多查找 5 层目录中的 csproj 文件
2. **仅升级业务类**：只在生成 `.Biz.cs` 业务类文件时执行升级
3. **已存在文件**：只升级已存在的业务类文件，不影响新生成的文件
4. **安全性**：升级前会检查文件是否存在，升级后只有内容变化才会保存

## 版本要求

- 最低目标版本：XCode 11.23.2026.127-beta0417
- 低于此版本的项目不会执行升级

## 相关属性

- `EntityBuilder.EnableUpgradeModuleNames`：是否启用升级模块名称，默认 `false`
- 在 `BuildTables()` 中会根据检测到的版本自动设置此属性
