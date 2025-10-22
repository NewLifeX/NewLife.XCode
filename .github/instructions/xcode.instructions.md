# XCode 协作指令

本指令适用于新生命团队（NewLife）开源的数据中间件XCode，用于规范在使用XCode组件的项目中使用 Copilot（及类似智能助手）时的行为，主要面向.Net技术栈。

## 1. 准备工作
- 安装 NewLife.Templates 模板。`dotnet new install NewLife.Templates`
- 创建XML数据模型文件。如果当前目录下没有 `*.xml` 文件，则运行 `xcode` 自动生成 `Model.xml` 文件。
- 数据类库项目引用 XCode。在当前目录或上级目录中查找`*.csproj` 文件，并添加以下内容：
```xml
<ItemGroup>
  <PackageReference Include="NewLife.XCode" Version="*" />
</ItemGroup>
```
