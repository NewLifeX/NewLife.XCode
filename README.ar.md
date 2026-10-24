# NewLife.XCode - وسيط بيانات / ORM فائق

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
🌐 [English](README.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md)
<!-- lang:end -->

> ⚠️ قد تكون هذه الترجمة قديمة، بما في ذلك هيكلها؛ النسخة المعتمدة هي [README بالإنجليزية](README.md).

**NewLife.XCode** هو وسيط بيانات .NET عالي الأداء يركّز على «CRUD + أداء فائق + بيانات ضخمة». يشمل التخزين المؤقت متعدد المستويات، والنمذجة/الترحيل التلقائي، وتجزئة الجداول، والاستعلامات قوية النوع، والترحيل بين قواعد البيانات. يدعم MySQL / SQLite / SqlServer / Oracle / PostgreSQL / TDengine / InfluxDB / DaMeng / KingBase / HighGo / DB2 / ClickHouse / DuckDB / MongoDB / Hana (SAP HANA) / IRIS (InterSystems) / VastBase / قاعدة بيانات الشبكة الافتراضية / NovaDb / SonnetDB وغيرها. تصل عمليات النشر في بيئات الإنتاج إلى عشرات المليارات من الصفوف لكل جدول، ويمكن أن يبلغ معدل نقل الاستعلامات مليارات QPS (مع استراتيجيات التخزين المؤقت).

التوثيق: https://newlifex.com/xcode   مجموعات QQ المجتمعية: 1600800 / 1600838

