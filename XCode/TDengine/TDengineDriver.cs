/*
 * Copyright (c) 2019 TAOS Data, Inc. <jhtao@taosdata.com>
 *
 * This program is free software: you can use, redistribute, and/or modify
 * it under the terms of the GNU Affero General Public License, version 3
 * or later ("AGPL"), as published by the Free Software Foundation.
 *
 * This program is distributed in the hope that it will be useful, but WITHOUT
 * ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or
 * FITNESS FOR A PARTICULAR PURPOSE.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using NewLife;
using NewLife.Serialization;

namespace TDengineDriver;

/// <summary>TDengine数据类型</summary>
enum TDengineDataType
{
    TSDB_DATA_TYPE_NULL = 0,     // 1 bytes
    TSDB_DATA_TYPE_BOOL = 1,     // 1 bytes
    TSDB_DATA_TYPE_TINYINT = 2,  // 1 bytes
    TSDB_DATA_TYPE_SMALLINT = 3, // 2 bytes
    TSDB_DATA_TYPE_INT = 4,      // 4 bytes
    TSDB_DATA_TYPE_BIGINT = 5,   // 8 bytes
    TSDB_DATA_TYPE_FLOAT = 6,    // 4 bytes
    TSDB_DATA_TYPE_DOUBLE = 7,   // 8 bytes
    TSDB_DATA_TYPE_BINARY = 8,   // string
    TSDB_DATA_TYPE_TIMESTAMP = 9,// 8 bytes
    TSDB_DATA_TYPE_NCHAR = 10,   // unicode string
    TSDB_DATA_TYPE_UTINYINT = 11,// 1 byte
    TSDB_DATA_TYPE_USMALLINT = 12,// 2 bytes
    TSDB_DATA_TYPE_UINT = 13,    // 4 bytes
    TSDB_DATA_TYPE_UBIGINT = 14   // 8 bytes
}

/// <summary>字段元数据</summary>
class TDengineMeta
{
    /// <summary>字段名</summary>
    public String name = String.Empty;
    
    /// <summary>字段大小</summary>
    public Int16 size;
    
    /// <summary>字段类型</summary>
    public Byte type;
    
    /// <summary>获取类型名称</summary>
    public String TypeName()
    {
        switch ((TDengineDataType)type)
        {
            case TDengineDataType.TSDB_DATA_TYPE_BOOL:
                return "BOOL";
            case TDengineDataType.TSDB_DATA_TYPE_TINYINT:
                return "TINYINT";
            case TDengineDataType.TSDB_DATA_TYPE_SMALLINT:
                return "SMALLINT";
            case TDengineDataType.TSDB_DATA_TYPE_INT:
                return "INT";
            case TDengineDataType.TSDB_DATA_TYPE_BIGINT:
                return "BIGINT";
            case TDengineDataType.TSDB_DATA_TYPE_UTINYINT:
                return "TINYINT UNSIGNED";
            case TDengineDataType.TSDB_DATA_TYPE_USMALLINT:
                return "SMALLINT UNSIGNED";
            case TDengineDataType.TSDB_DATA_TYPE_UINT:
                return "INT UNSIGNED";
            case TDengineDataType.TSDB_DATA_TYPE_UBIGINT:
                return "BIGINT UNSIGNED";
            case TDengineDataType.TSDB_DATA_TYPE_FLOAT:
                return "FLOAT";
            case TDengineDataType.TSDB_DATA_TYPE_DOUBLE:
                return "DOUBLE";
            case TDengineDataType.TSDB_DATA_TYPE_BINARY:
                return "STRING";
            case TDengineDataType.TSDB_DATA_TYPE_TIMESTAMP:
                return "TIMESTAMP";
            case TDengineDataType.TSDB_DATA_TYPE_NCHAR:
                return "NCHAR";
            default:
                return "undefine";
        }
    }
}

/// <summary>查询结果</summary>
class TDengineResult
{
    /// <summary>状态</summary>
    public String? status { get; set; }
    
    /// <summary>列名</summary>
    public List<String>? head { get; set; }
    
    /// <summary>列类型</summary>
    public List<String>? column_meta { get; set; }
    
    /// <summary>数据</summary>
    public List<List<Object>>? data { get; set; }
    
    /// <summary>行数</summary>
    public Int32 rows { get; set; }
    
    /// <summary>错误码</summary>
    public Int32 code { get; set; }
    
    /// <summary>错误描述</summary>
    public String? desc { get; set; }
    
    /// <summary>当前数据行索引</summary>
    public Int32 CurrentRowIndex { get; set; } = -1;
}

/// <summary>TDengine连接信息</summary>
class TDengineConnectionInfo
{
    /// <summary>连接标识</summary>
    public Int64 Id { get; set; }
    
    /// <summary>服务器地址</summary>
    public String Server { get; set; } = String.Empty;
    
    /// <summary>端口</summary>
    public Int32 Port { get; set; }
    
    /// <summary>用户名</summary>
    public String User { get; set; } = String.Empty;
    
    /// <summary>密码</summary>
    public String Password { get; set; } = String.Empty;
    
    /// <summary>数据库</summary>
    public String Database { get; set; } = String.Empty;
    
    /// <summary>HTTP客户端</summary>
    public HttpClient? Client { get; set; }
    
    /// <summary>服务器版本</summary>
    public String ServerVersion { get; set; } = "3.0";
}

/// <summary>TDengine HTTP驱动</summary>
class TDengine
{
    /// <summary>成功代码</summary>
    public const Int32 TSDB_CODE_SUCCESS = 0;
    
    private static Int64 _connectionIdSeed = 0;
    private static readonly ConcurrentDictionary<Int64, TDengineConnectionInfo> _connections = new();
    private static readonly ConcurrentDictionary<Int64, TDengineResult> _results = new();
    private static Int64 _resultIdSeed = 0;
    
    /// <summary>初始化</summary>
    static public void Init() { }
    
    /// <summary>清理</summary>
    static public void Cleanup()
    {
        foreach (var conn in _connections.Values)
        {
            conn.Client?.Dispose();
        }
        _connections.Clear();
        _results.Clear();
    }
    
    /// <summary>设置选项（HTTP模式下不需要）</summary>
    static public void Options(Int32 option, String value) { }
    
    /// <summary>连接数据库</summary>
    static public IntPtr Connect(String ip, String user, String password, String db, Int16 port)
    {
        if (port <= 0) port = 6041; // HTTP默认端口
        
        var connId = System.Threading.Interlocked.Increment(ref _connectionIdSeed);
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        
        // 设置Basic认证
        var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{user}:{password}"));
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", auth);
        
        var conn = new TDengineConnectionInfo
        {
            Id = connId,
            Server = ip,
            Port = port,
            User = user,
            Password = password,
            Database = db,
            Client = client
        };
        
        _connections[connId] = conn;
        
        // 获取服务器版本
        try
        {
            var result = QuerySync(new IntPtr(connId), "SELECT SERVER_VERSION()");
            if (result != IntPtr.Zero && _results.TryGetValue(result.ToInt64(), out var res))
            {
                if (res.data?.Count > 0 && res.data[0]?.Count > 0)
                {
                    conn.ServerVersion = res.data[0][0]?.ToString() ?? "3.0";
                }
                FreeResult(result);
            }
        }
        catch { conn.ServerVersion = "3.0"; }
        
        return new IntPtr(connId);
    }
    
    /// <summary>获取错误信息</summary>
    static public String Error(IntPtr res)
    {
        if (res == IntPtr.Zero) return "Unknown error";
        
        if (_results.TryGetValue(res.ToInt64(), out var result))
        {
            return result.desc ?? "Unknown error";
        }
        
        return "Unknown error";
    }
    
    /// <summary>获取错误码</summary>
    static public Int32 ErrorNo(IntPtr res)
    {
        if (res == IntPtr.Zero) return -1;
        
        if (_results.TryGetValue(res.ToInt64(), out var result))
        {
            return result.code;
        }
        
        return -1;
    }
    
    /// <summary>执行查询（同步）</summary>
    static private IntPtr QuerySync(IntPtr conn, String sqlstr)
    {
        if (conn == IntPtr.Zero) return IntPtr.Zero;
        
        if (!_connections.TryGetValue(conn.ToInt64(), out var connInfo))
            return IntPtr.Zero;
        
        try
        {
            var url = $"http://{connInfo.Server}:{connInfo.Port}/rest/sql";
            if (!connInfo.Database.IsNullOrEmpty())
                url = $"http://{connInfo.Server}:{connInfo.Port}/rest/sql/{connInfo.Database}";
            
            var content = new StringContent(sqlstr, Encoding.UTF8, "text/plain");
            var response = connInfo.Client.PostAsync(url, content).Result;
            var json = response.Content.ReadAsStringAsync().Result;
            
            var result = json.ToJsonEntity<TDengineResult>();
            if (result == null)
            {
                result = new TDengineResult { code = -1, desc = "解析响应失败" };
            }
            
            var resId = System.Threading.Interlocked.Increment(ref _resultIdSeed);
            _results[resId] = result;
            
            return new IntPtr(resId);
        }
        catch (Exception ex)
        {
            var result = new TDengineResult { code = -1, desc = ex.Message };
            var resId = System.Threading.Interlocked.Increment(ref _resultIdSeed);
            _results[resId] = result;
            return new IntPtr(resId);
        }
    }
    
    /// <summary>执行查询</summary>
    static public IntPtr Query(IntPtr conn, String sqlstr) => QuerySync(conn, sqlstr);
    
    /// <summary>获取影响行数</summary>
    static public Int32 AffectRows(IntPtr res)
    {
        if (res == IntPtr.Zero) return 0;
        
        if (_results.TryGetValue(res.ToInt64(), out var result))
        {
            return result.rows;
        }
        
        return 0;
    }
    
    /// <summary>获取字段数量</summary>
    static public Int32 FieldCount(IntPtr res)
    {
        if (res == IntPtr.Zero) return 0;
        
        if (_results.TryGetValue(res.ToInt64(), out var result))
        {
            return result.head?.Count ?? 0;
        }
        
        return 0;
    }
    
    /// <summary>获取字段元数据</summary>
    static public List<TDengineMeta> FetchFields(IntPtr res)
    {
        var metas = new List<TDengineMeta>();
        if (res == IntPtr.Zero) return metas;
        
        if (_results.TryGetValue(res.ToInt64(), out var result))
        {
            if (result.head != null)
            {
                for (var i = 0; i < result.head.Count; i++)
                {
                    var meta = new TDengineMeta
                    {
                        name = result.head[i],
                        type = GetTypeFromColumnMeta(result, i),
                        size = GetSizeFromColumnMeta(result, i)
                    };
                    metas.Add(meta);
                }
            }
        }
        
        return metas;
    }
    
    /// <summary>获取下一行数据</summary>
    static public IntPtr FetchRows(IntPtr res)
    {
        if (res == IntPtr.Zero) return IntPtr.Zero;
        
        if (_results.TryGetValue(res.ToInt64(), out var result))
        {
            if (result.data != null && result.CurrentRowIndex < result.data.Count - 1)
            {
                result.CurrentRowIndex++;
                return new IntPtr(result.CurrentRowIndex + 1); // 返回非零表示有数据
            }
        }
        
        return IntPtr.Zero; // 没有更多数据
    }
    
    /// <summary>获取当前行的列数据指针（用于DataReader）</summary>
    static public List<Object>? GetCurrentRow(IntPtr res)
    {
        if (res == IntPtr.Zero) return null;
        
        if (_results.TryGetValue(res.ToInt64(), out var result))
        {
            if (result.data != null && result.CurrentRowIndex >= 0 && result.CurrentRowIndex < result.data.Count)
            {
                return result.data[result.CurrentRowIndex];
            }
        }
        
        return null;
    }
    
    /// <summary>从列元数据获取类型</summary>
    private static Byte GetTypeFromColumnMeta(TDengineResult result, Int32 index)
    {
        if (result.column_meta == null || index >= result.column_meta.Count)
        {
            // 根据数据推断类型
            if (result.data != null && result.data.Count > 0 && index < result.data[0].Count)
            {
                var val = result.data[0][index];
                if (val == null) return (Byte)TDengineDataType.TSDB_DATA_TYPE_NULL;
                if (val is Boolean) return (Byte)TDengineDataType.TSDB_DATA_TYPE_BOOL;
                if (val is SByte) return (Byte)TDengineDataType.TSDB_DATA_TYPE_TINYINT;
                if (val is Int16) return (Byte)TDengineDataType.TSDB_DATA_TYPE_SMALLINT;
                if (val is Int32) return (Byte)TDengineDataType.TSDB_DATA_TYPE_INT;
                if (val is Int64) return (Byte)TDengineDataType.TSDB_DATA_TYPE_BIGINT;
                if (val is Single) return (Byte)TDengineDataType.TSDB_DATA_TYPE_FLOAT;
                if (val is Double) return (Byte)TDengineDataType.TSDB_DATA_TYPE_DOUBLE;
                if (val is String) return (Byte)TDengineDataType.TSDB_DATA_TYPE_NCHAR;
            }
            return (Byte)TDengineDataType.TSDB_DATA_TYPE_NCHAR;
        }
        
        var typeStr = result.column_meta[index];
        return typeStr?.ToUpper() switch
        {
            "BOOL" => (Byte)TDengineDataType.TSDB_DATA_TYPE_BOOL,
            "TINYINT" => (Byte)TDengineDataType.TSDB_DATA_TYPE_TINYINT,
            "SMALLINT" => (Byte)TDengineDataType.TSDB_DATA_TYPE_SMALLINT,
            "INT" => (Byte)TDengineDataType.TSDB_DATA_TYPE_INT,
            "BIGINT" => (Byte)TDengineDataType.TSDB_DATA_TYPE_BIGINT,
            "FLOAT" => (Byte)TDengineDataType.TSDB_DATA_TYPE_FLOAT,
            "DOUBLE" => (Byte)TDengineDataType.TSDB_DATA_TYPE_DOUBLE,
            "BINARY" => (Byte)TDengineDataType.TSDB_DATA_TYPE_BINARY,
            "TIMESTAMP" => (Byte)TDengineDataType.TSDB_DATA_TYPE_TIMESTAMP,
            "NCHAR" => (Byte)TDengineDataType.TSDB_DATA_TYPE_NCHAR,
            _ => (Byte)TDengineDataType.TSDB_DATA_TYPE_NCHAR,
        };
    }
    
    /// <summary>从列元数据获取大小</summary>
    private static Int16 GetSizeFromColumnMeta(TDengineResult result, Int32 index)
    {
        // HTTP模式下返回的数据不包含精确的字段大小，使用默认值
        if (result.column_meta == null || index >= result.column_meta.Count)
            return 256;
        
        return 256;
    }
    
    /// <summary>释放结果</summary>
    static public IntPtr FreeResult(IntPtr res)
    {
        if (res != IntPtr.Zero)
        {
            _results.TryRemove(res.ToInt64(), out _);
        }
        return IntPtr.Zero;
    }
    
    /// <summary>关闭连接</summary>
    static public Int32 Close(IntPtr taos)
    {
        if (taos != IntPtr.Zero && _connections.TryRemove(taos.ToInt64(), out var conn))
        {
            conn.Client?.Dispose();
            return 0;
        }
        return -1;
    }
    
    /// <summary>获取客户端版本</summary>
    public static String GetClientInfo() => "3.0 (HTTP)";
    
    /// <summary>获取服务器版本</summary>
    public static String GetServerInfo(IntPtr taos)
    {
        if (taos != IntPtr.Zero && _connections.TryGetValue(taos.ToInt64(), out var conn))
        {
            return conn.ServerVersion ?? "3.0";
        }
        return "3.0";
    }
    
    /// <summary>选择数据库</summary>
    public static Int32 SelectDatabase(IntPtr taos, String db)
    {
        if (taos != IntPtr.Zero && _connections.TryGetValue(taos.ToInt64(), out var conn))
        {
            conn.Database = db;
            return 0;
        }
        return -1;
    }
}
