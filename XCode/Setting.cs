﻿using System.ComponentModel;
using System.Runtime.CompilerServices;
using NewLife.Configuration;
using XCode.DataAccessLayer;

[assembly: InternalsVisibleTo("XUnitTest.XCode, PublicKey=00240000048000001401000006020000002400005253413100080000010001000d41eb3bdab5c2150958b46c95632b7e4dcb0af77ed8637bd8543875bc2443d01273143bb46655a48a92efa76251adc63ccca6d0e9cef2e0ce93e32b5043bea179a6c710981be4a71703a03e10960643f7df091f499cf60183ef0e4e4e2eebf26e25cea0eebf87c8a6d7f8130c283fc3f747cb90623f0aaa619825e3fcd82f267a0f4bfd26c9f2a6b5a62a6b180b4f6d1d091fce6bd60a9aa9aa5b815b833b44e0f2e58b28a354cb20f52f31bb3b3a7c54f515426537e41f9c20c07e51f9cab8abc311daac19a41bd473a51c7386f014edf1863901a5c29addc89da2f2659c9c1e95affd6997396b9680e317c493e974a813186da277ff9c1d1b30e33cb5a2f6")]

namespace XCode;

/// <summary>XCode设置</summary>
[Obsolete("=>XCodeSetting")]
public class Setting : XCodeSetting { }

/// <summary>XCode设置</summary>
[DisplayName("XCode设置")]
[Config("XCode")]
public class XCodeSetting : Config<XCodeSetting>
{
    #region 属性
    /// <summary>是否启用调试。默认启用</summary>
    [Description("调试")]
    public Boolean Debug { get; set; } = true;

    /// <summary>是否输出SQL语句，默认启用</summary>
    [Description("输出SQL。是否输出SQL语句，默认启用")]
    public Boolean ShowSQL { get; set; } = true;

    /// <summary>设置SQL输出的单独目录，默认为空，SQL输出到当前日志中。生产环境建议输出到站点外单独的SqlLog目录</summary>
    [Description("SQL目录。设置SQL输出的单独目录，默认为空，SQL输出到当前日志中。生产环境建议输出到站点外单独的SqlLog目录")]
    public String SQLPath { get; set; } = "";

    /// <summary>跟踪SQL执行时间，大于该阀值将输出日志，默认1000毫秒</summary>
    [Description("SQL执行时间。跟踪SQL执行时间，大于该阀值将输出日志，默认1000毫秒")]
    public Int32 TraceSQLTime { get; set; } = 1000;

    /// <summary>SQL最大长度，输出日志时的SQL最大长度，超长截断，默认4096，不截断用0</summary>
    [Description("SQL最大长度。输出日志时的SQL最大长度，超长截断，默认4096，不截断用0")]
    public Int32 SQLMaxLength { get; set; } = 4096;

    ///// <summary>连接名映射#，表名映射@，表名映射@，把实体类中的Test2和Test3连接名映射到Test去</summary>
    //[Description("连接映射。连接名映射#，表名映射@，把实体类中的Test2和Test3连接名映射到Test去")]
    //public String ConnMaps { get; set; } = "";

    /// <summary>参数化添删改查。默认关闭</summary>
    [Description("参数化添删改查。默认关闭")]
    public Boolean UseParameter { get; set; }

    ///// <summary>SQLite数据库默认目录。没有设置连接字符串的连接默认创建SQLite连接，数据库放在该目录</summary>
    //[Description("SQLite默认目录。没有设置连接字符串的连接默认创建SQLite连接，数据库放在该目录")]
    //public String SQLiteDbPath { get; set; } = "";

    ///// <summary>备份目录。备份数据库时存放的目录</summary>
    //[Description("备份目录。备份数据库时存放的目录")]
    //public String BackupPath { get; set; } = "";

    /// <summary>批大小。用于批量操作数据，抽取、删除、备份、恢复，默认5000</summary>
    [Description("批大小。用于批量操作数据，抽取、删除、备份、恢复，默认5000")]
    public Int32 BatchSize { get; set; } = 5_000;

    /// <summary>批操作间隙。用于批量删除数据时的暂停间隙，单位毫秒，默认100</summary>
    [Description("批操作间隙。用于批量删除数据时的暂停间隙，单位毫秒，默认100")]
    public Int32 BatchInterval { get; set; } = 100;

