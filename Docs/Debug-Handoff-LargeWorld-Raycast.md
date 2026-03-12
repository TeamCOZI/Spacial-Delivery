# Large World / Camera / Raycast Debug Handoff

## 목적

이 문서는 새 세션에서 바로 이어서 디버깅할 수 있도록, 지금까지의 문제 정의, 이미 해결된 항목, 실패한 시도, 사용자 제약사항을 정리한 handoff 문서다.

## 사용자 제약사항

- 기본 `Position Update` 순서는 건드리면 안 된다.
- 이미 위치를 갱신하는 코드가 있는데, 같은 프레임에 같은 성격의 위치 갱신을 한 번 더 추가하는 방식은 피해야 한다.
- `threshold`를 높이거나, 조건 분기로 증상을 숨기는 식의 봉합은 근본 해결책으로 인정되지 않는다.
- 현재 정상인 천체 움직임, 공전선, 중력장 선, launcher 화살표/버튼 등은 다시 깨지면 안 된다.

## 현재까지 해결된 문제

### 1. `focus 없음 + drag` 중 origin recenter 때 화면 오브젝트가 한 번 튀는 문제

원인:
- free-camera drag 중 fallback origin recenter가 같은 상호작용 루프 안에서 발생해, pan delta와 frame-of-reference 변화가 동시에 적용됐다.

해결:
- 우클릭 drag 중에는 no-focus fallback recenter를 미루도록 변경.

관련 파일:
- `Assets/Scripts/World/LargeWorld/LargeWorldCoordinator.cs`
- `Assets/Scripts/Core/UserInput.CameraInput.cs`

### 2. `focus 없음` 상태에서 빠른 우클릭 drag가 손을 못 따라가는 문제

원인:
- `CameraManager.UpdateDragOffset()`에서 drag delta를 매 프레임 `maxDragDeltaPerFrame`으로 clamp하고 있었다.

해결:
- `focus 없음` 상태에서는 clamp를 적용하지 않도록 변경.

관련 파일:
- `Assets/Scripts/Core/CameraManager.FocusAndInput.cs`

### 3. Launcher 화살표 / Launcher Button 떨림

원인:
- launcher visual pose 계산이 stable world anchor와 rendered transform을 섞어 읽고 있었다.

해결:
- launcher 시각 경로는 현재 화면에 보이는 basis transform에서 직접 `TransformPoint / TransformDirection`으로 계산하도록 변경.

관련 파일:
- `Assets/Scripts/Gameplay/Spaceship/LauncherLaunchUtility.cs`

결과:
- 사용자 확인으로 해결됨.

### 4. Launcher / focused satellite part raycast 깜빡임

해결:
- focused artificial satellite part 전용 visual hit 경로를 추가했다.

관련 파일:
- `Assets/Scripts/Core/UserInput.FocusInput.cs`

주의:
- 이 경로는 launcher/part 문제 해결에는 효과가 있었고, 현재 baseline으로 유지되어야 한다.

### 5. 우주선 focus 시 배경 천체가 상대적으로 떠는 문제

원인:
- 우주선은 non-interpolated pose를 쓰고, 배경 천체는 interpolated pose를 쓰면서 시간축이 달랐다.

해결:
- launched spaceship이 focus된 동안에는 해당 우주선의 `Rigidbody.interpolation`만 `Interpolate`로 켜도록 변경.

관련 파일:
- `Assets/Scripts/Gameplay/Spaceship/Spaceship.cs`

결과:
- 사용자 확인으로 해결됨.

## 아직 남은 핵심 문제

### Raycast hit / hover 어긋남

증상 요약:
- `Pause` 상태에서는 문제 없음.
- 객체가 멈춰 있으면 떨림 없이 잘 맞는다.
- 움직이는 동안에만 문제 발생.
- `Canvas Gizmo`가 떨리는 반대 방향으로 raycast hit gizmo가 튄다.
- 객체가 클 때는 덜 보이고, 객체가 작을수록 잘 보인다.
- `Artificial Satellite` focus, `Spaceship` focus:
  - 위치가 튀는 느낌보다 `success / fail` 깜빡임에 가깝다.
- 일반 천체 focus:
  - 노란 hit 선이 `Canvas` 떨림의 반대 방향으로 튄다.

추가 관찰:
- `Icon`은 본체와 같은 위치에 있고, 화면상 크기가 일정하게 보이도록 스케일만 조정된다.
- 줌아웃 시 아이콘이 드러나고, 줌인 시 본체에 가려진다.
- 클릭 로그는 `Icon` 또는 천체 이름으로 정상적으로 찍힌다.
- `ScaledSpaceProxy`는 대략 카메라 Z 기준 `-30000 / -25000` hysteresis로 활성/비활성되는 것으로 보인다.

### 중요 해석

현재까지의 관찰상, 문제는 높은 확률로 아래 둘 중 하나다.

1. 사용자가 실제로 보는 최종 렌더 pose와 `Physics.RaycastAll`이 읽는 pose가 서로 다른 시간축에 있다.
2. `focus 종류별`로 다른 경로를 타는 객체들이 동일한 raycast 기준을 공유하지 못한다.

## Focus별 구조 차이

