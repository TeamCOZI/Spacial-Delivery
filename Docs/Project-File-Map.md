# Project File Map

## 범위
- 이 문서는 `Scripts` 외의 프로젝트 파일을 중심으로 구조를 정리한 인덱스다.
- `Assets/Scripts`의 file-by-file 역할은 이미 `Docs/Scripts-Directory-Guide.md`에 정리되어 있으므로, 이 문서에서는 그 문서를 companion index로 간주한다.
- 목적은 새 세션이 의미 있는 파일부터 열고, 생성/런타임 데이터가 어디에 있는지 빠르게 찾게 만드는 것이다.

## 의도적으로 축약한 범위
- `.meta` 파일: Unity GUID 메타데이터이므로 per-file 설명을 생략.
- `Library/`, `Logs/`, `Temp/`, `UserSettings/`: 생성물이라 생략.
- `Assets/TextMesh Pro/`, `Assets/UI Toolkit/`: 대부분 Unity 기본 import 자산이라 디렉터리 단위로만 요약.
- `.codex-dotnet-home/`: Codex 로컬 캐시 성격이므로 생략.

## 루트 디렉터리
- `Assets/`: 게임 소스, 씬, prefab, scriptable asset, UI, 테스트.
- `Docs/`: 프로젝트 이해를 위한 문서.
- `Packages/`: Unity package manifest/lock.
- `ProjectSettings/`: Unity 프로젝트 설정.
- `.vscode/`: 로컬 VS Code 워크스페이스 설정.
- `Library/`, `Logs/`, `Temp/`, `UserSettings/`: Unity 생성 디렉터리.
- `UIElementsSchema/`: UI Toolkit schema 파일 위치.

## 루트 파일
- `README.md`: 현재는 제목만 있는 최소 루트 문서.
- `PART_SPEC.md`: 새 Assembly Part 추가 시 사용하는 표준 스펙 문서.

## .vscode
- `.vscode/settings.json`: Unity 프로젝트용 파일 숨김, YAML association, solution 지정.
- `.vscode/launch.json`: `Attach to Unity` 디버그 설정.
- `.vscode/extensions.json`: `visualstudiotoolsforunity.vstuc` 추천 목록.

## Docs
- `Docs/AI-Session-Bootstrap.md`: 새 세션 첫 진입 문서. 큰 그림, 현재 기대 동작, 고위험 수정 포인트 정리.
- `Docs/Project-File-Map.md`: 이 문서. 스크립트 밖의 프로젝트 자산/설정 인덱스.
- `Docs/Scripts-Directory-Guide.md`: `Assets/Scripts` 전체 file-by-file 가이드.
- `Docs/Debug-Handoff-LargeWorld-Raycast.md`: large-world/camera/raycast 이슈 히스토리. 현재 인코딩이 깨져 있어 참고용으로만 사용.

## Packages
- `Packages/manifest.json`: 주요 패키지 선언. 현재 핵심은 `com.unity.inputsystem`, `com.unity.render-pipelines.universal`, `com.unity.ai.navigation`, `com.unity.probuilder`, `com.unity.test-framework`, `com.unity.ugui`.
- `Packages/packages-lock.json`: 패키지 해상 결과 lock 파일.
- `ProjectSettings/Packages/com.unity.probuilder/Settings.json`: ProBuilder 패키지별 설정.

