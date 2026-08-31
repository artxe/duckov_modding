# Super Black Market
Turns the black market into a searchable full item catalog with storage-style pages, unlimited trades, and inventory-first purchases.

https://github.com/artxe/duckov_modding


> r3: v2.3.30
> - Fixed the search and page-number panel width growing whenever the black market was reopened.

> r2: v2.3.30
> - Added a localized search bar that filters the full item catalog before pagination.
> - Search updates are debounced while typing, and clearing or closing the market restores the complete catalog.
> - Matched the left catalog panel height to the market panel and balanced the search and page-button spacing.

> r1: v2.2.0
> - Browse black market pages with numbered buttons, similar to storage pages.
> - Shows localized, priced items with real icons, sorted by item category, quality, and item ID.
> - Trades do not run out, so stock counts and refresh timers are hidden.
> - Selling to the black market always uses the best price multiplier; buying always uses the lowest.
> - Black market purchases go to the character inventory first.
> - If the character inventory has no room, purchased items fall back to player storage.
