using System;
using System.Linq;
using XCode.Code;
using Xunit;

namespace XUnitTest.XCode.Code;

public class MemberSectionTests
{
    [Fact]
    public void Parse()
    {
        var code = 
        """

            #region 扩展属性
            /// <summary>部门</summary>
            [XmlIgnore, IgnoreDataMember, ScriptIgnore]
            public Department Department => Extends.Get(nameof(Department), k => Department.FindByID(DepartmentID));

            /// <summary>部门</summary>
            [Map(nameof(DepartmentID), typeof(Department), "ID")]
            [Category("登录信息")]
            public String DepartmentName => Department?.Name;
            #endregion

            #region 扩展查询
            /// <summary>根据编号查找</summary>
            /// <param name="id">编号</param>
            /// <returns>实体对象</returns>
            public static User FindByID(Int32 id)
            {
                if (id <= 0) return null;

                // 实体缓存
                if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.ID == id);

                // 单对象缓存
                return Meta.SingleCache[id];

                //return Find(_.ID == id);
            }

            /// <summary>根据名称查找</summary>
            /// <param name="name">名称</param>
            /// <returns>实体对象</returns>
            public static User FindByName(String name)
            {
                if (name.IsNullOrEmpty()) return null;

                // 实体缓存
                if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Name.EqualIgnoreCase(name));

                // 单对象缓存
                //return Meta.SingleCache.GetItemWithSlaveKey(name) as User;

                return Find(_.Name == name);
            }
            /// <summary>根据邮件查找</summary>
            /// <param name="mail">邮件</param>
            /// <returns>实体列表</returns>
            public static IList<User> FindAllByMail(String mail)
            {
                if (mail.IsNullOrEmpty()) return new List<User>();

                // 实体缓存
                if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.Mail.EqualIgnoreCase(mail));

                return FindAll(_.Mail == mail);
            }
            /// <summary>根据手机查找</summary>
            /// <param name="mobile">手机</param>
            /// <returns>实体列表</returns>
            public static IList<User> FindAllByMobile(String mobile)
            {
                if (mobile.IsNullOrEmpty()) return new List<User>();

                // 实体缓存
                if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.Mobile.EqualIgnoreCase(mobile));

                return FindAll(_.Mobile == mobile);
            }

            /// <summary>根据代码查找</summary>
            /// <param name="code">代码</param>
            /// <returns>实体列表</returns>
            public static IList<User> FindAllByCode(String code)
            {
                if (code.IsNullOrEmpty()) return new List<User>();

                // 实体缓存
                if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.Code.EqualIgnoreCase(code));

                return FindAll(_.Code == code);
            }

            /// <summary>根据角色查找</summary>
            /// <param name="roleId">角色</param>
            /// <returns>实体列表</returns>
            public static IList<User> FindAllByRoleID(Int32 roleId)
            {
                if (roleId <= 0) return new List<User>();

                // 实体缓存
                if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.RoleID == roleId);

                return FindAll(_.RoleID == roleId);
            }
            #endregion


        """;

        var lines = code.Split(Environment.NewLine);

        var list = MemberSection.Parse(lines);

        Assert.NotNull(list);
        Assert.Equal(8, list.Count);

        Assert.Equal("Department", list[0].Name);
        Assert.Equal("DepartmentName", list[1].Name);
        Assert.Equal("FindByID(Int32 id)", list[2].Name);
        Assert.Equal("FindByName(String name)", list[3].Name);
        Assert.Equal("FindAllByMail(String mail)", list[4].Name);
        Assert.Equal("FindAllByMobile(String mobile)", list[5].Name);
        Assert.Equal("FindAllByCode(String code)", list[6].Name);
        Assert.Equal("FindAllByRoleID(Int32 roleId)", list[7].Name);

        //Assert.Equal(lines.Length, list.Sum(e => e.Lines.Length));
    }
}
