# NewLife.XCode — промежуточное ПО для данных / супер-ORM

![GitHub top language](https://img.shields.io/github/languages/top/newlifex/newlife.xcode?logo=github)

![GitHub License](https://img.shields.io/github/license/NewLifeX/NewLife.XCode?logo=github)
![NuGet Downloads](https://img.shields.io/nuget/dt/NewLife.XCode?logo=nuget)
![NuGet](https://img.shields.io/nuget/v/NewLife.XCode?logo=nuget)
![NuGet prerelease](https://img.shields.io/nuget/vpre/NewLife.XCode?label=dev%20nuget&logo=nuget)
[![test](https://github.com/NewLifeX/NewLife.XCode/actions/workflows/test.yml/badge.svg?branch=master)](https://github.com/NewLifeX/NewLife.XCode/actions/workflows/test.yml)
[![locales](https://img.shields.io/badge/locales-31-2ea44f?logo=dotnet)](Doc/多语言本地化.md)
![platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS%20%7C%20Android%20%7C%20iOS-blue)
![arch](https://img.shields.io/badge/arch-x64%20%7C%20arm64%20%7C%20loongarch64%20%7C%20riscv64%20%7C%20s390x-orange)
[![contributors](https://img.shields.io/github/contributors/NewLifeX/NewLife.XCode?logo=github)](https://github.com/NewLifeX/NewLife.XCode/graphs/contributors)

<!-- lang:start -->
🌐 [English](README.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

> ⚠️ Этот перевод может устареть, включая структуру; авторитетной является [версия на английском](README.md).

**NewLife.XCode** — это высокопроизводительное промежуточное ПО для работы с данными в .NET, ориентированное на «CRUD + предельную производительность + огромные объёмы данных». Оно включает многоуровневое кэширование, автоматическое моделирование и миграцию, шардирование, строго типизированные запросы и межбазовую миграцию. Поддерживаются MySQL / SQLite / SqlServer / Oracle / PostgreSQL / TDengine / InfluxDB / DaMeng / KingBase / HighGo / DB2 / ClickHouse / DuckDB / MongoDB / Hana (SAP HANA) / IRIS (InterSystems) / VastBase / Network (сетевая виртуальная база данных) / NovaDb / SonnetDB и многие другие. В промышленной эксплуатации достигаются десятки миллиардов строк на таблицу, а пропускная способность запросов может достигать миллиардов QPS (при использовании стратегий кэширования).

Документация: https://newlifex.com/xcode   QQ-группы сообщества: 1600800 / 1600838

**Содержание**: [Быстрый старт](#быстрый-старт) · [Почему XCode](#почему-xcode) · [Основные возможности](#основные-возможности) · [Карта экосистемы](#карта-экосистемы) · [Документация](#документация) · [Участие в проекте](#участие-в-проекте) · [Команда](#команда) · [Лицензия](#лицензия)

## Быстрый старт

### Установка

Ссылка на NuGet (в любом проекте):

```
dotnet add package NewLife.XCode
```

Или используйте шаблон-заготовку:

```
dotnet new install NewLife.Templates
dotnet new xcode --name Demo.Data
cd Demo.Data
dotnet build
```

> Сертификат строгого имени встроен (newlife.snk); при необходимости можно пересобрать сборку для частного корпоративного фреймворка.

### Определение модели (db.xml)

```xml
<?xml version="1.0" encoding="utf-8"?>
<Tables Version="1.0" Output="MyEntity" NameSpace="Model" BaseClass="Entity" ConnName="DB">
  <Table Name="UserInfo" Description="Информация о пользователе">
    <Columns>
      <Column Name="Id" DataType="Int32" Identity="True" PrimaryKey="True" Description="Идентификатор участника" />
      <Column Name="UserName" DataType="String" Nullable="False" Description="Имя участника" />
      <Column Name="Password" DataType="String" Nullable="False" Description="Пароль участника" />
      <Column Name="Age" DataType="Int32" Description="Возраст участника" />
    </Columns>
  </Table>
</Tables>
```

### Генерация классов сущностей

- Скачайте xcodetool.exe в нужный каталог и запустите двойным щелчком либо выполните: `dotnet tool install xcodetool -g --prerelease` → `xcode db.xml`.
- Генерируются: 用户信息.cs (класс данных, перезаписывается) + 用户信息.Biz.cs (бизнес-класс, больше не перезаписывается).

### CRUD

```csharp
using XCode;

var user = new UserInfo { UserName = "Zhang San", Password = "123456", Age = 18 };
user.Insert();
Console.WriteLine("Вставлена новая запись, id пользователя: " + user.Id);

var u2 = UserInfo.FindById(user.Id);
u2.Age = 19;
u2.Update();

var list = UserInfo.FindAll(UserInfo._.Age > 10 & UserInfo._.UserName == "Zhang San");
UserInfo.FindById(user.Id)?.Delete();
```

> При использовании кэша: менее 1000 сущностей → кэш всей таблицы; иначе обычные запросы / кэш одного объекта.

## Почему XCode

- **Собственная разработка**: полностью создан в Китае, без сторонних компонентов, полностью контролируем и обеспечен долгосрочной поддержкой.
- **Простота**: 90 % бизнес-сценариев требуют лишь операций с одной таблицей, без ручного написания SQL.
- **Совместимость с Dapper**: выполняет произвольные SQL-запросы и сопоставляет их с любой моделью сущностей; полностью совместим с практикой использования Dapper, добавляя кэширование, разделение чтения и записи и трассировку.
- **Предельная производительность**: многоуровневое кэширование на основном пути + пакетная оптимизация + соединения в памяти; скорость запросов может быть значительно выше, чем у обычного SQL.
- **Огромные данные**: собственные стратегии разбиения на страницы и шардирования; в промышленных проектах — десятки миллиардов строк на таблицу; алгоритмы разбиения на страницы адаптированы к разным базам данных.
- **Стабильность и зрелость**: более 20 лет эволюции и широкая производственная проверка; сохранена совместимость со старыми версиями .NET Framework (начиная с net45).
- **Автоматическая миграция**: модель/сущность — это и есть схема; новые поля синхронизируются автоматически при развёртывании, без дополнительных SQL-скриптов.
- **Распределённая эволюция**: собственные стратегии шардирования по таблицам / времени / первичному ключу для плавного разделения и масштабирования.
- **Подключаемость**: абстракция DAL + отдельные провайдеры; поддерживаются собственные адаптеры баз данных.

## Основные возможности

<details>
<summary><b>Моделирование и генерация кода</b> (5)</summary>

| Возможность | Ключевые особенности |
| --- | --- |
| XML-модель (Tables/Columns/Indexes) | Модель — единственный источник истины; атрибуты охватывают индексы, уникальные ограничения, значения по умолчанию, описания и расширенные свойства. |
| Генератор кода | `xcodetool` (глобальный инструмент / exe) или T4-шаблон VS превращает XML-модель в классы сущностей. |
| Класс данных (`.cs`) | Перезаписывается при каждой генерации; содержит поля и базовые запросы. |
| Бизнес-класс (`.Biz.cs`) | После первой генерации не перезаписывается; размещайте здесь собственную логику. |
| Автоматически генерируемые методы | Индексы и столбцы автоматически порождают вспомогательные методы `FindByXxx` / `Search` и шаблоны. |

</details>

<details>
<summary><b>Среда выполнения сущностей и DAL</b> (7)</summary>

| Компонент | Ответственность |
| --- | --- |
| `IEntity` / `Entity` | Насыщенная доменная модель, инкапсулирующая поля, валидацию и жизненный цикл. |
| `IEntityOperate` | Статическая точка входа операций (Find / FindAll / Query и т. д.). |
| `EntityFactory` | Фабрика кэша метаданных сущностей. |
| `EntitySession` (`Meta.Session`) | Контекст сущности на конкретном соединении/таблице с кэшем и статистикой уровня сессии. |
| `Meta` | Статический контекст сущности (схема таблицы, поля, кэши, политика шардирования и т. д.). |
| `DAL` / `IDatabase` / `IDbSession` | Унифицированный доступ к базе данных и операции с метаданными. |
| `CheckTables()` | Проверяет и синхронизирует схему при запуске/первом обращении — асинхронно или синхронно по необходимости. |

</details>

<details>
<summary><b>Query DSL</b> (8)</summary>

| Возможность | Ключевые особенности |
| --- | --- |
| `IQueryable<T>` LINQ (v12.0) | `Entity<T>.Query` возвращает `IQueryable<T>` с поддержкой стандартных LINQ-операций: `Where/OrderBy/Skip/Take/Count/First/ToList`. |
| Свободные запросы `DAL.Select<T>()` (v12.1) | `DAL.Select<T>()` возвращает `IQueryable<T>`, привязанный к текущему соединению, и поддерживает полностью цепочечные вызовы `WhereIf/Where/OrderBy/Skip/Take/Page/Count/FirstOrDefault/ToList`. |
| Навигационные свойства (v12.1) | Fluent API `HasOne`/`HasMany` настраивает навигационные связи; `Include` строит пути и выполняет пакетную загрузку; `SelectBuilder` поддерживает JOIN по навигационным связям. |
| Встроенные условия `WhereIf` | `Query.WhereIf(condition, predicate)` и `FindAllWhereIf` обеспечивают динамическую фильтрацию. |
| `WhereExpression` | Цепочечная композиция выражений `&` / `\|` / `Contains` / `Between` / `Like`. |
| `Find` / `FindAll` / `Query` / `PageFindAll` | Точки входа запросов; разбиение на страницы встроено в `PageFindAll`. |
| `Pager` / `PageParameter` | Несёт разбиение на страницы, сортировку, статистику `TotalCount` и расширения области данных. |
| Оконные функции (v12.0) | `SelectBuilder.Window` + `WindowFunction` поддерживают ROW_NUMBER/RANK/DENSE_RANK и агрегатные оконные функции. |

</details>

<details>
<summary><b>Многоуровневая система кэширования</b> (6)</summary>

Рассчитана на нагрузки с преобладанием чтения и редкой записью:

| Кэш | Ключевые особенности |
| --- | --- |
| `Meta.Count` | Быстрый кэш количества строк (при >1000 использует системные индексы; SQLite использует Max(identity) и асинхронную точную компенсацию). |
| `EntityCache` (`Meta.Cache.Entities`) | Кэш всей таблицы (рекомендуется для небольших таблиц <1000 строк; запрещён при >10000 строк). |
| `SingleCache` (`Meta.SingleCache[key]`) | Кэш отдельных объектов уровня словаря по первичному/альтернативному ключу; подходит для частых точечных выборок, например пользователей. |
| Политика истечения | По умолчанию 60 с. Просроченное чтение немедленно возвращает устаревшие данные + асинхронно их обновляет. Запись немедленно инвалидирует/обновляет кэши. |
| Осведомлённость о транзакциях | Внутри транзакции изменения накапливаются; после commit/rollback кэши единообразно очищаются или откатываются. |
| Вторичный кэш (v12.0) | `CacheInvalidator.Provider` подключает Redis и согласует инвалидацию кэша сущностей между процессами через механизм версий. |

</details>

<details>
<summary><b>Обратный инжиниринг и миграция</b> (3)</summary>

| Возможность | Ключевые особенности |
| --- | --- |
| Обратный инжиниринг схемы | Создаёт/изменяет схемы таблиц и индексы из сущностей в обратном порядке, автоматически поддерживая их согласованность. |
| Политика миграции на уровне сущности | Конфигурация `Migration` уровня таблицы (атрибут модели `Table` / `[BindTable]` / переопределение при запуске через `TableItem`) управляет стратегией обратного инжиниринга для каждой сущности (Off / ReadOnly / On / Full). |
| Извлечение модели и межбазовая миграция | Поддерживает извлечение модели из любой базы данных + межбазовую миграцию данных (с сопутствующим инструментом DataMigration). |

</details>

<details>
<summary><b>Шардирование (ShardPolicy)</b> (3)</summary>

| Возможность | Ключевые особенности |
| --- | --- |
| Абстракция `ShardPolicy` | Пример `TimeShardPolicy`: имя таблицы Table_yyyyMMdd по дате. |
| Прозрачная маршрутизация | Бизнес-код по-прежнему вызывает Find / Insert / Update и не заботится о фактическом имени таблицы. |
| Собственные стратегии | Настраиваемые стратегии по хэшу первичного ключа / времени / диапазону. |

</details>

<details>
<summary><b>Транзакции и согласованность кэша</b> (3)</summary>

| Сценарий | Ключевые особенности |
| --- | --- |
| Нетранзакционная запись | Запись немедленно инвалидирует связанные кэши и асинхронно перестраивает их, одновременно пытаясь напрямую обновить объекты в памяти для согласованности чтения и записи. |
| Commit | Внутри транзакции кэши всей таблицы не очищаются; локально изменяются только затронутые объекты; после commit они очищаются или обновляются единообразно. |
| Rollback | При откате изменения, сделанные внутри транзакции, отменяются. |

</details>

<details>
<summary><b>Оптимизация производительности</b> (6)</summary>

| Приём | Ключевые особенности |
| --- | --- |
| Многоуровневое кэширование | Удерживает горячие запросы в стороне от базы данных. |
| Пакетная запись | Объединяет операции с несколькими полями и записями (внутренняя оптимизация). |
| Алгоритмы разбиения на страницы | Выбирает оптимальную стратегию для каждой базы данных/версии. |
| Ленивость/асинхронность | Количество строк, проверка модели и обновление кэша могут выполняться асинхронно. |
| Кэш рефлексии / ускорение делегатами | Предварительно прогревает аксессоры свойств сущностей. |
| Соединение в памяти | Сначала кэширует основную таблицу, затем выполняет «псевдосоединения» в памяти. |

</details>

<details>
<summary><b>Область данных и мультитенантность</b> (5)</summary>

| Возможность | Ключевые особенности |
| --- | --- |
| Область данных | Управление доступом к данным на уровне строк с пятью областями: «Все / Отдел и подотделы / Отдел / Только свои / Настраиваемая». |
| Подключение области | `DataScopeContext` + `DataScopeInterceptor` автоматически применяют фильтры запросов, владельца по умолчанию при вставке и проверку при обновлении/удалении. |
| Включение в сущности | Сущности реализуют `IUserScope` / `IDepartmentScope` / `IDataScope`; руководители отделов автоматически включаются в видимую область, а собственные данные всегда доступны. |
| Мультитенантность | После реализации сущностью `ITenantScope` `TenantInterceptor` автоматически добавляет фильтры арендатора в запросы и проверяет/заполняет идентификатор арендатора при записи; EntityBuilder автоматически реализует `ITenantScope` для бизнес-классов, содержащих столбец `TenantId`. |
| Подробности | Подробнее см. `Doc/数据权限.md` и `Doc/权限管理.md`. |

</details>

<details>
<summary><b>Конфигурация нескольких баз данных</b> (4)</summary>

| Возможность | Ключевые особенности |
| --- | --- |
| SQLite по умолчанию | SQLite используется по умолчанию для разработки (без конфигурации). |
| Настройки подключения | При развёртывании настройте `XCode_{ConnName}` в файле конфигурации или в переменных среды. |
| Идентификатор провайдера | Строка подключения должна содержать идентификатор провайдера (например: `provider=MySql;server=...;user=...;******;database=...;`). |
| Несколько подключений | Несколько имён подключений к базам данных (ConnName) можно использовать совместно в одном процессе. |

</details>

### Примеры использования

#### Динамические условия и разбиение на страницы

```csharp
public static IList<UserInfo> Search(String name, Int32 age, Pager p)
{
    var exp = new WhereExpression();
    if (!name.IsNullOrEmpty()) exp &= UserInfo._.UserName == name;
    if (age > 0) exp &= UserInfo._.Age == age;
    return UserInfo.FindAll(exp, p);
}
```

#### Обновление указанных полей (обходит кэш; не рекомендуется для обычного использования)

```csharp
UserInfo.Update(UserInfo._.UserName == "Li Si" & UserInfo._.Age == 18, UserInfo._.Id == 1);
```

#### Пользовательская сортировка и проекция столбцов

```csharp
var list = UserInfo.FindAll(UserInfo._.Age > 10, " Age DESC,Id ASC",
    string.Join(",", UserInfo._.UserName, UserInfo._.Age), 0, 20);
```

#### Индексы и генерируемые методы

Добавьте индексы в модель:

```xml
<Indexes>
  <Index Columns="UserName" Unique="True" />
  <Index Columns="UserName,Age" />
</Indexes>
```

После повторной генерации автоматически появляются `FindByUserName`, вспомогательные методы для комбинированных условий / шаблоны Search.

Стратегия кэша сущностей: когда `Meta.Session.Count < 1000`, используйте поиск по коллекции в памяти; иначе обращайтесь к базе данных или к кэшу одного объекта.

```csharp
public static UserInfo FindByUserName(String userName)
{
    if (userName.IsNullOrEmpty()) return null;
    if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.UserName.EqualIgnoreCase(userName));
    return Find(_.UserName == userName);
}
```

#### Пример шардирования

```csharp
Meta.ShardPolicy = new TimeShardPolicy(nameof(UserInfo.Id), Meta.Factory)
{
    ConnPolicy = "{0}",           // настраиваемая политика имени базы данных
    TablePolicy = "{0}_{1:yyyyMMdd}", // шаблон имени таблицы
    Step = TimeSpan.FromDays(1)
};
```

Весь остальной код, например Insert / Query, остаётся неизменным и автоматически маршрутизируется в соответствующую таблицу даты.

#### Исходные примеры (сохранены)

<details>
<summary><b>Исходные примеры CRUD и сложных запросов</b></summary>

##### Создание, чтение, обновление, удаление

```csharp
var user = new UserInfo();
user.UserName = "Zhang San";
user.Password = "123456";
user.Age = 18;
user.Insert();
Console.WriteLine("Вставлена новая запись, id пользователя: " + user.Id);

var u = UserInfo.FindById(1);
u.UserName = "Zhang San";
u.Password = "123456";
u.Age = 19;
u.Update();

u = UserInfo.FindById(1);
Console.WriteLine($"ID пользователя={u.Id}, возраст обновлён на: {u.Age}");
```

##### Сложный запрос

```csharp
var list = UserInfo.FindAll(UserInfo._.UserName == "Zhang San" & UserInfo._.Age == 19, UserInfo._.UserName.Desc(), string.Join(",", UserInfo._.UserName, UserInfo._.Age), 0, 0);
```

</details>

### Бенчмарки

| Возможность | Показатель (типовая среда) | Примечания |
| ---- | ---------------- | ---- |
| Вставка на одной машине | до ~890 тыс. TPS | комбинированные тесты пакетов / кэша + SQLite / RAM-диска |
| Запрос на одной машине | до ~1,8 млрд QPS | сценарии небольших таблиц + кэша сущностей / кэша одного объекта |
| Объём данных на таблицу | десятки миллиардов в производстве | стратегии разбиения на страницы + шардирования |

> Фактическая производительность зависит от типа базы данных / оборудования / стратегии шардирования / доли попаданий в кэш.

### FAQ

- Почему первый Count выполняется быстро? `Meta.Count` использует системные индексы; SQLite может сначала взять максимальное значение identity, а затем асинхронно уточнить его.
- Насколько быстро кэш становится согласованным после обновления? Записи в текущем процессе отражаются немедленно; между процессами дождитесь истечения кэша или используйте активную стратегию инвалидации.
- Можно ли отключить автоматическую миграцию? Установите `Migration=Off` (глобально или для отдельной таблицы) либо используйте ReadOnly/On/Full по необходимости.
- Сложные запросы к нескольким таблицам? Рекомендуются несколько запросов к одной таблице + соединение в памяти (быстрее при попаданиях в кэш и понятнее по структуре).

## Карта экосистемы

NewLife — это многоуровневая экосистема независимо версионируемых open-source проектов. Карта ниже дословно используется во всех репозиториях NewLife; каждый проект ссылается на свой репозиторий и свой основной пакет NuGet.

| Проект | Что делает | Загрузки |
| --- | --- | --- |
| **Базовый** | | |
| [NewLife.Core](https://github.com/NewLifeX/X) | Логирование, конфигурация, кэширование, сеть, сериализация, планирование и APM-трассировка. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Core?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Core) |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | Промежуточное ПО данных: отдельные таблицы масштаба десятков миллиардов строк, автоматическое секционирование, разделение чтения и записи, более 10 баз данных. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCode?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCode) |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | Высокопроизводительная сетевая библиотека: 1,77M pps / 14,5 Гбит/с на машину (TCP 1KB, 2026-10), 4M долгоживущих TCP-соединений. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Net?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Net) |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | Протокольная связь и RPC-фреймворк для клиент-серверных приложений и IoT-устройств. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Remoting?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Remoting) |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | Устанавливает .NET-приложения как демоны ОС: службы Windows и systemd в Linux. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Agent?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Agent) |
| [NewLife.Templates](https://github.com/NewLifeX/NewLife.Zero) | Шаблоны проектов `dotnet new` для консоли, web, API, службы, данных и IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Templates?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Templates) |
| **Данные и обмен сообщениями** | | |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | Клиент Redis с микросекундной задержкой, миллионами операций в секунду и встроенными очередями сообщений. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Redis?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Redis) |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | Полностью управляемый клиент RocketMQ для Apache RocketMQ и Alibaba Cloud Message Queue. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.RocketMQ?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.RocketMQ) |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | Клиент и сервер MQTT, включая подключение к Alibaba Cloud IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MQTT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MQTT) |
| [NewLife.NovaDb](https://github.com/NewLifeX/NewLife.NovaDb) | Гибридная база данных на C#: встроенная или сетевая, реляционная, временных рядов, очередь сообщений и KV в одном. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.NovaDb?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.NovaDb) |
| [NewLife.MySql](https://github.com/NewLifeX/NewLife.MySql) | Драйвер MySql и вспомогательный слой на основе стека данных NewLife. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MySql?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MySql) |
| **Web, AI и распределённые системы** | | |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | Фреймворк быстрой разработки админ-платформ: права, SSO, сервер OAuth и CRUD-скаффолдинг. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Cube?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Cube) |
| [Stardust](https://github.com/NewLifeX/Stardust) | Распределённая сервисная платформа: управление узлами, APM, центр конфигурации, реестр и центр публикации. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Stardust?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Stardust) |
| [AntJob](https://github.com/NewLifeX/AntJob) | Распределённая платформа задач и планирования данных в реальном времени с секционированием, повторами и дозаполнением. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AntJob?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AntJob) |
| [NewLife.AI](https://github.com/NewLifeX/NewLife.AI) | Единый клиент для 46 поставщиков LLM плюс ChatAI — готовое веб-приложение для чата. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AI?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AI) |
| **Промышленность и IoT** | | |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | Стандартная библиотека IoT, определяющая контракты протоколов и абстракции устройств. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IoT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IoT) |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | Реализация Modbus TCP/RTU/ASCII поверх NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Modbus?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Modbus) |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | Протокол Siemens PLC поверх NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Siemens?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Siemens) |
| [NewLife.Omron](https://github.com/NewLifeX/NewLife.Omron) | Протокол Omron PLC поверх NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Omron?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Omron) |
| [NewLife.Melsec](https://github.com/NewLifeX/NewLife.Melsec) | Протокол Mitsubishi Melsec PLC поверх NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Melsec?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Melsec) |
| [NewLife.OPC](https://github.com/NewLifeX/NewLife.OPC) | Клиент OPC для доступа к промышленным данным. | — |
| [NewLife.BACnet](https://github.com/NewLifeX/NewLife.BACnet) | Стек протокола автоматизации зданий BACnet. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.BACnet?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.BACnet) |
| [ZeroIoT](https://github.com/NewLifeX/ZeroIoT) | Лёгкая open-source платформа IoT с управлением шлюзами и устройствами. | — |
| **Телематика транспорта** | | |
| [NewLife.JT808](https://github.com/NewLifeX/NewLife.JT808) | Протокол позиционирования транспорта JT/T 808: более 70 типов сообщений, 2011/2013/2019. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT808?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT808) |
| [NewLife.JT809](https://github.com/NewLifeX/NewLife.JT809) | Межплатформенный протокол JT/T 809 для органов управления транспортом. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT809?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT809) |
| [NewLife.JT1078](https://github.com/NewLifeX/NewLife.JT1078) | Автомобильное видео JT/T 1078: разбор RTP, ремультиплексирование в FLV/fMP4 и прямая трансляция. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT1078?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT1078) |
| **Прикладные компоненты** | | |
| [NewLife.Office](https://github.com/NewLifeX/NewLife.Office) | Один пакет для Excel, Word, PPT, PDF и более 15 форматов документов. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Office?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Office) |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | Кроссплатформенное аудио: кодеки, ввод-вывод устройств, DSP, контейнеры и потоковая передача. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Audio?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Audio) |
| [NewLife.Video](https://github.com/NewLifeX/NewLife.Video) | Видеокодеки, форматы контейнеров, обработка кадров и потоковая передача. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Video?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Video) |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | Единый интерфейс к Baidu, AMap, Tencent и Tianditu с геокодированием и преобразованием координат. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Map?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Map) |
| [NewLife.IP](https://github.com/NewLifeX/NewLife.IP) | Геолокация IPv4 с офлайн-базой, отображаемой в память, и онлайн-API как резервом. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IP?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IP) |
| **Инструменты и продукты** | | |
| [CrazyCoder](https://github.com/NewLifeX/XCoder) | Инструменты разработчика: сеть, последовательный порт, шифрование, регулярные выражения, Modbus и MQTT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCoder?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCoder) |
| [GitCandy](https://github.com/NewLifeX/GitCandy) | Самостоятельно размещаемая система управления исходным кодом Git. | — |
| [LuckyClover](https://github.com/NewLifeX/LuckyClover) | Автоматически устанавливает подходящую среду выполнения .NET при запуске на старом хосте. | — |

> Бейджи показывают основной пакет каждого проекта; пакеты тем, расширений и сателлитов перечислены на соответствующих страницах NuGet. Более 30 пакетов, не перечисленных здесь (FTP, LoRa, расширения Office, драйверы протоколов и другие), доступны в [организации NewLifeX](https://github.com/NewLifeX) и на [newlifex.com](https://newlifex.com). Эта таблица ведётся в README проекта NewLife.Core.

## Документация

- [Навигация по документации XCode](Doc/XCode文档导航.md): полный индекс документов, сгруппированный по модулям
- [Руководство пользователя XCode](Doc/XCode使用手册.md): сквозной обзор использования
- [Архитектурный дизайн](Doc/架构设计.md): слои, компоненты и ключевые проектные решения
- [Многоязычная локализация](Doc/多语言本地化.md): модель сообщений, таблицы языков и переопределения во время выполнения
- Онлайн-документация: https://newlifex.com/xcode
- [ChangeLog.md](ChangeLog.md): история версий

## Участие в проекте

### Сборка и тестирование

```
dotnet restore
dotnet build -c Release
dotnet test XUnitTest.XCode -c Release
```

- Добавляйте тестовые случаи в `XUnitTest.XCode`; предложения, связанные с производительностью, должны сопровождаться бенчмарками или объяснениями.
- Соблюдайте соглашения репозитория (строгое именование, multi-TFM, XML-комментарии, условная компиляция).
- Изменения публичного API требуют стратегии миграции (Obsolete → удаление).

### Релиз

Отправьте тег с префиксом `v` (например, `v2026.1018`), чтобы запустить `.github/workflows/publish.yml`. Рабочий процесс упаковывает XCode, XCodeTool и пакеты провайдеров (`XCode.MySql`, `XCode.SQLite`, `XCode.SqlServer`, `XCode.Oracle`, `XCode.PostgreSQL`, `XCode.DaMeng`, `XCode.DB2`, `XCode.KingBase`, `XCode.HighGo`) и публикует их в NuGet.org и GitHub Packages.

### Обратная связь

- Issues / PR приветствуются: оптимизация производительности / новые провайдеры баз данных / улучшение документации.
- Перед отправкой убедитесь, что `dotnet build` и соответствующие модульные тесты проходят; соблюдайте соглашения репозитория по стилю кода и сообщениям коммитов.
- Сообщайте об ошибках и предложениях через [Issue](https://github.com/NewLifeX/NewLife.XCode/issues); Stars и PR приветствуются.

## Команда

<img src="https://newlifex.com/logo.png" width="48" align="right" alt="NewLife" />

Основанная в 2002 году команда NewLife более двух десятилетий работает в экосистеме .NET и IoT, поддерживает около 100 open-source проектов (34 из них — ядро этой карты) и предоставляет интегрированные программно-аппаратные решения для энергетики, логистики, здравоохранения, промышленного управления и интернет-отрасли. Мы следуем девизу «в учении нет старших — мастерство приносит уважение».

Open-source компоненты скачали <!--nuget-total:start-->6.48M<!--nuget-total:end--> раз с NuGet (по всей экосистеме, по состоянию на 2026-10).

Сайт: https://newlifex.com · Организация open source: https://github.com/NewLifeX

<details>
<summary>Официальный аккаунт WeChat: 智能大石头 (отсканируйте, чтобы подписаться)</summary>

![智能大石头](https://newlifex.com/stone.jpg)

</details>

## Лицензия

MIT, полностью открытая. Разрешены коммерческое использование / модификация / распространение (для частных развёртываний вы можете заменить "NewLife" на бренд вашего предприятия). Сертификат строгого имени `newlife.snk` встроен.

> Если этот проект вам помог, поставьте Star, поделитесь им с командой или пришлите предложения по улучшению через Issue/PR.
