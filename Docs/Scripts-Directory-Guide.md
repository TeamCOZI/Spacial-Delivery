# Scripts 디렉터리 가이드

## 목적
- `Assets/Scripts` 전체를 빠르게 훑으면서 어느 시스템을 어디서 수정해야 하는지 찾기 위한 참조 문서.
- 새 세션에서는 이 문서와 `Docs/AI-Session-Bootstrap.md`, `Docs/Project-File-Map.md`를 같이 읽는 것을 전제로 한다.

## 디렉터리 요약
- `Core`: 입력, 포커스, 카메라, 월드 원점, 전역 서비스 접근.
- `Gameplay/Artificial`: 인공위성 루트와 위성 내부 게임플레이 컴포넌트.
- `Gameplay/Assembly`: 조립 모드 전체. 배치, 포트, 파이프, 제거, 선택, structure 설치, logistics, module processing, output-port UI까지 포함한다.
- `Gameplay/Spaceship`: 우주선 발사, launcher panel/stock, 중력 이동, 타깃 지정, 공전 보조, orbit commit, HUD.
- `Gameplay` 루트: 게임플레이 서비스 접근 헬퍼.
- `Rendering`: 아이콘, 오버레이 카메라, 레이어 유틸.
- `Shared`: 범용 유틸, 설정, 스케일, 풀링.
- `System/SolarSystem`: 절차적 태양계 생성.
- `System/Time`: 시간 상태, 기록, 리플레이.
- `UI`: 주기 UI, 시간 UI, 연료 패널 UI.
- `World/Celestial`: 천체 본체, 중력, 공전, 궤도 시각화.
- `World/LargeWorld`: double 좌표 기반 large-world 관리.
- `World/ScaledSpace`: 먼 거리용 축소 프록시.

## Core
- `CameraFocusPolicy.cs`: 현재 카메라 포커스 정책 진입점. 실제 계산은 정책 객체에 위임한다.
- `CameraManager.cs`: 카메라 기본 상태, 줌/추적 상태, 입력 구독, assembly orthographic/perspective projection blend, 전체 이동 루프를 담당한다.
- `CameraManager.Collision.cs`: 카메라-천체/표면 충돌 보정, 최소 거리 확보, collider 기반 clipping 처리.
- `CameraManager.FocusAndInput.cs`: 포커스 전환, drag/zoom, gravity-field 줌 전환, structure/output-port parent focus 처리, launcher launch 즉시 줌, 현재 focus 뷰 reset을 담당한다.
- `CameraManager.WorldOrigin.cs`: world origin 이동 시 카메라와 focus 기준점 동기화.
- `CoreRuntimeAccess.cs`: 핵심 singleton/service 접근 헬퍼.
- `DefaultCameraFocusPolicy.cs`: 기본 포커스 rotation/zoom 판단 규칙. structure와 output-port는 owner satellite/module 기준 scale을 사용하고, 커밋된 우주선 공전 카메라 회전도 여기서 처리한다.
- `FocusEventSubscriber.cs`: 포커스 이벤트 구독 베이스 클래스.
- `FocusFeatureBootstrap.cs`: launcher, spaceship HUD/target/orbit presenter, logistics/module presenter, focused part highlight를 보장하고 legacy `OutputPortSelectionPresenter`를 제거한다.
- `FocusManager.cs`: 현재 focus/hover 관리, 포커스 정보 텍스트 업데이트, focus change 이벤트 전파.
- `ICameraFocusPolicy.cs`: 카메라 포커스 정책 인터페이스.
- `IFocusService.cs`: 포커스 서비스 인터페이스.
- `IInputService.cs`: 입력 이벤트 서비스 인터페이스.
- `UserInput.CameraInput.cs`: 마우스 drag/휠 입력 처리.
- `UserInput.cs`: 입력 액션 연결 및 입력 이벤트 발행 루트.
- `UserInput.FocusInput.cs`: 포커스/hover/타깃 지정/조립 모드 전환 입력 처리. structure 우선 raycast, focused satellite 내부 visual hit, `AssemblyOutputPortFocus`, panel/popup에 따른 world input 억제, spaceship target 토글, `C`/`X` 단축키까지 포함한다.
- `WorldOriginManager.cs`: 월드 시프트 이벤트 발행 및 루트 이동.

