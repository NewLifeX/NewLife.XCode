﻿using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife.Data;
using NewLife.Reflection;
using XCode.Common;
using XCode.Configuration;

namespace XCode;

/// <summary>数据实体基类的基类</summary>
[Serializable]
[EditorBrowsable(EditorBrowsableState.Never)]
public abstract partial class EntityBase : IEntity, IModel, IExtend, ICloneable
{
    #region 初始化数据
    /// <summary>首次连接数据库时初始化数据，仅用于实体类重载，用户不应该调用该方法</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected internal virtual void InitData() { }
    #endregion

    #region 填充数据
    /// <summary>填充数据完成时调用。默认设定标记<see cref="IsFromDatabase"/></summary>
    protected virtual void OnLoad() => IsFromDatabase = true;

    /// <summary>是否来自数据库。设置相同属性值时不改变脏数据</summary>
    protected virtual Boolean IsFromDatabase { get; set; }

    /// <summary>是否来自数据库。设置相同属性值时不改变脏数据</summary>
    Boolean IEntity.IsFromDatabase => IsFromDatabase;
    #endregion

    #region 操作
    /// <summary>把该对象持久化到数据库</summary>
    /// <returns></returns>
    public abstract Int32 Insert();

    /// <summary>更新数据库</summary>
    /// <returns></returns>
    public abstract Int32 Update();

    /// <summary>从数据库中删除该对象</summary>
    /// <returns></returns>
    public abstract Int32 Delete();

    /// <summary>把该对象持久化到数据库</summary>
    /// <returns></returns>
    public abstract Task<Int32> InsertAsync();

    /// <summary>更新数据库</summary>
    /// <returns></returns>
    public abstract Task<Int32> UpdateAsync();

    /// <summary>从数据库中删除该对象</summary>
    /// <returns></returns>
    public abstract Task<Int32> DeleteAsync();

    /// <summary>保存。根据主键检查数据库中是否已存在该对象，再决定调用Insert或Update</summary>
    /// <returns></returns>
    public abstract Int32 Save();

    /// <summary>不需要验证的保存</summary>
    /// <returns></returns>
    public abstract Int32 SaveWithoutValid();

    /// <summary>异步保存。实现延迟保存，大事务保存。主要面向日志表和频繁更新的在线记录表</summary>
    /// <param name="msDelay">延迟保存的时间。默认0ms近实时保存</param>
    /// <returns>是否成功加入异步队列</returns>
    public abstract Boolean SaveAsync(Int32 msDelay = 0);

    /// <summary>验证数据，通过抛出异常的方式提示验证失败。</summary>
    /// <remarks>建议重写者调用基类的实现，因为基类根据数据字段的唯一索引进行数据验证。</remarks>
    /// <param name="isNew">是否新数据</param>
    public abstract void Valid(Boolean isNew);

    /// <summary>验证数据，支持添删改，通过返回值表示验证失败</summary>
    /// <param name="method"></param>
    /// <returns></returns>
    public abstract Boolean Valid(DataMethod method);
    #endregion

    #region 获取/设置 字段值
    /// <summary>获取/设置 字段值，不影响脏数据</summary>
    /// <param name="name">字段名</param>
    /// <returns></returns>
    public abstract Object? this[String name] { get; set; }

    /// <summary>设置字段值，该方法影响脏数据。</summary>
    /// <param name="name">字段名</param>
    /// <param name="value">值</param>
    /// <returns>返回是否成功设置了数据</returns>
    public Boolean SetItem(String name, Object? value)
    {
        var fact = GetType().AsFactory();
        var fi = fact.Table.FindByName(name);
        // 确保数据类型一致
        if (fi is not null) value = value.ChangeType(fi.Type);

        var b = OnPropertyChanging(name, value);
        if (b)
        {
            // OnPropertyChanging中根据新旧值是否相同来影响脏数据
            // SetItem作为必定影响脏数据的代替者
            //Dirtys.Add(name, this[name]);
            this[name] = value;
        }
        return b;
    }
    #endregion

    #region 克隆
    /// <summary>创建当前对象的克隆对象，仅拷贝基本字段</summary>
    /// <returns></returns>
    public abstract Object Clone();

    /// <summary>克隆实体。创建当前对象的克隆对象，仅拷贝基本字段</summary>
    /// <param name="setDirty">是否设置脏数据</param>
    /// <returns></returns>
    IEntity IEntity.CloneEntity(Boolean setDirty) => CloneEntityInternal(setDirty);

    /// <summary>克隆实体</summary>
    /// <param name="setDirty"></param>
    /// <returns></returns>
    internal protected abstract IEntity CloneEntityInternal(Boolean setDirty = true);

