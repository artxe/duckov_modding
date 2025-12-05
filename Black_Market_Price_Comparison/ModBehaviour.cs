using Duckov.BlackMarkets;
using Duckov.BlackMarkets.UI;
using HarmonyLib;
using System.Reflection;
using TMPro;
namespace Black_Market_Price_Comparison
{
	[HarmonyPatch(typeof(DemandPanel_Entry), "Setup")]
	internal class DemandPanel_Entry__Setup
	{
		static FieldInfo I_moneyDisplay = AccessTools.Field(typeof(DemandPanel_Entry), "moneyDisplay");
		static FieldInfo I_CostDisplay_money = AccessTools.Field(typeof(CostDisplay), "money");
		static FieldInfo I_DemandSupplyEntry_priceFactor = AccessTools.Field(typeof(BlackMarket.DemandSupplyEntry), "priceFactor");
		static void Postfix(DemandPanel_Entry __instance, BlackMarket.DemandSupplyEntry target)
		{
			float priceFactor = (float)I_DemandSupplyEntry_priceFactor.GetValue(target);
			TextMeshProUGUI moneyDisplay = (TextMeshProUGUI)I_moneyDisplay.GetValue(__instance);
			int.TryParse(moneyDisplay.text, out int num);
			moneyDisplay.text = num.ToString("#,0") + $"(x{priceFactor})";
		}
	}
	public class ModBehaviour : Duckov.Modding.ModBehaviour
	{
		Harmony harmony = new Harmony("Black_Market_Price_Comparison.Harmony");
		void Awake()
		{
			harmony.PatchAll(Assembly.GetExecutingAssembly());
		}
		void OnDestroy()
		{
			harmony.UnpatchAll(harmony.Id);
		}
	}
	[HarmonyPatch(typeof(SupplyPanel_Entry), "Setup")]
	internal class SupplyPanel_Entry__Setup
	{
		static FieldInfo I_costDisplay = AccessTools.Field(typeof(SupplyPanel_Entry), "costDisplay");
		static FieldInfo I_CostDisplay_money = AccessTools.Field(typeof(CostDisplay), "money");
		static FieldInfo I_DemandSupplyEntry_priceFactor = AccessTools.Field(typeof(BlackMarket.DemandSupplyEntry), "priceFactor");
		static void Postfix(SupplyPanel_Entry __instance, BlackMarket.DemandSupplyEntry target)
		{
			float priceFactor = (float)I_DemandSupplyEntry_priceFactor.GetValue(target);
			CostDisplay costDisplay = (CostDisplay)I_costDisplay.GetValue(__instance);
			TextMeshProUGUI money = (TextMeshProUGUI)I_CostDisplay_money.GetValue(costDisplay);
			money.text += $"(x{priceFactor})";
		}
	}
}