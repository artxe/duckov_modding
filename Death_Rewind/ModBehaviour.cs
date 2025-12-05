using Duckov;
using HarmonyLib;
using System.Reflection;
namespace Death_Rewind
{
	[HarmonyPatch(typeof(ItemShortcut), "OnCollectSaveData")]
	internal class ItemShortcut__OnCollectSaveData
	{
		static bool Prefix(ItemShortcut __instance)
		{
			if (LevelManager.Instance.IsRaidMap && CharacterMainControl.Main.Health.IsDead)
			{
				return false;
			}
			return true;
		}
	}
	[HarmonyPatch(typeof(LevelManager), "SaveMainCharacter")]
	internal class LevelManager__SaveMainCharacter
	{
		static FieldInfo I_dieTask = AccessTools.Field(typeof(LevelManager), "dieTask");
		static bool Prefix(LevelManager __instance)
		{
			if (
				!LevelConfig.SaveCharacter
				|| __instance.IsRaidMap && (bool)I_dieTask.GetValue(__instance)
			)
			{
				return false;
			}
			return true;
		}
	}
	public class ModBehaviour : Duckov.Modding.ModBehaviour
	{
		Harmony harmony = new Harmony("Death_Rewind.Harmony");
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