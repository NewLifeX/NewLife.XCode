# 实体缓存（EntityCache）

`EntityCache<TEntity>` 是“整表级缓存”，适合字典表、参数表、枚举表等“读多写少 + 行数较小”的实体。

> 启用 [实体操作过滤器（IEntityFilter）](实体过滤器（IEntityFilter）.md) 时：缓存加载阶段抑制过滤器，保证缓存保存整表原始数据；`FindAllWithCache/FindByKeyWithCache/FindBySlaveWithCache` 读取时执行过滤器后置阶段，按租户/数据权限裁剪结果。直接访问 `Meta.Cache.Entities` 等内部成员仍不过滤，业务代码请使用这三个方法。

## 1）核心机制

`Meta.Cache.Entities` 首次访问会加载整表，之后在内存集合上做 `Find/FindAll`。

缓存更新策略：

1. **首次访问**：可阻塞加载（`WaitFirst=true`）
2. **过期访问**：先返回旧数据，再异步更新
3. **过久过期（约2倍周期）**：转同步更新
4. **清除缓存**：
   - `force=false`：标记过期，下次异步刷新
   - `force=true`：强制下次同步刷新

## 2）关键属性

- `Expire`：过期秒数（默认来自 `XCodeSetting.EntityCacheExpire`）
- `FillListMethod`：加载数据委托（默认 `Entity.FindAll`）
- `WaitFirst`：首次是否阻塞等待
- `Using`：是否已启用缓存
- `Times`：刷新次数

## 3）典型使用

```csharp
// 触发整表缓存
var list = User.Meta.Cache.Entities;

// 在缓存内查询
var admin = User.Meta.Cache.Find(e => e.Name == "admin");
```

## 4）数据变更行为

实体新增/更新/删除时，缓存会通过 `Add/Update/Remove` 同步调整；事务结束后会按策略触发过期刷新。

因此多数情况下，读取到的是“本进程内近实时”数据。

## 5）适用边界

建议：
- 小表（通常 < 1000）优先
- 中表（~1万）谨慎
- 大表（>1万）不建议整表缓存

## 6）调优建议

- 高并发读场景可略增 `Expire`，减少刷新频率。
- 有强一致要求时，关键更新后 `Clear(reason, force:true)`。
- 启动预热时主动访问 `Meta.Cache.Entities`，避免首请求抖动。

## 7）常见问题

- **第一次访问慢**：`WaitFirst=true` 导致同步加载，属于预期。
- **更新后偶发旧值**：可能命中异步刷新窗口，必要时强制清除。