## Gameplay/Artificial
- `ArtificialSatellite.cs`: 인공위성 런타임 루트. 본체 mesh/collider를 숨기고, 핵심 파트 비주얼을 유지하며, `AssemblyPartFocus`와 `CoreFocusGridUI`를 보장한다.
- `ArtificialSatelliteBlueprint.cs`: 인공위성 청사진/설정 데이터 자리.
- `Docking.cs`: 도킹 포인트 방향/활성 상태 마커.
- `Durability.cs`: 충돌 기반 내구도 상태.
- `Inventory.cs`: 단순 인벤토리 상태 컨테이너.

## Gameplay/Assembly
- `Assembly.cs`: 조립 세션 핵심. 활성화/비활성화, part 선택, structure 선택, placement/apply/cancel, 제거, selection 상태, output-port refresh까지 포함한다.
- `Assembly.CameraMode.cs`: 조립 모드 카메라 전환 헬퍼.
- `Assembly.Debugging.cs`: 포트/셀/hover 상태 디버그 로그 유틸.
- `Assembly.GhostVisuals.cs`: part/structure ghost 비주얼 색상/투명도 처리.
- `Assembly.PipeCorner.cs`: 파이프 코너 생성과 경로 고스트 생성.
- `Assembly.PipeEnds.cs`: pipe terminal replacement와 end-cap visual 갱신을 담당한다.
- `Assembly.PlacementAndPath.cs`: 그리드 좌표 변환, 셀 점유, 경로 탐색 유틸.
- `Assembly.PortGraph.cs`: output-port mapping, pipe connectivity cache, `AssemblyOutputPortFocus` 메타데이터, `OutputPortProductionState` 준비를 담당하는 포트 연결성 핵심.
- `Assembly.RuntimePorts.cs`: 런타임 포트 비주얼/레이아웃 생성. pipe part용 runtime marker 생성과 output-port clone metadata reset을 처리한다.
- `Assembly.SelectionDrag.cs`: 드래그 사각형 UI와 그리드 기반 다중 선택. 선택 highlight 색과 fill/outline visual도 여기서 만든다.
- `Assembly.StructurePlacement.cs`: core surface grid 위 structure ghost, footprint occupancy, 설치, `StructureFocus`/label 생성, logistics hub inventory 초기화 진입점을 처리한다.
- `AssemblyAttachmentHub.cs`: 위성 하위 조립물 로컬 고정 및 동기화.
- `AssemblyCorePortLayout.cs`: core 출력 포트 레이아웃 정의.
- `AssemblyFocusHighlightMarker.cs`: focused part highlight overlay 식별용 runtime marker.
- `AssemblyFocusedPartHighlightPresenter.cs`: 현재 focus된 satellite part에 outline overlay를 생성한다. output port, structure, icon, core grid plane은 제외한다.
- `AssemblyGhostMarker.cs`: 고스트 오브젝트 식별 마커.
- `AssemblyManager.cs`: 조립 모드 오케스트레이션. focus된 satellite/part/output port/structure에 맞춰 조립 대상을 자동 전환한다.
- `AssemblyMathUtility.cs`: 회전/셀/경로 유틸 함수.
- `AssemblyMeshCombiner.cs`: 결합 메쉬 재생성 로직. `AssemblyPortVisualMarker`와 runtime port visual 예외 처리 포함.
- `AssemblyOutputPortFocus.cs`: output-port focus 정보 provider. owner satellite/module, side label, cell mapping, close-focus 해석을 제공한다.
- `AssemblyPartFocus.cs`: 조립 파트 포커스 정보 provider. launcher part에서는 available spaceship stock도 노출한다.
- `AssemblyPartPortLayout.cs`: 파트별 포트 레이아웃 데이터.
- `AssemblyPartPortProfile.cs`: 입력 포트 프로필 데이터.
- `AssemblyPort.cs`: 포트 타입/점유 상태 런타임 데이터.
- `AssemblyPortPulse.cs`: 포트 강조 펄스 이펙트.
- `AssemblyPortVisualMarker.cs`: 포트 비주얼 런타임 마커.
- `AssemblyUI.cs`: 조립 UI Toolkit 컨트롤러. parts popup, structures popup, selection 버튼, tooltip, fabricator popup visibility를 관리한다. core focus일 때만 structures popup을 연다.
- `AssemblyUI.Fabricator.cs`: fabricator facility UI 상태, 필터/수량 버튼, manufacture 버튼의 launcher stock 증가 훅을 담당한다.
- `CoreFocusGridUI.cs`: core focus 시에만 보이는 `CoreFocusGridPlane`을 생성한다.
- `CoreLogisticsHubPresenter.cs`: `Core Logistics Hub` 전용 runtime panel. 아이템 목록, capacity, 생산/소모 수치, 필터, pointer-over world input block을 담당한다.
- `CraftRecipe.cs`: structure crafting recipe 직렬화 컨테이너.
- `InventoryResourceType.cs`: resource enum과 `InventoryResourceCatalog`를 정의한다. resource icon은 `Resources/Sprites/*`에서 로드한다.
- `ModuleInventoryPresenter.cs`: focused module, focused output port, 일반 structure inventory를 보여주는 runtime panel. output-port resource assignment와 run/stop도 여기서 처리한다.
- `ModuleOutputPortSelectorPresenter.cs`: focused module/output port 주변에 원형 버튼 overlay를 띄워 output-port focus로 직접 이동시킨다.
- `ModulePartInventoryUtility.cs`: module part에 general/input/output inventory와 processing state를 붙이는 헬퍼. core/pipe/launcher는 일반 inventory만 유지한다.
- `ModuleProcessingPresenter.cs`: module recipe 선택, power on/off, input/output inventory 상태를 보여주는 runtime panel.
- `ModuleProcessingState.cs`: 모듈별 processing timer, recipe, power 상태, 입력 소비와 출력 생산, 상태 라벨(`OFF`, `NO RECIPE`, `NO INPUT`, `OUTPUT FULL`, `RUNNING`)을 관리한다.
- `ModuleRecipeCatalog.cs`: Heater/Cooler/Refiner/Processor의 현재 recipe 정의를 제공한다.
- `OutputPortProductionState.cs`: output-port 단위 전송 상태. 파이프 경로를 따라 packet visual을 이동시키고 receiver capacity를 예약하며 자원을 한 단위씩 전달한다.
- `OutputPortProductionUtility.cs`: output-port source inventory/pipe path 관련 보조 유틸. 현재 직접 참조는 거의 없어 헬퍼 성격이 강하다.
- `OutputPortSelectionPresenter.cs`: legacy selector. 의도적으로 inert 상태이며 새 시스템에서는 `ModuleOutputPortSelectorPresenter`가 대체한다.
- `OutputPortTransferUtility.cs`: sender/receiver inventory 해석, route 계산, core logistics hub inventory 연결을 담당한다.
- `Part.cs`: 파트 정의 ScriptableObject. prefab/grid/mass/durability/inventory와 `inputCapacity` / `outputCapacity`를 가진다. mass는 prefab scale 또는 grid footprint로 자동 계산된다.
- `PartDB.cs`: `Resources/Parts` 로딩 및 파트 조회 DB.
- `Structure.cs`: structure 정의 ScriptableObject. footprint, mass/durability/capacity/power, craftRecipe, facilityKind를 가진다.
- `StructureCatalog.cs`: `Resources/Structures` 로딩/정렬/조회 catalog.
- `StructureFacilityKind.cs`: structure facility UI 유형 enum. 현재 `None`, `Fabricator`, `LogisticsHub`를 사용한다.
- `StructureFocus.cs`: 설치된 structure의 focus info provider. owner satellite 연결과 fabricator/logistics UI 판별을 담당한다.
- `StructureInstance.cs`: 설치된 structure의 source asset과 center cell을 보관한다.
- `StructureResourceInventory.cs`: structure 또는 part의 자원 inventory. `General`, `Input`, `Output` kind를 지원하고 logistics hub용 debug starter inventory seed도 담당한다.

