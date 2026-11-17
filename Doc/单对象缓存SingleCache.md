# 单对象缓存（SingleCache）

`SingleEntityCache<TKey,TEntity>` 是按主键缓存单行实体的字典缓存，典型用于用户、租户、设备等“按ID高频读取”的场景。

## 1）核心能力

- 主键缓存：`this[key]`
- 主键写入：`SingleCache[key] = entity`（写入或原位替换，并从键同步更新）
- 从键缓存：`GetItemWithSlaveKey(slaveKey)`
- 过期更新：过期后异步/同步刷新
- 定时清理：清理长期未访问项

## 2）关键属性

- `Expire`：过期秒数（默认 `XCodeSetting.SingleCacheExpire`）
- `ClearPeriod`：清理周期（默认 60 秒）
- `MaxEntity`：最大缓存数量（默认 10000）
- `GetKeyMethod / FindKeyMethod`
- `GetSlaveKeyMethod / FindSlaveKeyMethod`

## 3）典型使用

```csharp
// 主键读
var user = User.Meta.SingleCache[1001];

// 从键读（例如用户名）
var user2 = User.Meta.SingleCache.GetItemWithSlaveKey("admin");
```

## 4）回源与更新逻辑

- 未命中：回源数据库查询，写入缓存
- 命中但过期：
  - 轻度过期：异步更新
  - 严重过期：同步更新
- 数据库已删除：自动移除缓存项

## 5）清理策略

定时任务会：

- 清理长时间未访问（约 `10*ClearPeriod`）项
- 当缓存数超 `MaxEntity` 时，按最久未访问淘汰

## 6）从键建议

若业务有唯一业务键（如 `Code/Name`），可配置：

```csharp
sc.FindSlaveKeyMethod = key => User.Find(User._.Name == key);
sc.GetSlaveKeyMethod  = e => e.Name;
```

这能避免每次按业务键查询都回源数据库。

## 7）适用与边界

适用：
- 单行高频读
- 主键/唯一键查询

谨慎：
- 超多不同 key 短时间涌入（可能触发频繁淘汰）
- 强一致写后立读跨进程场景（需配合外部失效通知）

## 8）常见问题

- **缓存命中率低**：key 分布过散或 `Expire` 过短。
- **从键查不到**：未配置 `FindSlaveKeyMethod` / `GetSlaveKeyMethod`。