# NewLife.XCode – Daten-Middleware / Super-ORM

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
🌐 [English](README.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [Español](README.es.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

> ⚠️ Diese Übersetzung kann veraltet sein, einschließlich ihrer Struktur; maßgeblich ist das [englische README](README.md).

**NewLife.XCode** ist eine hochperformante .NET-Daten-Middleware mit dem Fokus auf „CRUD + extreme Performance + massenhafte Daten“. Sie umfasst mehrstufiges Caching, automatische Modellierung/Migration, Sharding, stark typisierte Abfragen und datenbankübergreifende Migration. Unterstützt werden MySQL / SQLite / SqlServer / Oracle / PostgreSQL / TDengine / InfluxDB / DaMeng / KingBase / HighGo / DB2 / ClickHouse / DuckDB / MongoDB / Hana (SAP HANA) / IRIS (InterSystems) / VastBase / Network virtual database / NovaDb und weitere. In Produktivumgebungen werden zig Milliarden Zeilen pro Tabelle erreicht, und der Abfragedurchsatz kann (mit Caching-Strategien) Milliarden QPS erreichen.

Dokumentation: https://newlifex.com/xcode   Community-QQ-Gruppen: 1600800 / 1600838

**Inhalt**: [Schnellstart](#schnellstart) · [Warum XCode](#warum-xcode) · [Hauptfunktionen](#hauptfunktionen) · [Ökosystem-Karte](#ökosystem-karte) · [Dokumentation](#dokumentation) · [Mitwirken](#mitwirken) · [Team](#team) · [Lizenz](#lizenz)

## Schnellstart

### Installation

NuGet-Referenz (beliebiges Projekt):

```
dotnet add package NewLife.XCode
```

Oder das Projektgerüst aus der Vorlage verwenden:

```
dotnet new install NewLife.Templates
dotnet new xcode --name Demo.Data
cd Demo.Data
dotnet build
```

> Das Strong-Name-Zertifikat ist integriert (newlife.snk); für ein unternehmenseigenes Framework kann neu kompiliert werden.

### Das Modell definieren (db.xml)

```xml
<?xml version="1.0" encoding="utf-8"?>
<Tables Version="1.0" Output="MyEntity" NameSpace="Model" BaseClass="Entity" ConnName="DB">
  <Table Name="UserInfo" Description="Benutzerinformationen">
    <Columns>
      <Column Name="Id" DataType="Int32" Identity="True" PrimaryKey="True" Description="Mitglieds-ID" />
      <Column Name="UserName" DataType="String" Nullable="False" Description="Mitgliedsname" />
      <Column Name="Password" DataType="String" Nullable="False" Description="Mitgliedspasswort" />
      <Column Name="Age" DataType="Int32" Description="Mitgliedsalter" />
    </Columns>
  </Table>
</Tables>
```

### Entitätsklassen generieren

- xcodetool.exe in das Verzeichnis herunterladen und doppelklicken oder ausführen: `dotnet tool install xcodetool -g --prerelease` → `xcode db.xml`.
- Erzeugt: 用户信息.cs (Datenklasse, wird überschrieben) + 用户信息.Biz.cs (Geschäftsklasse, wird nicht überschrieben).

### CRUD

```csharp
using XCode;

var user = new UserInfo { UserName = "Hans Müller", Password = "123456", Age = 18 };
user.Insert();
Console.WriteLine("Neuer Datensatz eingefügt, Benutzer-ID: " + user.Id);

var u2 = UserInfo.FindById(user.Id);
u2.Age = 19;
u2.Update();

var list = UserInfo.FindAll(UserInfo._.Age > 10 & UserInfo._.UserName == "Hans Müller");
UserInfo.FindById(user.Id)?.Delete();
```

> Bei Verwendung von Caches: weniger als 1000 Entitäten → Ganztabellen-Cache; andernfalls reguläre Abfragen / Einzelobjekt-Cache verwenden.

## Warum XCode

- **Eigenentwicklung und eigenständig**: vollständig in China entwickelt, ohne Komponenten von Drittanbietern, selbst kontrolliert und langfristig gepflegt.
- **Einfachheit**: 90 % der Geschäftsszenarien benötigen nur Entitätsoperationen auf einer einzelnen Tabelle – ohne handgeschriebenes SQL.
- **Dapper-kompatibel**: führt beliebige SQL-Abfragen aus und ordnet sie einem beliebigen Entitätsmodell zu; vollständig kompatibel zur Dapper-Nutzung, ergänzt um Caching, Lese-/Schreibtrennung und Tracing.
- **Extreme Performance**: mehrstufiges Caching auf dem Kernpfad + Batch-Optimierung + In-Memory-Joins; die Abfragegeschwindigkeit kann deutlich über rohem SQL liegen.
- **Massive Datenmengen**: native Paging- und Sharding-Strategien; Produktivprojekte erreichen zig Milliarden Zeilen pro Tabelle; die Paging-Algorithmen passen sich an verschiedene Datenbanken an.
- **Stabil und ausgereift**: über 20 Jahre Weiterentwicklung und breite Produktionsvalidierung; bleibt mit älteren .NET-Framework-Versionen kompatibel (ab net45).
- **Automatische Migration**: Modell/Entität ist das Schema; neue Felder werden beim Deployment automatisch synchronisiert – ohne zusätzliche SQL-Skripte.
- **Verteilte Evolution**: native Sharding-Strategien auf Tabellen-, Zeit- und Primärschlüsselbasis für nahtlose Aufteilung und Skalierung.
- **Plug-in-fähig**: DAL-Abstraktion + getrennte Provider; eigene Datenbankadapter werden unterstützt.

## Hauptfunktionen

<details>
<summary><b>Modellierung &amp; Codegenerierung</b> (5)</summary>

| Funktion | Wesentliche Merkmale |
| --- | --- |
| XML-Modell (Tables/Columns/Indexes) | Das Modell ist die einzige Wahrheitsquelle; Attribute decken Indizes, Eindeutigkeitsbedingungen, Standardwerte, Beschreibungen und erweiterte Eigenschaften ab. |
| Codegenerator | `xcodetool` (globales Tool / exe) oder die VS-T4-Vorlage erzeugt aus dem XML-Modell Entitätsklassen. |
| Datenklasse (`.cs`) | Wird bei jeder Generierung überschrieben; enthält Felder und Basisabfragen. |
| Geschäftsklasse (`.Biz.cs`) | Wird nach der ersten Generierung nicht mehr überschrieben; hier gehört eigene Logik hin. |
| Automatisch generierte Methoden | Indizes und Spalten erzeugen automatisch `FindByXxx`- / `Search`-Hilfsmethoden und -Vorlagen. |

</details>

<details>
<summary><b>Entitäts-Laufzeit &amp; DAL</b> (7)</summary>

| Komponente | Verantwortung |
| --- | --- |
| `IEntity` / `Entity` | Reichhaltiges Domänenmodell, das Felder, Validierung und Lebenszyklus kapselt. |
| `IEntityOperate` | Statischer Operations-Einstiegspunkt (Find / FindAll / Query usw.). |
| `EntityFactory` | Fabrik für den Cache der Entitätsmetadaten. |
| `EntitySession` (`Meta.Session`) | Kontext einer Entität auf einer bestimmten Verbindung/Tabelle, mit Cache/Statistik auf Sitzungsebene. |
| `Meta` | Statischer Entitätskontext (Tabellenschema, Felder, Caches, Sharding-Richtlinie usw.). |
| `DAL` / `IDatabase` / `IDbSession` | Einheitlicher Datenbankzugriff und Metadatenoperationen. |
| `CheckTables()` | Prüft und synchronisiert das Schema beim Start/zum ersten Zugriff – je nach Bedarf asynchron oder synchron. |

</details>

<details>
<summary><b>Abfrage-DSL</b> (8)</summary>

| Fähigkeit | Wesentliche Merkmale |
| --- | --- |
| `IQueryable<T>` LINQ (v12.0) | `Entity<T>.Query` liefert `IQueryable<T>` und unterstützt Standard-LINQ-Operationen wie `Where/OrderBy/Skip/Take/Count/First/ToList`. |
| `DAL.Select<T>()`-Fluent-Abfragen (v12.1) | `DAL.Select<T>()` liefert ein an die aktuelle Verbindung gebundenes `IQueryable<T>` und unterstützt vollständig verkettete Fluent-Aufrufe `WhereIf/Where/OrderBy/Skip/Take/Page/Count/FirstOrDefault/ToList`. |
| Navigationseigenschaften (v12.1) | Die `HasOne`/`HasMany`-Fluent-API konfiguriert Navigationsbeziehungen; `Include` baut Pfade auf und lädt gebündelt; `SelectBuilder` unterstützt JOINs über Navigationsbeziehungen hinweg. |
| `WhereIf`-Inline-Bedingungen | `Query.WhereIf(condition, predicate)` und `FindAllWhereIf` ermöglichen dynamische Filterung. |
| `WhereExpression` | Verkettbare Ausdruckskomposition aus `&` / `\|` / `Contains` / `Between` / `Like`. |
| `Find` / `FindAll` / `Query` / `PageFindAll` | Einstiegspunkte für Abfragen; Paging ist in `PageFindAll` integriert. |
| `Pager` / `PageParameter` | Trägt Paging, Sortierung, `TotalCount`-Statistiken und Erweiterungen für den Datenbereich. |
| Fensterfunktionen (v12.0) | `SelectBuilder.Window` + `WindowFunction` unterstützen ROW_NUMBER/RANK/DENSE_RANK sowie Aggregat-Fensterfunktionen. |

</details>

<details>
<summary><b>Mehrstufiges Cache-System</b> (6)</summary>

Ausgelegt auf leselastige, schreibarme Workloads:

| Cache | Wesentliche Merkmale |
| --- | --- |
| `Meta.Count` | Schneller Zeilenzahl-Cache (nutzt Systemindizes bei >1000; SQLite verwendet Max(identity) plus asynchrone exakte Kompensation). |
| `EntityCache` (`Meta.Cache.Entities`) | Ganztabellen-Cache (empfohlen für kleine Tabellen <1000 Zeilen; ab 10000 Zeilen untersagt). |
| `SingleCache` (`Meta.SingleCache[key]`) | Einzelobjekt-Cache auf Dictionary-Ebene nach Primär-/Alternativschlüssel; geeignet für häufige Punktabfragen etwa zu Benutzern. |
| Ablaufrichtlinie | Standardmäßig 60 s. Abgelaufene Lesevorgänge liefern sofort veraltete Daten + aktualisieren asynchron. Schreibvorgänge invalidieren/aktualisieren Caches sofort. |
| Transaktionsbewusstsein | Innerhalb einer Transaktion sammeln sich Änderungen; nach Commit/Rollback werden die Caches einheitlich geleert oder zurückgerollt. |
| Sekundär-Cache (v12.0) | `CacheInvalidator.Provider` bindet Redis ein und koordiniert die Entitäts-Cache-Invalidierung prozessübergreifend über einen Versionsmechanismus. |

</details>

<details>
<summary><b>Reverse Engineering &amp; Migration</b> (3)</summary>

| Fähigkeit | Wesentliche Merkmale |
| --- | --- |
| Schema-Reverse-Engineering | Erzeugt/ändert Tabellenschemata und Indizes rückwärts aus den Entitäten und hält sie automatisch konsistent. |
| Migration pro Entität | Die `Migration`-Konfiguration auf Tabellenebene (Modellattribut `Table` / `[BindTable]` / `TableItem`-Startüberschreibung) steuert die Reverse-Engineering-Strategie je Entität (Off / ReadOnly / On / Full). |
| Modellextraktion & datenbankübergreifende Migration | Unterstützt die Modellextraktion aus beliebigen Datenbanken + datenbankübergreifende Datenmigration (mit dem Begleitwerkzeug DataMigration). |

</details>

<details>
<summary><b>Sharding (ShardPolicy)</b> (3)</summary>

| Fähigkeit | Wesentliche Merkmale |
| --- | --- |
| `ShardPolicy`-Abstraktion | Beispiel `TimeShardPolicy`: Tabellenname Table_yyyyMMdd nach Datum. |
| Transparentes Routing | Geschäftscode ruft weiterhin Find / Insert / Update auf und kümmert sich nicht um den tatsächlichen Tabellennamen. |
| Eigene Strategien | Anpassbare Primärschlüssel-Hash-, Zeit- und Bereichsstrategien. |

</details>

<details>
<summary><b>Transaktionen &amp; Cache-Konsistenz</b> (3)</summary>

| Szenario | Wesentliche Merkmale |
| --- | --- |
| Nicht-transaktionale Schreibvorgänge | Schreibvorgänge lassen zugehörige Caches sofort verfallen und bauen sie asynchron neu auf; dabei wird versucht, In-Memory-Objekte direkt zu aktualisieren, um Lese-/Schreibkonsistenz zu wahren. |
| Commit | Innerhalb einer Transaktion werden Ganztabellen-Caches nicht geleert; nur getroffene Objekte werden lokal geändert; nach dem Commit werden sie einheitlich geleert oder aktualisiert. |
| Rollback | Beim Rollback werden innerhalb der Transaktion vorgenommene Änderungen rückgängig gemacht. |

</details>

<details>
<summary><b>Performance-Optimierungen</b> (6)</summary>

| Technik | Wesentliche Merkmale |
| --- | --- |
| Mehrstufiges Caching | Hält heiße Abfragen von der Datenbank fern. |
| Batch-Schreiben | Fasst Operationen über mehrere Felder und Datensätze zusammen (interne Optimierung). |
| Paging-Algorithmen | Wählt je Datenbank/Version die optimale Strategie. |
| Lazy/asynchron | Zeilenzählungen, Modellprüfungen und Cache-Aktualisierungen können alle asynchron laufen. |
| Reflection-Cache / Delegate-Beschleunigung | Wärmt Eigenschaftszugriffe von Entitäten vorab auf. |
| In-Memory-Verknüpfung | Lädt die Haupttabelle zuerst in den Cache und führt dann „Pseudo-Joins“ im Speicher aus. |

</details>

<details>
<summary><b>Datenbereich &amp; Mehrinstanzenfähigkeit</b> (5)</summary>

| Fähigkeit | Wesentliche Merkmale |
| --- | --- |
| Datenbereich | Zeilenbasierte Datenzugriffskontrolle mit fünf Bereichen: „Alle / Abteilung und Unterabteilungen / Abteilung / nur eigene Daten / benutzerdefiniert“. |
| Bereichs-Anbindung | `DataScopeContext` + `DataScopeInterceptor` wenden automatisch Abfragefilter an, setzen beim Einfügen die Standardzuständigkeit und validieren beim Aktualisieren/Löschen. |
| Opt-in je Entität | Entitäten implementieren `IUserScope` / `IDepartmentScope` / `IDataScope`; Abteilungsleiter werden automatisch in den sichtbaren Bereich einbezogen, und die eigenen Daten sind stets zugänglich. |
| Mehrinstanzenfähigkeit | Sobald eine Entität `ITenantScope` implementiert, hängt `TenantInterceptor` bei Abfragen automatisch Mandantenfilter an und validiert/füllt beim Schreiben die Mandantenkennung; EntityBuilder implementiert `ITenantScope` automatisch für Geschäftsklassen, die eine Spalte `TenantId` enthalten. |
| Details | Siehe `Doc/数据权限.md` und `Doc/权限管理.md` für Einzelheiten. |

</details>

<details>
<summary><b>Konfiguration für mehrere Datenbanken</b> (4)</summary>

| Fähigkeit | Wesentliche Merkmale |
| --- | --- |
| SQLite als Standard | SQLite ist der Standard für die Entwicklung (ohne Konfiguration). |
| Verbindungseinstellungen | Beim Deployment `XCode_{ConnName}` in der Konfigurationsdatei oder in Umgebungsvariablen konfigurieren. |
| Provider-Kennung | Die Verbindungszeichenfolge muss die Provider-Kennung enthalten (zum Beispiel: `provider=MySql;server=...;user=...;******;database=...;`). |
| Mehrere Verbindungen | Mehrere Verbindungsnamen (ConnName) können im selben Prozess gemeinsam verwendet werden. |

</details>

### Anwendungsbeispiele

#### Dynamische Bedingungen und Paging

```csharp
public static IList<UserInfo> Search(String name, Int32 age, Pager p)
{
    var exp = new WhereExpression();
    if (!name.IsNullOrEmpty()) exp &= UserInfo._.UserName == name;
    if (age > 0) exp &= UserInfo._.Age == age;
    return UserInfo.FindAll(exp, p);
}
```

#### Bestimmte Felder aktualisieren (umgeht den Cache; für den regulären Einsatz nicht empfohlen)

```csharp
UserInfo.Update(UserInfo._.UserName == "Anna Weber" & UserInfo._.Age == 18, UserInfo._.Id == 1);
```

#### Eigene Sortierung und Spaltenprojektion

```csharp
var list = UserInfo.FindAll(UserInfo._.Age > 10, " Age DESC,Id ASC",
    string.Join(",", UserInfo._.UserName, UserInfo._.Age), 0, 20);
```

#### Indizes und generierte Methoden

Indizes zum Modell hinzufügen:

```xml
<Indexes>
  <Index Columns="UserName" Unique="True" />
  <Index Columns="UserName,Age" />
</Indexes>
```

Nach der Neugenerierung erscheinen `FindByUserName` sowie Hilfsmethoden für kombinierte Bedingungen / Search-Vorlagen automatisch.

Strategie für den Entitäts-Cache: bei `Meta.Session.Count < 1000` die Suche in der In-Memory-Auflistung verwenden; andernfalls auf die Datenbank oder den Einzelobjekt-Cache zugreifen.

```csharp
public static UserInfo FindByUserName(String userName)
{
    if (userName.IsNullOrEmpty()) return null;
    if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.UserName.EqualIgnoreCase(userName));
    return Find(_.UserName == userName);
}
```

#### Sharding-Beispiel

```csharp
Meta.ShardPolicy = new TimeShardPolicy(nameof(UserInfo.Id), Meta.Factory)
{
    ConnPolicy = "{0}",           // anpassbare Richtlinie für den Datenbanknamen
    TablePolicy = "{0}_{1:yyyyMMdd}", // Vorlage für den Tabellennamen
    Step = TimeSpan.FromDays(1)
};
```

Der gesamte übrige Code wie Insert / Query bleibt unverändert und wird automatisch an die passende Datumstabelle geroutet.

#### Ursprüngliche Beispiele (beibehalten)

<details>
<summary><b>Ursprüngliche CRUD- und komplexe Abfragebeispiele</b></summary>

##### Erstellen, Lesen, Aktualisieren, Löschen

```csharp
var user = new UserInfo();
user.UserName = "Hans Müller";
user.Password = "123456";
user.Age = 18;
user.Insert();
Console.WriteLine("Neuer Datensatz eingefügt, Benutzer-ID: " + user.Id);

var u = UserInfo.FindById(1);
u.UserName = "Hans Müller";
u.Password = "123456";
u.Age = 19;
u.Update();

u = UserInfo.FindById(1);
Console.WriteLine($"Benutzer-ID={u.Id}, Alter aktualisiert auf: {u.Age}");
```

##### Komplexe Abfrage

```csharp
var list = UserInfo.FindAll(UserInfo._.UserName == "Hans Müller" & UserInfo._.Age == 19, UserInfo._.UserName.Desc(), string.Join(",", UserInfo._.UserName, UserInfo._.Age), 0, 0);
```

</details>

### Benchmarks

| Fähigkeit | Kennzahl (typische Umgebung) | Hinweise |
| ---- | ---------------- | ---- |
| Insert auf einer Maschine | bis zu ~890k TPS | Kombinationstests aus Batch / Cache + SQLite / RAM-Disk |
| Abfrage auf einer Maschine | bis zu ~1,8 Milliarden QPS | Szenarien mit kleinen Tabellen + Entitäts-Cache / Einzelobjekt-Cache |
| Datenmenge pro Tabelle | zig Milliarden im Produktivbetrieb | Paging- + Sharding-Strategien |

> Die tatsächliche Leistung hängt von Datenbanktyp / Hardware / Sharding-Strategie / Cache-Trefferrate ab.

### FAQ

- Warum ist der erste Count schnell? `Meta.Count` nutzt Systemindizes; SQLite nimmt möglicherweise zuerst das Identity-Maximum und verfeinert asynchron.
- Wie schnell ist der Cache nach einer Aktualisierung konsistent? Schreibvorgänge im aktuellen Prozess werden sofort wirksam; prozessübergreifend muss man auf den Cache-Ablauf warten oder eine aktive Invalidierungsstrategie verwenden.
- Lässt sich die automatische Migration deaktivieren? `Migration=Off` setzen (global oder pro Tabelle) oder je nach Bedarf ReadOnly/On/Full verwenden.
- Komplexe Abfragen über mehrere Tabellen? Empfohlen sind mehrere Einzeltabellen-Abfragen + In-Memory-Verknüpfung (bei Cache-Treffern schneller und strukturell klarer).

## Ökosystem-Karte

NewLife ist ein geschichtetes Ökosystem unabhängig versionierter Open-Source-Projekte. Die folgende Karte wird wortgleich in allen NewLife-Repositories verwendet; jedes Projekt verlinkt auf sein Repository und sein primäres NuGet-Paket.

| Projekt | Funktion | Downloads |
| --- | --- | --- |
| **Grundlage** | | |
| [NewLife.Core](https://github.com/NewLifeX/X) | Protokollierung, Konfiguration, Caching, Netzwerk, Serialisierung, Zeitplanung und APM-Tracing. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Core?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Core) |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | Daten-Middleware: einzelne Tabellen in der Größenordnung von zig Milliarden Zeilen, automatisches Sharding, Lese-/Schreibtrennung, über 10 Datenbanken. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCode?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCode) |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | Hochperformante Netzwerkbibliothek: 1,77 Mio. pps / 14,5 Gbps auf einer Maschine (TCP 1KB, 2026-10), 4 Mio. langlebige TCP-Verbindungen. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Net?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Net) |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | Protokollkommunikation und RPC-Framework für Client/Server-Anwendungen und IoT-Geräte. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Remoting?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Remoting) |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | Installiert .NET-Anwendungen als Betriebssystem-Dienste: Windows-Dienste und Linux-systemd. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Agent?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Agent) |
| [NewLife.Templates](https://github.com/NewLifeX/NewLife.Zero) | `dotnet new`-Projektvorlagen für Konsolen-, Web-, API-, Dienst-, Daten- und IoT-Projekte. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Templates?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Templates) |
| **Daten & Messaging** | | |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | Redis-Client mit Mikrosekunden-Latenz, Millionen Operationen pro Sekunde und integrierten Nachrichtenwarteschlangen. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Redis?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Redis) |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | Rein verwalteter RocketMQ-Client für Apache RocketMQ und Alibaba Cloud Message Queue. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.RocketMQ?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.RocketMQ) |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | MQTT-Client und -Server, einschließlich Anbindung an Alibaba Cloud IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MQTT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MQTT) |
| [NewLife.NovaDb](https://github.com/NewLifeX/NewLife.NovaDb) | Hybride Datenbank in C#: eingebettet oder vernetzt, relational, Zeitreihen, Nachrichtenwarteschlange und KV in einem. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.NovaDb?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.NovaDb) |
| [NewLife.MySql](https://github.com/NewLifeX/NewLife.MySql) | MySql-Treiber und Hilfsschicht auf Basis des NewLife-Datenstacks. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MySql?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MySql) |
| **Web, KI & Verteiltes** | | |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | Schnelles Admin-Plattform-Framework: Berechtigungen, SSO, OAuth-Server und CRUD-Gerüste. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Cube?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Cube) |
| [Stardust](https://github.com/NewLifeX/Stardust) | Verteilte Dienstplattform: Knotenverwaltung, APM, Konfigurationscenter, Registry und Release-Center. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Stardust?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Stardust) |
| [AntJob](https://github.com/NewLifeX/AntJob) | Plattform für verteilte Aufgaben und Echtzeit-Datenplanung mit Sharding, Wiederholungen und Backfill. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AntJob?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AntJob) |
| [NewLife.AI](https://github.com/NewLifeX/NewLife.AI) | Einheitlicher Client für 46 LLM-Anbieter plus ChatAI, eine sofort einsatzfähige Web-Chat-Anwendung. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AI?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AI) |
| **Industrie & IoT** | | |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | IoT-Standardbibliothek mit Protokollverträgen und Geräteabstraktionen. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IoT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IoT) |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | Modbus-TCP/RTU/ASCII-Implementierung auf Basis von NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Modbus?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Modbus) |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | Siemens-SPS-Protokoll auf Basis von NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Siemens?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Siemens) |
| [NewLife.Omron](https://github.com/NewLifeX/NewLife.Omron) | Omron-SPS-Protokoll auf Basis von NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Omron?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Omron) |
| [NewLife.Melsec](https://github.com/NewLifeX/NewLife.Melsec) | Mitsubishi-Melsec-SPS-Protokoll auf Basis von NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Melsec?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Melsec) |
| [NewLife.OPC](https://github.com/NewLifeX/NewLife.OPC) | OPC-Client für industriellen Datenzugriff. | — |
| [NewLife.BACnet](https://github.com/NewLifeX/NewLife.BACnet) | BACnet-Protokollstack für die Gebäudeautomation. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.BACnet?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.BACnet) |
| [ZeroIoT](https://github.com/NewLifeX/ZeroIoT) | Leichtgewichtige Open-Source-IoT-Plattform mit Gateway- und Geräteverwaltung. | — |
| **Fahrzeug-Telemetrie** | | |
| [NewLife.JT808](https://github.com/NewLifeX/NewLife.JT808) | JT/T-808-Fahrzeugpositionierungsprotokoll: über 70 Nachrichtenkörper, 2011/2013/2019. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT808?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT808) |
| [NewLife.JT809](https://github.com/NewLifeX/NewLife.JT809) | JT/T-809-Protokoll zwischen Plattformen für Verkehrsbehörden. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT809?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT809) |
| [NewLife.JT1078](https://github.com/NewLifeX/NewLife.JT1078) | JT/T-1078-Fahrzeugvideo: RTP-Parsing, FLV/fMP4-Remuxing und Live-Streaming. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT1078?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT1078) |
| **Domänenkomponenten** | | |
| [NewLife.Office](https://github.com/NewLifeX/NewLife.Office) | Ein Paket für Excel, Word, PPT, PDF und über 15 Dokumentformate. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Office?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Office) |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | Plattformübergreifendes Audio: Codecs, Geräte-I/O, DSP, Container und Streaming. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Audio?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Audio) |
| [NewLife.Video](https://github.com/NewLifeX/NewLife.Video) | Videocodecs, Containerformate, Frame-Verarbeitung und Streaming. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Video?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Video) |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | Einheitliche Schnittstelle über Baidu, AMap, Tencent und Tianditu mit Geokodierung und Koordinatenumrechnung. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Map?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Map) |
| [NewLife.IP](https://github.com/NewLifeX/NewLife.IP) | IPv4-Geolokalisierung mit einer offline per Memory-Mapping eingebundenen Datenbank und einem Online-API-Fallback. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IP?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IP) |
| **Werkzeuge & Produkte** | | |
| [CrazyCoder](https://github.com/NewLifeX/XCoder) | Entwickler-Toolbox: Netzwerk, serielle Schnittstelle, Krypto, Regex, Modbus und MQTT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCoder?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCoder) |
| [GitCandy](https://github.com/NewLifeX/GitCandy) | Selbst gehostetes Git-Quellcodeverwaltungssystem. | — |
| [LuckyClover](https://github.com/NewLifeX/LuckyClover) | Installiert beim Start aus einem älteren Host automatisch die passende .NET-Laufzeit. | — |

> Die Badges zeigen das primäre Paket jedes Projekts; Theme-, Erweiterungs- und Satellitenpakete sind auf den jeweiligen NuGet-Seiten aufgeführt. Die hier nicht gelisteten über 30 Pakete (FTP, LoRa, Office-Erweiterungen, Protokolltreiber und mehr) sind in der [NewLifeX-Organisation](https://github.com/NewLifeX) und auf [newlifex.com](https://newlifex.com) verfügbar. Diese Tabelle wird im README von NewLife.Core gepflegt.

## Dokumentation

- [XCode-Dokumentationsnavigation](Doc/XCode文档导航.md): vollständiger Dokumentindex, nach Modulen gruppiert
- [XCode-Benutzerhandbuch](Doc/XCode使用手册.md): durchgängige Einführung in die Anwendung
- [Architekturdesign](Doc/架构设计.md): Schichtung, Komponenten und zentrale Designentscheidungen
- [Mehrsprachige Lokalisierung](Doc/多语言本地化.md): Nachrichtenmodell, Sprachtabellen und Laufzeitüberschreibungen
- Online-Dokumentation: https://newlifex.com/xcode
- [ChangeLog.md](ChangeLog.md): Versionshistorie

## Mitwirken

### Build und Test

```
dotnet restore
dotnet build -c Release
dotnet test XUnitTest.XCode -c Release
```

- Neue Testfälle unter `XUnitTest.XCode` ergänzen; performancebezogene Vorschläge sollten Benchmarks oder Erläuterungen enthalten.
- Die Repository-Konventionen einhalten (Strong Naming, Multi-TFM, XML-Kommentare, bedingte Kompilierung).
- Änderungen an öffentlichen APIs erfordern eine Migrationsstrategie (Obsolete → Entfernung).

### Veröffentlichung

Ein Tag mit Präfix `v` pushen (zum Beispiel `v2026.1018`), um `.github/workflows/publish.yml` auszulösen. Der Workflow packt XCode, XCodeTool und die Provider-Pakete (`XCode.MySql`, `XCode.SQLite`, `XCode.SqlServer`, `XCode.Oracle`, `XCode.PostgreSQL`, `XCode.DaMeng`, `XCode.DB2`, `XCode.KingBase`, `XCode.HighGo`) und pusht sie zu NuGet.org und GitHub Packages.

### Feedback

- Issues / PRs sind willkommen: Performance-Optimierung / neue Datenbank-Provider / Dokumentationsverbesserungen.
- Vor dem Einreichen sicherstellen, dass `dotnet build` und die relevanten Unit-Tests durchlaufen; die Repository-Konventionen für Codestil und Commit-Nachrichten einhalten.
- Fehler und Vorschläge über [Issue](https://github.com/NewLifeX/NewLife.XCode/issues) melden; Stars und PRs sind willkommen.

## Team

<img src="https://newlifex.com/logo.png" width="48" align="right" alt="NewLife" />

Das 2002 gegründete NewLife-Team arbeitet seit über zwei Jahrzehnten am .NET- und IoT-Ökosystem, pflegt nahezu 100 Open-Source-Projekte (34 davon bilden den Kern dieser Karte) und liefert integrierte Software- und Hardwarelösungen für die Energie-, Logistik-, Gesundheits-, Industriesteuerungs- und Internetbranche. Unser Leitspruch lautet „Lernen kennt kein Dienstalter – Meisterschaft verschafft Respekt“.

Open-Source-Komponenten wurden <!--nuget-total:start-->6.48M<!--nuget-total:end--> Mal von NuGet heruntergeladen (ökosystemweit, Stand 2026-10).

Website: https://newlifex.com · Open-Source-Organisation: https://github.com/NewLifeX

<details>
<summary>Offizieller WeChat-Account: 智能大石头 (zum Folgen scannen)</summary>

![智能大石头](https://newlifex.com/stone.jpg)

</details>

## Lizenz

MIT, vollständig offen. Kommerzielle Nutzung / Änderung / Weitergabe sind erlaubt (für private Deployments darf „NewLife“ durch die eigene Unternehmensmarke ersetzt werden). Das Strong-Name-Zertifikat `newlife.snk` ist integriert.

> Wenn dieses Projekt Ihnen hilft, geben Sie ihm bitte einen Star, teilen Sie es mit Ihrem Team oder senden Sie Verbesserungsvorschläge per Issue/PR.