## Gameplay/Spaceship
- `CaptureRangeHandler.cs`: 포획 범위 트리거 처리.
- `GravityAffectedMover.cs`: 중력/충돌/이동 통합 처리. launched state의 world-position 기반 kinematic 적분, swept celestial collision, orbit-driven state 전환까지 담당한다.
- `LauncherDirectionGuide.cs`: 발사 방향 라인/화살표 렌더링.
- `LauncherLaunchController.cs`: 발사 요청 처리 및 발사 실행. launcher stock consume/refund를 포함한다.
- `LauncherLaunchFocusHandler.cs`: 발사 성공 시 생성 우주선으로 focus를 옮기고 launch-view 줌을 즉시 적용한다.
- `LauncherLaunchUtility.cs`: 런처 앵커/방향 계산 유틸. large-world에서 owner satellite의 `WorldPosition`을 기준으로 stable anchor를 계산한다.
- `LauncherSelectionPresenter.cs`: launcher focus 시 생성되는 대형 panel. hangar/inventory/fuel 표시, launch gating, pointer-over 시 world input block을 담당한다.
- `LauncherSpaceshipStock.cs`: owner satellite별 발사 가능 spaceship 수량 상태 컨테이너. fabricator manufacture와 launcher 발사가 이 값을 공유한다.
- `LauncherSpawner.cs`: stable launcher anchor 기준 실제 우주선을 스폰하고 초기 clearance 보정과 host collision ignore를 수행한다.
- `Spaceship.cs`: 우주선 파사드. proxy 보장, rendezvous controller 보장, focus 프리젠테이션, orbit-committed 시각 상태, focus info 조합을 담당한다.
- `SpaceshipCommittedOrbitRenderSync.cs`: 커밋된 공전 상태에서 우주선 렌더 위치/회전을 `OrbitRevolution`의 보간값과 정확히 맞춘다.
- `SpaceshipFlightController.cs`: 입력/연료 기반 추진 제어. 수동 입력과 자동 가속 요청을 합산한다.
- `SpaceshipFocusProxy.cs`: 우주선 자체 대신 포커스/레이캐스트에 노출되는 프록시 객체 관리.
- `SpaceshipFocusUtility.cs`: focus proxy, 실제 spaceship, focus target 사이의 변환 유틸.
- `SpaceshipFuel.cs`: 연료 상태와 업데이트 이벤트.
- `SpaceshipHUDPresenter.cs`: 우주선 연료 HUD 프리젠터.
- `SpaceshipOrbitCommitPresenter.cs`: 안정된 공전이 만들어지면 `Orbit` 버튼을 띄우고 commit 클릭을 처리한다.
- `SpaceshipRendezvousController.cs`: 현재 target 기준 공전 보조의 핵심. `Space` 토글, 안정화 판정, orbit commit, 커밋 후 `W/S` 반지름 조절, `A/D` 접선 속도 조절, 카메라 회전 기준 제공까지 담당한다.
- `SpaceshipRendezvousUtility.cs`: 공전 보조에 필요한 target/속도/가속도/world position 문맥 계산 유틸.
- `SpaceshipState.cs`: 우주선 상태 enum.
- `SpaceshipTargetPresenter.cs`: 현재 target 이름을 월드 스크린 위치에 맞춰 표시하는 UI 프리젠터.

