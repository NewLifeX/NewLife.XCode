# XCode 协作指令（面向 Copilot）

本指令适用于新生命团队（NewLife）开源的数据中间件 XCode，指导在 .NET 项目中正确、统一地使用 XCode 及相关模板，帮助 Copilot（及类似助手）按步骤可靠执行。

> 说明：以下步骤面向 Windows、Linux、macOS 的通用 .NET CLI。关于“查找解决方案/项目文件”的操作，应由 Copilot 通过自身目录遍历与解析能力完成。

## 摘要
- 目标：规范化 XCode 项目初始化与接入，减少歧义，提升自动化效率。
- 受众：Copilot 或开发者在本地仓库内执行初始化、定位项目、创建数据模型等操作。
- 产物：规范的解决方案/项目结构、已安装的模板、已引入的 `NewLife.XCode` 包、可用的 `Model.xml` 数据模型。

## 1) 前置要求（环境与模板）
1. 检查 .NET SDK 版本（要求 .NET 8.0+）：`dotnet --version`
2. 校验 NewLife.Templates 模板是否已安装且版本满足要求：
   - 查看模板详情：`dotnet new details NewLife.Templates`
    - 判定规则：输出包含类似 `包版本: 1.1.2025.820-beta1836`
       - 其中 `1.1.2025.820` 表示 v1.1，发布日期为 2025-08-20。
       - 要求：发布日期需 > 2025.0801（即严格晚于 2025-08-01）。
   - 若未安装或版本不满足要求：`dotnet new install NewLife.Templates`

## 2) 解决方案与项目定位（已有项目）
1. 查找解决方案（.sln）
   - 要求：Copilot 自动扫描仓库根及子目录定位现有 `.sln` 文件。
   - 若不存在，可创建：`dotnet new sln --name Zero`（“Zero”为示例名，请按系统英文名调整）。

2. 定位数据类库项目（.csproj）
   - 筛选建议：文件名包含 `Data` 后缀，或 `.csproj` 内有 `<OutputType>Library</OutputType>`，或完全无 `<OutputType>`（默认是类库）。
   - 若缺失数据类库项目：`dotnet new xcode -n Zero.Data`（`Zero.Data` 为示例名，一般建议 `{系统英文名}.Data`）。

3. 引入 XCode NuGet 包（若未引入）：`dotnet add package NewLife.XCode`

4. 定位应用启动项目（.csproj）
   - 判定标准：`csproj` 中包含 `<OutputType>Exe</OutputType>`。
   - 新建 Web 项目：`dotnet new cube -n CubeWeb`
   - 新建控制台应用：`dotnet new nconsole -n CubeApp`（名称请按 `{系统英文名}` 或 `{系统英文名}.App` 规范调整）。

## 3) 准备数据模型（Model.xml）
1. 检查是否已有数据模型：Copilot 在数据类库项目范围内判断是否存在如 `Model.xml` 的数据模型文件（也可能是其它名为 `*.xml` 的文件）。
2. 安装/校验 xcode 命令（dotnet 全局工具）：先执行 `dotnet tool list -g` 检查是否存在 `xcodetool`；如未安装则执行 `dotnet tool install xcodetool -g`（安装后提供 `xcode` 命令）。
3. 生成数据模型：在“数据类库项目根目录或其下级子目录”执行 `xcode`（无参数）；默认在当前目录生成 `Model.xml`，即“执行目录即输出目录”，把它重命名为 `{系统英文名}.xml`。

