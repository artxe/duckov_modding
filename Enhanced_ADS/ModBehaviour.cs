using Duckov.Options;
using Duckov.Utilities;
using HarmonyLib;
using System.Reflection;
using UnityEngine;
namespace Enhanced_ADS
{
	public class ModBehaviour : Duckov.Modding.ModBehaviour
	{
		public static Vector2 aim = Vector2.zero;
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

	[HarmonyPatch(typeof(GameCamera), "UpdateAimOffsetNormal")]
	internal class GameCamera_UpdateAimOffsetNormal
	{
		static FieldInfo I_offsetFromTargetX = AccessTools.Field(typeof(GameCamera), "offsetFromTargetX");
		static FieldInfo I_offsetFromTargetZ = AccessTools.Field(typeof(GameCamera), "offsetFromTargetZ");
		public static bool Prefix(GameCamera __instance, float deltaTime)
		{
			if (__instance.target && __instance.target.IsInAdsInput)
			{
				I_offsetFromTargetX.SetValue(__instance, ModBehaviour.aim.x);
				I_offsetFromTargetZ.SetValue(__instance, ModBehaviour.aim.y);
			}
			else
			{
				I_offsetFromTargetX.SetValue(__instance, 0f);
				I_offsetFromTargetZ.SetValue(__instance, 0f);
			}
			return false;
		}
	}

