using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using NewLife.Reflection;
using XCode.Configuration;
using XCode.DataAccessLayer;
using XCode.Model;
using LinqExpression = System.Linq.Expressions.Expression;

namespace XCode.Linq;

/// <summary>实体查询提供者。将LINQ表达式树翻译为XCode查询</summary>
/// <remarks>
/// 为 XCode 实体提供标准 IQueryable&lt;T&gt; 支持，兼容 EF Core 风格的 LINQ 查询。
/// 通过 LinqExpressionVisitor 将 LINQ 表达式树翻译为 XCode WhereExpression 和排序/分页参数。
/// </remarks>
public class EntityQueryProvider : IQueryProvider
{
    #region 属性
    /// <summary>实体工厂</summary>
    public IEntityFactory Factory { get; }

    /// <summary>实体会话</summary>
    public IEntitySession Session => Factory.Session;

    /// <summary>指定连接名。非空时 Execute 将临时切换到该连接执行查询，用于 DAL.Select&lt;T&gt;() 多连接场景</summary>
    private readonly String? _connName;

    /// <summary>需要预加载的关联实体类型列表</summary>
    private readonly List<Type> _includes = [];

    /// <summary>导航属性加载路径。用于生成 LEFT JOIN</summary>
    private readonly List<IncludePath> _includePaths = [];
    #endregion

    #region 构造
    /// <summary>实例化查询提供者</summary>
    /// <param name="factory"></param>
    public EntityQueryProvider(IEntityFactory factory)
    {
        Factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <summary>实例化查询提供者，绑定到指定连接名</summary>
    /// <param name="factory">实体工厂</param>
    /// <param name="connName">连接名。非空时所有查询将路由到该连接</param>
    public EntityQueryProvider(IEntityFactory factory, String connName)
    {
        Factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _connName = connName;
    }
    #endregion

    #region IQueryProvider
    /// <summary>创建查询</summary>
    /// <typeparam name="TElement"></typeparam>
    /// <param name="expression"></param>
    /// <returns></returns>
    public IQueryable<TElement> CreateQuery<TElement>(LinqExpression expression)
    {
        if (expression == null) throw new ArgumentNullException(nameof(expression));

        return new EntityQueryable<TElement>(this, expression);
    }

    /// <summary>创建查询</summary>
    /// <param name="expression"></param>
    /// <returns></returns>
    public IQueryable CreateQuery(LinqExpression expression)
    {
        if (expression == null) throw new ArgumentNullException(nameof(expression));

        var elementType = expression.Type.GetElementTypeEx() ?? expression.Type.GenericTypeArguments[0];
        var queryType = typeof(EntityQueryable<>).MakeGenericType(elementType);
        return (IQueryable)Activator.CreateInstance(queryType, this, expression)!;
    }

    /// <summary>执行查询</summary>
    /// <typeparam name="TResult"></typeparam>
    /// <param name="expression"></param>
    /// <returns></returns>
    public TResult Execute<TResult>(LinqExpression expression)
    {
        if (expression == null) throw new ArgumentNullException(nameof(expression));

        var result = Execute(expression);

        // 直接类型匹配
        if (result is TResult typedResult) return typedResult;

        // 计数类型转换
        if (typeof(TResult) == typeof(Int32) && result is Int64 longCount)
            return (TResult)(Object)(Int32)longCount;

        if (typeof(TResult) == typeof(Int64) && result is Int32 intCount)
            return (TResult)(Object)(Int64)intCount;

        // 列表转换：IList<IEntity> → IList<T>
        if (result is IEnumerable enumerable)
        {
            var elementType = typeof(TResult).GenericTypeArguments.FirstOrDefault();
            if (elementType != null)
            {
                var typedList = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType));
                foreach (var item in enumerable)
                {
                    typedList.Add(item);
                }
                return (TResult)typedList;
            }

            // IEnumerable<T> 协变
            if (typeof(TResult).IsGenericType && typeof(TResult).GetGenericTypeDefinition() == typeof(IEnumerable<>))
                return (TResult)(Object)enumerable.Cast<Object>().ToList();
        }

