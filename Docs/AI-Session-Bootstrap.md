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
- 게임 흐름: 절차적 태양계 생성 -> 천체/인공위성 focus 탐색 -> 인공위성 조립(파트/파이프/structure/output port) -> core structure 및 logistics/module 상호작용 -> launcher stock 확보 -> launcher panel에서 우주선 발사 -> 수동 비행/target 지정 -> target 중심 공전 보조 -> 안정화 후 `Orbit` commit -> `OrbitRevolution` 기반 공전.

## 현재 씬 루트
`SampleScene.unity`의 주요 루트 오브젝트는 아래와 같다.

- `WorldOriginManager`: world shift 이벤트 발행.
- `SpaceScaleManager`: `ScaledSpaceManager` 루트.
- `Main Camera`: `CameraManager` + `SmallScaleOverlayCamera`.
- `Solar System Generator`: 시작 시 태양계 생성과 large-world 매니저 bootstrap.
- `Focus Manager`: 현재 focus/hover와 focus info text 관리.
- `Assembly Manager`: 인공위성 조립 모드 오케스트레이션.
- `UI`: 주기 UI 루트.
- `TimeManager`: 시간 상태 제어.
- `LauncherLaunchSystem`: focus 관련 presenter/bootstrap 루트.
- `EventSystem`: UI 이벤트 시스템.

## 런타임 구조

### 1. Focus / Camera / Input
- 모든 상호작용의 출발점은 `FocusManager.currentFocus`다.
- `UserInput.FocusInput.cs`가 클릭/hover raycast를 처리하고, focused satellite 내부 visual raycast, structure 우선 hit, `AssemblyOutputPortFocus` 판정까지 담당한다.
- world input은 아래 상황에서 막힌다.
  - `LauncherSelectionPresenter` panel 위 pointer
  - `CoreLogisticsHubPresenter` panel 위 pointer
  - `ModuleInventoryPresenter` panel 위 pointer
  - `ModuleProcessingPresenter` panel 위 pointer
  - `ModuleOutputPortSelectorPresenter` output-port 버튼 위 pointer
  - `AssemblyUI`의 parts / structures / fabricator popup
- 현재 focus가 우주선이면 클릭은 focus 변경이 아니라 `CurrentTarget` 토글로 해석한다. target 후보는 `Star`, `Planet`, `Satellite`, `ArtificialSatellite`다.
- `C`는 현재 spaceship focus를 해제하고, `X`는 `CameraManager.ResetToCurrentFocusView()`로 현재 focus 타입의 기본 카메라 뷰를 다시 적용한다.
- `FocusParent` 경로에는 part, structure뿐 아니라 `AssemblyOutputPortFocus`도 포함된다.
- `CameraManager`는 focus 추적, drag, zoom, assembly projection 전환, gravity-field 줌, launch 직후 즉시 줌을 모두 담당한다.
- assembly mode에서는 기본적으로 orthographic를 사용하고, `Launcher`/`Drop` 파트와 그 owner module/output-port 문맥에서는 perspective를 유지한다.
- `DefaultCameraFocusPolicy`가 focus 타입별 rotation/zoom 기준을 정한다. structure와 output-port는 owner satellite / owner module 규칙을 따르고, 커밋된 공전 상태의 우주선은 일반 우주선 규칙이 아니라 target이 화면 아래로 오도록 별도 회전 규칙을 사용한다.

