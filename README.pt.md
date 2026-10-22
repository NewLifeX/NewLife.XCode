# NewLife.XCode - Middleware de dados / Super ORM

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
🌐 [English](README.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Русский](README.ru.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

> ⚠️ Esta tradução pode ficar desatualizada, inclusive em sua estrutura; a versão autorizada é o [README em inglês](README.md).

**NewLife.XCode** é um middleware de dados .NET de alto desempenho, focado em "CRUD + desempenho extremo + dados massivos". Inclui cache em múltiplos níveis, modelagem/migração automática, sharding, consultas fortemente tipadas e migração entre bancos de dados. Suporta MySQL / SQLite / SqlServer / Oracle / PostgreSQL / TDengine / InfluxDB / DaMeng / KingBase / HighGo / DB2 / ClickHouse / DuckDB / MongoDB / Hana (SAP HANA) / IRIS (InterSystems) / VastBase / banco de dados virtual Network / NovaDb e muito mais. Implantações em produção alcançam dezenas de bilhões de linhas por tabela, e a taxa de transferência de consultas pode chegar a bilhões de QPS (com estratégias de cache).

Documentação: https://newlifex.com/xcode   Grupos QQ da comunidade: 1600800 / 1600838

**Conteúdo**: [Início rápido](#início-rápido) · [Por que XCode](#por-que-xcode) · [Principais recursos](#principais-recursos) · [Mapa do ecossistema](#mapa-do-ecossistema) · [Documentação](#documentação) · [Como contribuir](#como-contribuir) · [Equipe](#equipe) · [Licença](#licença)

## Início rápido

### Instalação

Referência NuGet (qualquer projeto):

```
dotnet add package NewLife.XCode
```

Ou use o modelo de scaffolding:

```
dotnet new install NewLife.Templates
dotnet new xcode --name Demo.Data
cd Demo.Data
dotnet build
```

> O certificado de nome forte é incorporado (newlife.snk); você pode recompilar para um framework corporativo privado.

### Definir o modelo (db.xml)

```xml
<?xml version="1.0" encoding="utf-8"?>
<Tables Version="1.0" Output="MyEntity" NameSpace="Model" BaseClass="Entity" ConnName="DB">
  <Table Name="UserInfo" Description="Informações do usuário">
    <Columns>
      <Column Name="Id" DataType="Int32" Identity="True" PrimaryKey="True" Description="Id do membro" />
      <Column Name="UserName" DataType="String" Nullable="False" Description="Nome do membro" />
      <Column Name="Password" DataType="String" Nullable="False" Description="Senha do membro" />
      <Column Name="Age" DataType="Int32" Description="Idade do membro" />
    </Columns>
  </Table>
</Tables>
```

### Gerar as classes de entidade

- Baixe o xcodetool.exe no diretório e clique duas vezes nele, ou execute: `dotnet tool install xcodetool -g --prerelease` → `xcode db.xml`.
- Gera: 用户信息.cs (classe de dados, sobrescrita) + 用户信息.Biz.cs (classe de negócios, não sobrescrita).

### CRUD

```csharp
using XCode;

var user = new UserInfo { UserName = "Zhang San", Password = "123456", Age = 18 };
user.Insert();
Console.WriteLine("Inserted a new record, user id: " + user.Id);

var u2 = UserInfo.FindById(user.Id);
u2.Age = 19;
u2.Update();

var list = UserInfo.FindAll(UserInfo._.Age > 10 & UserInfo._.UserName == "Zhang San");
UserInfo.FindById(user.Id)?.Delete();
```

> Ao usar caches: menos de 1000 entidades → cache de tabela inteira; caso contrário, use consultas normais / cache de objeto único.

## Por que XCode

- **Original e próprio**: totalmente desenvolvido na China, sem componentes de terceiros, com controle próprio e respaldo de manutenção de longo prazo.
- **Simplicidade**: 90% dos cenários de negócio precisam apenas de operações de entidade de tabela única, sem SQL escrito à mão.
- **Compatível com Dapper**: executa consultas SQL arbitrárias e as mapeia para qualquer modelo de entidade; totalmente compatível com o uso de Dapper, acrescentando cache, separação leitura/escrita e rastreamento.
- **Desempenho extremo**: cache em múltiplos níveis no caminho crítico + otimização em lote + junções em memória; a velocidade de consulta pode ser muito superior ao SQL bruto.
- **Dados massivos**: estratégias nativas de paginação e sharding; projetos em produção alcançam dezenas de bilhões de linhas por tabela; algoritmos de paginação adaptados a vários bancos de dados.
- **Estável e maduro**: mais de 20 anos de evolução e ampla validação em produção; mantém compatibilidade com versões antigas do .NET Framework (a partir de net45).
- **Migração automática**: modelo/entidade é o esquema; novos campos são sincronizados automaticamente na implantação, sem scripts SQL extras.
- **Evolução distribuída**: estratégias nativas de sharding por tabela / tempo / chave primária para divisão e expansão suaves.
- **Plugável**: abstração DAL + provedores separados; adaptadores de banco de dados personalizados são suportados.

## Principais recursos

<details>
<summary><b>Modelagem e geração de código</b> (5)</summary>

| Recurso | Características principais |
| --- | --- |
| Modelo XML (Tables/Columns/Indexes) | O modelo é a única fonte de verdade; os atributos cobrem índices, restrições exclusivas, valores padrão, descrições e propriedades estendidas. |
| Gerador de código | `xcodetool` (ferramenta global / exe) ou o template T4 do VS transforma o modelo XML em classes de entidade. |
| Classe de dados (`.cs`) | Sobrescrita a cada geração; contém os campos e as consultas básicas. |
| Classe de negócios (`.Biz.cs`) | Não é sobrescrita após a primeira geração; coloque a lógica personalizada aqui. |
| Métodos gerados automaticamente | Índices e colunas geram automaticamente métodos auxiliares `FindByXxx` / `Search` e templates. |

</details>

<details>
<summary><b>Runtime de entidades e DAL</b> (7)</summary>

| Componente | Responsabilidade |
| --- | --- |
| `IEntity` / `Entity` | Modelo de domínio rico que encapsula campos, validação e ciclo de vida. |
| `IEntityOperate` | Ponto de entrada estático de operações (Find / FindAll / Query etc.). |
| `EntityFactory` | Fábrica de cache de metadados de entidades. |
| `EntitySession` (`Meta.Session`) | Contexto de uma entidade em uma conexão/tabela específica, com cache/estatísticas no nível da sessão. |
| `Meta` | Contexto estático da entidade (esquema da tabela, campos, caches, política de sharding etc.). |
| `DAL` / `IDatabase` / `IDbSession` | Acesso unificado ao banco de dados e operações de metadados. |
| `CheckTables()` | Verifica e sincroniza o esquema na inicialização/primeiro acesso, de forma assíncrona ou síncrona conforme necessário. |

</details>

<details>
<summary><b>DSL de consulta</b> (8)</summary>

| Capacidade | Características principais |
| --- | --- |
| LINQ `IQueryable<T>` (v12.0) | `Entity<T>.Query` retorna `IQueryable<T>`, com suporte a operações LINQ padrão como `Where/OrderBy/Skip/Take/Count/First/ToList`. |
| Consultas fluentes `DAL.Select<T>()` (v12.1) | `DAL.Select<T>()` retorna `IQueryable<T>` vinculado à conexão atual, com suporte a chamadas fluentes totalmente encadeadas `WhereIf/Where/OrderBy/Skip/Take/Page/Count/FirstOrDefault/ToList`. |
| Propriedades de navegação (v12.1) | A Fluent API `HasOne`/`HasMany` configura relacionamentos de navegação; `Include` constrói caminhos e carrega em lote; `SelectBuilder` suporta JOINs entre relacionamentos de navegação. |
| Condições inline `WhereIf` | `Query.WhereIf(condition, predicate)` e `FindAllWhereIf` permitem filtragem dinâmica. |
| `WhereExpression` | Composição encadeável de expressões com `&` / `\|` / `Contains` / `Between` / `Like`. |
| `Find` / `FindAll` / `Query` / `PageFindAll` | Pontos de entrada de consulta, com paginação integrada em `PageFindAll`. |
| `Pager` / `PageParameter` | Transporta paginação, ordenação, estatísticas de `TotalCount` e extensões de escopo de dados. |
| Funções de janela (v12.0) | `SelectBuilder.Window` + `WindowFunction` com suporte a ROW_NUMBER/RANK/DENSE_RANK e funções de janela agregadas. |

</details>

<details>
<summary><b>Sistema de cache em múltiplos níveis</b> (6)</summary>

Projetado para cargas com muitas leituras e poucas gravações:

| Cache | Características principais |
| --- | --- |
| `Meta.Count` | Cache rápido de contagem de linhas (usa índices do sistema quando >1000; SQLite usa Max(identity) mais compensação assíncrona exata). |
| `EntityCache` (`Meta.Cache.Entities`) | Cache de tabela inteira (recomendado para tabelas pequenas com <1000 linhas; proibido acima de 10000 linhas). |
| `SingleCache` (`Meta.SingleCache[key]`) | Cache de objeto único em nível de dicionário por chave primária/alternativa; adequado para consultas pontuais frequentes, como usuários. |
| Política de expiração | 60s por padrão. Leituras expiradas retornam dados obsoletos imediatamente + atualização assíncrona. Gravações invalidam/atualizam os caches imediatamente. |
| Consciência transacional | Dentro de uma transação, as modificações se acumulam; os caches são limpos ou revertidos uniformemente após commit/rollback. |
| Cache de segundo nível (v12.0) | `CacheInvalidator.Provider` injeta Redis e coordena a invalidação de cache de entidades entre processos por meio de um mecanismo de versão. |

</details>

<details>
<summary><b>Engenharia reversa e migração</b> (3)</summary>

| Capacidade | Características principais |
| --- | --- |
| Engenharia reversa de esquema | Cria/modifica esquemas de tabelas e índices a partir das entidades, em sentido inverso, mantendo-os consistentes automaticamente. |
| Política de migração por entidade | Configuração `Migration` no nível da tabela (atributo `Table` do modelo / `[BindTable]` / substituição na inicialização por `TableItem`) controla a estratégia de engenharia reversa por entidade (Off / ReadOnly / On / Full). |
| Extração de modelo e migração entre bancos | Suporta extração de modelo de qualquer banco de dados + migração de dados entre bancos (com a ferramenta complementar DataMigration). |

</details>

<details>
<summary><b>Sharding (ShardPolicy)</b> (3)</summary>

| Capacidade | Características principais |
| --- | --- |
| Abstração `ShardPolicy` | Exemplo `TimeShardPolicy`: nome de tabela Table_yyyyMMdd por data. |
| Roteamento transparente | O código de negócio continua chamando Find / Insert / Update e não se preocupa com o nome real da tabela. |
| Estratégias personalizadas | Estratégias personalizáveis de hash de chave primária / tempo / intervalo. |

</details>

<details>
<summary><b>Transações e consistência de cache</b> (3)</summary>

| Cenário | Características principais |
| --- | --- |
| Gravações fora de transação | As gravações expiram imediatamente os caches relacionados e os reconstroem de forma assíncrona, enquanto tentam atualizar diretamente os objetos em memória para consistência de leitura/gravação. |
| Commit | Dentro de uma transação, os caches de tabela inteira não são limpos; apenas os objetos atingidos são alterados localmente; após o commit eles são limpos ou atualizados uniformemente. |
| Rollback | No rollback, as alterações feitas dentro da transação são revertidas. |

</details>

<details>
<summary><b>Otimizações de desempenho</b> (6)</summary>

| Técnica | Características principais |
| --- | --- |
| Cache em múltiplos níveis | Mantém consultas quentes fora do banco de dados. |
| Gravações em lote | Mescla operações de vários campos e vários registros (otimização interna). |
| Algoritmos de paginação | Escolhe a estratégia ideal por banco de dados/versão. |
| Preguiçoso/assíncrono | Contagens de linhas, verificações de modelo e atualizações de cache podem rodar de forma assíncrona. |
| Cache de reflexão / aceleração por delegate | Pré-aquece os acessadores de propriedades das entidades. |
| Associação em memória | Armazena primeiro a tabela principal e depois faz "pseudo joins" em memória. |

</details>

<details>
<summary><b>Escopo de dados e multi-inquilino</b> (5)</summary>

| Capacidade | Características principais |
| --- | --- |
| Escopo de dados | Controle de acesso a dados em nível de linha com suporte a cinco escopos: "Todos / Departamento e subdepartamentos / Departamento / Apenas o próprio / Personalizado". |
| Integração do escopo | `DataScopeContext` + `DataScopeInterceptor` aplicam automaticamente filtros de consulta, propriedade padrão na inserção e validação na atualização/exclusão. |
| Adesão por entidade | As entidades implementam `IUserScope` / `IDepartmentScope` / `IDataScope`; gerentes de departamento são incluídos automaticamente no escopo visível, e os próprios dados são sempre acessíveis. |
| Multi-inquilino | Depois que uma entidade implementa `ITenantScope`, o `TenantInterceptor` adiciona automaticamente filtros de inquilino nas consultas e valida/preenche o identificador de inquilino nas gravações; o EntityBuilder implementa automaticamente `ITenantScope` para classes de negócio que contenham uma coluna `TenantId`. |
| Detalhes | Consulte `Doc/数据权限.md` e `Doc/权限管理.md` para detalhes. |

</details>

<details>
<summary><b>Configuração multi-banco de dados</b> (4)</summary>

| Capacidade | Características principais |
| --- | --- |
| SQLite por padrão | SQLite é o padrão para desenvolvimento (configuração zero). |
| Configurações de conexão | Na implantação, configure `XCode_{ConnName}` no arquivo de configuração ou em variáveis de ambiente. |
| Identificador do provedor | A cadeia de conexão deve incluir o identificador do provedor (por exemplo: `provider=MySql;server=...;user=...;******;database=...;`). |
| Múltiplas conexões | Vários nomes de conexão de banco (ConnName) podem ser usados juntos no mesmo processo. |

</details>

### Exemplos de uso

#### Condições dinâmicas e paginação

```csharp
public static IList<UserInfo> Search(String name, Int32 age, Pager p)
{
    var exp = new WhereExpression();
    if (!name.IsNullOrEmpty()) exp &= UserInfo._.UserName == name;
    if (age > 0) exp &= UserInfo._.Age == age;
    return UserInfo.FindAll(exp, p);
}
```

#### Atualização de campos específicos (ignora o cache; não recomendado para uso rotineiro)

```csharp
UserInfo.Update(UserInfo._.UserName == "Li Si" & UserInfo._.Age == 18, UserInfo._.Id == 1);
```

#### Ordenação personalizada e projeção de colunas

```csharp
var list = UserInfo.FindAll(UserInfo._.Age > 10, " Age DESC,Id ASC",
    string.Join(",", UserInfo._.UserName, UserInfo._.Age), 0, 20);
```

#### Índices e métodos gerados

Adicione índices ao modelo:

```xml
<Indexes>
  <Index Columns="UserName" Unique="True" />
  <Index Columns="UserName,Age" />
</Indexes>
```

Após a regeneração, `FindByUserName`, métodos auxiliares de condição combinada / templates de Search aparecem automaticamente.

Estratégia de cache de entidade: quando `Meta.Session.Count < 1000`, use consultas na coleção em memória; caso contrário, vá ao banco de dados ou ao cache de objeto único.

```csharp
public static UserInfo FindByUserName(String userName)
{
    if (userName.IsNullOrEmpty()) return null;
    if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.UserName.EqualIgnoreCase(userName));
    return Find(_.UserName == userName);
}
```

#### Exemplo de sharding

```csharp
Meta.ShardPolicy = new TimeShardPolicy(nameof(UserInfo.Id), Meta.Factory)
{
    ConnPolicy = "{0}",           // política de nome de banco personalizável
    TablePolicy = "{0}_{1:yyyyMMdd}", // template de nome de tabela
    Step = TimeSpan.FromDays(1)
};
```

Todo o restante do código, como Insert / Query, permanece inalterado e é roteado automaticamente para a tabela da data correspondente.

#### Exemplos originais (mantidos)

<details>
<summary><b>Exemplos originais de CRUD e consultas complexas</b></summary>

##### Criar, ler, atualizar e excluir

```csharp
var user = new UserInfo();
user.UserName = "Zhang San";
user.Password = "123456";
user.Age = 18;
user.Insert();
Console.WriteLine("Inserted a new record, user id: " + user.Id);

var u = UserInfo.FindById(1);
u.UserName = "Zhang San";
u.Password = "123456";
u.Age = 19;
u.Update();

u = UserInfo.FindById(1);
Console.WriteLine($"User ID={u.Id} age updated to: {u.Age}");
```

##### Consulta complexa

```csharp
var list = UserInfo.FindAll(UserInfo._.UserName == "Zhang San" & UserInfo._.Age == 19, UserInfo._.UserName.Desc(), string.Join(",", UserInfo._.UserName, UserInfo._.Age), 0, 0);
```

</details>

### Benchmarks

| Capacidade | Métrica (ambiente típico) | Observações |
| ---- | ---------------- | ---- |
| Inserção em máquina única | até ~890 mil TPS | testes combinados de lote / cache + SQLite / disco RAM |
| Consulta em máquina única | até ~1,8 bilhão de QPS | cenários de tabelas pequenas + cache de entidade / cache de objeto único |
| Volume de dados por tabela | dezenas de bilhões em produção | estratégias de paginação + sharding |

> O desempenho real depende do tipo de banco de dados / hardware / estratégia de sharding / taxa de acerto do cache.

### FAQ

- Por que o primeiro Count é rápido? `Meta.Count` usa índices do sistema; o SQLite pode primeiro obter o máximo do identity e depois refinar de forma assíncrona.
- Quão rápido o cache fica consistente após uma atualização? Gravações no processo atual são refletidas imediatamente; entre processos, aguarde a expiração do cache ou use uma estratégia de invalidação ativa.
- É possível desativar a migração automática? Defina `Migration=Off` (globalmente ou por tabela), ou use ReadOnly/On/Full conforme necessário.
- Consultas complexas com várias tabelas? Recomenda-se várias consultas de tabela única + associação em memória (mais rápido com acertos de cache e estrutura mais clara).

## Mapa do ecossistema

NewLife é um ecossistema em camadas de projetos open source versionados de forma independente. O mapa abaixo é compartilhado literalmente por todos os repositórios NewLife; cada projeto tem links para o seu repositório e para o seu pacote principal no NuGet.

| Projeto | O que faz | Downloads |
| --- | --- | --- |
| **Base** | | |
| [NewLife.Core](https://github.com/NewLifeX/X) | Log, configuração, cache, rede, serialização, agendamento e rastreamento APM. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Core?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Core) |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | Middleware de dados: tabelas únicas na escala de dezenas de bilhões de linhas, particionamento automático, separação de leitura e escrita, mais de 10 bancos de dados. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCode?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCode) |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | Biblioteca de rede de alto desempenho: 1,77M pps / 14,5 Gbps em uma máquina (TCP 1KB, 2026-10), 4M de conexões TCP persistentes. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Net?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Net) |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | Framework de comunicação por protocolo e RPC para aplicações cliente/servidor e dispositivos IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Remoting?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Remoting) |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | Instala aplicações .NET como daemons do sistema operacional: serviços do Windows e systemd do Linux. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Agent?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Agent) |
| [NewLife.Templates](https://github.com/NewLifeX/NewLife.Zero) | Modelos de projeto `dotnet new` para console, web, API, serviço, dados e IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Templates?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Templates) |
| **Dados e mensagens** | | |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | Cliente Redis com latência em microssegundos, milhões de operações por segundo e filas de mensagens integradas. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Redis?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Redis) |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | Cliente RocketMQ totalmente gerenciado para Apache RocketMQ e Alibaba Cloud Message Queue. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.RocketMQ?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.RocketMQ) |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | Cliente e servidor MQTT, incluindo conectividade com o Alibaba Cloud IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MQTT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MQTT) |
| [NewLife.NovaDb](https://github.com/NewLifeX/NewLife.NovaDb) | Banco de dados híbrido em C#: embarcado ou em rede, relacional, séries temporais, fila de mensagens e KV em um só. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.NovaDb?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.NovaDb) |
| [NewLife.MySql](https://github.com/NewLifeX/NewLife.MySql) | Driver MySql e camada auxiliar construídos sobre a pilha de dados NewLife. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MySql?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MySql) |
| **Web, IA e distribuído** | | |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | Framework de plataforma administrativa rápida: permissões, SSO, servidor OAuth e scaffolding CRUD. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Cube?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Cube) |
| [Stardust](https://github.com/NewLifeX/Stardust) | Plataforma de serviços distribuídos: gestão de nós, APM, centro de configuração, registro e centro de publicação. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Stardust?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Stardust) |
| [AntJob](https://github.com/NewLifeX/AntJob) | Plataforma distribuída de tarefas e agendamento de dados em tempo real, com particionamento, repetição e reprocessamento. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AntJob?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AntJob) |
| [NewLife.AI](https://github.com/NewLifeX/NewLife.AI) | Cliente unificado para 46 provedores de LLM, além do ChatAI, uma aplicação web de chat pronta para uso. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AI?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AI) |
| **Industrial e IoT** | | |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | Biblioteca padrão de IoT que define contratos de protocolo e abstrações de dispositivos. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IoT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IoT) |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | Implementação Modbus TCP/RTU/ASCII sobre NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Modbus?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Modbus) |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | Protocolo Siemens PLC sobre NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Siemens?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Siemens) |
| [NewLife.Omron](https://github.com/NewLifeX/NewLife.Omron) | Protocolo Omron PLC sobre NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Omron?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Omron) |
| [NewLife.Melsec](https://github.com/NewLifeX/NewLife.Melsec) | Protocolo Mitsubishi Melsec PLC sobre NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Melsec?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Melsec) |
| [NewLife.OPC](https://github.com/NewLifeX/NewLife.OPC) | Cliente OPC para acesso a dados industriais. | — |
| [NewLife.BACnet](https://github.com/NewLifeX/NewLife.BACnet) | Pilha do protocolo de automação predial BACnet. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.BACnet?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.BACnet) |
| [ZeroIoT](https://github.com/NewLifeX/ZeroIoT) | Plataforma IoT de código aberto leve, com gestão de gateways e dispositivos. | — |
| **Telemática veicular** | | |
| [NewLife.JT808](https://github.com/NewLifeX/NewLife.JT808) | Protocolo de posicionamento veicular JT/T 808: mais de 70 corpos de mensagem, 2011/2013/2019. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT808?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT808) |
| [NewLife.JT809](https://github.com/NewLifeX/NewLife.JT809) | Protocolo plataforma-a-plataforma JT/T 809 para autoridades de transporte. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT809?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT809) |
| [NewLife.JT1078](https://github.com/NewLifeX/NewLife.JT1078) | Vídeo veicular JT/T 1078: análise RTP, remux para FLV/fMP4 e transmissão ao vivo. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT1078?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT1078) |
| **Componentes de domínio** | | |
| [NewLife.Office](https://github.com/NewLifeX/NewLife.Office) | Um pacote para Excel, Word, PPT, PDF e mais de 15 formatos de documento. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Office?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Office) |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | Áudio multiplataforma: codecs, E/S de dispositivos, DSP, contêineres e streaming. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Audio?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Audio) |
| [NewLife.Video](https://github.com/NewLifeX/NewLife.Video) | Codecs de vídeo, formatos de contêiner, processamento de frames e streaming. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Video?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Video) |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | Interface unificada sobre Baidu, AMap, Tencent e Tianditu, com geocodificação e conversão de coordenadas. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Map?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Map) |
| [NewLife.IP](https://github.com/NewLifeX/NewLife.IP) | Geolocalização IPv4 com banco de dados offline mapeado em memória e API online como alternativa. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IP?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IP) |
| **Ferramentas e produtos** | | |
| [CrazyCoder](https://github.com/NewLifeX/XCoder) | Caixa de ferramentas do desenvolvedor: rede, porta serial, criptografia, regex, Modbus e MQTT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCoder?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCoder) |
| [GitCandy](https://github.com/NewLifeX/GitCandy) | Sistema auto-hospedado de gerenciamento de código-fonte Git. | — |
| [LuckyClover](https://github.com/NewLifeX/LuckyClover) | Instala automaticamente o runtime .NET correspondente quando iniciado em um host mais antigo. | — |

> Os badges mostram o pacote principal de cada projeto; pacotes de tema, extensão e satélites estão listados nas respectivas páginas do NuGet.Os mais de 30 pacotes não listados aqui (FTP, LoRa, extensões do Office, drivers de protocolo e outros) estão disponíveis na [organização NewLifeX](https://github.com/NewLifeX) e em [newlifex.com](https://newlifex.com). Esta tabela é mantida no README do NewLife.Core.

## Documentação

- [Navegação da documentação do XCode](Doc/XCode文档导航.md): índice completo de documentos agrupado por módulo
- [Manual do usuário do XCode](Doc/XCode使用手册.md): passo a passo de uso de ponta a ponta
- [Arquitetura](Doc/架构设计.md): camadas, componentes e decisões de projeto fundamentais
- [Localização multilíngue](Doc/多语言本地化.md): modelo de mensagens, tabelas de idioma e substituições em tempo de execução
- Documentação online: https://newlifex.com/xcode
- [ChangeLog.md](ChangeLog.md): histórico de versões

## Como contribuir

### Compilar e testar

```
dotnet restore
dotnet build -c Release
dotnet test XUnitTest.XCode -c Release
```

- Adicione casos em `XUnitTest.XCode`; sugestões relacionadas a desempenho devem incluir benchmarks ou explicações.
- Siga as convenções do repositório (nome forte, múltiplos TFMs, comentários XML, compilação condicional).
- Alterações na API pública exigem uma estratégia de migração (Obsolete → remoção).

### Release

Envie uma tag com prefixo `v` (por exemplo `v2026.1018`) para acionar `.github/workflows/publish.yml`. O workflow empacota XCode, XCodeTool e os pacotes de provedores (`XCode.MySql`, `XCode.SQLite`, `XCode.SqlServer`, `XCode.Oracle`, `XCode.PostgreSQL`, `XCode.DaMeng`, `XCode.DB2`, `XCode.KingBase`, `XCode.HighGo`) e os envia para NuGet.org e GitHub Packages.

### Feedback

- Issues / PRs são bem-vindos: otimização de desempenho / novos provedores de banco de dados / melhorias na documentação.
- Antes de enviar, certifique-se de que `dotnet build` e os testes unitários relevantes passam; siga as convenções do repositório para estilo de código e mensagens de commit.
- Relate bugs e sugestões via [Issue](https://github.com/NewLifeX/NewLife.XCode/issues); Stars e PRs são bem-vindos.

## Equipe

<img src="https://newlifex.com/logo.png" width="48" align="right" alt="NewLife" />

Fundada em 2002, a equipe NewLife dedica-se há mais de duas décadas ao ecossistema .NET e de IoT, mantém cerca de 100 projetos de código aberto (34 deles principais) e oferece soluções integradas de software e hardware para os setores de energia, logística, saúde, controle industrial e internet. Seguimos o lema "não há hierarquia no aprendizado — o domínio conquista o respeito".

Os componentes de código aberto já foram baixados <!--nuget-total:start-->6.48M<!--nuget-total:end--> vezes no NuGet (todo o ecossistema, até 2026-10).

Website: https://newlifex.com · Organização de código aberto: https://github.com/NewLifeX

<details>
<summary>Conta oficial no WeChat: 智能大石头 (escaneie para seguir)</summary>

![智能大石头](https://newlifex.com/stone.jpg)

</details>

## Licença

MIT, totalmente aberto. Uso comercial / modificação / redistribuição são permitidos (você pode substituir "NewLife" pela marca da sua empresa em implantações privadas). O certificado de nome forte `newlife.snk` é incorporado.

> Se este projeto ajuda você, dê uma Star, compartilhe com sua equipe ou envie sugestões de melhoria via Issue/PR.
