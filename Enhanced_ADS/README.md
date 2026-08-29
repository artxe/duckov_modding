# Enhanced ADS
Adds multiple ADS camera modes with smooth world-space aiming, stable sensitivity, scope FOV correction, aim distance display, and optional auto-reload on empty.

https://github.com/artxe/duckov_modding


> r17: v2.3.30
> - Added a persistent Auto Reload setting. Set it to Disabled in the game options to stop Enhanced ADS from forcing a reload and restore the vanilla empty-magazine behavior.

> r16: v2.2.0
> - Added a "Default" option to the ADS Camera Mode setting to revert to vanilla behavior without disabling the mod.
> - ADS camera panning speed now matches the gun's ADS speed instead of a fixed rate.
> - Aiming down sights now extends the gun's half-damage distance by 5m per point of Aiming Range, granting a range advantage for using ADS.
> - Aim distance display now shows the gun's maximum bullet distance as the reference value instead of the half-damage distance.
> - Aim distance display now remains visible while using the Default ADS Camera Mode.

> r15: v2.2.0
> - Fixed camera snapping/jumping that could occur when exiting ADS.
> - Fixed invalid cursor movement near extreme camera angles and FOV limits.
> - Improved consistency of aim positioning while zoom levels/FOV change.
> - Improved vertical camera movement limits for Adaptive Sensitivity and Scrollable modes at steep viewing angles.

> r14: v2.2.0
> - Rewrote Adaptive Sensitivity and Trace Aim Point camera calculations using world-space math for improved accuracy.
> - Added FOV correction so the aim point no longer drifts when the camera FOV changes.
> - Camera bounds now handle upward and downward directions separately for correct asymmetric panning.
> - Fixed an issue where the ADS Camera Mode option would duplicate in the settings panel when reopened.
> - Aim range now accounts for the distance between the character and the gun muzzle.
> - Close-range distance display no longer shows a negative value when aiming behind the character.

> r13: v1.2.5
> - Fixed an issue where the option settings would reset upon restarting the game.
> - An Adaptive Sensitivity option has been added, which behaves similarly to the vanilla system but offers better visibility and more consistent sensitivity.

> r12: v1.2.5
> - When the game loses focus, it no longer synchronizes the mouse cursor position with the aim point (fixes an issue when using another monitor in a dual-monitor setup).

> r11: v1.2.5
> - Multilingual Support in Menu Options. (简体中文, 繁體中文, English, Français, Deutsch, 日本語, 한국어, Português (Brasil), Русский, Español)
> - Fixed a vanilla game issue where the mouse cursor failed to align with the aim point when controlling recoil.

> r10: v1.1.6
> - Added an option to choose the camera behavior while ADS.

> r9: v1.1.6
> - In ADS mode, when aiming toward the edge of the screen, the camera no longer snaps, instead, it smoothly moves in that direction, similar to the feel in LOL.

> r8: v1.1.6
> - While in ADS mode, display both the current aiming distance and the distance at which the gun's damage is reduced by half next to the aiming point.
> - The camera no longer moves toward the aiming point when aiming beyond the weapon's range.

> r7: v1.1.6
> - Automatically reload when ammo runs out during continuous fire, without requiring another left click.

> r6: v1.1.6
> - Add a 20px margin to off-screen detection for the aiming point.

> r5: v1.1.6
> - Make the camera stay aligned with the character (hip-fire position) until the aiming point goes off-screen.

> r4: v1.1.6
> - Prevent aiming distance from being shorter than the bullet range.

> r3: v1.1.6
> - At the start of ADS, the camera now moves twice as fast toward the aim point, and mouse input updates instantly while aiming.

> r2: v1.1.6
> - Camera position now interpolates smoothly according to the current ADS progress.

> r1: v1.1.6
> - No changes.

> r0: v1.1.6
> - During ADS or scoped view, the camera is positioned directly at the aiming point.
