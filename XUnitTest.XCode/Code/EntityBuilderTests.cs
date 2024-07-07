using System;
using System.IO;
using System.Linq;
using NewLife.Log;
using XCode.Code;
using XCode.DataAccessLayer;
using Xunit;

namespace XUnitTest.XCode.Code;

public class EntityBuilderTests
{
    private IDataTable _table;
    private IDataTable _tableLog;
    private BuilderOption _option;

    public EntityBuilderTests()
    {
        _option = new BuilderOption();
        var tables = ClassBuilder.LoadModels(@"..\..\XCode\Membership\Member.xml", _option, out _);
        _table = tables.FirstOrDefault(e => e.Name == "User");
        _tableLog = tables.FirstOrDefault(e => e.Name == "Log");
    }

    private String ReadTarget(String file, String text)
    {
        var target = "";
        var file2 = @"..\..\XUnitTest.XCode\".CombinePath(file);
        if (File.Exists(file2)) target = File.ReadAllText(file2.GetFullPath());

        File.WriteAllText(file2, text);

        //if (!File.Exists(file)) return null;
        //var target = File.ReadAllText(file.GetFullPath());

        return target;
    }

    [Fact]
    public void Normal()
    {
        var option = new EntityBuilderOption
        {
            ConnName = "MyConn",
            Namespace = "Company.MyName",
            //Partial = true,
            Nullable = true,
        };
        option.Usings.Add("NewLife.Remoting");

        var builder = new EntityBuilder
        {
            Table = _table,
            Option = option,
        };

        // 数据类
        builder.Execute();

        var rs = builder.ToString();
        Assert.NotEmpty(rs);

        var target = ReadTarget("Code\\entity_user_normal.cs", rs);
        Assert.Equal(target, rs);

        // 业务类
        builder.Clear();
        builder.Business = true;
        builder.Execute();

        rs = builder.ToString();
        Assert.NotEmpty(rs);

        target = ReadTarget("Code\\entity_user_normal_biz.cs", rs);
        Assert.Equal(target, rs);
    }

    [Fact]
    public void ExtendOnData()
    {
        var option = new EntityBuilderOption
        {
            ConnName = "MyConn",
            Namespace = "Company.MyName",
            //Partial = true,
            Nullable = true,
        };
        option.Usings.Add("NewLife.Remoting");

        var builder = new EntityBuilder
        {
            Table = _tableLog,
            Option = option,
        };

        // 数据类
        builder.Execute();

        var rs = builder.ToString();
        Assert.NotEmpty(rs);

        var target = ReadTarget("Code\\entity_log_normal.cs", rs);
        Assert.Equal(target, rs);

        // 业务类
        builder.Clear();
        builder.Business = true;
        builder.Execute();

        rs = builder.ToString();
        Assert.NotEmpty(rs);

        target = ReadTarget("Code\\entity_log_normal_biz.cs", rs);
        Assert.Equal(target, rs);
    }

    [Fact]
    public void Exclude()
    {
        var option = new EntityBuilderOption
        {
            ConnName = "MyConn",
            Namespace = "Company.MyName",
            //Partial = true,
            Nullable = true,
        };
        option.Usings.Add("NewLife.Remoting");

        var builder = new EntityBuilder
        {
            Table = _table,
            Option = option,
        };

        // 数据类
        builder.Execute();

        var columns = _table.Columns.Where(e => e.Properties["Model"] == "False").ToList();
        Assert.Equal(4, columns.Count);

        var rs = builder.ToString();
        Assert.NotEmpty(rs);

        var target = ReadTarget("Code\\entity_user_normal.cs", rs);
        Assert.Equal(target, rs);

        // 业务类
        builder.Clear();
        builder.Business = true;
        builder.Execute();

        rs = builder.ToString();
        Assert.NotEmpty(rs);

        target = ReadTarget("Code\\entity_user_normal_biz.cs", rs);
        Assert.Equal(target, rs);
    }

    //[Fact]
    //public void GenericType()
    //{
    //    var option = new BuilderOption
    //    {
    //        ConnName = "MyConn",
    //        Namespace = "Company.MyName"
    //    };

    //    var builder = new EntityBuilder
    //    {
    //        Table = _table,
    //        GenericType = true,
    //        Option = option,
    //    };

    //    builder.Execute();

    //    var rs = builder.ToString();
    //    Assert.NotEmpty(rs);

    //    var target = File.ReadAllText("Code\\entity_user_generictype.cs",rs);
    //    Assert.Equal(target, rs);
    //}