## 3) 修改数据模型
XCode的数据模型文件完整描述的系统的数据表结构，定义每张表的字段和索引。
数据模型文件的标准示例如下：
```xml
<?xml version="1.0" encoding="utf-8"?>
<EntityModel xmlns:xs="http://www.w3.org/2001/XMLSchema-instance" xs:schemaLocation="https://newlifex.com https://newlifex.com/Model202509.xsd" Document="https://newlifex.com/xcode/model" xmlns="https://newlifex.com/Model202509.xsd">
  <Option>
    <!--类名模板。其中{name}替换为Table.Name，如{name}Model/I{name}Dto等-->
    <ClassNameTemplate />
    <!--显示名模板。其中{displayName}替换为Table.DisplayName-->
    <DisplayNameTemplate />
    <!--基类。可能包含基类和接口，其中{name}替换为Table.Name-->
    <BaseClass>Entity</BaseClass>
    <!--命名空间-->
    <Namespace>XCode.Membership</Namespace>
    <!--输出目录-->
    <Output>.\</Output>
    <!--是否使用中文文件名。默认false-->
    <ChineseFileName>True</ChineseFileName>
    <!--用于生成Copy函数的参数类型。例如{name}或I{name}-->
    <ModelNameForCopy />
    <!--带有索引器。实现IModel接口-->
    <HasIModel>True</HasIModel>
    <!--可为null上下文。生成String?等-->
    <Nullable>True</Nullable>
    <!--数据库连接名-->
    <ConnName>Membership</ConnName>
    <!--模型类模版。设置后生成模型类，用于接口数据传输，例如{name}Model-->
    <ModelClass>{name}Model</ModelClass>
    <!--模型类输出目录。默认当前目录的Models子目录-->
    <ModelsOutput>.\Models\</ModelsOutput>
    <!--模型接口模版。设置后生成模型接口，用于约束模型类和实体类，例如I{name}-->
    <ModelInterface>I{name}</ModelInterface>
    <!--模型接口输出目录。默认当前目录的Interfaces子目录-->
    <InterfacesOutput>.\Interfaces\</InterfacesOutput>
    <!--用户实体转为模型类的模型类。例如{name}或{name}DTO-->
    <ModelNameForToModel />
    <!--命名格式。Default/Upper/Lower/Underline-->
    <NameFormat>Default</NameFormat>
    <!--魔方区域显示名-->
    <DisplayName>新生命用户</DisplayName>
    <!--魔方控制器输出目录-->
    <CubeOutput>../../CubeWeb/Areas/CubeWeb</CubeOutput>
  </Option>
  <Tables>
    <Table Name="User" Description="用户。用户帐号信息，以身份验证为中心，拥有多种角色，可加入多个租户">
      <Columns>
        <Column Name="ID" DataType="Int32" Identity="True" PrimaryKey="True" Description="编号" />
        <Column Name="Name" DataType="String" Master="True" Nullable="False" Description="名称。登录用户名" />
        <Column Name="Password" DataType="String" Length="200" Description="密码" />
        <Column Name="DisplayName" DataType="String" Description="昵称" />
        <Column Name="Sex" DataType="Int32" Description="性别。未知、男、女" Type="XCode.Membership.SexKinds" />
        <Column Name="Mail" DataType="String" ItemType="mail" ShowIn="Auto,-Search" Description="邮件。支持登录" />
        <Column Name="MailVerified" DataType="Boolean" Description="邮箱验证。邮箱是否已通过验证" />
        <Column Name="Mobile" DataType="String" ItemType="mobile" ShowIn="Y|Y|A|A|N" Description="手机。支持登录" />
        <Column Name="MobileVerified" DataType="Boolean" Description="手机验证。手机是否已通过验证" />
        <Column Name="Code" DataType="String" ShowIn="11110" Description="代码。身份证、员工编码等，支持登录" />
        <Column Name="AreaId" DataType="Int32" Map="XCode.Membership.Area@Id@$@AreaPath" Description="地区。省市区" />
        <Column Name="Avatar" DataType="String" ItemType="image" Length="200" Description="头像" />
        <Column Name="RoleID" DataType="Int32" Map="Role@Id@Name" DefaultValue="3" Description="角色。主要角色" Category="登录信息" />
        <Column Name="RoleIds" DataType="String" Length="200" Description="角色组。次要角色集合" Category="登录信息" />
        <Column Name="DepartmentID" DataType="Int32" Map="Department@Id@Name" Description="部门。组织机构" Category="登录信息" />
        <Column Name="Online" DataType="Boolean" Description="在线" Category="登录信息" />
        <Column Name="Enable" DataType="Boolean" Description="启用" Category="登录信息" />
        <Column Name="Age" DataType="Int32" Description="年龄。周岁" />
        <Column Name="Birthday" DataType="DateTime" Description="生日。公历年月日" />
        <Column Name="Logins" DataType="Int32" Description="登录次数" Category="登录信息" />
        <Column Name="LastLogin" DataType="DateTime" Description="最后登录" Category="登录信息" />
        <Column Name="LastLoginIP" DataType="String" Description="最后登录IP" Category="登录信息" />
        <Column Name="RegisterTime" DataType="DateTime" Description="注册时间" Category="登录信息" />
        <Column Name="RegisterIP" DataType="String" Description="注册IP" Category="登录信息" />
        <Column Name="OnlineTime" DataType="Int32" ItemType="TimeSpan" Description="在线时间。累计在线总时间，单位秒" Category="登录信息" />
        <Column Name="Ex1" DataType="Int32" Description="扩展1" Category="扩展" />
        <Column Name="Ex2" DataType="Int32" Description="扩展2" Category="扩展" />
        <Column Name="Ex3" DataType="Double" Description="扩展3" Category="扩展" />
        <Column Name="Ex4" DataType="String" Description="扩展4" Category="扩展" />
        <Column Name="Ex5" DataType="String" Description="扩展5" Category="扩展" />
        <Column Name="Ex6" DataType="String" Description="扩展6" Attribute="XmlIgnore, IgnoreDataMember, ScriptIgnore" Category="扩展" />
        <Column Name="UpdateUser" DataType="String" Nullable="False" DefaultValue="''" Description="更新者" Model="False" Category="扩展" />
        <Column Name="UpdateUserID" DataType="Int32" Description="更新用户" Model="False" Category="扩展" />
        <Column Name="UpdateIP" DataType="String" Description="更新地址" Model="False" Category="扩展" />
        <Column Name="UpdateTime" DataType="DateTime" Nullable="False" Description="更新时间" Model="False" Category="扩展" />
        <Column Name="Remark" DataType="String" Length="500" Description="备注" Category="扩展" />
      </Columns>
      <Indexes>
        <Index Columns="Name" Unique="True" />
        <Index Columns="Mail" />
        <Index Columns="Mobile" />
        <Index Columns="Code" />
        <Index Columns="RoleID" />
        <Index Columns="DepartmentID" />
        <Index Columns="UpdateTime" />
      </Indexes>
    </Table>
    <Table Name="Log" Description="日志。应用系统审计日志，记录用户的各种操作，禁止修改和删除" ConnName="Log" InsertOnly="True" UsingCache="False">
      <Columns>
        <Column Name="ID" DataType="Int64" PrimaryKey="True" DataScale="time" Description="编号" />
        <Column Name="Category" DataType="String" Description="类别" />
        <Column Name="Action" DataType="String" Nullable="False" Description="操作" />
        <Column Name="LinkID" DataType="Int64" Description="链接" />
        <Column Name="Success" DataType="Boolean" Description="成功" />
        <Column Name="UserName" DataType="String" Description="用户名" />
        <Column Name="Ex1" DataType="Int32" Description="扩展1" Category="扩展" />
        <Column Name="Ex2" DataType="Int32" Description="扩展2" Category="扩展" />
        <Column Name="Ex3" DataType="Double" Description="扩展3" Category="扩展" />
        <Column Name="Ex4" DataType="String" Description="扩展4" Category="扩展" />
        <Column Name="Ex5" DataType="String" Description="扩展5" Category="扩展" />
        <Column Name="Ex6" DataType="String" Description="扩展6" Category="扩展" />
        <Column Name="TraceId" DataType="String" Description="性能追踪。用于APM性能追踪定位，还原该事件的调用链" />
        <Column Name="CreateUser" DataType="String" Description="创建者" Category="扩展" />
        <Column Name="CreateUserID" DataType="Int32" ShowIn="Auto,-Search" Map="User@Id@$" Description="创建用户" Category="扩展" />
        <Column Name="CreateIP" DataType="String" Description="创建地址" Category="扩展" />
        <Column Name="CreateTime" DataType="DateTime" Nullable="False" Description="时间" />
        <Column Name="Remark" DataType="String" Length="2000" Description="详细信息" />
      </Columns>
      <Indexes>
        <Index Columns="Action,Category,ID" />
        <Index Columns="Category,LinkID,ID" />
        <Index Columns="CreateUserID,ID" />
      </Indexes>
    </Table>
  </Tables>
</EntityModel>
```


