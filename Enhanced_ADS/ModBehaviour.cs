using Duckov.Options;
using Duckov.Utilities;
using HarmonyLib;
using System.Reflection;
using TMPro;
using UnityEngine;
namespace Enhanced_ADS
{
	public class ModBehaviour : Duckov.Modding.ModBehaviour
	{
		private Harmony harmony = new Harmony("Enhanced_ADS.Harmony");
		void Awake()
		{
			harmony.PatchAll(Assembly.GetExecutingAssembly());
		}
		void OnDestroy()
		{
			harmony.UnpatchAll(harmony.Id);
		}
	}

	public class State
	{
		public static Vector2 camera_offset = Vector2.zero;
		public static TextMeshProUGUI out_of_range;
	}

	[HarmonyPatch(typeof(AimMarker), "LateUpdate")]
	internal class AimMarker__LateUpdate
	{
		public static void Postfix(AimMarker __instance)
		{
			if (!State.out_of_range)
			{
				State.out_of_range = Object.Instantiate(GameplayDataSettings.UIStyle.TemplateTextUGUI);
				State.out_of_range.fontSize = 18;
				State.out_of_range.rectTransform.SetParent(__instance.aimMarkerUI, false);
				State.out_of_range.rectTransform.anchoredPosition = new Vector2(30f, 0f);
				State.out_of_range.rectTransform.pivot = new Vector2(0, 0);
			}
		}
	}

	[HarmonyPatch(typeof(GameCamera), "UpdateAimOffsetNormal")]
	internal class GameCamera__UpdateAimOffsetNormal
	{
		static FieldInfo I_offsetFromTargetX = AccessTools.Field(typeof(GameCamera), "offsetFromTargetX");
		static FieldInfo I_offsetFromTargetZ = AccessTools.Field(typeof(GameCamera), "offsetFromTargetZ");
		public static bool Prefix(GameCamera __instance, float deltaTime)
		{
			I_offsetFromTargetX.SetValue(__instance, State.camera_offset.x);
			I_offsetFromTargetZ.SetValue(__instance, State.camera_offset.y);
			return false;
		}
	}