        return (TResult)result!;
    }

    /// <summary>添加需要预加载的关联实体类型</summary>
    /// <param name="entityType">关联实体类型</param>
    public void AddInclude(Type entityType)
    {
        if (entityType == null) return;

        if (!_includes.Contains(entityType))
            _includes.Add(entityType);
    }

    /// <summary>添加导航属性加载路径。用于生成 LEFT JOIN</summary>
    /// <param name="path">导航路径</param>
    public void AddIncludePath(IncludePath path)
    {
        if (path == null) return;

        if (!_includePaths.Any(p => p.NavigationName == path.NavigationName))
            _includePaths.Add(path);
    }

    /// <summary>解析表达式树，返回访问器用于测试和调试。不执行数据库查询</summary>
    /// <param name="expression">LINQ 表达式</param>
    /// <returns>表达式访问器，包含解析后的 Where/OrderBy/Skip/Take/IsCount 等参数</returns>
    public LinqExpressionVisitor Parse(LinqExpression expression)
    {
        if (expression == null) throw new ArgumentNullException(nameof(expression));

        var visitor = new LinqExpressionVisitor(Factory);
        visitor.Visit(expression);
        return visitor;
    }

    /// <summary>预加载关联实体缓存。在查询执行前调用，预热关联实体缓存以加速内存联表</summary>
    private void PreloadIncludes()
    {
        if (_includes.Count == 0) return;

        foreach (var entityType in _includes)
        {
            try
            {
                var factory = entityType.AsFactory();
                if (factory != null)
                {
                    // 触发整表缓存加载（适合 <1000 行的小表）
                    var list = factory.FindAllWithCache();
                    if (DAL.Debug)
                        DAL.WriteLog("Include 预加载 [{0}] 缓存，共 {1} 条", entityType.Name, list.Count);
                }
            }
            catch (Exception ex)
            {
                DAL.WriteLog("Include 预加载 [{0}] 失败: {1}", entityType.Name, ex.Message);
            }
        }
    }

    /// <summary>执行查询</summary>
    /// <param name="expression"></param>
    /// <returns></returns>
    public Object? Execute(LinqExpression expression)
    {
        if (expression == null) throw new ArgumentNullException(nameof(expression));

        // 先预加载关联实体缓存（Include）
        PreloadIncludes();

        // 解析表达式树，构建查询参数
        var visitor = new LinqExpressionVisitor(Factory);
        visitor.Visit(expression);

        var where = visitor.WhereExpression;
        var orderBy = visitor.OrderBy;
        var skip = visitor.Skip;
        var take = visitor.Take;
        var isCount = visitor.IsCount;
        var isFirst = visitor.IsFirst;
        var isSingle = visitor.IsSingle;
        var throwIfNotFound = visitor.ThrowIfNotFound;

        // 临时切换到指定连接（DAL.Select<T>() 注入）
        var oldConn = _connName != null ? Factory.ConnName : null;
        if (_connName != null) Factory.ConnName = _connName;
        try
        {
            // 计数查询
            if (isCount)
            {
                return Factory.FindCount(where ?? new WhereExpression());
            }

            // 单条查询
            if (isFirst || isSingle)
            {
                var maxRows = isSingle ? 2 : 1;
                var list = Factory.FindAll(where ?? new WhereExpression(), orderBy, null, 0, maxRows);
                if (list.Count > 0)
                {
                    if (isSingle && list.Count > 1)
                        throw new InvalidOperationException("序列包含多个元素");

                    return list[0];
                }

                if (throwIfNotFound)
                    throw new InvalidOperationException("序列不包含任何元素");

                return null;
            }

            // Take(0) 显式调用时返回空集合（此时 HasTake=true 且 take==0）
            if (take == 0 && !isFirst && !isSingle && visitor.HasTake) return new List<IEntity>();

            // 存在导航路径时，在常规查询后批量加载关联实体
            if (_includePaths.Count > 0)
            {
                var mainList = Factory.FindAll(where ?? new WhereExpression(), orderBy, null, skip, take);
                BatchLoadNavigations(mainList);
                return mainList;
            }

            // 常规查询
            return Factory.FindAll(where ?? new WhereExpression(), orderBy, null, skip, take);
        }
        finally
        {
            if (_connName != null) Factory.ConnName = oldConn!;
        }
    }

    /// <summary>批量加载导航属性。对 HasOne 收集 FK 批量查询，对 HasMany 预热缓存</summary>
    private void BatchLoadNavigations(IList<IEntity> list)
    {
        if (list is null || list.Count == 0) return;

        foreach (var path in _includePaths)
        {
            try
            {
                switch (path.NavigationType)
                {
                    case NavigationType.HasOne:
                        BatchLoadHasOne(list, path);
                        break;
                    case NavigationType.HasMany:
                        BatchLoadHasMany(list, path);
                        break;
                }
            }
            catch (Exception ex)
            {
                DAL.WriteLog("导航批量加载 [{0}.{1}] 失败: {2}", Factory.EntityType.Name, path.NavigationName, ex.Message);
            }
        }
    }

    /// <summary>批量加载 HasOne 导航属性。收集外键 → 批量查目标表 → 填充 Extends 缓存</summary>
    private static void BatchLoadHasOne(IList<IEntity> list, IncludePath path)
    {
        var targetFactory = path.TargetType.AsFactory();
        if (targetFactory == null || path.ForeignKey is null || path.PrimaryKey is null) return;

        // 收集所有非空外键值
        var fkValues = new HashSet<Object>();
        foreach (var entity in list)
        {
            var fkValue = entity[path.ForeignKey.Name];
            if (fkValue is not null && !Equals(fkValue, 0) && !(fkValue is String s && s.IsNullOrEmpty()))
                fkValues.Add(fkValue);
        }

        if (fkValues.Count == 0) return;

        // 按目标表主键批量查询
        var pkCol = path.PrimaryKey.ColumnName;
        var inClause = fkValues.Select(v => v is String ? $"'{v}'" : v.ToString()).Join(",");
        var relatedList = targetFactory.FindAll($"{pkCol} in({inClause})", null, null, 0, 0);
        if (relatedList is null || relatedList.Count == 0) return;

        // 构建字典：PK → 实体
        var dict = new Dictionary<Object, IEntity>();
        foreach (var rel in relatedList)
        {
            var key = rel[path.PrimaryKey.Name];
            if (key is not null)
                dict[key] = rel;
        }

        // 填充导航属性到 Extends
        foreach (var entity in list)
        {
            if (entity is not EntityBase eb) continue;

            var fkValue = entity[path.ForeignKey.Name];
            if (fkValue is null) continue;

            if (dict.TryGetValue(fkValue, out var related))
            {
                // 将结果缓存到 Extends 中
                var extends = GetEntityExtends(eb);
                if (extends is not null)
                {
                    var key = path.NavigationName;
                    extends.Get<Object>(key, _ => related);
                }
            }
        }
    }

    /// <summary>批量预加载 HasMany 导航属性。缓存预热整表数据</summary>
    private static void BatchLoadHasMany(IList<IEntity> list, IncludePath path)
    {
        if (list is null || list.Count == 0) return;

        var targetFactory = path.TargetType.AsFactory();
        if (targetFactory == null || path.ForeignKey is null || path.PrimaryKey is null) return;

        // 收集所有主键值
        var pkValues = new HashSet<Object>();
        foreach (var entity in list)
        {
            var pkValue = entity[path.PrimaryKey.Name];
            if (pkValue is not null && !Equals(pkValue, 0))
                pkValues.Add(pkValue);
        }

        if (pkValues.Count == 0) return;

        // 按外键批量查询所有子实体
        var fkCol = path.ForeignKey.ColumnName;
        var inClause = pkValues.Select(v => v is String ? $"'{v}'" : v.ToString()).Join(",");
        var children = targetFactory.FindAll($"{fkCol} in({inClause})", null, null, 0, 0);
        if (children is null || children.Count == 0) return;

        // 按外键分组
        var grouped = new Dictionary<Object, List<IEntity>>();
        foreach (var child in children)
        {
            var fkVal = child[path.ForeignKey.Name];
            if (fkVal is null) continue;

            if (!grouped.TryGetValue(fkVal, out var group))
                grouped[fkVal] = group = [];

            group.Add(child);
        }

        // 填充到每个实体的 Extends 中
        foreach (var entity in list)
        {
            if (entity is not EntityBase eb) continue;

            var pkValue = entity[path.PrimaryKey.Name];
            if (pkValue is null) continue;

            if (grouped.TryGetValue(pkValue, out var childList))
            {
                var extends = GetEntityExtends(eb);
                if (extends is not null)
                {
                    var key = path.NavigationName;
                    extends.Get<Object>(key, _ => childList);
                }
            }
        }
    }

    /// <summary>通过反射获取实体内部的 Extends 对象</summary>
    private static EntityExtend? GetEntityExtends(EntityBase entity)
    {
        try
        {
            // 通过 IEntity.Extends 接口访问（避免反射）
            return ((IEntity)entity).Extends;
        }
        catch
        {
            return null;
        }
    }
    #endregion
}