### 2. Large World / WorldPosition / Scaled Space
- 큰 좌표는 `WorldPosition.worldPosition`과 `Double3`가 진실(source of truth)이다.
- `LargeWorldCoordinator`가 visual origin rebasing과 physics rebasing을 분리해 처리한다.
- 카메라 drag 중에는 camera fallback 기준 rebasing을 늦춰서 사용자가 보는 뷰가 갑자기 꺾이지 않게 한다.
- origin rebase가 일어나면 `OrbitRevolution.SuspendInterpolationForOriginRebase()`로 보간 경로를 재동기화한다.
- transform에서 world 좌표로 역동기화가 필요한 경우 `DynamicWorldPositionSync`를 사용한다.
- `SimulationTierManager` / `SimulationTierTarget`이 거리 기반 `Near` / `Mid` / `Far` tier를 적용한다. tier는 orbit step interval을 낮추고, 설정에 따라 far tier에서 collider / orbit visual / gravity field를 끌 수 있다.
- 현재 focus가 우주선이면 tier target 전체를 `Near`로 강제한다.
- `ScaledSpaceManager`는 `UpdateFocusInfo`를 가진 객체 중 `OrbitRevolution` 또는 `Star`를 가진 대상을 축소 프록시로 만든다. scaled mode에서는 원본 renderer/orbit line을 숨길 수 있고, star는 원본 renderer 경로로 계속 보이게 유지한다.
- orbit-driven 객체를 수정할 때 `transform.position`만 직접 건드리면 jitter와 raycast mismatch가 다시 생긴다. 가능한 한 `WorldPosition`, `OrbitRevolution`, `LargeWorldCoordinator` 경로를 유지해야 한다.

### 3. Celestial / Orbit
- 항성, 행성, 자연위성, 인공위성, 커밋된 우주선 공전은 전부 `OrbitRevolution`이 공전 좌표를 계산한다.
- `OrbitRevolution`은 현재 angle만 가진 단순 스크립트가 아니다. orbit tilt, time multiplier, parent center velocity/acceleration, simulation step interval, render interpolation, origin rebase 후 interpolation reset을 포함한다.
- `OrbitRevolution`은 `TimeManager`에 등록되어 시간 정지/배율의 영향을 직접 받는다.
- `OrbitVisualizer`는 `3600` 샘플 기반으로 orbit line을 만들고, semi-major / semi-minor / tilt를 캐시한다. 커밋된 우주선 orbit line도 같은 보간 경로를 따라가도록 맞춰져 있다.
- `Gravity`는 중력 소스, `GravityField`는 시각화, `GravityAffectedMover`는 이동체 쪽 소비자다.

### 4. Solar System Generation
- `SolarSystemGenerator` 시작 시 한 개의 `Central Star`, 6개의 planet, 자연위성, asteroid belt를 생성한다.
- 모든 planet에는 artificial satellite가 하나씩 자동 생성된다.
- 생성기는 `Resources/Prefabs/System/LargeWorldCoordinatorPrefab`와 `SimulationTierManagerPrefab`을 우선 로드하고, 없으면 런타임 fallback object를 만들어 붙인다.
- 생성 대상 prefab은 `SolarSystemSettings.asset`에서 가져오고, planet/satellite 속성은 `BiomeSettings/*.asset`을 사용한다.

### 5. Artificial Satellite / Assembly / Logistics
- `ArtificialSatellite`는 조립 가능한 위성 루트다. 자신의 본체 mesh/collider는 지속적으로 숨기고, `Launcher` / `Drop` / `Pipe`는 핵심 비주얼을 유지한다.
- 위성은 기존 파트에 `AssemblyPartFocus`를 보장하고, core part에는 `CoreFocusGridUI`를 보장한다.
- `AssemblyManager`는 focus된 artificial satellite / assembly part / output port / structure를 기준으로 조립 대상을 자동 전환한다. focus가 현재 위성 계층 밖으로 빠지면 assembly mode를 정리하고, 다른 위성으로 넘어갔으면 새 source로 다시 활성화한다.
- `Assembly`는 단순 배치 스크립트가 아니라 아래를 함께 관리한다.
  - part / structure 선택
  - runtime output-port graph
  - pipe path 시작점, preview, terminal/end-cap 시각 갱신
  - drag rectangle 기반 selection
  - remove-selection cascade preview
  - structure placement context
  - output-port refresh queue
