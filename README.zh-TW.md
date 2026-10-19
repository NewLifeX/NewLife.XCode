# NewLife.XCode - 資料中介層與超級 ORM

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
🌐 [English](README.md) | [简体中文](README.zh-CN.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

> ⚠️ 本翻譯可能落後於[英文 README](README.md)，內容以英文版為準。

NewLife.XCode 是面向大數據場景的資料中介層，支援 MySQL、SQLite、SqlServer、Oracle、PostgreSQL 與國產資料庫，提供自動分表、讀寫分離與實體快取。

## 安裝

```bash
dotnet add package NewLife.XCode
```

## 快速開始

```csharp
var list = User.FindAll(User._.Status == 1, new PageParameter { PageIndex = 1, PageSize = 20 });
```

## 核心特性

- 高吞吐、低延遲，已在生產環境大規模驗證
- 多目標框架 net45 ~ net10.0，舊系統可平滑接入
- 與 NewLife 生態（XCode/Cube/Remoting）無縫整合

## 文件

> 📖 完整文件（中文）見 [儲存庫文件](Doc)

## 授權

MIT License © 2002-2026 新生命開發團隊

[GitHub](https://github.com/NewLifeX/NewLife.XCode) | [Website](https://newlifex.com)
