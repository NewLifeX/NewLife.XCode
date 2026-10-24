# NewLife.XCode - Middleware de datos / Super ORM

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
🌐 [English](README.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

**NewLife.XCode** es un middleware de datos .NET de alto rendimiento centrado en «CRUD + rendimiento extremo + datos masivos». Incluye caché multinivel, modelado y migración automáticos, sharding, consultas fuertemente tipadas y migración entre bases de datos. Es compatible con MySQL / SQLite / SqlServer / Oracle / PostgreSQL / TDengine / InfluxDB / DaMeng / KingBase / HighGo / DB2 / ClickHouse / DuckDB / MongoDB / Hana (SAP HANA) / IRIS (InterSystems) / VastBase / base de datos virtual Network / NovaDb / SonnetDB y muchas más. Los despliegues en producción alcanzan decenas de miles de millones de filas por tabla, y el rendimiento de consulta puede llegar a miles de millones de QPS (con estrategias de caché).

Documentación: https://newlifex.com/xcode   Grupos QQ de la comunidad: 1600800 / 1600838

**Contenido**: [Inicio rápido](#inicio-rápido) · [Por qué XCode](#por-qué-xcode) · [Funciones principales](#funciones-principales) · [Mapa del ecosistema](#mapa-del-ecosistema) · [Documentación](#documentación) · [Contribuir](#contribuir) · [Equipo](#equipo) · [Licencia](#licencia)

## Inicio rápido

### Instalación

Referencia NuGet (cualquier proyecto):

```
dotnet add package NewLife.XCode
```

O usa el andamiaje de plantillas:

```
dotnet new install NewLife.Templates
dotnet new xcode --name Demo.Data
cd Demo.Data
dotnet build
```

> El certificado de nombre seguro (strong name) viene integrado (newlife.snk); puedes recompilar para un framework empresarial privado.

### Definir el modelo (db.xml)

```xml
<?xml version="1.0" encoding="utf-8"?>
<Tables Version="1.0" Output="MyEntity" NameSpace="Model" BaseClass="Entity" ConnName="DB">
  <Table Name="UserInfo" Description="Información del usuario">
    <Columns>
      <Column Name="Id" DataType="Int32" Identity="True" PrimaryKey="True" Description="Id del miembro" />
      <Column Name="UserName" DataType="String" Nullable="False" Description="Nombre del miembro" />
      <Column Name="Password" DataType="String" Nullable="False" Description="Contraseña del miembro" />
      <Column Name="Age" DataType="Int32" Description="Edad del miembro" />
    </Columns>
  </Table>
</Tables>
```

### Generar las clases de entidad

- Descarga xcodetool.exe en el directorio y haz doble clic, o ejecuta: `dotnet tool install xcodetool -g --prerelease` → `xcode db.xml`.
- Genera: 用户信息.cs (clase de datos, se sobrescribe) + 用户信息.Biz.cs (clase de negocio, no se sobrescribe).

### CRUD

```csharp
using XCode;

var user = new UserInfo { UserName = "Zhang San", Password = "123456", Age = 18 };
user.Insert();
Console.WriteLine("Se insertó un nuevo registro, id de usuario: " + user.Id);

var u2 = UserInfo.FindById(user.Id);
u2.Age = 19;
u2.Update();

var list = UserInfo.FindAll(UserInfo._.Age > 10 & UserInfo._.UserName == "Zhang San");
UserInfo.FindById(user.Id)?.Delete();
```

> Al usar cachés: menos de 1000 entidades → caché de tabla completa; en caso contrario, usa consultas normales / caché de objeto único.

## Por qué XCode

- **Desarrollo propio y original**: desarrollado íntegramente en China sin componentes de terceros, autocontrolado y respaldado por un mantenimiento a largo plazo.
- **Simplicidad**: el 90 % de los escenarios de negocio solo necesitan operaciones de entidad sobre una única tabla, sin SQL escrito a mano.
- **Compatible con Dapper**: ejecuta consultas SQL arbitrarias y las mapea a cualquier modelo de entidad; totalmente compatible con el uso de Dapper y, además, con caché, separación lectura/escritura y trazabilidad.
- **Rendimiento extremo**: caché multinivel en la ruta crítica + optimización por lotes + joins en memoria; la velocidad de consulta puede superar ampliamente al SQL puro.
- **Datos masivos**: estrategias nativas de paginación y sharding; proyectos en producción alcanzan decenas de miles de millones de filas por tabla; los algoritmos de paginación se adaptan a múltiples bases de datos.
- **Estable y maduro**: más de 20 años de evolución y una amplia validación en producción; mantiene la compatibilidad con versiones anteriores de .NET Framework (desde net45).
- **Migración automática**: el modelo/entidad es el esquema; los nuevos campos se sincronizan automáticamente al desplegar, sin scripts SQL adicionales.
- **Evolución distribuida**: estrategias nativas de sharding por tabla / tiempo / clave primaria para una división y un escalado fluidos.
- **Conectable**: abstracción DAL + proveedores separados; se admiten adaptadores de base de datos personalizados.

## Funciones principales

<details>
<summary><b>Modelado y generación de código</b> (5)</summary>

| Característica | Características clave |
| --- | --- |
| Modelo XML (Tables/Columns/Indexes) | El modelo es la única fuente de verdad; los atributos cubren índices, restricciones únicas, valores predeterminados, descripciones y propiedades extendidas. |
| Generador de código | `xcodetool` (herramienta global / exe) o la plantilla T4 de VS convierten el modelo XML en clases de entidad. |
| Clase de datos (`.cs`) | Se sobrescribe en cada generación; contiene los campos y las consultas básicas. |
| Clase de negocio (`.Biz.cs`) | No se sobrescribe tras la primera generación; coloca aquí la lógica personalizada. |
| Métodos autogenerados | Los índices y las columnas producen automáticamente métodos auxiliares `FindByXxx` / `Search` y plantillas. |

</details>

<details>
<summary><b>Motor de entidades y DAL</b> (7)</summary>

| Componente | Responsabilidad |
| --- | --- |
| `IEntity` / `Entity` | Modelo de dominio enriquecido que encapsula campos, validación y ciclo de vida. |
| `IEntityOperate` | Punto de entrada estático para operaciones (Find / FindAll / Query, etc.). |
| `EntityFactory` | Fábrica de caché de metadatos de entidades. |
| `EntitySession` (`Meta.Session`) | Contexto de una entidad en una conexión/tabla concreta, con caché y estadísticas a nivel de sesión. |
| `Meta` | Contexto estático de la entidad (esquema de tabla, campos, cachés, política de sharding, etc.). |
| `DAL` / `IDatabase` / `IDbSession` | Acceso unificado a la base de datos y operaciones de metadatos. |
| `CheckTables()` | Comprueba y sincroniza el esquema al iniciar/primer acceso, de forma asíncrona o síncrona según sea necesario. |

</details>

<details>
<summary><b>DSL de consultas</b> (8)</summary>

| Capacidad | Características clave |
| --- | --- |
| LINQ con `IQueryable<T>` (v12.0) | `Entity<T>.Query` devuelve `IQueryable<T>`, con soporte para operaciones LINQ estándar como `Where/OrderBy/Skip/Take/Count/First/ToList`. |
| Consultas fluidas `DAL.Select<T>()` (v12.1) | `DAL.Select<T>()` devuelve `IQueryable<T>` vinculado a la conexión actual, con llamadas fluidas totalmente encadenadas `WhereIf/Where/OrderBy/Skip/Take/Page/Count/FirstOrDefault/ToList`. |
| Propiedades de navegación (v12.1) | La Fluent API `HasOne`/`HasMany` configura relaciones de navegación; `Include` construye rutas y carga por lotes; `SelectBuilder` admite JOIN entre relaciones de navegación. |
| Condiciones en línea `WhereIf` | `Query.WhereIf(condition, predicate)` y `FindAllWhereIf` permiten el filtrado dinámico. |
| `WhereExpression` | Composición encadenable de expresiones `&` / `\|` / `Contains` / `Between` / `Like`. |
| `Find` / `FindAll` / `Query` / `PageFindAll` | Puntos de entrada de consulta, con paginación integrada en `PageFindAll`. |
| `Pager` / `PageParameter` | Transporta paginación, ordenación, estadísticas de `TotalCount` y extensiones de alcance de datos. |
| Funciones de ventana (v12.0) | `SelectBuilder.Window` + `WindowFunction` admiten ROW_NUMBER/RANK/DENSE_RANK y funciones de ventana de agregación. |

</details>

<details>
<summary><b>Sistema de caché multinivel</b> (6)</summary>

Diseñado para cargas con muchas lecturas y pocas escrituras:

| Caché | Características clave |
| --- | --- |
| `Meta.Count` | Caché rápido del número de filas (usa índices del sistema cuando >1000; SQLite usa Max(identity) más una compensación exacta asíncrona). |
| `EntityCache` (`Meta.Cache.Entities`) | Caché de tabla completa (recomendado para tablas pequeñas de <1000 filas; prohibido por encima de 10000 filas). |
| `SingleCache` (`Meta.SingleCache[key]`) | Caché de objeto único a nivel de diccionario por clave primaria/alterna; adecuado para búsquedas puntuales frecuentes como usuarios. |
| Política de expiración | 60 s por defecto. Las lecturas expiradas devuelven datos obsoletos de inmediato y se refrescan de forma asíncrona. Las escrituras invalidan/actualizan las cachés de inmediato. |
| Integración con transacciones | Dentro de una transacción, las modificaciones se acumulan; las cachés se limpian o revierten de forma uniforme tras el commit/rollback. |
| Caché de segundo nivel (v12.0) | `CacheInvalidator.Provider` inyecta Redis y coordina la invalidación de la caché de entidades entre procesos mediante un mecanismo de versión. |

</details>

<details>
<summary><b>Ingeniería inversa y migración</b> (3)</summary>

| Capacidad | Características clave |
| --- | --- |
| Ingeniería inversa del esquema | Crea/modifica esquemas de tabla e índices a partir de las entidades en sentido inverso, manteniéndolos consistentes automáticamente. |
| Política de migración por entidad | La configuración `Migration` a nivel de tabla (atributo `Table` del modelo / `[BindTable]` / sobrescritura de `TableItem` al iniciar) controla la estrategia de ingeniería inversa de cada entidad (Off / ReadOnly / On / Full). |
| Extracción de modelos y migración entre bases de datos | Admite la extracción de modelos desde cualquier base de datos + migración de datos entre bases de datos (con la herramienta complementaria DataMigration). |

</details>

<details>
<summary><b>Sharding (ShardPolicy)</b> (3)</summary>

| Capacidad | Características clave |
| --- | --- |
| Abstracción `ShardPolicy` | Ejemplo `TimeShardPolicy`: nombre de tabla Table_yyyyMMdd por fecha. |
| Enrutamiento transparente | El código de negocio sigue llamando a Find / Insert / Update y no le importa el nombre real de la tabla. |
| Estrategias personalizadas | Estrategias personalizables de hash de clave primaria / tiempo / rango. |

</details>

<details>
<summary><b>Transacciones y consistencia de caché</b> (3)</summary>

| Escenario | Características clave |
| --- | --- |
| Escrituras no transaccionales | Las escrituras invalidan de inmediato las cachés relacionadas y las reconstruyen de forma asíncrona, a la vez que intentan actualizar directamente los objetos en memoria para mantener la consistencia lectura/escritura. |
| Commit | Dentro de una transacción, no se limpian las cachés de tabla completa; solo se modifican localmente los objetos afectados; tras el commit se limpian o refrescan de forma uniforme. |
| Rollback | Al revertir, se deshacen los cambios realizados dentro de la transacción. |

</details>

<details>
<summary><b>Optimizaciones de rendimiento</b> (6)</summary>

| Técnica | Características clave |
| --- | --- |
| Caché multinivel | Mantiene las consultas frecuentes fuera de la base de datos. |
| Escrituras por lotes | Fusiona operaciones de varios campos y varios registros (optimización interna). |
| Algoritmos de paginación | Elige la estrategia óptima según la base de datos/versión. |
| Perezoso/asíncrono | El recuento de filas, la comprobación del modelo y el refresco de la caché pueden ejecutarse de forma asíncrona. |
| Caché de reflexión / aceleración por delegados | Precalienta los accesores de propiedades de las entidades. |
| Asociación en memoria | Cachea primero la tabla principal y luego realiza «pseudo joins» en memoria. |

</details>

<details>
<summary><b>Alcance de datos y multiinquilino</b> (5)</summary>

| Capacidad | Características clave |
| --- | --- |
| Alcance de datos | Control de acceso a datos a nivel de fila con cinco alcances: «Todos / Departamento y subdepartamentos / Departamento / Solo propio / Personalizado». |
| Configuración del alcance | `DataScopeContext` + `DataScopeInterceptor` aplican automáticamente filtros de consulta, la propiedad predeterminada al insertar y la validación al actualizar/eliminar. |
| Adopción por entidad | Las entidades implementan `IUserScope` / `IDepartmentScope` / `IDataScope`; los responsables de departamento se incluyen automáticamente en el alcance visible y los datos propios siempre son accesibles. |
| Multiinquilino | Tras implementar `ITenantScope`, `TenantInterceptor` añade automáticamente filtros de inquilino en las consultas y valida/rellena el identificador de inquilino en las escrituras; EntityBuilder implementa automáticamente `ITenantScope` para las clases de negocio que contienen una columna `TenantId`. |
| Detalles | Consulta `Doc/数据权限.md` y `Doc/权限管理.md` para más detalles. |

</details>

<details>
<summary><b>Configuración multibase de datos</b> (4)</summary>

| Capacidad | Características clave |
| --- | --- |
| SQLite por defecto | SQLite es el valor predeterminado para desarrollo (configuración cero). |
| Ajustes de conexión | En el despliegue, configura `XCode_{ConnName}` en el archivo de configuración o en las variables de entorno. |
| Identificador de proveedor | La cadena de conexión debe incluir el identificador del proveedor (por ejemplo: `provider=MySql;server=...;user=...;******;database=...;`). |
| Múltiples conexiones | Se pueden usar varios nombres de conexión (ConnName) de base de datos juntos en el mismo proceso. |

</details>

### Ejemplos de uso

#### Condiciones dinámicas y paginación

```csharp
public static IList<UserInfo> Search(String name, Int32 age, Pager p)
{
    var exp = new WhereExpression();
    if (!name.IsNullOrEmpty()) exp &= UserInfo._.UserName == name;
    if (age > 0) exp &= UserInfo._.Age == age;
    return UserInfo.FindAll(exp, p);
}
```

#### Actualización de campos específicos (omite la caché; no recomendado para uso habitual)

```csharp
UserInfo.Update(UserInfo._.UserName == "Li Si" & UserInfo._.Age == 18, UserInfo._.Id == 1);
```

#### Ordenación personalizada y proyección de columnas

```csharp
var list = UserInfo.FindAll(UserInfo._.Age > 10, " Age DESC,Id ASC",
    string.Join(",", UserInfo._.UserName, UserInfo._.Age), 0, 20);
```

#### Índices y métodos generados

Añade índices al modelo:

```xml
<Indexes>
  <Index Columns="UserName" Unique="True" />
  <Index Columns="UserName,Age" />
</Indexes>
```

Tras la regeneración, aparecen automáticamente `FindByUserName` y métodos auxiliares de condiciones combinadas / plantillas Search.

Estrategia de caché de entidades: cuando `Meta.Session.Count < 1000`, usa búsquedas en la colección en memoria; en caso contrario, acude a la base de datos o a la caché de objeto único.

```csharp
public static UserInfo FindByUserName(String userName)
{
    if (userName.IsNullOrEmpty()) return null;
    if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.UserName.EqualIgnoreCase(userName));
    return Find(_.UserName == userName);
}
```

#### Ejemplo de sharding

```csharp
Meta.ShardPolicy = new TimeShardPolicy(nameof(UserInfo.Id), Meta.Factory)
{
    ConnPolicy = "{0}",           // política personalizable del nombre de base de datos
    TablePolicy = "{0}_{1:yyyyMMdd}", // plantilla del nombre de tabla
    Step = TimeSpan.FromDays(1)
};
```

Todo el resto del código, como Insert / Query, permanece sin cambios y se enruta automáticamente a la tabla de la fecha correspondiente.

#### Ejemplos originales (conservados)

<details>
<summary><b>Ejemplos originales de CRUD y consultas complejas</b></summary>

##### Crear, leer, actualizar, eliminar

```csharp
var user = new UserInfo();
user.UserName = "Zhang San";
user.Password = "123456";
user.Age = 18;
user.Insert();
Console.WriteLine("Se insertó un nuevo registro, id de usuario: " + user.Id);

var u = UserInfo.FindById(1);
u.UserName = "Zhang San";
u.Password = "123456";
u.Age = 19;
u.Update();

u = UserInfo.FindById(1);
Console.WriteLine($"ID de usuario={u.Id}, edad actualizada a: {u.Age}");
```

##### Consulta compleja

```csharp
var list = UserInfo.FindAll(UserInfo._.UserName == "Zhang San" & UserInfo._.Age == 19, UserInfo._.UserName.Desc(), string.Join(",", UserInfo._.UserName, UserInfo._.Age), 0, 0);
```

</details>

### Benchmarks

| Capacidad | Métrica (entorno típico) | Notas |
| ---- | ---------------- | ---- |
| Inserción en una sola máquina | hasta ~890k TPS | pruebas combinadas de lote / caché + SQLite / disco RAM |
| Consulta en una sola máquina | hasta ~1,8 mil millones de QPS | escenarios de tablas pequeñas + caché de entidades / caché de objeto único |
| Volumen de datos por tabla | decenas de miles de millones en producción | estrategias de paginación + sharding |

> El rendimiento real depende del tipo de base de datos / hardware / estrategia de sharding / tasa de aciertos de caché.

### Preguntas frecuentes (FAQ)

- ¿Por qué el primer Count es rápido? `Meta.Count` usa índices del sistema; SQLite puede tomar primero el máximo de identity y luego refinar de forma asíncrona.
- ¿Cuánto tarda la caché en ser consistente tras una actualización? Las escrituras del proceso actual se reflejan de inmediato; entre procesos, espera a que expire la caché o usa una estrategia de invalidación activa.
- ¿Se puede desactivar la migración automática? Establece `Migration=Off` (global o por tabla), o usa ReadOnly/On/Full según sea necesario.
- ¿Consultas complejas de varias tablas? Se recomiendan varias consultas de una sola tabla + asociación en memoria (más rápido con aciertos de caché y con una estructura más clara).

## Mapa del ecosistema

NewLife es un ecosistema por capas de proyectos de código abierto con versionado independiente. El mapa siguiente se comparte literalmente en todos los repositorios de NewLife; cada proyecto enlaza a su repositorio y a su paquete principal de NuGet.

| Proyecto | Qué hace | Descargas |
| --- | --- | --- |
| **Fundamentos** | | |
| [NewLife.Core](https://github.com/NewLifeX/X) | Registro de logs, configuración, caché, red, serialización, programación de tareas y seguimiento APM. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Core?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Core) |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | Middleware de datos: tablas individuales con decenas de miles de millones de filas, particionado automático, separación lectura/escritura y más de 10 bases de datos. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCode?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCode) |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | Biblioteca de red de alto rendimiento: 1,77 M pps / 14,5 Gbps en una sola máquina (TCP 1KB, 2026-10), 4 M conexiones TCP persistentes. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Net?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Net) |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | Framework de comunicación por protocolo y RPC para aplicaciones cliente/servidor y dispositivos IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Remoting?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Remoting) |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | Instala aplicaciones .NET como demonios del sistema operativo: servicios de Windows y systemd de Linux. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Agent?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Agent) |
| [NewLife.Templates](https://github.com/NewLifeX/NewLife.Zero) | Plantillas de proyecto `dotnet new` para consola, web, API, servicio, datos e IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Templates?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Templates) |
| **Datos y mensajería** | | |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | Cliente Redis con latencia de microsegundos, millones de operaciones por segundo y colas de mensajes integradas. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Redis?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Redis) |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | Cliente RocketMQ totalmente administrado para Apache RocketMQ y Alibaba Cloud Message Queue. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.RocketMQ?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.RocketMQ) |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | Cliente y servidor MQTT, incluida la conectividad con Alibaba Cloud IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MQTT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MQTT) |
| [NewLife.NovaDb](https://github.com/NewLifeX/NewLife.NovaDb) | Base de datos híbrida en C#: embebida o en red, relacional, series temporales, cola de mensajes y KV en una sola. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.NovaDb?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.NovaDb) |
| [NewLife.MySql](https://github.com/NewLifeX/NewLife.MySql) | Controlador y capa de ayuda de MySql construidos sobre la pila de datos de NewLife. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MySql?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MySql) |
| **Web, IA y distribuido** | | |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | Framework rápido para plataformas de administración: permisos, SSO, servidor OAuth y andamiaje CRUD. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Cube?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Cube) |
| [Stardust](https://github.com/NewLifeX/Stardust) | Plataforma distribuida de servicios: gestión de nodos, APM, centro de configuración, registro y centro de despliegues. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Stardust?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Stardust) |
| [AntJob](https://github.com/NewLifeX/AntJob) | Plataforma de tareas distribuidas y programación de datos en tiempo real con particionado, reintentos y relleno histórico. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AntJob?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AntJob) |
| [NewLife.AI](https://github.com/NewLifeX/NewLife.AI) | Cliente unificado para 46 proveedores de LLM más ChatAI, una aplicación web de chat lista para usar. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AI?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AI) |
| **Industrial e IoT** | | |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | Biblioteca estándar de IoT que define contratos de protocolo y abstracciones de dispositivos. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IoT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IoT) |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | Implementación de Modbus TCP/RTU/ASCII sobre NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Modbus?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Modbus) |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | Protocolo para PLC Siemens sobre NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Siemens?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Siemens) |
| [NewLife.Omron](https://github.com/NewLifeX/NewLife.Omron) | Protocolo para PLC Omron sobre NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Omron?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Omron) |
| [NewLife.Melsec](https://github.com/NewLifeX/NewLife.Melsec) | Protocolo para PLC Mitsubishi Melsec sobre NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Melsec?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Melsec) |
| [NewLife.OPC](https://github.com/NewLifeX/NewLife.OPC) | Cliente OPC para el acceso a datos industriales. | — |
| [NewLife.BACnet](https://github.com/NewLifeX/NewLife.BACnet) | Pila de protocolo BACnet para la automatización de edificios. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.BACnet?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.BACnet) |
| [ZeroIoT](https://github.com/NewLifeX/ZeroIoT) | Plataforma IoT de código abierto y ligera con gestión de pasarelas y dispositivos. | — |
| **Telemática vehicular** | | |
| [NewLife.JT808](https://github.com/NewLifeX/NewLife.JT808) | Protocolo de posicionamiento vehicular JT/T 808: más de 70 cuerpos de mensaje, 2011/2013/2019. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT808?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT808) |
| [NewLife.JT809](https://github.com/NewLifeX/NewLife.JT809) | Protocolo plataforma a plataforma JT/T 809 para autoridades de transporte. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT809?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT809) |
| [NewLife.JT1078](https://github.com/NewLifeX/NewLife.JT1078) | Vídeo a bordo JT/T 1078: análisis RTP, remultiplexado FLV/fMP4 y transmisión en directo. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT1078?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT1078) |
| **Componentes de dominio** | | |
| [NewLife.Office](https://github.com/NewLifeX/NewLife.Office) | Un solo paquete para Excel, Word, PPT, PDF y más de 15 formatos de documento. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Office?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Office) |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | Audio multiplataforma: códecs, E/S de dispositivos, DSP, contenedores y streaming. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Audio?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Audio) |
| [NewLife.Video](https://github.com/NewLifeX/NewLife.Video) | Códecs de vídeo, formatos de contenedor, procesamiento de fotogramas y streaming. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Video?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Video) |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | Interfaz unificada sobre Baidu, AMap, Tencent y Tianditu con geocodificación y conversión de coordenadas. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Map?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Map) |
| [NewLife.IP](https://github.com/NewLifeX/NewLife.IP) | Geolocalización IPv4 con una base de datos offline mapeada en memoria y una API en línea de respaldo. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IP?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IP) |
| **Herramientas y productos** | | |
| [CrazyCoder](https://github.com/NewLifeX/XCoder) | Caja de herramientas del desarrollador: red, puerto serie, criptografía, expresiones regulares, Modbus y MQTT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCoder?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCoder) |
| [GitCandy](https://github.com/NewLifeX/GitCandy) | Sistema de gestión de código fuente Git autoalojado. | — |
| [LuckyClover](https://github.com/NewLifeX/LuckyClover) | Instala automáticamente el runtime de .NET correspondiente cuando se ejecuta desde un host más antiguo. | — |

> Las insignias muestran el paquete principal de cada proyecto; los paquetes de temas, extensiones y satélites figuran en sus páginas de NuGet. Los más de 30 paquetes no listados aquí (FTP, LoRa, extensiones de Office, controladores de protocolo, etc.) están disponibles en la [organización NewLifeX](https://github.com/NewLifeX) y en [newlifex.com](https://newlifex.com). Esta tabla se mantiene en el README de NewLife.Core.

## Documentación

- [Navegación de la documentación de XCode](Doc/XCode文档导航.md): índice completo de documentos agrupado por módulo
- [Manual de usuario de XCode](Doc/XCode使用手册.md): recorrido de uso de extremo a extremo
- [Diseño de arquitectura](Doc/架构设计.md): capas, componentes y decisiones de diseño clave
- [Localización multilingüe](Doc/多语言本地化.md): modelo de mensajes, tablas de idioma y sobrescrituras en tiempo de ejecución
- Documentación en línea: https://newlifex.com/xcode
- [ChangeLog.md](ChangeLog.md): historial de versiones

## Contribuir

### Compilar y probar

```
dotnet restore
dotnet build -c Release
dotnet test XUnitTest.XCode -c Release
```

- Añade casos en `XUnitTest.XCode`; las sugerencias relacionadas con el rendimiento deben incluir benchmarks o explicaciones.
- Sigue las convenciones del repositorio (strong naming, multi-TFM, comentarios XML, compilación condicional).
- Los cambios de API pública requieren una estrategia de migración (Obsolete → eliminación).

### Publicación

Envía una etiqueta con prefijo `v` (por ejemplo `v2026.1018`) para activar `.github/workflows/publish.yml`. El flujo de trabajo empaqueta XCode, XCodeTool y los paquetes de proveedores (`XCode.MySql`, `XCode.SQLite`, `XCode.SqlServer`, `XCode.Oracle`, `XCode.PostgreSQL`, `XCode.DaMeng`, `XCode.DB2`, `XCode.KingBase`, `XCode.HighGo`) y los publica en NuGet.org y GitHub Packages.

### Comentarios y sugerencias

- Se agradecen Issues / PRs: optimización de rendimiento / nuevos proveedores de base de datos / mejoras de documentación.
- Antes de enviar, asegúrate de que `dotnet build` y las pruebas unitarias relevantes pasan; sigue las convenciones del repositorio para el estilo de código y los mensajes de commit.
- Informa de errores y sugerencias mediante [Issue](https://github.com/NewLifeX/NewLife.XCode/issues); las Stars y los PRs son bienvenidos.

## Equipo

<img src="https://newlifex.com/logo.png" width="48" align="right" alt="NewLife" />

Fundado en 2002, el equipo de NewLife lleva más de dos décadas dedicado al ecosistema .NET e IoT, mantiene cerca de 100 proyectos de código abierto (34 de ellos principales en este mapa) y ofrece soluciones integradas de software y hardware para los sectores de energía, logística, salud, control industrial e Internet. Nos guía el lema «en el aprendizaje no hay antigüedad: quien domina es maestro».

Los componentes de código abierto acumulan <!--nuget-total:start-->6.48M<!--nuget-total:end--> descargas en NuGet (todo el ecosistema, a fecha de 2026-10).

Sitio web: https://newlifex.com · Organización de código abierto: https://github.com/NewLifeX

<details>
<summary>Cuenta oficial de WeChat: 智能大石头 (escanea para seguir)</summary>

![智能大石头](https://newlifex.com/stone.jpg)

</details>

## Licencia

MIT, totalmente abierto. Se permite el uso comercial / modificación / redistribución (puedes reemplazar «NewLife» por la marca de tu empresa para despliegues privados). El certificado de nombre seguro `newlife.snk` está integrado.

> Si este proyecto te resulta útil, dale una Star, compártelo con tu equipo o envía sugerencias de mejora mediante Issue/PR.