/// <summary>导航属性加载路径</summary>
public class IncludePath
{
    /// <summary>导航属性名</summary>
    public String NavigationName { get; set; } = null!;

    /// <summary>目标实体类型</summary>
    public Type TargetType { get; set; } = null!;

    /// <summary>导航关系类型</summary>
    public NavigationType NavigationType { get; set; }

    /// <summary>外键字段</summary>
    public FieldItem? ForeignKey { get; set; }

    /// <summary>主键字段</summary>
    public FieldItem? PrimaryKey { get; set; }
}

/// <summary>LINQ 表达式访问器。将System.Linq.Expressions翻译为XCode查询参数</summary>
public class LinqExpressionVisitor : ExpressionVisitor
{
    #region 属性
    /// <summary>实体工厂</summary>
    public IEntityFactory Factory { get; }

    /// <summary>查询条件</summary>
    public Expression? WhereExpression { get; set; }

    /// <summary>排序字段</summary>
    public String? OrderBy { get; set; }

    /// <summary>跳过行数</summary>
    public Int32 Skip { get; set; }

    /// <summary>获取行数</summary>
    public Int32 Take { get; set; }

    /// <summary>是否显式调用了 Take</summary>
    public Boolean HasTake { get; set; }

    /// <summary>是否计数查询</summary>
    public Boolean IsCount { get; set; }

