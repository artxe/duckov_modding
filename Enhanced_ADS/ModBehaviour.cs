using Duckov.Options;
using Duckov.Options.UI;
using Duckov.Utilities;
using HarmonyLib;
using SodaCraft.Localizations;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
namespace Enhanced_ADS
{
	[HarmonyPatch(typeof(AimMarker), "LateUpdate")]
	internal class AimMarker__LateUpdate
	{
		static void Postfix(AimMarker __instance)
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
		static bool Prefix(GameCamera __instance, float deltaTime)
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
		static FieldInfo I_inputMousePosition = AccessTools.Field(typeof(InputManager), "inputMousePosition");
		static FieldInfo I_obsticleLayers = AccessTools.Field(typeof(InputManager), "obsticleLayers");
		static MethodInfo I_ProcessMousePosViaRecoil = AccessTools.Method(typeof(InputManager), "ProcessMousePosViaRecoil");
		static FieldInfo I_GameCamera_defaultAimOffset = AccessTools.Field(typeof(GameCamera), "defaultAimOffset");
		static void Postfix(InputManager __instance, Vector2 mouseDelta)
		{
			if (__instance.characterMainControl && (bool)Get_InputActived.Invoke(null, null))
			{
				Mouse.current.WarpCursorPosition( __instance.AimScreenPoint);
			}
		}
		static bool Prefix(InputManager __instance, Vector2 mouseDelta)
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
				int screen_offset = 15;
				Vector2 center = new Vector2(Screen.width * .5f, Screen.height * .5f);
				if (State.ads_mode_type == OptionsProvider_ads_mode_type.Options.Trace_Aim_Point)
				{
					if (
						!State.tracking
						&& (
							screen_pos.x < screen_offset
							|| screen_pos.x > Screen.width - screen_offset
							|| screen_pos.y < screen_offset
							|| screen_pos.y > Screen.height - screen_offset
						)
					)
					{
						GameCamera game_camera = GameCamera.Instance;
						Vector2 predicted_offset = screen_pos - center;
						float f = game_camera.mainVCam.m_Lens.FieldOfView * Mathf.Deg2Rad;
						float d = Mathf.Abs(game_camera.mianCameraArm.distance);
						float p = (game_camera.mianCameraArm.pitch - 90f) * Mathf.Deg2Rad;
						float w = predicted_offset.y / Screen.height + .5f;
						float tan_half_fov = Mathf.Tan(f * .5f);
						float tan_p = Mathf.Tan(p);
						float sec_p = Mathf.Sqrt(1f + tan_p * tan_p);
						float y_over_z = tan_half_fov * (2f * w - 1f);
						float denom = 1f + tan_p * y_over_z;
						float mag = d * Mathf.Abs(y_over_z) / Mathf.Abs(denom) * sec_p;
						float sign = predicted_offset.y < 0f ? -1f : 1f;
						predicted_offset.x = predicted_offset.x * d / denom * tan_half_fov * 2f / Screen.height;
						predicted_offset.y = sign * mag;
						float range = gun.BulletDistance + .5f;
						if (range * range >= predicted_offset.sqrMagnitude)
						{
							State.tracking = true;
						}
					}
					if (State.tracking)
					{
						mouseDelta *= OptionsManager.MouseSensitivity / 10f;
						State.delta = Mathf.Min(1f, State.delta + Time.deltaTime * 3);
						Vector2 next_screen_pos = screen_pos - mouseDelta - (screen_pos - mouseDelta - center) * State.delta;
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
						State.camera_offset.x += (
							mouseDelta.x * d / denom_mouse
							+ delta_from.x * d / denom_from
							- delta_to.x * d / denom_to
						) * tan_half_fov * 2f / Screen.height;
						State.camera_offset.y += sign_mouse * mag_mouse + sign_from * mag_from - sign_to * mag_to;
						State.camera_offset = Vector2.ClampMagnitude(
							State.camera_offset,
							gun.BulletDistance + .5f
						);
						screen_pos = next_screen_pos;
					}
				}
				else if (State.ads_mode_type == OptionsProvider_ads_mode_type.Options.Scrollable)
				{
					if (
						screen_pos.x < screen_offset
						|| screen_pos.x > Screen.width - screen_offset
						|| screen_pos.y < screen_offset
						|| screen_pos.y > Screen.height - screen_offset
					)
					{
						Vector2 camera_move = Vector2.zero;
						if (screen_pos.x < screen_offset)
						{
							camera_move.x -= Time.deltaTime;
						} else if (screen_pos.x > Screen.width - screen_offset)
						{
							camera_move.x += Time.deltaTime;
						}
						if (screen_pos.y < screen_offset)
						{
							camera_move.y -= Time.deltaTime;
						}
						else if (screen_pos.y > Screen.height - screen_offset)
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
				State.delta = 0f;
				State.out_of_range.gameObject.SetActive(false);
				State.tracking = false;
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
					: distance < gun.BulletDistance + .5f
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
		static bool Prefix(ItemAgent_Gun __instance)
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
	public class OptionsProvider_ads_mode_type : OptionsProviderBase
	{
		public enum Options
		{
			Trace_Aim_Point = 0,
			Scrollable = 1
		}
		private static string[] options
		{
			get
			{
				switch (LocalizationManager.CurrentLanguage)
				{
					case SystemLanguage.ChineseSimplified:// 简体中文
						return new[]
						{
							"瞄准点为中心",
							"可滚动"
						};
					case SystemLanguage.ChineseTraditional:// 繁體中文
						return new[]
						{
							"以瞄準點為中心",
							"可滾動"
						};
					case SystemLanguage.English:// English
						return new[]
						{
							"Center of Aim Point",
							"Scrollable"
						};
					case SystemLanguage.French:// Français
						return new[]
						{
							"Centré sur le point visé",
							"Déroulable"
						};
					case SystemLanguage.German:// Deutsch
						return new[]
						{
							"Zielpunkt zentriert",
							"Scrollbar"
						};
					case SystemLanguage.Japanese:// 日本語
						return new[]
						{
							"照準点中心",
							"スクロール可能"
						};
					case SystemLanguage.Korean:// 한국어
						return new[]
						{
							"조준점 중심",
							"스크롤 가능"
						};
					case SystemLanguage.Portuguese:// Português (Brasil)
						return new[]
						{
							"Centralizar no ponto de mira",
							"Rolável"
						};
					case SystemLanguage.Russian:// Русский
						return new[]
						{
							"Центрировать по точке прицеливания",
							"Прокручиваемый"
						};
					case SystemLanguage.Spanish:// Español
						return new[]
						{
							"Centrado en el punto de mira",
							"Desplazable"
						};
					default:
						return new[]
						{
							"Center of Aim Point",
							"Scrollable"
						};
				}
			}
		}
		public override string Key => "Enhanced_ADS.ads_camera_type";
		public override string[] GetOptions()
		{
			return options;
		}
		public override string GetCurrentOption()
		{
			return options[(int)State.ads_mode_type];
		}
		public override void Set(int index)
		{
			State.ads_mode_type = (Options)index;
		}
	}
	[HarmonyPatch(typeof(OptionsUIEntry_Dropdown), "OnSetLanguage")]
	internal class OptionsUIEntry_Dropdown__OnSetLanguage
	{
		static FieldInfo I_label = AccessTools.Field(typeof(OptionsUIEntry_Dropdown), "label");
		static FieldInfo I_provider = AccessTools.Field(typeof(OptionsUIEntry_Dropdown), "provider");

		static void Postfix(OptionsUIEntry_Dropdown __instance, SystemLanguage language)
		{
			if (__instance.gameObject.name != "Enhanced_ADS.ads_mode_type")
			{
				return;
			}
			I_provider.SetValue(__instance, I_provider.GetValue(__instance));
			string label_text;
			switch (language)
			{
				case SystemLanguage.ChineseSimplified:// 简体中文
					label_text = "ADS 摄像机模式";
					break;
				case SystemLanguage.ChineseTraditional:// 繁體中文
					label_text = "ADS 攝影機模式";
					break;
				case SystemLanguage.English:// English
					label_text = "ADS Camera Mode";
					break;
				case SystemLanguage.French:// Français
					label_text = "Mode caméra ADS";
					break;
				case SystemLanguage.German:// Deutsch
					label_text = "ADS-Kameramodus";
					break;
				case SystemLanguage.Japanese:// 日本語
					label_text = "ADSカメラモード";
					break;
				case SystemLanguage.Korean:// 한국어
					label_text = "ADS 카메라 모드";
					break;
				case SystemLanguage.Portuguese:// Português (Brasil)
					label_text = "Modo de câmera ADS";
					break;
				case SystemLanguage.Russian:// Русский
					label_text = "Режим камеры ADS";
					break;
				case SystemLanguage.Spanish:// Español
					label_text = "Modo de cámara ADS";
					break;
				default:
					label_text = "ADS Camera Mode";
					break;
			}
			((TextMeshProUGUI)I_label.GetValue(__instance)).text = label_text;
		}
	}
	[HarmonyPatch(typeof(OptionsUIEntry_Dropdown), "Start")]
	internal class OptionsUIEntry_Dropdown__Start
	{
		static FieldInfo I_label = AccessTools.Field(typeof(OptionsUIEntry_Dropdown), "label");
		static FieldInfo I_provider = AccessTools.Field(typeof(OptionsUIEntry_Dropdown), "provider");
		static void Postfix(OptionsUIEntry_Dropdown __instance)
		{
			if (__instance.gameObject.name != "UI_HurtVisual")
			{
				return;
			}
			GameObject template = __instance.gameObject;
			Transform parent = template.transform.parent;
			GameObject ads_mode_option = Object.Instantiate(template, parent);
			ads_mode_option.name = "Enhanced_ADS.ads_mode_type";
			ads_mode_option.SetActive(true);
			OptionsUIEntry_Dropdown entry = ads_mode_option.GetComponent<OptionsUIEntry_Dropdown>();
			I_provider.SetValue(entry, ads_mode_option.AddComponent<OptionsProvider_ads_mode_type>());
			string label_text;
			switch (LocalizationManager.CurrentLanguage)
			{
				case SystemLanguage.ChineseSimplified:// 简体中文
					label_text = "ADS 摄像机模式";
					break;
				case SystemLanguage.ChineseTraditional:// 繁體中文
					label_text = "ADS 攝影機模式";
					break;
				case SystemLanguage.English:// English
					label_text = "ADS Camera Mode";
					break;
				case SystemLanguage.French:// Français
					label_text = "Mode caméra ADS";
					break;
				case SystemLanguage.German:// Deutsch
					label_text = "ADS-Kameramodus";
					break;
				case SystemLanguage.Japanese:// 日本語
					label_text = "ADSカメラモード";
					break;
				case SystemLanguage.Korean:// 한국어
					label_text = "ADS 카메라 모드";
					break;
				case SystemLanguage.Portuguese:// Português (Brasil)
					label_text = "Modo de câmera ADS";
					break;
				case SystemLanguage.Russian:// Русский
					label_text = "Режим камеры ADS";
					break;
				case SystemLanguage.Spanish:// Español
					label_text = "Modo de cámara ADS";
					break;
				default:
					label_text = "ADS Camera Mode";
					break;
			}
			((TextMeshProUGUI)I_label.GetValue(entry)).text = label_text;
		}
	}
	public class State
	{
		public static OptionsProvider_ads_mode_type.Options ads_mode_type
		{
			get => OptionsManager.Load("Enhanced_ADS.ads_mode_type", OptionsProvider_ads_mode_type.Options.Trace_Aim_Point);
			set => OptionsManager.Save("Enhanced_ADS.ads_mode_type", value);
		}
		public static Vector2 camera_offset = Vector2.zero;
		public static float delta = 0f;
		public static TextMeshProUGUI out_of_range;
		public static bool tracking = false;
	}
}