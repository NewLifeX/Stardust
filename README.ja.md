# Stardust - Stardust 分散サービスプラットフォーム

![GitHub License](https://img.shields.io/github/license/NewLifeX/Stardust?logo=github)
![NuGet Downloads](https://img.shields.io/nuget/dt/NewLife.Stardust?logo=nuget)
![NuGet](https://img.shields.io/nuget/v/NewLife.Stardust?logo=nuget)
![NuGet prerelease](https://img.shields.io/nuget/vpre/NewLife.Stardust?label=dev%20nuget&logo=nuget)
[![test](https://github.com/NewLifeX/Stardust/actions/workflows/test.yml/badge.svg?branch=master)](https://github.com/NewLifeX/Stardust/actions/workflows/test.yml)
![platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS%20%7C%20Android%20%7C%20iOS-blue)
![arch](https://img.shields.io/badge/arch-x64%20%7C%20arm64-orange)
[![contributors](https://img.shields.io/github/contributors/NewLifeX/Stardust?logo=github)](https://github.com/NewLifeX/Stardust/graphs/contributors)

<!-- lang:start -->
🌐 [中文](Readme.MD) | [繁體中文](README.zh-TW.md) | [English](README.en.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

> この翻訳は中国語版より遅れている場合があります。

Stardust はノード管理、レジストリ、設定センター、APM 監視、リリースセンター、リモートコマンドを備えた分散サービスプラットフォームです。

## インストール

```bash
dotnet add package NewLife.Stardust
```

## クイックスタート

```csharp
var client = new StarClient("http://star.newlifex.com:6600") { AppId = "app" };
client.Start();
```

## 主な機能

- ログ、トレーシング、実行時診断を充実
- 長期の本番運用で実績あり
- プラグイン可能な拡張アーキテクチャで二次開発が容易

## ドキュメント

> 📖 詳細ドキュメント（中国語）： [リポジトリ内ドキュメント](Doc)

## ライセンス

MIT License © 2002-2026 NewLife チーム

[GitHub](https://github.com/NewLifeX/Stardust) | [Website](https://newlifex.com)
