# NewLife.XCode - Daten-Middleware und Super-ORM

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

> ⚠️ Diese Übersetzung kann veraltet sein; maßgeblich ist das [englische README](README.md).

NewLife.XCode ist eine Daten-Middleware für große Datenmengen mit Unterstützung für MySQL, SQLite, SQL Server, Oracle, PostgreSQL und nationale Datenbanken – inklusive Sharding, Read/Write-Splitting und Entity-Cache.

## Installation

```bash
dotnet add package NewLife.XCode
```

## Schnellstart

```csharp
var list = User.FindAll(User._.Status == 1, new PageParameter { PageIndex = 1, PageSize = 20 });
```

## Funktionen

- Hoher Durchsatz und niedrige Latenz, im Produktivbetrieb erprobt
- Ziele von net45 bis net10.0 für die reibungslose Integration in Altsysteme
- Nahtlose Integration in das NewLife-Ökosystem (XCode/Cube/Remoting)

## Dokumentation

> 📖 Vollständige Dokumentation (Chinesisch): [Repository-Dokumente](Doc)

## Lizenz

MIT License © 2002-2026 NewLife-Team

[GitHub](https://github.com/NewLifeX/NewLife.XCode) | [Website](https://newlifex.com)
