# 大数据



## 大数据的表设计

在数据模型Model.xml中，需要能够把大数据的表标记出来。

一般大数据表有时间字段或者Ds字符串字段，可能是单表设计，也可能是多表分表设计。



### 大数据单表

模型列增加数据规模属性DataScale，赋值time，表示该字段是数据时间字段。

时间字段几种场景：

1. 创建时间 CreateTime。创建数据的时间，一般用于只插入日志表。
2. 更新时间 UpdateTime。最后更新数据的时间，一般用于追踪数据变更。
3. 雪花Id。雪花Id带有业务时间，毫秒精度，用于千万级大表。
4. 数据时间 DataTime。一般表示业务数据所在时间日期，默认日期，支持time:yyyyMMddHH。
5. 数据分区 Ds。一边表示业务数据所在日期，默认yyyyMMdd，支持time:yyyyMMddHH。



### 大数据分表

模型列增加数据规模属性DataScale，赋值timeshard，表示该字段是数据时间分表字段。

代码生成时，数据文件增加 DropWith(start, end) 方法，支持按时间整表删除。

业务文件自动识别并增加分表策略代码。



时间字段几种场景：

1. 时间字段。创建更新时间或者业务时间DataTime，支持timeshard:yyMMdd:yyyy，后面两段是分库分表表达式，第三段为空时只分表不分库，即timeshard::yyMMdd。
2. 雪花Id。支持timeshard:yyMMdd:yyyy，后面两段是分库分表表达式，第三段为空时只分表不分库，即timeshard::yyMMdd。



### 高级查询

带有DataScale字段的表，Search查询时，SearchWhere默认使用该字段。



### 缓存查询

带有DataScale字段的表，生成查询代码时，一律取消所有缓存，包括实体缓存和单对象缓存。



### 时间删除

带有DataScale字段的表，生成代码时，数据文件增加 DeleteWith(start, end) 方法，删除指定时间之前的数据。MySql数据库在底下会支持分批删除。