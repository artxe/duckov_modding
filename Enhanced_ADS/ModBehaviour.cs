using Duckov.Options;
using Duckov.Options.UI;
using Duckov.Utilities;
using HarmonyLib;
using Saves;
using SodaCraft.Localizations;
using System.IO;
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
			if (
				__instance.characterMainControl
				&& (bool)Get_InputActived.Invoke(null, null)
				&& Application.isFocused
			)
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
			mouseDelta *= OptionsManager.MouseSensitivity / 10f;
			float max_range = gun.BulletDistance + .5f;
			Vector3 char_pos = __instance.characterMainControl.transform.position;
			Vector2 aim_pos = (Vector2)I_ProcessMousePosViaRecoil.Invoke(__instance, new object[] {
				(Vector2)Get_AimMousePosition.Invoke(__instance, null), 
				mouseDelta,
				gun
			});
			bool aimingEnemyHead = false;
			if (__instance.characterMainControl.IsInAdsInput)
			{
				int screen_edge_offset = 15;
				Vector2 center = new Vector2(Screen.width * .5f, Screen.height * .5f);
				if (State.ads_mode_type == OptionsProvider_ads_mode_type.Options.Adaptive_Sensitivity)
				{
					Vector2 max_offset = world_offset_to_screen(
						new Vector2(max_range, max_range) + screen_offset_to_world(-new Vector2(Screen.width * .5f - screen_edge_offset, Screen.height * .5f - screen_edge_offset))
					);
					float x_mul = (Screen.width * .5f - screen_edge_offset) / (Screen.width * .5f - screen_edge_offset + max_offset.x);
					float y_mul = (Screen.height * .5f - screen_edge_offset) / (Screen.height * .5f - screen_edge_offset + max_offset.y);
					Vector2 delta = aim_pos - center;
					if (State.camera_offset == Vector2.zero && delta != Vector2.zero)
					{
						delta += mouseDelta;
						delta.x *= x_mul;
						delta.y *= y_mul;
						aim_pos = center + delta;
					}
					else
					{
						mouseDelta.x *= x_mul;
						mouseDelta.y *= y_mul;
						aim_pos += mouseDelta;
						delta += mouseDelta;
					}
					Vector2 ratio = new Vector2(
						delta.x / (Screen.width * .5f - screen_edge_offset),
						delta.y / (Screen.height * .5f - screen_edge_offset)
					);
					State.camera_offset = -screen_offset_to_world(-ratio * max_offset);
				}
				else if (State.ads_mode_type == OptionsProvider_ads_mode_type.Options.Trace_Aim_Point)
				{
					if (
						!State.tracking
						&& (
							aim_pos.x < screen_edge_offset
							|| aim_pos.x > Screen.width - screen_edge_offset
							|| aim_pos.y < screen_edge_offset
							|| aim_pos.y > Screen.height - screen_edge_offset
						)
					)
					{
						Vector2 predicted_offset = screen_offset_to_world(aim_pos - center + mouseDelta);
						if (max_range * max_range >= predicted_offset.sqrMagnitude)
						{
							State.tracking = true;
						}
					}
					if (State.tracking)
					{
						State.delta = Mathf.Min(1f, State.delta + Time.deltaTime * 3);
						Vector2 next_aim_pos = aim_pos - (aim_pos - center) * State.delta;
						Vector2 delta_from = aim_pos - center;
						Vector2 delta_to = next_aim_pos - center;
						State.camera_offset += screen_offset_to_world(mouseDelta) + screen_offset_to_world(delta_from) - screen_offset_to_world(delta_to);
						State.camera_offset = Vector2.ClampMagnitude(State.camera_offset, max_range);
						aim_pos = next_aim_pos;
					}
					else
					{
						aim_pos += mouseDelta;
					}
				}
				else if (State.ads_mode_type == OptionsProvider_ads_mode_type.Options.Scrollable)
				{
					aim_pos += mouseDelta;
					if (
						aim_pos.x < screen_edge_offset
						|| aim_pos.x > Screen.width - screen_edge_offset
						|| aim_pos.y < screen_edge_offset
						|| aim_pos.y > Screen.height - screen_edge_offset
					)
					{
						Vector2 camera_move = Vector2.zero;
						if (aim_pos.x < screen_edge_offset)
						{
							camera_move.x -= Time.deltaTime;
						} else if (aim_pos.x > Screen.width - screen_edge_offset)
						{
							camera_move.x += Time.deltaTime;
						}
						if (aim_pos.y < screen_edge_offset)
						{
							camera_move.y -= Time.deltaTime;
						}
						else if (aim_pos.y > Screen.height - screen_edge_offset)
						{
							camera_move.y += Time.deltaTime;
						}
						State.camera_offset += screen_offset_to_world(camera_move * Screen.width * 2.5f);
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
				aim_pos += mouseDelta;
				if (State.camera_offset != Vector2.zero && State.ads_mode_type == OptionsProvider_ads_mode_type.Options.Trace_Aim_Point)
				{
					aim_pos += world_offset_to_screen(State.camera_offset);
				}
				State.camera_offset = Vector2.zero;
				State.delta = 0f;
				State.out_of_range.gameObject.SetActive(false);
				State.tracking = false;
			}
			aim_pos.x = Mathf.Clamp(aim_pos.x, 0, Screen.width);
			aim_pos.y = Mathf.Clamp(aim_pos.y, 0, Screen.height);
			Set_AimMousePosition.Invoke(__instance, new object[] { aim_pos });
			I_aimScreenPoint.SetValue(__instance, aim_pos);
			Ray ray = LevelManager.Instance.GameCamera.renderCamera.ScreenPointToRay(aim_pos);
			Plane plane = new Plane(Vector3.up, Vector3.up * (char_pos.y + .5f));
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
				Vector3 position = char_pos;
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
				if (Vector3.Distance(char_pos, gun.muzzle.position) > Vector3.Distance(char_pos, vector))
				{
					distance *= -1;
				}
				double range = gun.BulletDistance * .5f;
				State.out_of_range.color = distance <= range
					? Color.white
					: distance <= max_range
						? new Color(1f, .5f, 0f)
						: Color.red;
				State.out_of_range.text = $"{System.Math.Round(distance, 1)}/{System.Math.Round(range, 1)}M";
			}
			return false;
		}
		static Vector2 screen_offset_to_world(Vector2 vector)
		{
			GameCamera game_camera = GameCamera.Instance;
			float f = game_camera.mainVCam.m_Lens.FieldOfView * Mathf.Deg2Rad;
			float d = Mathf.Abs(game_camera.mianCameraArm.distance);
			float p = (game_camera.mianCameraArm.pitch - 90f) * Mathf.Deg2Rad;
			float w = vector.y / Screen.height + .5f;
			float tan_half_fov = Mathf.Tan(f * .5f);
			float tan_p = Mathf.Tan(p);
			float sec_p = Mathf.Sqrt(1f + tan_p * tan_p);
			float y_over_z = tan_half_fov * (2f * w - 1f);
			float denom = 1f + tan_p * y_over_z;
			float mag = d * Mathf.Abs(y_over_z) / Mathf.Abs(denom) * sec_p;
			float sign = vector.y < 0f ? -1f : 1f;
			return new Vector2(
				vector.x * d / denom * tan_half_fov * 2f / Screen.height,
				sign * mag
			);
		}
		static Vector2 world_offset_to_screen(Vector2 vector)
		{
			GameCamera game_camera = GameCamera.Instance;
			float f = game_camera.mainVCam.m_Lens.FieldOfView * Mathf.Deg2Rad;
			float d = Mathf.Abs(game_camera.mianCameraArm.distance);
			float p = (game_camera.mianCameraArm.pitch - 90f) * Mathf.Deg2Rad;
			float m = vector.y;
			float tan_half_fov = Mathf.Tan(f * .5f);
			float tan_p = Mathf.Tan(p);
			float cos_p = 1f / Mathf.Sqrt(1f + tan_p * tan_p);
			float k = Mathf.Abs(m) / d * cos_p;
			float denom_k = 1f - k * k * tan_p * tan_p;
			float y_over_z = (k * k * tan_p + (m < 0f ? -k : k)) / denom_k;
			float denom = 1f + tan_p * y_over_z;
			float w = .5f * (1f + y_over_z / tan_half_fov);
			return new Vector2(
				Screen.height * vector.x * denom / d / tan_half_fov * .5f,
				Screen.height * (w - .5f)
			);
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
		Harmony harmony = new Harmony("Enhanced_ADS.Harmony");
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
			Adaptive_Sensitivity = 0,
			Trace_Aim_Point = 1,
			Scrollable = 2
		}
		static string[] options
		{
			get
			{
				switch (LocalizationManager.CurrentLanguage)
				{
					case SystemLanguage.ChineseSimplified:// 简体中文
						return new[]
						{
							"自适应灵敏度",
							"瞄准点为中心",
							"可滚动"
						};

					case SystemLanguage.ChineseTraditional:// 繁體中文
						return new[]
						{
							"自適應靈敏度",
							"以瞄準點為中心",
							"可滾動"
						};

					case SystemLanguage.English: default:// English
						return new[]
						{
							"Adaptive Sensitivity",
							"Center of Aim Point",
							"Scrollable"
						};

					case SystemLanguage.French:// Français
						return new[]
						{
							"Sensibilité adaptative",
							"Centré sur le point visé",
							"Déroulable"
						};

					case SystemLanguage.German:// Deutsch
						return new[]
						{
							"Adaptive Empfindlichkeit",
							"Zielpunkt zentriert",
							"Scrollbar"
						};

					case SystemLanguage.Japanese:// 日本語
						return new[]
						{
							"適応感度",
							"照準点中心",
							"スクロール可能"
						};

					case SystemLanguage.Korean:// 한국어
						return new[]
						{
							"적응형 감도",
							"조준점 중심",
							"스크롤 가능"
						};

					case SystemLanguage.Portuguese:// Português (Brasil)
						return new[]
						{
							"Sensibilidade adaptativa",
							"Centralizar no ponto de mira",
							"Rolável"
						};

					case SystemLanguage.Russian:// Русский
						return new[]
						{
							"Адаптивная чувствительность",
							"Центрировать по точке прицеливания",
							"Прокручиваемый"
						};

					case SystemLanguage.Spanish:// Español
						return new[]
						{
							"Sensibilidad adaptativa",
							"Centrado en el punto de mira",
							"Desplazable"
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
				case SystemLanguage.English: default:// English
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
				case SystemLanguage.English: default:// English
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
			}
			((TextMeshProUGUI)I_label.GetValue(entry)).text = label_text;
		}
	}
	public class State
	{
		static FieldInfo I_OnOptionsChanged = AccessTools.Field(typeof(OptionsManager), "OnOptionsChanged");
		static ES3Settings es3_settings;
		public static OptionsProvider_ads_mode_type.Options ads_mode_type
		{
			get => load_option("Enhanced_ADS.ads_mode_type", OptionsProvider_ads_mode_type.Options.Adaptive_Sensitivity);
			set => save_option("Enhanced_ADS.ads_mode_type", value);
		}
		public static Vector2 camera_offset = Vector2.zero;
		public static float delta = 0f;
		public static TextMeshProUGUI out_of_range;
		public static bool tracking = false;
		static State()
		{
			es3_settings = new ES3Settings(true);
			es3_settings.path = Path.Combine(SavesSystem.SavesFolder, "Mod.ES3");
			es3_settings.location = ES3.Location.File;
		}
		static T load_option<T>(string key, T default_value)
		{
			try
			{
				if (ES3.KeyExists(key, es3_settings))
				{
					return ES3.Load<T>(key, es3_settings);
				}
				ES3.Save(key, default_value, es3_settings);
			}
			catch
			{
				ES3.RestoreBackup(es3_settings);
				if (ES3.KeyExists(key, es3_settings))
				{
					return ES3.Load<T>(key, es3_settings);
				}
				ES3.Save(key, default_value, es3_settings);
			}
			return default_value;
		}
		static void save_option<T>(string key, T value)
		{
			ES3.Save(key, value, es3_settings);
			((System.Action<string>)I_OnOptionsChanged.GetValue(null))?.Invoke(key);
			ES3.CreateBackup(es3_settings);
		}
	}
}