## ProjectSettings
- `ProjectSettings/ProjectVersion.txt`: Unity 버전 `6000.2.4f1`.
- `ProjectSettings/EditorBuildSettings.asset`: 빌드 씬은 `Assets/Scenes/SampleScene.unity` 하나.
- `ProjectSettings/ProjectSettings.asset`: 제품명 `Spacial Delivery`, 번들 버전, player 전반 설정.
- `ProjectSettings/GraphicsSettings.asset`: 커스텀 렌더 파이프라인 자산 연결. 현재 URP 사용.
- `ProjectSettings/URPProjectSettings.asset`: URP 전역 설정.
- `ProjectSettings/QualitySettings.asset`: 품질 프리셋.
- `ProjectSettings/TimeManager.asset`: Unity 시간 스텝 설정.
- `ProjectSettings/DynamicsManager.asset`: 3D Physics 설정. 글로벌 중력은 `0,0,0`, `AutoSyncTransforms`는 꺼져 있다.
- `ProjectSettings/Physics2DSettings.asset`: 2D Physics 설정.
- `ProjectSettings/TagManager.asset`: 레이어 정의. 커스텀 레이어로 `Assembly`, `SmallScale`가 중요하다.
- `ProjectSettings/AudioManager.asset`: 오디오 설정.
- `ProjectSettings/ClusterInputManager.asset`: 입력 클러스터 설정.
- `ProjectSettings/EditorSettings.asset`: 에디터 공통 설정.
- `ProjectSettings/InputManager.asset`: 레거시 입력 설정. 실제 런타임은 새 Input System을 사용한다.
- `ProjectSettings/MemorySettings.asset`: 메모리 관련 설정.
- `ProjectSettings/MultiplayerManager.asset`: 멀티플레이어 관련 기본 설정.
- `ProjectSettings/NavMeshAreas.asset`: 네비메시 영역 설정.
- `ProjectSettings/PackageManagerSettings.asset`: 패키지 매니저 설정.
- `ProjectSettings/PresetManager.asset`: 프리셋 관리 설정.
- `ProjectSettings/SceneTemplateSettings.json`: 씬 템플릿 설정.
- `ProjectSettings/ShaderGraphSettings.asset`: Shader Graph 설정.
- `ProjectSettings/UnityConnectSettings.asset`: Unity Services 연결 정보.
- `ProjectSettings/VFXManager.asset`: VFX Graph 설정.
- `ProjectSettings/VersionControlSettings.asset`: 버전 관리 연동 설정.
- `ProjectSettings/XRSettings.asset`: XR 설정.

## Assets 진입점
- `Assets/Scenes/SampleScene.unity`: 유일한 플레이 씬. 루트로 `WorldOriginManager`, `SpaceScaleManager`, `Main Camera`, `Solar System Generator`, `Focus Manager`, `Assembly Manager`, `UI`, `TimeManager`, `LauncherLaunchSystem`, `EventSystem`가 배치되어 있다.
- `Assets/InputSystem_Actions.inputactions`: `Player` / `UI` 액션맵 정의. `Move`, `Look`, `Attack`, `Interact`, `Previous`, `Next`, `FocusParent`, `AssemblyMode` 등이 핵심.

## Assets/Scripts
- `Assets/Scripts/**`: 전체 script file map은 `Docs/Scripts-Directory-Guide.md`를 참조.
- 특히 큰 영향도가 높은 하위 디렉터리는 `Core`, `Gameplay/Assembly`, `Gameplay/Spaceship`, `World/Celestial`, `World/LargeWorld`, `System/SolarSystem`이다.

## Assets/Prefabs

### 천체 / 이동체 / 시스템 대상 prefab
- `Assets/Prefabs/StarPrefab.prefab`: `Star`, `Gravity`, `GravityField`, `WorldPosition`, `SimulationTierTarget`가 붙은 항성 기본 prefab.
- `Assets/Prefabs/PlanetPrefab.prefab`: 행성 기본 prefab. `OrbitRevolution`과 `OrbitVisualizer`를 포함.
- `Assets/Prefabs/SatellitePrefab.prefab`: 자연위성 기본 prefab. 행성과 동일한 large-world/orbit 계열 구성.
- `Assets/Prefabs/ArtificialSatellitePrefab.prefab`: 조립 가능한 인공위성 루트 prefab. `ArtificialSatellite`, `AssemblyAttachmentHub`, `AssemblyMeshCombiner`, `Gravity`, `OrbitRevolution`, `OrbitVisualizer`, `WorldPosition` 포함.
- `Assets/Prefabs/AsteroidBeltPrefab.prefab`: 소행성 벨트 시각 표현용 prefab.
- `Assets/Prefabs/SpaceshipPrefab.prefab`: `Spaceship`, `GravityAffectedMover`, `SpaceshipFlightController`, `SpaceshipFuel`, `WorldPosition`, `Icon`을 가진 우주선 prefab.

