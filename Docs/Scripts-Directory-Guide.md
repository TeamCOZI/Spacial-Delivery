# Scripts 디렉터리 가이드

## 목적
- `Assets/Scripts` 전체를 빠르게 훑으면서 어느 시스템을 어디서 수정해야 하는지 찾기 위한 참조 문서.
- 새 세션에서는 이 문서와 `Docs/AI-Session-Bootstrap.md`, `Docs/Project-File-Map.md`를 같이 읽는 것을 전제로 한다.

## 디렉터리 요약
- `Core`: 입력, 포커스, 카메라, 월드 원점, 전역 서비스 접근.
- `Gameplay/Artificial`: 인공위성 루트와 위성 내부 게임플레이 컴포넌트.
- `Gameplay/Assembly`: 조립 모드 전체(배치, 포트, 파이프, 제거, 선택, UI 연동).
- `Gameplay/Spaceship`: 우주선 발사, 중력 이동, 타깃 지정, 공전 보조, Orbit commit, HUD.
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
- `CameraFocusPolicy.cs`: 현재 카메라 포커스 정책 진입점. 실제 계산은 정책 객체에 위임.
- `CameraManager.cs`: 카메라 기본 상태, 줌/추적 상태, 입력 구독, 전체 이동 루프.
- `CameraManager.Collision.cs`: 카메라-천체/표면 충돌 보정, 최소 거리 확보, collider 기반 클리핑 처리.
- `CameraManager.FocusAndInput.cs`: 포커스 전환, 드래그/줌, 중력장 줌 전환, 포커스 회전 추적.
- `CameraManager.WorldOrigin.cs`: 월드 원점 이동 시 카메라와 포커스 기준점 동기화.
- `CoreRuntimeAccess.cs`: 핵심 싱글톤/서비스 접근 헬퍼.
- `DefaultCameraFocusPolicy.cs`: 기본 포커스 회전/줌 판단 규칙. 커밋된 우주선 공전 카메라 회전도 여기서 우선 처리.
- `FocusEventSubscriber.cs`: 포커스 이벤트 구독 베이스 클래스.
- `FocusFeatureBootstrap.cs`: 포커스 관련 UI/프리젠터 의존성 부트스트랩.
- `FocusManager.cs`: 현재 포커스/호버 관리, 포커스 정보 텍스트 업데이트.
- `ICameraFocusPolicy.cs`: 카메라 포커스 정책 인터페이스.
- `IFocusService.cs`: 포커스 서비스 인터페이스.
- `IInputService.cs`: 입력 이벤트 서비스 인터페이스.
- `UserInput.CameraInput.cs`: 마우스 드래그/휠 입력 처리.
- `UserInput.cs`: 입력 액션 연결 및 입력 이벤트 발행 루트.
- `UserInput.FocusInput.cs`: 포커스/호버/타깃 지정/조립 모드 전환 입력 처리. 우주선 포커스 중에는 클릭이 focus 변경이 아니라 target 토글로 들어간다.
- `WorldOriginManager.cs`: 월드 시프트 이벤트 발행 및 루트 이동.

## Gameplay/Artificial
- `ArtificialSatellite.cs`: 인공위성 런타임 루트. 메쉬 결합, 포트/파트 가시성, 포커스 정보 갱신.
- `ArtificialSatelliteBlueprint.cs`: 인공위성 청사진/설정 데이터 자리.
- `Docking.cs`: 도킹 포인트 방향/활성 상태 마커.
- `Durability.cs`: 충돌 기반 내구도 상태.
- `Inventory.cs`: 인벤토리 상태 컨테이너.