    /// <summary>是否取第一条（First/FirstOrDefault）</summary>
    public Boolean IsFirst { get; set; }

    /// <summary>是否取单条（Single/SingleOrDefault）</summary>
    public Boolean IsSingle { get; set; }

    /// <summary>无匹配时是否抛异常。Single=true，SingleOrDefault=false</summary>
    public Boolean ThrowIfNotFound { get; set; }
    #endregion

    #region 构造
    /// <summary>实例化</summary>
    /// <param name="factory"></param>
    public LinqExpressionVisitor(IEntityFactory factory)
    {
        Factory = factory;
    }
    #endregion

    #region 方法访问
    /// <summary>访问方法调用表达式</summary>
    /// <param name="node"></param>
    /// <returns></returns>
    protected override LinqExpression VisitMethodCall(MethodCallExpression node)
    {
        if (node == null) throw new ArgumentNullException(nameof(node));

        var methodName = node.Method.Name;

        // 处理链式调用的内部表达式
        if (node.Arguments.Count > 0)
        {
            var firstArg = node.Arguments[0];
            if (firstArg is MethodCallExpression || firstArg.NodeType == ExpressionType.Constant)
            {
                Visit(firstArg);
            }
        }

        switch (methodName)
        {
            case "Where":
                VisitWhere(node);
                break;
            case "OrderBy":
            case "OrderByDescending":
            case "ThenBy":
            case "ThenByDescending":
                VisitOrderBy(node, methodName);
                break;
            case "Skip":
                VisitSkip(node);
                break;
            case "Take":
                VisitTake(node);
                break;
            case "Select":
                // Select 暂不处理（字段投影），保留原有逻辑
                break;
            case "Count":
            case "LongCount":
                IsCount = true;
                VisitPredicate(node);
                break;
            case "First":
                IsFirst = true;
                Take = 1;
                VisitPredicate(node);
                break;
            case "FirstOrDefault":
                IsFirst = true;
                ThrowIfNotFound = false;
                Take = 1;
                VisitPredicate(node);
                break;
            case "Single":
                IsSingle = true;
                ThrowIfNotFound = true;
                Take = 2;
                VisitPredicate(node);
                break;
            case "SingleOrDefault":
                IsSingle = true;
                ThrowIfNotFound = false;
                Take = 2;
                VisitPredicate(node);
                break;
            case "ToList":
            case "ToArray":
            case "ToListAsync":
            case "ToArrayAsync":
                break;
        }

        return node;
    }