## 4) 典型创建路径（从零开始）
1. 新建解决方案：`dotnet new sln --name Zero`
2. 新建数据类库项目：`dotnet new xcode -n Zero.Data`
3. 将项目加入解决方案：`dotnet sln add ./Zero.Data/Zero.Data.csproj`
4. 为数据类库添加 XCode 包：`dotnet add ./Zero.Data/Zero.Data.csproj package NewLife.XCode`
5. 新建应用启动项目（任选其一）：
   - Web：`dotnet new cube -n CubeWeb`，然后 `dotnet sln add ./CubeWeb/CubeWeb.csproj`
   - 控制台：`dotnet new nconsole -n CubeApp`，然后 `dotnet sln add ./CubeApp/CubeApp.csproj`

## 5) Copilot 执行要点（给自动化助手）
- 使用 Copilot 自身能力定位 `.sln` 与 `*.csproj`，根据 `OutputType` 判定项目类型。
- 模板策略：先 `dotnet new details NewLife.Templates` 检查；未安装或发布日期 ≤ 2025.0801 则 `dotnet new install NewLife.Templates`；不进行在线搜索。
- 若缺失数据类库：`dotnet new xcode -n {系统英文名}.Data`。
- 确保数据类库包含 `NewLife.XCode` 包；若无则执行添加命令。
- 若缺失数据模型：先检查/安装 dotnet 工具（`dotnet tool list -g`；未装则 `dotnet tool install xcodetool -g`），再在“数据类库项目根目录或其下级子目录”执行 `xcode`（无参数），默认在当前目录生成 `Model.xml`。
- 变更保持最小影响面，不做无关格式化；保留并补充必要注释。

## 6) 待确认 / 不明确清单（请提供权威信息）
1. `Model.xml` 的默认生成路径与命名；是否支持参数指定输出目录/文件名；推荐运行目录。
2. 示例名 `Zero`、`Zero.Data`、`CubeWeb`、`CubeApp` 是否仅为示例；团队是否有统一命名约定。
3. 模板 `cube` 与 `nconsole` 的最小可用版本、关键差异与推荐使用场景（Web vs 控制台）。
