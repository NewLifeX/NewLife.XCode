# NewLife.XCode - 数据中间件

![GitHub top language](https://img.shields.io/github/languages/top/newlifex/newlife.xcode?logo=github)
![GitHub License](https://img.shields.io/github/license/newlifex/newlife.xcode?logo=github)
![Nuget Downloads](https://img.shields.io/nuget/dt/newlife.xcode?logo=nuget)
![Nuget](https://img.shields.io/nuget/v/newlife.xcode?logo=nuget)
![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/newlife.xcode?label=dev%20nuget&logo=nuget)

数据中间件，支持MySQL、SQLite、SqlServer、Oracle、Postgresql、TDengine、达梦，重点在缓存、性能、分表、自动建表。  

XCode文档：[https://newlifex.com/xcode](https://newlifex.com/xcode)  

## 大数据中间件
主要特点：  
1，超高性能，极致的缓存设计，批量操作优化，插入速度最高77万tps，查询速度最高18亿qps。[飞仙](http://feixian.newlifex.com)  
2，反向工程，根据实体类主动建立数据库表结构并初始化数据，支持多种数据库  
3，自动分表分库，支持任意数据库，无需修改业务代码  
4，跨库数据迁移，支持任意数据库进行数据迁移。[跨库数据迁移工具](https://github.com/qaz734913414/DataMigration)  

## XCode具有哪些优势？

* 采用最好的分页算法，高效处理海量数据。  
数据分页的思想贯穿整个XCode的生命周期，任何一个不论大小的测试，数据样本都是单表一千万起。  
其它很多ORM，在数据达到百万之后会变慢，达到千万后基本上就卡死了。XCode同时支持最流行的几大数据库分页算法，经过20多年的经验积累，根据数据库类型、版本以及所处环境，自动选择最佳的分页算法。  
当前很多其它ORM要么仅支持一两种数据库的分页算法，要么采用的分页算法具有极大的局限性，要求所设计的数据表必须具有某种特征。
* 先进的多级缓存思想，保证最出色的性能。  
XCode在最重要的数据管道主线上拥有三级缓存设计，在其它地方拥有无数的缓存设计。  
缓存的设计，让使用XCode开发的系统在性能上能够轻易超越采用原生数据库操作开发的系统，甚至有成千上万倍的差距！  
几乎所有的ORM都基于原生数据库操作，理论上会比原生数据库操作要慢一点，只有极少数使用了缓存的ORM会比原生数据库操作要快一些，但远没有XCode这么多的缓存设计。
* 化繁为简，容易上手。  
最流行的Hibernate、EF等ORM框架，追求完美的支持所有功能，庞大无比，让很多使用者望而却步。  
而XCode的核心思想是让一切变得简单，变得更简单！甚至连多表关联查询都不支持，而建议分为多次单表查询。也正因为化繁为简，使得XCode能够采用更多的缓存，化繁为简与缓存思想互相促进，甚至可以让多次单表查询远快于单次多表关联查询。
* 抽象实体结构，支持多数据库的正向反向工程。  
绝大多数ORM只负责对象与关系数据库的数据映射，而少有数据结构的映射。  
正向功能能够取得数据库结构，便于代码生成器使用；反向工程能够根据实体结构创建数据表、修改数据表结构、删除数据表或字段等，所以采用XCode开发的系统，发布的时候都可以不需要附带初始数据库。  
正向工程反向工程均是XCode提出的概念，在08年之前，除了XCode外没有任何其它ORM支持反向工程。也正是因为实体结构映射这一设计，使得XCode超越ORM，发展成为可以把实体对象映射到其它非数据库的形式。
* 分布式支持。  
尽管XCode采用了最好的分页算法，但对于大型系统甚至超级系统来说，单表数千万乃至数亿的数据是远远不能满足要求的。不管从数据存储还是从性能瓶颈的角度来考虑，分布式是必然趋势！  
XCode原生支持分布式设计。单表拆成多表，拆分到不同数据库、不同数据库服务器，XCode能够完全屏蔽数据层，使用起来就跟一张超级大表一样。其它绝大多数ORM不会对分布式实行原生支持，需要分布式数据库或者极其昂贵的硬件来配合才能做到。
* 总的来说，最好的分页算法保证基本操作不犯错，化繁为简和多级缓存的设计保证XCode在性能上永无止境，所以，XCode的最大优势就是性能！

## 教程

### 打开Visual Studio，新建一个控制台应用

### 使用NuGet安装到你的项目

PM> Install-Package NewLife.XCode

### 编写一个简单Xml模型（如果你已有现成的数据库，则可以使用XCoder.exe工具生成）
```xml
<?xml version="1.0" encoding="utf-8"?>
<Tables Version="9.6.6644.19804" Output="MyEntity" NameSpace="Model" BaseClass="Entity" ConnName="DB">
  <Table Name="UserInfo" Description="用户信息">
    <Columns>
      <Column Name="UserId" DataType="Int32" Identity="True" PrimaryKey="True" Description="会员id" />
      <Column Name="UserName" DataType="String" Nullable="False" Description="会员名称" />
      <Column Name="Password" DataType="String" Nullable="False" Description="会员密码" />
      <Column Name="Age" DataType="Int32" Description="会员年龄" />
    </Columns>
  </Table>
</Tables>
```
这是一个简单的XCode映射模型，跟hibernate原理相似，依靠这个模型，我们可以自动生成我们想要的数据库而不用写任何sql语句，所以也不用打开你的Sqlserver管理工具、navicat、mysqladminister等等
，这个xml会自动帮你生成，一切都来得如此简单快捷。  

### 生成模型文件
双击xml文件旁边的xcodetool.exe，这时你会看到生成一个MyEntity文件目录，目录包含了两个文件：用户信息.Biz.cs、用户信息.cs。  
作为一个orm组件，你完全不用关心里面代码的写法，我们只需会用就行了。

### 使用XCode实现简单的增删改查
* Program.cs文件最上面引用命名空间
```
using XCode;
```
不然会使用不了Save、Update、Delete等扩展方法。

* 新增一条数据。注：这里没有指定数据库，所以XCode默认的是sqlite数据库
```csharp
var user = new UserInfo();
user.UserName = "张三";
user.Password = "123456";
user.Age = 18;
user.Insert();//user.Save()等效

Console.WriteLine("插入一条新数据，用户id为：" + user.UserId);
```
* 修改一条数据
```csharp
var user = UserInfo.FindById(1);
user.UserName = "张三";
user.Password = "123456";
user.Age = 19;
user.Update();//user.Save()等效

user = UserInfo.FindById(1);
Console.WriteLine($"用户ID={user.UserId}已修改岁数，岁数为：{user.Age}");
```
* 删除一条数据
```csharp
var user = UserInfo.FindById(1);
user.Delete();

user = UserInfo.FindById(1);
Console.WriteLine("用户" + (user == null ? "已删除" : "还存在"));
```
* 查询数据，查询是一个十分复杂的需求，这里只举一些简单的例子，如果你想学更高级更复杂的查询，可以浏览页面底部的链接
```csharp
//根据UserId查询用户信息
var userId = 2;
var user = UserInfo.FindById(userId);
Console.WriteLine($"ID为{user.UserId}用户姓名为：{user.UserName}");

//根据用户名称查询
var userList = UserInfo.FindAll(UserInfo._.UserName == "张三");
Console.WriteLine($"查询符合姓名为张三的记录有{userList.Count}");

//多复合条件查询
userList = UserInfo.FindAll(UserInfo._.UserName == "张三" & UserInfo._.Age == 19);
Console.WriteLine($"查询符合姓名为张三年龄为19的记录有{userList.Count}");
```

## 一些高级的用法
* 更新（注：XCode是使用缓存，所以有些高级方法会绕过缓存直接更新数据库，这样会导致缓存数据与数据库的数据不一致。）
	更新某些字段。
```csharp
UserInfo.Update(UserInfo._.UserName == "李四" & UserInfo._.Age == 18, UserInfo._.UserId == 1);
// 相当于Update UserInfo Set UserName='李四' And Age=18 Where UserId=1
// 绕过了缓存，不推荐这种写法。建议先查出来对象，然后修改并Update回去
```

复杂的查询语句

```csharp
var userList = UserInfo.FindAll(UserInfo._.UserName == "张三" & UserInfo._.Age == 19, UserInfo._.UserName.Desc(), string.Join(",", UserInfo._.UserName, UserInfo._.Age), 0, 0);
// 相当于Select UserName,Age From UserInfo Where UserName='张三' And Age=19 Order By  UserName desc
```

带分页排序的复杂查询  
一般写在实体类业务文件（.Biz.cs）里面
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
以上的分页有缺点，就是只应用单字段排序

复合排序的分页查询语句

```csharp
var pageIndex = 2;//第二页
var pageSize = 10;//每页十行

var userList = UserInfo.FindAll(UserInfo._.UserName == "张三" & UserInfo._.Age == 19, " UserName desc,Age asc", string.Join(",", UserInfo._.UserName, UserInfo._.Age), (pageIndex - 1) * pageSize, pageSize);
//相当于Select UserName,Age From UserInfo Where UserName='张三' And Age=19 Order By  UserName desc,Age asc limit 10, 10
```

## 容器化部署
XCode 支持从环境变量中获取数据库连接字符串，用于容器化部署。  
连接字符串的环境变量命名规范为 `XCode_{ConnName}`，不区分大小写，连接字符串中需要使用provider指定数据库类型。  

## 新生命项目矩阵
各项目默认支持net7.0/netstandard2.1/netstandard2.0/net4.61/net4.5，旧版（2023.0308）支持net4.0/net2.0  

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

## 新生命开发团队
![XCode](https://newlifex.com/logo.png)  

新生命团队（NewLife）成立于2002年，是新时代物联网行业解决方案提供者，致力于提供软硬件应用方案咨询、系统架构规划与开发服务。  
团队主导的开源NewLife系列组件已被广泛应用于各行业，Nuget累计下载量高达60余万次。  
团队开发的大数据核心组件NewLife.XCode、蚂蚁调度计算平台AntJob、星尘分布式平台Stardust、缓存队列组件NewLife.Redis以及物联网平台NewLife.IoT，均成功应用于电力、高校、互联网、电信、交通、物流、工控、医疗、文博等行业，为客户提供了大量先进、可靠、安全、高质量、易扩展的产品和系统集成服务。  

我们将不断通过服务的持续改进，成为客户长期信赖的合作伙伴，通过不断的创新和发展，成为国内优秀的IT服务供应商。  

`新生命团队始于2002年，部分开源项目具有20年以上漫长历史，源码库保留有2010年以来所有修改记录`  
网站：https://newlifex.com  
开源：https://github.com/newlifex  
QQ群：1600800/1600838  
微信公众号：  
![智能大石头](https://newlifex.com/stone.jpg)  
