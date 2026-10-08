# Super_Black_Market

Shared convention and build: root `CLAUDE.md`.

## Generation

- `BlackMarket__GenerateDemandsAndSupplies` fully replaces the vanilla method. Keep the post-generation side effects: invoke `onAfterGenerateEntries`, save the main character, `SavesSystem.CollectSaveData()` and `SaveFile()` when the level is initialized.
- Search generations are transient: still invoke `onAfterGenerateEntries`, but never save filtered lists. `BlackMarket__Save` is suppressed while a query is active. Clearing the query, closing the view or unloading the mod must regenerate and persist the full catalog before search state is removed.
- `selected_page` is shared by the demand and supply tabs; both reset their cursor to `get_page_start(selected_page, ...)` every generation. `next_demand_index`/`next_supply_index` are fallback state only, overwritten by the page logic.
- `is_accepted_item` icon-name rules pin one canonical `DisplayNameKey` per sprite because several item entries share a sprite (apple, carrot, Drum03, Medicine_bottle02, Potato, WPN_SnowBall); otherwise the list shows look-alike duplicates.
- Entries use `int.MaxValue` for `remaining` (unlimited trades). `BlackMarket__PayAndRegenerate` calls generation directly without checking or spending `RefreshChance`.
- Price factors bypass the vanilla `RandomValue<float>` roll: demand (player sells) takes `get_max_float_value`, supply (player buys) takes `get_min_float_value`. Both reflect into the container's `entries` field and fall back if the layout changes.

## Paging UI

- `find_pages_template` borrows a `PagesControl_Entry` from elsewhere in the scene by scoring; the mod authors no template.
- Page click → `show_page` sets `has_requested_page` → calls private `GenerateDemandsAndSupplies` → the generation Prefix consumes it via `try_consume_requested_page` to set `selected_page`. The one-shot flag is the only way the patch learns which page was clicked.
- `ScrollRectPositionSetter` applies the saved scroll position immediately and again on the next `Canvas.willRenderCanvases`; layout rebuilds after the first apply reset it.

## Entry layout

- `*Panel_Entry__Refresh` Postfixes hide `remainingInfoContainer`/`outOfStockIndicator`: `remaining = int.MaxValue` renders as a misleading "∞ / ∞".
- `align_top_left` forces the parent grid to `UpperLeft`; vanilla center-aligns and looks broken when a page has fewer entries than the panel.

## Buy

`BlackMarket__Buy` keeps the vanilla guards (null entry, remaining count, membership in private `supplies`, `BuyCost.Pay()`), instantiates the items and tries `ItemUtilities.SendToPlayerCharacterInventory` first, then `SendToPlayerStorage(item)` without `directToBuffer` so storage merge/empty-slot handling runs before the mail buffer. Afterwards decrement private `remaining` and invoke private `NotifyChange` via reflection.
