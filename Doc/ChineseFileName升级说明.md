# ChineseFileName 配置升级说明

## 问题背景

在 2026 年 1 月 6 日，Program.cs 中注释了强制设置 `option.ChineseFileName = true;` 的代码，改为使用 xml 文件中的配置。但旧的 xml 文件中 `ChineseFileName` 默认值为 `false`，导致已有项目生成的文件名从中文变为英文。

## 解决方案

通过版本号机制自动识别并升级旧配置：

### 1. 版本号标记

- **ModelVersion < 2.0.2026.0130**：旧版本配置，ChineseFileName 默认 false
- **ModelVersion >= 2.0.2026.0130**：新版本配置，ChineseFileName 默认 true
- **无 ModelVersion**：通过文件修改时间判断（早于 2026-01-30 视为旧版本）

版本号格式：`主版本.子版本.年.月日`，例如 `2.0.2026.0130` 表示 2026年1月30日的 2.0 版本。

### 2. 版本号存储机制

版本号通过 `BuilderOption.ModelVersion` 属性集中管理：

- 该属性类型为 `Version?`，不参与 XML 序列化（标记为 `[XmlIgnore, IgnoreDataMember]`）
- **加载模型文件时**，`ClassBuilder.LoadModels` 自动从 xml 的 `ModelVersion` 特性读取并设置该属性
- **修正模型文件时**，`EntityBuilder.FixModelFile` 比较该属性与当前最新版本，必要时更新
- **保存模型文件时**，将该属性值写入 xml 的 `ModelVersion` 特性

### 3. 自动升级逻辑

加载模型后，`option.ModelVersion` 已自动设置，可以直接使用：

```csharp
// 从atts中读取模型版本号
if (atts.TryGetValue("ModelVersion", out var versionStr) && Version.TryParse(versionStr, out var version))
{
    option.ModelVersion = version;
}

// 自动升级旧配置
if (!option.ChineseFileName)
{
    var needUpgrade = false;
    var targetVersion = new Version(2, 0, 2026, 130);
    
    if (option.ModelVersion != null)
    {
        // 如果有版本号，检查是否低于 2.0.2026.0130
        if (option.ModelVersion < targetVersion)
        {
            needUpgrade = true;
            XTrace.WriteLine("检测到旧版本({0})模型文件，自动启用中文文件名", option.ModelVersion);
        }
    }
    else
    {
        // 无版本号，检查文件修改时间
        var cutoffDate = new DateTime(2026, 1, 30);
        var fileInfo = new FileInfo(modelFile);
        if (fileInfo.LastWriteTime < cutoffDate)
        {
            needUpgrade = true;
            XTrace.WriteLine("检测到旧版本模型文件(无版本号且修改时间早于2026-01-30)，自动启用中文文件名");
        }
    }

    if (needUpgrade)
        option.ChineseFileName = true;
}
```

### 4. 版本号自动添加与更新

在 `EntityBuilder.FixModelFile` 方法中：

```csharp
// 添加模型版本号，用于自动升级配置（2.0.2026.0130开始ChineseFileName默认为true）
const String currentModelVersion = "2.0.2026.0130";
var curVer = Version.Parse(currentModelVersion);

// 如果模型版本低于当前版本，则更新
if (option.ModelVersion == null || option.ModelVersion < curVer)
{
    option.ModelVersion = curVer;
}

// 将版本号写入atts，用于保存到XML
atts["ModelVersion"] = option.ModelVersion.ToString();
```

当 xcodetool 运行时，会自动修正模型文件：
- 如果没有版本号，添加 `ModelVersion="2.0.2026.0130"`
- 如果有旧版本号（如 `1.0` 或 `2.0.2025.1215`），更新为 `2.0.2026.0130`

## 使用场景

### 场景1：旧项目自动升级

```xml
<!-- 旧的 model.xml（无版本号或 ModelVersion="1.0"） -->
<Tables ...>
  <!-- ChineseFileName 未设置或为 false -->
</Tables>
```

**结果**：自动启用 `ChineseFileName = true`，继续使用中文文件名，并更新版本号为 `2.0.2026.0130`

### 场景2：明确设置为 false 的配置

```xml
<!-- model.xml -->
<Tables xmlns="..." ChineseFileName="false" ModelVersion="2.0.2026.0130">
</Tables>
```

**结果**：保持 `ChineseFileName = false`，因为用户明确设置且版本号 >= 2.0.2026.0130

### 场景3：新创建的配置文件

通过 `FixModelFile` 自动添加：

```xml
<Tables xmlns="..." ModelVersion="2.0.2026.0130">
  <!-- 自动添加版本号 -->
</Tables>
```

**结果**：新文件带最新版本号，未来不会被误判为旧版本

## 版本兼容性

| ModelVersion | ChineseFileName 未设置时的默认值 | 说明 |
|--------------|--------------------------------|------|
| 无版本号（且文件早于 2026-01-30） | `true` | 旧项目自动升级 |
| 无版本号（且文件晚于 2026-01-30） | `false` | 新创建文件，等待修正时添加版本号 |
| < 2.0.2026.0130 | `true` | 明确标记的旧版本，自动升级 |
| >= 2.0.2026.0130 | `false` | 新版本，遵循 xml 配置 |

## 实现细节

### BuilderOption 新增属性

```csharp
/// <summary>模型版本号。用于自动升级配置，不参与XML序列化</summary>
[XmlIgnore, IgnoreDataMember]
public Version? ModelVersion { get; set; }
```

该属性：
- 不会序列化到 XML 文件中
- 在内存中统一管理版本号
- 通过 `atts` 字典与 XML 的 `ModelVersion` 特性交互

### 版本号更新机制

当需要引入新的配置变更时，开发者可以修改 `EntityBuilder.cs` 中的 `currentModelVersion` 常量：

```csharp
const String currentModelVersion = "2.1.2026.0615";  // 示例：引入新的配置项
```

系统会自动将旧版本号更新为新版本号，并触发相应的升级逻辑。

## 注意事项

1. **不影响已明确设置的配置**：如果 xml 中明确设置了 `ChineseFileName="false"` 且 `ModelVersion="2.0.2026.0130"`，则保持不变
2. **自动修正机制**：首次运行后会自动添加或更新 `ModelVersion` 到 xml 文件
3. **时间判断兜底**：对于没有版本号的文件，通过文件修改时间判断（兼容未及时升级的情况）
4. **版本号格式**：采用 `主版本.子版本.年.月日` 格式，便于追踪变更时期
5. **集中管理**：版本号通过 `BuilderOption.ModelVersion` 属性统一管理，避免重复解析

## 开发建议

- 新项目建议显式设置 `ChineseFileName` 配置，避免依赖默认值
- 定期运行 xcodetool 让模型文件自动更新到最新版本号
- 引入新的配置变更时，更新 `EntityBuilder.FixModelFile` 中的 `currentModelVersion` 常量
