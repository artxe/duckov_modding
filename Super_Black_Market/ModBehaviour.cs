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
using SodaCraft.Localizations;
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
			List<int> candidates = get_accepted_items(__instance);
			if (candidates.Count == 0 && !BlackMarketPageController.has_search_text(__instance))
			{
				return true;
			}
			List<BlackMarket.DemandSupplyEntry> demands = (List<BlackMarket.DemandSupplyEntry>)I_demands.GetValue(__instance);
			List<BlackMarket.DemandSupplyEntry> supplies = (List<BlackMarket.DemandSupplyEntry>)I_supplies.GetValue(__instance);
			demands.Clear();
			supplies.Clear();
			int demands_count = (int)I_demandsCount.GetValue(__instance);
			int supplies_count = (int)I_suppliesCount.GetValue(__instance);
			if (BlackMarketPageController.try_consume_requested_page(out BlackMarketView.Mode mode, out int page))
			{
				int entries_count = (mode & BlackMarketView.Mode.Supply) == BlackMarketView.Mode.Supply
					? supplies_count
					: demands_count;
				selected_page = clamp_page(page, candidates.Count, entries_count);
			}
			if (candidates.Count > 0)
			{
				int demand_cursor = get_page_start(selected_page, candidates.Count, demands_count);
				int supply_cursor = get_page_start(selected_page, candidates.Count, supplies_count);
				add_entries(__instance, demands, candidates, ref demand_cursor, demands_count, I_demandFactorRand, I_demandBatchCountRand, use_max_price_factor: true);
				add_entries(__instance, supplies, candidates, ref supply_cursor, supplies_count, I_supplyFactorRand, I_supplyBatchCountRand, use_min_price_factor: true);
				next_demand_index = demand_cursor % candidates.Count;
				next_supply_index = supply_cursor % candidates.Count;
			}
			else
			{
				selected_page = 0;
				next_demand_index = 0;
				next_supply_index = 0;
			}
			((Action)I_onAfterGenerateEntries.GetValue(__instance))?.Invoke();
			if (LevelManager.LevelInited && !BlackMarketPageController.has_search_text(__instance))
			{
				I_SaveMainCharacter.Invoke(LevelManager.Instance, null);
				SavesSystem.CollectSaveData();
				SavesSystem.SaveFile();
			}
			return false;
		}
		internal static int get_page_count(BlackMarket black_market, BlackMarketView.Mode mode)
		{
			List<int> candidates = get_accepted_items(black_market);
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
		static List<int> get_accepted_items(BlackMarket black_market)
		{
			string[] search_terms = BlackMarketPageController.get_search_text(black_market)
				.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
			return ItemAssetsCollection.Instance.entries
				.Where(entry => entry != null && entry.metaData.id > 0)
				.OrderBy(entry => get_tag_order(entry.metaData))
				.ThenBy(entry => get_tag_sort_key(entry.metaData))
				.ThenBy(entry => entry.metaData.quality)
				.ThenBy(entry => entry.typeID)
				.Where(entry => is_accepted_item(entry.metaData))
				.Where(entry => matches_search(entry.metaData, search_terms))
				.Select(entry => entry.typeID)
				.ToList();
		}
		static bool matches_search(ItemMetaData meta_data, string[] search_terms)
		{
			if (search_terms.Length == 0)
			{
				return true;
			}
			string display_name = meta_data.DisplayName ?? string.Empty;
			string name = meta_data.Name ?? string.Empty;
			string display_name_key = meta_data.DisplayNameKey ?? string.Empty;
			return search_terms.All(term =>
				display_name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0
				|| name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0
				|| display_name_key.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
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
	[HarmonyPatch(typeof(BlackMarket), "Save")]
	internal class BlackMarket__Save
	{
		static bool Prefix(BlackMarket __instance)
		{
			return !BlackMarketPageController.has_search_text(__instance);
		}
	}
	[HarmonyPatch(typeof(BlackMarket), "OnDestroy")]
	internal class BlackMarket__OnDestroy
	{
		static void Postfix(BlackMarket __instance)
		{
			BlackMarketPageController.release_market(__instance);
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
	[HarmonyPatch(typeof(BlackMarketView), "OnClose")]
	internal class BlackMarketView__OnClose
	{
		static void Prefix(BlackMarketView __instance)
		{
			BlackMarketPageController.begin_close(__instance);
		}
		static void Postfix(BlackMarketView __instance)
		{
			BlackMarketPageController.close(__instance);
			BlackMarketPageController.release(__instance);
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
		static readonly FieldInfo I_btn_demandPanel = AccessTools.Field(typeof(BlackMarketView), "btn_demandPanel");
		static readonly FieldInfo I_btn_refresh = AccessTools.Field(typeof(BlackMarketView), "btn_refresh");
		static readonly FieldInfo I_btn_supplyPanel = AccessTools.Field(typeof(BlackMarketView), "btn_supplyPanel");
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
		const float search_input_height = 38f;
		const float search_input_spacing = 8f;
		const float fallback_catalog_gap = 16f;
		static readonly Dictionary<BlackMarketView, GameObject> control_container_by_view = new Dictionary<BlackMarketView, GameObject>();
		static readonly Dictionary<BlackMarketView, GameObject> page_container_by_view = new Dictionary<BlackMarketView, GameObject>();
		static readonly Dictionary<BlackMarketView, float> scroll_position_by_view = new Dictionary<BlackMarketView, float>();
		static readonly Dictionary<BlackMarketView, BlackMarket> market_by_view = new Dictionary<BlackMarketView, BlackMarket>();
		static readonly Dictionary<BlackMarketView, RefreshChromeState> refresh_chrome_state_by_view = new Dictionary<BlackMarketView, RefreshChromeState>();
		static readonly Dictionary<BlackMarket, string> search_text_by_market = new Dictionary<BlackMarket, string>();
		static readonly HashSet<BlackMarketView> reset_scroll_on_next_refresh = new HashSet<BlackMarketView>();
		static readonly HashSet<BlackMarketView> closing_views = new HashSet<BlackMarketView>();
		static BlackMarketView.Mode requested_mode;
		static int requested_page;
		static bool has_requested_page;
		static bool listening_for_language_change;
		internal static string get_search_text(BlackMarket black_market)
		{
			if (black_market != null && search_text_by_market.TryGetValue(black_market, out string search_text))
			{
				return search_text;
			}
			return string.Empty;
		}
		internal static bool has_search_text(BlackMarket black_market)
		{
			return !string.IsNullOrWhiteSpace(get_search_text(black_market));
		}
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
			BlackMarket target = view.Target;
			if (template == null || target == null)
			{
				return;
			}
			ensure_language_listener();
			if (market_by_view.TryGetValue(view, out BlackMarket previous_target) && previous_target != target)
			{
				search_text_by_market.Remove(previous_target);
			}
			market_by_view[view] = target;
			search_text_by_market[target] = string.Empty;
			BlackMarketViewLifecycle lifecycle = view.GetComponent<BlackMarketViewLifecycle>();
			if (lifecycle == null)
			{
				lifecycle = view.gameObject.AddComponent<BlackMarketViewLifecycle>();
			}
			lifecycle.setup(view);
			hide_refresh_chrome(view, template);
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
			if (target == null
				|| !market_by_view.TryGetValue(view, out BlackMarket installed_target)
				|| installed_target != target)
			{
				return;
			}
			Button template = (Button)I_btn_refresh.GetValue(view);
			if (template == null)
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
			if (parent == null)
			{
				return;
			}
			GameObject controls = ensure_control_container(view, template, pages_template, parent);
			GameObject container = create_page_container(view, mode, pages_template, controls.transform, parent, page_count);
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
			set_layer_recursively(controls, template.gameObject.layer);
			TMP_InputField search_input = controls.GetComponentInChildren<TMP_InputField>(true);
			if (search_input != null)
			{
				search_input.transform.SetAsLastSibling();
			}
			LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)controls.transform);
			ScrollRect scroll_rect = container.GetComponentInParent<ScrollRect>();
			if (scroll_rect != null)
			{
				ScrollRectPositionSetter.set_after_layout(scroll_rect, scroll_position);
				scroll_position_by_view[view] = scroll_position;
			}
		}
		static void hide_refresh_chrome(BlackMarketView view, Button refresh_button)
		{
			destroy_control_container(view);
			if (!refresh_chrome_state_by_view.TryGetValue(view, out RefreshChromeState state))
			{
				Canvas.ForceUpdateCanvases();
				if (refresh_button.transform.parent is RectTransform parent_rect)
				{
					LayoutRebuilder.ForceRebuildLayoutImmediate(parent_rect);
				}
				state = capture_refresh_chrome_state(view, refresh_button);
				refresh_chrome_state_by_view[view] = state;
			}
			state.hide();
		}
		static RefreshChromeState capture_refresh_chrome_state(BlackMarketView view, Button refresh_button)
		{
			RefreshChromeState state = new RefreshChromeState(refresh_button);
			state.add_hidden_object(((TextMeshProUGUI)I_refreshETAText.GetValue(view))?.gameObject);
			state.add_hidden_object(((TextMeshProUGUI)I_refreshChanceText.GetValue(view))?.gameObject);
			state.add_hidden_object((GameObject)I_refreshInteractableIndicator.GetValue(view));
			Transform parent = refresh_button.transform.parent;
			if (parent == null)
			{
				return state;
			}
			state.set_background(parent.GetComponent<Image>());
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
				state.add_hidden_object(text.gameObject);
			}
			return state;
		}
		static void restore_refresh_chrome(BlackMarketView view, bool forget_state)
		{
			if (refresh_chrome_state_by_view.TryGetValue(view, out RefreshChromeState state))
			{
				state.restore();
			}
			if (forget_state)
			{
				refresh_chrome_state_by_view.Remove(view);
			}
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
		static GameObject ensure_control_container(BlackMarketView view, Button refresh_button, PagesControl_Entry pages_template, Transform parent)
		{
			if (control_container_by_view.TryGetValue(view, out GameObject existing) && existing != null)
			{
				return existing;
			}
			int refresh_sibling_index = refresh_button.transform.GetSiblingIndex();
			GameObject controls = new GameObject("BlackMarketCatalogControls", typeof(RectTransform), typeof(LayoutElement));
			controls.layer = refresh_button.gameObject.layer;
			controls.transform.SetParent(parent, worldPositionStays: false);
			controls.transform.SetSiblingIndex(refresh_sibling_index);
			control_container_by_view[view] = controls;
			RectTransform refresh_rect = refresh_button.GetComponent<RectTransform>();
			RectTransform controls_rect = (RectTransform)controls.transform;
			if (refresh_rect != null)
			{
				copy_rect_transform(refresh_rect, controls_rect);
			}
			float initial_width = get_available_left_width(view, parent);
			controls_rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, initial_width);
			controls_rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, search_input_height);
			set_layout_size(controls.GetComponent<LayoutElement>(), initial_width, search_input_height);
			TMP_InputField input_field = create_search_input(refresh_button, pages_template, controls.transform);
			set_layout_size(input_field.GetComponent<LayoutElement>(), initial_width, search_input_height);
			input_field.SetTextWithoutNotify(get_search_text(view.Target));
			SearchInputController input_controller = controls.AddComponent<SearchInputController>();
			input_controller.setup(view, input_field);
			return controls;
		}
		static TMP_InputField create_search_input(Button refresh_button, PagesControl_Entry pages_template, Transform parent)
		{
			int ui_layer = refresh_button.gameObject.layer;
			GameObject input_object = new GameObject("BlackMarketSearch", typeof(RectTransform));
			input_object.layer = ui_layer;
			input_object.SetActive(false);
			input_object.transform.SetParent(parent, worldPositionStays: false);
			input_object.AddComponent<CanvasRenderer>();
			input_object.AddComponent<Image>();
			input_object.AddComponent<LayoutElement>();
			input_object.AddComponent<TMP_InputField>();
			RectTransform input_rect = (RectTransform)input_object.transform;
			input_rect.anchorMin = new Vector2(0f, 1f);
			input_rect.anchorMax = new Vector2(1f, 1f);
			input_rect.pivot = new Vector2(0.5f, 1f);
			input_rect.anchoredPosition = Vector2.zero;
			input_rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, search_input_height);
			LayoutElement input_layout = input_object.GetComponent<LayoutElement>();
			set_layout_size(input_layout, 0f, search_input_height);
			Image background = input_object.GetComponent<Image>();
			Button? page_button = get_page_button(pages_template);
			Image template_background = page_button?.targetGraphic as Image
				?? page_button?.GetComponent<Image>()
				?? refresh_button.targetGraphic as Image
				?? refresh_button.GetComponent<Image>();
			if (template_background != null)
			{
				background.sprite = template_background.sprite;
				background.type = template_background.type;
				background.material = template_background.material;
				background.color = Color.white;
				background.preserveAspect = template_background.preserveAspect;
			}
			else
			{
				background.color = new Color(0f, 0f, 0f, 0.7f);
			}
			GameObject text_area = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
			text_area.layer = ui_layer;
			text_area.transform.SetParent(input_object.transform, worldPositionStays: false);
			RectTransform text_area_rect = (RectTransform)text_area.transform;
			stretch_rect(text_area_rect, new Vector2(10f, 3f), new Vector2(-10f, -3f));
			TextMeshProUGUI template_text = (TextMeshProUGUI)I_text.GetValue(pages_template)
				?? refresh_button.GetComponentInChildren<TextMeshProUGUI>(true);
			TextMeshProUGUI placeholder = create_search_text("Placeholder", text_area.transform, template_text, ui_layer);
			placeholder.text = get_search_placeholder();
			placeholder.color = new Color(placeholder.color.r, placeholder.color.g, placeholder.color.b, placeholder.color.a * 0.55f);
			TextMeshProUGUI text = create_search_text("Text", text_area.transform, template_text, ui_layer);
			TMP_InputField input_field = input_object.GetComponent<TMP_InputField>();
			input_field.targetGraphic = background;
			input_field.textViewport = text_area_rect;
			input_field.textComponent = text;
			input_field.placeholder = placeholder;
			input_field.contentType = TMP_InputField.ContentType.Standard;
			input_field.lineType = TMP_InputField.LineType.SingleLine;
			input_field.characterValidation = TMP_InputField.CharacterValidation.None;
			input_field.richText = false;
			input_field.customCaretColor = true;
			input_field.caretColor = text.color;
			input_field.caretWidth = 2;
			input_field.selectionColor = new Color(0.3f, 0.55f, 0.9f, 0.75f);
			Button style_button = page_button ?? refresh_button;
			input_field.transition = style_button.transition;
			ColorBlock colors = style_button.colors;
			colors.normalColor = with_alpha(colors.normalColor, 1f);
			colors.highlightedColor = with_alpha(colors.highlightedColor, 1f);
			colors.pressedColor = with_alpha(colors.pressedColor, 1f);
			colors.selectedColor = with_alpha(colors.selectedColor, 1f);
			colors.disabledColor = with_alpha(colors.disabledColor, 1f);
			input_field.colors = colors;
			input_field.navigation = Navigation.defaultNavigation;
			text.overflowMode = TextOverflowModes.Overflow;
			input_object.SetActive(true);
			return input_field;
		}
		static Color with_alpha(Color color, float alpha)
		{
			return new Color(color.r, color.g, color.b, alpha);
		}
		static Button? get_page_button(PagesControl_Entry pages_template)
		{
			return (Button)I_button.GetValue(pages_template)
				?? pages_template.GetComponentInChildren<Button>(true);
		}
		static void set_layer_recursively(GameObject game_object, int layer)
		{
			game_object.layer = layer;
			foreach (Transform child in game_object.transform)
			{
				set_layer_recursively(child.gameObject, layer);
			}
		}
		static TextMeshProUGUI create_search_text(string name, Transform parent, TextMeshProUGUI template, int layer)
		{
			GameObject text_object = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
			text_object.layer = layer;
			text_object.transform.SetParent(parent, worldPositionStays: false);
			RectTransform rect = (RectTransform)text_object.transform;
			stretch_rect(rect, Vector2.zero, Vector2.zero);
			TextMeshProUGUI text = text_object.GetComponent<TextMeshProUGUI>();
			if (template != null)
			{
				text.font = template.font;
				text.fontSharedMaterial = template.fontSharedMaterial;
				text.fontSize = template.fontSize;
				text.enableAutoSizing = template.enableAutoSizing;
				text.fontSizeMin = template.fontSizeMin;
				text.fontSizeMax = template.fontSizeMax;
				text.fontStyle = template.fontStyle;
				text.fontWeight = template.fontWeight;
				text.color = new Color(template.color.r, template.color.g, template.color.b, 1f);
			}
			else
			{
				text.fontSize = 20f;
				text.color = Color.white;
			}
			text.alignment = TextAlignmentOptions.MidlineLeft;
			text.enableWordWrapping = false;
			text.overflowMode = TextOverflowModes.Ellipsis;
			text.raycastTarget = false;
			return text;
		}
		static string get_search_placeholder()
		{
			const string key = "UI_Interact_Find";
			string placeholder = LocalizationManager.ToPlainText(key);
			if (string.IsNullOrWhiteSpace(placeholder) || placeholder == key || placeholder == "*" + key + "*")
			{
				placeholder = "Search";
			}
			return placeholder + "...";
		}
		static void update_search_placeholder(TMP_InputField input_field)
		{
			if (input_field.placeholder is TMP_Text placeholder)
			{
				placeholder.text = get_search_placeholder();
			}
		}
		static void copy_rect_transform(RectTransform source, RectTransform destination)
		{
			destination.anchorMin = source.anchorMin;
			destination.anchorMax = source.anchorMax;
			destination.pivot = source.pivot;
			destination.anchoredPosition = source.anchoredPosition;
			destination.sizeDelta = source.sizeDelta;
			destination.localScale = source.localScale;
		}
		static void stretch_rect(RectTransform rect, Vector2 offset_min, Vector2 offset_max)
		{
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.pivot = new Vector2(0.5f, 0.5f);
			rect.offsetMin = offset_min;
			rect.offsetMax = offset_max;
		}
		static GameObject create_page_container(BlackMarketView view, BlackMarketView.Mode mode, PagesControl_Entry pages_template, Transform controls_parent, Transform sizing_parent, int page_count)
		{
			GameObject viewport = new GameObject("BlackMarketPages_Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(ScrollRect), typeof(LayoutElement));
			viewport.layer = controls_parent.gameObject.layer;
			viewport.transform.SetParent(controls_parent, worldPositionStays: false);
			viewport.transform.SetAsLastSibling();
			page_container_by_view[view] = viewport;
			RectTransform viewport_rect = (RectTransform)viewport.transform;
			viewport_rect.anchorMin = new Vector2(0f, 1f);
			viewport_rect.anchorMax = new Vector2(1f, 1f);
			viewport_rect.pivot = new Vector2(0.5f, 1f);
			viewport_rect.anchoredPosition = new Vector2(0f, -(search_input_height + search_input_spacing));
			GameObject container = new GameObject("BlackMarketPages_FromStorageLayout", typeof(RectTransform), typeof(LayoutElement));
			container.layer = controls_parent.gameObject.layer;
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
				resize_page_rects(
					view,
					mode,
					sizing_parent,
					(RectTransform)controls_parent,
					controls_parent.GetComponent<LayoutElement>(),
					viewport_rect,
					viewport.GetComponent<LayoutElement>(),
					container_rect,
					container.GetComponent<LayoutElement>(),
					container.GetComponent<GridLayoutGroup>(),
					page_count);
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
		static void resize_page_rects(
			BlackMarketView view,
			BlackMarketView.Mode mode,
			Transform sizing_parent,
			RectTransform controls_rect,
			LayoutElement? controls_layout,
			RectTransform viewport_rect,
			LayoutElement? viewport_layout,
			RectTransform content_rect,
			LayoutElement? content_layout,
			GridLayoutGroup grid,
			int page_count)
		{
			if (grid == null)
			{
				return;
			}
			Canvas.ForceUpdateCanvases();
			float base_spacing_y = grid.spacing.y;
			apply_page_layout(
				view,
				mode,
				sizing_parent,
				controls_rect,
				controls_layout,
				viewport_rect,
				viewport_layout,
				content_rect,
				content_layout,
				grid,
				page_count,
				base_spacing_y);
			CatalogLayoutRenderSetter.set_after_layout(
				view,
				mode,
				sizing_parent,
				controls_rect,
				controls_layout,
				viewport_rect,
				viewport_layout,
				content_rect,
				content_layout,
				grid,
				page_count,
				base_spacing_y);
		}
		static void apply_page_layout(
			BlackMarketView view,
			BlackMarketView.Mode mode,
			Transform sizing_parent,
			RectTransform controls_rect,
			LayoutElement? controls_layout,
			RectTransform viewport_rect,
			LayoutElement? viewport_layout,
			RectTransform content_rect,
			LayoutElement? content_layout,
			GridLayoutGroup grid,
			int page_count,
			float base_spacing_y)
		{
			if (view == null || controls_rect == null || viewport_rect == null || content_rect == null || grid == null)
			{
				return;
			}
			float minimum_gap = Mathf.Max(fallback_catalog_gap, Mathf.Max(grid.padding.top, grid.padding.bottom));
			float catalog_height = get_main_panel_height(view, mode);
			float target_top_world = get_rect_top_world(controls_rect) + (minimum_gap * get_world_y_scale(controls_rect));
			if (try_get_right_frame(view, mode, out RectTransform[] right_rects))
			{
				get_vertical_world_bounds(right_rects, out target_top_world, out float target_bottom_world);
				if (try_get_catalog_shells(view, right_rects, out _, out RectTransform[] shell_rects))
				{
					foreach (RectTransform shell_rect in shell_rects)
					{
						if (refresh_chrome_state_by_view.TryGetValue(view, out RefreshChromeState state))
						{
							state.set_shell(shell_rect);
						}
						align_vertical_world_bounds(shell_rect, target_top_world, target_bottom_world);
					}
				}
				if (try_get_vertical_bounds(right_rects, controls_rect, out float right_top, out float right_bottom))
				{
					catalog_height = right_top - right_bottom;
				}
			}
			float maximum_button_height = Mathf.Max(grid.cellSize.y, catalog_height - search_input_height - (minimum_gap * 3f));
			int visible_rows = calculate_visible_rows(maximum_button_height, grid.cellSize.y, base_spacing_y);
			float visible_button_height = (grid.cellSize.y * visible_rows) + (base_spacing_y * Mathf.Max(0, visible_rows - 1));
			float gap = Mathf.Max(minimum_gap, (catalog_height - search_input_height - visible_button_height) / 3f);
			grid.spacing = new Vector2(grid.spacing.x, base_spacing_y);
			float available_width = get_available_left_width(view, sizing_parent);
			int columns = calculate_columns(grid, available_width);
			grid.constraintCount = columns;
			int rows = Mathf.Max(1, Mathf.CeilToInt((float)Mathf.Max(1, page_count) / columns));
			float width = grid.padding.left + grid.padding.right + (grid.cellSize.x * columns) + (grid.spacing.x * Mathf.Max(0, columns - 1));
			float content_height = grid.padding.top + grid.padding.bottom + (grid.cellSize.y * rows) + (grid.spacing.y * Mathf.Max(0, rows - 1));
			float viewport_height = grid.padding.top + visible_button_height + grid.padding.bottom;
			float controls_height = Mathf.Max(search_input_height, catalog_height - (gap * 2f));
			controls_rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
			controls_rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, controls_height);
			set_layout_size(controls_layout, width, controls_height);
			float controls_scale_y = get_world_y_scale(controls_rect);
			align_rect_top_world(controls_rect, target_top_world - (gap * controls_scale_y));
			TMP_InputField search_input = controls_rect.GetComponentInChildren<TMP_InputField>(true);
			if (search_input != null)
			{
				RectTransform search_rect = search_input.GetComponent<RectTransform>();
				search_rect.anchorMin = new Vector2(0f, 1f);
				search_rect.anchorMax = new Vector2(1f, 1f);
				search_rect.pivot = new Vector2(0.5f, 1f);
				search_rect.anchoredPosition = Vector2.zero;
				search_rect.sizeDelta = new Vector2(0f, search_input_height);
			}
			viewport_rect.anchorMin = new Vector2(0f, 1f);
			viewport_rect.anchorMax = new Vector2(1f, 1f);
			viewport_rect.pivot = new Vector2(0.5f, 1f);
			viewport_rect.anchoredPosition = new Vector2(0f, -(search_input_height + gap - grid.padding.top));
			viewport_rect.sizeDelta = new Vector2(0f, viewport_height);
			set_layout_size(viewport_layout, width, viewport_height);
			content_rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
			content_rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, content_height);
			set_layout_size(content_layout, width, content_height);
		}
		static int calculate_visible_rows(float available_height, float cell_height, float base_spacing)
		{
			float row_pitch = Mathf.Max(1f, cell_height + Mathf.Max(0f, base_spacing));
			int rows = Mathf.Max(1, Mathf.FloorToInt((available_height + Mathf.Max(0f, base_spacing)) / row_pitch));
			while (rows > 1 && (cell_height * rows) > available_height)
			{
				rows--;
			}
			return rows;
		}
		static bool try_get_right_frame(BlackMarketView view, BlackMarketView.Mode mode, out RectTransform[] right_rects)
		{
			right_rects = Array.Empty<RectTransform>();
			Button demand_button = (Button)I_btn_demandPanel.GetValue(view);
			Button supply_button = (Button)I_btn_supplyPanel.GetValue(view);
			MonoBehaviour? panel = (mode & BlackMarketView.Mode.Supply) == BlackMarketView.Mode.Supply
				? (MonoBehaviour?)I_supplyPanel.GetValue(view)
				: (MonoBehaviour?)I_demandPanel.GetValue(view);
			RectTransform? panel_rect = panel != null ? panel.GetComponent<RectTransform>() : null;
			RectTransform? demand_rect = demand_button != null ? demand_button.GetComponent<RectTransform>() : null;
			RectTransform? supply_rect = supply_button != null ? supply_button.GetComponent<RectTransform>() : null;
			if (panel_rect == null || demand_rect == null || supply_rect == null)
			{
				return false;
			}
			right_rects = new[] { panel_rect, demand_rect, supply_rect };
			return true;
		}
		static bool try_get_catalog_shells(
			BlackMarketView view,
			RectTransform[] right_rects,
			out RectTransform visual_shell_rect,
			out RectTransform[] shell_rects)
		{
			visual_shell_rect = null!;
			shell_rects = Array.Empty<RectTransform>();
			Button refresh_button = (Button)I_btn_refresh.GetValue(view);
			if (refresh_button == null || right_rects.Length == 0)
			{
				return false;
			}
			Transform? common_ancestor = find_common_ancestor(
				refresh_button.transform,
				right_rects.Cast<Transform>().ToArray());
			if (common_ancestor == null || !(common_ancestor == view.transform || common_ancestor.IsChildOf(view.transform)))
			{
				return false;
			}
			Transform? left_branch = get_direct_child_under(common_ancestor, refresh_button.transform);
			if (!(left_branch is RectTransform candidate) || right_rects.Any(rect => rect == candidate || rect.IsChildOf(candidate)))
			{
				return false;
			}
			visual_shell_rect = find_visual_shell(view, candidate, refresh_button) ?? candidate;
			shell_rects = visual_shell_rect == candidate
				? new[] { candidate }
				: new[] { candidate, visual_shell_rect };
			return true;
		}
		static RectTransform? find_visual_shell(BlackMarketView view, RectTransform left_branch, Button refresh_button)
		{
			RectTransform? refresh_parent = refresh_button.transform.parent as RectTransform;
			if (refresh_parent == null)
			{
				return null;
			}
			get_rect_world_bounds(refresh_parent, out Vector2 refresh_min, out Vector2 refresh_max);
			float refresh_width = refresh_max.x - refresh_min.x;
			Vector2 refresh_center = (refresh_min + refresh_max) * 0.5f;
			RectTransform? best = null;
			float best_score = float.NegativeInfinity;
			control_container_by_view.TryGetValue(view, out GameObject controls);
			foreach (Image image in left_branch.GetComponentsInChildren<Image>(true))
			{
				RectTransform image_rect = image.rectTransform;
				if (!image.enabled || !image.gameObject.activeInHierarchy || image.color.a <= 0.05f)
				{
					continue;
				}
				if (image.GetComponent<Selectable>() != null
					|| image_rect == refresh_button.transform
					|| image_rect.IsChildOf(refresh_button.transform)
					|| (controls != null && (image_rect == controls.transform || image_rect.IsChildOf(controls.transform))))
				{
					continue;
				}
				get_rect_world_bounds(image_rect, out Vector2 image_min, out Vector2 image_max);
				float width = image_max.x - image_min.x;
				float height = image_max.y - image_min.y;
				float width_ratio = width / Mathf.Max(1f, refresh_width);
				if (width_ratio < 0.9f || width_ratio > 1.35f || height <= width)
				{
					continue;
				}
				if (refresh_center.x < image_min.x || refresh_center.x > image_max.x
					|| refresh_center.y < image_min.y || refresh_center.y > image_max.y)
				{
					continue;
				}
				int depth = 0;
				for (Transform? current = image_rect; current != null && current != left_branch; current = current.parent)
				{
					depth++;
				}
				float score = (image_rect == left_branch ? 500f : 300f)
					+ (200f - (Mathf.Abs(width_ratio - 1.08f) * 200f))
					- (depth * 10f);
				if (score > best_score)
				{
					best = image_rect;
					best_score = score;
				}
			}
			return best;
		}
		static Transform? find_common_ancestor(Transform first, params Transform[] others)
		{
			for (Transform? candidate = first; candidate != null; candidate = candidate.parent)
			{
				if (others.All(transform => transform == candidate || transform.IsChildOf(candidate)))
				{
					return candidate;
				}
			}
			return null;
		}
		static Transform? get_direct_child_under(Transform ancestor, Transform descendant)
		{
			Transform current = descendant;
			while (current.parent != null && current.parent != ancestor)
			{
				current = current.parent;
			}
			return current.parent == ancestor ? current : null;
		}
		static bool try_get_vertical_bounds(IEnumerable<RectTransform> rects, Transform relative_to, out float top, out float bottom)
		{
			top = float.NegativeInfinity;
			bottom = float.PositiveInfinity;
			Vector3[] corners = new Vector3[4];
			bool found = false;
			foreach (RectTransform rect in rects)
			{
				if (rect == null)
				{
					continue;
				}
				rect.GetWorldCorners(corners);
				foreach (Vector3 corner in corners)
				{
					float y = relative_to.InverseTransformPoint(corner).y;
					top = Mathf.Max(top, y);
					bottom = Mathf.Min(bottom, y);
				}
				found = true;
			}
			return found && top > bottom;
		}
		static void get_vertical_world_bounds(IEnumerable<RectTransform> rects, out float top, out float bottom)
		{
			top = float.NegativeInfinity;
			bottom = float.PositiveInfinity;
			Vector3[] corners = new Vector3[4];
			foreach (RectTransform rect in rects)
			{
				rect.GetWorldCorners(corners);
				foreach (Vector3 corner in corners)
				{
					top = Mathf.Max(top, corner.y);
					bottom = Mathf.Min(bottom, corner.y);
				}
			}
		}
		static void align_vertical_world_bounds(RectTransform rect, float target_top, float target_bottom)
		{
			float target_height = target_top - target_bottom;
			for (int i = 0; i < 2; i++)
			{
				float current_top = get_rect_top_world(rect);
				float current_bottom = get_rect_bottom_world(rect);
				float current_height = current_top - current_bottom;
				if (current_height <= 0.01f || target_height <= 0.01f)
				{
					return;
				}
				float local_height = rect.rect.height * (target_height / current_height);
				rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, local_height);
				align_rect_top_world(rect, target_top);
			}
			LayoutElement layout = rect.GetComponent<LayoutElement>();
			if (layout == null && rect.parent != null && rect.parent.GetComponent<LayoutGroup>() != null)
			{
				layout = rect.gameObject.AddComponent<LayoutElement>();
			}
			if (layout != null)
			{
				layout.minHeight = rect.rect.height;
				layout.preferredHeight = rect.rect.height;
				layout.flexibleHeight = 0f;
			}
		}
		static void align_rect_top_world(RectTransform rect, float target_top)
		{
			float delta = target_top - get_rect_top_world(rect);
			rect.position += Vector3.up * delta;
		}
		static void get_rect_world_bounds(RectTransform rect, out Vector2 minimum, out Vector2 maximum)
		{
			Vector3[] corners = new Vector3[4];
			rect.GetWorldCorners(corners);
			minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
			maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
			foreach (Vector3 corner in corners)
			{
				minimum = Vector2.Min(minimum, corner);
				maximum = Vector2.Max(maximum, corner);
			}
		}
		static float get_rect_top_world(RectTransform rect)
		{
			Vector3[] corners = new Vector3[4];
			rect.GetWorldCorners(corners);
			return (corners[1].y + corners[2].y) * 0.5f;
		}
		static float get_rect_bottom_world(RectTransform rect)
		{
			Vector3[] corners = new Vector3[4];
			rect.GetWorldCorners(corners);
			return (corners[0].y + corners[3].y) * 0.5f;
		}
		static float get_world_y_scale(RectTransform rect)
		{
			float scale = Mathf.Abs(rect.TransformPoint(Vector3.up).y - rect.TransformPoint(Vector3.zero).y);
			return Mathf.Max(0.0001f, scale);
		}
		static void set_layout_size(LayoutElement? layout, float width, float height)
		{
			if (layout == null)
			{
				return;
			}
			layout.minWidth = width;
			layout.preferredWidth = width;
			layout.minHeight = height;
			layout.preferredHeight = height;
			layout.flexibleWidth = 0f;
			layout.flexibleHeight = 0f;
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
		static float get_available_left_width(BlackMarketView view, Transform parent)
		{
			if (refresh_chrome_state_by_view.TryGetValue(view, out RefreshChromeState state)
				&& state.try_get_original_width(parent, out float original_width))
			{
				return original_width;
			}
			return get_available_left_width(parent);
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
		static void ensure_language_listener()
		{
			if (listening_for_language_change)
			{
				return;
			}
			LocalizationManager.OnSetLanguage += on_set_language;
			listening_for_language_change = true;
		}
		static void on_set_language(SystemLanguage language)
		{
			foreach (KeyValuePair<BlackMarketView, GameObject> pair in control_container_by_view.ToList())
			{
				BlackMarketView view = pair.Key;
				GameObject controls = pair.Value;
				if (controls == null)
				{
					continue;
				}
				SearchInputController input_controller = controls.GetComponent<SearchInputController>();
				input_controller?.update_placeholder();
				if (view != null && view.gameObject.activeInHierarchy && has_search_text(view.Target))
				{
					regenerate_search(view);
				}
			}
		}
		static void apply_search(BlackMarketView view, string value)
		{
			BlackMarket target = view.Target;
			if (target == null)
			{
				return;
			}
			string search_text = (value ?? string.Empty).Trim();
			if (string.Equals(get_search_text(target), search_text, StringComparison.Ordinal))
			{
				return;
			}
			search_text_by_market[target] = search_text;
			regenerate_search(view);
		}
		static void regenerate_search(BlackMarketView view)
		{
			BlackMarketView.Mode mode = (BlackMarketView.Mode)I_mode.GetValue(view);
			reset_scroll_on_next_refresh.Add(view);
			if (mode == BlackMarketView.Mode.None)
			{
				refresh(view);
				return;
			}
			show_page(view, mode, 0);
		}
		internal static void begin_close(BlackMarketView view)
		{
			closing_views.Add(view);
		}
		internal static bool is_closing(BlackMarketView view)
		{
			return closing_views.Contains(view);
		}
		internal static void close(BlackMarketView view)
		{
			closing_views.Add(view);
			try
			{
				BlackMarket target = view.Target;
				if (control_container_by_view.TryGetValue(view, out GameObject controls) && controls != null)
				{
					controls.GetComponent<SearchInputController>()?.reset_text();
				}
				if (target == null || !has_search_text(target))
				{
					return;
				}
				search_text_by_market[target] = string.Empty;
				requested_mode = (BlackMarketView.Mode)I_mode.GetValue(view);
				requested_page = 0;
				has_requested_page = true;
				I_GenerateDemandsAndSupplies.Invoke(target, null);
			}
			finally
			{
				closing_views.Remove(view);
			}
		}
		internal static void release(BlackMarketView view, bool forget_chrome_state = false)
		{
			destroy_control_container(view);
			restore_refresh_chrome(view, forget_chrome_state);
			scroll_position_by_view.Remove(view);
			reset_scroll_on_next_refresh.Remove(view);
			if (market_by_view.TryGetValue(view, out BlackMarket market))
			{
				if (!has_search_text(market))
				{
					search_text_by_market.Remove(market);
				}
			}
			market_by_view.Remove(view);
		}
		internal static void release_market(BlackMarket market)
		{
			search_text_by_market.Remove(market);
		}
		internal static void uninstall_all()
		{
			if (listening_for_language_change)
			{
				LocalizationManager.OnSetLanguage -= on_set_language;
				listening_for_language_change = false;
			}
			List<BlackMarketView> views = control_container_by_view.Keys
				.Concat(page_container_by_view.Keys)
				.Concat(market_by_view.Keys)
				.Concat(refresh_chrome_state_by_view.Keys)
				.Distinct()
				.ToList();
			foreach (BlackMarketView view in views)
			{
				if (view != null)
				{
					close(view);
				}
			}
			foreach (BlackMarketView view in views)
			{
				release(view, forget_chrome_state: true);
			}
			control_container_by_view.Clear();
			page_container_by_view.Clear();
			scroll_position_by_view.Clear();
			market_by_view.Clear();
			refresh_chrome_state_by_view.Clear();
			search_text_by_market.Clear();
			reset_scroll_on_next_refresh.Clear();
			closing_views.Clear();
			has_requested_page = false;
		}
		static void destroy_control_container(BlackMarketView view)
		{
			destroy_page_container(view);
			if (control_container_by_view.TryGetValue(view, out GameObject controls) && controls != null)
			{
				controls.GetComponent<SearchInputController>()?.disconnect();
				controls.SetActive(false);
				UnityEngine.Object.Destroy(controls);
			}
			control_container_by_view.Remove(view);
		}
		static void destroy_page_container(BlackMarketView view)
		{
			if (page_container_by_view.TryGetValue(view, out GameObject container) && container != null)
			{
				container.GetComponent<CatalogLayoutRenderSetter>()?.disconnect();
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
		class RefreshChromeState
		{
			readonly Button refresh_button;
			readonly bool refresh_button_active;
			readonly Transform? controls_parent;
			readonly float controls_parent_width;
			readonly Dictionary<GameObject, HiddenObjectState> hidden_objects = new Dictionary<GameObject, HiddenObjectState>();
			Image? background;
			bool background_enabled;
			readonly List<RectTransformState> shell_states = new List<RectTransformState>();
			internal RefreshChromeState(Button refresh_button)
			{
				this.refresh_button = refresh_button;
				refresh_button_active = refresh_button.gameObject.activeSelf;
				controls_parent = refresh_button.transform.parent;
				controls_parent_width = controls_parent != null
					? get_available_left_width(controls_parent)
					: 0f;
			}
			internal bool try_get_original_width(Transform parent, out float width)
			{
				width = controls_parent_width;
				return parent == controls_parent && width > 0f;
			}
			internal void add_hidden_object(GameObject? game_object)
			{
				if (game_object != null && !hidden_objects.ContainsKey(game_object))
				{
					hidden_objects[game_object] = new HiddenObjectState(game_object);
				}
			}
			internal void set_background(Image? target_background)
			{
				background = target_background;
				background_enabled = target_background != null && target_background.enabled;
			}
			internal void set_shell(RectTransform shell_rect)
			{
				if (shell_rect == null)
				{
					return;
				}
				RectTransformState? shell_state = shell_states.FirstOrDefault(state => state.is_target(shell_rect));
				if (shell_state == null)
				{
					shell_state = new RectTransformState(shell_rect);
					shell_states.Add(shell_state);
				}
				shell_state.prepare_for_manual_height();
			}
			internal void hide()
			{
				if (refresh_button != null)
				{
					refresh_button.gameObject.SetActive(false);
				}
				if (background != null)
				{
					background.enabled = false;
				}
				foreach (HiddenObjectState state in hidden_objects.Values)
				{
					state.hide();
				}
			}
			internal void restore()
			{
				for (int i = shell_states.Count - 1; i >= 0; i--)
				{
					shell_states[i].restore();
				}
				foreach (HiddenObjectState state in hidden_objects.Values)
				{
					state.restore();
				}
				if (background != null)
				{
					background.enabled = background_enabled;
				}
				if (refresh_button != null)
				{
					refresh_button.gameObject.SetActive(refresh_button_active);
				}
			}
		}
		class RectTransformState
		{
			readonly RectTransform rect;
			readonly Vector2 anchor_min;
			readonly Vector2 anchor_max;
			readonly Vector2 pivot;
			readonly Vector3 anchored_position;
			readonly Vector2 size_delta;
			readonly Vector3 local_scale;
			readonly bool had_layout_element;
			readonly bool ignore_layout;
			readonly float min_height;
			readonly float preferred_height;
			readonly float flexible_height;
			readonly ContentSizeFitter? content_size_fitter;
			readonly ContentSizeFitter.FitMode vertical_fit;
			internal RectTransformState(RectTransform target_rect)
			{
				rect = target_rect;
				anchor_min = target_rect.anchorMin;
				anchor_max = target_rect.anchorMax;
				pivot = target_rect.pivot;
				anchored_position = target_rect.anchoredPosition3D;
				size_delta = target_rect.sizeDelta;
				local_scale = target_rect.localScale;
				LayoutElement layout = target_rect.GetComponent<LayoutElement>();
				had_layout_element = layout != null;
				ignore_layout = layout != null && layout.ignoreLayout;
				min_height = layout != null ? layout.minHeight : -1f;
				preferred_height = layout != null ? layout.preferredHeight : -1f;
				flexible_height = layout != null ? layout.flexibleHeight : -1f;
				content_size_fitter = target_rect.GetComponent<ContentSizeFitter>();
				vertical_fit = content_size_fitter != null
					? content_size_fitter.verticalFit
					: ContentSizeFitter.FitMode.Unconstrained;
			}
			internal bool is_target(RectTransform target_rect)
			{
				return rect == target_rect;
			}
			internal void prepare_for_manual_height()
			{
				if (content_size_fitter != null)
				{
					content_size_fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
				}
			}
			internal void restore()
			{
				if (rect == null)
				{
					return;
				}
				LayoutElement layout = rect.GetComponent<LayoutElement>();
				if (had_layout_element && layout != null)
				{
					layout.ignoreLayout = ignore_layout;
					layout.minHeight = min_height;
					layout.preferredHeight = preferred_height;
					layout.flexibleHeight = flexible_height;
				}
				else if (!had_layout_element && layout != null)
				{
					layout.ignoreLayout = true;
					UnityEngine.Object.Destroy(layout);
				}
				if (content_size_fitter != null)
				{
					content_size_fitter.verticalFit = vertical_fit;
				}
				rect.anchorMin = anchor_min;
				rect.anchorMax = anchor_max;
				rect.pivot = pivot;
				rect.anchoredPosition3D = anchored_position;
				rect.sizeDelta = size_delta;
				rect.localScale = local_scale;
				if (rect.parent is RectTransform parent_rect)
				{
					LayoutRebuilder.MarkLayoutForRebuild(parent_rect);
				}
			}
		}
		class HiddenObjectState
		{
			readonly GameObject game_object;
			readonly bool active;
			readonly bool had_layout_element;
			readonly bool ignore_layout;
			internal HiddenObjectState(GameObject game_object)
			{
				this.game_object = game_object;
				active = game_object.activeSelf;
				LayoutElement layout_element = game_object.GetComponent<LayoutElement>();
				had_layout_element = layout_element != null;
				ignore_layout = layout_element != null && layout_element.ignoreLayout;
			}
			internal void hide()
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
			internal void restore()
			{
				if (game_object == null)
				{
					return;
				}
				LayoutElement layout_element = game_object.GetComponent<LayoutElement>();
				if (layout_element != null)
				{
					layout_element.ignoreLayout = had_layout_element && ignore_layout;
					if (!had_layout_element)
					{
						UnityEngine.Object.Destroy(layout_element);
					}
				}
				game_object.SetActive(active);
			}
		}
		class CatalogLayoutRenderSetter : MonoBehaviour
		{
			BlackMarketView? view;
			BlackMarketView.Mode mode;
			Transform? sizing_parent;
			RectTransform? controls_rect;
			LayoutElement? controls_layout;
			RectTransform? viewport_rect;
			LayoutElement? viewport_layout;
			RectTransform? content_rect;
			LayoutElement? content_layout;
			GridLayoutGroup? grid;
			int page_count;
			float base_spacing_y;
			int corrections_remaining;
			internal static void set_after_layout(
				BlackMarketView view,
				BlackMarketView.Mode mode,
				Transform sizing_parent,
				RectTransform controls_rect,
				LayoutElement? controls_layout,
				RectTransform viewport_rect,
				LayoutElement? viewport_layout,
				RectTransform content_rect,
				LayoutElement? content_layout,
				GridLayoutGroup grid,
				int page_count,
				float base_spacing_y)
			{
				CatalogLayoutRenderSetter setter = viewport_rect.GetComponent<CatalogLayoutRenderSetter>();
				if (setter == null)
				{
					setter = viewport_rect.gameObject.AddComponent<CatalogLayoutRenderSetter>();
				}
				setter.view = view;
				setter.mode = mode;
				setter.sizing_parent = sizing_parent;
				setter.controls_rect = controls_rect;
				setter.controls_layout = controls_layout;
				setter.viewport_rect = viewport_rect;
				setter.viewport_layout = viewport_layout;
				setter.content_rect = content_rect;
				setter.content_layout = content_layout;
				setter.grid = grid;
				setter.page_count = page_count;
				setter.base_spacing_y = base_spacing_y;
				setter.corrections_remaining = 4;
				Canvas.willRenderCanvases -= setter.on_will_render_canvases;
				Canvas.willRenderCanvases += setter.on_will_render_canvases;
			}
			void OnDestroy()
			{
				Canvas.willRenderCanvases -= on_will_render_canvases;
			}
			internal void disconnect()
			{
				Canvas.willRenderCanvases -= on_will_render_canvases;
				view = null;
				sizing_parent = null;
				controls_rect = null;
				viewport_rect = null;
				content_rect = null;
				grid = null;
				corrections_remaining = 0;
			}
			void on_will_render_canvases()
			{
				if (view == null || sizing_parent == null || controls_rect == null || viewport_rect == null || content_rect == null || grid == null)
				{
					Canvas.willRenderCanvases -= on_will_render_canvases;
					Destroy(this);
					return;
				}
				apply_page_layout(
					view,
					mode,
					sizing_parent,
					controls_rect,
					controls_layout,
					viewport_rect,
					viewport_layout,
					content_rect,
					content_layout,
					grid,
					page_count,
					base_spacing_y);
				corrections_remaining--;
				if (corrections_remaining <= 0)
				{
					Canvas.willRenderCanvases -= on_will_render_canvases;
					Destroy(this);
				}
			}
		}
		class SearchInputController : MonoBehaviour
		{
			const float debounce_seconds = 0.3f;
			BlackMarketView? view;
			TMP_InputField? input_field;
			string pending_text = string.Empty;
			float apply_time;
			bool has_pending_text;
			internal void setup(BlackMarketView target_view, TMP_InputField target_input_field)
			{
				disconnect();
				view = target_view;
				input_field = target_input_field;
				pending_text = target_input_field.text;
				update_placeholder();
				target_input_field.onValueChanged.AddListener(on_value_changed);
				target_input_field.onEndEdit.AddListener(on_end_edit);
			}
			internal void disconnect()
			{
				if (input_field != null)
				{
					input_field.onValueChanged.RemoveListener(on_value_changed);
					input_field.onEndEdit.RemoveListener(on_end_edit);
				}
				has_pending_text = false;
				view = null;
				input_field = null;
			}
			internal void update_placeholder()
			{
				if (input_field != null)
				{
					update_search_placeholder(input_field);
				}
			}
			internal void reset_text()
			{
				has_pending_text = false;
				pending_text = string.Empty;
				if (input_field != null)
				{
					input_field.SetTextWithoutNotify(string.Empty);
					input_field.DeactivateInputField(true);
				}
			}
			void OnDestroy()
			{
				disconnect();
			}
			void Update()
			{
				if (!has_pending_text || Time.unscaledTime < apply_time)
				{
					return;
				}
				if (input_field != null && input_field.isFocused && !string.IsNullOrEmpty(Input.compositionString))
				{
					return;
				}
				apply_pending_text();
			}
			void on_value_changed(string value)
			{
				pending_text = value;
				apply_time = Time.unscaledTime + debounce_seconds;
				has_pending_text = true;
			}
			void on_end_edit(string value)
			{
				pending_text = value;
				has_pending_text = true;
				apply_pending_text();
			}
			void apply_pending_text()
			{
				has_pending_text = false;
				if (view != null && view.gameObject.activeInHierarchy && !is_closing(view))
				{
					apply_search(view, pending_text);
				}
			}
		}
		class BlackMarketViewLifecycle : MonoBehaviour
		{
			BlackMarketView? view;
			internal void setup(BlackMarketView target_view)
			{
				view = target_view;
			}
			void OnDestroy()
			{
				if (!ReferenceEquals(view, null))
				{
					release(view!, forget_chrome_state: true);
				}
			}
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
			BlackMarketPageController.uninstall_all();
			harmony.UnpatchAll(harmony.Id);
		}
	}
}