- `Assembly.PortGraph.cs`는 output-port mapping을 만들고 `AssemblyOutputPortFocus` 메타데이터와 `OutputPortProductionState`를 준비한다.
- `Assembly.RuntimePorts.cs`는 pipe 파트용 runtime port marker를 만들고 `AssemblyOutputPortFocus` clone metadata를 초기화한다.
- `Assembly.PipeEnds.cs`는 pipe terminal replacement / end-cap visuals를 관리한다.
- `AssemblyFocusedPartHighlightPresenter`는 현재 focus된 satellite part에 runtime outline overlay를 붙인다. output port, structure, icon, core grid plane은 하이라이트에서 제외된다.
- `ModuleOutputPortSelectorPresenter`는 focused module 또는 output port 주변에 작은 원형 버튼을 띄워 output-port focus로 직접 내려가게 한다.
- core focus일 때만 `Structures` 버튼이 보이며, `StructureCatalog`가 `Resources/Structures`에서 structure asset을 로딩한다.
- 현재 structure catalog에는 `Core Laboratory`, `Core Logistics Hub`, `Core Power Control`, `Core Residential Facility`, `Small Fabricator`, `Medium Fabricator`, `Large Fabricator`, `Merge Workshop`가 들어 있다.
- `StructureFacilityKind`는 현재 `None`, `Fabricator`, `LogisticsHub`를 사용한다.
- `Core Logistics Hub`는 `CoreLogisticsHubPresenter`와 `StructureResourceInventory`를 사용한다. debug starter resource를 seed하고, 현재 core output-port source inventory의 기본 공급원 역할도 한다.
- processor 계열 part는 `ModulePartInventoryUtility`를 통해 input/output inventory와 `ModuleProcessingState`를 가진다. core/pipe/launcher는 여전히 general inventory만 사용한다.
- `ModuleRecipeCatalog`에 현재 연결된 recipe는 아래 네 묶음이다.
  - `Heater`: crystal -> liquid, liquid -> gas
  - `Cooler`: gas -> liquid, liquid -> crystal
  - `Refiner`: crystal -> ingot
  - `Processor`: `Territe_Ingot` -> `Beam` / `Plate`
- `ModuleProcessingPresenter`는 recipe 선택, power on/off, input/output inventory 상태를 보여준다.
- `ModuleInventoryPresenter`는 focused module, output port, 일반 structure inventory를 보여주고 output-port resource assignment와 run/stop도 여기서 연결된다.
- `OutputPortProductionState`는 파이프 경로를 따라 packet visual을 움직이며 resource를 한 단위씩 전송한다. receiver capacity는 in-flight packet까지 고려해 예약된다.
- `OutputPortTransferUtility`는 sender/receiver inventory와 pipe route를 계산한다. core output-port의 sender inventory는 `Core Logistics Hub` structure inventory에서 찾는다.
- legacy `OutputPortSelectionPresenter`는 의도적으로 inert 상태이며 `FocusFeatureBootstrap`이 있으면 제거된다.
- `Fabricator` facility의 `Manufacture` 버튼은 아직 placeholder 구현이다. 현재는 owner satellite의 `LauncherSpaceshipStock`을 1 증가시키는 최소 동작만 연결되어 있다.