    /// <summary>复制来自指定模型的成员，可以是不同类型。脏数据用于控制局部复制</summary>
    /// <param name="model">来源实体对象</param>
    /// <param name="setDirty">是否设置脏数据。默认true，仅拷贝有脏数据的源字段，也设置目标脏数据</param>
    /// <returns>实际复制成员数</returns>
    public virtual Int32 CopyFrom(IModel model, Boolean setDirty = true)
    {
        if (model == this) return 0;

        IEntity dest = this;

        var n = 0;
        var destNames = dest.GetType().AsFactory().FieldNames;
        if (model is IEntity source)
        {
            var srcNames = source.GetType().AsFactory().FieldNames;
            if (srcNames == null || srcNames.Count <= 0) return 0;

            foreach (var item in srcNames)
            {
                if (destNames.Contains(item))
                {
                    if (setDirty)
                    {
                        // 启用脏数据，仅复制有脏数据的字段，同时避免拷贝主键
                        if (source.IsFromDatabase || source.Dirtys[item])
                            dest.SetItem(item, source[item]);
                    }
                    else
                        dest[item] = source[item];
                }
                else
                {
                    // 如果没有该字段，则写入到扩展属性里面去
                    if (setDirty) Dirtys.Add(item, dest[item]);
                    dest[item] = source[item];
                }

                n++;
            }
            // 赋值扩展数据
            //entity.Extends.CopyTo(src.Extends);
            if (source is EntityBase source2 && source2._Items != null && source2._Items.Count > 0)
            {
                foreach (var item in source2._Items)
                {
                    dest[item.Key] = item.Value;
                }
            }
        }
        else
        {
            foreach (var item in destNames)
            {
                if (setDirty)
                    dest.SetItem(item, model[item]);
                else
                    dest[item] = model[item];

                n++;
            }
        }

        return n;
    }
    #endregion

    #region 脏数据
    [NonSerialized]
    private DirtyCollection? _Dirtys;
    /// <summary>脏属性。存储哪些属性的数据被修改过了。</summary>
    [XmlIgnore, ScriptIgnore, IgnoreDataMember]
    protected DirtyCollection Dirtys => _Dirtys ??= new DirtyCollection();

    /// <summary>脏属性。存储哪些属性的数据被修改过了。</summary>
    DirtyCollection IEntity.Dirtys => Dirtys;

    /// <summary>是否有脏数据。被修改为不同值</summary>
    /// <param name="name"></param>
    /// <returns></returns>
    protected Boolean IsDirty(String name) => _Dirtys != null && _Dirtys[name];

    /// <summary>是否有脏数据。被修改为不同值</summary>
    /// <param name="name"></param>
    /// <returns></returns>
    Boolean IEntity.IsDirty(String name) => _Dirtys != null && _Dirtys[name];

    /// <summary>是否有脏数据。决定是否可以Update</summary>
    protected Boolean HasDirty => _Dirtys != null && _Dirtys.Count > 0;

    /// <summary>是否有脏数据。决定是否可以Update</summary>
    Boolean IEntity.HasDirty => _Dirtys != null && _Dirtys.Count > 0;
    #endregion

    #region 扩展属性
    [NonSerialized]
    internal EntityExtend? _Extends;
    /// <summary>扩展属性。避免在序列化中出现</summary>
    [XmlIgnore, ScriptIgnore, IgnoreDataMember]
    protected EntityExtend Extends { get => _Extends ??= new EntityExtend(); set => _Extends = value; }

    /// <summary>扩展属性。避免在序列化中出现</summary>
    EntityExtend IEntity.Extends => Extends;

    [NonSerialized]
    internal IDictionary<String, Object?>? _Items;
    /// <summary>扩展字段。存放未能映射到实体属性的数据库字段</summary>
    [XmlIgnore, ScriptIgnore, IgnoreDataMember]
    IDictionary<String, Object?> IExtend.Items => _Items ??= new Dictionary<String, Object?>();

    /// <summary>仅用于扩展数据的索引器</summary>
    /// <param name="key"></param>
    /// <returns></returns>
    Object? IExtend.this[String key] { get => _Items?[key]; set => (this as IExtend).Items[key] = value; }

    ///// <summary>扩展数据键集合</summary>
    //IEnumerable<String> IExtend2.Keys => _Items?.Keys;
    #endregion

    #region 累加
    [NonSerialized]
    private IEntityAddition? _Addition;
    /// <summary>累加</summary>
    [XmlIgnore, ScriptIgnore, IgnoreDataMember]
    IEntityAddition IEntity.Addition => _Addition ??= new EntityAddition(this);
    #endregion

    #region 主键为空
    /// <summary>主键是否为空</summary>
    [XmlIgnore, ScriptIgnore, IgnoreDataMember]
    Boolean IEntity.IsNullKey => Helper.IsEntityNullKey(this);

    /// <summary>主键是否为空</summary>
    protected Boolean IsNullKey => Helper.IsEntityNullKey(this);

    /// <summary>设置主键为空。Save将调用Insert</summary>
    void IEntity.SetNullKey()
    {
        var eop = GetType().AsFactory();
        foreach (var item in eop.Fields)
        {
            if (item.PrimaryKey || item.IsIdentity) this[item.Name] = null;
        }
    }
    #endregion

    #region 实体相等
    /// <summary>判断两个实体是否相等。有可能是同一条数据的两个实体对象</summary>
    /// <param name="entity"></param>
    /// <returns></returns>
    Boolean IEntity.EqualTo(IEntity entity)
    {
        if (entity == null || GetType() != entity.GetType()) return false;
        if (this == entity) return true;

        // 判断是否所有主键相等
        var op = GetType().AsFactory();
        var ps = op.Table.PrimaryKeys;
        // 如果没有主键，则判断所有字段
        if (ps == null || ps.Length <= 0) ps = op.Table.Fields;
        foreach (var item in ps)
        {
            var v1 = this[item.Name];
            var v2 = entity[item.Name];
            //// 特殊处理整数类型，避免出现相同值不同整型而导致结果不同
            //if (item.Type.IsIntType() && Convert.ToInt64(v1) != Convert.ToInt64(v2)) return false;

            //if (item.Type == typeof(String)) { v1 += ""; v2 += ""; }

            //if (!Object.Equals(v1, v2)) return false;
            if (!CheckEqual(v1, v2)) return false;
        }

        return true;
    }
    #endregion
}