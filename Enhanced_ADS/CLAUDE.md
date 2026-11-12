# Enhanced_ADS

Shared convention and build: root `CLAUDE.md`.

## Aim input (`InputManager__SetAimInputUsingMouse`)

- `ProcessMousePosViaRecoil` applies recoil to `prev_aim_pos` only; it does not add `mouseDelta`. Each mode adds `mouseDelta` itself.
- `current_world_aim = screen_offset_to_world(target_screen_pos - center) + camera_offset`; the cursor is then `aim_pos = center + world_offset_to_screen(current_world_aim - camera_offset)`.
- Adaptive_Sensitivity: `camera_offset.x = delta * current_world_aim.x * max(0, aim_range - ex) / aim_range`, clamped to `±max_x`; `y` uses `ey_up`/`ey_dn` by the sign of `current_world_aim.y` and clamps to `-max_y_dn..max_y_up` (see `camera_bounds`).
- Trace_Aim_Point: `ClampMagnitude(current_world_aim, aim_range)` first, then `camera_offset = current_world_aim * delta` (delta=1: cursor at center, camera fully panned).
- Scrollable: cursor moves in screen space; touching the 15 px edge scrolls `camera_offset`, clamped to `camera_bounds`.
- `camera_offset.y` stays 0 unless `pitch - 1 > fov / 2`.
- ADS exit (first frame with `camera_offset != 0`): `current_world_aim = screen_offset_to_world(aim_pos - center) + camera_offset`, zero `camera_offset`, `aim_pos = center + world_offset_to_screen(current_world_aim)`. World space, so no FOV correction here. Exact for Adaptive/Trace, approximate for Scrollable.

## camera_bounds

`camera_bounds(aim_range, center, edge_offset)` returns `(ex, ey_up, ey_dn, max_x, max_y_up, max_y_dn)`. `ex`, `ey_up`, `ey_dn` = world distance of the edge offset (`center - 15 px`) along x, up and down. `max_* = screen_offset_to_world(full center offset in that direction) * max(0, aim_range - e) / e` with the matching `e`: the camera covers the range beyond the cursor edge. `screen_offset_to_world(edge)` ≪ `aim_range`; never use `aim_range / edge` as the factor.

## State

- `camera_offset`: camera XZ panning offset (world), applied through `GameCamera__UpdateAimOffsetNormal`; extends the effective aim range beyond the cursor limit; zero on ADS exit.
- `delta`: 0→1 via `Mathf.MoveTowards(delta, 1, Time.deltaTime * gun.AdsSpeed)` (the gun's own ADS speed). 0 = first ADS frame, camera fixed; 1 = full panning.
- `prev_fov`: previous frame's FOV, 0 = uninitialized. Detects FOV changes during ADS.
- `out_of_range`: distance display UI built by `AimMarker__LateUpdate` (aim distance / effective range).

## FOV correction

`fov_correct(aim_pos, center, prev_fov) = center + world_offset_to_screen(screen_offset_to_world(aim_pos - center, prev_fov))`: world space is FOV-independent, so old-FOV pixels → world → new-FOV pixels keeps the direction. Applied on frames where `prev_fov > 0` and the FOV changed; outside ADS only when `camera_offset == 0` (otherwise the ADS exit restore handles it).

## Coordinate conversion

Quarter-view: Y is nonlinear (perspective trapezoid), X is linear per row. `screen_offset_to_world(A - B) ≠ screen_offset_to_world(A) - screen_offset_to_world(B)`; always convert each point and subtract.

## Auto reload

`ItemAgent_Gun__TransToEmpty` replaces the transition only while `State.auto_reload`; when disabled the prefix returns `true` so vanilla runs. The persisted setting defaults to enabled; keep it so.