### 일반 천체 (`Star`, `Planet`, `Satellite`)

- 보통 `SphereCollider` 사용.
- `OrbitRevolution` + `WorldPosition` + large-world 경로 사용.
- 일부 구간에서는 `ScaledSpaceProxy`가 개입 가능.
- `UserInput` raycast mask에는 `small scale layer`도 포함되어 있다.

### `Artificial Satellite`

- 일반 천체처럼 root는 large-world 경로를 타지만,
- focus / camera / input은 owner satellite 및 `AssemblyPartFocus` 기반의 특수 경로를 탄다.
- launcher 문제를 해결하기 위해 focused satellite part 전용 visual raycast 경로가 이미 들어가 있다.

### `Spaceship`

- `GravityAffectedMover` 기반.
- focus는 실제 본체가 아니라 `SpaceshipFocusProxy`를 경유할 수 있다.
- launched 상태에서 focus 중이면 `Spaceship.cs`에서 interpolation을 켠 상태다.

## 실패한 시도들

아래 시도들은 효과가 없었고, 다시 기본 해법으로 반복 제안하지 않는 것이 좋다.

### 시도 A. `LargeWorldCoordinator.LateUpdate()`에서 우주선 focus 시 orbit-driven 루트까지 추가 sync

의도:
- 우주선 / 배경 천체의 렌더 pose를 강제로 같은 시점으로 맞추기.

결과:
- 사용자 기준 변화 없음.
- 사용자 요청에 따라 원복.

### 시도 B. `UserInput.FocusInput`에서 focused object 전체 hierarchy에 대한 visual hit 확장

의도:
- physics hit 대신 rendered transform 기준 visual hit를 일반 천체 / 우주선 / 위성에도 넓게 사용하기.

결과:
- 사용자 기준 변화 없음.
- 원복.

### 시도 C. `FocusHover()`를 `Canvas.willRenderCanvases`에서 실행

의도:
- 프레임 마지막에 raycast 해서 실제 화면에 그려질 pose 기준으로 맞추기.

결과:
- 변화 없음.
- 원복.

### 시도 D. `FocusHover()`를 `RenderPipelineManager.beginCameraRendering`에서 실행

의도:
- main camera 렌더 직전 `Physics.SyncTransforms()` 후 hit를 계산하기.

결과:
- 변화 없음.
- 원복.

### 시도 E. 현재 프레임에 보이는 collider pose를 직접 raycast 하는 global visual-scene hit

의도:
- `Physics.RaycastAll`을 fallback으로만 두고, collider transform 기준 custom visual raycast를 우선 사용하기.

결과:
- 아직 사용자 검증 전.
- 현재 worktree에 남아 있는 마지막 미검증 변경이다.

관련 파일:
- `Assets/Scripts/Core/UserInput.FocusInput.cs`

## 현재 worktree 상태

현재 `git status` 기준 수정 파일:

- `Assets/Scripts/Core/UserInput.FocusInput.cs`
- `Assets/Scripts/Core/UserInput.cs`

주의:
- `Assets/Scripts/Core/UserInput.cs`는 사실상 의미 있는 변경이 거의 없고, 마지막 render hook 시도 원복 과정의 흔적 수준이다.
- `Assets/Scripts/Core/UserInput.FocusInput.cs`에는 마지막으로 시도한 global visual-scene hit 코드가 남아 있다.
- 새 세션에서 깨끗한 baseline으로 시작하려면, 이 마지막 시도는 먼저 검토하거나 필요 시 원복하는 것이 좋다.

## 새 세션에서 바로 이어갈 때의 권장 시작점

### 먼저 유지해야 할 상태

다음 수정들은 유지 전제로 진행하는 것이 좋다.

- no-focus drag 중 fallback recenter defer
- no-focus free drag unclamped
- launcher visual pose 안정화
- focused satellite part 전용 visual raycast
- focused launched spaceship interpolation 활성화

### 다음으로 유력한 조사 포인트

1. `Pause` 상태에서는 완전히 정상이라는 점을 기준으로, 문제를 "시간축 불일치"로 한정해서 본다.
2. 일반 천체 / 우주선 / artificial satellite 각각에 대해
   - 화면에 보이는 renderer 중심
   - collider 중심
   - `Physics.RaycastAll` hit point
   - `ScreenPointToRay`의 ray
   의 차이를 같은 프레임에서 로그/디버그로 비교한다.
3. 특히 일반 천체는 `ScaledSpaceProxy`와 `source` 중 어느 쪽이 실제 hit source인지 분리해서 본다.
4. `Icon`이 아니라 본체 collider를 직접 클릭했을 때도 동일한 오차가 재현되는지 확인한다.

### 피해야 할 방향

- origin recenter threshold 조정
- extra sync pass 추가
- local position을 또 덮어쓰는 방식
- 전체 collider를 임의로 매 프레임 teleport하는 방식

## 새 세션에 전달할 한 줄 요약

`Pause` 시 문제 없음 + 움직일 때만 raycast/hit gizmo가 어긋남 + focus 종류별 증상이 다름 => large-world나 threshold보다 "최종 렌더 pose와 raycast pose의 시간축 불일치"를 focus별 경로에서 분리해서 보는 쪽이 맞다.