## Gameplay/Assembly
- `Assembly.cs`: 조립 세션 핵심. 활성화/비활성화, 파트 선택, 배치, 적용, 취소, 제거, 선택 하이라이트까지 포함.
- `Assembly.CameraMode.cs`: 조립 모드 카메라 전환 헬퍼.
- `Assembly.Debugging.cs`: 포트/셀/호버 상태 디버그 로그 유틸.
- `Assembly.GhostVisuals.cs`: 고스트 비주얼 색상/투명도 처리.
- `Assembly.PipeCorner.cs`: 파이프 코너 생성과 경로 고스트 생성.
- `Assembly.PlacementAndPath.cs`: 그리드 좌표 변환, 셀 점유, 경로 탐색.
- `Assembly.PortGraph.cs`: 출력 포트 그래프와 cell+side 캐시를 구축하는 포트 연결성 핵심.
- `Assembly.RuntimePorts.cs`: 런타임 포트 비주얼/레이아웃 생성. 설치 완료된 파트에서도 포트 비주얼을 유지하도록 수정된 상태.
- `Assembly.SelectionDrag.cs`: 드래그 사각형 UI와 그리드 기반 다중 선택. 시작 grid와 끝 grid 사이에 1픽셀이라도 닿는 셀의 파트를 선택하는 방식.
- `AssemblyAttachmentHub.cs`: 위성 하위 조립물 로컬 고정 및 동기화.
- `AssemblyCorePortLayout.cs`: 코어 출력 포트 레이아웃 정의.
- `AssemblyGhostMarker.cs`: 고스트 오브젝트 식별 마커.
- `AssemblyManager.cs`: 조립 모드 오케스트레이션.
- `AssemblyMathUtility.cs`: 회전/셀/경로 유틸 함수.
- `AssemblyMeshCombiner.cs`: 결합 메쉬 재생성 로직. 포트 비주얼 예외 처리 포함.
- `AssemblyPartFocus.cs`: 조립 파트 포커스 정보 제공.
- `AssemblyPartPortLayout.cs`: 파트별 포트 레이아웃 데이터.
- `AssemblyPartPortProfile.cs`: 입력 포트 프로필 데이터.
- `AssemblyPort.cs`: 포트 타입/점유 상태 런타임 데이터.
- `AssemblyPortPulse.cs`: 포트 강조 펄스 이펙트.
- `AssemblyPortVisualMarker.cs`: 포트 비주얼 런타임 마커.
- `AssemblyUI.cs`: 조립 UI Toolkit 컨트롤러.
- `CoreFocusGridUI.cs`: 코어 포커스 그리드 UI 보조.
- `Part.cs`: 파트 정의 ScriptableObject. prefab/grid/mass/durability/inventory를 가진다.
- `PartDB.cs`: `Resources/Parts` 로딩 및 파트 조회 DB.

## Gameplay/Spaceship
- `CaptureRangeHandler.cs`: 포획 범위 트리거 처리.
- `GravityAffectedMover.cs`: 중력/충돌/이동 통합 처리. 발사 상태와 Orbit-driven 상태 전환도 여기서 관리.
- `LauncherDirectionGuide.cs`: 발사 방향 라인/화살표 렌더링.
- `LauncherLaunchController.cs`: 발사 요청 처리 및 발사 실행.
- `LauncherLaunchFocusHandler.cs`: 발사 성공 시 생성 우주선으로 포커스 이동.
- `LauncherLaunchUtility.cs`: 런처 앵커/방향 계산 유틸.
- `LauncherSelectionPresenter.cs`: 런처 선택 프리젠터.
- `LauncherSpawner.cs`: 실제 우주선 스폰, 초기 속도 적용, 충돌 무시, 아이콘 설정.
- `Spaceship.cs`: 우주선 파사드. 상태 전이, 포커스 프리젠테이션, target 보유, focus info 조합.
- `SpaceshipCommittedOrbitRenderSync.cs`: 커밋된 공전 상태에서 우주선 렌더 위치/회전을 `OrbitRevolution`의 보간값과 정확히 맞추는 렌더 동기화 컴포넌트.
- `SpaceshipFlightController.cs`: 입력/연료 기반 추진 제어. 수동 입력과 자동 가속 요청을 합산한다.
- `SpaceshipFocusProxy.cs`: 우주선 자체 대신 포커스/레이캐스트에 노출되는 프록시 객체 관리.
- `SpaceshipFocusUtility.cs`: focus proxy, 실제 spaceship, focus target 사이의 변환 유틸.
- `SpaceshipFuel.cs`: 연료 상태와 업데이트 이벤트.
- `SpaceshipHUDPresenter.cs`: 우주선 연료 HUD 프리젠터.
- `SpaceshipOrbitCommitPresenter.cs`: 안정된 공전이 만들어지면 `Orbit` 버튼을 띄우고 commit 클릭을 처리.
- `SpaceshipRendezvousController.cs`: 현재 target 기준 공전 보조의 핵심. `Space` 토글, 안정화 판정, Orbit commit, 커밋 후 WASD 반지름/속도 제어, 공전 진행 방향 회전까지 담당.
- `SpaceshipRendezvousUtility.cs`: 공전 보조에 필요한 target/속도/가속도/world position 문맥 계산 유틸.
- `SpaceshipState.cs`: 우주선 상태 enum.
- `SpaceshipTargetPresenter.cs`: 현재 target 이름을 월드 스크린 위치에 맞춰 표시하는 UI 프리젠터.

