# NewLife.XCode 更新日志

## v11.27.2026.0601 (2026-06-01)

### 模型与反向工程
- **表级反向工程模式**：支持在模型中为表级配置 `Migration` 策略，`BindTableAttribute` 与 `EntityBuilder` 已联动支持，便于按实体粒度控制建模行为

### 查询与 SQL 体验
- **FieldExtension.Between 增强**：扩展区间查询表达能力，覆盖更多边界场景并补齐单元测试，提升条件构造可读性与正确性
- **SQL 预览能力增强**：支持 `DbCommand.ToString()` 直接输出可读 SQL，降低排障与调试成本

### 批量写入与稳定性优化
- **批量 SQL 生成优化**：统一并优化批量 `Update/Upsert` SQL 生成流程，修复 UPSERT 逗号拼接问题，提升跨库执行稳定性
- **批量累加字段升级**：批量累加支持快照差值与降级覆写，增强复杂业务场景适配能力
- **批量操作健壮性增强**：批量实体操作统一过滤空元素，避免无效数据干扰批处理链路
- **链路追踪改进**：优化批量更新与缓存清理埋点，增强诊断可观测性

### Bug 修复
- **[fix] MySql 参数数组兼容**：支持 Boolean 数组参数自动转换为 Int16 数组，修复特定驱动参数绑定异常

### 测试与依赖
- **测试覆盖完善**：新增并完善 `FieldExtension.Between` 等单元测试，提升回归保障
- **依赖更新**：升级基础组件并同步核心依赖版本

---

## v11.26.2026.0501 (2026-05-01)

### 功能增强
- **Model 模型规范升级至 202604 版本**：新增多款数据库支持，增加 `ItemType` 等字段类型枚举，Membership 相关表新增 html/markdown 字段类型
- **批量保存重构**：统一 `BatchSave` 方法的验证流程与批量能力判断逻辑，提升跨库批量操作的行为一致性
- **批量 Update 主键参与 Where 构建**：批量 Update 操作现强制主键参与构建 Where 子句，避免因缺少主键条件导致全表更新风险
- **AddQueue 链路追踪埋点**：为 `AddQueue` 操作增加 `Tracer` 埋点，提升异步队列写入的可观测性

### Bug 修复
- **[fix] 达梦 BatchInsert 时间字段丢失**：修复达梦数据库批量插入时 DateTime 字段值丢失问题
- **[fix] GetFieldType Nullable\<T\> 类型映射**：修复 `GetFieldType` 方法对 `Nullable<T>` 类型未拆箱导致映射失败的问题，使用 `Nullable.GetUnderlyingType` 正确处理可空值类型
- **[fix] KingBase GetTableNames 列名错误**：修复 `KingBaseMetaData.GetTableNames()` 使用错误的列名 `Name` 而非 `TableName` 导致获取表清单失败的问题
- **[fix] 默认租户编码**：将默认租户的 `Code` 从空值修正为 `Default`，避免多租户判断逻辑异常

### 依赖更新
- 升级依赖包并优化 NovaDb 相关实现

---

## v11.25.2026.0403 (2026-04-03)

### 新功能
- **远程数据库服务重构**：重构远程数据库服务链路，增强鉴权机制并补齐集成测试覆盖
- **批量能力体系升级**：引入 `BatchCapability` 枚举，重构批量操作能力模型，提升跨库批量行为表达力
- **租户拦截器增强**：新增并完善 `TenantInterceptor`，强化多租户场景下的数据隔离控制
- **建库字符集支持**：创建数据库时支持为 SqlServer、Oracle、PostgreSQL 指定默认字符集

### 重要修复
- **SqlServer 空表赋值异常修复**：修复 SqlServer 在表无数据时 `SetValue` 报错问题
- **SqlServer 字段映射修复**：修复 `varchar(-1)` 映射为 `varchar(max)`，并补充 `TimeSpan` 类型映射支持
- **KingBase 查询列引用修复**：修复 KingBase `SELECT` 列名双引号处理问题，避免特殊列名查询异常
- **脏数据字段匹配修复**：`EntityInterceptor.SetNoDirtyItem()` 支持按 `Name/ColumnName`（忽略大小写）匹配字段

### 性能与稳定性优化
- **SQLite 截断策略优化**：`SQLiteSession.Truncate` 改为 Drop+重建路径，提升大数据场景清理稳定性
- **数据库适配信息优化**：优化多数据库适配包与主库描述信息，提升驱动适配一致性

### 测试与文档
- **测试覆盖增强**：新增并增强 XCode 核心模块单元测试，补充 SQLite 全路径测试与远程服务相关测试
- **文档体系完善**：新增 XCode 核心能力与最佳实践文档，完善协作规范与数据层开发约定文档
- **构建指令打包修正**：修正 `NewLife.XCode.targets` 命名与项目引用，确保 Copilot 指令文件打包链路稳定

### 依赖更新
- 多次升级 NuGet 依赖包
- 更新 `NewLife.Core` 至 `11.13.2026.323-beta0701`

### 破坏性变更
无

---

## v11.25.2026.0227 (2026-02-27)

