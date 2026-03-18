# AI Session Bootstrap

## 목적
- 이 프로젝트를 새 세션에서 가장 빠르게 재이해하기 위한 첫 진입 문서.
- 큰 그림, 런타임 루트, 수정 시 주의점, 현재 기대 동작을 먼저 잡고 나서 세부 파일은 `Docs/Scripts-Directory-Guide.md`와 `Docs/Project-File-Map.md`로 내려간다.

## 먼저 읽을 것
1. `Docs/AI-Session-Bootstrap.md`
2. `Docs/Project-File-Map.md`
3. `Docs/Scripts-Directory-Guide.md`
4. 파트 추가/수정이면 `PART_SPEC.md`
5. 실제 수정은 해당 시스템의 `.cs`, `.prefab`, `.asset`, `SampleScene.unity` 순서로 확인

## 프로젝트 스냅샷
- 엔진: Unity `6000.2.4f1`
- 렌더링: `URP 17.2.0`
- 입력: `Input System 1.14.2`
- 씬: 빌드에 등록된 씬은 `Assets/Scenes/SampleScene.unity` 하나뿐이다.
- 물리: Unity 전역 중력은 `0,0,0`이고, 실제 중력은 `Gravity` / `GravityAffectedMover` / `OrbitRevolution` 조합으로 구현한다.
- 게임 흐름: 절차적 태양계 생성 -> 천체/인공위성 focus 탐색 -> 인공위성 조립 -> Launcher로 우주선 발사 -> 수동 비행/target 지정 -> target 중심 공전 보조 -> 안정화 후 `Orbit` commit -> `OrbitRevolution` 기반 공전.

## 현재 씬 루트
`SampleScene.unity`의 주요 루트 오브젝트는 아래와 같다.

- `WorldOriginManager`: world shift 이벤트 발행.
- `SpaceScaleManager`: `ScaledSpaceManager` 루트.
- `Main Camera`: `CameraManager` + `SmallScaleOverlayCamera`.
- `Solar System Generator`: 시작 시 태양계 생성.
- `Focus Manager`: 현재 focus/hover와 focus info text 관리.
- `Assembly Manager`: 인공위성 조립 모드 오케스트레이션.
- `UI`: 주기 UI 루트.
- `TimeManager`: 시간 상태 제어.
- `LauncherLaunchSystem`: 런처 관련 프리젠터/컨트롤러 부트스트랩.
- `EventSystem`: UI 이벤트 시스템.

## 런타임 구조

### 1. Focus / Camera / Input
- 모든 상호작용의 출발점은 `FocusManager.currentFocus`다.
- `UserInput.FocusInput.cs`가 클릭/hover raycast를 처리하고, 현재 focus가 우주선이면 클릭을 focus 변경이 아니라 `CurrentTarget` 토글로 해석한다.
- `CameraManager`는 focus를 따라가며, drag/zoom/assembly mode/중력장 출입 줌을 모두 담당한다.
- `DefaultCameraFocusPolicy`가 rotation/zoom 기준을 정한다.
- 커밋된 공전 상태의 우주선은 일반 우주선 카메라 규칙이 아니라, target이 화면 아래에 오도록 별도 회전 규칙을 쓴다.

### 2. Large World / WorldPosition / Scaled Space
- 큰 좌표는 `WorldPosition.worldPosition`과 `Double3`가 진실(source of truth)이다.
- `LargeWorldCoordinator`가 local transform을 world origin 기준으로 다시 써 준다.
- 원점 재조정은 visual `LateUpdate`와 physics `FixedUpdate`에서 따로 다룬다.
- orbit-driven 객체를 수정할 때 `transform.position`만 직접 건드리면 jitter가 다시 생긴다. 가능한 한 `WorldPosition`, `OrbitRevolution`, `LargeWorldCoordinator` 경로를 유지해야 한다.
- 멀리 줌아웃하면 `ScaledSpaceManager`가 `SmallScale` 레이어용 프록시를 만든다. 원본 renderer/orbit line은 필요 시 숨겨진다.

### 3. Celestial / Orbit
- 항성, 행성, 자연위성, 인공위성, 커밋된 우주선 공전은 전부 `OrbitRevolution`이 공전 좌표를 계산한다.
- `OrbitRevolution`은 현재 angle만 저장하는 단순 스크립트가 아니라, parent center velocity/acceleration, render interpolation, origin rebase 후 interpolation reset까지 포함한다.
- `OrbitVisualizer`도 별도 수학식을 쓰지 않고 `OrbitRevolution` 샘플을 따라가도록 맞춰진 상태다.
- `Gravity`는 중력 소스, `GravityField`는 시각화, `GravityAffectedMover`는 이동체 쪽 소비자다.

