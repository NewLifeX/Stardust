# Stardust - Plataforma de servicios distribuidos Stardust

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
🌐 [English](README.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

> ⚠️ Esta traducción puede estar desactualizada; la referencia es el [README en inglés](README.md).
> ℹ️ La estructura de esta traducción puede estar desactualizada; prevalece el [README en inglés](README.md).

Stardust es una plataforma de servicios distribuidos con gestión de nodos, registro, centro de configuración, monitoreo APM, centro de despliegues y comandos remotos.

## Instalación

```bash
dotnet add package NewLife.Stardust
```

## Inicio rápido

```csharp
var client = new StarClient("http://star.newlifex.com:6600") { AppId = "app" };
client.Start();
```

<details>
<summary><b>Características</b> (3)</summary>

- Registro, trazabilidad y diagnóstico en tiempo de ejecución completos
- Probado en entornos de producción de larga duración
- Arquitectura extensible y conectable para desarrollo propio

</details>

## Documentación

> 📖 Documentación completa (en chino): [documentos del repositorio](Doc)

## Licencia

MIT License © 2002-2026 Equipo NewLife

[GitHub](https://github.com/NewLifeX/Stardust) | [Website](https://newlifex.com)