    /// <summary>命令超时。查询执行超时时间，默认0秒不限制</summary>
    [Description("命令超时。查询执行超时时间，默认0秒不限制")]
    public Int32 CommandTimeout { get; set; }

    /// <summary>失败重试。执行命令超时后的重试次数，默认0不重试</summary>
    [Description("失败重试。执行命令超时后的重试次数，默认0不重试")]
    public Int32 RetryOnFailure { get; set; }

    /// <summary>反向工程。Off 关闭；ReadOnly 只读不执行；On 打开，仅新建；Full 完全，修改删除</summary>
    [Description("反向工程。Off 关闭；ReadOnly 只读不执行；On 打开，仅新建；Full 完全，修改删除")]
    public Migration Migration { get; set; } = Migration.On;

    /// <summary>是否检查索引重复。默认打开</summary>  
    [Description("检查索引重复。默认打开")]
    public Boolean CheckDuplicateIndex { get; set; } = true;

    /// <summary></summary>
    [Description("表名称、字段名大小写格式。Default 默认(由数据库决定);None 与模型保持一致;Upper 全大写;Lower 全小写;Underline下划线")]
    public NameFormats NameFormat { get; set; } = NameFormats.Default;

    /// <summary>全表查行数下限。在获取数据表行数时，先根据索引表进行快速统计。如果快速统计的结果大于该值，则使用快速统计的结果。反之则进行 count(*) 操作获取精确统计。默认1000万</summary>
    [Description("全表查行数下限。在获取数据表行数时，先根据索引表进行快速统计。如果快速统计的结果大于该值，则使用快速统计的结果。反之则进行 count(*) 操作获取精确统计。调小该值可避免大表页面首次访问太慢，调大该值可获得精确行数。默认1000万")]
    public Int32 FullCountFloor { get; set; } = 10_000_000;

    /// <summary>模型目录。从该目录加载连接名指定的模型文件，替代实体类模型，按需配置修改实体类所映射的表名字段名</summary>
    [Description("模型目录。从该目录加载连接名指定的模型文件，替代实体类模型，按需配置修改实体类所映射的表名字段名")]
    public String ModelPath { get; set; } = "Models";
    #endregion

    #region 缓存
    /// <summary>数据层缓存。根据sql做缓存，默认0秒</summary>
    [Category("缓存")]
    [Description("数据层缓存。根据sql做缓存，默认0秒")]
    public Int32 DataCacheExpire { get; set; }

    /// <summary>实体缓存过期。整表缓存实体列表，默认10秒</summary>
    [Category("缓存")]
    [Description("实体缓存过期。整表缓存实体列表，默认10秒")]
    public Int32 EntityCacheExpire { get; set; } = 10;

    /// <summary>单对象缓存过期。按主键缓存实体，默认10秒</summary>
    [Category("缓存")]
    [Description("单对象缓存过期。按主键缓存实体，默认10秒")]
    public Int32 SingleCacheExpire { get; set; } = 10;

    /// <summary>扩展属性过期。扩展属性Extends缓存，默认10秒</summary>
    [Category("缓存")]
    [Description("扩展属性过期。扩展属性Extends缓存，默认10秒")]
    public Int32 ExtendExpire { get; set; } = 10;

    /// <summary>字段缓存过期。缓存表中分类型字段的分组数据，默认3600秒</summary>
    [Category("缓存")]
    [Description("字段缓存过期。缓存表中分类型字段的分组数据，默认3600秒")]
    public Int32 FieldCacheExpire { get; set; } = 3600;

    /// <summary>缓存统计周期。在日志中定期输出各个缓存的统计数据，用于分析性能问题，单位秒，0表示不输出，默认3600秒</summary>
    [Category("缓存")]
    [Description("缓存统计周期。在日志中定期输出各个缓存的统计数据，用于分析性能问题，单位秒，0表示不输出，默认3600秒")]
    public Int32 CacheStatPeriod { get; set; } = 3600;
    #endregion

    #region 方法
    ///// <summary>加载后检查默认值</summary>
    //protected override void OnLoaded()
    //{
    //    if (SQLiteDbPath.IsNullOrEmpty()) SQLiteDbPath = Runtime.IsWeb ? "..\\Data" : "Data";
    //    if (BackupPath.IsNullOrEmpty()) BackupPath = Runtime.IsWeb ? "..\\Backup" : "Backup";

    //    base.OnLoaded();
    //}
    #endregion
}