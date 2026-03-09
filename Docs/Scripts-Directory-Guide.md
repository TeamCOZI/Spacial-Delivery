# Scripts 디렉터리 가이드

## 목적
- 현재 `Assets/Scripts` 디렉터리 구조와 각 스크립트의 책임을 빠르게 파악하기 위한 참조 문서.

## 디렉터리 요약
- `Core`: 입력, 포커스, 카메라, 월드 원점 같은 전역 런타임 루트.
- `Gameplay/Artificial`: 인공위성/도킹/인벤토리/내구도 등 인공 객체 게임플레이 구성요소.
- `Gameplay/Assembly`: 조립 모드 전체(배치, 포트 그래프, 파이프 경로, UI 연동).
- `Gameplay/Spaceship`: 우주선 발사/비행/연료/HUD.
- `Rendering`: 아이콘/오버레이 카메라/레이어 보조 렌더링.
- `Shared`: 공용 유틸/설정/풀링/스케일.
- `System/SolarSystem`: 태양계 생성/팩토리/설정.
- `System/Time`: 시간 제어/기록 데이터/경로 시각화.
- `UI`: UI 컨트롤러/프리젠터/팩토리.
- `World/Celestial`: 천체 런타임(중력, 공전, 천체 본체).
- `World/LargeWorld`: 더블 정밀도 좌표 및 시뮬레이션 티어.
- `World/ScaledSpace`: 축소공간 프록시 시스템.

## Core
- `CameraFocusPolicy.cs`: 카메라 포커스 정책 진입점(현재 정책 객체로 위임).
- `CameraManager.cs`: 카메라 기본 상태, 줌/추적 상태, 입력 구독 수명주기 관리.
- `CameraManager.FocusAndInput.cs`: 포커스 전환, 드래그/줌 처리.
- `CameraManager.WorldOrigin.cs`: 월드 원점 이동 시 카메라 동기화.
- `CoreRuntimeAccess.cs`: 핵심 싱글톤/서비스 접근 헬퍼.
- `DefaultCameraFocusPolicy.cs`: 기본 포커스 회전/줌 판단 규칙.
- `FocusEventSubscriber.cs`: 포커스 이벤트 구독 베이스 클래스.
- `FocusFeatureBootstrap.cs`: 포커스 관련 기능 의존성 부트스트랩.
- `FocusManager.cs`: 현재 포커스/호버 관리 및 포커스 정보 UI 업데이트.
- `ICameraFocusPolicy.cs`: 카메라 포커스 정책 인터페이스.
- `IFocusService.cs`: 포커스 서비스 인터페이스.
- `IInputService.cs`: 입력 이벤트 서비스 인터페이스.
- `UserInput.CameraInput.cs`: 마우스 드래그/휠 입력 처리.
- `UserInput.cs`: 입력 액션 연결 및 입력 이벤트 발행 루트.
- `UserInput.FocusInput.cs`: 포커스/호버/조립 모드 전환 입력 처리.
- `WorldOriginManager.cs`: 월드 시프트 이벤트 관리.

## Gameplay/Artificial
- `ArtificialSatellite.cs`: 인공위성 런타임 루트 및 조립 연동.
- `ArtificialSatelliteBlueprint.cs`: 인공위성 청사진/설정 데이터.
- `Docking.cs`: 도킹 포인트 방향/활성 상태 마커.
- `Durability.cs`: 내구도 상태.
- `Inventory.cs`: 인벤토리 상태 컨테이너.

## Gameplay/Assembly
- `Assembly.cs`: 조립 세션 핵심 흐름(활성화/선택/적용/취소).
- `Assembly.CameraMode.cs`: 조립 모드 카메라 전환 헬퍼.
- `Assembly.Debugging.cs`: 포트/셀 상태 디버그 로그 유틸.
- `Assembly.GhostVisuals.cs`: 고스트 비주얼 색상/투명도 처리.
- `Assembly.PipeCorner.cs`: 파이프 코너 생성 및 경로 고스트 생성.
- `Assembly.PlacementAndPath.cs`: 그리드 좌표 변환, 점유, 경로 탐색.
- `Assembly.PortGraph.cs`: 출력 포트 수집 및 포트 그래프/셀 매핑 구축.
- `Assembly.RuntimePorts.cs`: 런타임 포트 비주얼/레이아웃 생성.
- `AssemblyAttachmentHub.cs`: 하위 조립물 로컬 고정 및 동기화.
- `AssemblyCorePortLayout.cs`: 코어 출력 포트 레이아웃 정의.
- `AssemblyGhostMarker.cs`: 고스트 오브젝트 식별 마커.
- `AssemblyManager.cs`: 조립 모드 오케스트레이션.
- `AssemblyMathUtility.cs`: 회전/셀/경로 유틸 함수.
- `AssemblyMeshCombiner.cs`: 결합 메쉬 재생성 로직.
- `AssemblyPartFocus.cs`: 조립 파트 포커스 정보 제공.
- `AssemblyPartPortLayout.cs`: 파트별 포트 레이아웃 데이터.
- `AssemblyPartPortProfile.cs`: 입력 포트 프로필 데이터.
- `AssemblyPort.cs`: 포트 타입/점유 상태 런타임 데이터.
- `AssemblyPortPulse.cs`: 포트 강조 펄스 이펙트.
- `AssemblyPortVisualMarker.cs`: 포트 비주얼 런타임 마커.
- `AssemblyUI.cs`: 조립 UI Toolkit 컨트롤러.
- `CoreFocusGridUI.cs`: 코어 포커스 그리드 UI 보조.
- `Part.cs`: 파트 정의 데이터.
- `PartDB.cs`: 파트 조회/DB.

