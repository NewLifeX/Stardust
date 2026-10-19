# Stardust - Платформа распределённых сервисов Stardust

![GitHub License](https://img.shields.io/github/license/NewLifeX/Stardust?logo=github)
![NuGet Downloads](https://img.shields.io/nuget/dt/NewLife.Stardust?logo=nuget)
![NuGet](https://img.shields.io/nuget/v/NewLife.Stardust?logo=nuget)
![NuGet prerelease](https://img.shields.io/nuget/vpre/NewLife.Stardust?label=dev%20nuget&logo=nuget)
[![test](https://github.com/NewLifeX/Stardust/actions/workflows/test.yml/badge.svg?branch=master)](https://github.com/NewLifeX/Stardust/actions/workflows/test.yml)
![platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS%20%7C%20Android%20%7C%20iOS-blue)
![arch](https://img.shields.io/badge/arch-x64%20%7C%20arm64-orange)
[![contributors](https://img.shields.io/github/contributors/NewLifeX/Stardust?logo=github)](https://github.com/NewLifeX/Stardust/graphs/contributors)

<!-- lang:start -->
🌐 [中文](Readme.MD) | [繁體中文](README.zh-TW.md) | [English](README.en.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

> Перевод может отставать от китайской версии.

Stardust — платформа распределённых сервисов с управлением узлами, реестром, центром конфигурации, APM-мониторингом, центром релизов и удалёнными командами.

## Установка

```bash
dotnet add package NewLife.Stardust
```

## Быстрый старт

```csharp
var client = new StarClient("http://star.newlifex.com:6600") { AppId = "app" };
client.Start();
```

## Возможности

- Полное журналирование, трассировка и диагностика выполнения
- Проверено в длительной промышленной эксплуатации
- Расширяемая модульная архитектура для собственной разработки

## Документация

> 📖 Полная документация (на китайском): [документы репозитория](Doc)

## Лицензия

MIT License © 2002-2026 Команда NewLife

[GitHub](https://github.com/NewLifeX/Stardust) | [Website](https://newlifex.com)