    [Fact]
    public void BuildUser()
    {
        var dir = @".\Entity\".GetFullPath();
        if (Directory.Exists(dir)) Directory.Delete(dir, true);

        dir = @".\Output\EntityModels\".GetFullPath();
        if (Directory.Exists(dir)) Directory.Delete(dir, true);

        dir = @".\Output\EntityInterfaces\".GetFullPath();
        if (Directory.Exists(dir)) Directory.Delete(dir, true);

        // 加载模型文件，得到数据表
        var file = @"..\..\XUnitTest.XCode\Code\Member.xml";
        var option = new EntityBuilderOption
        {
            //Partial = true,
        };
        var tables = ClassBuilder.LoadModels(file, option, out var atts);
        EntityBuilder.FixModelFile(file, option, atts, tables);

        option.Nullable = true;

        // 生成实体类
        option.Output = @".\Entity\";
        option.BaseClass = "I{name}";
        option.ModelNameForCopy = "I{name}";
        option.ChineseFileName = true;
        EntityBuilder.BuildTables(tables, option);

        // 生成简易模型类
        option.Output = @"Output\EntityModels\";
        option.ClassNameTemplate = "{name}Model";
        option.ModelNameForCopy = "I{name}";
        ModelBuilder.BuildModels(tables, option);

        // 生成简易接口
        option.BaseClass = null;
        option.ClassNameTemplate = null;
        option.Output = @"Output\EntityInterfaces\";
        InterfaceBuilder.BuildInterfaces(tables, option);

        // 精确控制生成
        /*foreach (var item in tables)
        {
            var builder = new ClassBuilder
            {
                Table = item,
                Option = option,
            };
            builder.Execute();
            builder.Save(null, true, false);
        }*/

        {
            var rs = File.ReadAllText("Entity\\用户.cs".GetFullPath());
            var target = ReadTarget("Code\\Entity\\用户.cs", rs);
            Assert.Equal(target, rs);
        }

        {
            var rs = File.ReadAllText("Entity\\用户.Biz.cs".GetFullPath());
            var target = ReadTarget("Code\\Entity\\用户.Biz.cs", rs);
            Assert.Equal(target, rs);
        }

        {
            var rs = File.ReadAllText("Output\\EntityModels\\UserModel.cs".GetFullPath());
            var target = ReadTarget("Code\\EntityModels\\UserModel.cs", rs);
            Assert.Equal(target, rs);
        }

        {
            var rs = File.ReadAllText("Output\\EntityInterfaces\\IUser.cs".GetFullPath());
            var target = ReadTarget("Code\\EntityInterfaces\\IUser.cs", rs);
            Assert.Equal(target, rs);
        }
    }

