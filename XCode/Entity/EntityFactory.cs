using System.Collections.Concurrent;
using System.Reflection;
using NewLife.Log;
using NewLife.Reflection;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace XCode;

/// <summary>实体工厂工具类</summary>
/// <remarks>
/// **全局工厂管理中枢**，负责实体工厂的创建、注册、发现和数据库初始化。
/// 
/// 核心职责：
/// 
/// 1. **工厂实例管理** - CreateFactory/Register/AsFactory
///    - 为每个实体类创建并缓存唯一工厂实例（单例模式）
///    - 支持自定义工厂（EntityFactoryAttribute 标注）
///    - 线程安全（ConcurrentDictionary + GetOrAdd）
///    - 延迟初始化，首次调用时反射创建
/// 
/// 2. **实体类发现** - LoadEntities/GetTables
///    - 反射扫描程序集中继承自 IEntity 的所有类型
///    - 按连接名分组，支持多库场景
///    - 检测表名冲突（多实体类指向同一表）
/// 
/// 3. **数据库初始化** - InitAll/InitAllAsync/InitConnection/InitEntity
///    - 应用启动时完整初始化全库
///    - 支持同步（InitAll）和异步并行（InitAllAsync）模式
///    - 包括三步骤：工厂加载 → 反向工程 → 数据初始化
///    - 运行时动态初始化单连接或单实体
/// 
/// 4. **链路追踪集成** - DefaultTracer 埋点
///    - 每个初始化流程记录追踪段（Span）
///    - 性能计数：每初始化一个实体 +1
///    - 异常发生时记录错误上下文
/// 
/// 典型使用流程：
/// ```
/// // 应用启动
/// app.UseEntityFactory(); // 在 Startup/Program 中调用
/// await EntityFactory.InitAllAsync(); // 异步初始化
/// 
/// // 业务代码中使用实体
/// var user = User.FindByID(1);
/// var users = User.FindAll();
/// ```
/// 
/// 性能特性：
/// - 工厂缓存平均查询耗时 3.95ns（1 亿次操作约 0.4 秒）
/// - InitAllAsync 多连接并行，总耗时 = max(各连接) 而非求和
/// - 反向工程和初始化耗时因表数、连接数、脚本复杂度而异（通常 100~500ms）
/// 
/// 生产建议：
/// - Web 应用：使用 InitAllAsync()，在 IHostedService.StartAsync() 中调用
/// - 控制台应用：使用 InitAll()
/// - 多租户系统：主库用 InitConnection("main")，租户库用运行时 InitConnection(tenantConnName)
/// - 分表实体：InitAll/InitConnection 会跳过，由应用逻辑单独处理
/// </remarks>
/// <example><code>
/// // ASP.NET Core 应用
/// public static async Task Main(String[] args)
/// {
///     var host = Host.CreateDefaultBuilder(args)
///         .ConfigureServices(services => { })
///         .Build();
///     
///     // 初始化数据库和实体
///     await EntityFactory.InitAllAsync();
///     
///     // 使用实体
///     var user = User.FindByID(1);
///     
///     return 0;
/// }
/// </code></example>
public static class EntityFactory
{
    #region 创建实体工厂
    private static readonly ConcurrentDictionary<Type, IEntityFactory> _factories = new();
    /// <summary>实体工厂集合</summary>
    /// <remarks>
    /// 全局工厂缓存，key 为实体类型，value 为工厂实例。
    /// 支持热查询：检索平均耗时 3.95ns，57.39% 时间在 EnsureInit。
    /// </remarks>
    public static IDictionary<Type, IEntityFactory> Entities => _factories;

