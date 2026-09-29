# Hive Axyl Core

Hive Axyl SDK (Unity)의 Core 인프라 모듈.

로깅, 세션 관리, Transport 추상화, 네이티브 브릿지 인터페이스, 설정 관리, 스레딩 디스패처, 직렬화, 표준 에러/결과 타입을 제공한다.

## 설치

OpenUPM(`https://package.openupm.com`, scope `com.com2usplatform.hiveaxyl`)에서
설치한다. `manifest.json` 설정 전체는 저장소 README에 있다.

## 빠른 시작

```csharp
using Hive.Axyl.Core;
using Hive.Axyl.Core.Unity;

var config = CoreConfig.CreateBuilder("my-app").Build();

HiveBootstrap.Initialize(config);
```

`HiveBootstrap` (Unity 어댑터) 가 플랫폼 어댑터를 조립하여 내부적으로
`HiveCore.Initialize` 를 호출한다. 앱은 `HiveCore` 를 직접 호출하지 않는다.

## 배포 형태

이 패키지는 **사전 빌드된 Core DLL**과 **Unity 어댑터 소스**로 구성된다.

> `Hive.Axyl.Core.Internal.dll` 은 _구현_ 어셈블리이고, _공개_ 식별자는
> `Hive.Axyl.Core` asmdef 이름이다. asmdef 가 `precompiledReferences` 로
> DLL을 감싸며, 어댑터 코드는 `[InternalsVisibleTo]` 로 Core internals 에
> 접근한다. 외부 코드는 _DLL 이름이 아니라_ asmdef 이름을 참조한다.

### 릴리즈 패키지 구조

```
com.com2usplatform.hiveaxyl.core/
├── package.json
├── README.md
├── CHANGELOG.md
├── LICENSE.md
└── Runtime/
    ├── com.com2usplatform.hiveaxyl.core.asmdef       ← 어셈블리 정의, 아래 DLL을 참조
    ├── link.xml                         ← IL2CPP managed code stripping 방지
    ├── Plugins/
    │   ├── Hive.Axyl.Core.Internal.dll  ← 사전 빌드된 Core (순수 C#, netstandard2.1)
    │   └── WebGL/
    │       ├── HiveAxylCore.jslib       ← WebGL JavaScript 브릿지
    │       └── HiveAxylCore.jspre       ← 번들된 브라우저 런타임 (pre-js)
    └── Unity/                            ← Unity 어댑터 소스 (asmdef가 컴파일)
        ├── HiveBootstrap.cs
        ├── UnityDispatcher.cs
        ├── UnityTransport.cs
        ├── UnityAppEnvironment.cs
        ├── UnityConsoleSink.cs
        └── Bridge/
```

## 라이선스

[LICENSE.md](LICENSE.md) 참조.
