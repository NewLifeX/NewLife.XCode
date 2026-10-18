# NewLife.XCode - Промежуточное ПО данных и супер-ORM

![GitHub License](https://img.shields.io/github/license/NewLifeX/NewLife.XCode?logo=github)
![NuGet Downloads](https://img.shields.io/nuget/dt/NewLife.XCode?logo=nuget)
![NuGet](https://img.shields.io/nuget/v/NewLife.XCode?logo=nuget)
![NuGet prerelease](https://img.shields.io/nuget/vpre/NewLife.XCode?label=dev%20nuget&logo=nuget)
[![test](https://github.com/NewLifeX/NewLife.XCode/actions/workflows/test.yml/badge.svg?branch=master)](https://github.com/NewLifeX/NewLife.XCode/actions/workflows/test.yml)
![platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS-blue)
[![contributors](https://img.shields.io/github/contributors/NewLifeX/NewLife.XCode?logo=github)](https://github.com/NewLifeX/NewLife.XCode/graphs/contributors)

<!-- lang:start -->
🌐 [中文](Readme.MD) | [繁體中文](README.zh-TW.md) | [English](README.en.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

> Перевод может отставать от китайской версии.

NewLife.XCode — промежуточное ПО данных для больших объёмов: MySQL, SQLite, SQL Server, Oracle, PostgreSQL и национальные СУБД, с шардированием, разделением чтения/записи и кэшем сущностей.

## Установка

```bash
dotnet add package NewLife.XCode
```

## Быстрый старт

```csharp
var list = User.FindAll(User._.Status == 1, new PageParameter { PageIndex = 1, PageSize = 20 });
```

## Возможности

- Высокая пропускная способность и низкая задержка, проверено в продакшене
- Цели net45–net10.0 для плавного внедрения в унаследованные системы
- Бесшовная интеграция с экосистемой NewLife (XCode/Cube/Remoting)

## Документация

> 📖 Полная документация (на китайском): [документы репозитория](Doc)

## Лицензия

MIT License © 2002-2026 Команда NewLife

[GitHub](https://github.com/NewLifeX/NewLife.XCode) | [Website](https://newlifex.com)