### 조립 파트 본체 prefab
- `Assets/Prefabs/CorePrefab.prefab`: 조립 코어 본체. 코어 고정 포트 레이아웃의 기준.
- `Assets/Prefabs/AssemblerPrefab.prefab`: 세로 1x2 계열 Processor 파트.
- `Assets/Prefabs/CoolerPrefab.prefab`: 1x1 Processor 파트.
- `Assets/Prefabs/HeaterPrefab.prefab`: 1x1 Processor 파트.
- `Assets/Prefabs/ManufacturerPrefab.prefab`: 세로 1x3 Processor 파트.
- `Assets/Prefabs/MergerPrefab.prefab`: 세로 1x4 Processor 파트.
- `Assets/Prefabs/MolderPrefab.prefab`: 1x1 Processor 파트.
- `Assets/Prefabs/PipePrefab.prefab`: 1x1 Pipe 파트.
- `Assets/Prefabs/ProcessorPrefab.prefab`: 1x1 Processor 파트.
- `Assets/Prefabs/RefinerPrefab.prefab`: 1x1 Processor 파트.
- `Assets/Prefabs/LauncherPrefab.prefab`: 런처 파트 본체. 우주선 스폰 기준점이 된다.
- `Assets/Prefabs/OutputPortPrefab.prefab`: runtime port clone의 시각 템플릿.
- `Assets/Prefabs/DropPrefab.prefab`: 드롭/부품 결과물 쪽 보조 prefab.

### 조립 고스트 prefab
- `Assets/Prefabs/AssemblerGhostPrefab.prefab`: Assembler 고스트.
- `Assets/Prefabs/CoolerGhostPrefab.prefab`: Cooler 고스트.
- `Assets/Prefabs/CoreGhostPrefab.prefab`: Core 고스트.
- `Assets/Prefabs/HeaterGhostPrefab.prefab`: Heater 고스트.
- `Assets/Prefabs/ManufacturerGhostPrefab.prefab`: Manufacturer 고스트.
- `Assets/Prefabs/MergerGhostPrefab.prefab`: Merger 고스트.
- `Assets/Prefabs/MolderGhostPrefab.prefab`: Molder 고스트.
- `Assets/Prefabs/PipeGhostPrefab.prefab`: Pipe 고스트.
- `Assets/Prefabs/ProcessorGhostPrefab.prefab`: Processor 고스트.
- `Assets/Prefabs/RefinerGhostPrefab.prefab`: Refiner 고스트.

## Assets/Resources

### Resources/Prefabs/System
- `Assets/Resources/Prefabs/System/LargeWorldCoordinatorPrefab.prefab`: `LargeWorldCoordinator` 기본 설정 prefab.
- `Assets/Resources/Prefabs/System/SimulationTierManagerPrefab.prefab`: `SimulationTierManager` 기본 설정 prefab.
- `Assets/Resources/Prefabs/System/ScaledSpaceProxyPrefab.prefab`: scaled-space proxy mesh/collider 루트 prefab.

### Resources/Parts
아래 `Part` ScriptableObject들은 `PartDB`가 `Resources.LoadAll<Part>("Parts")`로 로딩한다.

- `Assets/Resources/Parts/Core.asset`: 코어 파트 정의. `partType=Core`, `grid=9x9`, `mass=8100`.
- `Assets/Resources/Parts/Launcher.asset`: 런처 파트 정의. 발사 시스템이 이름과 part asset을 통해 식별한다.
- `Assets/Resources/Parts/Pipe.asset`: `partType=Pipe`, `grid=1x1`, `mass=50`.
- `Assets/Resources/Parts/Assembler.asset`: `partType=Processor`, `grid=1x2`, `durability=200`, `inventory=3`, `mass=200`.
- `Assets/Resources/Parts/Cooler.asset`: `partType=Processor`, `grid=1x1`, `inventory=2`, `mass=100`.
- `Assets/Resources/Parts/Heater.asset`: `partType=Processor`, `grid=1x1`, `inventory=2`, `mass=100`.
- `Assets/Resources/Parts/Manufacturer.asset`: `partType=Processor`, `grid=1x3`, `inventory=4`, `mass=300`.
- `Assets/Resources/Parts/Merger.asset`: `partType=Processor`, `grid=1x4`, `inventory=5`, `mass=400`.
- `Assets/Resources/Parts/Molder.asset`: `partType=Processor`, `grid=1x1`, `inventory=2`, `mass=100`.
- `Assets/Resources/Parts/Processor.asset`: `partType=Processor`, `grid=1x1`, `inventory=2`, `mass=100`.
- `Assets/Resources/Parts/Refiner.asset`: `partType=Processor`, `grid=1x1`, `inventory=2`, `mass=100`.