## Gameplay (root)
- `GameplayRuntimeAccess.cs`: 게임플레이 싱글톤/서비스 접근 헬퍼.

## Rendering
- `Icon.cs`: 런타임 아이콘 렌더링, hover/focus 반응, 표시/확장 규칙.
- `SmallScaleLayerUtility.cs`: 소규모/오버레이 레이어 유틸.
- `SmallScaleOverlayCamera.cs`: 오버레이 카메라 구성 및 메인 카메라 동기화.

## Shared
- `ComponentUtility.cs`: `GetOrAddComponent` 헬퍼.
- `GameSettings.cs`: 전역 게임 설정 자리.
- `ObjectPool.cs`: 오브젝트 풀 유틸.
- `RuntimeHierarchyOrganizer.cs`: 런타임 계층 정리 유틸.
- `Utility.cs`: 범용 유틸 함수(`UpdateFocusInfo`, 정규분포, 소인수분해 등).
- `WorldScale.cs`: 월드 스케일 변환 유틸.

## System/SolarSystem
- `BiomeSettings.cs`: 바이옴 설정 데이터. 색, 스케일, 질량, 중력 반경, 자원 구성을 포함.
- `SolarSystemFactory.cs`: 항성/행성/자연위성/소행성대 생성 팩토리.
- `SolarSystemGenerator.cs`: 절차적 태양계 생성 오케스트레이션. 모든 행성에 인공위성을 하나씩 붙이는 현재 구성도 여기서 결정된다.
- `SolarSystemSettings.cs`: 생성기 설정 ScriptableObject.

## System/Time
- `BaseTimeRecorder.cs`: 타임라인 기록 베이스 클래스.
- `LaunchData.cs`: 발사 기록 데이터(위치/속도/추력 히스토리 구조체).
- `PathVisualizer.cs`: 경로 시각화.
- `PhysicsTimeRecorder.cs`: 물리 타임라인 기록/리와인드/리플레이.
- `TimeManager.cs`: 시간 재생/일시정지/배속/되감기 루트 컨트롤러.

## UI
- `PeriodUI.cs`: 현재 focus 계층의 공전 주기 정보 UI.
- `PeriodVisualizer.cs`: 여러 공전 주기의 LCM 기반 시각화 UI.
- `SpaceshipFuelPanelFactory.cs`: 우주선 연료 패널 런타임 생성 팩토리.
- `TimeControllerUI.cs`: 시간 제어 UI 버튼 컨트롤러.
- `UIRootLocator.cs`: 공용 메인 캔버스 탐색/캐시.

## World/Celestial
- `AsteroidBelt.cs`: 소행성 벨트 런타임 표현.
- `CelestialBody.cs`: 천체 공통 마커 인터페이스 역할.
- `Gravity.cs`: 중력 소스 컴포넌트.
- `GravityField.cs`: 중력장 시각 요소.
- `OrbitRevolution.cs`: 공전 업데이트 본체. world position 계산, 보간, parent orbit 전파, velocity/acceleration 조회를 가진다.
- `OrbitVisualizer.cs`: 궤도 라인 시각화. 현재는 `OrbitRevolution` 보간 샘플을 따라가도록 맞춰져 있다.
- `Planet.cs`: 행성 런타임 동작.
- `Satellite.cs`: 자연위성 런타임 동작.
- `Star.cs`: 항성 런타임 동작.
- `StarLight.cs`: 항성 광원/빛 관련 동작.

## World/LargeWorld
- `Double3.cs`: double 정밀도 벡터 타입.
- `DynamicWorldPositionSync.cs`: 월드 좌표와 트랜스폼 동기화.
- `LargeWorldCoordinator.cs`: 월드 원점 리베이스, local/world 변환, tracked `WorldPosition` 동기화.
- `SimulationTier.cs`: 시뮬레이션 티어 타입.
- `SimulationTierManager.cs`: 거리 기반 티어 할당 매니저.
- `SimulationTierTarget.cs`: 티어 대상 마커/설정.
- `WorldPosition.cs`: 절대 월드 좌표 컨테이너.

## World/ScaledSpace
- `ScaledSpaceManager.cs`: 축소공간 프록시 생성/토글/갱신.
- `ScaledSpaceProxyTarget.cs`: 프록시와 원본 트랜스폼 매핑.