**المحتويات**: [البدء السريع](#البدء-السريع) · [لماذا XCode](#لماذا-xcode) · [الميزات الرئيسية](#الميزات-الرئيسية) · [خريطة المنظومة](#خريطة-المنظومة) · [التوثيق](#التوثيق) · [المساهمة](#المساهمة) · [الفريق](#الفريق) · [الترخيص](#الترخيص)

## البدء السريع

### التثبيت

مرجع NuGet (أي مشروع):

```
dotnet add package NewLife.XCode
```

أو استخدم سقالة القالب:

```
dotnet new install NewLife.Templates
dotnet new xcode --name Demo.Data
cd Demo.Data
dotnet build
```

> شهادة التوقيع القوي مدمجة (newlife.snk)؛ ويمكنك إعادة الترجمة لإطار عمل خاص بمؤسستك.

### تعريف النموذج (db.xml)

```xml
<?xml version="1.0" encoding="utf-8"?>
<Tables Version="1.0" Output="MyEntity" NameSpace="Model" BaseClass="Entity" ConnName="DB">
  <Table Name="UserInfo" Description="معلومات المستخدم">
    <Columns>
      <Column Name="Id" DataType="Int32" Identity="True" PrimaryKey="True" Description="معرّف العضو" />
      <Column Name="UserName" DataType="String" Nullable="False" Description="اسم العضو" />
      <Column Name="Password" DataType="String" Nullable="False" Description="كلمة مرور العضو" />
      <Column Name="Age" DataType="Int32" Description="عمر العضو" />
    </Columns>
  </Table>
</Tables>
```

### توليد فئات الكيانات

- نزّل xcodetool.exe إلى الدليل وانقر عليه نقرًا مزدوجًا، أو شغّل: `dotnet tool install xcodetool -g --prerelease` → `xcode db.xml`.
- يُولِّد: 用户信息.cs (فئة البيانات، تُستبدل) + 用户信息.Biz.cs (فئة الأعمال، لا تُستبدل).

### CRUD

```csharp
using XCode;

var user = new UserInfo { UserName = "زانغ سان", Password = "123456", Age = 18 };
user.Insert();
Console.WriteLine("تم إدراج سجل جديد، معرّف المستخدم: " + user.Id);

var u2 = UserInfo.FindById(user.Id);
u2.Age = 19;
u2.Update();

var list = UserInfo.FindAll(UserInfo._.Age > 10 & UserInfo._.UserName == "زانغ سان");
UserInfo.FindById(user.Id)?.Delete();
```

> عند استخدام ذاكرات التخزين المؤقت: أقل من 1000 كيان → ذاكرة كاملة للجدول؛ وإلا فاستخدم الاستعلامات العادية / ذاكرة الكائن الواحد.

## لماذا XCode

- **تطوير ذاتي وأصيل**: مطوَّر بالكامل محليًا دون أي مكوّنات من طرف ثالث، ومسيطر عليه ذاتيًا ومدعوم بصيانة طويلة الأمد.
- **البساطة**: 90% من سيناريوهات الأعمال تحتاج فقط إلى عمليات كيان على جدول واحد، دون كتابة SQL يدويًا.
- **متوافق مع Dapper**: ينفّذ استعلامات SQL عشوائية ويحوّلها إلى أي نموذج كيان؛ متوافق تمامًا مع استخدام Dapper مع إضافة التخزين المؤقت وفصل القراءة/الكتابة والتتبّع.
- **أداء فائق**: تخزين مؤقت متعدد المستويات على المسار الأساسي + تحسين الدفعات + الربط داخل الذاكرة؛ ويمكن أن تتجاوز سرعة الاستعلام SQL الخام بكثير.
- **بيانات ضخمة**: استراتيجيات أصلية لتقسيم الصفحات وتجزئة الجداول؛ وتصل المشاريع الإنتاجية إلى عشرات المليارات من الصفوف لكل جدول؛ وتتكيّف خوارزميات تقسيم الصفحات مع قواعد بيانات متعددة.
- **مستقر وناضج**: أكثر من 20 عامًا من التطور والتحقق الواسع في بيئات الإنتاج؛ ويحافظ على التوافق مع إصدارات .NET Framework الأقدم (من net45).
- **ترحيل تلقائي**: النموذج/الكيان هو المخطط؛ وتُزامَن الحقول الجديدة تلقائيًا عند النشر، دون سكربتات SQL إضافية.
- **تطور موزّع**: استراتيجيات أصلية للتجزئة على مستوى الجدول / الوقت / المفتاح الأساسي لتحقيق تقسيم وتوسّع سلسين.
- **قابل للتوصيل**: تجريد DAL + موفرو خدمات منفصلون؛ وموائمات قواعد بيانات مخصّصة مدعومة.

## الميزات الرئيسية

<details>
<summary><b>النمذجة وتوليد الشيفرة</b> (5)</summary>

| الميزة | الخصائص الرئيسية |
| --- | --- |
| نموذج XML (Tables/Columns/Indexes) | النموذج هو المصدر الوحيد للحقيقة؛ وتغطي السمات الفهارسَ وقيود التفرّد والقيم الافتراضية والأوصاف والخصائص الموسّعة. |
| مولّد الشيفرة | يحوّل `xcodetool` (أداة عامة / exe) أو قالب T4 في VS نموذج XML إلى فئات كيانات. |
| فئة البيانات (`.cs`) | تُستبدل مع كل عملية توليد؛ وتحتوي الحقول والاستعلامات الأساسية. |
| فئة الأعمال (`.Biz.cs`) | لا تُستبدل بعد التوليد الأول؛ ضع منطقك المخصّص هنا. |
| الطرق المولَّدة تلقائيًا | تُنتج الفهارس والأعمدة طرقًا مساعدة وقوالب `FindByXxx` / `Search` تلقائيًا. |

</details>

<details>
<summary><b>وقت تشغيل الكيانات وDAL</b> (7)</summary>

| المكوّن | المسؤولية |
| --- | --- |
| `IEntity` / `Entity` | نموذج مجال ثري يغلّف الحقول والتحقق ودورة الحياة. |
| `IEntityOperate` | نقطة الدخول الثابتة للعمليات (Find / FindAll / Query وغيرها). |
| `EntityFactory` | مصنع الذاكرة المؤقتة للبيانات الوصفية للكيانات. |
| `EntitySession` (`Meta.Session`) | سياق الكيان على اتصال/جدول محدّد، مع ذاكرة مؤقتة وإحصاءات على مستوى الجلسة. |
| `Meta` | السياق الثابت للكيان (مخطط الجدول، الحقول، ذاكرات التخزين المؤقت، سياسة التجزئة، إلخ). |
| `DAL` / `IDatabase` / `IDbSession` | وصول موحّد إلى قاعدة البيانات وعمليات البيانات الوصفية. |
| `CheckTables()` | يفحص المخطط ويزامنه عند بدء التشغيل/أول وصول، بشكل غير متزامن أو متزامن حسب الحاجة. |

</details>

<details>
<summary><b>لغة استعلام DSL</b> (8)</summary>

| القدرة | الخصائص الرئيسية |
| --- | --- |
| `IQueryable<T>` LINQ (v12.0) | يُعيد `Entity<T>.Query` كائن `IQueryable<T>` يدعم عمليات LINQ القياسية مثل `Where/OrderBy/Skip/Take/Count/First/ToList`. |
| استعلامات `DAL.Select<T>()` السلسة (v12.1) | يُعيد `DAL.Select<T>()` كائن `IQueryable<T>` مرتبطًا بالاتصال الحالي، ويدعم الاستدعاءات السلسة الكاملة `WhereIf/Where/OrderBy/Skip/Take/Page/Count/FirstOrDefault/ToList`. |
| خصائص التنقّل (v12.1) | تضبط واجهة `HasOne`/`HasMany` Fluent علاقات التنقّل؛ ويبني `Include` المسارات ويحمِّل على دفعات؛ ويدعم `SelectBuilder` عمليات JOIN عبر علاقات التنقّل. |
| شروط `WhereIf` المضمّنة | يمكّن `Query.WhereIf(condition, predicate)` و`FindAllWhereIf` التصفية الديناميكية. |
| `WhereExpression` | تركيب تعبيرات متسلسل بـ `&` / `\|` / `Contains` / `Between` / `Like`. |
| `Find` / `FindAll` / `Query` / `PageFindAll` | نقاط دخول الاستعلام، مع تقسيم الصفحات مدمج في `PageFindAll`. |
| `Pager` / `PageParameter` | يحمل تقسيم الصفحات والترتيب وإحصاء `TotalCount` وامتدادات نطاق البيانات. |
| دوال النوافذ (v12.0) | يدعم `SelectBuilder.Window` + `WindowFunction` دوال ROW_NUMBER/RANK/DENSE_RANK ودوال النوافذ التجميعية. |

</details>

<details>
<summary><b>نظام تخزين مؤقت متعدد المستويات</b> (6)</summary>

مصمَّم لأحمال القراءة الكثيفة والكتابة الخفيفة:

| ذاكرة التخزين المؤقت | الخصائص الرئيسية |
| --- | --- |
| `Meta.Count` | ذاكرة سريعة لعدد الصفوف (تستخدم فهارس النظام عندما >1000؛ ويستخدم SQLite قيمة Max(identity) مع تعويض دقيق غير متزامن). |
| `EntityCache` (`Meta.Cache.Entities`) | ذاكرة كاملة للجدول (يُنصح بها للجداول الصغيرة <1000 صف؛ وممنوعة فوق 10000 صف). |
| `SingleCache` (`Meta.SingleCache[key]`) | ذاكرة على مستوى القاموس لكائن واحد حسب المفتاح الأساسي/البديل؛ مناسبة للاستعلامات المتكرّرة بنقطة بحث مثل المستخدمين. |
| سياسة الانتهاء | 60 ثانية افتراضيًا. القراءات المنتهية تُعيد بيانات قديمة فورًا + تحديثًا غير متزامن. والكتابات تُبطل/تحدّث ذاكرات التخزين المؤقت فورًا. |
| الوعي بالمعاملات | داخل المعاملة تتراكم التعديلات؛ وتُمسح ذاكرات التخزين المؤقت أو تُرجَع بشكل موحّد بعد الالتزام/التراجع. |
| ذاكرة المستوى الثاني (v12.0) | يحقن `CacheInvalidator.Provider` خدمة Redis وينسّق إبطال ذاكرة الكيانات بين العمليات عبر آلية الإصدارات. |

</details>

<details>
<summary><b>الهندسة العكسية والترحيل</b> (3)</summary>

| القدرة | الخصائص الرئيسية |
| --- | --- |
| الهندسة العكسية للمخطط | يُنشئ/يعدّل مخططات الجداول والفهارس من الكيانات بشكل عكسي، ويحافظ على تناسقها تلقائيًا. |
| سياسة الترحيل لكل كيان | إعداد `Migration` على مستوى الجدول (سمة `Table` في النموذج / `[BindTable]` / تجاوز `TableItem` عند بدء التشغيل) يتحكّم في استراتيجية الهندسة العكسية لكل كيان (Off / ReadOnly / On / Full). |
| استخراج النموذج والترحيل بين قواعد البيانات | يدعم استخراج النموذج من أي قاعدة بيانات + ترحيل البيانات بين قواعد بيانات مختلفة (بأداة DataMigration المرافقة). |

</details>

<details>
<summary><b>تجزئة الجداول (ShardPolicy)</b> (3)</summary>

| القدرة | الخصائص الرئيسية |
| --- | --- |
| تجريد `ShardPolicy` | مثال `TimeShardPolicy`: اسم الجدول Table_yyyyMMdd حسب التاريخ. |
| توجيه شفّاف | ما زال كود الأعمال يستدعي Find / Insert / Update ولا يهتم باسم الجدول الفعلي. |
| استراتيجيات مخصّصة | إمكانية تخصيص استراتيجيات تجزئة المفتاح الأساسي / الوقت / النطاق. |

</details>

<details>
<summary><b>المعاملات واتساق التخزين المؤقت</b> (3)</summary>

| السيناريو | الخصائص الرئيسية |
| --- | --- |
| الكتابات غير المعاملاتية | تنتهي صلاحية ذاكرات التخزين المؤقت المرتبطة فورًا وتُعاد بناؤها بشكل غير متزامن، مع محاولة تحديث الكائنات في الذاكرة مباشرة لتحقيق اتساق القراءة/الكتابة. |
| الالتزام (Commit) | داخل المعاملة لا تُمسح ذاكرات الجدول الكاملة؛ بل تُعدّل الكائنات المصابة محليًا فقط؛ وبعد الالتزام تُمسح أو تُحدَّث بشكل موحّد. |
| التراجع (Rollback) | عند التراجع تُرجَع التعديلات التي أُجريت داخل المعاملة. |

</details>

<details>
<summary><b>تحسينات الأداء</b> (6)</summary>

| التقنية | الخصائص الرئيسية |
| --- | --- |
| التخزين المؤقت متعدد المستويات | يُبقي الاستعلامات الساخنة بعيدًا عن قاعدة البيانات. |
| الكتابة على دفعات | تدمج عمليات الحقول المتعددة والسجلات المتعددة (تحسين داخلي). |
| خوارزميات تقسيم الصفحات | تختار الاستراتيجية المثلى حسب قاعدة البيانات/الإصدار. |
| التحميل الكسول/غير المتزامن | يمكن تشغيل عدّ الصفوف وفحص النموذج وتحديث ذاكرات التخزين المؤقت كلها بشكل غير متزامن. |
| تخزين الانعكاس المؤقت / تسريع المفوّضات | تُهيّئ مسبقًا وصولات خصائص الكيانات. |
| الربط داخل الذاكرة | يخزّن الجدول الرئيسي مؤقتًا أولًا، ثم ينفّذ «ربطًا شبهيًا» في الذاكرة. |

</details>

<details>
<summary><b>نطاق البيانات وتعدّد المستأجرين</b> (5)</summary>

| القدرة | الخصائص الرئيسية |
| --- | --- |
| نطاق البيانات | تحكّم في الوصول إلى البيانات على مستوى الصف يدعم خمسة نطاقات: «الكل / القسم والأقسام الفرعية / القسم / الذات فقط / مخصّص». |
| ربط النطاق | يطبّق `DataScopeContext` + `DataScopeInterceptor` مرشّحات الاستعلام تلقائيًا، والملكية الافتراضية عند الإدراج، والتحقق عند التحديث/الحذف. |
| اشتراك الكيانات | تنفّذ الكيانات `IUserScope` / `IDepartmentScope` / `IDataScope`؛ ويُدرَج مديرو الأقسام تلقائيًا في النطاق المرئي، وتبقى بيانات المستخدم نفسه متاحة دائمًا. |
| تعدّد المستأجرين | بعد أن ينفّذ الكيان `ITenantScope`، يضيف `TenantInterceptor` مرشّحات المستأجر تلقائيًا على الاستعلامات ويتحقق/يملأ معرّف المستأجر عند الكتابة؛ وينفّذ EntityBuilder تلقائيًا `ITenantScope` لفئات الأعمال التي تحتوي عمود `TenantId`. |
| التفاصيل | راجع `Doc/数据权限.md` و`Doc/权限管理.md` للتفاصيل. |

</details>

<details>
<summary><b>إعداد قواعد بيانات متعددة</b> (4)</summary>

| القدرة | الخصائص الرئيسية |
| --- | --- |
| SQLite افتراضيًا | SQLite هو الافتراضي للتطوير (بدون أي إعداد). |
| إعدادات الاتصال | عند النشر، اضبط `XCode_{ConnName}` في ملف الإعدادات أو في متغيّرات البيئة. |
| معرّف الموفر | يجب أن تتضمّن سلسلة الاتصال معرّف الموفر (مثال: `provider=MySql;server=...;user=...;******;database=...;`). |
| اتصالات متعددة | يمكن استخدام أسماء اتصال (ConnName) متعددة لقواعد بيانات مختلفة معًا في العملية نفسها. |

</details>

### أمثلة الاستخدام

#### شروط ديناميكية وتقسيم صفحات

```csharp
public static IList<UserInfo> Search(String name, Int32 age, Pager p)
{
    var exp = new WhereExpression();
    if (!name.IsNullOrEmpty()) exp &= UserInfo._.UserName == name;
    if (age > 0) exp &= UserInfo._.Age == age;
    return UserInfo.FindAll(exp, p);
}
```

#### تحديث حقول محدّدة (يتجاوز التخزين المؤقت؛ غير موصى به للاستخدام العادي)

```csharp
UserInfo.Update(UserInfo._.UserName == "لي سي" & UserInfo._.Age == 18, UserInfo._.Id == 1);
```

#### ترتيب مخصّص وإسقاط أعمدة

```csharp
var list = UserInfo.FindAll(UserInfo._.Age > 10, " Age DESC,Id ASC",
    string.Join(",", UserInfo._.UserName, UserInfo._.Age), 0, 20);
```

#### الفهارس والطرق المولَّدة

أضف فهارس إلى النموذج:

```xml
<Indexes>
  <Index Columns="UserName" Unique="True" />
  <Index Columns="UserName,Age" />
</Indexes>
```

بعد إعادة التوليد، تظهر تلقائيًا `FindByUserName` والطرق المساعدة للشروط المركّبة / قوالب Search.

استراتيجية ذاكرة الكيانات المؤقتة: عندما يكون `Meta.Session.Count < 1000`، استخدم البحث في المجموعة داخل الذاكرة؛ وإلا فارجع إلى قاعدة البيانات أو إلى ذاكرة الكائن الواحد.

```csharp
public static UserInfo FindByUserName(String userName)
{
    if (userName.IsNullOrEmpty()) return null;
    if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.UserName.EqualIgnoreCase(userName));
    return Find(_.UserName == userName);
}
```

#### مثال على التجزئة

```csharp
Meta.ShardPolicy = new TimeShardPolicy(nameof(UserInfo.Id), Meta.Factory)
{
    ConnPolicy = "{0}",           // سياسة قابلة للتخصيص لاسم قاعدة البيانات
    TablePolicy = "{0}_{1:yyyyMMdd}", // قالب اسم الجدول
    Step = TimeSpan.FromDays(1)
};
```

تبقى بقية الشيفرة مثل Insert / Query دون تغيير وتُوجَّه تلقائيًا إلى جدول التاريخ المناسب.

#### الأمثلة الأصلية (محفوظة)

<details>
<summary><b>أمثلة CRUD الأصلية والاستعلامات المعقّدة</b></summary>

##### إنشاء وقراءة وتحديث وحذف

```csharp
var user = new UserInfo();
user.UserName = "زانغ سان";
user.Password = "123456";
user.Age = 18;
user.Insert();
Console.WriteLine("تم إدراج سجل جديد، معرّف المستخدم: " + user.Id);

var u = UserInfo.FindById(1);
u.UserName = "زانغ سان";
u.Password = "123456";
u.Age = 19;
u.Update();

u = UserInfo.FindById(1);
Console.WriteLine($"تم تحديث عمر المستخدم ذي المعرّف={u.Id} إلى: {u.Age}");
```

##### استعلام معقّد

```csharp
var list = UserInfo.FindAll(UserInfo._.UserName == "زانغ سان" & UserInfo._.Age == 19, UserInfo._.UserName.Desc(), string.Join(",", UserInfo._.UserName, UserInfo._.Age), 0, 0);
```

</details>

### معايير الأداء

| القدرة | القياس (بيئة نمطية) | ملاحظات |
| ---- | ---------------- | ---- |
| إدراج على جهاز واحد | حتى ~890 ألف TPS | اختبارات مزيج الدفعات / الذاكرة المؤقتة + SQLite / قرص RAM |
| استعلام على جهاز واحد | حتى ~1.8 مليار QPS | سيناريوهات الجداول الصغيرة + ذاكرة الكيانات / ذاكرة الكائن الواحد |
| حجم البيانات لكل جدول | عشرات المليارات في الإنتاج | استراتيجيات تقسيم الصفحات + التجزئة |

> يعتمد الأداء الفعلي على نوع قاعدة البيانات / العتاد / استراتيجية التجزئة / نسبة إصابة الذاكرة المؤقتة.

### الأسئلة الشائعة

- لماذا يكون Count الأول سريعًا؟ يستخدم `Meta.Count` فهارس النظام؛ وقد يأخذ SQLite أولًا القيمة العظمى للهوية ثم يصحّح بشكل غير متزامن.
- متى يتّسق التخزين المؤقت بعد التحديث؟ تنعكس الكتابات في العملية الحالية فورًا؛ وعبر العمليات انتظر انتهاء صلاحية الذاكرة أو استخدم استراتيجية إبطال نشطة.
- هل يمكن تعطيل الترحيل التلقائي؟ اضبط `Migration=Off` (عالميًا أو لكل جدول)، أو استخدم ReadOnly/On/Full حسب الحاجة.
- استعلامات معقّدة متعددة الجداول؟ يُنصح باستعلامات متعددة لجداول مفردة + ربط في الذاكرة (أسرع مع إصابات الذاكرة وأوضح بنية).

## خريطة المنظومة

NewLife منظومة متعدّدة الطبقات من مشاريع مفتوحة المصدر تُصدَر بإصدارات مستقلة. الخريطة أدناه مشتركة حرفيًا بين جميع مستودعات NewLife؛ وكل مشروع يرتبط بمستودعه وبحزمته الرئيسية على NuGet.

| المشروع | الوظيفة | التنزيلات |
| --- | --- | --- |
| **الأساس** | | |
| [NewLife.Core](https://github.com/NewLifeX/X) | التسجيل والتهيئة والتخزين المؤقت والشبكات والتسلسل والجدولة وتتبّع أداء APM. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Core?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Core) |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | وسيط بيانات: جداول واحدة بمستوى عشرات المليارات من الصفوف، تقسيم تلقائي للجداول، فصل القراءة عن الكتابة، وأكثر من 10 قواعد بيانات. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCode?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCode) |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | مكتبة شبكات عالية الأداء: 1.77 مليون pps / 14.5 Gbps على جهاز واحد (TCP 1KB، 2026-10)، و4 ملايين اتصال TCP طويل الأمد. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Net?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Net) |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | إطار اتصال بروتوكولي وRPC لتطبيقات العميل/الخادم وأجهزة إنترنت الأشياء. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Remoting?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Remoting) |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | يثبّت تطبيقات .NET كخدمات حارسة لنظام التشغيل: خدمات Windows وsystemd في Linux. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Agent?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Agent) |
| [NewLife.Templates](https://github.com/NewLifeX/NewLife.Zero) | قوالب مشاريع `dotnet new` للطرفية وweb وAPI والخدمات والبيانات وإنترنت الأشياء. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Templates?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Templates) |
| **البيانات والمراسلة** | | |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | عميل Redis بزمن استجابة بالميكروثانية، وملايين العمليات في الثانية، وقوائم رسائل مدمجة. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Redis?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Redis) |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | عميل RocketMQ مُدار بالكامل لـ Apache RocketMQ وخدمة رسائل Alibaba Cloud. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.RocketMQ?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.RocketMQ) |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | عميل وخادم MQTT، بما في ذلك الاتصال بـ Alibaba Cloud IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MQTT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MQTT) |
| [NewLife.NovaDb](https://github.com/NewLifeX/NewLife.NovaDb) | قاعدة بيانات هجينة بلغة C#: مدمجة أو شبكية، وعلائقية، وسلاسل زمنية، وقائمة رسائل، وKV في واحد. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.NovaDb?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.NovaDb) |
| [NewLife.MySql](https://github.com/NewLifeX/NewLife.MySql) | برنامج تشغيل MySql وطبقة مساعدة مبنية على مكدّس بيانات NewLife. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MySql?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MySql) |
| **Web والذكاء الاصطناعي والأنظمة الموزّعة** | | |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | إطار منصة إدارة سريعة: الصلاحيات، والدخول الموحّد SSO، وخادم OAuth، وتوليد واجهات CRUD. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Cube?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Cube) |
| [Stardust](https://github.com/NewLifeX/Stardust) | منصة خدمات موزّعة: إدارة العقد، وAPM، ومركز الإعدادات، ومركز التسجيل، ومركز النشر. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Stardust?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Stardust) |
| [AntJob](https://github.com/NewLifeX/AntJob) | منصة موزّعة للمهام وجدولة البيانات الفورية، مع التقسيم وإعادة المحاولة وإعادة التعبئة. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AntJob?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AntJob) |
| [NewLife.AI](https://github.com/NewLifeX/NewLife.AI) | عميل موحّد لـ 46 مزوّد LLM بالإضافة إلى ChatAI، تطبيق ويب جاهز للمحادثة. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AI?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AI) |
| **الصناعة وإنترنت الأشياء** | | |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | مكتبة معايير إنترنت الأشياء التي تعرّف عقود البروتوكولات وتجريدات الأجهزة. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IoT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IoT) |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | تنفيذ Modbus TCP/RTU/ASCII فوق NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Modbus?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Modbus) |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | بروتوكول Siemens PLC فوق NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Siemens?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Siemens) |
| [NewLife.Omron](https://github.com/NewLifeX/NewLife.Omron) | بروتوكول Omron PLC فوق NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Omron?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Omron) |
| [NewLife.Melsec](https://github.com/NewLifeX/NewLife.Melsec) | بروتوكول Mitsubishi Melsec PLC فوق NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Melsec?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Melsec) |
| [NewLife.OPC](https://github.com/NewLifeX/NewLife.OPC) | عميل OPC للوصول إلى البيانات الصناعية. | — |
| [NewLife.BACnet](https://github.com/NewLifeX/NewLife.BACnet) | مكدّس بروتوكول BACnet لأتمتة المباني. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.BACnet?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.BACnet) |
| [ZeroIoT](https://github.com/NewLifeX/ZeroIoT) | منصة إنترنت أشياء مفتوحة المصدر وخفيفة، مع إدارة البوابات والأجهزة. | — |
| **تليماتيكا المركبات** | | |
| [NewLife.JT808](https://github.com/NewLifeX/NewLife.JT808) | بروتوكول تحديد مواقع المركبات JT/T 808: أكثر من 70 جسم رسالة، 2011/2013/2019. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT808?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT808) |
| [NewLife.JT809](https://github.com/NewLifeX/NewLife.JT809) | بروتوكول JT/T 809 بين المنصات لجهات إدارة النقل. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT809?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT809) |
| [NewLife.JT1078](https://github.com/NewLifeX/NewLife.JT1078) | فيديو داخل المركبة JT/T 1078: تحليل RTP وإعادة التجميع إلى FLV/fMP4 والبث المباشر. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT1078?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT1078) |
| **مكوّنات المجال** | | |
| [NewLife.Office](https://github.com/NewLifeX/NewLife.Office) | حزمة واحدة لـ Excel وWord وPPT وPDF وأكثر من 15 صيغة مستندات. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Office?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Office) |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | صوت عبر المنصات: مرمّزات، وإدخال/إخراج الأجهزة، وDSP، وحاويات، وبث. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Audio?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Audio) |
| [NewLife.Video](https://github.com/NewLifeX/NewLife.Video) | مرمّزات الفيديو وصيغ الحاويات ومعالجة الإطارات والبث. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Video?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Video) |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | واجهة موحّدة فوق Baidu وAMap وTencent وTianditu مع الترميز الجغرافي وتحويل الإحداثيات. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Map?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Map) |
| [NewLife.IP](https://github.com/NewLifeX/NewLife.IP) | تحديد الموقع الجغرافي لعناوين IPv4 بقاعدة بيانات غير متصلة مُعيّنة في الذاكرة، وواجهة API عبر الإنترنت كبديل. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IP?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IP) |
| **الأدوات والمنتجات** | | |
| [CrazyCoder](https://github.com/NewLifeX/XCoder) | صندوق أدوات المطوّر: الشبكات والمنفذ التسلسلي والتشفير والتعبيرات النمطية وModbus وMQTT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCoder?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCoder) |
| [GitCandy](https://github.com/NewLifeX/GitCandy) | نظام استضافة ذاتية لإدارة الشيفرة المصدرية Git. | — |
| [LuckyClover](https://github.com/NewLifeX/LuckyClover) | يثبّت بيئة تشغيل .NET المطابقة تلقائيًا عند التشغيل من مضيف أقدم. | — |

> تعرض الشارات الحزمة الرئيسية لكل مشروع؛ أما حزم السمات والإضافات والحزم التابعة فمذكورة في صفحاتها على NuGet. الحزم التي تتجاوز 30 حزمة وغير المذكورة هنا (FTP وLoRa وإضافات Office وبرامج تشغيل البروتوكولات وغيرها) متاحة في [منظمة NewLifeX](https://github.com/NewLifeX) وعلى [newlifex.com](https://newlifex.com). يُصان هذا الجدول في README الخاص بـ NewLife.Core.

## التوثيق

- [دليل توثيق XCode](Doc/XCode文档导航.md): فهرس مستندات كامل مجمَّع حسب الوحدة
- [دليل استخدام XCode](Doc/XCode使用手册.md): جولة استخدام من البداية إلى النهاية
- [تصميم البنية](Doc/架构设计.md): الطبقات والمكوّنات وقرارات التصميم الأساسية
- [الترجمة متعددة اللغات](Doc/多语言本地化.md): نموذج الرسائل وجداول اللغة والتجاوزات في وقت التشغيل
- التوثيق على الإنترنت: https://newlifex.com/xcode
- [ChangeLog.md](ChangeLog.md): سجل الإصدارات

## المساهمة

### البناء والاختبار

```
dotnet restore
dotnet build -c Release
dotnet test XUnitTest.XCode -c Release
```

- أضف حالات الاختبار تحت `XUnitTest.XCode`؛ ويجب أن تتضمّن اقتراحات الأداء قياسات أو تفسيرات.
- اتبع اتفاقيات المستودع (التوقيع القوي، تعدّد TFM، تعليقات XML، الترجمة الشرطية).
- تتطلّب تغييرات واجهة API العامة استراتيجية ترحيل (Obsolete → الإزالة).

### الإصدار

ادفع وسمًا يبدأ بـ `v` (مثل `v2026.1018`) لتشغيل `.github/workflows/publish.yml`. تقوم سير العمل بحزم XCode وXCodeTool وحزم الموفرين (`XCode.MySql`، `XCode.SQLite`، `XCode.SqlServer`، `XCode.Oracle`، `XCode.PostgreSQL`، `XCode.DaMeng`، `XCode.DB2`، `XCode.KingBase`، `XCode.HighGo`) ودفعها إلى NuGet.org وGitHub Packages.

### التغذية الراجعة

- نرحّب بالمشكلات وطلبات الدمج: تحسين الأداء / موفرو قواعد بيانات جدد / تحسينات التوثيق.
- قبل الإرسال، تأكد من نجاح `dotnet build` واختبارات الوحدة ذات الصلة؛ واتبع اتفاقيات المستودع في أسلوب الشيفرة ورسائل الالتزام.
- أبلغ عن الأخطاء والاقتراحات عبر [Issue](https://github.com/NewLifeX/NewLife.XCode/issues)؛ والنجوم وطلبات الدمج مرحّب بها.

## الفريق

<img src="https://newlifex.com/logo.png" width="48" align="right" alt="NewLife" />

تأسّست NewLife في عام 2002، وأمضت أكثر من عقدين في منظومة .NET وإنترنت الأشياء، وتدير ما يقارب 100 مشروع مفتوح المصدر (34 منها مشاريع رئيسية)، وتقدّم حلولًا متكاملة للبرمجيات والأجهزة لقطاعات الكهرباء والخدمات اللوجستية والرعاية الصحية والتحكّم الصناعي والإنترنت. ونلتزم بمبدأ "لا أسبقية في التعلّم — الإتقان يكسب الاحترام".

نُزّلت المكوّنات مفتوحة المصدر <!--nuget-total:start-->6.48M<!--nuget-total:end--> مرة من NuGet (على مستوى المنظومة بأكملها، حتى 2026-10).

الموقع: https://newlifex.com · المنظمة مفتوحة المصدر: https://github.com/NewLifeX

<details>
<summary>حساب WeChat الرسمي: 智能大石头 (امسح للاشتراك)</summary>

![智能大石头](https://newlifex.com/stone.jpg)

</details>

## الترخيص

MIT، مفتوح بالكامل. يُسمح بالاستخدام التجاري / التعديل / إعادة التوزيع (يمكنك استبدال "NewLife" بعلامة مؤسستك للنشر الخاص). شهادة التوقيع القوي `newlife.snk` مدمجة.

> إذا ساعدك هذا المشروع، فيرجى منحه نجمة، أو مشاركته مع فريقك، أو إرسال اقتراحات تحسين عبر Issue/PR.