## Assets/Settings

### 생성 / 렌더링 공통 asset
- `Assets/Settings/SolarSystemSettings.asset`: star/planet/satellite/artificial satellite prefab 참조와 생성 반경, orbit 증가량, biome preset 참조를 보관.
- `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset`: URP 전역 설정.
- `Assets/Settings/PC_RPAsset.asset`: PC용 URP pipeline asset.
- `Assets/Settings/PC_Renderer.asset`: PC용 renderer data.
- `Assets/Settings/Mobile_RPAsset.asset`: Mobile용 URP pipeline asset.
- `Assets/Settings/Mobile_Renderer.asset`: Mobile용 renderer data.
- `Assets/Settings/DefaultVolumeProfile.asset`: 기본 post-processing / volume profile.
- `Assets/Settings/SampleSceneProfile.asset`: 샘플 씬용 볼륨 프로파일. Bloom, Tonemapping, Vignette 등이 들어 있다.

### BiomeSettings
각 파일은 `BiomeSettings` ScriptableObject 하나이며 color, scale, mass, gravityRadius, heat/atm, 자원 구성을 정의한다.

- `Assets/Settings/BiomeSettings/Alpine.asset`: 작은 냉대계 행성 preset.
- `Assets/Settings/BiomeSettings/Antartic.asset`: 큰 냉대/빙설 행성 preset.
- `Assets/Settings/BiomeSettings/Desert.asset`: 건조 행성 preset.
- `Assets/Settings/BiomeSettings/Green House.asset`: 온실형 행성 preset.
- `Assets/Settings/BiomeSettings/Lava.asset`: 용암 행성 preset.
- `Assets/Settings/BiomeSettings/Ocean.asset`: 해양 행성 preset.
- `Assets/Settings/BiomeSettings/Temperate.asset`: 지구형 온대 행성 preset.
- `Assets/Settings/BiomeSettings/Tropics.asset`: 열대 행성 preset.
- `Assets/Settings/BiomeSettings/Glacier.asset`: 빙하 행성 preset.
- `Assets/Settings/BiomeSettings/Iron.asset`: 철질 행성 preset.
- `Assets/Settings/BiomeSettings/Gas Giant.asset`: 가스 거대행성 preset.
- `Assets/Settings/BiomeSettings/Ice Giant.asset`: 얼음 거대행성 preset.
- `Assets/Settings/BiomeSettings/Glacier Satellite.asset`: 빙하 위성 preset.
- `Assets/Settings/BiomeSettings/Iron Satellite.asset`: 철질 위성 preset.

## Assets/UI
- `Assets/UI/Assembly.uxml`: 조립 UI 레이아웃. `OpenParts`, `ToggleSelection`, `RemoveSelected` 버튼과 popup 구조를 정의.
- `Assets/UI/PartsList.uxml`: 파트 목록 한 칸 템플릿. `PartIcon` + `PartName`.
- `Assets/UI/Period.uxml`: 주기 progress bar UI 레이아웃.
- `Assets/UI/Period.uss`: `Period.uxml` progress bar 높이/스타일.
- `Assets/UI/Period.asset`: `PanelSettings` asset. `Period.uxml` 계열 UI 렌더 설정.

