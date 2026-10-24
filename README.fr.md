# NewLife.XCode - Intergiciel de données / Super ORM

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
🌐 [English](README.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

> ⚠️ Cette traduction peut être obsolète, y compris sa structure ; la version de référence est le [README anglais](README.md).

**NewLife.XCode** est un intergiciel de données .NET hautes performances, axé sur « CRUD + performances extrêmes + données massives ». Il comprend le cache multi-niveaux, la modélisation/migration automatique, le partitionnement (sharding), les requêtes fortement typées et la migration inter-bases de données. Il prend en charge MySQL / SQLite / SqlServer / Oracle / PostgreSQL / TDengine / InfluxDB / DaMeng / KingBase / HighGo / DB2 / ClickHouse / DuckDB / MongoDB / Hana (SAP HANA) / IRIS (InterSystems) / VastBase / base de données virtuelle Network / NovaDb / SonnetDB et bien d'autres. Les déploiements en production atteignent des dizaines de milliards de lignes par table, et le débit de requêtes peut atteindre des milliards de QPS (avec des stratégies de cache).

Documentation : https://newlifex.com/xcode   Groupes QQ de la communauté : 1600800 / 1600838

**Sommaire** : [Démarrage rapide](#démarrage-rapide) · [Pourquoi XCode](#pourquoi-xcode) · [Fonctionnalités principales](#fonctionnalités-principales) · [Carte de l'écosystème](#carte-de-lécosystème) · [Documentation](#documentation) · [Contribuer](#contribuer) · [Équipe](#équipe) · [Licence](#licence)

## Démarrage rapide

### Installation

Référence NuGet (tout projet) :

```
dotnet add package NewLife.XCode
```

Ou utilisez le squelette de modèle :

```
dotnet new install NewLife.Templates
dotnet new xcode --name Demo.Data
cd Demo.Data
dotnet build
```

> Le certificat de nom fort est intégré (newlife.snk) ; vous pouvez recompiler pour un framework d'entreprise privé.

### Définir le modèle (db.xml)

```xml
<?xml version="1.0" encoding="utf-8"?>
<Tables Version="1.0" Output="MyEntity" NameSpace="Model" BaseClass="Entity" ConnName="DB">
  <Table Name="UserInfo" Description="Informations utilisateur">
    <Columns>
      <Column Name="Id" DataType="Int32" Identity="True" PrimaryKey="True" Description="ID du membre" />
      <Column Name="UserName" DataType="String" Nullable="False" Description="Nom du membre" />
      <Column Name="Password" DataType="String" Nullable="False" Description="Mot de passe du membre" />
      <Column Name="Age" DataType="Int32" Description="Âge du membre" />
    </Columns>
  </Table>
</Tables>
```

### Générer les classes d'entités

- Téléchargez xcodetool.exe dans le répertoire et double-cliquez dessus, ou exécutez : `dotnet tool install xcodetool -g --prerelease` → `xcode db.xml`.
- Génère : 用户信息.cs (classe de données, écrasée) + 用户信息.Biz.cs (classe métier, non écrasée).

### CRUD

```csharp
using XCode;

var user = new UserInfo { UserName = "Jean Dupont", Password = "123456", Age = 18 };
user.Insert();
Console.WriteLine("Nouvel enregistrement inséré, ID utilisateur : " + user.Id);

var u2 = UserInfo.FindById(user.Id);
u2.Age = 19;
u2.Update();

var list = UserInfo.FindAll(UserInfo._.Age > 10 & UserInfo._.UserName == "Jean Dupont");
UserInfo.FindById(user.Id)?.Delete();
```

> Lors de l'utilisation des caches : moins de 1000 entités → cache de table complète ; sinon, utilisez des requêtes classiques / le cache d'objets uniques.

## Pourquoi XCode

- **Original et développé en interne** : entièrement développé en Chine, sans composants tiers, maîtrisé et soutenu par une maintenance à long terme.
- **Simplicité** : 90 % des scénarios métier ne nécessitent que des opérations d'entité sur une seule table, sans SQL écrit à la main.
- **Compatible Dapper** : exécute des requêtes SQL arbitraires et les mappe à n'importe quel modèle d'entité ; entièrement compatible avec l'usage de Dapper, avec en plus le cache, la séparation lecture/écriture et le traçage.
- **Performances extrêmes** : cache multi-niveaux sur le chemin critique + optimisation par lots + jointures en mémoire ; la vitesse d'interrogation peut être bien supérieure au SQL brut.
- **Données massives** : stratégies natives de pagination et de partitionnement ; les projets en production atteignent des dizaines de milliards de lignes par table ; les algorithmes de pagination s'adaptent à plusieurs bases de données.
- **Stable et mature** : plus de 20 ans d'évolution et une large validation en production ; conserve la compatibilité avec les anciennes versions de .NET Framework (depuis net45).
- **Migration automatique** : le modèle/entité est le schéma ; les nouveaux champs sont synchronisés automatiquement au déploiement, sans scripts SQL supplémentaires.
- **Évolution distribuée** : stratégies natives de partitionnement par table / temps / clé primaire pour une découpe et une montée en charge fluides.
- **Extensible** : abstraction DAL + fournisseurs séparés ; des adaptateurs de base de données personnalisés sont pris en charge.

## Fonctionnalités principales

<details>
<summary><b>Modélisation &amp; génération de code</b> (5)</summary>

| Fonctionnalité | Caractéristiques clés |
| --- | --- |
| Modèle XML (Tables/Columns/Indexes) | Le modèle est l'unique source de vérité ; les attributs couvrent les index, les contraintes d'unicité, les valeurs par défaut, les descriptions et les propriétés étendues. |
| Générateur de code | `xcodetool` (outil global / exe) ou le modèle T4 de Visual Studio transforme le modèle XML en classes d'entités. |
| Classe de données (`.cs`) | Remplacée à chaque génération ; contient les champs et les requêtes de base. |
| Classe métier (`.Biz.cs`) | Non remplacée après la première génération ; placez-y votre logique personnalisée. |
| Méthodes générées automatiquement | Les index et les colonnes produisent automatiquement des méthodes d'assistance `FindByXxx` / `Search` et des modèles. |

</details>

<details>
<summary><b>Exécution des entités &amp; DAL</b> (7)</summary>

| Composant | Responsabilité |
| --- | --- |
| `IEntity` / `Entity` | Modèle de domaine riche encapsulant les champs, la validation et le cycle de vie. |
| `IEntityOperate` | Point d'entrée statique des opérations (Find / FindAll / Query, etc.). |
| `EntityFactory` | Fabrique de cache des métadonnées d'entités. |
| `EntitySession` (`Meta.Session`) | Contexte d'une entité sur une connexion/table donnée, avec cache/statistiques au niveau de la session. |
| `Meta` | Contexte statique de l'entité (schéma de table, champs, caches, politique de partitionnement, etc.). |
| `DAL` / `IDatabase` / `IDbSession` | Accès unifié à la base de données et opérations sur les métadonnées. |
| `CheckTables()` | Vérifie et synchronise le schéma au démarrage/premier accès, de manière asynchrone ou synchrone selon les besoins. |

</details>

<details>
<summary><b>DSL de requête</b> (8)</summary>

| Capacité | Caractéristiques clés |
| --- | --- |
| `IQueryable<T>` LINQ (v12.0) | `Entity<T>.Query` renvoie `IQueryable<T>` et prend en charge les opérations LINQ standard telles que `Where/OrderBy/Skip/Take/Count/First/ToList`. |
| Requêtes fluides `DAL.Select<T>()` (v12.1) | `DAL.Select<T>()` renvoie un `IQueryable<T>` lié à la connexion courante et prend en charge les appels fluides entièrement chaînés `WhereIf/Where/OrderBy/Skip/Take/Page/Count/FirstOrDefault/ToList`. |
| Propriétés de navigation (v12.1) | L'API Fluent `HasOne`/`HasMany` configure les relations de navigation ; `Include` construit les chemins et effectue un chargement par lots ; `SelectBuilder` prend en charge les JOIN sur les relations de navigation. |
| Conditions en ligne `WhereIf` | `Query.WhereIf(condition, predicate)` et `FindAllWhereIf` permettent un filtrage dynamique. |
| `WhereExpression` | Composition d'expressions chaînables avec `&` / `\|` / `Contains` / `Between` / `Like`. |
| `Find` / `FindAll` / `Query` / `PageFindAll` | Points d'entrée des requêtes, avec pagination intégrée à `PageFindAll`. |
| `Pager` / `PageParameter` | Transporte la pagination, le tri, les statistiques `TotalCount` et les extensions de portée des données. |
| Fonctions de fenêtrage (v12.0) | `SelectBuilder.Window` + `WindowFunction` prennent en charge ROW_NUMBER/RANK/DENSE_RANK et les fonctions de fenêtrage d'agrégation. |

</details>

<details>
<summary><b>Système de cache multi-niveaux</b> (6)</summary>

Conçu pour les charges de travail à forte lecture et faible écriture :

| Cache | Caractéristiques clés |
| --- | --- |
| `Meta.Count` | Cache rapide du nombre de lignes (utilise les index système au-delà de 1000 ; SQLite utilise Max(identity) plus une compensation exacte asynchrone). |
| `EntityCache` (`Meta.Cache.Entities`) | Cache de table complète (recommandé pour les petites tables <1000 lignes ; interdit au-delà de 10000 lignes). |
| `SingleCache` (`Meta.SingleCache[key]`) | Cache d'objet unique au niveau dictionnaire par clé primaire/alternative ; adapté aux recherches ponctuelles fréquentes telles que les utilisateurs. |
| Politique d'expiration | 60 s par défaut. Les lectures expirées renvoient immédiatement des données périmées + rafraîchissent en asynchrone. Les écritures invalident/mettent à jour les caches immédiatement. |
| Prise en compte des transactions | Dans une transaction, les modifications s'accumulent ; les caches sont vidés ou restaurés uniformément après validation/annulation. |
| Cache de second niveau (v12.0) | `CacheInvalidator.Provider` injecte Redis et coordonne l'invalidation des caches d'entités entre processus via un mécanisme de version. |

</details>

<details>
<summary><b>Ingénierie inverse &amp; migration</b> (3)</summary>

| Capacité | Caractéristiques clés |
| --- | --- |
| Ingénierie inverse du schéma | Crée/modifie les schémas de table et les index à partir des entités, en les maintenant automatiquement cohérents. |
| Politique de migration par entité | La configuration `Migration` au niveau de la table (attribut `Table` du modèle / `[BindTable]` / surcharge au démarrage via `TableItem`) contrôle la stratégie d'ingénierie inverse par entité (Off / ReadOnly / On / Full). |
| Extraction de modèle & migration inter-bases | Prend en charge l'extraction de modèle depuis n'importe quelle base de données + la migration de données inter-bases (avec l'outil compagnon DataMigration). |

</details>

<details>
<summary><b>Partitionnement (ShardPolicy)</b> (3)</summary>

| Capacité | Caractéristiques clés |
| --- | --- |
| Abstraction `ShardPolicy` | Exemple `TimeShardPolicy` : nom de table Table_yyyyMMdd par date. |
| Routage transparent | Le code métier appelle toujours Find / Insert / Update et ne se soucie pas du nom réel de la table. |
| Stratégies personnalisées | Stratégies personnalisables par hachage de clé primaire / temps / plage. |

</details>

<details>
<summary><b>Transactions &amp; cohérence du cache</b> (3)</summary>

| Scénario | Caractéristiques clés |
| --- | --- |
| Écritures non transactionnelles | Les écritures expirent immédiatement les caches associés et les reconstruisent en asynchrone, tout en tentant de mettre à jour directement les objets en mémoire pour assurer la cohérence lecture/écriture. |
| Validation | Dans une transaction, les caches de table complète ne sont pas vidés ; seuls les objets touchés sont modifiés localement ; après validation, ils sont vidés ou rafraîchis uniformément. |
| Annulation | Lors d'une annulation, les modifications effectuées dans la transaction sont annulées. |

</details>

<details>
<summary><b>Optimisations de performances</b> (6)</summary>

| Technique | Caractéristiques clés |
| --- | --- |
| Cache multi-niveaux | Garde les requêtes fréquentes hors de la base de données. |
| Écritures par lots | Fusionne les opérations multi-champs et multi-enregistrements (optimisation interne). |
| Algorithmes de pagination | Choisit la stratégie optimale selon la base de données/version. |
| Différé/asynchrone | Les comptages de lignes, les vérifications de modèle et les rafraîchissements de cache peuvent tous s'exécuter en asynchrone. |
| Cache de réflexion / accélération par délégués | Pré-chauffe les accesseurs de propriétés des entités. |
| Association en mémoire | Met d'abord la table principale en cache, puis effectue des « pseudo-jointures » en mémoire. |

</details>

<details>
<summary><b>Portée des données &amp; multi-locataires</b> (5)</summary>

| Capacité | Caractéristiques clés |
| --- | --- |
| Portée des données | Contrôle d'accès aux données au niveau des lignes selon cinq portées : « Toutes / Département et sous-départements / Département / Uniquement soi-même / Personnalisée ». |
| Câblage des portées | `DataScopeContext` + `DataScopeInterceptor` appliquent automatiquement les filtres de requête, définissent la propriété par défaut à l'insertion et valident lors des mises à jour/suppressions. |
| Activation par entité | Les entités implémentent `IUserScope` / `IDepartmentScope` / `IDataScope` ; les responsables de département sont automatiquement inclus dans la portée visible, et ses propres données restent toujours accessibles. |
| Multi-locataires | Une fois qu'une entité implémente `ITenantScope`, `TenantInterceptor` ajoute automatiquement les filtres de locataire aux requêtes et valide/renseigne l'identifiant de locataire lors des écritures ; EntityBuilder implémente automatiquement `ITenantScope` pour les classes métier contenant une colonne `TenantId`. |
| Détails | Voir `Doc/数据权限.md` et `Doc/权限管理.md` pour plus de détails. |

</details>

<details>
<summary><b>Configuration multi-bases de données</b> (4)</summary>

| Capacité | Caractéristiques clés |
| --- | --- |
| SQLite par défaut | SQLite est la base par défaut pour le développement (zéro configuration). |
| Paramètres de connexion | Au déploiement, configurez `XCode_{ConnName}` dans le fichier de configuration ou les variables d'environnement. |
| Identifiant de fournisseur | La chaîne de connexion doit inclure l'identifiant du fournisseur (par exemple : `provider=MySql;server=...;user=...;******;database=...;`). |
| Connexions multiples | Plusieurs noms de connexion (ConnName) peuvent coexister dans le même processus. |

</details>

### Exemples d'utilisation

#### Conditions dynamiques et pagination

```csharp
public static IList<UserInfo> Search(String name, Int32 age, Pager p)
{
    var exp = new WhereExpression();
    if (!name.IsNullOrEmpty()) exp &= UserInfo._.UserName == name;
    if (age > 0) exp &= UserInfo._.Age == age;
    return UserInfo.FindAll(exp, p);
}
```

#### Mise à jour de champs spécifiés (contourne le cache ; non recommandé en usage courant)

```csharp
UserInfo.Update(UserInfo._.UserName == "Marie Durand" & UserInfo._.Age == 18, UserInfo._.Id == 1);
```

#### Tri personnalisé et projection de colonnes

```csharp
var list = UserInfo.FindAll(UserInfo._.Age > 10, " Age DESC,Id ASC",
    string.Join(",", UserInfo._.UserName, UserInfo._.Age), 0, 20);
```

#### Index et méthodes générées

Ajoutez des index au modèle :

```xml
<Indexes>
  <Index Columns="UserName" Unique="True" />
  <Index Columns="UserName,Age" />
</Indexes>
```

Après régénération, `FindByUserName`, les méthodes d'assistance pour conditions combinées / modèles Search apparaissent automatiquement.

Stratégie de cache d'entités : lorsque `Meta.Session.Count < 1000`, utilisez les recherches dans la collection en mémoire ; sinon, passez par la base de données ou le cache d'objets uniques.

```csharp
public static UserInfo FindByUserName(String userName)
{
    if (userName.IsNullOrEmpty()) return null;
    if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.UserName.EqualIgnoreCase(userName));
    return Find(_.UserName == userName);
}
```

#### Exemple de partitionnement (sharding)

```csharp
Meta.ShardPolicy = new TimeShardPolicy(nameof(UserInfo.Id), Meta.Factory)
{
    ConnPolicy = "{0}",           // politique de nom de base de données personnalisable
    TablePolicy = "{0}_{1:yyyyMMdd}", // modèle de nom de table
    Step = TimeSpan.FromDays(1)
};
```

Tout le reste du code, comme Insert / Query, reste inchangé et est routé automatiquement vers la table de la date correspondante.

#### Exemples d'origine (conservés)

<details>
<summary><b>Exemples CRUD et requêtes complexes d'origine</b></summary>

##### Créer, lire, mettre à jour, supprimer

```csharp
var user = new UserInfo();
user.UserName = "Jean Dupont";
user.Password = "123456";
user.Age = 18;
user.Insert();
Console.WriteLine("Nouvel enregistrement inséré, ID utilisateur : " + user.Id);

var u = UserInfo.FindById(1);
u.UserName = "Jean Dupont";
u.Password = "123456";
u.Age = 19;
u.Update();

u = UserInfo.FindById(1);
Console.WriteLine($"ID utilisateur={u.Id}, âge mis à jour : {u.Age}");
```

##### Requête complexe

```csharp
var list = UserInfo.FindAll(UserInfo._.UserName == "Jean Dupont" & UserInfo._.Age == 19, UserInfo._.UserName.Desc(), string.Join(",", UserInfo._.UserName, UserInfo._.Age), 0, 0);
```

</details>

### Benchmarks

| Capacité | Métrique (environnement typique) | Remarques |
| ---- | ---------------- | ---- |
| Insertion sur une seule machine | jusqu'à ~890k TPS | tests combinés batch / cache + SQLite / disque RAM |
| Requêtes sur une seule machine | jusqu'à ~1,8 milliard de QPS | scénarios petites tables + cache d'entités / cache d'objets uniques |
| Volume de données par table | dizaines de milliards en production | stratégies de pagination + partitionnement |

> Les performances réelles dépendent du type de base de données / du matériel / de la stratégie de partitionnement / du taux de succès du cache.

### FAQ

- Pourquoi le premier Count est-il rapide ? `Meta.Count` utilise les index système ; SQLite peut d'abord prendre le maximum d'identité puis affiner en asynchrone.
- Au bout de combien de temps le cache est-il cohérent après une mise à jour ? Les écritures du processus courant sont reflétées immédiatement ; entre processus, attendez l'expiration du cache ou utilisez une stratégie d'invalidation active.
- Peut-on désactiver la migration automatique ? Définissez `Migration=Off` (globalement ou par table), ou utilisez ReadOnly/On/Full selon vos besoins.
- Requêtes complexes multi-tables ? Les requêtes multiples sur une seule table + l'association en mémoire sont recommandées (plus rapides avec des cache hits et une structure plus claire).

## Carte de l'écosystème

NewLife est un écosystème en couches de projets open source versionnés indépendamment. La carte ci-dessous est reprise à l'identique par tous les dépôts NewLife ; chaque projet renvoie à son dépôt et à son paquet NuGet principal.

| Projet | Rôle | Téléchargements |
| --- | --- | --- |
| **Fondation** | | |
| [NewLife.Core](https://github.com/NewLifeX/X) | Journalisation, configuration, cache, réseau, sérialisation, ordonnancement et traçage APM. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Core?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Core) |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | Intergiciel de données : tables uniques à l'échelle de dizaines de milliards, partitionnement automatique, séparation lecture/écriture et plus de 10 bases de données. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCode?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCode) |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | Bibliothèque réseau hautes performances : 1,77 M pps / 14,5 Gbps sur une machine (TCP 1KB, 2026-10), 4 M connexions TCP persistantes. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Net?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Net) |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | Communication protocolaire et framework RPC pour applications client/serveur et appareils IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Remoting?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Remoting) |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | Installe des applications .NET comme démons système : services Windows et systemd Linux. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Agent?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Agent) |
| [NewLife.Templates](https://github.com/NewLifeX/NewLife.Zero) | Modèles de projet `dotnet new` pour console, web, API, service, données et IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Templates?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Templates) |
| **Données & messagerie** | | |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | Client Redis avec une latence en microsecondes, des millions d'opérations/s et des files de messages intégrées. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Redis?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Redis) |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | Client RocketMQ entièrement managé pour Apache RocketMQ et Alibaba Cloud Message Queue. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.RocketMQ?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.RocketMQ) |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | Client et serveur MQTT, y compris la connectivité Alibaba Cloud IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MQTT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MQTT) |
| [NewLife.NovaDb](https://github.com/NewLifeX/NewLife.NovaDb) | Base de données hybride en C# : embarquée ou en réseau, relationnelle, séries temporelles, file de messages et KV réunis. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.NovaDb?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.NovaDb) |
| [NewLife.MySql](https://github.com/NewLifeX/NewLife.MySql) | Pilote et couche d'assistance MySql construits sur la pile de données NewLife. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MySql?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MySql) |
| **Web, IA & distribué** | | |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | Framework rapide de plateforme d'administration : permissions, SSO, serveur OAuth et échafaudage CRUD. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Cube?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Cube) |
| [Stardust](https://github.com/NewLifeX/Stardust) | Plateforme de services distribués : gestion des nœuds, APM, centre de configuration, registre et centre de publication. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Stardust?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Stardust) |
| [AntJob](https://github.com/NewLifeX/AntJob) | Plateforme de tâches distribuées et d'ordonnancement de données en temps réel avec partitionnement, reprises et backfill. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AntJob?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AntJob) |
| [NewLife.AI](https://github.com/NewLifeX/NewLife.AI) | Client unifié pour 46 fournisseurs de LLM, plus ChatAI, une application web de chat prête à l'emploi. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AI?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AI) |
| **Industriel & IoT** | | |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | Bibliothèque standard IoT définissant les contrats de protocole et les abstractions d'appareils. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IoT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IoT) |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | Implémentation Modbus TCP/RTU/ASCII au-dessus de NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Modbus?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Modbus) |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | Protocole pour automate Siemens au-dessus de NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Siemens?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Siemens) |
| [NewLife.Omron](https://github.com/NewLifeX/NewLife.Omron) | Protocole pour automate Omron au-dessus de NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Omron?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Omron) |
| [NewLife.Melsec](https://github.com/NewLifeX/NewLife.Melsec) | Protocole pour automate Mitsubishi Melsec au-dessus de NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Melsec?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Melsec) |
| [NewLife.OPC](https://github.com/NewLifeX/NewLife.OPC) | Client OPC pour l'accès aux données industrielles. | — |
| [NewLife.BACnet](https://github.com/NewLifeX/NewLife.BACnet) | Pile de protocoles BACnet pour l'automatisation des bâtiments. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.BACnet?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.BACnet) |
| [ZeroIoT](https://github.com/NewLifeX/ZeroIoT) | Plateforme IoT open source légère avec gestion des passerelles et des appareils. | — |
| **Télématique embarquée** | | |
| [NewLife.JT808](https://github.com/NewLifeX/NewLife.JT808) | Protocole de positionnement de véhicules JT/T 808 : plus de 70 corps de message, 2011/2013/2019. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT808?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT808) |
| [NewLife.JT809](https://github.com/NewLifeX/NewLife.JT809) | Protocole plateforme à plateforme JT/T 809 pour les autorités de transport. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT809?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT809) |
| [NewLife.JT1078](https://github.com/NewLifeX/NewLife.JT1078) | Vidéo embarquée JT/T 1078 : analyse RTP, remuxage FLV/fMP4 et diffusion en direct. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT1078?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT1078) |
| **Composants métier** | | |
| [NewLife.Office](https://github.com/NewLifeX/NewLife.Office) | Un seul paquet pour Excel, Word, PPT, PDF et plus de 15 formats de documents. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Office?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Office) |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | Audio multiplateforme : codecs, E/S d'appareils, DSP, conteneurs et streaming. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Audio?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Audio) |
| [NewLife.Video](https://github.com/NewLifeX/NewLife.Video) | Codecs vidéo, formats de conteneurs, traitement des images et streaming. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Video?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Video) |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | Interface unifiée pour Baidu, AMap, Tencent et Tianditu avec géocodage et conversion de coordonnées. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Map?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Map) |
| [NewLife.IP](https://github.com/NewLifeX/NewLife.IP) | Géolocalisation IPv4 avec une base de données hors ligne mappée en mémoire et une API en ligne en secours. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IP?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IP) |
| **Outils & produits** | | |
| [CrazyCoder](https://github.com/NewLifeX/XCoder) | Boîte à outils du développeur : réseau, port série, cryptographie, regex, Modbus et MQTT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCoder?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCoder) |
| [GitCandy](https://github.com/NewLifeX/GitCandy) | Système de gestion de code source Git auto-hébergé. | — |
| [LuckyClover](https://github.com/NewLifeX/LuckyClover) | Installe automatiquement le runtime .NET correspondant lorsqu'il est lancé depuis un hôte plus ancien. | — |

> Les badges indiquent le paquet principal de chaque projet ; les paquets de thème, d'extension et satellites sont répertoriés sur leurs pages NuGet. Les plus de 30 paquets non listés ici (FTP, LoRa, extensions Office, pilotes de protocole, etc.) sont disponibles dans l'[organisation NewLifeX](https://github.com/NewLifeX) et sur [newlifex.com](https://newlifex.com). Ce tableau est maintenu dans le README de NewLife.Core.

## Documentation

- [Navigation de la documentation XCode](Doc/XCode文档导航.md) : index complet des documents, regroupés par module
- [Manuel d'utilisation XCode](Doc/XCode使用手册.md) : parcours d'utilisation de bout en bout
- [Conception de l'architecture](Doc/架构设计.md) : couches, composants et décisions de conception clés
- [Localisation multilingue](Doc/多语言本地化.md) : modèle de messages, tables de langues et substitutions à l'exécution
- Documentation en ligne : https://newlifex.com/xcode
- [ChangeLog.md](ChangeLog.md) : historique des versions

## Contribuer

### Compilation et tests

```
dotnet restore
dotnet build -c Release
dotnet test XUnitTest.XCode -c Release
```

- Ajoutez des cas de test sous `XUnitTest.XCode` ; les suggestions liées aux performances doivent inclure des benchmarks ou des explications.
- Respectez les conventions du dépôt (nommage fort, multi-TFM, commentaires XML, compilation conditionnelle).
- Toute modification de l'API publique nécessite une stratégie de migration (Obsolete → suppression).

### Publication

Poussez un tag préfixé par `v` (par exemple `v2026.1018`) pour déclencher `.github/workflows/publish.yml`. Le workflow empaquette XCode, XCodeTool et les paquets de fournisseurs (`XCode.MySql`, `XCode.SQLite`, `XCode.SqlServer`, `XCode.Oracle`, `XCode.PostgreSQL`, `XCode.DaMeng`, `XCode.DB2`, `XCode.KingBase`, `XCode.HighGo`) et les pousse vers NuGet.org et GitHub Packages.

### Retour d'information

- Les Issues / PR sont les bienvenues : optimisation des performances / nouveaux fournisseurs de bases de données / améliorations de la documentation.
- Avant de soumettre, assurez-vous que `dotnet build` et les tests unitaires concernés passent ; respectez les conventions du dépôt pour le style de code et les messages de commit.
- Signalez les bogues et suggestions via une [Issue](https://github.com/NewLifeX/NewLife.XCode/issues) ; les Stars et PR sont les bienvenues.

## Équipe

<img src="https://newlifex.com/logo.png" width="48" align="right" alt="NewLife" />

Fondée en 2002, l'équipe NewLife consacre plus de deux décennies à l'écosystème .NET et IoT, maintient près de 100 projets open source (dont 34 sont au cœur de cette carte) et fournit des solutions matérielles et logicielles intégrées aux secteurs de l'énergie, de la logistique, de la santé, du contrôle industriel et d'Internet. Notre devise : « l'apprentissage ne connaît pas l'ancienneté — la maîtrise forge le respect ».

Les composants open source ont été téléchargés <!--nuget-total:start-->6.48M<!--nuget-total:end--> fois depuis NuGet (à l'échelle de l'écosystème, en date de 2026-10).

Site web : https://newlifex.com · Organisation open source : https://github.com/NewLifeX

<details>
<summary>Compte officiel WeChat : 智能大石头 (scannez pour suivre)</summary>

![智能大石头](https://newlifex.com/stone.jpg)

</details>

## Licence

MIT, entièrement ouvert. L'utilisation commerciale / la modification / la redistribution sont autorisées (vous pouvez remplacer « NewLife » par la marque de votre entreprise pour des déploiements privés). Le certificat de nom fort `newlife.snk` est intégré.

> Si ce projet vous aide, merci de lui donner une Star, de le partager avec votre équipe ou d'envoyer des suggestions d'amélioration via Issue/PR.