### 6. Spaceship / Launcher / Orbit Assist
- `LauncherSelectionPresenter`는 launcher focus 시 대형 runtime panel을 띄워 hangar ship name, available count, inventory capacity, fuel capacity를 보여준다.
- `LauncherLaunchController`는 발사 시 `LauncherSpaceshipStock`을 1 소비하고, spawn 실패 시 stock을 환불한다.
- `LauncherLaunchUtility`와 `LauncherSpawner`는 owner satellite의 `WorldPosition`을 기반으로 stable launch anchor를 계산한다. 초기 clearance 보정과 host collision ignore도 여기서 처리한다.
- 발사 성공 후 `LauncherLaunchFocusHandler`가 새 우주선으로 focus를 넘기고, `CameraManager.ApplyImmediateLauncherLaunchZoom()`으로 launch-view 줌을 즉시 적용한다.
- 우주선 포커스는 본체가 아니라 `SpaceshipFocusProxy`를 거친다.
- `Spaceship` focus info에는 `Name`, `Category`, `State`, `Fuel`, `Speed`, `Rendezvous`가 포함된다.
- `SpaceshipHUDPresenter`, `SpaceshipTargetPresenter`, `SpaceshipOrbitCommitPresenter`는 모두 runtime canvas UI다.
- `GravityAffectedMover`는 launched state에서 rigidbody 기반 비행이 아니라 world-position 기반 kinematic 적분과 swept celestial collision을 사용한다.
- 우주선이 `Launcher`와 다시 충돌하면 `Success` 로그가 찍히도록 되어 있다.
- `Space`는 이제 hold가 아니라 toggle이다. target과 gravity 문맥이 유효하면 `SpaceshipRendezvousController`가 입력을 우선 예약하고, 그렇지 않을 때만 `TimeControllerUI`가 pause/play 토글로 사용한다.
- 안정적인 공전 후보가 `0.75`초 이상 유지되면 `SpaceshipOrbitCommitPresenter`가 `Orbit` 버튼을 띄운다.
- 버튼을 누르면 현재 상대 공전 값을 `OrbitRevolution`과 `OrbitVisualizer`로 옮기고, `SpaceshipCommittedOrbitRenderSync`를 붙인 뒤 `GravityAffectedMover`를 orbit-driven state로 전환한다.
- commit 이후:
  - 우주선은 공전 진행 방향을 바라본다.
  - 카메라는 target이 화면 아래로 오도록 별도 회전을 사용한다.
  - `W/S`는 공전 반지름, `A/D`는 접선 속도를 조절한다.
  - `A/D` 가감속 방향은 clockwise / counter-clockwise를 고려해 부호가 뒤집힌다.
  - 최소 orbit radius는 target collider/renderer bounds와 padding을 기준으로 계산된다.

### 7. Time / Replay / UI
- `TimeManager`가 play, pause, fast-forward, rewind, replay 상태를 관리한다.
- `BaseTimeRecorder` / `PhysicsTimeRecorder`는 stateful 객체의 위치/회전/속도 히스토리를 기록한다.
- `OrbitRevolution`은 stateless orbiter로 취급되어 시간 상태 변화에 따라 별도 배율/정지가 적용된다.
- `PeriodUI` / `PeriodVisualizer`는 공전 주기와 LCM 기반 주기 시각화를 보여준다.
- `PeriodUI`는 foreground runtime panel 아래로 가도록 `UIDocument.sortingOrder = -1000`을 사용한다.
- assembly / launcher / logistics / module 패널은 대부분 UXML이 아니라 runtime canvas에서 생성되는 presenter UI다.