## Gameplay/Spaceship
- `CaptureRangeHandler.cs`: 포획 범위 트리거 처리.
- `GravityAffectedMover.cs`: 중력/충돌/이동 통합 처리.
- `LauncherDirectionGuide.cs`: 발사 방향 라인/화살표 렌더링.
- `LauncherLaunchController.cs`: 발사 요청 처리 및 발사 실행.
- `LauncherLaunchFocusHandler.cs`: 발사 성공 시 생성 우주선으로 포커스 이동.
- `LauncherLaunchUtility.cs`: 런처 앵커/방향 계산 유틸.
- `LauncherSelectionPresenter.cs`: 신규 명칭 런처 선택 프리젠터.
- `LauncherSpawner.cs`: 실제 스폰/속도 적용/충돌 무시/아이콘 설정.
- `Spaceship.cs`: 우주선 파사드 및 상태 전이.
- `SpaceshipFlightController.cs`: 입력/연료 기반 추진 제어.
- `SpaceshipFuel.cs`: 연료 상태 및 업데이트 이벤트.
- `SpaceshipHUDPresenter.cs`: 우주선 연료 HUD 프리젠터.
- `SpaceshipState.cs`: 우주선 상태 enum.

## Gameplay (root)
- `GameplayRuntimeAccess.cs`: 게임플레이 싱글톤/서비스 접근 헬퍼.

## Rendering
- `Icon.cs`: 런타임 아이콘 렌더링 및 호버 반응.
- `SmallScaleLayerUtility.cs`: 소규모/오버레이 레이어 유틸.
- `SmallScaleOverlayCamera.cs`: 오버레이 카메라 구성/동기화.

## Shared
- `ComponentUtility.cs`: `GetOrAddComponent` 헬퍼.
- `GameSettings.cs`: 전역 게임 설정.
- `ObjectPool.cs`: 오브젝트 풀 유틸.
- `RuntimeHierarchyOrganizer.cs`: 런타임 계층 정리 유틸.
- `Utility.cs`: 공용 범용 유틸 함수.
- `WorldScale.cs`: 월드 스케일 변환 유틸.

## System/SolarSystem
- `BiomeSettings.cs`: 바이옴 설정 데이터.
- `SolarSystemFactory.cs`: 천체 팩토리 생성 로직.
- `SolarSystemGenerator.cs`: 절차적 태양계 생성 오케스트레이션.
- `SolarSystemSettings.cs`: 생성기 설정.

## System/Time
- `BaseTimeRecorder.cs`: 타임라인 기록 베이스 클래스.
- `LaunchData.cs`: 발사 기록 데이터(위치/속도/추력 히스토리).
- `PathVisualizer.cs`: 경로 시각화.
- `PhysicsTimeRecorder.cs`: 물리 타임라인 기록.
- `TimeManager.cs`: 시간 재생/일시정지/배속 루트 컨트롤러.

## UI
- `PeriodUI.cs`: 주기 정보 표시 UI.
- `PeriodVisualizer.cs`: 주기 그래프 시각화 UI.
- `SpaceshipFuelPanelFactory.cs`: 우주선 연료 패널 런타임 생성 팩토리.
- `TimeControllerUI.cs`: 시간 제어 UI 컨트롤러.
- `UIRootLocator.cs`: 공용 메인 캔버스 탐색/캐시.

## World/Celestial
- `AsteroidBelt.cs`: 소행성 벨트 런타임 표현.
- `CelestialBody.cs`: 천체 공통 베이스 동작.
- `Gravity.cs`: 중력 소스 컴포넌트.
- `GravityField.cs`: 중력장 시각 요소.
- `OrbitRevolution.cs`: 공전 업데이트 동작.
- `OrbitVisualizer.cs`: 궤도 라인 시각화.
- `Planet.cs`: 행성 런타임 동작.
- `Satellite.cs`: 위성 런타임 동작.
- `Star.cs`: 항성 런타임 동작.
- `StarLight.cs`: 항성 광원 동작.

## World/LargeWorld
- `Double3.cs`: 더블 정밀도 벡터 타입.
- `DynamicWorldPositionSync.cs`: 월드 좌표와 트랜스폼 동기화.
- `LargeWorldCoordinator.cs`: 월드 원점/로컬 좌표 변환 코디네이터.
- `SimulationTier.cs`: 시뮬레이션 티어 타입.
- `SimulationTierManager.cs`: 거리 기반 티어 할당 매니저.
- `SimulationTierTarget.cs`: 티어 대상 마커/설정.
- `WorldPosition.cs`: 절대 월드 좌표 컨테이너.

## World/ScaledSpace
- `ScaledSpaceManager.cs`: 축소공간 프록시 생명주기 관리.
- `ScaledSpaceProxyTarget.cs`: 프록시와 원본 트랜스폼 매핑.
