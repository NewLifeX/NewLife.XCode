using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using NewLife.Data;

namespace XCode.DataAccessLayer;

/// <summary>SonnetDB关系数据库适配，支持嵌入式目录和远程服务。</summary>
internal class SonnetDB : RemoteDb
{
    /// <inheritdoc/>
    public override DatabaseType Type => DatabaseType.SonnetDB;

    /// <inheritdoc/>
    public override BatchCapability BatchCapability => BatchCapability.Insert | BatchCapability.InsertIgnore | BatchCapability.Upsert | BatchCapability.Replace;

    /// <inheritdoc/>
    public override String SystemDatabaseName => String.Empty;

    /// <summary>使用SonnetDB 5.0原生参数、精确数值与DATETIME2时间合同。</summary>
    public SonnetDB() => UseParameter = true;

    protected override DbProviderFactory? CreateFactory() => GetProviderFactory(null, "SonnetDB.Data.dll", "SonnetDB.Data.SndbProviderFactory");
    protected override IDbSession OnCreateSession() => new SonnetDBSession(this);
    protected override IMetaData OnCreateMetaData() => new SonnetDBMetaData();

    /// <inheritdoc/>
    public override Boolean Support(String providerName) => providerName.IndexOf("SonnetDB", StringComparison.OrdinalIgnoreCase) >= 0;

    protected override void OnSetConnectionString(ConnectionStringBuilder builder)
    {
        base.OnSetConnectionString(builder);
        if (String.IsNullOrEmpty(DatabaseName))
        {
            var source = builder["Data Source"];
            // 嵌入式目录允许sonnetdb://前缀，官方驱动会剥离该前缀，数据库名必须与打开的目录保持一致
            var embedded = source?.StartsWith("sonnetdb://", StringComparison.OrdinalIgnoreCase) == true ? source["sonnetdb://".Length..] : source;
            var uriSource = embedded?.StartsWith("sonnetdb+", StringComparison.OrdinalIgnoreCase) == true ? embedded["sonnetdb+".Length..] : embedded;
            DatabaseName = Uri.TryCreate(uriSource, UriKind.Absolute, out var uri) && (uri.Scheme == "http" || uri.Scheme == "https")
                ? Uri.UnescapeDataString(uri.AbsolutePath.Trim('/')) : embedded;
        }
    }