    [Fact]
    public void BuildLog()
    {
        var dir = @".\Entity\".GetFullPath();
        if (Directory.Exists(dir)) Directory.Delete(dir, true);

        dir = @".\Output\EntityModels\".GetFullPath();
        if (Directory.Exists(dir)) Directory.Delete(dir, true);

        dir = @".\Output\EntityInterfaces\".GetFullPath();
        if (Directory.Exists(dir)) Directory.Delete(dir, true);

        // 加载模型文件，得到数据表
        var file = @"..\..\XUnitTest.XCode\Code\Member.xml";
        var option = new EntityBuilderOption
        {
            //Partial = true,
        };
        var tables = ClassBuilder.LoadModels(file, option, out var atts);
        EntityBuilder.FixModelFile(file, option, atts, tables);

        option.Nullable = true;

        // 生成实体类
        option.Output = @".\Entity\";
        option.BaseClass = "I{name}";
        option.ModelNameForCopy = "I{name}";
        option.ChineseFileName = true;
        EntityBuilder.BuildTables(tables, option);

        // 生成简易模型类
        option.Output = @"Output\EntityModels\";
        option.ClassNameTemplate = "{name}Model";
        option.ModelNameForCopy = "I{name}";
        ModelBuilder.BuildModels(tables, option);

        // 生成简易接口
        option.BaseClass = null;
        option.ClassNameTemplate = null;
        option.Output = @"Output\EntityInterfaces\";
        InterfaceBuilder.BuildInterfaces(tables, option);

        // 拷贝输出到原始目录，仅测试使用
        foreach (var table in tables)
        {
            {
                var rs = File.ReadAllText($"Entity\\{table.DisplayName}.cs".GetFullPath());
                var target = ReadTarget($"Code\\Entity\\{table.DisplayName}.cs", rs);
            }
            {
                var rs = File.ReadAllText($"Entity\\{table.DisplayName}.Biz.cs".GetFullPath());
                var target = ReadTarget($"Code\\Entity\\{table.DisplayName}.Biz.cs", rs);
            }
            {
                var rs = File.ReadAllText($"Output\\EntityModels\\{table.Name}Model.cs".GetFullPath());
                var target = ReadTarget($"Code\\EntityModels\\{table.Name}Model.cs", rs);
            }
            {
                var rs = File.ReadAllText($"Output\\EntityInterfaces\\I{table.Name}.cs".GetFullPath());
                var target = ReadTarget($"Code\\EntityInterfaces\\I{table.Name}.cs", rs);
            }
        }

        {
            var rs = File.ReadAllText("Entity\\日志.cs".GetFullPath());
            var target = ReadTarget("Code\\Entity\\日志.cs", rs);
            Assert.Equal(target, rs);
        }
        {
            var rs = File.ReadAllText("Entity\\日志.Biz.cs".GetFullPath());
            var target = ReadTarget("Code\\Entity\\日志.Biz.cs", rs);
            Assert.Equal(target, rs);
        }
        {
            var rs = File.ReadAllText("Output\\EntityModels\\LogModel.cs".GetFullPath());
            var target = ReadTarget("Code\\EntityModels\\LogModel.cs", rs);
            Assert.Equal(target, rs);
        }
        {
            var rs = File.ReadAllText("Output\\EntityInterfaces\\ILog.cs".GetFullPath());
            var target = ReadTarget("Code\\EntityInterfaces\\ILog.cs", rs);
            Assert.Equal(target, rs);
        }

        {
            var rs = File.ReadAllText("Entity\\用户日志.cs".GetFullPath());
            var target = ReadTarget("Code\\Entity\\用户日志.cs", rs);
            Assert.Equal(target, rs);
        }
        {
            var rs = File.ReadAllText("Entity\\用户日志.Biz.cs".GetFullPath());
            var target = ReadTarget("Code\\Entity\\用户日志.Biz.cs", rs);
            Assert.Equal(target, rs);

            rs = File.ReadAllText("Output\\EntityInterfaces\\IUserLog.cs".GetFullPath());
            ReadTarget("Code\\EntityInterfaces\\IUserLog.cs", rs);
        }

        {
            var rs = File.ReadAllText("Entity\\成员日志.cs".GetFullPath());
            var target = ReadTarget("Code\\Entity\\成员日志.cs", rs);
            Assert.Equal(target, rs);
        }
        {
            var rs = File.ReadAllText("Entity\\成员日志.Biz.cs".GetFullPath());
            var target = ReadTarget("Code\\Entity\\成员日志.Biz.cs", rs);
            Assert.Equal(target, rs);

            rs = File.ReadAllText("Output\\EntityInterfaces\\IMemberLog.cs".GetFullPath());
            ReadTarget("Code\\EntityInterfaces\\IMemberLog.cs", rs);
        }
    }

    [Fact]
    public void FixModelFile()
    {
        // 加载模型文件，得到数据表
        var file = @"..\..\XUnitTest.XCode\Code\Member.xml";
        var option = new EntityBuilderOption();
        var tables = ClassBuilder.LoadModels(file, option, out var atts);
        EntityBuilder.FixModelFile(file, option, atts, tables);

        //atts["NameFormat"] = "underline";
        option.NameFormat = NameFormats.Underline;
        file = @"..\..\XUnitTest.XCode\Code\Member2.xml";
        EntityBuilder.FixModelFile(file, option, atts, tables);

        var xml = File.ReadAllText(file);
        Assert.Contains("Name", xml);
    }

    [Fact(Skip = "跳过")]
    public void Merge()
    {
        // 加载模型文件，得到数据表
        var file = @"..\..\XUnitTest.XCode\Code\Member.xml";
        var option = new EntityBuilderOption();
        var tables = ClassBuilder.LoadModels(file, option, out var atts);

        // 生成实体类
        option.Output = @".\Entity\";
        option.Nullable = true;

        var builder = new EntityBuilder
        {
            AllTables = tables,
            Option = option.Clone(),
            Log = XTrace.Log,
        };

        builder.Load(tables.FirstOrDefault(e => e.Name == "User"));

        builder.Business = true;
        builder.Execute();
        //builder.Save(null, false, option.ChineseFileName);

        // 该文件需要手工维护，ReadTarget并不会回写
        var fileName = "Code\\Entity\\用户.Biz2.cs".GetBasePath();
        builder.Merge(fileName);

        {
            var rs = File.ReadAllText(fileName);
            var target = ReadTarget("Code\\Entity\\用户.Biz.cs", rs);
            //Assert.Equal(target, rs);

            // 扩展查询部分，由于插入在后面，无法进行相等比较
            var p1 = rs.IndexOf("#region 高级查询");
            var p2 = target.IndexOf("#region 高级查询");
            var str1 = rs.Substring(0, p1);
            var str2 = target.Substring(0, p2);
            Assert.Equal(str2, str1);
            //Assert.Contains("FindByName(String name)", rs);
            //Assert.Contains("FindAllByMail(String mail)", rs);
        }
    }
}