## Gameplay (root)
- `GameplayRuntimeAccess.cs`: 게임플레이 singleton/service 접근 헬퍼.

## Rendering
- `Icon.cs`: 런타임 아이콘 렌더링, hover/focus 반응, 표시/확장 규칙. focused artificial satellite 또는 그 내부 focus 문맥에서는 아이콘을 숨긴다.
- `SmallScaleLayerUtility.cs`: small-scale / overlay 레이어 적용 유틸.
- `SmallScaleOverlayCamera.cs`: 오버레이 카메라 구성, clip range 조절, URP camera stack 동기화.

## Shared
- `ComponentUtility.cs`: `GetOrAddComponent` 헬퍼.
- `GameSettings.cs`: 전역 게임 설정 자리.
- `ObjectPool.cs`: 오브젝트 풀 유틸.
- `RuntimeHierarchyOrganizer.cs`: 런타임 계층 정리 유틸.
- `Utility.cs`: 범용 유틸 함수(`UpdateFocusInfo`, 정규분포, 소인수분해 등).
- `WorldScale.cs`: 월드 스케일 변환 유틸.

## System/SolarSystem
- `BiomeSettings.cs`: 바이옴 설정 데이터. 색, 스케일, 질량, 중력 반경, 자원 구성을 포함한다.
- `SolarSystemFactory.cs`: 항성/행성/자연위성/소행성대 생성 팩토리.
- `SolarSystemGenerator.cs`: 절차적 태양계 생성 오케스트레이션. `LargeWorldCoordinator`와 `SimulationTierManager` bootstrap, 모든 행성에 인공위성을 하나씩 붙이는 현재 구성도 여기서 결정된다.
- `SolarSystemSettings.cs`: 생성기 설정 ScriptableObject.