	[HarmonyPatch(typeof(InputManager), nameof(InputManager.SetAimInputUsingMouse))]
	internal class InputManager_SetAimInputUsingMouse
	{
		static MethodInfo Set_AimMousePosition = AccessTools.PropertySetter(typeof(InputManager), "AimMousePosition");
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
			mouseDelta *= OptionsManager.MouseSensitivity / 10f;
			Vector2 center = new Vector2(Screen.width * .5f, Screen.height * .5f);
			if (__instance.characterMainControl && __instance.characterMainControl.IsInAdsInput)
			{
				float ads_value = Mathf.Min(1, __instance.characterMainControl.AdsValue * 2);
				Vector2 screen_pos = (Vector2)I_aimScreenPoint.GetValue(__instance);
				bool aimingEnemyHead = false;
				ItemAgent_Gun gun = __instance.characterMainControl.GetGun();
				if (gun)
				{
					screen_pos = (Vector2)I_ProcessMousePosViaRecoil.Invoke(__instance, new object[] { screen_pos, mouseDelta, gun });
				}
				Vector2 next_screen_pos = screen_pos - (screen_pos - center) * ads_value;
				if (screen_pos != center)
				{
					Set_AimMousePosition.Invoke(__instance, new object[] { next_screen_pos });
					I_aimScreenPoint.SetValue(__instance, next_screen_pos);
				}
				Ray ray = LevelManager.Instance.GameCamera.renderCamera.ScreenPointToRay(next_screen_pos);
				Plane plane = new Plane(Vector3.up, Vector3.up * (__instance.characterMainControl.transform.position.y + 0.5f));
				plane.Raycast(ray, out var enter);
				Vector3 vector = ray.origin + ray.direction * enter;
				Debug.DrawLine(vector, vector + Vector3.up * 3f, Color.yellow);
				Vector3 aimPoint = vector;
				RaycastHit hittedHead = (RaycastHit)I_hittedHead.GetValue(__instance);
				if ((bool)gun && __instance.characterMainControl.CanControlAim())
				{
					if (Physics.Raycast(ray, out hittedHead, 100f, 1 << LayerMask.NameToLayer("HeadCollider")))
					{
						aimingEnemyHead = true;
					}
					I_hittedHead.SetValue(__instance, hittedHead);
					Vector3 position = __instance.characterMainControl.transform.position;
					if ((bool)gun)
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
							Physics.SphereCast(ray2, 0.02f, out var hittedCharacterDmgReceiverInfo, gun.BulletDistance, aimCheckLayers, QueryTriggerInteraction.Ignore)
							&& hittedCharacterDmgReceiverInfo.distance > 0.1f
							&& !Physics.SphereCast(ray2, 0.1f, out var _, hittedCharacterDmgReceiverInfo.distance, (LayerMask)I_obsticleLayers.GetValue(__instance), QueryTriggerInteraction.Ignore))
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
					aimPoint = hittedHead.point + direction * num3 * 0.5f;
				}
				I_aimingEnemyHead.SetValue(__instance, aimingEnemyHead);
				I_inputAimPoint.SetValue(__instance, vector);
        		__instance.characterMainControl.SetAimPoint(aimPoint);
				GameCamera game_camera = GameCamera.Instance;
				Vector2 delta_from = screen_pos - center;
				Vector2 delta_to = next_screen_pos - center;
				float f = game_camera.mainVCam.m_Lens.FieldOfView * Mathf.Deg2Rad;
				float d = Mathf.Abs(game_camera.mianCameraArm.distance);
				float p = (game_camera.mianCameraArm.pitch - 90f) * Mathf.Deg2Rad;
				float w_mouse = mouseDelta.y / Screen.height + .5f;
				float w_from = delta_from.y / Screen.height + .5f;
				float w_to = delta_to.y / Screen.height + .5f;
				float tan_half_fov = Mathf.Tan(f * .5f);
				float tan_p = Mathf.Tan(p);
				float sec_p = Mathf.Sqrt(1f + tan_p * tan_p);
				float y_over_z_mouse = tan_half_fov * (2f * w_mouse - 1f);
				float y_over_z_from = tan_half_fov * (2f * w_from - 1f);
				float y_over_z_to = tan_half_fov * (2f * w_to - 1f);
				float denom_mouse = 1f + tan_p * y_over_z_mouse;
				float denom_from = 1f + tan_p * y_over_z_from;
				float denom_to = 1f + tan_p * y_over_z_to;
				float mag_mouse = d * Mathf.Abs(y_over_z_mouse) / Mathf.Abs(denom_mouse) * sec_p;
				float mag_from = d * Mathf.Abs(y_over_z_from) / Mathf.Abs(denom_from) * sec_p;
				float mag_to = d * Mathf.Abs(y_over_z_to) / Mathf.Abs(denom_to) * sec_p;
				float sign_mouse = mouseDelta.y < 0f ? -1f : 1f;
				float sign_from = delta_from.y < 0f ? -1f : 1f;
				float sign_to = delta_to.y < 0f ? -1f : 1f;
				ModBehaviour.aim.x += (
					mouseDelta.x * d / denom_mouse
					+ delta_from.x * d / denom_from
					- delta_to.x * d / denom_to
				) * tan_half_fov * 2f / Screen.height;
				ModBehaviour.aim.y += sign_mouse * mag_mouse + sign_from * mag_from - sign_to * mag_to;
				float defaultAimOffset = (float)I_GameCamera_defaultAimOffset.GetValue(GameCamera.Instance) * 3;
				float maxAimOffset = gun
					? defaultAimOffset * gun.ADSAimDistanceFactor
					: defaultAimOffset * 1.25f;
				ModBehaviour.aim = Vector2.ClampMagnitude(
					ModBehaviour.aim,
					maxAimOffset
				);
				return false;
			}
			else if (ModBehaviour.aim != Vector2.zero)
			{
				GameCamera game_camera = GameCamera.Instance;
				float f = game_camera.mainVCam.m_Lens.FieldOfView * Mathf.Deg2Rad;
				float d = Mathf.Abs(game_camera.mianCameraArm.distance);
				float p = (game_camera.mianCameraArm.pitch - 90f) * Mathf.Deg2Rad;
				float m = ModBehaviour.aim.y;
				float tan_half_fov = Mathf.Tan(f * .5f);
				float tan_p = Mathf.Tan(p);
				float cos_p = 1f / Mathf.Sqrt(1f + tan_p * tan_p);
				float k = Mathf.Abs(m) / d * cos_p;
				float denom_k = 1f - k * k * tan_p * tan_p;
				float y_over_z = (k * k * tan_p + (m < 0f ? -k : k)) / denom_k;
				float denom = 1f + tan_p * y_over_z;
				float w = .5f * (1f + y_over_z / tan_half_fov);
				Vector2 screen_pos = new Vector2(
					center.x + ModBehaviour.aim.x * Screen.height * denom / d / tan_half_fov / 2f,
					center.y + (w - .5f) * Screen.height
				);
				Set_AimMousePosition.Invoke(__instance, new object[] { screen_pos });
				I_aimScreenPoint.SetValue(__instance, screen_pos);
				ModBehaviour.aim = Vector2.zero;
			}
			return true;
		}
	}
}