    private void VisitWhere(MethodCallExpression node)
    {
        if (node.Arguments.Count < 2) return;

        var lambda = node.Arguments[1] as LambdaExpression;
        if (lambda == null)
        {
            if (node.Arguments[1] is UnaryExpression unary && unary.Operand is LambdaExpression lam)
                lambda = lam;
        }

        if (lambda == null) return;

        var xexp = TranslateExpression(lambda.Body);
        if (xexp != null)
        {
            if (WhereExpression == null)
                WhereExpression = xexp;
            else
                WhereExpression &= xexp;
        }
    }

    private void VisitOrderBy(MethodCallExpression node, String methodName)
    {
        if (node.Arguments.Count < 2) return;

        var lambda = node.Arguments[1] as LambdaExpression;
        if (lambda == null)
        {
            if (node.Arguments[1] is UnaryExpression unary && unary.Operand is LambdaExpression lam)
                lambda = lam;
        }

        if (lambda == null) return;

        var fieldName = GetMemberName(lambda.Body);
        if (fieldName.IsNullOrEmpty()) return;

        var desc = methodName.Contains("Descending") ? " desc" : "";
        var orderClause = $"{fieldName}{desc}";

        if (OrderBy.IsNullOrEmpty() || methodName.StartsWith("OrderBy"))
            OrderBy = orderClause;
        else
            OrderBy += $",{orderClause}";
    }

    private void VisitSkip(MethodCallExpression node)
    {
        if (node.Arguments.Count < 2) return;

        var arg = node.Arguments[1];
        if (arg is ConstantExpression constant)
        {
            Skip = constant.Value.ToInt();
        }
    }

    private void VisitTake(MethodCallExpression node)
    {
        if (node.Arguments.Count < 2) return;

        var arg = node.Arguments[1];
        if (arg is ConstantExpression constant)
        {
            Take = constant.Value.ToInt();
            HasTake = true;
        }
    }

    /// <summary>提取方法中的 predicate 参数作为 WHERE 条件</summary>
    private void VisitPredicate(MethodCallExpression node)
    {
        if (node.Arguments.Count < 2) return;

        var lambda = node.Arguments[1] as LambdaExpression;
        if (lambda == null)
        {
            if (node.Arguments[1] is UnaryExpression unary && unary.Operand is LambdaExpression lam)
                lambda = lam;
        }

        if (lambda == null) return;

        var xexp = TranslateExpression(lambda.Body);
        if (xexp != null)
        {
            if (WhereExpression == null)
                WhereExpression = xexp;
            else
                WhereExpression &= xexp;
        }
    }

    private static String? GetMemberName(LinqExpression expression)
    {
        if (expression is MemberExpression member)
            return member.Member.Name;

        if (expression is UnaryExpression unary)
            return GetMemberName(unary.Operand);

        return null;
    }
    #endregion

    #region 表达式翻译
    /// <summary>翻译表达式为XCode Expression</summary>
    /// <param name="node"></param>
    /// <returns></returns>
    private Expression? TranslateExpression(LinqExpression node)
    {
        if (node == null) return null;

        switch (node.NodeType)
        {
            case ExpressionType.Equal:
            case ExpressionType.NotEqual:
            case ExpressionType.GreaterThan:
            case ExpressionType.GreaterThanOrEqual:
            case ExpressionType.LessThan:
            case ExpressionType.LessThanOrEqual:
                return TranslateBinary((BinaryExpression)node);
            case ExpressionType.AndAlso:
            case ExpressionType.OrElse:
                return TranslateLogical((BinaryExpression)node);
            case ExpressionType.Call:
                return TranslateMethodCall((MethodCallExpression)node);
            case ExpressionType.Not:
                return TranslateNot((UnaryExpression)node);
            case ExpressionType.Convert:
                return TranslateExpression(((UnaryExpression)node).Operand);
            default:
                return null;
        }
    }

