# NewLife.XCode - Middleware de dados e super ORM

![GitHub License](https://img.shields.io/github/license/NewLifeX/NewLife.XCode?logo=github)
![NuGet Downloads](https://img.shields.io/nuget/dt/NewLife.XCode?logo=nuget)
![NuGet](https://img.shields.io/nuget/v/NewLife.XCode?logo=nuget)
![NuGet prerelease](https://img.shields.io/nuget/vpre/NewLife.XCode?label=dev%20nuget&logo=nuget)
[![test](https://github.com/NewLifeX/NewLife.XCode/actions/workflows/test.yml/badge.svg?branch=master)](https://github.com/NewLifeX/NewLife.XCode/actions/workflows/test.yml)
![platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS-blue)
[![contributors](https://img.shields.io/github/contributors/NewLifeX/NewLife.XCode?logo=github)](https://github.com/NewLifeX/NewLife.XCode/graphs/contributors)

<!-- lang:start -->
🌐 [中文](Readme.MD) | [繁體中文](README.zh-TW.md) | [English](README.en.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Русский](README.ru.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

> Esta tradução pode estar desatualizada em relação à versão em chinês.

NewLife.XCode é um middleware de dados para grandes volumes, com MySQL, SQLite, SQL Server, Oracle, PostgreSQL e bancos nacionais, além de sharding, separação leitura/escrita e cache de entidades.

## Instalação

```bash
dotnet add package NewLife.XCode
```

## Início rápido

```csharp
var list = User.FindAll(User._.Status == 1, new PageParameter { PageIndex = 1, PageSize = 20 });
```

## Recursos

- Alta taxa de transferência e baixa latência, comprovados em produção
- Alvos net45 a net10.0 para adoção em sistemas legados
- Integração perfeita com o ecossistema NewLife (XCode/Cube/Remoting)

## Documentação

> 📖 Documentação completa (em chinês): [documentos do repositório](Doc)

## Licença

MIT License © 2002-2026 Equipe NewLife

[GitHub](https://github.com/NewLifeX/NewLife.XCode) | [Website](https://newlifex.com)