	[HarmonyPatch(typeof(InputManager), nameof(InputManager.SetAimInputUsingMouse))]
	internal class InputManager__SetAimInputUsingMouse
	{
		static MethodInfo Get_AimMousePosition = AccessTools.PropertyGetter(typeof(InputManager), "AimMousePosition");
		static MethodInfo Set_AimMousePosition = AccessTools.PropertySetter(typeof(InputManager), "AimMousePosition");
		static MethodInfo Get_InputActived = AccessTools.PropertyGetter(typeof(InputManager), "InputActived");
		static FieldInfo I_aimCheckLayers = AccessTools.Field(typeof(InputManager), "aimCheckLayers");
		static FieldInfo I_aimingEnemyHead = AccessTools.Field(typeof(InputManager), "aimingEnemyHead");
		static FieldInfo I_aimScreenPoint = AccessTools.Field(typeof(InputManager), "aimScreenPoint");
		static FieldInfo I_hittedHead = AccessTools.Field(typeof(InputManager), "hittedHead");
		static FieldInfo I_inputAimPoint = AccessTools.Field(typeof(InputManager), "inputAimPoint");
		static FieldInfo I_obsticleLayers = AccessTools.Field(typeof(InputManager), "obsticleLayers");
		static MethodInfo I_ProcessMousePosViaRecoil = AccessTools.Method(typeof(InputManager), "ProcessMousePosViaRecoil");
		static FieldInfo I_GameCamera_defaultAimOffset = AccessTools.Field(typeof(GameCamera), "defaultAimOffset");
		public static bool Prefix(InputManager __instance, Vector2 mouseDelta)
		{
			if (!__instance.characterMainControl || !(bool)Get_InputActived.Invoke(null, null))
            {
                return true;
            }
			ItemAgent_Gun gun = __instance.characterMainControl.GetGun();
			if (!gun)
            {
                return true;
            }
			bool aimingEnemyHead = false;
			Vector2 screen_pos = (Vector2)I_ProcessMousePosViaRecoil.Invoke(__instance, new object[] {
				(Vector2)Get_AimMousePosition.Invoke(__instance, null) + mouseDelta * OptionsManager.MouseSensitivity / 10f, 
				mouseDelta,
				gun
			});
			if (__instance.characterMainControl.IsInAdsInput)
            {
				float scroll_offset = 15;
				if (screen_pos.x < scroll_offset || screen_pos.x > Screen.width - scroll_offset || screen_pos.y < scroll_offset || screen_pos.y > Screen.height - scroll_offset)
				{
					Vector2 camera_move = Vector2.zero;
					if (screen_pos.x < scroll_offset)
                    {
                        camera_move.x -= Time.deltaTime;
                    } else if (screen_pos.x > Screen.width - scroll_offset)
                    {
                        camera_move.x += Time.deltaTime;
                    }
					if (screen_pos.y < scroll_offset)
                    {
                        camera_move.y -= Time.deltaTime;
                    }
					else if (screen_pos.y > Screen.height - scroll_offset)
                    {
                        camera_move.y += Time.deltaTime;
                    }
					camera_move *= Screen.width * 2.5f;
					GameCamera game_camera = GameCamera.Instance;
					float f = game_camera.mainVCam.m_Lens.FieldOfView * Mathf.Deg2Rad;
					float d = Mathf.Abs(game_camera.mianCameraArm.distance);
					float p = (game_camera.mianCameraArm.pitch - 90f) * Mathf.Deg2Rad;
					float w = camera_move.y / Screen.height + .5f;
					float tan_half_fov = Mathf.Tan(f * .5f);
					float tan_p = Mathf.Tan(p);
					float sec_p = Mathf.Sqrt(1f + tan_p * tan_p);
					float y_over_z = tan_half_fov * (2f * w - 1f);
					float denom = 1f + tan_p * y_over_z;
					float mag = d * Mathf.Abs(y_over_z) / Mathf.Abs(denom) * sec_p;
					float sign = camera_move.y < 0f ? -1f : 1f;
					State.camera_offset.x += camera_move.x * d / denom * tan_half_fov * 2f / Screen.height;
					State.camera_offset.y += sign * mag;
					float default_aim_offset = (float)I_GameCamera_defaultAimOffset.GetValue(GameCamera.Instance);
					float max_aim_offset = default_aim_offset * gun.ADSAimDistanceFactor + gun.BulletDistance / 2;
					State.camera_offset = Vector2.ClampMagnitude(
						State.camera_offset,
						max_aim_offset
					);
				}
				State.out_of_range.gameObject.SetActive(true);
            }
			else
			{
				if (State.camera_offset != Vector2.zero) {
					GameCamera game_camera = GameCamera.Instance;
					float f = game_camera.mainVCam.m_Lens.FieldOfView * Mathf.Deg2Rad;
					float d = Mathf.Abs(game_camera.mianCameraArm.distance);
					float p = (game_camera.mianCameraArm.pitch - 90f) * Mathf.Deg2Rad;
					float m = State.camera_offset.y;
					float tan_half_fov = Mathf.Tan(f * .5f);
					float tan_p = Mathf.Tan(p);
					float cos_p = 1f / Mathf.Sqrt(1f + tan_p * tan_p);
					float k = Mathf.Abs(m) / d * cos_p;
					float denom_k = 1f - k * k * tan_p * tan_p;
					float y_over_z = (k * k * tan_p + (m < 0f ? -k : k)) / denom_k;
					float denom = 1f + tan_p * y_over_z;
					float w = .5f * (1f + y_over_z / tan_half_fov);
					screen_pos += new Vector2(
						Screen.height * State.camera_offset.x * denom / d / tan_half_fov * .5f,
						Screen.height * (w - .5f)
					);
					State.camera_offset = Vector2.zero;
                }
				State.out_of_range.gameObject.SetActive(false);
			}
			screen_pos.x = Mathf.Clamp(screen_pos.x, 0, Screen.width);
			screen_pos.y = Mathf.Clamp(screen_pos.y, 0, Screen.height);
			Set_AimMousePosition.Invoke(__instance, new object[] { screen_pos });
			I_aimScreenPoint.SetValue(__instance, screen_pos);
			Ray ray = LevelManager.Instance.GameCamera.renderCamera.ScreenPointToRay(screen_pos);
			Plane plane = new Plane(Vector3.up, Vector3.up * (__instance.characterMainControl.transform.position.y + .5f));
			plane.Raycast(ray, out var enter);
			Vector3 vector = ray.origin + ray.direction * enter;
			Debug.DrawLine(vector, vector + Vector3.up * 3f, Color.yellow);
			Vector3 aimPoint = vector;
			RaycastHit hittedHead = (RaycastHit)I_hittedHead.GetValue(__instance);
			if (gun && __instance.characterMainControl.CanControlAim())
			{
				if (Physics.Raycast(ray, out hittedHead, 100f, 1 << LayerMask.NameToLayer("HeadCollider")))
				{
					aimingEnemyHead = true;
				}
				I_hittedHead.SetValue(__instance, hittedHead);
				Vector3 position = __instance.characterMainControl.transform.position;
				if (gun)
				{
					position = gun.muzzle.transform.position;
				}
				Vector3 vector2 = vector - position;
				vector2.y = 0f;
				vector2.Normalize();
				Vector3 axis = Vector3.Cross(vector2, ray.direction);
				LayerMask aimCheckLayers = GameplayDataSettings.Layers.damageReceiverLayerMask;
				I_aimCheckLayers.SetValue(__instance, aimCheckLayers);
				for (int i = 0; i < 45f; i++)
				{
					int num = i;
					if (i > 23)
					{
						num = -(i - 23);
					}
					float num2 = 1.5f;
					Vector3 vector3 = Quaternion.AngleAxis(-2f * num, axis) * vector2;
					Ray ray2 = new Ray(position + num2 * vector3, vector3);
					if (
						Physics.SphereCast(ray2, .02f, out var hittedCharacterDmgReceiverInfo, gun.BulletDistance, aimCheckLayers, QueryTriggerInteraction.Ignore)
						&& hittedCharacterDmgReceiverInfo.distance > .1f
						&& !Physics.SphereCast(ray2, .1f, out var _, hittedCharacterDmgReceiverInfo.distance, (LayerMask)I_obsticleLayers.GetValue(__instance), QueryTriggerInteraction.Ignore))
					{
						aimPoint = hittedCharacterDmgReceiverInfo.point;
						break;
					}
				}
			}
			if (aimingEnemyHead)
			{
				Vector3 direction = ray.direction;
				Vector3 rhs = hittedHead.collider.transform.position - hittedHead.point;
				float num3 = Vector3.Dot(direction, rhs);
				aimPoint = hittedHead.point + direction * num3 * .5f;
			}
			I_aimingEnemyHead.SetValue(__instance, aimingEnemyHead);
			I_inputAimPoint.SetValue(__instance, vector);
			__instance.characterMainControl.SetAimPoint(aimPoint);
			if (__instance.characterMainControl.IsInAdsInput)
			{
				double distance = Vector3.Distance(gun.muzzle.position, vector);
				double range = gun.BulletDistance * .5f + .5f;
				State.out_of_range.color = distance < range
					? Color.white
					: distance < gun.BulletDistance
						? new Color(1f, .5f, 0f)
						: Color.red;
				State.out_of_range.text = $"{System.Math.Round(distance, 1)}/{System.Math.Round(range, 1)}M";
			}
			return false;
		}
	}

	[HarmonyPatch(typeof(ItemAgent_Gun), "TransToEmpty")]
	internal class ItemAgent_Gun__TransToEmpty
	{
		static FieldInfo I_gunState = AccessTools.Field(typeof(ItemAgent_Gun), "gunState");
		public static bool Prefix(ItemAgent_Gun __instance)
		{
			if (__instance.GunState == ItemAgent_Gun.GunStates.fire)
			{
				I_gunState.SetValue(__instance, ItemAgent_Gun.GunStates.empty);
				__instance.CharacterReload();
			}
			else
			{
				I_gunState.SetValue(__instance, ItemAgent_Gun.GunStates.empty);
			}
			return false;
		}
	}
}