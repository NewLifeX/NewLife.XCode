XCode 是新生命团队专为 .NET 企业应用设计的高性能**数据中间件**，提供从 ORM、多级缓存、自动分表分库到内置权限体系的完整数据链路能力，适用于中小应用到百亿级大数据场景。

- 官网入口：[https://newlifex.com/xcode](https://newlifex.com/xcode)
- 功能总览：[XCode功能特点与最佳实践](https://newlifex.com/xcode/feature)

---

## 产品亮点

- **零手工建表**：反向工程根据实体特性自动建库建表、增量改列，告别人工 DDL 维护脚本
- **多级缓存加速**：实体缓存 / 对象缓存 / 字段缓存 / 数据层缓存四档递进，热点查询命中率可达 99%+，相比纯 DB 性能提升数倍至十倍
- **一行分表分库**：声明式 `ShardPolicy` 按时间或值自动路由，业务代码零修改，原生支持百亿级数据存取
- **高吞吐批量写入**：`EntityQueue` 将多线程写入自动合并批量落库，单机峰值 TPS 可达十万级
- **多数据库无缝切换**：同一套业务代码，MySQL / SQLite / SqlServer / Oracle / PostgreSQL / TDengine / 达梦 / 金仓 / 瀚高自由迁移，仅需修改连接字符串
- **内置 RBAC 权限体系**：用户、角色、菜单、行级数据权限、多租户隔离全覆盖，开箱即用，可与 NewLife.Cube 无缝集成
- **数据管道 ETL**：内置增量抽取器与跨库数据搬运（DAL_Backup/DbPackage）；ETL 执行引擎与 Sync 同步框架已移除（相关文档为历史参考）

```csharp
// 示例：一行配置开启按天分表
Meta.ShardPolicy = new TimeShardPolicy(nameof(Id), Meta.Factory)
{
    ConnPolicy  = "{0}",
    TablePolicy = "{0}_{1:yyyyMMdd}",
    Step        = TimeSpan.FromDays(1),
};
```

---

## 快速入门

- [XCode快速使用手册](https://newlifex.com/xcode/fast)
- [XCode使用手册（本地完整版）](/xcode/usage_manual)
- [增删改查入门](https://newlifex.com/xcode/curd)
- [数据模型与代码生成](https://newlifex.com/xcode/model)
- [代码生成器与XCodeTool](/xcode/xcode_tool)
- [连接字符串与功能设置](https://newlifex.com/xcode/setting)
- [连接字符串构造与脱敏处理（ConnectionStringBuilder）](/xcode/connection_string_builder)
- [类型与数组转换助手（ValidHelper）](/xcode/valid_helper)
- [主键空值判定与DataRow辅助（Helper）](/xcode/helper_null_key)

---

## 实体类与模型

- [实体类详解](https://newlifex.com/xcode/entity)
- [实体绑定特性（BindTable/BindColumn/BindIndex/Map）](/xcode/entity_attributes)
- [实体工厂（拦截处理实体操作）](https://newlifex.com/xcode/factory)
- [实体工厂初始化与建模（EntityFactory）](/xcode/entity_factory)
- [实体工厂完整接口（IEntityFactory）](/xcode/entity_factory_interface)
- [实体持久化接口与默认实现（IEntityPersistence）](/xcode/entity_persistence)
- [实体会话与初始化流程（EntitySession）](/xcode/entity_session)
- [实体扩展字典（EntityExtend）](/xcode/entity_extend)
- [树形实体（EntityTree）](/xcode/entity_tree)

---

## 增删改查

- [高级增删改](https://newlifex.com/xcode/curd_adv)
- [实体验证钩子（Valid）](/xcode/entity_valid)
- [脏数据（哪些字段将会Update到库中）](https://newlifex.com/xcode/dirty)
- [脏数据跟踪机制（DirtyCollection）](/xcode/dirty_collection)
- [数据初始化（安装后写入默认数据）](https://newlifex.com/xcode/initdata)
- [数据初始化（InitData）](/xcode/initdata_detail)
- [事务处理（算准你的每一分钱）](https://newlifex.com/xcode/transaction)
- [实体事务（EntityTransaction）](/xcode/entity_transaction)
- [增量累加（免加锁多用户更新数据记录）](https://newlifex.com/xcode/additional)
- [增量累加字段（AdditionalFields）](/xcode/additional_fields)
- [实体拦截器（IEntityInterceptor）](/xcode/entity_interceptor)
- [内置实体拦截器（Time/User/IP/Trace）](/xcode/builtin_interceptors)
- [导入导出（实体对象百变魔君）](https://newlifex.com/xcode/import_export)
- [数据合并场景与应用（数据合并）](/xcode/data_merge)

---

## 数据查询

- [通用数据查询](https://newlifex.com/xcode/find)
- [高级查询与分页](https://newlifex.com/xcode/search)
- [扩展属性（替代多表关联Join提升性能）](https://newlifex.com/xcode/extend)
- [查询条件表达式（WhereExpression）](/xcode/where_expression)
- [排序与分页参数构造（SqlBuilder）](/xcode/sql_builder)
- [字段时间区间扩展（FieldExtension）](/xcode/field_extension)
- [批量主键查找器（BatchFinder）](/xcode/batch_finder)

---

## 多级缓存

- [数据层缓存（网站性能翻10倍）](https://newlifex.com/xcode/dbcache)
- [实体列表缓存（最土的办法实现百万级性能）](https://newlifex.com/xcode/entitycache)
- [对象字典缓存（百万军中取敌首级）](https://newlifex.com/xcode/singlecache)
- [缓存架构总览](/xcode/cache_overview)
- [实体缓存（EntityCache）](/xcode/entity_cache)
- [单对象缓存（SingleCache）](/xcode/single_cache)
- [字段缓存（FieldCache）](/xcode/field_cache)
- [数据层缓存（DbCache）](/xcode/db_cache)
- [缓存基类与惰性消费者（CacheBase/LazyConsumer）](/xcode/cache_base_lazy_consumer)
- [缓存预热与失效策略](/xcode/cache_warmup_invalidation)

---

## 高性能批量操作

- [批量添删改操作（提升吞吐率）](https://newlifex.com/xcode/batch)
- [集合批量操作（EntityExtension）](/xcode/entity_extension_batch)
- [实体队列（多线程生产的大数据集中保存）](https://newlifex.com/xcode/queue)
- [实体队列（EntityQueue）](/xcode/entity_queue)
- [读写分离（查询性能无限扩展）](https://newlifex.com/xcode/readwrite)
- [读写分离策略（ReadWriteStrategy）](/xcode/read_write_strategy)
- [百亿级性能（索引的威力）](https://newlifex.com/xcode/100billion)
- [数据模拟压测（DataSimulation）](/xcode/data_simulation)

---

## 分表分库

- [分表分库（百亿级大数据存储）](https://newlifex.com/xcode/division)
- [大数据分析](https://newlifex.com/xcode/bigdata)
- [大数据表设计与 DataScale 属性规范](/xcode/bigdata_design)
- [Shards分配策略与路由](/xcode/shards_routing)
- [Shards跨分片查询与AutoShard](/xcode/shards_auto_shard)
- [时间分片策略与范围裁剪（TimeShardPolicy）](/xcode/time_shard_policy)
- [分片路由模型（ShardModel）](/xcode/shard_model)
- [分库分表临时切换（EntitySplit）](/xcode/entity_split)

---

## ETL 与数据同步

- [ETL数据抽取转换](/xcode/etl_transform)（已移除）
- [ETL执行内核与并发调度（ETL）](/xcode/etl_engine)（已移除）
- [抽取配置与运行上下文（ExtractSetting_DataContext）](/xcode/extract_setting_data_context)（部分过时）
- [ETL模块管道与生命周期（IETLModule）](/xcode/etl_module)（已移除）
- [ETL统计指标与观测模型（IETLStat）](/xcode/etl_stat)（已移除）
- [抽取器族谱与选型（Extracter）](/xcode/extracter_family)（部分过时）
- [数据包并行管道与抽取策略（DbPackage）](/xcode/db_package)
- [备份恢复与同步（数据搬运专家）](https://newlifex.com/xcode/backup)
- [数据备份恢复与跨库同步（DAL_Backup）](/xcode/dal_backup_sync)
- [数据同步（Sync）](/xcode/data_sync)（已移除）
- [增量同步框架（Transform.Sync）](/xcode/transform_sync)（已移除）

---

## 统计报表

- [高级统计（数据报表利器）](https://newlifex.com/xcode/stat)
- [统计模型与聚合扩展实践](/xcode/statistics_model)（示例模型已移除）
- [统计运营报表实战（VisitStat）](/xcode/visit_stat_practice)（示例实体已移除）
- [统计报表实战（二）：同比环比与异常告警](/xcode/statistics_yoy_mom_alert)

---

## 元数据与反向工程

- [反向工程（自动建表建库大杀器）](https://newlifex.com/xcode/negative)
- [元数据管理总览](/xcode/metadata_overview)
- [表元数据（TableItem）](/xcode/table_item)
- [TableItem全局启动配置（连接名/表名/Migration 永久修改）](/xcode/table_item_startup)
- [字段元数据（FieldItem与ShowIn）](/xcode/field_item_showin)
- [XCode数据模型与结构同步（IDataTable正/反向工程）](/xcode/data_model_sync)

---

## 数据库驱动层（DAL）

- [DAL与SQL构建器](/xcode/dal_sql_builders)
- [SQL查询与插入构建器（SelectBuilder/InsertBuilder）](/xcode/select_insert_builder)
- [通用映射查询与对象写入（DAL_Mapper）](/xcode/dal_mapper)
- [跨数据库SQL模板（SqlTemplate）](/xcode/sql_template)
- [DAL执行管道、缓存与读写分离（DAL_DbOperate）](/xcode/dal_db_operate)
- [DAL调试开关与批处理参数（DAL_Setting）](/xcode/dal_setting)
- [数据库驱动抽象（IDatabase/DbBase）](/xcode/idatabase_dbbase)
- [数据库工厂与提供者注册（DbFactory）](/xcode/db_factory)
- [数据库连接池（ConnectionPool）](/xcode/connection_pool)
- [数据库会话接口（IDbSession）](/xcode/idb_session)
- [数据库连接元信息模型（DbInfo）](/xcode/db_info)
- [驱动自动加载与下载（DriverLoader）](/xcode/driver_loader)
- [SQL Server分页算法（MSPageSplit）](/xcode/ms_page_split)
- [键控锁与并发分段（KeyedLocker）](/xcode/keyed_locker)

---

## 企业级权限体系

- [用户角色权限](https://newlifex.com/xcode/membership)
- [Membership权限体系实体指南](/xcode/membership_guide)
- [用户提供者接口（IManageProvider）](/xcode/manage_provider)
- [Membership权限管理模块（RBAC/多租户）](/xcode/membership_rbac)
- [数据权限与行级过滤（DataScope）](/xcode/data_scope)
- [业务审计日志（LogProvider/LogEntity）](/xcode/log_provider)

---

## 扩展功能与特殊数据库

- [数据库HTTP服务（DbService）](/xcode/db_service)
- [网络虚拟数据库（NetworkDb）](/xcode/network_db)
- [配置中心（DbConfigProvider）](/xcode/db_config_provider)
- [Web行为模块与运行时监测](/xcode/web_runtime_monitor)（已移除）
- [ASP.NET Core行为监测迁移指南](/xcode/aspnetcore_runtime_migration)（部分过时）
- [TDengine时序数据库支持（TDengine）](/xcode/tdengine)
- [InfluxDB时序数据库支持说明](/xcode/influxdb)
- [MySql客户端驱动NewLife.MySql](https://newlifex.com/xcode/mysql)

---

## 经验与参考

- [XCode无实体模型使用案例](https://newlifex.com/xcode/no_entity)
- [上千列的医疗CSV文件如何导入数据库](https://newlifex.com/xcode/import_csv)
- [Sqlite数据库线上收缩急救](https://newlifex.com/xcode/sqlite_shrink)
- [MySql中TinyInt(1)读取数据不正确](https://newlifex.com/xcode/tinyint_in_mysql)
- [常见问题FAQ](https://newlifex.com/xcode/faq)