## Assets/Materials
- `Assets/Materials/Grid.shader`: 조립 그리드 시각화용 커스텀 shader.
- `Assets/Materials/Outline.shader`: 아웃라인/선택 강조용 커스텀 shader.
- `Assets/Materials/OutlineMaterial.mat`: `Outline.shader`를 쓰는 아웃라인 material.
- `Assets/Materials/GhostMaterial.mat`: 조립 ghost body용 material.
- `Assets/Materials/InputPortMaterial.mat`: input port 시각 material.
- `Assets/Materials/OutputPortMaterial.mat`: output port 시각 material.
- `Assets/Materials/RangeMaterial.mat`: 범위/가이드용 material.
- `Assets/Materials/StarMaterial.mat`: star 본체용 material.
- `Assets/Materials/VolumetricLightMaterial.mat`: star light / volumetric 계열 material.
- `Assets/Materials/AssemblerMaterial.mat`, `CoolerMaterial.mat`, `HeaterMaterial.mat`, `ManufacturerMaterial.mat`, `MergerMaterial.mat`, `MolderMaterial.mat`, `ProcessorMaterial.mat`, `RefinerMaterial.mat`: 각 조립 파트 본체 표면 material.

## Assets/Tests
- `Assets/Tests/EditMode/AssemblyMathUtilityTests.cs`: quarter-turn 회전, 셀 quantize, path reconstruction 같은 순수 유틸 테스트.
- `Assets/Tests/EditMode/AssemblyPortConnectivityTests.cs`: `AssemblyPartPortLayout` 포트 매핑과 `AssemblyPort` 점유 상태 테스트.

## Assets/Sprites
- `Assets/Sprites/Launcher.fbx`: 런처 모델 리소스.
- `Assets/Sprites/Device_Test.fbx`: 테스트용 디바이스 모델.
- `Assets/Sprites/Untitled.fbx`: 임시/실험 모델로 보이는 FBX 자산.

## Unity 기본 import 디렉터리

### Assets/TextMesh Pro
- 역할: TextMesh Pro 기본 폰트, shader, sprite atlas, TMP settings.
- 새 세션에서는 보통 무시해도 된다. UI 폰트/텍스트 렌더를 만질 때만 본다.
- 포함 파일 묶음:
  - `Fonts/LiberationSans.ttf`, `Fonts/DungGeunMo.ttf`, `Fonts/LiberationSans - OFL.txt`
  - `Resources/TMP Settings.asset`, `Resources/Style Sheets/Default Style Sheet.asset`
  - `Resources/Fonts & Materials/LiberationSans SDF.asset`, `LiberationSans SDF - Outline.mat`, `LiberationSans SDF - Fallback.asset`, `LiberationSans SDF - Drop Shadow.mat`, `DungGeunMo SDF.asset`
  - `Resources/Sprite Assets/EmojiOne.asset`
  - `Resources/LineBreaking Leading Characters.txt`, `Resources/LineBreaking Following Characters.txt`
  - `Sprites/EmojiOne.png`, `Sprites/EmojiOne.json`, `Sprites/EmojiOne Attribution.txt`
  - `Shaders/TMP_Sprite.shader`, `TMP_SDF.shader`, `TMP_SDF-Surface.shader`, `TMP_SDF-Surface-Mobile.shader`, `TMP_SDF-Mobile.shader`, `TMP_SDF-Mobile-2-Pass.shader`, `TMP_SDF-Mobile SSD.shader`, `TMP_SDF-Mobile Overlay.shader`, `TMP_SDF-Mobile Masking.shader`, `TMP_SDF SSD.shader`, `TMP_SDF Overlay.shader`, `TMP_Bitmap.shader`, `TMP_Bitmap-Mobile.shader`, `TMP_Bitmap-Custom-Atlas.shader`, `TMP_SDF-URP Unlit.shadergraph`, `TMP_SDF-URP Lit.shadergraph`, `TMP_SDF-HDRP UNLIT.shadergraph`, `TMP_SDF-HDRP LIT.shadergraph`, `TMPro.cginc`, `TMPro_Mobile.cginc`, `TMPro_Properties.cginc`, `TMPro_Surface.cginc`, `SDFFunctions.hlsl`

### Assets/UI Toolkit
- 역할: Unity 기본 런타임 theme.
- 포함 파일: `Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss`

## 무엇을 먼저 열어야 하는가
- 코드 수정 전 구조 파악: `Docs/AI-Session-Bootstrap.md`
- 코드 파일 위치 찾기: `Docs/Scripts-Directory-Guide.md`
- prefab / asset / scene / settings 위치 찾기: `Docs/Project-File-Map.md`
- 파트 추가 작업: `PART_SPEC.md`