    internal Boolean IsRemote
    {
        get
        {
            var builder = new ConnectionStringBuilder(ConnectionString);
            var mode = builder["Mode"];
            if (!String.IsNullOrEmpty(mode)) return mode.Equals("Remote", StringComparison.OrdinalIgnoreCase);
            if (new[] { "Host", "Username", "User ID", "UserId", "Uid" }.Any(key => !String.IsNullOrWhiteSpace(builder[key]))) return true;
            var source = builder["Data Source"] ?? String.Empty;
            return new[] { "sonnetdb+http://", "sonnetdb+https://", "http://", "https://" }.Any(prefix => source.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <inheritdoc/>
    public override String FormatKeyWord(String keyWord) => String.IsNullOrEmpty(keyWord) || keyWord.StartsWith("\"") && keyWord.EndsWith("\"") ? keyWord : $"\"{keyWord.Replace("\"", "\"\"")}\"";

    /// <inheritdoc/>
    public override String FormatName(String name) => FormatKeyWord(base.FormatName(name));

    /// <inheritdoc/>
    public override String PageSplit(String sql, Int64 startRowIndex, Int64 maximumRows, String? keyColumn) => sql + Pagination(startRowIndex, maximumRows);

    /// <inheritdoc/>
    public override SelectBuilder PageSplit(SelectBuilder builder, Int64 startRowIndex, Int64 maximumRows)
    {
        var pagination = Pagination(startRowIndex, maximumRows);
        if (pagination.Length > 0) builder.Limit = pagination.TrimStart();
        return builder;
    }

    private static String Pagination(Int64 startRowIndex, Int64 maximumRows)
    {
        var sql = maximumRows > 0 ? $" LIMIT {maximumRows}" : String.Empty;
        if (startRowIndex > 0) sql += $" OFFSET {startRowIndex}";
        return sql;
    }

    /// <inheritdoc/>
    public override String StringConcat(String left, String right) => $"CONCAT({(String.IsNullOrEmpty(left) ? "''" : left)},{(String.IsNullOrEmpty(right) ? "''" : right)})";

    /// <inheritdoc/>
    public override String FormatValue(IDataColumn column, Object? value)
    {
        if (value == null || value == DBNull.Value) return "NULL";
        var type = column?.DataType ?? value.GetType();
        type = Nullable.GetUnderlyingType(type) ?? type;
        var underlying = type.IsEnum ? Enum.GetUnderlyingType(type) : type;

        // 外部文本先转换为列声明类型，转换失败直接抛错，避免未转义文本拼入SQL字面量
        if (value is String text && underlying != typeof(String) && underlying != typeof(Char))
        {
            value = underlying == typeof(DateTime) ? Convert.ToDateTime(text, CultureInfo.InvariantCulture)
                : underlying == typeof(DateTimeOffset) ? DateTimeOffset.Parse(text, CultureInfo.InvariantCulture)
                : underlying == typeof(TimeSpan) ? TimeSpan.Parse(text, CultureInfo.InvariantCulture)
                : underlying == typeof(Guid) ? Guid.Parse(text)
                : Convert.ChangeType(text, underlying, CultureInfo.InvariantCulture);
        }
        CheckValue(value, underlying);
        if (type.IsEnum) return Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
        if (type == typeof(Boolean)) return Convert.ToBoolean(value, CultureInfo.InvariantCulture) ? "TRUE" : "FALSE";
        if (value is Byte[] bytes) return Quote(Convert.ToBase64String(bytes));
        if (type == typeof(DateTime) || value is DateTime) return FormatDateTime(column!, Convert.ToDateTime(value, CultureInfo.InvariantCulture));
        if (type == typeof(DateTimeOffset) || value is DateTimeOffset)
        {
            var offset = value is DateTimeOffset dateOffset ? dateOffset : DateTimeOffset.Parse(value.ToString()!, CultureInfo.InvariantCulture);
            CheckValue(offset, typeof(DateTimeOffset));
            return "CAST(" + Quote(offset.ToString("O", CultureInfo.InvariantCulture)) + " AS DATETIMEOFFSET)";
        }
        if (type == typeof(TimeSpan) || value is TimeSpan)
        {
            var span = value is TimeSpan timeSpan ? timeSpan : TimeSpan.Parse(value.ToString()!, CultureInfo.InvariantCulture);
            return "CAST(" + Quote(span.ToString("c", CultureInfo.InvariantCulture)) + " AS DURATION)";
        }
        if (type == typeof(Guid) || value is Guid) return Quote(Guid.Parse(value.ToString()!).ToString("D"));
        if (type == typeof(String) || type == typeof(Char)) return Quote(Convert.ToString(value, CultureInfo.InvariantCulture)!);
        if (value is Decimal number) return number.ToString(CultureInfo.InvariantCulture);
        if (value is Double dbl) return dbl.ToString("R", CultureInfo.InvariantCulture);
        if (value is Single single) return single.ToString("R", CultureInfo.InvariantCulture);
        // 到这里只剩整数等无边界的数值，其他类型按文本加引号，禁止再输出未转义字面量
        if (value is SByte or Byte or Int16 or Int32 or Int64 or UInt16 or UInt32 or UInt64) return Convert.ToString(value, CultureInfo.InvariantCulture)!;
        return Quote(Convert.ToString(value, CultureInfo.InvariantCulture)!);
    }

    internal static String Quote(String value) => "'" + value.Replace("'", "''") + "'";

    /// <inheritdoc/>
    public override String FormatDateTime(DateTime dateTime) => FormatDateTime(null!, dateTime);

    /// <inheritdoc/>
    public override String FormatDateTime(IDataColumn column, DateTime dateTime)
    {
        CheckValue(dateTime, typeof(DateTime));
        if (dateTime.Kind == DateTimeKind.Local) dateTime = DateTime.SpecifyKind(dateTime, DateTimeKind.Unspecified);
        return "CAST(" + Quote(dateTime.ToString("O", CultureInfo.InvariantCulture)) + " AS DATETIME2)";
    }

    internal static void CheckValue(Object value, Type? type)
    {
        if (value == DBNull.Value) return;
        if ((type == typeof(UInt64) || value is UInt64) && Convert.ToUInt64(value, CultureInfo.InvariantCulture) > Int64.MaxValue)
            throw new NotSupportedException(L.T("SonnetDB INT uses signed Int64 and supports only UInt64 values up to Int64.MaxValue."));
    }

    /// <inheritdoc/>
    public override IDataParameter CreateParameter(String name, Object? value, Type? type = null)
    {
        type = type == null ? value?.GetType() : Nullable.GetUnderlyingType(type) ?? type;
        if (type?.IsEnum == true)
        {
            type = Enum.GetUnderlyingType(type);
            if (value != null && value != DBNull.Value) value = Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
        }
        if (value != null) CheckValue(value, type);
        if (type == typeof(SByte) || type == typeof(UInt16) || type == typeof(UInt32) || type == typeof(UInt64))
        {
            if (value != null && value != DBNull.Value) value = Convert.ToInt64(value, CultureInfo.InvariantCulture);
            type = typeof(Int64);
        }
        if (type == typeof(Guid))
        {
            if (value != null && value != DBNull.Value) value = value.ToString();
            type = typeof(String);
        }
        else if (type == typeof(Char))
        {
            if (value != null && value != DBNull.Value) value = value.ToString();
            type = typeof(String);
        }
        else if (type == typeof(DateTimeOffset))
        {
            var parameter = Factory.CreateParameter()!;
            parameter.ParameterName = FormatParameterName(name);
            parameter.SourceColumn = name;
            parameter.DbType = DbType.DateTimeOffset;
            parameter.Value = value ?? DBNull.Value;
            return parameter;
        }
        else if (type == typeof(TimeSpan))
        {
            var parameter = Factory.CreateParameter()!;
            parameter.ParameterName = FormatParameterName(name);
            parameter.SourceColumn = name;
            parameter.DbType = DbType.Object;
            parameter.Value = value ?? DBNull.Value;
            return parameter;
        }
        if (value is DateTime { Kind: DateTimeKind.Local } local) value = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        var result = base.CreateParameter(name, value, type);
        if (type == typeof(DateTime)) result.DbType = DbType.DateTime2;
        if (value == null || value == DBNull.Value) result.Value = DBNull.Value;
        return result;
    }

    /// <inheritdoc/>
    public override String? BuildDeleteSql(String tableName, String where, Int32 batchSize)
    {
        if (batchSize <= 0) return base.BuildDeleteSql(tableName, where, batchSize);
        var condition = String.IsNullOrWhiteSpace(where) ? String.Empty : " WHERE " + where;
        return $"DELETE FROM {FormatName(tableName)}{condition} LIMIT {batchSize}";
    }
}

/// <summary>SonnetDB会话，使用ADO读取器执行查询。</summary>
internal class SonnetDBSession : RemoteDbSession
{
    public SonnetDBSession(IDatabase database) : base(database) { }

