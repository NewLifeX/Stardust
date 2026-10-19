# Stardust - Stardust 분산 서비스 플랫폼

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
🌐 [English](README.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [العربية](README.ar.md)
<!-- lang:end -->

> ⚠️ 이 번역은 오래되었을 수 있습니다. 기준은 [영어 README](README.md)입니다.
> ℹ️ 이 번역의 구조는 오래되었을 수 있으며, 기준은 [영어 README](README.md)입니다.

Stardust는 노드 관리, 레지스트리, 구성 센터, APM 모니터링, 릴리스 센터, 원격 명령을 제공하는 분산 서비스 플랫폼입니다.

## 설치

```bash
dotnet add package NewLife.Stardust
```

## 빠른 시작

```csharp
var client = new StarClient("http://star.newlifex.com:6600") { AppId = "app" };
client.Start();
```

<details>
<summary><b>주요 기능</b> (3)</summary>

- 로깅, 추적, 런타임 진단 지원
- 장기 운영 환경에서 검증됨
- 플러그인 방식의 확장 가능한 아키텍처

</details>

## 문서

> 📖 전체 문서(중국어): [저장소 문서](Doc)

## 라이선스

MIT License © 2002-2026 NewLife 팀

[GitHub](https://github.com/NewLifeX/Stardust) | [Website](https://newlifex.com)
