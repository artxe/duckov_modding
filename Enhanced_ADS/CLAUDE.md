# CLAUDE.md

## Build

```
dotnet build
```

Post-build copies DLL/assets to `$(DuckovPath)\Mods\Enhanced_ADS\` — hardcoded in [Enhanced_ADS.csproj](Enhanced_ADS.csproj).

---

## Non-obvious constraints

### Coding convention

Follow the local Duckov modding convention.

- Use `small_snake_case` for helper methods, fields, parameters, and local variables.
- Keep Unity/Harmony entry points with the names required by the framework, such as `Awake`, `OnDestroy`, `Prefix`, and `Postfix`.
- Harmony patch classes may keep the `Type__Method` shape.
- Reflection cache variables use `I_` plus the original member name for both fields and methods, such as `I_inputAimPoint`, `I_gunState`, and `I_ProcessMousePosViaRecoil`.
- Do not rename API members, named arguments, serialized/private game member strings, or Harmony special parameters such as `__instance` and `__result`.

### ProcessMousePosViaRecoil
Applies recoil offset to `prev_aim_pos` — does **not** add `mouseDelta`. `mouseDelta` is added separately per mode.

### Auto reload

`ItemAgent_Gun__TransToEmpty` only replaces the game's transition while `State.auto_reload` is enabled. When disabled, its prefix must return `true` so the vanilla empty-magazine behavior runs unchanged. Keep the persisted setting enabled by default for backward compatibility.

### Adaptive_Sensitivity camera_offset
`camera_offset` is **linear** in `(aim_pos - center)`:

```
world_edge_x = screen_offset_to_world(edge_x, 0).x
world_edge_y = screen_offset_to_world(0, edge_y).y
kx = (max_range - world_edge_x) / edge_x
ky = (max_range - world_edge_y) / edge_y
camera_offset = new Vector2(kx * (aim_pos.x - center.x), ky * (aim_pos.y - center.y))
```

`edge = (center.x - 15, center.y - 15)`. `State.delta` lerps 0→1 with `Mathf.MoveTowards(..., Time.deltaTime * gun.AdsSpeed)`, matching the gun's original ADS speed (`AdsSpeed = 1 / ADSTime`). delta=0: ADS 진입 첫 프레임(카메라 패닝 없음). delta=1: 풀 카메라 패닝 활성.

**ADS entry (delta == 0)**: cursor snaps to the natural ADS position for the pre-ADS aim, preserving world aim:
```
aim_pos = new Vector2(
    center.x + (aim_pos.x - center.x) * world_edge_x / max_range,
    center.y + (aim_pos.y - center.y) * world_edge_y / max_range
)
```
Proof: `(cx+kx) * snap_offset.x = (max_range/edge_x) * (entry_cursor.x - center.x) * world_edge_x/max_range = cx*(entry_cursor.x - center.x) = entry_aim.x` ✓

**Mouse sensitivity**: `mouseDelta` is scaled by `world_edge / max_range`. Effective world change = `(cx+kx) * (dx * world_edge_x/max_range) = cx * dx` — matches hip-fire. ✓

**Positional guarantees** (1920×1080):
- cursor `(1905, 540)` → `world.x = max_range` exactly
- cursor `(1920, 540)` → `world.x = max_range * 960/945`
- Along right screen edge (`aim_pos.x = 1905`, y varies): `camera_offset.x = kx * 945` — constant.

`screen_offset_to_world(edge)` ≪ `max_range` — do NOT use `max_range / edge` as k directly.

### Trace_Aim_Point camera_offset
`camera_offset = current_world_aim * delta`. Cursor position = `center + world_offset_to_screen(current_world_aim * (1 - delta))` — lerps from world aim toward center as camera pans. At delta=1: cursor is at center, camera fully panned to world aim. `ClampMagnitude(aim_range)` is applied to `current_world_aim` before this split.

### ADS 종료 프레임의 aim_pos 복원

ADS OFF 전환 첫 프레임에서 `aim_pos`를 hip 화면 좌표로 복원하는 방식이 모드마다 다름:

| 모드 | ADS ON 마지막 aim_pos | 복원 방식 | 정확도 |
|---|---|---|---|
| `Adaptive_Sensitivity` | `center + world_offset_to_screen(current_world_aim - camera_offset)` (≠ center) | `center + world_offset_to_screen(current_world_aim)` | 정확 |
| `Trace_Aim_Point` | `center + world_offset_to_screen(current_world_aim * (1-delta))` | `center + world_offset_to_screen(current_world_aim)` | 정확 (delta 무관) |
| `Scrollable` | 물리적 커서 위치 | `center + world_offset_to_screen(screen_offset_to_world(aim_pos - center) + camera_offset)` | 근사 |

모든 모드에서 코드상 동일한 경로(else 분기 첫 번째 if): `current_world_aim = screen_offset_to_world(aim_pos - center) + camera_offset`으로 재계산 후 `center + world_offset_to_screen(current_world_aim)` 사용. `current_world_aim`이 월드 공간이라 FOV 보정 불필요.

`Trace_Aim_Point`에서 이전 방식(`aim_pos += world_offset_to_screen(camera_offset)`)이 delta=1에서만 정확했던 이유: delta=1일 때만 aim_pos=center이므로 `center + world_offset_to_screen(camera_offset) = center + world_offset_to_screen(current_world_aim)`. delta < 1이면 aim_pos = `center + world_offset_to_screen(current_world_aim*(1-delta))` ≠ center이므로 `+= world_offset_to_screen(camera_offset)`은 비선형 오차 발생.

### State 변수 의미

- **`camera_offset`**: 카메라 XZ 패닝 오프셋(월드). `GameCamera__UpdateAimOffsetNormal`을 통해 실제 카메라에 적용. 유효 조준 범위를 커서 한계 너머로 확장. ADS 종료 시 zero.
- **`delta`**: 0→1 lerp 가중치. delta=0이면 카메라 고정(커서만으로 조준), delta=1이면 풀 패닝. Adaptive_Sensitivity: `camera_offset = delta * current_world_aim * ratio`. Trace_Aim_Point: `camera_offset = delta * current_world_aim`.
- **`prev_fov`**: 전 프레임 FOV. 0=미초기화. ADS 중 FOV 변경 감지 및 커서 보정에 사용.
- **`out_of_range`**: `AimMarker__LateUpdate`가 생성한 거리 표시 UI. ADS 중 조준 거리/유효 사거리를 표시.

### FOV 보정

**목적**: FOV 변경 시 같은 스크린 픽셀이 다른 월드 방향을 가리킴. 보정은 커서 위치를 구 FOV 픽셀 공간 → 신 FOV 픽셀 공간으로 월드 방향을 유지하며 변환.

**패턴**: `fov_correct(aim_pos, center, prev_fov)` = `center + world_offset_to_screen(screen_offset_to_world(aim_pos - center, prev_fov))`. 월드 공간이 FOV 독립적이라 이 2단계 변환이 성립.

**Adaptive_Sensitivity에서 `prev_aim_pos`도 보정하는 이유**: `ads_world_aim` 델타를 `screen_offset_to_world(aim_pos) - screen_offset_to_world(prev_aim_pos)`로 계산하는데, `prev_aim_pos`를 보정 안 하면 FOV 변경으로 인한 변위가 recoil이 아닌 조준 이동으로 잘못 누적됨.

**Trace_Aim_Point가 FOV 보정을 건너뛰는 이유**: `current_world_aim`은 월드 공간이라 FOV 변경과 무관. `screen_offset_to_world(mouseDelta)`는 매 프레임 현재 FOV로 계산되므로 자동 반영. ADS 종료 복원도 `current_world_aim` 직접 사용으로 FOV 독립적.

### camera_bounds 공식

`world_edge_*` = 엣지 오프셋(픽셀)에 해당하는 월드 거리. `max_camera_*` = 해당 방향의 최대 카메라 패닝 월드량.

`max_camera_x = world_at_center_x * (aim_range - world_edge_x) / world_edge_x`

도출: 커서가 엣지에 있을 때 aim_range까지의 나머지를 카메라가 담당. 비율은 `(aim_range - world_edge) / world_edge`.

### Coordinate conversion (screen ↔ world)
Quarter-view: Y is nonlinear (perspective trapezoid), X is linear per row.

**Critical**: `screen_offset_to_world(A − B) ≠ screen_offset_to_world(A) − screen_offset_to_world(B)`. Always call twice and subtract — never merge into one call.

`camera_offset` must be reset if camera `d` (arm distance), `p` (pitch), or `f` (FOV) change mid-accumulation.