## 수정할 때 가장 자주 보는 파일
- focus/target/raycast: `Assets/Scripts/Core/UserInput.FocusInput.cs`, `Assets/Scripts/Core/FocusManager.cs`, `Assets/Scripts/Core/DefaultCameraFocusPolicy.cs`, `Assets/Scripts/Core/FocusFeatureBootstrap.cs`
- camera zoom/rotation/origin: `Assets/Scripts/Core/CameraManager.cs`, `Assets/Scripts/Core/CameraManager.FocusAndInput.cs`, `Assets/Scripts/Core/CameraManager.WorldOrigin.cs`, `Assets/Scripts/Core/CameraManager.Collision.cs`
- large-world jitter/tiering: `Assets/Scripts/World/LargeWorld/LargeWorldCoordinator.cs`, `Assets/Scripts/World/LargeWorld/SimulationTierManager.cs`, `Assets/Scripts/World/LargeWorld/SimulationTierTarget.cs`, `Assets/Scripts/World/Celestial/OrbitRevolution.cs`, `Assets/Scripts/Gameplay/Spaceship/SpaceshipCommittedOrbitRenderSync.cs`
- celestial orbit/visual: `Assets/Scripts/World/Celestial/OrbitVisualizer.cs`, `Assets/Scripts/World/Celestial/Gravity.cs`, `Assets/Scripts/World/Celestial/GravityField.cs`, `Assets/Scripts/World/ScaledSpace/ScaledSpaceManager.cs`
- spaceship orbit assist/commit: `Assets/Scripts/Gameplay/Spaceship/SpaceshipRendezvousController.cs`, `Assets/Scripts/Gameplay/Spaceship/SpaceshipOrbitCommitPresenter.cs`, `Assets/Scripts/Gameplay/Spaceship/GravityAffectedMover.cs`, `Assets/Scripts/Gameplay/Spaceship/Spaceship.cs`
- assembly selection/ports/structures: `Assets/Scripts/Gameplay/Assembly/Assembly.cs`, `Assets/Scripts/Gameplay/Assembly/Assembly.SelectionDrag.cs`, `Assets/Scripts/Gameplay/Assembly/Assembly.StructurePlacement.cs`, `Assets/Scripts/Gameplay/Assembly/Assembly.RuntimePorts.cs`, `Assets/Scripts/Gameplay/Assembly/Assembly.PortGraph.cs`, `Assets/Scripts/Gameplay/Assembly/Assembly.PipeEnds.cs`, `Assets/Scripts/Gameplay/Assembly/AssemblyOutputPortFocus.cs`, `Assets/Scripts/Gameplay/Assembly/AssemblyMeshCombiner.cs`
- module/logistics/output-port UI: `Assets/Scripts/Gameplay/Assembly/CoreLogisticsHubPresenter.cs`, `Assets/Scripts/Gameplay/Assembly/ModuleInventoryPresenter.cs`, `Assets/Scripts/Gameplay/Assembly/ModuleProcessingPresenter.cs`, `Assets/Scripts/Gameplay/Assembly/ModuleOutputPortSelectorPresenter.cs`, `Assets/Scripts/Gameplay/Assembly/OutputPortProductionState.cs`, `Assets/Scripts/Gameplay/Assembly/OutputPortTransferUtility.cs`
- launcher stock/panel: `Assets/Scripts/Gameplay/Spaceship/LauncherSelectionPresenter.cs`, `Assets/Scripts/Gameplay/Spaceship/LauncherLaunchController.cs`, `Assets/Scripts/Gameplay/Spaceship/LauncherLaunchUtility.cs`, `Assets/Scripts/Gameplay/Spaceship/LauncherSpaceshipStock.cs`, `Assets/Scripts/Gameplay/Assembly/AssemblyUI.cs`, `Assets/Scripts/Gameplay/Assembly/AssemblyUI.Fabricator.cs`
- generation data: `Assets/Scripts/System/SolarSystem/SolarSystemGenerator.cs`, `Assets/Scripts/System/SolarSystem/SolarSystemFactory.cs`, `Assets/Settings/SolarSystemSettings.asset`, `Assets/Settings/BiomeSettings/*.asset`
- structure/resource data: `Assets/Resources/Structures/*.asset`, `Assets/Resources/Parts/*.asset`, `Assets/Resources/Sprites/*`, `Assets/UI/Assembly.uxml`

