using Duckov;
using Duckov.BlackMarkets;
using Duckov.BlackMarkets.UI;
using Duckov.Economy;
using Duckov.UI.Inventories;
using Duckov.Utilities;
using Cysharp.Threading.Tasks;
using HarmonyLib;
using ItemStatsSystem;
using Saves;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Super_Black_Market
{
	[HarmonyPatch(typeof(BlackMarket), "GenerateDemandsAndSupplies")]
	internal class BlackMarket__GenerateDemandsAndSupplies
	{
		static readonly FieldInfo I_demandsCount = AccessTools.Field(typeof(BlackMarket), "demandsCount");
		static readonly FieldInfo I_suppliesCount = AccessTools.Field(typeof(BlackMarket), "suppliesCount");
		static readonly FieldInfo I_demandFactorRand = AccessTools.Field(typeof(BlackMarket), "demandFactorRand");
		static readonly FieldInfo I_demandBatchCountRand = AccessTools.Field(typeof(BlackMarket), "demandBatchCountRand");
		static readonly FieldInfo I_supplyFactorRand = AccessTools.Field(typeof(BlackMarket), "supplyFactorRand");
		static readonly FieldInfo I_supplyBatchCountRand = AccessTools.Field(typeof(BlackMarket), "supplyBatchCountRand");
		static readonly FieldInfo I_demands = AccessTools.Field(typeof(BlackMarket), "demands");
		static readonly FieldInfo I_supplies = AccessTools.Field(typeof(BlackMarket), "supplies");
		static readonly FieldInfo I_onAfterGenerateEntries = AccessTools.Field(typeof(BlackMarket), "onAfterGenerateEntries");
		static readonly FieldInfo I_itemID = AccessTools.Field(typeof(BlackMarket.DemandSupplyEntry), "itemID");
		static readonly FieldInfo I_remaining = AccessTools.Field(typeof(BlackMarket.DemandSupplyEntry), "remaining");
		static readonly FieldInfo I_priceFactor = AccessTools.Field(typeof(BlackMarket.DemandSupplyEntry), "priceFactor");
		static readonly FieldInfo I_batchCount = AccessTools.Field(typeof(BlackMarket.DemandSupplyEntry), "batchCount");
		static readonly MethodInfo I_SaveMainCharacter = AccessTools.Method(typeof(LevelManager), "SaveMainCharacter");
		static int next_demand_index;
		static int next_supply_index;
		static int selected_page;
		static Dictionary<int, int>? tag_order;
		static bool Prefix(BlackMarket __instance)
		{
			List<int> candidates = get_accepted_items();
			if (candidates.Count == 0)
			{
				return true;
			}
			List<BlackMarket.DemandSupplyEntry> demands = (List<BlackMarket.DemandSupplyEntry>)I_demands.GetValue(__instance);
			List<BlackMarket.DemandSupplyEntry> supplies = (List<BlackMarket.DemandSupplyEntry>)I_supplies.GetValue(__instance);
			demands.Clear();
			supplies.Clear();
			int demand_cursor = next_demand_index % candidates.Count;
			int supply_cursor = next_supply_index % candidates.Count;
			int demands_count = (int)I_demandsCount.GetValue(__instance);
			int supplies_count = (int)I_suppliesCount.GetValue(__instance);
			if (BlackMarketPageController.try_consume_requested_page(out BlackMarketView.Mode mode, out int page))
			{
				selected_page = clamp_page(page, candidates.Count, Mathf.Max(demands_count, supplies_count));
			}
			demand_cursor = get_page_start(selected_page, candidates.Count, demands_count);
			supply_cursor = get_page_start(selected_page, candidates.Count, supplies_count);
			add_entries(__instance, demands, candidates, ref demand_cursor, demands_count, I_demandFactorRand, I_demandBatchCountRand, use_max_price_factor: true);
			add_entries(__instance, supplies, candidates, ref supply_cursor, supplies_count, I_supplyFactorRand, I_supplyBatchCountRand, use_min_price_factor: true);
			next_demand_index = demand_cursor % candidates.Count;
			next_supply_index = supply_cursor % candidates.Count;
			((Action)I_onAfterGenerateEntries.GetValue(__instance))?.Invoke();
			if (LevelManager.LevelInited)
			{
				I_SaveMainCharacter.Invoke(LevelManager.Instance, null);
				SavesSystem.CollectSaveData();
				SavesSystem.SaveFile();
			}
			return false;
		}
		internal static int get_page_count(BlackMarket black_market, BlackMarketView.Mode mode)
		{
			List<int> candidates = get_accepted_items();
			if (candidates.Count == 0)
			{
				return 0;
			}
			int count = (mode & BlackMarketView.Mode.Supply) == BlackMarketView.Mode.Supply
				? (int)I_suppliesCount.GetValue(black_market)
				: (int)I_demandsCount.GetValue(black_market);
			return Mathf.Max(1, Mathf.CeilToInt((float)candidates.Count / Mathf.Max(1, count)));
		}
		internal static int get_selected_page(BlackMarketView.Mode mode)
		{
			return selected_page;
		}
		static List<int> get_accepted_items()
		{
			return ItemAssetsCollection.Instance.entries
				.Where(entry => entry != null && entry.metaData.id > 0)
				.OrderBy(entry => get_tag_order(entry.metaData))
				.ThenBy(entry => get_tag_sort_key(entry.metaData))
				.ThenBy(entry => entry.metaData.quality)
				.ThenBy(entry => entry.typeID)
				.Where(entry => is_accepted_item(entry.metaData))
				.Select(entry => entry.typeID)
				.ToList();
		}
		static bool is_accepted_item(ItemMetaData meta_data)
		{
			string display_name = meta_data.DisplayName ?? string.Empty;
			string description = meta_data.Description ?? string.Empty;
			if ((display_name.StartsWith("*") && display_name.EndsWith("*"))
				|| (description.StartsWith("*") && description.EndsWith("*")))
			{
				return false;
			}
			string display_name_key = meta_data.DisplayNameKey ?? string.Empty;
			if (display_name_key == "Item_1_AccessoryTemplate")
			{
				return false;
			}
			if (display_name_key == "Item_1_Blueprint")
			{
				return false;
			}
			if (display_name_key == "Item_1_Rifle-A_template")
			{
				return false;
			}
			if (display_name_key == "Item_Aquarium")
			{
				return false;
			}
			if (display_name_key == "Item_Bullet_Template")
			{
				return false;
			}
			Sprite icon = meta_data.icon;
			if (icon.name == "apple")
			{
				return display_name_key == "Item_Apple";
			}
			if (icon.name == "carrot")
			{
				return display_name_key == "Item_Carrot";
			}
			if (icon.name == "cross")
			{
				return false;
			}
			if (icon.name == "Drum03")
			{
				return display_name_key == "Item_Drum3";
			}
			if (icon.name == "Knife05")
			{
				return false;
			}
			if (icon.name == "Medicine_bottle02")
			{
				return display_name_key == "Item_Drugs";
			}
			if (icon.name == "Potato")
			{
				return display_name_key == "Item_Potato";
			}
			if (icon.name == "Rifle03")
			{
				return false;
			}
			if (icon.name == "waffle")
			{
				return false;
			}
			if (icon.name == "WPN_SnowBall")
			{
				return display_name_key == "Item_RKT_SnowGun_Ice";
			}
			if (meta_data.priceEach <= 1)
			{
				return false;
			}
			return true;
		}
		static int get_tag_order(ItemMetaData meta_data)
		{
			if (tag_order == null)
			{
				tag_order = build_tag_order();
			}
			int result = int.MaxValue;
			foreach (Tag tag in meta_data.tags ?? Enumerable.Empty<Tag>())
			{
				if (tag != null && tag_order.TryGetValue(tag.Hash, out int order) && order < result)
				{
					result = order;
				}
			}
			return result;
		}
		static string get_tag_sort_key(ItemMetaData meta_data)
		{
			if (tag_order == null)
			{
				tag_order = build_tag_order();
			}
			return string.Join(
				",",
				(meta_data.tags ?? Enumerable.Empty<Tag>())
					.Where(tag => tag != null && tag_order.ContainsKey(tag.Hash))
					.Select(tag => tag_order[tag.Hash])
					.OrderBy(order => order)
					.Select(order => order.ToString("D5")));
		}
		static Dictionary<int, int> build_tag_order()
		{
			Dictionary<int, int> result = new Dictionary<int, int>();
			int order = 0;
			foreach (Tag tag in GameplayDataSettings.Tags.AllTags)
			{
				if (tag != null && !result.ContainsKey(tag.Hash))
				{
					result[tag.Hash] = order++;
				}
			}
			return result;
		}
		static void add_entries(
			BlackMarket black_market,
			List<BlackMarket.DemandSupplyEntry> entries,
			List<int> candidates,
			ref int cursor,
			int count,
			FieldInfo? factor_rand,
			FieldInfo batch_count_rand,
			bool use_max_price_factor = false,
			bool use_min_price_factor = false)
		{
			for (int i = 0; i < count && cursor < candidates.Count; i++)
			{
				int item_id = candidates[cursor++];
				object? factor_container = factor_rand?.GetValue(black_market);
				float price_factor = use_max_price_factor
					? get_max_float_value(factor_container, 1f)
					: use_min_price_factor
						? get_min_float_value(factor_container, 1f)
						: get_random_value<float>(factor_container, 1f);
				entries.Add(create_entry(
					item_id,
					int.MaxValue,
					price_factor,
					get_random_value<int>(batch_count_rand.GetValue(black_market), 1)));
			}
		}
		static int clamp_page(int page, int candidates_count, int entries_count)
		{
			int page_count = Mathf.Max(1, Mathf.CeilToInt((float)candidates_count / Mathf.Max(1, entries_count)));
			return Mathf.Clamp(page, 0, page_count - 1);
		}
		static int get_page_start(int page, int candidates_count, int entries_count)
		{
			if (candidates_count <= 0)
			{
				return 0;
			}
			return clamp_page(page, candidates_count, entries_count) * Mathf.Max(1, entries_count);
		}
		static BlackMarket.DemandSupplyEntry create_entry(int item_id, int remaining, float price_factor, int batch_count)
		{
			BlackMarket.DemandSupplyEntry entry = new BlackMarket.DemandSupplyEntry();
			I_itemID.SetValue(entry, item_id);
			I_remaining.SetValue(entry, remaining);
			I_priceFactor.SetValue(entry, price_factor);
			I_batchCount.SetValue(entry, batch_count);
			return entry;
		}
		static float get_max_float_value(object? random_container, float fallback)
		{
			if (random_container == null)
			{
				return fallback;
			}
			FieldInfo entries_field = random_container.GetType().GetField("entries");
			if (!(entries_field?.GetValue(random_container) is System.Collections.IEnumerable entries))
			{
				return fallback;
			}
			float result = fallback;
			bool found = false;
			foreach (object entry in entries)
			{
				FieldInfo value_field = entry.GetType().GetField("value");
				if (value_field?.GetValue(entry) is float value)
				{
					result = found ? Mathf.Max(result, value) : value;
					found = true;
				}
			}
			return result;
		}
		static float get_min_float_value(object? random_container, float fallback)
		{
			if (random_container == null)
			{
				return fallback;
			}
			FieldInfo entries_field = random_container.GetType().GetField("entries");
			if (!(entries_field?.GetValue(random_container) is System.Collections.IEnumerable entries))
			{
				return fallback;
			}
			float result = fallback;
			bool found = false;
			foreach (object entry in entries)
			{
				FieldInfo value_field = entry.GetType().GetField("value");
				if (value_field?.GetValue(entry) is float value)
				{
					result = found ? Mathf.Min(result, value) : value;
					found = true;
				}
			}
			return result;
		}
		static T get_random_value<T>(object? random_container, T fallback)
		{
			if (random_container == null)
			{
				return fallback;
			}
			MethodInfo method = random_container.GetType().GetMethod("GetRandom", Type.EmptyTypes);
			if (method == null)
			{
				return fallback;
			}
			return (T)method.Invoke(random_container, null);
		}
	}
	[HarmonyPatch(typeof(BlackMarket), nameof(BlackMarket.PayAndRegenerate))]
	internal class BlackMarket__PayAndRegenerate
	{
		static readonly MethodInfo I_GenerateDemandsAndSupplies = AccessTools.Method(typeof(BlackMarket), "GenerateDemandsAndSupplies");
		static bool Prefix(BlackMarket __instance)
		{
			I_GenerateDemandsAndSupplies.Invoke(__instance, null);
			return false;
		}
	}
	[HarmonyPatch(typeof(BlackMarket), nameof(BlackMarket.Buy))]
	internal class BlackMarket__Buy
	{
		static readonly FieldInfo I_supplies = AccessTools.Field(typeof(BlackMarket), "supplies");
		static readonly FieldInfo I_remaining = AccessTools.Field(typeof(BlackMarket.DemandSupplyEntry), "remaining");
		static readonly MethodInfo I_NotifyChange = AccessTools.Method(typeof(BlackMarket.DemandSupplyEntry), "NotifyChange");
		static bool Prefix(BlackMarket __instance, BlackMarket.DemandSupplyEntry entry, ref UniTask<bool> __result)
		{
			__result = buy_to_character_inventory(__instance, entry);
			return false;
		}
		static async UniTask<bool> buy_to_character_inventory(BlackMarket black_market, BlackMarket.DemandSupplyEntry entry)
		{
			if (entry == null)
			{
				return false;
			}
			if (entry.Remaining <= 0)
			{
				return false;
			}
			List<BlackMarket.DemandSupplyEntry> supplies = (List<BlackMarket.DemandSupplyEntry>)I_supplies.GetValue(black_market);
			if (!supplies.Contains(entry))
			{
				return false;
			}
			if (!entry.BuyCost.Pay())
			{
				return false;
			}
			await return_to_character_inventory_first(entry.SellCost);
			I_remaining.SetValue(entry, entry.Remaining - 1);
			I_NotifyChange.Invoke(entry, null);
			return true;
		}
		static async UniTask return_to_character_inventory_first(Cost cost)
		{
			if (cost.items != null)
			{
				foreach (Cost.ItemEntry item_entry in cost.items)
				{
					long count = item_entry.amount;
					while (count > 0)
					{
						Item item = await ItemAssetsCollection.InstantiateAsync(item_entry.id);
						if (item.Stackable)
						{
							item.StackCount = count > item.MaxStackCount ? item.MaxStackCount : (int)count;
							count -= Mathf.Max(1, item.StackCount);
						}
						else
						{
							count--;
						}
						if (!ItemUtilities.SendToPlayerCharacterInventory(item))
						{
							ItemUtilities.SendToPlayerStorage(item);
						}
					}
				}
			}
			if (cost.money > 0)
			{
				EconomyManager.Add(cost.money);
			}
		}
	}
	[HarmonyPatch(typeof(BlackMarketView), "Setup")]
	internal class BlackMarketView__Setup
	{
		static void Postfix(BlackMarketView __instance)
		{
			BlackMarketPageController.install(__instance);
		}
	}
	[HarmonyPatch(typeof(BlackMarketView), "SetMode")]
	internal class BlackMarketView__SetMode
	{
		static void Postfix(BlackMarketView __instance)
		{
			BlackMarketPageController.refresh(__instance);
		}
	}
	[HarmonyPatch(typeof(DemandPanel_Entry), "Refresh")]
	internal class DemandPanel_Entry__Refresh
	{
		static readonly FieldInfo I_remainingInfoContainer = AccessTools.Field(typeof(DemandPanel_Entry), "remainingInfoContainer");
		static readonly FieldInfo I_outOfStockIndicator = AccessTools.Field(typeof(DemandPanel_Entry), "outOfStockIndicator");
		static void Postfix(DemandPanel_Entry __instance)
		{
			((GameObject)I_remainingInfoContainer.GetValue(__instance))?.SetActive(false);
			((GameObject)I_outOfStockIndicator.GetValue(__instance))?.SetActive(false);
		}
	}
	[HarmonyPatch(typeof(DemandPanel_Entry), "Setup")]
	internal class DemandPanel_Entry__Setup
	{
		static void Postfix(DemandPanel_Entry __instance)
		{
			BlackMarketEntryLayout.align_top_left(__instance.transform);
		}
	}
	[HarmonyPatch(typeof(SupplyPanel_Entry), "Refresh")]
	internal class SupplyPanel_Entry__Refresh
	{
		static readonly FieldInfo I_remainingInfoContainer = AccessTools.Field(typeof(SupplyPanel_Entry), "remainingInfoContainer");
		static readonly FieldInfo I_outOfStockIndicator = AccessTools.Field(typeof(SupplyPanel_Entry), "outOfStockIndicator");
		static void Postfix(SupplyPanel_Entry __instance)
		{
			((GameObject)I_remainingInfoContainer.GetValue(__instance))?.SetActive(false);
			((GameObject)I_outOfStockIndicator.GetValue(__instance))?.SetActive(false);
		}
	}
	[HarmonyPatch(typeof(SupplyPanel_Entry), "Setup")]
	internal class SupplyPanel_Entry__Setup
	{
		static void Postfix(SupplyPanel_Entry __instance)
		{
			BlackMarketEntryLayout.align_top_left(__instance.transform);
		}
	}
	internal static class BlackMarketEntryLayout
	{
		internal static void align_top_left(Transform entry_transform)
		{
			entry_transform.SetAsLastSibling();
			GridLayoutGroup? grid = entry_transform.parent != null ? entry_transform.parent.GetComponent<GridLayoutGroup>() : null;
			if (grid == null)
			{
				return;
			}
			grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
			grid.startAxis = GridLayoutGroup.Axis.Horizontal;
			grid.childAlignment = TextAnchor.UpperLeft;
		}
	}
	internal static class BlackMarketPageController
	{
		static readonly FieldInfo I_mode = AccessTools.Field(typeof(BlackMarketView), "mode");
		static readonly FieldInfo I_btn_refresh = AccessTools.Field(typeof(BlackMarketView), "btn_refresh");
		static readonly FieldInfo I_demandPanel = AccessTools.Field(typeof(BlackMarketView), "demandPanel");
		static readonly FieldInfo I_supplyPanel = AccessTools.Field(typeof(BlackMarketView), "supplyPanel");
		static readonly FieldInfo I_refreshETAText = AccessTools.Field(typeof(BlackMarketView), "refreshETAText");
		static readonly FieldInfo I_refreshChanceText = AccessTools.Field(typeof(BlackMarketView), "refreshChanceText");
		static readonly FieldInfo I_refreshInteractableIndicator = AccessTools.Field(typeof(BlackMarketView), "refreshInteractableIndicator");
		static readonly FieldInfo I_template = AccessTools.Field(typeof(PagesControl), "template");
		static readonly FieldInfo I_text = AccessTools.Field(typeof(PagesControl_Entry), "text");
		static readonly FieldInfo I_selectedIndicator = AccessTools.Field(typeof(PagesControl_Entry), "selectedIndicator");
		static readonly FieldInfo I_button = AccessTools.Field(typeof(PagesControl_Entry), "button");
		static readonly MethodInfo I_GenerateDemandsAndSupplies = AccessTools.Method(typeof(BlackMarket), "GenerateDemandsAndSupplies");
		static readonly Dictionary<BlackMarketView, GameObject> page_container_by_view = new Dictionary<BlackMarketView, GameObject>();
		static readonly Dictionary<BlackMarketView, float> scroll_position_by_view = new Dictionary<BlackMarketView, float>();
		static readonly HashSet<BlackMarketView> reset_scroll_on_next_refresh = new HashSet<BlackMarketView>();
		static BlackMarketView.Mode requested_mode;
		static int requested_page;
		static bool has_requested_page;
		internal static bool try_consume_requested_page(out BlackMarketView.Mode mode, out int page)
		{
			mode = requested_mode;
			page = requested_page;
			bool result = has_requested_page;
			has_requested_page = false;
			return result;
		}
		internal static void install(BlackMarketView view)
		{
			Button template = (Button)I_btn_refresh.GetValue(view);
			if (template == null)
			{
				return;
			}
			hide_refresh_chrome(view, template);
			template.gameObject.SetActive(false);
			BlackMarketView.Mode mode = (BlackMarketView.Mode)I_mode.GetValue(view);
			if (mode != BlackMarketView.Mode.None)
			{
				reset_scroll_on_next_refresh.Add(view);
				show_page(view, mode, 0);
				return;
			}
			reset_scroll_on_next_refresh.Add(view);
			refresh(view);
		}
		internal static void refresh(BlackMarketView view)
		{
			BlackMarket target = view.Target;
			Button template = (Button)I_btn_refresh.GetValue(view);
			if (target == null || template == null)
			{
				return;
			}
			BlackMarketView.Mode mode = (BlackMarketView.Mode)I_mode.GetValue(view);
			if (mode == BlackMarketView.Mode.None)
			{
				return;
			}
			PagesControl_Entry? pages_template = find_pages_template();
			if (pages_template == null)
			{
				Debug.LogWarning("Super Black Market: 창고 페이지 버튼 템플릿을 찾지 못했습니다.");
				return;
			}
			bool reset_scroll = reset_scroll_on_next_refresh.Remove(view);
			float scroll_position = reset_scroll ? 1f : get_current_scroll_position(view);
			destroy_page_container(view);
			int page_count = BlackMarket__GenerateDemandsAndSupplies.get_page_count(target, mode);
			int selected_page = BlackMarket__GenerateDemandsAndSupplies.get_selected_page(mode);
			Transform parent = template.transform.parent;
			GameObject container = create_page_container(view, mode, template, pages_template, parent, page_count);
			for (int i = 0; i < page_count; i++)
			{
				int page = i;
				PagesControl_Entry entry = UnityEngine.Object.Instantiate(pages_template, container.transform);
				entry.gameObject.name = $"BlackMarketPage_{page}";
				entry.gameObject.SetActive(true);
				Button button = (Button)I_button.GetValue(entry) ?? entry.GetComponentInChildren<Button>(true);
				if (button != null)
				{
					button.onClick.RemoveAllListeners();
					button.onClick.AddListener(delegate
					{
						show_page(view, mode, page);
					});
				}
				TextMeshProUGUI text = (TextMeshProUGUI)I_text.GetValue(entry);
				if (text != null)
				{
					text.text = page.ToString();
				}
				((GameObject)I_selectedIndicator.GetValue(entry))?.SetActive(page == selected_page);
			}
			ScrollRect scroll_rect = container.GetComponentInParent<ScrollRect>();
			if (scroll_rect != null)
			{
				ScrollRectPositionSetter.set_after_layout(scroll_rect, scroll_position);
				scroll_position_by_view[view] = scroll_position;
			}
		}
		static void hide_refresh_chrome(BlackMarketView view, Button refresh_button)
		{
			destroy_page_container(view);
			hide_and_ignore_layout(((TextMeshProUGUI)I_refreshETAText.GetValue(view))?.gameObject);
			hide_and_ignore_layout(((TextMeshProUGUI)I_refreshChanceText.GetValue(view))?.gameObject);
			hide_and_ignore_layout((GameObject)I_refreshInteractableIndicator.GetValue(view));
			Transform parent = refresh_button.transform.parent;
			if (parent == null)
			{
				return;
			}
			Image background = parent.GetComponent<Image>();
			if (background != null)
			{
				background.enabled = false;
			}
			foreach (TextMeshProUGUI text in parent.GetComponentsInChildren<TextMeshProUGUI>(true))
			{
				if (text.transform.IsChildOf(refresh_button.transform))
				{
					continue;
				}
				if (text.transform.GetComponentInParent<PagesControl_Entry>() != null)
				{
					continue;
				}
				hide_and_ignore_layout(text.gameObject);
			}
		}
		static void hide_and_ignore_layout(GameObject? game_object)
		{
			if (game_object == null)
			{
				return;
			}
			LayoutElement layout_element = game_object.GetComponent<LayoutElement>();
			if (layout_element == null)
			{
				layout_element = game_object.AddComponent<LayoutElement>();
			}
			layout_element.ignoreLayout = true;
			game_object.SetActive(false);
		}
		static PagesControl_Entry? find_pages_template()
		{
			PagesControl_Entry? best_template = null;
			int best_score = int.MinValue;
			foreach (PagesControl pages_control in Resources.FindObjectsOfTypeAll<PagesControl>())
			{
				PagesControl_Entry template = (PagesControl_Entry)I_template.GetValue(pages_control);
				if (template != null)
				{
					int score = score_pages_template(pages_control, template);
					if (score > best_score)
					{
						best_template = template;
						best_score = score;
					}
				}
			}
			return best_template;
		}
		static int score_pages_template(PagesControl pages_control, PagesControl_Entry template)
		{
			int score = pages_control.gameObject.activeInHierarchy ? 10 : 0;
			Transform source_parent = template.transform.parent;
			if (source_parent == null)
			{
				return score;
			}
			GridLayoutGroup grid = source_parent.GetComponent<GridLayoutGroup>();
			if (grid != null)
			{
				score += 100;
				if (grid.constraintCount <= 2)
				{
					score += 50;
				}
				if (grid.startAxis == GridLayoutGroup.Axis.Vertical)
				{
					score += 25;
				}
			}
			if (source_parent.GetComponent<HorizontalLayoutGroup>() != null)
			{
				score -= 25;
			}
			return score;
		}
		static GameObject create_page_container(BlackMarketView view, BlackMarketView.Mode mode, Button refresh_button, PagesControl_Entry pages_template, Transform parent, int page_count)
		{
			int refresh_sibling_index = refresh_button.transform.GetSiblingIndex();
			GameObject viewport = new GameObject("BlackMarketPages_Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(ScrollRect), typeof(LayoutElement));
			viewport.transform.SetParent(parent, worldPositionStays: false);
			viewport.transform.SetSiblingIndex(refresh_sibling_index);
			page_container_by_view[view] = viewport;
			RectTransform refresh_rect = refresh_button.GetComponent<RectTransform>();
			RectTransform viewport_rect = (RectTransform)viewport.transform;
			if (refresh_rect != null)
			{
				viewport_rect.anchorMin = refresh_rect.anchorMin;
				viewport_rect.anchorMax = refresh_rect.anchorMax;
				viewport_rect.pivot = refresh_rect.pivot;
				viewport_rect.anchoredPosition = refresh_rect.anchoredPosition;
				viewport_rect.sizeDelta = refresh_rect.sizeDelta;
				viewport_rect.localScale = refresh_rect.localScale;
			}
			GameObject container = new GameObject("BlackMarketPages_FromStorageLayout", typeof(RectTransform), typeof(LayoutElement));
			container.transform.SetParent(viewport.transform, worldPositionStays: false);
			RectTransform container_rect = (RectTransform)container.transform;
			container_rect.anchorMin = new Vector2(0f, 1f);
			container_rect.anchorMax = new Vector2(0f, 1f);
			container_rect.pivot = new Vector2(0f, 1f);
			container_rect.anchoredPosition = Vector2.zero;
			Transform pages_layout_source = pages_template.transform.parent;
			if (pages_layout_source != null)
			{
				copy_layout_component<HorizontalLayoutGroup>(pages_layout_source.gameObject, container);
				copy_layout_component<VerticalLayoutGroup>(pages_layout_source.gameObject, container);
				copy_layout_component<GridLayoutGroup>(pages_layout_source.gameObject, container);
				force_black_market_page_grid(container);
				resize_page_rects(view, mode, parent, viewport_rect, viewport.GetComponent<LayoutElement>(), container_rect, container.GetComponent<LayoutElement>(), container.GetComponent<GridLayoutGroup>(), page_count);
			}
			ScrollRect scroll_rect = viewport.GetComponent<ScrollRect>();
			scroll_rect.viewport = viewport_rect;
			scroll_rect.content = container_rect;
			scroll_rect.horizontal = false;
			scroll_rect.vertical = true;
			scroll_rect.movementType = ScrollRect.MovementType.Clamped;
			scroll_rect.inertia = true;
			scroll_rect.scrollSensitivity = 24f;
			return container;
		}
		static void force_black_market_page_grid(GameObject container)
		{
			GridLayoutGroup grid = container.GetComponent<GridLayoutGroup>();
			if (grid == null)
			{
				return;
			}
			grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
			grid.startAxis = GridLayoutGroup.Axis.Horizontal;
			grid.childAlignment = TextAnchor.UpperLeft;
			grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
		}
		static void resize_page_rects(BlackMarketView view, BlackMarketView.Mode mode, Transform parent, RectTransform viewport_rect, LayoutElement viewport_layout, RectTransform content_rect, LayoutElement content_layout, GridLayoutGroup grid, int page_count)
		{
			if (grid == null)
			{
				return;
			}
			float main_height = get_main_panel_height(view, mode);
			float available_width = get_available_left_width(parent);
			int columns = calculate_columns(grid, available_width);
			grid.constraintCount = columns;
			int rows = Mathf.Max(1, Mathf.CeilToInt((float)Mathf.Max(1, page_count) / columns));
			float width = grid.padding.left + grid.padding.right + (grid.cellSize.x * columns) + (grid.spacing.x * Mathf.Max(0, columns - 1));
			float height = grid.padding.top + grid.padding.bottom + (grid.cellSize.y * rows) + (grid.spacing.y * Mathf.Max(0, rows - 1));
			float viewport_height = Mathf.Min(height, main_height);
			viewport_rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
			viewport_rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, viewport_height);
			if (viewport_layout != null)
			{
				viewport_layout.minWidth = width;
				viewport_layout.preferredWidth = width;
				viewport_layout.minHeight = viewport_height;
				viewport_layout.preferredHeight = viewport_height;
				viewport_layout.flexibleWidth = 0f;
				viewport_layout.flexibleHeight = 0f;
			}
			content_rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
			content_rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
			if (content_layout != null)
			{
				content_layout.minWidth = width;
				content_layout.preferredWidth = width;
				content_layout.minHeight = height;
				content_layout.preferredHeight = height;
				content_layout.flexibleWidth = 0f;
				content_layout.flexibleHeight = 0f;
			}
		}
		static float get_main_panel_height(BlackMarketView view, BlackMarketView.Mode mode)
		{
			MonoBehaviour? panel = (mode & BlackMarketView.Mode.Supply) == BlackMarketView.Mode.Supply
				? (MonoBehaviour?)I_supplyPanel.GetValue(view)
				: (MonoBehaviour?)I_demandPanel.GetValue(view);
			RectTransform? panel_rect = panel != null ? panel.GetComponent<RectTransform>() : null;
			if (panel_rect != null && panel_rect.rect.height > 0f)
			{
				return panel_rect.rect.height;
			}
			return 16f * 36f;
		}
		static float get_available_left_width(Transform parent)
		{
			RectTransform? parent_rect = parent as RectTransform;
			if (parent_rect != null && parent_rect.rect.width > 0f)
			{
				return parent_rect.rect.width;
			}
			return 4f * 40f;
		}
		static int calculate_columns(GridLayoutGroup grid, float available_width)
		{
			float usable_width = Mathf.Max(grid.cellSize.x, available_width - grid.padding.left - grid.padding.right);
			float column_width = grid.cellSize.x + grid.spacing.x;
			return Mathf.Max(1, Mathf.FloorToInt((usable_width + grid.spacing.x) / column_width));
		}
		static void copy_layout_component<T>(GameObject source, GameObject destination) where T : Component
		{
			T component = source.GetComponent<T>();
			if (component == null)
			{
				return;
			}
			T copy = destination.AddComponent<T>();
			JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(component), copy);
		}
		static void destroy_page_container(BlackMarketView view)
		{
			if (page_container_by_view.TryGetValue(view, out GameObject container) && container != null)
			{
				container.SetActive(false);
				UnityEngine.Object.Destroy(container);
			}
			page_container_by_view.Remove(view);
		}
		static float get_current_scroll_position(BlackMarketView view)
		{
			if (page_container_by_view.TryGetValue(view, out GameObject container) && container != null)
			{
				ScrollRect scroll_rect = container.GetComponent<ScrollRect>();
				if (scroll_rect != null)
				{
					return scroll_rect.verticalNormalizedPosition;
				}
			}
			if (scroll_position_by_view.TryGetValue(view, out float position))
			{
				return position;
			}
			return 1f;
		}
		static void show_page(BlackMarketView view, BlackMarketView.Mode mode, int page)
		{
			BlackMarket target = view.Target;
			if (target == null)
			{
				return;
			}
			requested_mode = mode;
			requested_page = page;
			has_requested_page = true;
			I_GenerateDemandsAndSupplies.Invoke(target, null);
			refresh(view);
		}
		class ScrollRectPositionSetter : MonoBehaviour
		{
			ScrollRect? scroll_rect;
			float vertical_position = 1f;
			bool waiting_for_render_pass;
			internal static void set_after_layout(ScrollRect scroll_rect, float vertical_position)
			{
				ScrollRectPositionSetter setter = scroll_rect.GetComponent<ScrollRectPositionSetter>();
				if (setter == null)
				{
					setter = scroll_rect.gameObject.AddComponent<ScrollRectPositionSetter>();
				}
				setter.scroll_rect = scroll_rect;
				setter.vertical_position = Mathf.Clamp01(vertical_position);
				setter.apply_now();
				setter.schedule_render_pass_correction();
			}
			void OnDestroy()
			{
				Canvas.willRenderCanvases -= on_will_render_canvases;
			}
			void apply_now()
			{
				if (scroll_rect == null)
				{
					return;
				}
				Canvas.ForceUpdateCanvases();
				force_rebuild(scroll_rect.content);
				force_rebuild(scroll_rect.viewport);
				force_rebuild(scroll_rect.transform as RectTransform);
				Canvas.ForceUpdateCanvases();
				scroll_rect.StopMovement();
				scroll_rect.verticalNormalizedPosition = vertical_position;
			}
			void schedule_render_pass_correction()
			{
				if (waiting_for_render_pass)
				{
					return;
				}
				waiting_for_render_pass = true;
				Canvas.willRenderCanvases += on_will_render_canvases;
			}
			void on_will_render_canvases()
			{
				Canvas.willRenderCanvases -= on_will_render_canvases;
				waiting_for_render_pass = false;
				apply_now();
				Destroy(this);
			}
			static void force_rebuild(RectTransform? rect_transform)
			{
				if (rect_transform != null)
				{
					LayoutRebuilder.ForceRebuildLayoutImmediate(rect_transform);
				}
			}
		}
	}
	public class ModBehaviour : Duckov.Modding.ModBehaviour
	{
		Harmony harmony = new Harmony("Super_Black_Market.Harmony");
		void Awake()
		{
			harmony.PatchAll(Assembly.GetExecutingAssembly());
		}
		void OnDestroy()
		{
			harmony.UnpatchAll(harmony.Id);
		}
	}
}