### 新功能
- **NovaDb 数据库支持**：新增对 NewLife 自研 NovaDb 数据库的适配，扩展国产自主数据库生态
- **InfluxDB 2.x 时序数据库适配**：新增 InfluxDB 数据库适配器与驱动基础设施，支持时序数据 CRUD 及单元测试覆盖
- **TDengine 改用 HTTP 驱动**：将 TDengine 驱动从原生驱动切换为 HTTP 协议，提升跨平台兼容性，更新默认端口与文档
- **TableItem 启动时全局配置**：支持在应用启动阶段全局永久修改实体的表名（TableName）和连接名（ConnName），适用于表前缀、分库、单租户等场景；属性变更同时自动同步到 DataTable，无需手动调用
- **Meta.Session 自动同步**：完善 `Entity<TEntity>.Meta.Session` 逻辑，自动检测并同步 ConnName / TableName 变化，确保 Session 始终指向最新连接和表名
- **CreateTable() 方法公开**：将 `internal static IDataTable CreateTable()` 改为 `public`，方便外部直接构造实体元数据

### 重大变更（破坏性）
- **移除模型表（外部 XML 映射）功能**：彻底移除通过外部 xml 文件动态映射实体表结构的支持，包括 `TableItem` 合并逻辑、`DAL.ModelTables` 属性、`XCodeSetting.ModelPath` 配置项及相关单元测试；实体表结构现仅以代码特性为准
- **移除 SQLite WAL 自动恢复与手动操作代码**：简化 `OpenConnection` 逻辑，删除 WAL 恢复和 `_checkpoint_` 手动干预，依赖 SQLite 自身自动处理，避免多进程场景下的干预风险

### 性能与代码优化
- **缓存清理精确化**：`ClearCache` 改用 `func.Method` 标识具体方法，避免因委托实例不同导致缓存未被正确清理
- **MySQL 驱动替换**：以 `NewLife.MySql` 替换 `MySql.Data`，统一 MySqlSslMode 类型兼容性处理

### 文档与工具
- **XCode 数据模型与结构同步文档**：新增完整的数据模型体系、正反向工程、类型映射、分表分库原理及与主流 ORM 对比说明
- **TableItem 启动配置文档**：新增详细说明启动配置与动态修改的区别、典型用法及注意事项
- **InfluxDB 支持说明文档**：新增 InfluxDB 接入与使用说明

### 依赖更新
- 升级 `NewLife.Core`、`NewLife.MySql` 等依赖包至最新版本

---

## v11.24.2026.0102 (2026-01-02)

### 新功能
- **数据权限体系**：引入多维度数据访问控制，支持多租户数据隔离与安全性
  - 支持租户级、部门级、用户级等多维度数据权限
  - 完善数据权限上下文及菜单级权限支持
  - 重构数据权限过滤逻辑与QueryAction枚举
  - 新增数据权限文档与单元测试覆盖
  - 完善租户相关单元测试
  - 只能操作本租户数据，提升数据隔离
- **实体拦截器重构**：统一实体模块为拦截器机制，提升扩展性
  - EntityBuilder支持XCode模块名称自动升级
  - 增强拦截器注释与结构，提升健壮性
  - 重构实体拦截器字段为EntityModules类型
  - 重构模块命名为拦截器并自动升级旧代码
- **模型文件版本管理**：支持模型文件版本号与自动升级机制
  - 检测旧版本文件并自动升级
  - 增强Version属性赋值处理
  - 支持ChineseFileName自动升级
- **会员功能扩展**：
  - Member表新增类型、数据范围等核心字段
  - 扩展Value字段长度至500
  - Department新增FindByCode查询方法
  - 会员核心表字段补全与类型规范，增强查询能力
- **配置增强**：
  - 优化DAL配置提供者绑定，支持ServiceProvider注入
  - 参数配置支持长文本，优化存储与读取逻辑
- **地区数据更新**：从PluginServer下载民政部最新地区数据
- **菜单数据范围**：使用枚举管理菜单数据范围

### 重要修复
- **[fix]** 修复数据库注释特殊字符导致的SQL错误
- **[fix]** 修复DateTime默认值SQL生成未加引号的问题，避免被误解析为数学表达式
- **[fix]** 修复数据库驱动加载失败时的处理，失败时删除后重新下载
- **[fix]** 修复批量插入实体列表时顺序不一致的问题（Valid内生成雪花Id需确保插入顺序）

### 性能优化
- 引入AsArray，借助数组优化多线程修改集合问题

### 代码质量改进
- **空引用安全**：
  - 支持可空引用类型，提升空值安全性和健壮性
  - DDL方法可空化，接口兼容性提升
  - 提升空引用安全与健壮性，优化类型与判空处理
  - 支持可空引用类型与集合初始化现代化
- **数据库兼容性**：
  - 增强MySQL SslMode兼容性，支持新版驱动枚举
  - 优化CI测试环境及数据库单元测试兼容性
  - 恢复SQLite文件时增加埋点
- **链路追踪**：增强数据库检查链路追踪与日志记录

### 文档与工具
- **协作指令**：重构XCode协作指令文档并新增使用手册
- **指令文件部署**：重构部署方式，移除props，新增targets
- **CI/CD**：删除中间临时文件，优化发布流程
- **XCodeTool**：注释掉ChineseFileName选项赋值

### 依赖更新
- 多次Upgrade Nuget包到最新版本

### 破坏性变更
无

---

## v11.23.2026.0102 (2026-01-02)

修正延迟队列更新的BUG；优化大量对象缓存的并发查询；梳理重构Merge逻辑