## System/Time
- `BaseTimeRecorder.cs`: 타임라인 기록 베이스 클래스.
- `LaunchData.cs`: 발사 기록 데이터(위치/속도/추력 히스토리 구조체).
- `PathVisualizer.cs`: 경로 시각화.
- `PhysicsTimeRecorder.cs`: 물리 타임라인 기록/리와인드/리플레이.
- `TimeManager.cs`: 시간 재생/일시정지/배속/되감기 루트 컨트롤러.

## UI
- `PeriodUI.cs`: 현재 focus 계층의 공전 주기 정보 UI. `UIDocument.sortingOrder = -1000`으로 가장 뒤에 깔리고 pointer 입력도 무시한다.
- `PeriodVisualizer.cs`: 여러 공전 주기의 LCM 기반 시각화 UI.
- `SpaceshipFuelPanelFactory.cs`: 우주선 연료 패널 런타임 생성 팩토리.
- `TimeControllerUI.cs`: 시간 제어 UI 버튼 컨트롤러. 단, rendezvous 문맥에서는 `Space` 입력을 우주선 쪽에 양보한다.
- `UIRootLocator.cs`: 공용 메인 캔버스 탐색/캐시.

## World/Celestial
- `AsteroidBelt.cs`: 소행성 벨트 런타임 표현.
- `CelestialBody.cs`: 천체 공통 마커 인터페이스 역할.
- `Gravity.cs`: 중력 소스 컴포넌트.
- `GravityField.cs`: 중력장 시각 요소.
- `OrbitRevolution.cs`: 공전 업데이트 본체. world position 계산, orbit tilt, 보간, parent orbit 전파, velocity/acceleration 조회, simulation step interval, origin rebase interpolation reset을 가진다.
- `OrbitVisualizer.cs`: 궤도 라인 시각화. `OrbitRevolution` 샘플을 따라가며 커밋된 우주선 orbit도 같은 보간 경로로 그린다.
- `Planet.cs`: 행성 런타임 동작.
- `Satellite.cs`: 자연위성 런타임 동작.
- `Star.cs`: 항성 런타임 동작.
- `StarLight.cs`: 항성 광원/빛 관련 동작.

## World/LargeWorld
- `Double3.cs`: double 정밀도 벡터 타입.
- `DynamicWorldPositionSync.cs`: local transform과 `WorldPosition` 사이의 동기화를 담당한다.
- `LargeWorldCoordinator.cs`: world origin rebase, local/world 변환, tracked `WorldPosition` 동기화, visual/physics 분리 rebasing, camera-drag 중 재배치 지연을 담당한다.
- `SimulationTier.cs`: 시뮬레이션 티어 enum.
- `SimulationTierManager.cs`: 거리 기반 tier 할당 매니저. hysteresis를 가지며 spaceship focus 중에는 전체 tier target을 near로 강제한다.
- `SimulationTierTarget.cs`: tier 대상 마커/설정. tier별 orbit step interval, far-tier collider/visual helper 비활성 옵션을 가진다.
- `WorldPosition.cs`: 절대 월드 좌표 컨테이너.

## World/ScaledSpace
- `ScaledSpaceManager.cs`: 축소공간 프록시 생성/토글/갱신. orbit 객체와 star를 대상으로 프록시를 만들고, scaled mode에서 원본 renderer/orbit line 가시성도 제어한다.
- `ScaledSpaceProxyTarget.cs`: 프록시와 원본 트랜스폼 매핑.
