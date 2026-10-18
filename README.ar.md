# NewLife.XCode - وسيط بيانات وORM فائق

![GitHub License](https://img.shields.io/github/license/NewLifeX/NewLife.XCode?logo=github)
![NuGet Downloads](https://img.shields.io/nuget/dt/NewLife.XCode?logo=nuget)
![NuGet](https://img.shields.io/nuget/v/NewLife.XCode?logo=nuget)
![NuGet prerelease](https://img.shields.io/nuget/vpre/NewLife.XCode?label=dev%20nuget&logo=nuget)
[![test](https://github.com/NewLifeX/NewLife.XCode/actions/workflows/test.yml/badge.svg?branch=master)](https://github.com/NewLifeX/NewLife.XCode/actions/workflows/test.yml)
![platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS-blue)
[![contributors](https://img.shields.io/github/contributors/NewLifeX/NewLife.XCode?logo=github)](https://github.com/NewLifeX/NewLife.XCode/graphs/contributors)

<!-- lang:start -->
🌐 [中文](Readme.MD) | [繁體中文](README.zh-TW.md) | [English](README.en.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md)
<!-- lang:end -->

> قد تتأخر هذه الترجمة عن النسخة الصينية.

NewLife.XCode وسيط بيانات للأحجام الكبيرة، يدعم MySQL وSQLite وSQL Server وOracle وPostgreSQL وقواعد البيانات المحلية، مع التجزئة وفصل القراءة/الكتابة وتخزين الكيانات.

## التثبيت

```bash
dotnet add package NewLife.XCode
```

## البدء السريع

```csharp
var list = User.FindAll(User._.Status == 1, new PageParameter { PageIndex = 1, PageSize = 20 });
```

## الميزات الرئيسية

- إنتاجية عالية وزمن استجابة منخفض، مُثبت على نطاق الإنتاج
- يدعم net45 إلى net10.0 لسهولة التبني في الأنظمة القديمة
- تكامل سلس مع منظومة NewLife (XCode/Cube/Remoting)

## التوثيق

> 📖 التوثيق الكامل (بالصينية): [مستندات المستودع](Doc)

## الترخيص

MIT License © 2002-2026 فريق NewLife

[GitHub](https://github.com/NewLifeX/NewLife.XCode) | [Website](https://newlifex.com)