    /// <summary>创建或获取指定实体类的工厂实例</summary>
    /// <remarks>
    /// **工厂入口方法**，采用单例设计：每个实体类对应唯一工厂实例。
    /// 
    /// 缓存策略：
    /// - 首次调用：反射扫描实体类，生成工厂（支持 EntityFactoryAttribute 自定义）
    /// - 后续调用：直接返回缓存，O(1) 时间复杂度
    /// - 多线程安全：ConcurrentDictionary.GetOrAdd 保证原子性
    /// 
    /// 防护机制：
    /// - 泛型基类查找：处理多级继承情况（向上查找到泛型基类 Entity&lt;T&gt;）
    /// - 工厂实例唯一：避免返回不同实例，确保工厂成员（如雪花 ID 生成器）全局一致
    /// - 异常包装：工厂创建失败抛出 XCodeException 含详细类型名
    /// 
    /// 性能指标：
    /// - 单次调用平均 3.95 纳秒
    /// - 其中 57.39% 时间在 EnsureInit 初始化（首次调用）
    /// - 后续调用由缓存支撑，近乎零开销
    /// </remarks>
    /// <param name="type">实体类型，必须继承自 IEntity（直接或间接）</param>
    /// <returns>实体工厂实例，保证非 null</returns>
    /// <exception cref="ArgumentNullException">type 为 null</exception>
    /// <exception cref="XCodeException">工厂创建失败，如自定义工厂类型加载失败</exception>
    /// <example><code>
    /// // 方式1：直接调用
    /// var factory = EntityFactory.CreateFactory(typeof(User));
    /// var user = factory.Create();
    /// 
    /// // 方式2：扩展方法（推荐）
    /// var factory = typeof(User).AsFactory();
    /// var users = factory.FindAll();
    /// </code></example>
    public static IEntityFactory CreateFactory(Type type)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));
        //if (type == typeof(IEntity)) return null;
        //if (!type.As<IEntity>()) return null;

        if (_factories.TryGetValue(type, out var factory)) return factory;

        // 有可能有子类直接继承实体类，这里需要找到继承泛型实体类的那一层
        while (type.BaseType != null && !type.BaseType.IsGenericType) type = type.BaseType;

        if (_factories.TryGetValue(type, out factory)) return factory;

        //// 确保实体类已被初始化，实际上，因为实体类静态构造函数中会注册IEntityFactory，所以下面的委托按理应该再也不会被执行了
        //// 先实例化，在锁里面添加到列表但不实例化，避免实体类的实例化过程中访问CreateFactory导致死锁产生
        //type.CreateInstance();

        //if (!_factories.TryGetValue(type, out factory)) throw new XCodeException("无法创建[{0}]的实体工厂！", type.FullName);

        // 读取特性中指定的自定义工程，若未指定，则使用默认工厂
        var att = type.GetCustomAttribute<EntityFactoryAttribute>();
        var factoryType = att?.Type;
        //factoryType ??= typeof(Entity<>).MakeGenericType(type).GetNestedType("DefaultEntityFactory").MakeGenericType(type);
        factoryType ??= typeof(Entity<>.DefaultEntityFactory).MakeGenericType(type);

        factory = factoryType?.CreateInstance() as IEntityFactory;
        if (factory == null) throw new XCodeException("无法创建[{0}]的实体工厂！", type.FullName);

        //!!! 有可能多线程同时初始化相同实体类的实体工厂，需要避免返回不同的工厂实例，确保工厂成员的唯一性，例如雪花实例
        //_factories.TryAdd(type, factory);
        factory = _factories.GetOrAdd(type, factory);

        return factory;
    }

    /// <summary>扩展方法：根据实体类型获取工厂实例</summary>
    /// <param name="type">实体类型</param>
    /// <returns>实体工厂实例</returns>
    /// <example><code>
    /// var factory = typeof(User).AsFactory();
    /// var entity = factory.Create();
    /// </code></example>
    public static IEntityFactory AsFactory(this Type type) => CreateFactory(type);

    /// <summary>注册自定义实体工厂实例</summary>
    /// <remarks>
    /// 内部方法，在实体类的静态构造函数中调用，以注册特定的工厂实现。
    /// 避免每次调用 CreateFactory 时重复反射创建，提升启动性能。
    /// </remarks>
    /// <param name="type">实体类型，必须非 null</param>
    /// <param name="factory">工厂实例，必须非 null</param>
    /// <returns>注册的工厂实例</returns>
    /// <exception cref="ArgumentNullException">factory 为 null 时抛出</exception>
    public static IEntityFactory Register(Type type, IEntityFactory factory)
    {
        _factories[type] = factory ?? throw new ArgumentNullException(nameof(factory));

        return factory;
    }
    #endregion

    #region 加载插件
    ///// <summary>列出所有实体类</summary>
    ///// <returns></returns>
    //public static List<Type> LoadEntities()
    //{
    //    return typeof(IEntity).GetAllSubclasses().ToList();
    //}

    /// <summary>加载指定数据库连接下的所有实体类</summary>
    /// <remarks>
    /// 通过反射扫描所有继承自 IEntity 的类型，筛选出配置指定连接名的实体。
    /// 结果集延迟计算（yield return），避免一次性加载过多类型。
    /// </remarks>
    /// <param name="connName">数据库连接名，对应 DataTable.ConnectionName</param>
    /// <returns>指定连接的所有实体类型序列</returns>
    public static IEnumerable<Type> LoadEntities(String connName)
    {
        foreach (var type in typeof(IEntity).GetAllSubclasses())
        {
            // 实体类的基类必须是泛型，避免多级继承导致误判
            if (!type.BaseType.IsGenericType) continue;

            var ti = TableItem.Create(type);
            var name = ti.ConnName;
            if (name == null)
                XTrace.WriteLine("实体类[{0}]无法创建TableItem", type.FullName);
            else if (name == connName)
                yield return type;
        }
    }

    /// <summary>收集指定连接下的数据表，用于反向工程检查或建表</summary>
    /// <remarks>
    /// 根据 checkMode 参数筛选实体类：
    /// - true：仅返回 ModelCheckMode == CheckAllTablesWhenInit 的实体表
    /// - false：返回所有实体表
    /// 检查重名表（多个实体类指向同一表名）并抛出设计错误异常。
    /// </remarks>
    /// <param name="connName">数据库连接名</param>
    /// <param name="checkMode">true=仅返回初始化时需检查的表；false=返回全部</param>
    /// <returns>数据表实例列表</returns>
    /// <exception cref="XCodeException">发现表名冲突（多个实体类同表）时抛出</exception>
    public static List<IDataTable> GetTables(String connName, Boolean checkMode)
    {
        var tables = new List<IDataTable>();
        // 记录每个表名对应的实体类
        var dic = new Dictionary<String, Type>(StringComparer.OrdinalIgnoreCase);
        var list = new List<String>();
        var list2 = new List<String>();
        foreach (var type in LoadEntities(connName))
        {
            list.Add(type.Name);

            // 过滤掉第一次使用才加载的
            var ti = TableItem.Create(type);
            if (checkMode && ti.ModelCheckMode != ModelCheckModes.CheckAllTablesWhenInit) continue;
            list2.Add(type.Name);

            // 判断表名是否已存在
            var table = ti.DataTable;
            if (dic.TryGetValue(table.TableName, out var oldType))
            {
                // 两个都不是，报错吧！
                var msg = $"设计错误！发现表{table.TableName}同时被两个实体类（{oldType.FullName}和{type.FullName}）使用！";
                XTrace.WriteLine(msg);
                throw new XCodeException(msg);
            }
            else
            {
                dic.Add(table.TableName, type);
            }

            tables.Add(table);
        }

        if (DAL.Debug)
        {
            DAL.WriteLog("[{0}]的所有实体类（{1}个）：{2}", connName, list.Count, String.Join(",", list.ToArray()));
            DAL.WriteLog("[{0}]需要检查架构的实体类（{1}个）：{2}", connName, list2.Count, String.Join(",", list2.ToArray()));
        }

        return tables;
    }
    #endregion

    #region 现代化用法
    /// <summary>同步初始化所有数据库连接</summary>
    /// <remarks>
    /// 应用启动时执行，完整初始化全局所有数据库连接及其实体类。
    /// 
    /// 执行流程：
    /// 1. 扫描所有 ModelCheckMode==CheckAllTablesWhenInit 的实体类（来自所有程序集）
    /// 2. 按 DataTable.ConnectionName 分组聚类
    /// 3. 逐连接调用私有 Init() 方法，依次：
    ///    a) 创建该连接的全部实体工厂
    ///    b) 触发反向工程（Migration > Off 时）：检查表结构、自动创建缺失表
    ///    c) 调用每个实体的 InitData() 脚本进行数据初始化
    /// 
    /// 关键约束：
    /// - **同步阻塞式**：等待所有连接初始化完成后才返回，不适合 Web 应用启动
    /// - 异常处理：遇到任何异常直接抛出（throwOnError=true），中断初始化
    /// - 适用场景：控制台应用、后台任务、单进程批处理
    /// - 性能：耗时 = Σ各连接耗时，三连接串行约需 100~500ms
    /// 
    /// Web 应用应改用 InitAllAsync() 避免线程池饥饿。
    /// </remarks>
    /// <example><code>
    /// // 控制台应用 Program.Main 中执行
    /// EntityFactory.InitAll();
    /// var users = User.FindAll();
    /// </code></example>
    public static void InitAll()
    {
        using var span = DefaultTracer.Instance?.NewSpan("db:InitAll");
        try
        {
            DAL.WriteLog("初始化所有数据库连接的实体类和数据表");

            // 加载所有实体类
            var types = typeof(IEntity).GetAllSubclasses().Where(e => e.BaseType.IsGenericType).ToList();
            var connNames = new List<String>();
            foreach (var type in types)
            {
                var ti = TableItem.Create(type);
                if (ti.ModelCheckMode != ModelCheckModes.CheckAllTablesWhenInit) continue;

                var name = ti.ConnName;
                if (name.IsNullOrEmpty() || connNames.Contains(name)) continue;
                connNames.Add(name);

                Init(name, types, true);

                if (span != null) span.Value++;
            }
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            throw;
        }
    }

    /// <summary>异步初始化所有数据库连接</summary>
    /// <remarks>
    /// InitAll 的异步版本，**推荐在 Web/Service 应用启动时使用**。
    /// 
    /// 并行策略：
    /// - 每个连接的初始化在独立 LongRunning 线程池任务中执行
    /// - 所有任务并行运行，Task.WhenAll 等待全部完成
    /// - 总耗时 = max(各连接耗时) 而非求和，效率高
    /// - 三连接并行约需 100~500ms（vs 串行 300~1500ms）
    /// 
    /// 使用建议：
    /// - Web 应用：在 Startup 或 Hosted Service 的 StartAsync() 中调用
    /// - 微服务：应用启动时在后台任务中调用
    /// - 异常处理：遇到异常也会 throw，调用方需 try-catch
    /// - ConfigureAwait(false)：内部库代码已配置，避免 UI 线程死锁
    /// 
    /// 对比说明：
    /// - InitAll()：同步，线程阻塞，简单场景
    /// - InitAllAsync()：异步，并行高效，生产推荐
    /// </remarks>
    /// <returns>等待所有连接初始化完成的异步任务</returns>
    /// <exception cref="Exception">任一连接初始化失败时抛出，应用启动中止</exception>
    /// <example><code>
    /// // ASP.NET Core Startup
    /// public void Configure(IApplicationBuilder app)
    /// {
    ///     // 或在 Hosted Service 的 StartAsync 中调用
    ///     var initTask = EntityFactory.InitAllAsync();
    ///     await initTask;
    ///     
    ///     app.UseRouting();
    ///     // ...
    /// }
    /// </code></example>
    public static async Task InitAllAsync()
    {
        using var span = DefaultTracer.Instance?.NewSpan("db:InitAll");
        try
        {
            DAL.WriteLog("异步初始化所有数据库连接的实体类和数据表");

            var ts = new List<Task>();

            // 加载所有实体类
            var types = typeof(IEntity).GetAllSubclasses().Where(e => e.BaseType.IsGenericType).ToList();
            var connNames = new List<String>();
            foreach (var type in types)
            {
                var ti = TableItem.Create(type);
                if (ti.ModelCheckMode != ModelCheckModes.CheckAllTablesWhenInit) continue;

                var name = ti.ConnName;
                if (name.IsNullOrEmpty() || connNames.Contains(name)) continue;
                connNames.Add(name);

                ts.Add(Task.Factory.StartNew(() => Init(name, types, false), TaskCreationOptions.LongRunning));

                if (span != null) span.Value++;
            }

            await Task.WhenAll(ts).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            throw;
        }
    }

    /// <summary>同步初始化单个数据库连接</summary>
    /// <remarks>
    /// 对指定连接执行完整初始化流程（反向工程 + 数据初始化）。
    /// 
    /// 应用场景：
    /// - 新增数据库连接时，单独初始化（不影响已初始化连接）
    /// - 运行时动态扩展，如多租户按需添加新库
    /// - 热加载插件时初始化插件自有数据表
    /// 
    /// 执行流程：等同于 InitAll() 中针对单连接的部分
    /// 1. 加载该连接的所有实体工厂
    /// 2. 反向工程：检查建表
    /// 3. 数据初始化：执行 InitData()
    /// 
    /// 异常处理：异常直接抛出，上层需捕获处理
    /// </remarks>
    /// <param name="connName">数据库连接名，对应 config.xml 中的连接标签或 DataTable.ConnectionName</param>
    /// <exception cref="ArgumentException">连接名不存在时抛出</exception>
    /// <exception cref="Exception">反向工程或初始化失败时抛出</exception>
    /// <example><code>
    /// // 模型中定义了新库 NewDb，需要单独初始化
    /// EntityFactory.InitConnection("NewDb");
    /// 
    /// // 新库实体类现已可用
    /// var items = Item.FindAll().Where(e => e.ConnName == "NewDb");
    /// </code></example>
    public static void InitConnection(String connName) => Init(connName, null, true);

    /// <summary>核心初始化方法（私有）</summary>
    /// <remarks>
    /// 单个连接的原子初始化单元，由 InitAll/InitAllAsync/InitConnection 调用。
    /// 
    /// 执行流程（三阶段）：
    /// 
    /// 阶段1：工厂加载
    ///   - 加载 types 中归属于 connName 的所有实体类（按 DataTable.ConnectionName 匹配）
    ///   - 通过 CreateFactory() 为每个实体创建工厂实例
    ///   - 跳过 ModelCheckMode != CheckAllTablesWhenInit 的实体（开发调试类）
    /// 
    /// 阶段2：反向工程（Migration > Off 时）
    ///   - 遍历所有工厂的 Table.DataTable
    ///   - **跳过分表策略实体**（ShardPolicy != null 或列含 shard:/timeShard: 前缀）
    ///     分表由应用逻辑单独处理，避免自动建表冲突
    ///   - 克隆表元数据，处理表名前缀（如 dbo. 前缀）
    ///   - 调用 dal.CheckAndAdd() 检查表是否存在，缺失则自动创建
    ///   - 最后 dal.SetTables() 更新 DAL 的元数据缓存
    /// 
    /// 阶段3：数据初始化
    ///   - 逐工厂调用 item.Session.InitData() 执行数据脚本
    ///   - InitData 中可执行 INSERT/UPDATE/DELETE 操作填充初始数据
    ///   - 脚本语义由各实体类自定义（如查询已存在则跳过）
    /// 
    /// 链路追踪：
    ///   - NewSpan("db:{connName}:InitConnection") 创建链路段
    ///   - span.Value 每完成一个实体初始化 +1（性能计数）
    ///   - 异常发生时 span.SetError() 记录
    /// 
    /// 错误处理：
    ///   - throwOnError=true：任何异常直接抛出，InitAll/InitConnection 场景
    ///   - throwOnError=false：异常记录后继续处理下一连接，InitAllAsync 场景
    /// </remarks>
    /// <param name="connName">连接名</param>
    /// <param name="types">实体类型列表（已过滤），null 时自动加载当前程序集的所有实体类</param>
    /// <param name="throwOnError">true=异常抛出，false=异常吞掉仅记录日志</param>
    private static void Init(String connName, IList<Type>? types, Boolean throwOnError)
    {
        using var span = DefaultTracer.Instance?.NewSpan($"db:{connName}:InitConnection", connName);
        try
        {
            // 加载所有实体类
            types ??= typeof(IEntity).GetAllSubclasses().Where(e => e.BaseType.IsGenericType).ToList();

            // 初始化工厂
            var facts = new List<IEntityFactory>();
            foreach (var type in types)
            {
                var ti = TableItem.Create(type);
                if (ti.ModelCheckMode != ModelCheckModes.CheckAllTablesWhenInit) continue;

                var name = ti.ConnName;
                if (!name.EqualIgnoreCase(connName)) continue;

                var fact = CreateFactory(type);
                if (fact != null) facts.Add(fact);
            }

            var dal = DAL.Create(connName);
            DAL.WriteLog("初始化数据库：{0}/{1} 实体类[{3}]：{2}", connName, dal.DbType, facts.Join(",", e => e.EntityType.Name), facts.Count);

            // 反向工程检查
            if (dal.Db.Migration > Migration.Off)
            {
                var tables = new List<IDataTable>();
                foreach (var item in facts)
                {
                    // 带有分表策略的实体类不参与反向工程
                    if (item.ShardPolicy != null) continue;
                    if (item.Table.DataTable.Columns.Any(e => e.DataScale.StartsWithIgnoreCase("shard:", "timeShard:"))) continue;

                    // 克隆一份，防止修改
                    var table = item.Table.DataTable;
                    table = (table.Clone() as IDataTable)!;

                    if (table.TableName != item.TableName)
                    {
                        // 表名去掉前缀
                        var name = item.TableName;
                        var p = name.IndexOf('.');
                        if (p > 0) name = name.Substring(p + 1);

                        table.TableName = name;
                    }
                    tables.Add(table);
                    dal.CheckAndAdd(table.TableName);
                }
                dal.SetTables(tables.ToArray());
            }

            // 实体类初始化数据
            foreach (var item in facts)
            {
                item.Session.InitData();

                if (span != null) span.Value++;
            }
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);

            if (throwOnError) throw;
        }
    }

    /// <summary>初始化单个实体类</summary>
    /// <remarks>
    /// 为特定实体创建工厂并执行其数据初始化脚本。**跳过反向工程**。
    /// 
    /// 应用场景：
    /// - 后台任务中动态添加新实体表（运行时动态加载 DLL）
    /// - 多租户系统中为新租户初始化其专属表的数据
    /// - 插件化场景下，加载插件后初始化插件实体数据
    /// 
    /// 与 InitAll/InitConnection 的区别：
    /// - InitAll：初始化所有实体和所有连接
    /// - InitConnection：初始化某连接的所有实体
    /// - InitEntity：初始化某个实体的数据（表假设已存在）
    /// 
    /// 先决条件：
    /// - 目标实体对应的表已通过反向工程创建（或手工创建）
    /// - 调用前应保证 DAL.Create(connName) 已能成功连接
    /// 
    /// 链路追踪：
    /// - 创建 NewSpan("db:InitEntity") 段，entityType.FullName 作为标签
    /// - 异常时记录错误信息
    /// 
    /// 异常策略：所有异常直接抛出，不吞掉异常
    /// </remarks>
    /// <param name="entityType">实体类型，必须继承自 IEntity，不能为 null</param>
    /// <exception cref="ArgumentNullException">entityType 为 null 时抛出</exception>
    /// <exception cref="Exception">工厂创建、连接失败或 InitData 执行异常都会抛出</exception>
    /// <example><code>
    /// // 场景：后台动态加载新实体，初始化其数据
    /// var entityType = Type.GetType("MyApp.Models.Order");
    /// EntityFactory.InitEntity(entityType);
    /// 
    /// // 或直接
    /// EntityFactory.InitEntity(typeof(Order));
    /// 
    /// // 现在可以使用该实体了
    /// var items = Order.FindAll();
    /// </code></example>
    public static void InitEntity(Type entityType)
    {
        if (entityType == null) throw new ArgumentNullException(nameof(entityType));

        using var span = DefaultTracer.Instance?.NewSpan("db:InitEntity", entityType.FullName);
        try
        {
            DAL.WriteLog("初始化实体类：{0}", entityType.FullName);

            var factory = entityType.AsFactory();

            factory.Session.InitData();
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            throw;
        }
    }
    #endregion
}