# NewLife.XCode - 数据中间件

![GitHub top language](https://img.shields.io/github/languages/top/newlifex/newlife.xcode?logo=github)
![GitHub License](https://img.shields.io/github/license/newlifex/newlife.xcode?logo=github)
![Nuget Downloads](https://img.shields.io/nuget/dt/newlife.xcode?logo=nuget)
![Nuget](https://img.shields.io/nuget/v/newlife.xcode?logo=nuget)
![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/newlife.xcode?label=dev%20nuget&logo=nuget)

数据中间件，聚焦于添删改查、性能和大数据，支持MySQL、SQLite、SqlServer、Oracle、Postgresql、TDengine、达梦、金仓、瀚高。  

XCode文档：[https://newlifex.com/xcode](https://newlifex.com/xcode)  

## 快速拥有

​	使用NewLife组件的最简便方式是从Nuget引用，例如在项目Nuget管理中搜索`NewLife.XCode` 并引入。

​	NewLife组件由社区共创20多年，使用MIT开源协议，**任何人可任意修改并再次发行**（无需声明来源）！许多企业基于此构建内部开发框架时，甚至可通过批量替换源码中所有`NewLife`字符串为贵公司名实现私有化定制。

​	团队始终秉承开放态度，不仅支持VisualStudio（最新正式版）打开解决方案编译，也兼容`dotnet build`命令行编译，项目文件摒弃复杂功能以追求简单易用，真正做到开箱即用。

​	我们公开强命名证书`newlife.snk`以支持独自编译替换程序集。



​	命令行中运行以下命令快速体验NewLife组件：

```
dotnet new install NewLife.Templates
dotnet new xcode --name NewLife.Data
cd NewLife.Data
dotnet build
```


## 数据中间件
功能特点：  

#### 化繁为简

建立XML数据模型后，经代码生成得到实体类，即可使用实体类对数据进行添删改查操作。

开发环境默认使用SQLite数据库，发布到生产环境时再配置连接字符串即可。

日常项目中90%的功能开发无需掌握数据库和SQL，降低对开发者的能力要求，特别是Java等其它语言转过来的开发者。

#### 多数据库迁移

支持MySQL、SQLite、SqlServer、Oracle、Postgresql等各种主流数据库，基于XCode开发的应用无需修改编译即可迁移到另一种数据库上。甚至开发数据库和生产数据库是两种不同的数据库，例如常见SQLite开发，生产用MySql。

#### 高性能

极致的多级缓存设计，批量操作优化，单机**插入速度最高89万tps，查询速度最高18亿qps**。[飞仙](http://feixian.newlifex.com)  

单表查询配合内存关联（带数据缓存），降低系统复杂度并提升性能。

#### 大数据

正式项目最大**单表100亿数据**，分页查询的思想贯穿上下。

根据模型表的索引自动生成各种高效的查询方法，让开发者无惧大数据表。

#### 自动分表分库

支持任意数据库按策略自动分表分库，无需修改业务代码，XCode能够自动找到正确的表。

例如星尘跟踪数据按天分表，配置分表策略后，其它添删改查操作跟单表一样：

```c#
Meta.ShardPolicy = new TimeShardPolicy(nameof(Id), Meta.Factory)
{
    ConnPolicy = "{0}",
    TablePolicy = "{0}_{1:yyyyMMdd}",
    Step = TimeSpan.FromDays(1),
};
```

#### 反向工程

根据实体类主动建立数据库表结构并初始化数据（支持所有数据库），开发阶段新增字段，在发布到生产后自动新增，开发者无需准备SQL发布脚本。

#### 跨库数据迁移

支持任意数据库进行数据迁移。[跨库数据迁移工具](https://github.com/qaz734913414/DataMigration)  



## 快速入门

### 新建项目

打开[Visual Studio](https://x.newlifex.com/vs2022.exe)，新建一个控制台项目，从Nuget引入**NewLife.XCode**。

### 新建模型

在项目目录下编写一个简单Xml模型文件db.xml，或者下载[xcodetool.exe](http://x.newlifex.com/xcodetool.exe)保存到项目目录，运行xcodetool.exe即可得到一个样本模型文件。

```xml
<?xml version="1.0" encoding="utf-8"?>
<Tables Version="9.6.6644.19804" Output="MyEntity" NameSpace="Model" BaseClass="Entity" ConnName="DB">
  <Table Name="UserInfo" Description="用户信息">
    <Columns>
      <Column Name="Id" DataType="Int32" Identity="True" PrimaryKey="True" Description="会员id" />
      <Column Name="UserName" DataType="String" Nullable="False" Description="会员名称" />
      <Column Name="Password" DataType="String" Nullable="False" Description="会员密码" />
      <Column Name="Age" DataType="Int32" Description="会员年龄" />
    </Columns>
  </Table>
</Tables>
```
双击xml文件旁边的[xcodetool.exe](https://x.newlifex.com/xcodetool.exe)，这时你会看到生成一个MyEntity文件目录，目录包含了两个文件：用户信息.Biz.cs、用户信息.cs。 “用户信息.cs” 一般称为数据类，每次代码生成都会覆盖，“用户信息.Biz.cs”称为业务类，首次生成后不再覆盖，适合用户在其中写代码。

如果不用xcodetool.exe，也可以从Nuget安装全局工具：`dotnet tool install xcodetool -g --prerelease`。

然后在项目目录下执行命令：`xcode db.xml`

### 增删改查
* Program.cs文件最上面引用命名空间
```c#
using XCode;
```
不然会使用不了Save、Update、Delete等扩展方法。

* 新增一条数据。注：这里没有指定数据库，所以XCode默认的是sqlite数据库。代码完成后可以直接运行看效果。
```csharp
var user = new UserInfo();
user.UserName = "张三";
user.Password = "123456";
user.Age = 18;
user.Insert();
//user.Save()等效

Console.WriteLine("插入一条新数据，用户id为：" + user.Id);
```
* 修改一条数据
```csharp
var user = UserInfo.FindById(1);
user.UserName = "张三";
user.Password = "123456";
user.Age = 19;
user.Update();
//user.Save()等效

user = UserInfo.FindById(1);
Console.WriteLine($"用户ID={user.Id}已修改岁数，岁数为：{user.Age}");
```
* 删除一条数据
```csharp
var user = UserInfo.FindById(1);
user.Delete();

user = UserInfo.FindById(1);
Console.WriteLine("用户" + (user == null ? "已删除" : "还存在"));
```
* 查询数据，查询是一个十分复杂的需求，这里只举一些简单的例子
```csharp
// 根据Id查询用户信息
var userId = 2;
var user = UserInfo.FindById(userId);
Console.WriteLine($"ID为{user.Id}用户姓名为：{user.UserName}");

// 根据用户名称查询
var userList = UserInfo.FindAll(UserInfo._.UserName == "张三");
Console.WriteLine($"查询符合姓名为张三的记录有{userList.Count}");

// 多复合条件查询
userList = UserInfo.FindAll(UserInfo._.UserName == "张三" & UserInfo._.Age == 19);
Console.WriteLine($"查询符合姓名为张三年龄为19的记录有{userList.Count}");
```

### 高级用法
更新某些字段。在“用户信息.Biz.cs”中新增方法写以下代码会更简洁，内置_类无需类名前缀。

```csharp
UserInfo.Update(UserInfo._.UserName == "李四" & UserInfo._.Age == 18, UserInfo._.Id == 1);
// 相当于Update UserInfo Set UserName='李四' And Age=18 Where Id=1
// 绕过了缓存，不推荐这种写法。建议先查出来对象，然后修改并Update回去
```

复杂的查询语句

```csharp
var userList = UserInfo.FindAll(UserInfo._.UserName == "张三" & UserInfo._.Age == 19, UserInfo._.UserName.Desc(), string.Join(",", UserInfo._.UserName, UserInfo._.Age), 0, 0);
// 相当于Select UserName,Age From UserInfo Where UserName='张三' And Age=19 Order By  UserName desc
```

带分页排序的复杂查询  
一般写在实体类业务文件（.Biz.cs）里面，可以直接使用实体类内置_类而无需加类名前缀。

```csharp
public static IList<UserInfo> Search(String name, Int32 age, Pager p)
{
    // 多条件复杂查询
    var exp = new WhereExpression();
    if(!name.IsNullOrEmpty()) exp &= _.UserName == name;
    if(age > 0) exp &= _.Age == age;

    return FindAll(exp, p);
}

void Test()
{
    // 默认第一页，每页20行。魔方NewLife.Cube自动从页面Request获取以下参数
    var p = new Pager();
    p.PageIndex = 3;
    p.PageSize = 10;
    p.Sort = UserInfo.__.Age;
    p.Desc = true;

    // 需要总记录数来分页，FindAll后p.TotalCount有总记录数，用于计算页数PageCount
    p.RetrieveTotalCount = true;

    // 相当于Select * From UserInfo Where UserName='张三' And Age=19 Order By Age Desc limit 20, 10
    var list = UserInfo.Search("张三", 19, p);
}
```
复合排序的分页查询语句

```csharp
var pageIndex = 2;//第二页
var pageSize = 10;//每页十行

var userList = UserInfo.FindAll(UserInfo._.UserName == "张三" & UserInfo._.Age == 19, " UserName desc,Age asc", string.Join(",", UserInfo._.UserName, UserInfo._.Age), (pageIndex - 1) * pageSize, pageSize);
//相当于Select UserName,Age From UserInfo Where UserName='张三' And Age=19 Order By UserName desc,Age asc limit 10, 10
```

### 索引查询

修改XML模型文件，增加索引。

```xml
  <Tables>
    <Table Name="UserInfo" Description="用户信息">
      <Columns>
        <Column Name="Id" DataType="Int32" Identity="True" PrimaryKey="True" Description="会员id" />
        <Column Name="UserName" DataType="String" Nullable="False" Description="会员名称" />
        <Column Name="Password" DataType="String" Nullable="False" Description="会员密码" />
        <Column Name="Age" DataType="Int32" Description="会员年龄" />
      </Columns>
      <Indexes>
        <Index Columns="UserName" Unique="True" />
        <Index Columns="UserName,Age" />
      </Indexes>
    </Table>
  </Tables>
```

再次运行xcodetool.exe，即可看到，“用户信息.cs”增加了FindByUserName方法。此处不建议认为修改，下次代码生成会覆盖。

```csharp
    /// <summary>根据会员名称查找</summary>
    /// <param name="userName">会员名称</param>
    /// <returns>实体对象</returns>
    public static UserInfo FindByUserName(String userName)
    {
        if (userName.IsNullOrEmpty()) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.UserName.EqualIgnoreCase(userName));

        return Find(_.UserName == userName);
    }
```

“用户信息.Biz.cs”中增加了Search方法。在Biz业务类中，这个代码可以根据需要修改调整，代码生成器不会覆盖。

```csharp
    /// <summary>高级查询</summary>
    /// <param name="userName">会员名称</param>
    /// <param name="age">会员年龄</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<UserInfo> Search(String userName, Int32 age, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (!userName.IsNullOrEmpty()) exp &= _.UserName == userName;
        if (age >= 0) exp &= _.Age == age;
        if (!key.IsNullOrEmpty()) exp &= _.UserName.Contains(key) | _.Password.Contains(key);

        return FindAll(exp, page);
    }
```



## 容器化部署

XCode 支持从环境变量中获取数据库连接字符串，用于容器化部署。  
连接字符串的环境变量命名规范为 `XCode_{ConnName}`，不区分大小写，连接字符串中需要使用provider指定数据库类型。  



# 新生命项目矩阵

各项目默认支持net8.0/netstandard2.1/netstandard2.0/net4.61/net4.5，旧版（2023.0308）支持net4.0/net2.0  

|                               项目                               | 年份  | 说明                                                                                   |
| :--------------------------------------------------------------: | :---: | -------------------------------------------------------------------------------------- |
|                             基础组件                             |       | 支撑其它中间件以及产品项目                                                             |
|          [NewLife.Core](https://github.com/NewLifeX/X)           | 2002  | 核心库，日志、配置、缓存、网络、序列化、APM性能追踪                                    |
|    [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode)    | 2005  | 大数据中间件，单表百亿级，MySql/SQLite/SqlServer/Oracle/TDengine/达梦，自动分表        |
|      [NewLife.Net](https://github.com/NewLifeX/NewLife.Net)      | 2005  | 网络库，单机千万级吞吐率（2266万tps），单机百万级连接（400万Tcp）                      |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | 2011  | RPC通信框架，内网高吞吐或物联网硬件设备场景                                            |
|     [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube)     | 2010  | 魔方快速开发平台，集成了用户权限、SSO登录、OAuth服务端等，单表100亿级项目验证          |
|    [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent)    | 2008  | 服务管理组件，把应用安装成为操作系统守护进程，Windows服务、Linux的Systemd              |
|     [NewLife.Zero](https://github.com/NewLifeX/NewLife.Zero)     | 2020  | Zero零代脚手架，基于NewLife组件生态的项目模板，Web、WebApi、Service                    |
|                              中间件                              |       | 对接知名中间件平台                                                                     |
|    [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis)    | 2017  | Redis客户端，微秒级延迟，百万级吞吐，丰富的消息队列，百亿级数据量项目验证              |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | 2018  | RocketMQ纯托管客户端，支持Apache RocketMQ和阿里云消息队列，十亿级项目验                |
|     [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT)     | 2019  | 物联网消息协议，MqttClient/MqttServer，客户端支持阿里云物联网                          |
|      [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT)      | 2022  | IoT标准库，定义物联网领域的各种通信协议标准规范                                        |
|   [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus)   | 2022  | ModbusTcp/ModbusRTU/ModbusASCII，基于IoT标准库实现，支持IoT平台和IoTEdge               |
|  [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens)  | 2022  | 西门子PLC协议，基于IoT标准库实现，支持IoT平台和IoTEdge                                 |
|      [NewLife.Map](https://github.com/NewLifeX/NewLife.Map)      | 2022  | 地图组件库，封装百度地图、高德地图和腾讯地图                                           |
|       [NewLife.IP](https://github.com/NewLifeX/NewLife.IP)       | 2022  | IP地址库，IP地址转物理地址                                                             |
|                             产品平台                             |       | 产品平台级，编译部署即用，个性化自定义                                                 |
|           [AntJob](https://github.com/NewLifeX/AntJob)           | 2019  | 蚂蚁调度，分布式大数据计算平台（实时/离线），蚂蚁搬家分片思想，万亿级数据量项目验证    |
|         [Stardust](https://github.com/NewLifeX/Stardust)         | 2018  | 星尘，分布式服务平台，节点管理、APM监控中心、配置中心、注册中心、发布中心              |
|      [NewLife.ERP](https://github.com/NewLifeX/NewLife.ERP)      | 2021  | 企业ERP，产品管理、客户管理、销售管理、供应商管理                                      |
|         [CrazyCoder](https://github.com/NewLifeX/XCoder)         | 2006  | 码神工具，众多开发者工具，网络、串口、加解密、正则表达式、Modbus                       |
|           [XProxy](https://github.com/NewLifeX/XProxy)           | 2005  | 产品级反向代理，NAT代理、Http代理                                                      |
|        [HttpMeter](https://github.com/NewLifeX/HttpMeter)        | 2022  | Http压力测试工具                                                                       |
|         [GitCandy](https://github.com/NewLifeX/GitCandy)         | 2015  | Git源代码管理系统                                                                      |
|          [SmartOS](https://github.com/NewLifeX/SmartOS)          | 2014  | 嵌入式操作系统，完全独立自主，支持ARM Cortex-M芯片架构                                 |
|          [SmartA2](https://github.com/NewLifeX/SmartA2)          | 2019  | 嵌入式工业计算机，物联网边缘网关，高性能.NET6主机，应用于工业、农业、交通、医疗        |
|                           菲凡物联FIoT                           | 2020  | 物联网整体解决方案，建筑、环保、农业，软硬件及大数据分析一体化，单机十万级点位项目验证 |
|                           NewLife.UWB                            | 2020  | 厘米级（10~20cm）高精度室内定位，软硬件一体化，与其它系统联动，大型展厅项目验证        |



# 新生命开发团队

![XCode](https://newlifex.com/logo.png)  

新生命团队（NewLife）成立于2002年，是新时代物联网行业解决方案提供者，致力于提供软硬件应用方案咨询、系统架构规划与开发服务。  
团队主导的开源NewLife系列组件已被广泛应用于各行业，Nuget累计下载量高达260余万次。  
团队开发的大数据核心组件NewLife.XCode、蚂蚁调度计算平台AntJob、星尘分布式平台Stardust、缓存队列组件NewLife.Redis以及物联网平台NewLife.IoT，均成功应用于电力、高校、互联网、电信、交通、物流、工控、医疗、文博等行业，为客户提供了大量先进、可靠、安全、高质量、易扩展的产品和系统集成服务。  

我们将不断通过服务的持续改进，成为客户长期信赖的合作伙伴，通过不断的创新和发展，成为国内优秀的IT服务供应商。  

`新生命团队始于2002年，部分开源项目具有20年以上漫长历史，源码库保留有2010年以来所有修改记录`  
网站：https://newlifex.com  
开源：https://github.com/newlifex  
QQ群：1600800/1600838  
微信公众号：  
![智能大石头](https://newlifex.com/stone.jpg)  
