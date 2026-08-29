# AGENTS.md

## Build

```
dotnet build
```

Post-build copies DLL/assets to `$(DuckovPath)\Mods\Super_Black_Market\` -- hardcoded in [Super_Black_Market.csproj](Super_Black_Market.csproj).

---

## Non-obvious constraints

### Coding convention

Follow the local Duckov modding convention.

- Use `small_snake_case` for helper methods, fields, parameters, and local variables.
- Keep Unity/Harmony entry points with the names required by the framework, such as `Awake`, `OnDestroy`, `Prefix`, and `Postfix`.
- Harmony patch classes may keep the `Type__Method` shape.
- Reflection cache variables use `I_` plus the original member name for both fields and methods, such as `I_inputAimPoint`, `I_gunState`, and `I_ProcessMousePosViaRecoil`.
- Do not rename API members, named arguments, serialized/private game member strings, or Harmony special parameters such as `__instance` and `__result`.

### Generation patch

The mod fully replaces `BlackMarket.GenerateDemandsAndSupplies`. Preserve the original post-generation side effects: invoke `onAfterGenerateEntries`, save the main character, collect save data, and write the save file when the level is initialized.

Search generations are transient: keep `onAfterGenerateEntries`, but do not save filtered demand/supply lists. While a query is active, `BlackMarket.Save` must be suppressed. Clearing the query, closing the view, or unloading the mod must regenerate and persist the full catalog before removing search state.

### Item order

`selected_page` is shared between demand and supply tabs — both reset their cursor to `get_page_start(selected_page, ...)` every generation. `next_demand_index`/`next_supply_index` are only fallback state and get overwritten by the page logic.

### Candidate filter

`is_accepted_item` icon-name rules pin to one canonical `DisplayNameKey` because multiple item entries share the same sprite (e.g. apple, carrot, Drum03, Medicine_bottle02, Potato, WPN_SnowBall) — without this the list shows duplicates of the same-looking item.

### Paging UI

- `find_pages_template` borrows a `PagesControl_Entry` from elsewhere in the scene via scoring — no template is authored by the mod.
- Page click → `show_page` sets `has_requested_page = true` → calls private `GenerateDemandsAndSupplies` → the generation Prefix consumes the flag via `try_consume_requested_page` to update `selected_page`. The flag is a one-shot bridge; without it the generation patch has no way to know which page was clicked.
- `ScrollRectPositionSetter` applies the saved scroll position twice: immediately, and again on the next `Canvas.willRenderCanvases`. The second pass is required because layout rebuilds after the first apply reset the position.

### Entry layout

`*Panel_Entry__Refresh` Postfixes hide `remainingInfoContainer`/`outOfStockIndicator` because `remaining = int.MaxValue` makes the vanilla UI render misleading "∞ / ∞" text. `align_top_left` forces the parent grid to `UpperLeft` because vanilla center-aligns the row and looks broken when a page returns fewer entries than the panel size.

### Unlimited limits

Generated entries use `int.MaxValue` for `remaining`, making demand/supply trade counts effectively unlimited. `BlackMarket.PayAndRegenerate` is patched to call generation directly without checking or spending `RefreshChance`.

### Price factors

Demand (player sells) and supply (player buys) bypass the vanilla random roll on `RandomValue<float>`. Demand always picks the highest value from the random table via `get_max_float_value`; supply always picks the lowest via `get_min_float_value`. Both helpers reflect into the container's `entries` field, so they degrade to the fallback if the field layout changes.

### Buy delivery

`BlackMarket.Buy` is patched so purchased supply items are instantiated and sent to `ItemUtilities.SendToPlayerCharacterInventory` first. If that fails, use `ItemUtilities.SendToPlayerStorage(item)` without `directToBuffer`, so storage merge/empty-slot behavior runs before the incoming mail buffer.

Keep the original buy guards: null entry, remaining count, membership in the private `supplies` list, and `BuyCost.Pay()`. After delivery, decrement private `remaining` via reflection and invoke private `NotifyChange` via reflection.