### 4. Solar System Generation
- `SolarSystemGenerator` 시작 시 한 개의 `Central Star`, 6개의 planet, 자연위성, asteroid belt를 생성한다.
- 모든 planet에는 artificial satellite가 하나씩 자동 생성된다.
- 질량은 `Gravity` 컴포넌트가 있는 천체만 2배 적용되며, `GravityRadius`는 그대로 둔다.
- 생성 대상 prefab은 `SolarSystemSettings.asset`에서 가져오고, planet/satellite 속성은 `BiomeSettings/*.asset`을 사용한다.

### 5. Artificial Satellite / Assembly
- `ArtificialSatellite`는 조립 가능한 위성 루트다.
- `Assembly`는 grid 기반 조립 시스템이고, `PartDB`는 `Resources/Parts`에서 파트를 로딩한다.
- runtime port 최적화 포인트:
  - `Assembly.PortGraph.cs`에서 port graph를 cache한다.
  - `AssemblyCorePortLayout` / `AssemblyPartPortLayout` 같은 명시적 레이아웃을 우선 사용한다.
  - `AssemblyMeshCombiner`는 본체 mesh는 합치되 `AssemblyPortVisualMarker`가 붙은 port 비주얼은 살아남게 한다.
- 현재 선택 모드는 예전 path selection이 아니라 drag rectangle 기반이다.
- drag는 screen rect로 보이지만 판정은 grid 범위 기준이다. drag 시작 cell부터 끝 cell까지의 모든 occupied cell에 닿는 part가 선택된다.
- 선택된 part highlight 색은 `RGBA(255, 255, 255, 63)`다.

### 6. Spaceship / Launcher / Orbit Assist
- `LauncherSpawner`가 실제 우주선을 생성하고, `GravityAffectedMover`가 launched state를 관리한다.
- 우주선 포커스는 본체가 아니라 `SpaceshipFocusProxy`를 거친다.
- 현재 focus가 우주선이면 클릭한 천체/인공위성은 `CurrentTarget`이 된다. 인공위성도 target 지정 가능하다.
- `Space`는 이제 hold가 아니라 toggle이다. 켜지면 target 중심 공전 보조를 수행하고, 다시 누르면 해제된다.
- 이 보조는 target의 `GravityRadius` 안에서만 동작한다.
- 안정적인 공전이 일정 시간 유지되면 `SpaceshipOrbitCommitPresenter`가 `Orbit` 버튼을 띄운다.
- 버튼을 누르면 현재 상대 공전 값을 `OrbitRevolution`의 `semiMajorAxis/semiMinorAxis/revolutionPeriod/currentAngle/isClockwise`로 옮기고, `OrbitVisualizer`와 `SpaceshipCommittedOrbitRenderSync`까지 붙는다.
- commit 이후:
  - 우주선은 공전 진행 방향을 바라본다.
  - 카메라는 artificial satellite와 유사하게 회전 추종하되, target이 화면 아래로 가도록 회전값을 잡는다.
  - `W/S`는 공전 반지름, `A/D`는 공전 속도를 조절한다.
  - `A/D` 가속/감속 방향은 clockwise / counter-clockwise를 고려해 뒤집힌다.
  - 우주선 렌더 위치와 orbit line은 같은 interpolation 경로를 사용하도록 맞춰져 있다.
- 우주선이 `Launcher`와 다시 충돌하면 `Success` 로그가 찍히도록 되어 있다.

### 7. Time / Replay
- `TimeManager`가 play, pause, fast-forward, rewind, replay 상태를 관리한다.
- `BaseTimeRecorder` / `PhysicsTimeRecorder`는 stateful 객체의 위치/회전/속도 히스토리를 기록한다.
- `OrbitRevolution`은 stateless orbiter로 취급되어 시간 상태 변화에 따라 별도 배율/정지가 적용된다.
- `PeriodUI` / `PeriodVisualizer`는 공전 주기와 LCM 기반 주기 시각화를 보여준다.

