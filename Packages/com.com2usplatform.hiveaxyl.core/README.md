# Hive Axyl Core

Core infrastructure module for Hive Axyl SDK (Unity).

Provides foundation services: logging, session management, transport abstraction,
native bridge interface, configuration, threading dispatcher, serialization contracts,
and the canonical error/result types.

## Installation

Install from OpenUPM (`https://package.openupm.com`, scope
`com.com2usplatform.hiveaxyl`). The repository README has the full
`manifest.json` setup.

## Quick Start

```csharp
using Hive.Axyl.Core;
using Hive.Axyl.Core.Unity;

var config = CoreConfig.CreateBuilder("my-app").Build();

HiveBootstrap.Initialize(config);
```

`HiveBootstrap` (Unity adapter) assembles the platform adapters and
delegates to `HiveCore.Initialize`, so initialize through `HiveBootstrap`
rather than calling `HiveCore.Initialize` yourself. Once initialized, resolve
registered services with `HiveCore.Resolve<T>()` or `HiveCore.TryResolve<T>(out var service)`.

## Distribution

This package contains a **precompiled Core DLL** plus **Unity adapter source**.

> `Hive.Axyl.Core.Internal.dll` is the implementation assembly; the public
> `Hive.Axyl.Core` asmdef wraps it via `precompiledReferences` so adapter
> code can use Core internals via `[InternalsVisibleTo]`. Downstream code
> references `Hive.Axyl.Core` (the asmdef name), not the DLL.

The native libraries ship under `Runtime/Plugins/<platform>` (Android, iOS,
macOS, Windows, WebGL).

## License

See [LICENSE.md](LICENSE.md).