## 현재 기대 동작 체크리스트
- focus된 spaceship에서 클릭은 focus 변경이 아니라 target 토글이다.
- artificial satellite도 spaceship target이 될 수 있다.
- `Space`는 rendezvous가 가능한 문맥에서는 공전 보조 토글이고, 그렇지 않을 때만 시간 pause/play 입력으로 내려간다.
- 안정화되면 `Orbit` 버튼이 떠야 한다.
- orbit commit 후 orbit line과 spaceship은 시각적으로 같은 경로를 따라가야 한다.
- focused satellite 내부에서는 structure가 part보다 우선으로 raycast/focus되어야 한다.
- assembly part나 structure를 처음 클릭하면 먼저 owner satellite로 focus가 올라가고, 같은 satellite가 이미 focus된 상태에서 다시 클릭하면 세부 focus로 내려가야 한다.
- output-port selector overlay 버튼을 누르면 `AssemblyOutputPortFocus`로 focus가 내려가고, `FocusParent`로 owner module 또는 satellite로 되돌아갈 수 있어야 한다.
- core focus일 때만 `Structures` 버튼이 떠야 하고, structure 선택 후 core grid 위에 footprint를 맞춰 설치할 수 있어야 한다.
- `Core Logistics Hub` focus 시 logistics panel이 떠야 하고, `Small/Medium/Large Fabricator`와 `Merge Workshop` focus 시 fabricator popup이 떠야 한다.
- 일반 module focus에서는 processing panel과 inventory panel이 상황에 맞게 보여야 하고, output-port focus에서는 resource assignment / run toggle이 보여야 한다.
- core output-port는 `Core Logistics Hub` inventory를 sender source로 사용해야 한다.
- `Fabricator`의 `Manufacture` 버튼은 launcher stock을 1 증가시켜야 한다.
- launcher part focus info와 launcher panel은 현재 `Available Spaceships`를 보여야 하고, stock이 0이면 launch가 막혀야 한다.
- launcher / logistics / module / assembly popup 위에 pointer가 있거나 popup이 열려 있는 동안은 world hover/click raycast가 억제되어야 한다.
- assembly camera는 core/structure/일반 part에서는 orthographic, `Launcher`/`Drop` part에서는 perspective를 유지해야 한다.
- selection mode drag는 사각형 UI를 띄우고, drag 시작/끝 grid 범위를 기준으로 파트를 선택해야 한다.
- 설치가 끝난 part에서도 port mesh와 output-port metadata가 살아 있어야 한다.
- spaceship focus 중에는 simulation tier가 전부 near로 유지되어 orbit assist 대상이 비활성화되지 않아야 한다.

## 주의점
- large-world 계층에서는 `transform.position`만 수정하는 패치를 쉽게 넣지 말 것. `WorldPosition`과 coordinator sync를 깨뜨리면 jitter와 raycast mismatch가 같이 생긴다.
- orbit line과 본체가 조금이라도 어긋나면 `OrbitRevolution` 보간 경로와 `SpaceshipCommittedOrbitRenderSync` / `OrbitVisualizer` 경로가 같은지 먼저 확인할 것.
- assembly port 관련 수정은 `AssemblyPortVisualMarker`, `AssemblyOutputPortFocus`, `AssemblyMeshCombiner` 예외 처리를 같이 봐야 한다.
- core output-port 자원 전달은 core part inventory가 아니라 `Core Logistics Hub` structure inventory를 source로 찾는다.
- legacy `OutputPortSelectionPresenter`는 비활성 placeholder다. output-port UI를 건드릴 때는 `ModuleOutputPortSelectorPresenter`를 수정해야 한다.
- structure/fabricator 쪽은 아직 초기 버전이다. `Manufacture`만 launcher stock 증가에 연결되어 있고, requested count / filter / auto-manufacture는 실제 recipe 생산 로직과 아직 직접 연결되어 있지 않다.
- assembly camera는 focus 타입에 따라 orthographic/perspective가 바뀐다. launcher/drop만 perspective이므로 줌/회전 문제를 볼 때 현재 focus 타입을 먼저 확인할 것.
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
3. Any large-world / orbit / assembly / logistics constraints that must be preserved.

Do not assume direct transform edits are safe for orbit-driven objects.
Check whether the task touches Focus, Camera, WorldPosition, OrbitRevolution, SimulationTier, Assembly runtime ports, Output-port UI, Structure inventories, or Spaceship rendezvous/orbit systems.
```