## 수정할 때 가장 자주 보는 파일
- focus/target/raycast: `Assets/Scripts/Core/UserInput.FocusInput.cs`, `Assets/Scripts/Core/FocusManager.cs`, `Assets/Scripts/Core/DefaultCameraFocusPolicy.cs`
- camera zoom/rotation/origin: `Assets/Scripts/Core/CameraManager.cs`, `Assets/Scripts/Core/CameraManager.FocusAndInput.cs`, `Assets/Scripts/Core/CameraManager.WorldOrigin.cs`, `Assets/Scripts/Core/CameraManager.Collision.cs`
- large-world jitter: `Assets/Scripts/World/LargeWorld/LargeWorldCoordinator.cs`, `Assets/Scripts/World/Celestial/OrbitRevolution.cs`, `Assets/Scripts/Gameplay/Spaceship/SpaceshipCommittedOrbitRenderSync.cs`
- celestial orbit/visual: `Assets/Scripts/World/Celestial/OrbitVisualizer.cs`, `Assets/Scripts/World/Celestial/Gravity.cs`, `Assets/Scripts/World/Celestial/GravityField.cs`
- spaceship orbit assist/commit: `Assets/Scripts/Gameplay/Spaceship/SpaceshipRendezvousController.cs`, `Assets/Scripts/Gameplay/Spaceship/SpaceshipOrbitCommitPresenter.cs`, `Assets/Scripts/Gameplay/Spaceship/GravityAffectedMover.cs`, `Assets/Scripts/Gameplay/Spaceship/Spaceship.cs`
- assembly selection/ports: `Assets/Scripts/Gameplay/Assembly/Assembly.cs`, `Assets/Scripts/Gameplay/Assembly/Assembly.SelectionDrag.cs`, `Assets/Scripts/Gameplay/Assembly/Assembly.RuntimePorts.cs`, `Assets/Scripts/Gameplay/Assembly/Assembly.PortGraph.cs`, `Assets/Scripts/Gameplay/Assembly/AssemblyMeshCombiner.cs`
- generation data: `Assets/Scripts/System/SolarSystem/SolarSystemGenerator.cs`, `Assets/Scripts/System/SolarSystem/SolarSystemFactory.cs`, `Assets/Settings/SolarSystemSettings.asset`, `Assets/Settings/BiomeSettings/*.asset`

## 현재 기대 동작 체크리스트
- focus된 spaceship에서 클릭은 focus 변경이 아니라 target 토글이다.
- artificial satellite도 spaceship target이 될 수 있다.
- `Space`는 target 중심 공전 보조 토글이다.
- 안정화되면 `Orbit` 버튼이 떠야 한다.
- orbit commit 후 orbit line과 spaceship은 시각적으로 같은 경로를 따라가야 한다.
- selection mode drag는 사각형 UI를 띄우고, drag 시작/끝 grid 범위를 기준으로 파트를 선택해야 한다.
- 설치가 끝난 part에서도 port mesh가 보여야 한다.
- gravity 없는 천체/오브젝트는 mass 2배 예외다.

## 주의점
- large-world 계층에서는 `transform.position`만 수정하는 패치를 쉽게 넣지 말 것. `WorldPosition`과 coordinator sync를 깨뜨리면 jitter와 raycast mismatch가 같이 생긴다.
- orbit line과 본체가 조금이라도 어긋나면 `OrbitRevolution` 보간 경로와 render sync 경로가 같은지 먼저 확인할 것.
- assembly port 관련 수정은 `AssemblyPortVisualMarker`와 mesh combine 예외를 같이 봐야 한다.
- `Docs/Debug-Handoff-LargeWorld-Raycast.md`는 유용한 히스토리가 있지만 현재 파일 인코딩이 깨져 있어 읽기 품질이 낮다.

## 운영 메모
- 현재 작업 흐름상 사용자는 대체로 분석보다 직접 구현을 선호한다.
- 최근 세션에서는 빌드 확인을 하지 말라는 요청이 반복되었다. 새 세션에서도 명시적 요청이 없으면 먼저 확인할 것.

## 새 세션용 첫 프롬프트 템플릿
```text
Read these first before making changes:
- Docs/AI-Session-Bootstrap.md
- Docs/Project-File-Map.md
- Docs/Scripts-Directory-Guide.md
- PART_SPEC.md if the task touches assembly parts/assets

Then summarize:
1. Current architecture and high-risk systems.
2. The exact files likely relevant to the task.
3. Any large-world / orbit / assembly constraints that must be preserved.

Do not assume direct transform edits are safe for orbit-driven objects.
Check whether the task touches Focus, Camera, WorldPosition, OrbitRevolution, Assembly runtime ports, or Spaceship rendezvous/orbit systems.
```
