using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using NewLife;
using XCode;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.EntityTests;

/// <summary>树形实体测试实体</summary>
[Serializable]
[BindTable("TreeTestEntity", Description = "树形实体测试", ConnName = "TreeTest", DbType = DatabaseType.SQLite)]
public class TreeTestEntity : EntityTree<TreeTestEntity>
{
    #region 属性
    private Int32 _ID;
    /// <summary>编号</summary>
    [DataObjectField(true, true, false, 0)]
    [BindColumn("ID", "编号", "")]
    public Int32 ID { get => _ID; set { if (OnPropertyChanging("ID", value)) { _ID = value; OnPropertyChanged("ID"); } } }

    private Int32 _ParentID;
    /// <summary>父编号</summary>
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ParentID", "父编号", "")]
    public Int32 ParentID { get => _ParentID; set { if (OnPropertyChanging("ParentID", value)) { _ParentID = value; OnPropertyChanged("ParentID"); } } }

    private String _Name;
    /// <summary>名称</summary>
    [DataObjectField(false, false, false, 50)]
    [BindColumn("Name", "名称", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private Int32 _Sort;
    /// <summary>排序</summary>
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Sort", "排序", "")]
    public Int32 Sort { get => _Sort; set { if (OnPropertyChanging("Sort", value)) { _Sort = value; OnPropertyChanged("Sort"); } } }

    private String _Remark;
    /// <summary>备注</summary>
    [DataObjectField(false, false, true, 200)]
    [BindColumn("Remark", "备注", "")]
    public String Remark { get => _Remark; set { if (OnPropertyChanging("Remark", value)) { _Remark = value; OnPropertyChanged("Remark"); } } }
    #endregion

    #region 获取/设置 字段值
    /// <summary>获取/设置 字段值</summary>
    /// <param name="name">字段名</param>
    /// <returns></returns>
    public override Object this[String name]
    {
        get => name switch
        {
            "ID" => _ID,
            "ParentID" => _ParentID,
            "Name" => _Name,
            "Sort" => _Sort,
            "Remark" => _Remark,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "ID": _ID = value.ToInt(); break;
                case "ParentID": _ParentID = value.ToInt(); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "Sort": _Sort = value.ToInt(); break;
                case "Remark": _Remark = Convert.ToString(value); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion
}

/// <summary>树形实体（EntityTree）集成测试。使用 SQLite 临时库验证树形结构的导航、排序与校验。</summary>
[Collection("Database")]
public class EntityTreeTests
{
    private static readonly String _connName = "TreeTest";
    private static readonly String _dbFile;

    static EntityTreeTests()
    {
        _dbFile = Path.Combine(Path.GetTempPath(), $"TreeTest_{Guid.NewGuid():n}.db");
        DAL.AddConnStr(_connName, $"Data Source={_dbFile}", null, "SQLite");
        DAL.Create(_connName).Execute("CREATE TABLE IF NOT EXISTS TreeTestEntity (ID INTEGER PRIMARY KEY AUTOINCREMENT, ParentID INTEGER NOT NULL DEFAULT 0, Name TEXT NOT NULL DEFAULT '', Sort INTEGER NOT NULL DEFAULT 0, Remark TEXT)");
    }

    public EntityTreeTests()
    {
        DAL.Create(_connName).Execute("DELETE FROM TreeTestEntity");
        ResetTree();
    }

    private static void ResetTree()
    {
        TreeTestEntity.Meta.Session.Cache.Clear("测试重置", true);
        TreeTestEntity.Root = null!;
    }

    private static TreeTestEntity Add(String name, Int32 parentId = 0, Int32 sort = 0, String remark = null)
    {
        var entity = new TreeTestEntity { Name = name, ParentID = parentId, Sort = sort, Remark = remark };
        entity.Insert();
        return entity;
    }

    /// <summary>构建 A(B 同级) / A1 / A1a 三层测试树</summary>
    private static (TreeTestEntity A, TreeTestEntity B, TreeTestEntity A1, TreeTestEntity A1a) BuildTree()
    {
        var a = Add("A", 0, 1);
        var b = Add("B", 0, 2);
        var a1 = Add("A1", a.ID, 1);
        var a1a = Add("A1a", a1.ID, 1);
        ResetTree();
        return (a, b, a1, a1a);
    }

    private static TreeTestEntity Reload(TreeTestEntity entity) => TreeTestEntity.FindByKey(entity.ID)!;

    [Fact(DisplayName = "顶级子节点按排序降序返回")]
    public void RootChilds_SortedDesc()
    {
        var (a, b, _, _) = BuildTree();

        var childs = TreeTestEntity.FindAllByParent(0);
        Assert.Equal(2, childs.Count);
        Assert.Equal(new[] { "B", "A" }, childs.Select(e => e.Name));

        // Root 节点与顶级列表一致
        Assert.Equal(new[] { "B", "A" }, TreeTestEntity.Root.Childs.Select(e => e.Name));
    }

    [Fact(DisplayName = "子节点与父节点导航")]
    public void Childs_And_Parent_Navigation()
    {
        var (a, _, a1, a1a) = BuildTree();

        var na = Reload(a);
        var na1 = Reload(a1);
        var na1a = Reload(a1a);

        Assert.Equal(new[] { "A1" }, na.Childs.Select(e => e.Name));
        Assert.Equal(new[] { "A1a" }, na1.Childs.Select(e => e.Name));
        Assert.Empty(na1a.Childs);

        Assert.Equal("A", na1.Parent!.Name);
        Assert.Equal("A1", na1a.Parent!.Name);
        Assert.Null(na.Parent);

        // 深度：顶级1层，逐级递增；根节点为0
        Assert.Equal(0, TreeTestEntity.Root.Deepth);
        Assert.Equal(1, na.Deepth);
        Assert.Equal(2, na1.Deepth);
        Assert.Equal(3, na1a.Deepth);
    }

    [Fact(DisplayName = "子孙节点与父节点集合")]
    public void AllChilds_And_AllParents()
    {
        var (a, _, a1, a1a) = BuildTree();

        var na = Reload(a);
        var na1a = Reload(a1a);

        Assert.Equal(new[] { "A1", "A1a" }, na.AllChilds.Select(e => e.Name));
        Assert.Equal(new[] { "A", "A1", "A1a" }, na.MyAllChilds.Select(e => e.Name));
        Assert.Equal(new[] { "A", "A1" }, na1a.AllParents.Select(e => e.Name));

        // 静态查询入口
        Assert.Equal(new[] { "A", "A1", "A1a" }, TreeTestEntity.FindAllChildsByParent(a.ID).Select(e => e.Name));
        Assert.Equal(new[] { "A", "A1" }, TreeTestEntity.FindAllParentsByKey(a1a.ID).Select(e => e.Name));
        Assert.Empty(TreeTestEntity.FindAllChildsByParent(99999));
        Assert.Empty(TreeTestEntity.FindAllParentsByKey(99999));

        // 排除指定分支（连同子孙节点一起排除）
        Assert.Equal(new[] { "A" }, na.FindAllChildsExcept(Reload(a1)).Select(e => e.Name));
    }

    [Fact(DisplayName = "层级路径查找与全路径")]
    public void FindByPath_And_FullPath()
    {
        var (a, _, _, a1a) = BuildTree();

        var na = Reload(a);
        var na1a = Reload(a1a);

        Assert.Equal(a1a.ID, na.FindByPath("A1/A1a")!.ID);
        Assert.Equal(a1a.ID, na.FindByPath(@"A1\A1a")!.ID);
        Assert.Null(na.FindByPath("B"));
        Assert.Null(na.FindByPath("A1/NotFound"));

        Assert.Equal(@"\A", na.FullPath);
        Assert.Equal(@"\A\A1\A1a", na1a.FullPath);
    }

    [Fact(DisplayName = "节点名随深度前缀缩进")]
    public void NodeName_WithDepthPrefix()
    {
        var (a, _, a1, a1a) = BuildTree();

        Assert.Equal("A", Reload(a).NodeName);
        Assert.Equal("A", Reload(a).TreeNodeName);
        Assert.Equal(new String('　', 2) + "A1", Reload(a1).TreeNodeName);
        Assert.Equal(new String('　', 4) + "A1a", Reload(a1a).TreeNodeName);
        Assert.Equal(new String('　', 4), new String(Reload(a1a).TreeNodeName.TakeWhile(c => c == '　').ToArray()));
    }

    [Fact(DisplayName = "文本键设置生效（TreeNodeText 使用指定列）")]
    public void TreeNodeText_UsesTextSetting()
    {
        var p = Add("P", 0, 0, "备注P");
        var c = Add("C", p.ID, 0, "备注文本");
        ResetTree();

        try
        {
            TreeTestEntity.Setting.Text = "Remark";
            var node = Reload(c);
            Assert.EndsWith("备注文本", node.TreeNodeText);
        }
        finally
        {
            TreeTestEntity.Setting.Text = null;
        }

        // 未设置文本键时回退到名称
        ResetTree();
        var node2 = Reload(c);
        Assert.EndsWith("C", node2.TreeNodeText);
        Assert.DoesNotContain("备注文本", node2.TreeNodeText);
    }

    [Fact(DisplayName = "排序上升/下降交换同级顺序")]
    public void UpDown_SwapSiblings()
    {
        var p = Add("P", 0, 0);
        var s1 = Add("S1", p.ID, 1);
        var s2 = Add("S2", p.ID, 2);
        var s3 = Add("S3", p.ID, 3);
        ResetTree();

        String[] Siblings() => TreeTestEntity.FindAllByParent(p.ID).Select(e => e.Name).ToArray();
        Assert.Equal(new[] { "S3", "S2", "S1" }, Siblings());

        // 中间的上升到第一位
        Reload(s2).Up();
        Assert.Equal(new[] { "S2", "S3", "S1" }, Siblings());

        // 末位的上升一位
        Reload(s1).Up();
        Assert.Equal(new[] { "S2", "S1", "S3" }, Siblings());

        // 第一位下降一位
        Reload(s2).Down();
        Assert.Equal(new[] { "S1", "S2", "S3" }, Siblings());

        // 末位继续下降无效
        Reload(s3).Down();
        Assert.Equal(new[] { "S1", "S2", "S3" }, Siblings());
    }

    [Fact(DisplayName = "无头节点（父级缺失）并入顶级并单独列出")]
    public void NoParent_OrphanNodes()
    {
        var orphan = Add("Orphan", 999, 0);
        ResetTree();

        Assert.Contains("Orphan", TreeTestEntity.FindAllNoParent().Select(e => e.Name));
        Assert.Contains("Orphan", TreeTestEntity.Root.Childs.Select(e => e.Name));

        // 删除父节点后，子节点变成无头节点
        var b = Add("B", 0, 0);
        var b1 = Add("B1", b.ID, 0);
        ResetTree();
        Reload(b).Delete();
        ResetTree();

        Assert.Equal(new[] { "Orphan", "B1" }, TreeTestEntity.FindAllNoParent().Select(e => e.Name));
        Assert.Contains("B1", TreeTestEntity.Root.Childs.Select(e => e.Name));
        Assert.Null(Reload(b1).Parent);
    }

    [Fact(DisplayName = "上级校验：不能是自己或子孙节点")]
    public void Valid_RejectsCycle()
    {
        var (a, _, a1, a1a) = BuildTree();

        // 上级不能是当前节点
        var na = Reload(a);
        na.ParentID = na.ID;
        Assert.Throws<XException>(() => na.Update());

        // 上级不能是子孙节点
        var na2 = Reload(a);
        na2.ParentID = a1a.ID;
        Assert.Throws<XException>(() => na2.Update());

        // 校验失败不应写库
        ResetTree();
        Assert.Equal(0, Reload(a).ParentID);
    }

    [Fact(DisplayName = "包含判断与重新挂载父级")]
    public void Contains_And_MoveNode()
    {
        var (a, b, a1, a1a) = BuildTree();

        var na = Reload(a);
        Assert.True(na.Contains(a.ID));
        Assert.True(na.Contains(a1a.ID));
        Assert.False(na.Contains(Reload(b).ID));

        // 把 A1a 移动到 B 下
        var na1a = Reload(a1a);
        na1a.ParentID = b.ID;
        Assert.Equal(1, na1a.Update());
        ResetTree();

        Assert.Empty(Reload(a1).Childs);
        Assert.Equal(new[] { "A1a" }, Reload(b).Childs.Select(e => e.Name));
        Assert.Equal("B", Reload(a1a).Parent!.Name);
        Assert.Equal(new[] { "B" }, Reload(a1a).AllParents.Select(e => e.Name));
    }

    [Fact(DisplayName = "大排序开关反转同级顺序")]
    public void BigSort_False_Ascending()
    {
        var p = Add("P", 0, 0);
        var s1 = Add("S1", p.ID, 1);
        var s2 = Add("S2", p.ID, 2);
        ResetTree();

        try
        {
            TreeTestEntity.Setting.BigSort = false;
            Assert.Equal(new[] { "S1", "S2" }, TreeTestEntity.FindAllByParent(p.ID).Select(e => e.Name));
        }
        finally
        {
            TreeTestEntity.Setting.BigSort = true;
        }

        ResetTree();
        Assert.Equal(new[] { "S2", "S1" }, TreeTestEntity.FindAllByParent(p.ID).Select(e => e.Name));
    }
}
