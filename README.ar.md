# Stardust - منصة الخدمات الموزعة Stardust

![GitHub License](https://img.shields.io/github/license/NewLifeX/Stardust?logo=github)
![NuGet Downloads](https://img.shields.io/nuget/dt/NewLife.Stardust?logo=nuget)
![NuGet](https://img.shields.io/nuget/v/NewLife.Stardust?logo=nuget)
![NuGet prerelease](https://img.shields.io/nuget/vpre/NewLife.Stardust?label=dev%20nuget&logo=nuget)
[![test](https://github.com/NewLifeX/Stardust/actions/workflows/test.yml/badge.svg?branch=master)](https://github.com/NewLifeX/Stardust/actions/workflows/test.yml)
[![locales](https://img.shields.io/badge/locales-31-2ea44f?logo=dotnet)](Doc/多语言本地化.md)
![platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS%20%7C%20Android%20%7C%20iOS-blue)
![arch](https://img.shields.io/badge/arch-x64%20%7C%20arm64%20%7C%20riscv64%20%7C%20s390x-orange)
[![contributors](https://img.shields.io/github/contributors/NewLifeX/Stardust?logo=github)](https://github.com/NewLifeX/Stardust/graphs/contributors)

<!-- lang:start -->
🌐 [English](README.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md)
<!-- lang:end -->

> ⚠️ قد تكون هذه الترجمة قديمة؛ المرجع هو [README بالإنجليزية](README.md).
> ℹ️ قد يتأخر هيكل هذه الترجمة؛ المرجع هو [README بالإنجليزية](README.md).

Stardust منصة خدمات موزعة تتضمن إدارة العقد والسجل ومركز التهيئة ومراقبة APM ومركز الإصدارات والأوامر عن بُعد.

## التثبيت

```bash
dotnet add package NewLife.Stardust
```

## البدء السريع

```csharp
var client = new StarClient("http://star.newlifex.com:6600") { AppId = "app" };
client.Start();
```

<details>
<summary><b>الميزات الرئيسية</b> (3)</summary>

- تسجيل وتتبع وتشخيص شامل أثناء التشغيل
- مُختبر في بيئات إنتاج طويلة الأمد
- بنية قابلة للتوسع والإضافات لتطوير مخصص

</details>

## التوثيق

> 📖 التوثيق الكامل (بالصينية): [مستندات المستودع](Doc)

## الترخيص

MIT License © 2002-2026 فريق NewLife

[GitHub](https://github.com/NewLifeX/Stardust) | [Website](https://newlifex.com)