    // 普通读取使用既有会话扩展点，保留数值NULL，不修改共享DbSession。
    protected override DbTable OnFill(DbDataReader reader)
    {
        var result = new DbTable();
        result.ReadHeader(reader);
        while (reader.Read()) result.Rows.Add(ReadNullableRow(reader));
        result.Total = result.Rows.Count;
        return result;
    }

    /// <inheritdoc/>
    public override async Task<DbTable> QueryAsync(String sql, IDataParameter[]? ps)
    {
        using var cmd = OnCreateCommand(sql, CommandType.Text, ps);
        return await ExecuteAsync(cmd, true, async command =>
        {
            using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            var result = new DbTable();
            result.ReadHeader(reader);
            while (await reader.ReadAsync().ConfigureAwait(false)) result.Rows.Add(ReadNullableRow(reader));
            result.Total = result.Rows.Count;
            return result;
        }).ConfigureAwait(false);
    }

    internal static Object?[] ReadNullableRow(DbDataReader reader)
    {
        var row = new Object?[reader.FieldCount];
        for (var i = 0; i < row.Length; i++) row[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
        return row;
    }


    /// <inheritdoc/>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "SonnetDB驱动不提供DataAdapter，采用DataReader装配单结果集；仅在裁剪下需要DataTable相关成员，数据库访问本身依赖运行时驱动加载，不承诺裁剪后可用")]
    public override DataSet Query(DbCommand cmd) => Execute(cmd, true, command =>
    {
        using var reader = command.ExecuteReader();
        var result = new DataSet();
        var table = new DataTable();
        table.Load(reader);
        result.Tables.Add(table);
        return result;
    });

    /// <inheritdoc/>
    public override Int32 BeginTransaction(IsolationLevel level)
    {
        if (level != IsolationLevel.Unspecified && level != IsolationLevel.ReadCommitted && level != IsolationLevel.Serializable)
            throw new NotSupportedException(L.T("SonnetDB supports ReadCommitted and Serializable transaction isolation levels; other levels require an explicit supported mapping."));
        return base.BeginTransaction(level);
    }

    protected override DbCommand OnCreateCommand(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
    {
        // 仅含自增字段的实体由XCode生成空字段/值列表，SonnetDB原生语法使用DEFAULT VALUES。
        if (type == CommandType.Text && Regex.IsMatch(sql, @"^\s*INSERT\s+INTO\b", RegexOptions.IgnoreCase))
            sql = Regex.Replace(sql, @"\(\s*\)\s*VALUES\s*\(\s*\)(?=\s*(?:RETURNING\b|;|$))", "DEFAULT VALUES", RegexOptions.IgnoreCase);
        return base.OnCreateCommand(sql, type, ps);
    }

    /// <inheritdoc/>
    public override Int64 InsertAndGetIdentity(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
    {
        using var cmd = OnCreateCommand(WithReturning(sql), type, ps);
        return Execute(cmd, false, command =>
        {
            using var reader = command.ExecuteReader();
            var ordinal = IdentityOrdinal(reader);
            return reader.Read() && !reader.IsDBNull(ordinal) ? Convert.ToInt64(reader.GetValue(ordinal), CultureInfo.InvariantCulture) : 0;
        });
    }

    /// <inheritdoc/>
    public override async Task<Int64> InsertAndGetIdentityAsync(String sql, CommandType type = CommandType.Text, params IDataParameter[]? ps)
    {
        using var cmd = OnCreateCommand(WithReturning(sql), type, ps);
        return await ExecuteAsync(cmd, false, async command =>
        {
            using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            var ordinal = IdentityOrdinal(reader);
            return await reader.ReadAsync().ConfigureAwait(false) && !reader.IsDBNull(ordinal) ? Convert.ToInt64(reader.GetValue(ordinal), CultureInfo.InvariantCulture) : 0;
        }).ConfigureAwait(false);
    }

    private static String WithReturning(String sql) => sql.TrimEnd().TrimEnd(';') + " RETURNING *";

    private static Int32 IdentityOrdinal(DbDataReader reader)
    {
        var schema = reader.GetSchemaTable();
        if (schema != null && schema.Columns.Contains(SchemaTableOptionalColumn.IsAutoIncrement))
        {
            foreach (DataRow row in schema.Rows)
                if (row[SchemaTableOptionalColumn.IsAutoIncrement] is Boolean identity && identity)
                    return Convert.ToInt32(row[SchemaTableColumn.ColumnOrdinal], CultureInfo.InvariantCulture);
        }
        throw new NotSupportedException(L.T("The SonnetDB result does not identify identity columns, so the generated number cannot be obtained safely. Upgrade to a SonnetDB version that includes RETURNING column metadata."));
    }

    /// <inheritdoc/>
    public override Int32 Insert(IDataTable table, IDataColumn[] columns, IEnumerable<IModel> list) => ExecuteBatch(table, columns, list, null, null, false);

    /// <inheritdoc/>
    public override Int32 InsertIgnore(IDataTable table, IDataColumn[] columns, IEnumerable<IModel> list) => ExecuteBatch(table, columns, list, null, null, true);

    /// <inheritdoc/>
    public override Int32 Replace(IDataTable table, IDataColumn[] columns, IEnumerable<IModel> list) => ExecuteBatch(table, columns, list, null, null, false, true);

    /// <inheritdoc/>
    public override Int32 Upsert(IDataTable table, IDataColumn[] columns, ICollection<String>? updateColumns, ICollection<String>? addColumns, IEnumerable<IModel> list) => ExecuteBatch(table, columns, list, updateColumns, addColumns, false);

    private Int32 ExecuteBatch(IDataTable table, IDataColumn[] columns, IEnumerable<IModel> list, ICollection<String>? updateColumns, ICollection<String>? addColumns, Boolean ignore, Boolean replace = false)
    {
        var rows = list.ToArray();
        if (rows.Length == 0) return 0;
        columns ??= table.Columns.ToArray();
        if (columns.Length == 0) throw new ArgumentException(L.T("Bulk write must contain at least one field."), nameof(columns));

        var db = (SonnetDB)Database;
        var sql = new StringBuilder();
        sql.Append(replace ? "REPLACE INTO " : "INSERT INTO ").Append(db.FormatName(table)).Append('(').Append(String.Join(",", columns.Select(db.FormatName))).Append(") VALUES ");
        var parameters = new List<IDataParameter>(rows.Length * columns.Length);
        for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++)
        {
            if (rowIndex > 0) sql.Append(',');
            sql.Append('(');
            for (var columnIndex = 0; columnIndex < columns.Length; columnIndex++)
            {
                if (columnIndex > 0) sql.Append(',');
                var column = columns[columnIndex];
                var name = "p" + parameters.Count;
                sql.Append(db.FormatParameterName(name));
                parameters.Add(db.CreateParameter(name, rows[rowIndex][column.Name], column));
            }
            sql.Append(')');
        }
        if (ignore) sql.Append(" ON CONFLICT DO NOTHING");
        else if (updateColumns is { Count: > 0 } || addColumns is { Count: > 0 })
        {
            // 索引列可能声明为逻辑属性名，先解析为物理列名再与写入列匹配，否则会错误回退到自增主键
            var indexColumns = table.Indexes
                .Where(item => item.Unique && item.Columns.Length > 0)
                .Select(item => new { item.Columns, Resolved = table.GetColumns(item.Columns) })
                .Where(item => item.Resolved.Length == item.Columns.Length
                    && item.Resolved.All(column => columns.Any(inserted => inserted.ColumnName.Equals(column.ColumnName, StringComparison.OrdinalIgnoreCase))))
                .Select(item => item.Resolved.Select(column => column.ColumnName).ToArray())
                .FirstOrDefault();
            var keys = indexColumns ?? table.PrimaryKeys.Select(column => column.ColumnName).ToArray();
            if (keys.Length == 0) throw new NotSupportedException(L.T("SonnetDB Upsert requires a primary key or a complete unique-index conflict target."));
            var setters = new List<String>();
            foreach (var column in columns)
            {
                if (column.Identity || column.PrimaryKey || keys.Contains(column.ColumnName, StringComparer.OrdinalIgnoreCase)) continue;
                var name = db.FormatName(column);
                if (addColumns?.Contains(column.Name) == true) setters.Add($"{name}={name}+excluded.{name}");
                else if (updateColumns?.Contains(column.Name) == true) setters.Add($"{name}=excluded.{name}");
            }
            sql.Append(" ON CONFLICT (").Append(String.Join(",", table.GetColumns(keys).Select(db.FormatName))).Append(") ");
            sql.Append(setters.Count == 0 ? "DO NOTHING" : "DO UPDATE SET " + String.Join(",", setters));
        }
        return Execute(sql.ToString(), CommandType.Text, parameters.ToArray());
    }
}

/// <summary>读取SonnetDB原生关系表元数据并生成建模SQL。</summary>
internal class SonnetDBMetaData : RemoteDbMetaData
{
    public SonnetDBMetaData() => Types = new Dictionary<Type, String[]>
    {
        [typeof(Int64)] = ["INT"], [typeof(Int32)] = ["INT"], [typeof(Int16)] = ["INT"],
        [typeof(Byte)] = ["INT"], [typeof(SByte)] = ["INT"], [typeof(UInt16)] = ["INT"], [typeof(UInt32)] = ["INT"], [typeof(UInt64)] = ["INT"],
        [typeof(Double)] = ["FLOAT"], [typeof(Single)] = ["FLOAT"], [typeof(Decimal)] = ["DECIMAL"],
        [typeof(Boolean)] = ["BOOL"], [typeof(String)] = ["STRING"], [typeof(Guid)] = ["STRING"], [typeof(Char)] = ["STRING"],
        [typeof(DateTime)] = ["DATETIME2"], [typeof(DateTimeOffset)] = ["DATETIMEOFFSET"], [typeof(TimeSpan)] = ["DURATION"], [typeof(Byte[])] = ["BLOB"],
    };

    /// <inheritdoc/>
    public override IList<String> GetTableNames() => GetSchema("Tables", null)!.Rows.Cast<DataRow>().Where(IsRelational).Select(row => (String)row["TABLE_NAME"]).ToList();

    private static Boolean IsRelational(DataRow row) => (String)row["TABLE_TYPE"] is "BASE TABLE" or "VIEW" or "MATERIALIZED VIEW";

    protected override List<IDataTable> OnGetTables(String[]? names)
    {
        var tables = GetSchema("Tables", null)!;
        var columns = GetSchema("Columns", null)!;
        var indexes = GetSchema("Indexes", null)!;
        var requested = names == null ? null : new HashSet<String>(names, StringComparer.OrdinalIgnoreCase);
        var result = new List<IDataTable>();
        foreach (DataRow row in tables.Rows)
        {
            if (!IsRelational(row)) continue;
            var name = (String)row["TABLE_NAME"];
            if (requested != null && !requested.Contains(name)) continue;
            var table = DAL.CreateTable();
            table.TableName = name;
            table.DbType = Database.Type;
            table.IsView = (String)row["TABLE_TYPE"] != "BASE TABLE";
            if (tables.Columns.Contains("REMARKS") && !row.IsNull("REMARKS")) table.Description = (String)row["REMARKS"];
            foreach (var item in columns.Rows.Cast<DataRow>().Where(column => (String)column["TABLE_NAME"] == name).OrderBy(column => (Int32)column["ORDINAL_POSITION"]))
            {
                var column = table.CreateColumn();
                column.ColumnName = (String)item["COLUMN_NAME"];
                column.RawType = (String)item["DATA_TYPE"];
                column.DataType = GetDataType(column) ?? throw new NotSupportedException(L.T("SonnetDB field type has not been mapped: {0}", column.RawType));
                column.Identity = (Boolean)item["IS_AUTO_INCREMENT"];
                column.PrimaryKey = (Boolean)item["IS_PRIMARY_KEY"];
                column.Nullable = (Boolean)item["IS_NULLABLE"];
                column.DefaultValue = item.IsNull("COLUMN_DEFAULT") ? null : (String)item["COLUMN_DEFAULT"];
                if (columns.Columns.Contains("REMARKS") && !item.IsNull("REMARKS")) column.Description = (String)item["REMARKS"];
                column.Length = item.IsNull("CHARACTER_MAXIMUM_LENGTH") ? 0 : Math.Max(0, Convert.ToInt32(item["CHARACTER_MAXIMUM_LENGTH"], CultureInfo.InvariantCulture));
                column.Precision = item.IsNull("NUMERIC_PRECISION") ? 0 : Convert.ToInt32(item["NUMERIC_PRECISION"], CultureInfo.InvariantCulture);
                column.Scale = item.IsNull("NUMERIC_SCALE") ? 0 : Convert.ToInt32(item["NUMERIC_SCALE"], CultureInfo.InvariantCulture);
                column.Fix();
                table.Columns.Add(column);
            }
            if (table.IsView && table.Columns.Count == 0) ReadViewColumns(table);
            foreach (var group in indexes.Rows.Cast<DataRow>().Where(index => (String)index["TABLE_NAME"] == name && String.IsNullOrEmpty(Convert.ToString(index["JSON_PATH"]))).GroupBy(index => (String)index["INDEX_NAME"]))
            {
                var index = table.CreateIndex();
                index.Name = group.Key;
                index.Unique = (Boolean)group.First()["IS_UNIQUE"];
                index.Columns = group.OrderBy(item => (Int32)item["ORDINAL_POSITION"]).Select(item => (String)item["COLUMN_NAME"]).ToArray();
                table.Indexes.Add(index);
            }
            table.Fix();
            result.Add(table);
        }
        return result;
    }

    internal IDataTable? FindTable(String name) => OnGetTables([name]).FirstOrDefault();

    private void ReadViewColumns(IDataTable table)
    {
        // GetSchema("Columns")只包含关系表；尝试读取视图列元数据，声明类型缺失时明确拒绝。
        using var connection = Database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT * FROM {Database.FormatName(table.TableName)} LIMIT 0";
        using var reader = command.ExecuteReader();
        var schema = reader.GetSchemaTable() ?? throw new NotSupportedException(L.T("SonnetDB view [{0}] did not provide field metadata.", table.TableName));
        foreach (var row in schema.Rows.Cast<DataRow>().OrderBy(row => (Int32)row[SchemaTableColumn.ColumnOrdinal]))
        {
            var column = table.CreateColumn();
            column.ColumnName = (String)row[SchemaTableColumn.ColumnName];
            column.DataType = (Type)row[SchemaTableColumn.DataType];
            if (column.DataType.FullName == "System.TimeOnly") column.DataType = typeof(TimeSpan);
            if (column.DataType == typeof(Object) || row.IsNull(SchemaTableColumn.AllowDBNull))
                throw new NotSupportedException(L.T("SonnetDB view [{0}] field [{1}] does not provide a mappable declared type or nullability; do not infer it from the first row. See SonnetDB issue #261.", table.TableName, column.ColumnName));
            column.Nullable = (Boolean)row[SchemaTableColumn.AllowDBNull];
            column.PrimaryKey = (Boolean)row[SchemaTableColumn.IsKey];
            column.Identity = (Boolean)row[SchemaTableOptionalColumn.IsAutoIncrement];
            // ADO.NET 以 DBNull 表示未知长度/精度；XCode 以 0 保存未声明值，不能伪造上限。
            column.Length = row.IsNull(SchemaTableColumn.ColumnSize) ? 0 : Math.Max(0, Convert.ToInt32(row[SchemaTableColumn.ColumnSize], CultureInfo.InvariantCulture));
            column.Precision = row.IsNull(SchemaTableColumn.NumericPrecision) ? 0 : Convert.ToInt32(row[SchemaTableColumn.NumericPrecision], CultureInfo.InvariantCulture);
            column.Scale = row.IsNull(SchemaTableColumn.NumericScale) ? 0 : Convert.ToInt32(row[SchemaTableColumn.NumericScale], CultureInfo.InvariantCulture);
            column.RawType = GetFieldType(column) ?? throw new NotSupportedException(L.T("SonnetDB view [{0}] field [{1}] does not provide a mappable declared type. See SonnetDB issue #261.", table.TableName, column.ColumnName));
            column.Fix();
            table.Columns.Add(column);
        }
    }

    protected override Type? GetDataType(IDataColumn field) => field.RawType?.Equals("JSON", StringComparison.OrdinalIgnoreCase) == true ? typeof(String) : base.GetDataType(field);

    protected override String? GetFieldType(IDataColumn field)
    {
        if (field.RawType?.Equals("JSON", StringComparison.OrdinalIgnoreCase) == true && field.Table.DbType == Database.Type) return "JSON";
        if (field.DataType == typeof(String) && field.Length > 0 && field.Length < Int32.MaxValue) return $"STRING({field.Length})";
        if (field.DataType == typeof(Decimal) && field.Precision > 0) return $"DECIMAL({field.Precision},{field.Scale})";
        return base.GetFieldType(field);
    }

    protected override Boolean IsColumnTypeChanged(IDataColumn entityColumn, IDataColumn dbColumn) => !String.Equals(GetFieldType(entityColumn), GetFieldType(dbColumn), StringComparison.OrdinalIgnoreCase);
    protected override Boolean IsColumnLengthChanged(IDataColumn entityColumn, IDataColumn dbColumn, IDatabase? entityDb)
    {
        return entityColumn.DataType == typeof(String) && entityColumn.Length != dbColumn.Length;
    }
    protected override Boolean IsColumnChanged(IDataColumn entityColumn, IDataColumn dbColumn, IDatabase? entityDb)
    {
        var expectedDefault = GetDefault(entityColumn, false);
        if (expectedDefault != null) expectedDefault = expectedDefault.Substring(" DEFAULT ".Length);
        // 字符串默认值是区分大小写的字面量，'ABC'与'abc'不能视为相同
        var comparison = entityColumn.DataType == typeof(String) || entityColumn.DataType == typeof(Char) ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return entityColumn.Identity != dbColumn.Identity || entityColumn.PrimaryKey != dbColumn.PrimaryKey
            || (entityColumn.Nullable && !entityColumn.PrimaryKey && !entityColumn.Identity) != dbColumn.Nullable
            || !String.Equals(expectedDefault?.Trim(), dbColumn.DefaultValue?.Trim(), comparison);
    }

    /// <inheritdoc/>
    public override String FieldClause(IDataColumn field, Boolean onlyDefine)
    {
        var sql = $"{FormatName(field)} {GetFieldType(field) ?? throw new NotSupportedException(L.T("SonnetDB does not support field type {0}.", field.DataType))}";
        if (field.Identity) return sql + " AUTO_INCREMENT";
        sql += field.Nullable && !field.PrimaryKey ? " NULL" : " NOT NULL";
        return sql + GetDefault(field, onlyDefine);
    }

    protected override String? GetDefault(IDataColumn field, Boolean onlyDefine)
    {
        if (field.Identity) return null;
        var value = field.DefaultValue;
        if (!String.IsNullOrEmpty(value))
        {
            if ((field.DataType == typeof(DateTime) || field.DataType == typeof(DateTimeOffset) || field.DataType == typeof(TimeSpan))
                && !value.StartsWith("'") && value.IndexOf('(') < 0)
                value = SonnetDB.Quote(value);
            if (field.DataType == typeof(DateTime)) value = "CAST(" + value + " AS DATETIME2)";
            else if (field.DataType == typeof(DateTimeOffset)) value = "CAST(" + value + " AS DATETIMEOFFSET)";
            else if (field.DataType == typeof(TimeSpan)) value = "CAST(" + value + " AS DURATION)";
            return " DEFAULT " + value;
        }
        if (field.Nullable) return null;
        if (field.DataType == typeof(String) || field.DataType == typeof(Guid)) return " DEFAULT ''";
        if (field.DataType == typeof(Boolean)) return " DEFAULT FALSE";
        if (field.DataType == typeof(DateTime)) return " DEFAULT CAST('0001-01-01T00:00:00' AS DATETIME2)";
        if (field.DataType == typeof(DateTimeOffset)) return " DEFAULT CAST('0001-01-01T00:00:00+00:00' AS DATETIMEOFFSET)";
        if (field.DataType == typeof(TimeSpan)) return " DEFAULT CAST('00:00:00' AS DURATION)";
        if (field.DataType.IsEnum || field.DataType == typeof(Decimal) || field.DataType == typeof(Double) || field.DataType == typeof(Single) || field.DataType == typeof(Int64) || field.DataType == typeof(Int32) || field.DataType == typeof(Int16) || field.DataType == typeof(Byte) || field.DataType == typeof(SByte) || field.DataType == typeof(UInt16) || field.DataType == typeof(UInt32) || field.DataType == typeof(UInt64)) return " DEFAULT 0";
        return null;
    }

    /// <inheritdoc/>
    public override String CreateTableSQL(IDataTable table)
    {
        var clauses = table.Columns.Select(column => FieldClause(column, true)).ToList();
        if (table.PrimaryKeys.Length > 0) clauses.Add("PRIMARY KEY (" + String.Join(",", table.PrimaryKeys.Select(FormatName)) + ")");
        return $"CREATE TABLE {FormatName(table)} ({String.Join(",", clauses)})";
    }

    /// <inheritdoc/>
    public override String? AlterColumnSQL(IDataColumn field, IDataColumn? oldfield)
    {
        if (oldfield != null && (field.Identity != oldfield.Identity || field.PrimaryKey != oldfield.PrimaryKey))
            throw new NotSupportedException(L.T("Changing identity or primary key on existing SonnetDB columns requires a separate migration and cannot be done through ALTER COLUMN."));
        var sql = $"ALTER TABLE {FormatName(field.Table)} ALTER COLUMN {FormatName(field)} TYPE {GetFieldType(field)}";
        sql += field.Nullable && !field.PrimaryKey && !field.Identity ? " DROP NOT NULL" : " SET NOT NULL";
        if (!field.Identity)
        {
            var defaultSql = GetDefault(field, false);
            sql += defaultSql == null ? " DROP DEFAULT" : " SET" + defaultSql;
        }
        return sql;
    }

    /// <inheritdoc/>
    public override String? AddColumnSQL(IDataColumn field)
    {
        return base.AddColumnSQL(field);
    }

    /// <inheritdoc/>
    public override String? CreateIndexSQL(IDataIndex index) => $"CREATE {(index.Unique ? "UNIQUE " : String.Empty)}INDEX {Database.FormatName(index.Name!)} ON {FormatIndexTable(index.Table)} ({String.Join(",", index.Table.GetColumns(index.Columns).Select(FormatName))})";

    /// <inheritdoc/>
    public override String DropIndexSQL(IDataIndex index) => $"DROP INDEX {Database.FormatName(index.Name!)} ON {FormatIndexTable(index.Table)}";

    /// <summary>格式化索引所属表名。反向工程得到的物理表名已包含表前缀，不能重复添加</summary>
    private String FormatIndexTable(IDataTable table)
    {
        var prefix = Database.TablePrefix;
        var name = table.TableName;
        if (!prefix.IsNullOrEmpty() && name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return Database.FormatName(name);

        return FormatName(table);
    }

    /// <inheritdoc/>
    public override Boolean DatabaseExist(String? databaseName)
    {
        var db = (SonnetDB)Database;
        if (!db.IsRemote)
        {
            if (!String.IsNullOrEmpty(databaseName) && databaseName != db.DatabaseName) return false;
            return !String.IsNullOrEmpty(db.DatabaseName) && Directory.Exists(db.DatabaseName);
        }
        var rows = db.CreateSession().Query("SHOW DATABASES", null);
        databaseName ??= db.DatabaseName;
        return rows.Rows.Any(row => row.Length > 0 && String.Equals(Convert.ToString(row[0]), databaseName, StringComparison.Ordinal));
    }

    /// <inheritdoc/>
    public override Boolean CreateDatabase(String databaseName, String? file = null)
    {
        if (((SonnetDB)Database).IsRemote)
        {
            if (DatabaseExist(databaseName)) return true;
            Database.CreateSession().Execute(CreateDatabaseSQL(databaseName, file));
            return true;
        }
        if (databaseName != Database.DatabaseName) throw new NotSupportedException(L.T("Embedded SonnetDB creates databases through the Data Source directory; configure a separate connection for the target directory."));
        using var connection = Database.OpenConnection();
        return connection.State == ConnectionState.Open;
    }

    /// <inheritdoc/>
    public override Boolean DropDatabase(String databaseName)
    {
        if (!((SonnetDB)Database).IsRemote) throw new NotSupportedException(L.T("Embedded SonnetDB does not support deleting the database directory via SQL."));
        if (!DatabaseExist(databaseName)) return true;
        Database.CreateSession().Execute(DropDatabaseSQL(databaseName));
        return true;
    }

    /// <inheritdoc/>
    public override String CreateDatabaseSQL(String dbname, String? file) => $"CREATE DATABASE {Database.FormatName(dbname)}";
    /// <inheritdoc/>
    public override String DropDatabaseSQL(String dbname) => $"DROP DATABASE {Database.FormatName(dbname)}";

    /// <inheritdoc/>
    public override String? AddTableDescriptionSQL(IDataTable table) => $"COMMENT ON TABLE {FormatName(table)} IS {SonnetDB.Quote(table.Description ?? String.Empty)}";
    /// <inheritdoc/>
    public override String? DropTableDescriptionSQL(IDataTable table) => $"COMMENT ON TABLE {FormatName(table)} IS NULL";
    /// <inheritdoc/>
    public override String? AddColumnDescriptionSQL(IDataColumn field) => $"COMMENT ON COLUMN {FormatName(field.Table)}.{FormatName(field)} IS {SonnetDB.Quote(field.Description ?? String.Empty)}";
    /// <inheritdoc/>
    public override String? DropColumnDescriptionSQL(IDataColumn field) => $"COMMENT ON COLUMN {FormatName(field.Table)}.{FormatName(field)} IS NULL";

    protected override Boolean PerformSchema(StringBuilder sb, Boolean onlySql, DDLSchema schema, params Object[] values)
    {
        return base.PerformSchema(sb, onlySql, schema, values);
    }
}