    private Expression? TranslateBinary(BinaryExpression node)
    {
        var left = GetFieldItem(node.Left);
        if (left == null) return null;

        var value = GetValue(node.Right);
        var op = node.NodeType switch
        {
            ExpressionType.Equal => "=",
            ExpressionType.NotEqual => "<>",
            ExpressionType.GreaterThan => ">",
            ExpressionType.GreaterThanOrEqual => ">=",
            ExpressionType.LessThan => "<",
            ExpressionType.LessThanOrEqual => "<=",
            _ => "="
        };

        var fieldName = left.Name;
        return new Expression($"{fieldName}{op}{value}");
    }

    private Expression? TranslateLogical(BinaryExpression node)
    {
        var left = TranslateExpression(node.Left);
        var right = TranslateExpression(node.Right);

        if (left == null && right == null) return null;
        if (left == null) return right;
        if (right == null) return left;

        return node.NodeType == ExpressionType.AndAlso
            ? left & right
            : left | right;
    }

    private Expression? TranslateMethodCall(MethodCallExpression node)
    {
        var methodName = node.Method.Name;
        var obj = node.Object;

        if (methodName == "Contains" && obj != null)
        {
            var field = GetFieldItem(obj);
            if (field != null && node.Arguments.Count > 0)
            {
                var value = GetValue(node.Arguments[0]);
                return new Expression($"{field.Name} Like '%{value?.Trim('\'')}%'");
            }
        }

        if (methodName == "StartsWith" && obj != null)
        {
            var field = GetFieldItem(obj);
            if (field != null && node.Arguments.Count > 0)
            {
                var value = GetValue(node.Arguments[0]);
                return new Expression($"{field.Name} Like '{value?.Trim('\'')}%'");
            }
        }

        if (methodName == "EndsWith" && obj != null)
        {
            var field = GetFieldItem(obj);
            if (field != null && node.Arguments.Count > 0)
            {
                var value = GetValue(node.Arguments[0]);
                return new Expression($"{field.Name} Like '%{value?.Trim('\'')}'");
            }
        }

        return null;
    }

    private Expression? TranslateNot(UnaryExpression node)
    {
        var inner = TranslateExpression(node.Operand);
        if (inner == null) return null;

        return new Expression($"NOT ({inner})");
    }

    private FieldItem? GetFieldItem(LinqExpression expression)
    {
        if (expression is MemberExpression member)
        {
            var fieldName = member.Member.Name;
            return Factory.Table.FindByName(fieldName) as FieldItem;
        }

        if (expression is UnaryExpression unary)
            return GetFieldItem(unary.Operand);

        return null;
    }

    private static String? GetValue(LinqExpression expression)
    {
        if (expression is ConstantExpression constant)
        {
            var value = constant.Value;
            if (value == null) return "null";
            if (value is Boolean b) return b ? "1" : "0";

            return value is String ? $"'{value}'" : value.ToString();
        }

        if (expression is MemberExpression member)
        {
            var value = EvaluateMember(member);
            if (value == null) return "null";
            if (value is Boolean b2) return b2 ? "1" : "0";

            return value is String ? $"'{value}'" : value.ToString();
        }

        var lambda = System.Linq.Expressions.Expression.Lambda(expression);
        var compiled = lambda.Compile();
        var result = compiled.DynamicInvoke();
        if (result == null) return "null";
        if (result is Boolean b3) return b3 ? "1" : "0";

        return result is String ? $"'{result}'" : result.ToString();
    }

    private static Object? EvaluateMember(MemberExpression member)
    {
        if (member.Expression is ConstantExpression constExp)
        {
            var container = constExp.Value;
            if (container == null) return null;

            var fieldInfo = member.Member as FieldInfo;
            if (fieldInfo != null)
                return fieldInfo.GetValue(container);

            var propInfo = member.Member as PropertyInfo;
            if (propInfo != null)
                return propInfo.GetValue(container);
        }

        try
        {
            var lambda = System.Linq.Expressions.Expression.Lambda(member);
            return lambda.Compile().DynamicInvoke();
        }
        catch
        {
            return null;
        }
    }
    #endregion
}
