using Duckov.CustomOptions;
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
				State.out_of_range.fontSize = 18f;
				State.out_of_range.rectTransform.SetParent(__instance.aimMarkerUI, false);
				State.out_of_range.rectTransform.anchoredPosition = new Vector2(30f, 0f);
				State.out_of_range.rectTransform.pivot = new Vector2(0f, 0f);
			}
		}
	}
	[HarmonyPatch(typeof(GameCamera), "UpdateAimOffsetNormal")]
	internal class GameCamera__UpdateAimOffsetNormal
	{
		static FieldInfo I_offsetFromTargetX = AccessTools.Field(typeof(GameCamera), "offsetFromTargetX");
		static FieldInfo I_offsetFromTargetZ = AccessTools.Field(typeof(GameCamera), "offsetFromTargetZ");
		static bool Prefix(GameCamera __instance)
		{
			if (State.ads_mode_type == OptionsProvider_ads_mode_type.Options.Vanilla)
			{
				State.camera_offset = Vector2.zero;
				State.delta = 0f;
				return true;
			}
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
		static bool Prefix(InputManager __instance, Vector2 mouseDelta)
		{
			if (State.ads_mode_type == OptionsProvider_ads_mode_type.Options.Vanilla)
			{
				State.camera_offset = Vector2.zero;
				State.delta = 0f;
				State.prev_fov = 0f;
				return true;
			}
			bool input_actived = (bool)Get_InputActived.Invoke(null, null);
			if (!__instance.characterMainControl || !input_actived)
			{
				return true;
			}
			ItemAgent_Gun gun = __instance.characterMainControl.GetGun();
			if (!gun)
			{
				return true;
			}
			mouseDelta *= OptionsManager.MouseSensitivity / 10f;
			GameCamera game_camera = GameCamera.Instance;
			float fov = game_camera.renderCamera.fieldOfView;
			float pitch = GameCamera.Instance.mianCameraArm.pitch;
			Vector3 char_pos = __instance.characterMainControl.transform.position;
			float aim_range =  Vector3.Distance(char_pos, gun.muzzle.position) + gun.BulletDistance + .5f;
			Vector2 prev_aim_pos = (Vector2)Get_AimMousePosition.Invoke(__instance, null);
			Vector2 aim_pos = (Vector2)I_ProcessMousePosViaRecoil.Invoke(__instance, new object[] {
				prev_aim_pos,
				mouseDelta,
				gun
			});
			bool aiming_enemy_head = false;
			Vector2 center = new Vector2(Screen.width * .5f, Screen.height * .5f);
			if (__instance.characterMainControl.IsInAdsInput)
			{
				int screen_edge_offset = 15;
				if (State.prev_fov > 0f && !Mathf.Approximately(fov, State.prev_fov))
				{
					aim_pos = fov_correct(aim_pos, center, State.prev_fov);
				}
				State.prev_fov = fov;
				if (
					State.ads_mode_type == OptionsProvider_ads_mode_type.Options.Adaptive_Sensitivity
					|| State.ads_mode_type == OptionsProvider_ads_mode_type.Options.Trace_Aim_Point
				)
				{
					Vector2 target_screen_pos = aim_pos + mouseDelta;
					target_screen_pos.x = Mathf.Clamp(target_screen_pos.x, 0f, Screen.width);
					target_screen_pos.y = Mathf.Clamp(target_screen_pos.y, 0f, Screen.height);
					Vector2 current_world_aim = screen_offset_to_world(target_screen_pos - center) + State.camera_offset;
					if (State.ads_mode_type == OptionsProvider_ads_mode_type.Options.Trace_Aim_Point)
					{
						current_world_aim = Vector2.ClampMagnitude(current_world_aim, aim_range);
					}
					State.delta = Mathf.MoveTowards(State.delta, 1f, Time.deltaTime * gun.AdsSpeed);
					if (State.ads_mode_type == OptionsProvider_ads_mode_type.Options.Adaptive_Sensitivity)
					{
						var (world_edge_x, world_edge_y_up, world_edge_y_down, max_camera_x, max_camera_y_up, max_camera_y_down) = camera_bounds(aim_range, center, screen_edge_offset);
						float current_y_ratio = current_world_aim.y > 0 ? (aim_range - world_edge_y_up) : (aim_range - world_edge_y_down);
						State.camera_offset.x = Mathf.Clamp(State.delta * current_world_aim.x * (aim_range - world_edge_x) / aim_range, -max_camera_x, max_camera_x);
						State.camera_offset.y = pitch - 1f > fov * .5f
							? Mathf.Clamp(State.delta * current_world_aim.y * current_y_ratio / aim_range, -max_camera_y_down, max_camera_y_up)
							: 0f;
					}
					else
					{
						State.camera_offset = current_world_aim * State.delta;
					}
					aim_pos = center + world_offset_to_screen(current_world_aim - State.camera_offset);
				}
				else if (State.ads_mode_type == OptionsProvider_ads_mode_type.Options.Scrollable)
				{
					aim_pos += mouseDelta;
					if (aim_pos.x < screen_edge_offset || aim_pos.x > Screen.width - screen_edge_offset ||
						aim_pos.y < screen_edge_offset || aim_pos.y > Screen.height - screen_edge_offset)
					{
						Vector2 camera_move = Vector2.zero;
						if (aim_pos.x < screen_edge_offset) camera_move.x -= Time.deltaTime;
						else if (aim_pos.x > Screen.width - screen_edge_offset) camera_move.x += Time.deltaTime;
						if (aim_pos.y < screen_edge_offset) camera_move.y -= Time.deltaTime;
						else if (aim_pos.y > Screen.height - screen_edge_offset) camera_move.y += Time.deltaTime;
						var (_, _, _, max_camera_x, max_camera_y_up, max_camera_y_down) = camera_bounds(aim_range, center, screen_edge_offset);
						Vector2 delta_camera = screen_offset_to_world(camera_move * Screen.width * 2.5f);
						Vector2 new_camera_offset_scroll = State.camera_offset + delta_camera;
						State.camera_offset.x = Mathf.Clamp(new_camera_offset_scroll.x, -max_camera_x, max_camera_x);
						State.camera_offset.y = pitch - 1f > fov * .5f
							? Mathf.Clamp(new_camera_offset_scroll.y, -max_camera_y_down, max_camera_y_up)
							: 0f;
					}
				}
				State.out_of_range?.gameObject.SetActive(true);
			}
			else
			{
				if (State.camera_offset != Vector2.zero)
				{
					Vector2 current_world_aim = screen_offset_to_world(aim_pos - center) + State.camera_offset;
					State.camera_offset = Vector2.zero;
					aim_pos = center + world_offset_to_screen(current_world_aim);
				}
				else
				{
					if (State.prev_fov > 0f && !Mathf.Approximately(fov, State.prev_fov))
					{
						aim_pos = fov_correct(aim_pos, center, State.prev_fov);
					}
				}
				aim_pos += mouseDelta;
				State.delta = 0f;
				State.out_of_range?.gameObject.SetActive(false);
				State.prev_fov = fov;
			}
			aim_pos.x = Mathf.Clamp(aim_pos.x, 0f, Screen.width);
			float y_limit = Screen.height;
			if (pitch - 1f <= fov * .5f)
			{
				float f = fov * Mathf.Deg2Rad;
				float p = (pitch - 91f) * Mathf.Deg2Rad;
				float tan_p = Mathf.Tan(p);
				float tan_half_fov = Mathf.Tan(f * .5f);
				float singular_w = .5f - .5f / (tan_p * tan_half_fov);
				y_limit = Mathf.Clamp(
					singular_w * Screen.height,
					0f,
					Screen.height
				);
			}
			aim_pos.y = Mathf.Clamp(aim_pos.y, 0f, y_limit);
			Set_AimMousePosition.Invoke(__instance, new object[] { aim_pos });
			I_aimScreenPoint.SetValue(__instance, aim_pos);
			Ray ray = LevelManager.Instance.GameCamera.renderCamera.ScreenPointToRay(aim_pos);
			Plane plane = new Plane(Vector3.up, Vector3.up * (char_pos.y + .5f));
			plane.Raycast(ray, out var enter);
			Vector3 vector = ray.origin + ray.direction * enter;
			Debug.DrawLine(vector, vector + Vector3.up * 3f, Color.yellow);
			Vector3 aim_point = vector;
			RaycastHit hitted_head = (RaycastHit)I_hittedHead.GetValue(__instance);
			if (gun && __instance.characterMainControl.CanControlAim())
			{
				if (Physics.Raycast(ray, out hitted_head, 100f, 1 << LayerMask.NameToLayer("HeadCollider")))
				{
					aiming_enemy_head = true;
				}
				I_hittedHead.SetValue(__instance, hitted_head);
				Vector3 position = char_pos;
				if (gun)
				{
					position = gun.muzzle.transform.position;
				}
				Vector3 vector2 = vector - position;
				vector2.y = 0f;
				vector2.Normalize();
				Vector3 axis = Vector3.Cross(vector2, ray.direction);
				LayerMask aim_check_layers = GameplayDataSettings.Layers.damageReceiverLayerMask;
				I_aimCheckLayers.SetValue(__instance, aim_check_layers);
				for (int i = 0; i < 45; i++)
				{
					int num = i;
					if (i > 23)
					{
						num = -i + 23;
					}
					float num2 = 1.5f;
					Vector3 vector3 = Quaternion.AngleAxis(-2f * num, axis) * vector2;
					Ray ray2 = new Ray(position + num2 * vector3, vector3);
					if (
						Physics.SphereCast(ray2, .02f, out var hitted_character_dmg_receiver_info, gun.BulletDistance, aim_check_layers, QueryTriggerInteraction.Ignore)
						&& hitted_character_dmg_receiver_info.distance > .1f
						&& !Physics.SphereCast(ray2, .1f, out var _, hitted_character_dmg_receiver_info.distance, (LayerMask)I_obsticleLayers.GetValue(__instance), QueryTriggerInteraction.Ignore))
					{
						aim_point = hitted_character_dmg_receiver_info.point;
						break;
					}
				}
			}
			if (aiming_enemy_head)
			{
				Vector3 direction = ray.direction;
				Vector3 rhs = hitted_head.collider.transform.position - hitted_head.point;
				float num3 = Vector3.Dot(direction, rhs);
				aim_point = hitted_head.point + direction * num3 * .5f;
			}
			I_aimingEnemyHead.SetValue(__instance, aiming_enemy_head);
			I_inputAimPoint.SetValue(__instance, vector);
			__instance.characterMainControl.SetAimPoint(aim_point);
			if (__instance.characterMainControl.IsInAdsInput)
			{
				update_range_display(gun, char_pos, vector, aim_pos);
			}
			if (Application.isFocused)
			{
				Mouse.current.WarpCursorPosition(__instance.AimScreenPoint);
			}
			return false;
		}
		static void Postfix(InputManager __instance)
		{
			if (State.ads_mode_type != OptionsProvider_ads_mode_type.Options.Vanilla) return;
			if (!__instance.characterMainControl) return;
			if (!(bool)Get_InputActived.Invoke(null, null)) return;
			Vector2 aim_pos = (Vector2)Get_AimMousePosition.Invoke(__instance, null);
			ItemAgent_Gun gun = __instance.characterMainControl.GetGun();
			if (gun && __instance.characterMainControl.IsInAdsInput)
			{
				update_range_display(
					gun,
					__instance.characterMainControl.transform.position,
					(Vector3)I_inputAimPoint.GetValue(__instance),
					aim_pos);
			}
			else
			{
				State.out_of_range?.gameObject.SetActive(false);
			}
			if (Application.isFocused)
			{
				Mouse.current.WarpCursorPosition(aim_pos);
			}
		}
		static void update_range_display(ItemAgent_Gun gun, Vector3 char_pos, Vector3 aim_point, Vector2 aim_pos)
		{
			if (State.out_of_range == null)
			{
				return;
			}
			double distance = Vector3.Distance(gun.muzzle.position, aim_point);
			if (Vector3.Distance(char_pos, gun.muzzle.position) > Vector3.Distance(char_pos, aim_point))
			{
				distance = 0d;
			}
			float half_damage_distance = effective_half_damage_distance(gun);
			State.out_of_range.gameObject.SetActive(true);
			State.out_of_range.color = distance <= half_damage_distance + .5f
				? Color.white
				: distance <= gun.BulletDistance + .5f
					? new Color(1f, .5f, 0f)
					: Color.red;
			State.out_of_range.text = $"{System.Math.Round(distance, 1)}/{System.Math.Round(gun.BulletDistance, 1)}M";
			float tw = State.out_of_range.preferredWidth;
			float th = Mathf.Max(State.out_of_range.preferredHeight, State.out_of_range.fontSize * 1.5f) * 1.5f;
			float offset = 30f;
			float cs = State.out_of_range.canvas?.scaleFactor ?? 1f;
			float tx = Mathf.Min(offset, (Screen.width - aim_pos.x) / cs - tw - offset);
			float ty = Mathf.Min(0f, (Screen.height - aim_pos.y) / cs - th - offset);
			State.out_of_range.rectTransform.anchoredPosition = new Vector2(tx, ty);
		}
		static Vector2 screen_offset_to_world(Vector2 vector, float? fov = null)
		{
			GameCamera game_camera = GameCamera.Instance;
			float f = (fov ?? game_camera.renderCamera.fieldOfView) * Mathf.Deg2Rad;
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
			float f = game_camera.renderCamera.fieldOfView * Mathf.Deg2Rad;
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
		static float ads_effective_range_bonus(ItemAgent_Gun gun)
		{
			if (!gun || !gun.Holder || !gun.Holder.IsInAdsInput)
			{
				return 0f;
			}
			return Mathf.Max(0f, gun.ADSAimDistanceFactor) * 5f * Mathf.Clamp01(gun.AdsValue);
		}
		static float effective_half_damage_distance(ItemAgent_Gun gun)
		{
			return Mathf.Min(gun.BulletDistance, gun.BulletDistance * .5f + ads_effective_range_bonus(gun));
		}
		static Vector2 fov_correct(Vector2 aim_pos, Vector2 center, float prev_fov)
		{
			return center + world_offset_to_screen(screen_offset_to_world(aim_pos - center, prev_fov));
		}
		static (float ex, float ey_up, float ey_dn, float max_x, float max_y_up, float max_y_dn) camera_bounds(float aim_range, Vector2 center, float edge_offset)
		{
			float ex = screen_offset_to_world(new Vector2(center.x - edge_offset, 0f)).x;
			float ey_up = screen_offset_to_world(new Vector2(0f, center.y - edge_offset)).y;
			float ey_dn = Mathf.Abs(screen_offset_to_world(new Vector2(0f, -(center.y - edge_offset))).y);
			float max_x = screen_offset_to_world(new Vector2(center.x, 0f)).x * (aim_range - ex) / ex;
			float max_y_up = screen_offset_to_world(new Vector2(0f, center.y)).y * (aim_range - ey_up) / ey_up;
			float max_y_dn = Mathf.Abs(screen_offset_to_world(new Vector2(0f, -center.y)).y) * (aim_range - ey_dn) / ey_dn;
			return (ex, ey_up, ey_dn, max_x, max_y_up, max_y_dn);
		}
	}
	[HarmonyPatch(typeof(ItemAgent_Gun), "TransToEmpty")]
	internal class ItemAgent_Gun__TransToEmpty
	{
		static FieldInfo I_gunState = AccessTools.Field(typeof(ItemAgent_Gun), "gunState");
		static bool Prefix(ItemAgent_Gun __instance)
		{
			if (!State.auto_reload)
			{
				return true;
			}
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
		static readonly FieldInfo I_label = AccessTools.Field(typeof(OptionsUIEntry_Dropdown), "label");
		static readonly FieldInfo I_provider = AccessTools.Field(typeof(OptionsUIEntry_Dropdown), "provider");
		static string label_text
		{
			get
			{
				switch (LocalizationManager.CurrentLanguage)
				{
					case SystemLanguage.ChineseSimplified: return "ADS 摄像机模式";
					case SystemLanguage.ChineseTraditional: return "ADS 攝影機模式";
					case SystemLanguage.French: return "Mode caméra ADS";
					case SystemLanguage.German: return "ADS-Kameramodus";
					case SystemLanguage.Japanese: return "ADSカメラモード";
					case SystemLanguage.Korean: return "ADS 카메라 모드";
					case SystemLanguage.Portuguese: return "Modo de câmera ADS";
					case SystemLanguage.Russian: return "Режим камеры ADS";
					case SystemLanguage.Spanish: return "Modo de cámara ADS";
					default: return "ADS Camera Mode";
				}
			}
		}
		static string auto_reload_label_text
		{
			get
			{
				switch (LocalizationManager.CurrentLanguage)
				{
					case SystemLanguage.ChineseSimplified: return "自动装填";
					case SystemLanguage.ChineseTraditional: return "自動裝填";
					case SystemLanguage.French: return "Rechargement automatique";
					case SystemLanguage.German: return "Automatisches Nachladen";
					case SystemLanguage.Japanese: return "自動リロード";
					case SystemLanguage.Korean: return "자동 재장전";
					case SystemLanguage.Portuguese: return "Recarga automática";
					case SystemLanguage.Russian: return "Автоматическая перезарядка";
					case SystemLanguage.Spanish: return "Recarga automática";
					default: return "Auto Reload";
				}
			}
		}
		Harmony harmony = new Harmony("Enhanced_ADS.Harmony");
		TextMeshProUGUI? ads_mode_label;
		TextMeshProUGUI? auto_reload_label;
		void Awake()
		{
			harmony.PatchAll(Assembly.GetExecutingAssembly());
			CustomOptionsPanel.OnPanelEnabled += OnOptionsPanel;
			LocalizationManager.OnSetLanguage += OnSetLanguage;
		}
		void OnDestroy()
		{
			harmony.UnpatchAll(harmony.Id);
			CustomOptionsPanel.OnPanelEnabled -= OnOptionsPanel;
			LocalizationManager.OnSetLanguage -= OnSetLanguage;
		}
		void OnSetLanguage(SystemLanguage _)
		{
			if (ads_mode_label != null || auto_reload_label != null)
			{
				StartCoroutine(UpdateLabelNextFrame());
			}
		}
		System.Collections.IEnumerator UpdateLabelNextFrame()
		{
			yield return null;
			if (ads_mode_label != null)
				ads_mode_label.text = label_text;
			if (auto_reload_label != null)
				auto_reload_label.text = auto_reload_label_text;
		}
		void OnOptionsPanel(RectTransform panel_transform)
		{
			OptionsUIEntry_Dropdown? template_entry = null;
			Transform search_root = panel_transform;
			while (search_root.parent != null && template_entry == null)
			{
				search_root = search_root.parent;
				foreach (OptionsUIEntry_Dropdown e in search_root.GetComponentsInChildren<OptionsUIEntry_Dropdown>(true))
				{
					if (e.gameObject.name == "UI_HurtVisual") { template_entry = e; break; }
				}
			}
			if (template_entry == null) return;
			Transform parent = template_entry.transform.parent;
			Transform existing_ads_mode = parent.Find("Enhanced_ADS.ads_mode_type");
			if (existing_ads_mode != null)
			{
				ads_mode_label = (TextMeshProUGUI)I_label.GetValue(existing_ads_mode.GetComponent<OptionsUIEntry_Dropdown>());
			}
			else
			{
				GameObject ads_mode_option = Object.Instantiate(template_entry.gameObject, parent);
				ads_mode_option.name = "Enhanced_ADS.ads_mode_type";
				ads_mode_option.SetActive(true);
				OptionsUIEntry_Dropdown entry = ads_mode_option.GetComponent<OptionsUIEntry_Dropdown>();
				I_provider.SetValue(entry, ads_mode_option.AddComponent<OptionsProvider_ads_mode_type>());
				ads_mode_label = (TextMeshProUGUI)I_label.GetValue(entry);
				ads_mode_label.text = label_text;
				existing_ads_mode = ads_mode_option.transform;
			}
			Transform existing_auto_reload = parent.Find("Enhanced_ADS.auto_reload");
			if (existing_auto_reload != null)
			{
				auto_reload_label = (TextMeshProUGUI)I_label.GetValue(existing_auto_reload.GetComponent<OptionsUIEntry_Dropdown>());
				return;
			}
			GameObject auto_reload_option = Object.Instantiate(template_entry.gameObject, parent);
			auto_reload_option.name = "Enhanced_ADS.auto_reload";
			auto_reload_option.transform.SetSiblingIndex(existing_ads_mode.GetSiblingIndex() + 1);
			auto_reload_option.SetActive(true);
			OptionsUIEntry_Dropdown auto_reload_entry = auto_reload_option.GetComponent<OptionsUIEntry_Dropdown>();
			I_provider.SetValue(auto_reload_entry, auto_reload_option.AddComponent<OptionsProvider_auto_reload>());
			auto_reload_label = (TextMeshProUGUI)I_label.GetValue(auto_reload_entry);
			auto_reload_label.text = auto_reload_label_text;
		}
	}
	public class OptionsProvider_ads_mode_type : OptionsProviderBase
	{
		public enum Options
		{
			Adaptive_Sensitivity = 0,
			Trace_Aim_Point = 1,
			Scrollable = 2,
			Vanilla = 3
		}
		static string[] options
		{
			get
			{
				switch (LocalizationManager.CurrentLanguage)
				{
					case SystemLanguage.ChineseSimplified:
						return new string[] { "自适应灵敏度", "瞄准点为中心", "可滚动", "默认" };
					case SystemLanguage.ChineseTraditional:
						return new string[] { "自適應靈敏度", "以瞄準點為中心", "可滾動", "預設" };
					case SystemLanguage.English: default:
						return new string[] { "Adaptive Sensitivity", "Center of Aim Point", "Scrollable", "Default" };
					case SystemLanguage.French:
						return new string[] { "Sensibilité adaptative", "Centré sur le point visé", "Déroulable", "Par défaut" };
					case SystemLanguage.German:
						return new string[] { "Adaptive Empfindlichkeit", "Zielpunkt zentriert", "Scrollbar", "Standard" };
					case SystemLanguage.Japanese:
						return new string[] { "適応感度", "照準点中心", "スクロール可能", "デフォルト" };
					case SystemLanguage.Korean:
						return new string[] { "적응형 감도", "조준점 중심", "스크롤 가능", "기본" };
					case SystemLanguage.Portuguese:
						return new string[] { "Sensibilidade adaptativa", "Centralizar no ponto de mira", "Rolável", "Padrão" };
					case SystemLanguage.Russian:
						return new string[] { "Адаптивная чувствительность", "Центрировать по точке прицеливания", "Прокручиваемый", "По умолчанию" };
					case SystemLanguage.Spanish:
						return new string[] { "Sensibilidad adaptativa", "Centrado en el punto de mira", "Desplazable", "Predeterminado" };
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
	public class OptionsProvider_auto_reload : OptionsProviderBase
	{
		static string[] options
		{
			get
			{
				switch (LocalizationManager.CurrentLanguage)
				{
					case SystemLanguage.ChineseSimplified:
						return new string[] { "开启", "关闭" };
					case SystemLanguage.ChineseTraditional:
						return new string[] { "開啟", "關閉" };
					case SystemLanguage.English: default:
						return new string[] { "Enabled", "Disabled" };
					case SystemLanguage.French:
						return new string[] { "Activé", "Désactivé" };
					case SystemLanguage.German:
						return new string[] { "Ein", "Aus" };
					case SystemLanguage.Japanese:
						return new string[] { "オン", "オフ" };
					case SystemLanguage.Korean:
						return new string[] { "켜기", "끄기" };
					case SystemLanguage.Portuguese:
						return new string[] { "Ativado", "Desativado" };
					case SystemLanguage.Russian:
						return new string[] { "Вкл.", "Выкл." };
					case SystemLanguage.Spanish:
						return new string[] { "Activada", "Desactivada" };
				}
			}
		}
		public override string Key => "Enhanced_ADS.auto_reload";
		public override string[] GetOptions()
		{
			return options;
		}
		public override string GetCurrentOption()
		{
			return options[State.auto_reload ? 0 : 1];
		}
		public override void Set(int index)
		{
			State.auto_reload = index == 0;
		}
	}
	[HarmonyPatch(typeof(Projectile), nameof(Projectile.Init), new System.Type[] { typeof(ProjectileContext) })]
	internal class Projectile__Init
	{
		static void Prefix(ref ProjectileContext _context)
		{
			CharacterMainControl character = _context.realFromCharacter ?? _context.fromCharacter;
			if (!character || _context.halfDamageDistance <= 0f)
			{
				return;
			}
			ItemAgent_Gun gun = character.GetGun();
			if (!gun)
			{
				return;
			}
			if (!character.IsInAdsInput)
			{
				return;
			}
			float max_damage_distance = _context.distance > 0f ? _context.distance : gun.BulletDistance;
			_context.halfDamageDistance = Mathf.Min(
				max_damage_distance,
				_context.halfDamageDistance + Mathf.Max(0f, gun.ADSAimDistanceFactor) * 5f * Mathf.Clamp01(gun.AdsValue));
		}
	}
	public class State
	{
		static FieldInfo I_OnOptionsChanged = AccessTools.Field(typeof(OptionsManager), "OnOptionsChanged");
		public static OptionsProvider_ads_mode_type.Options ads_mode_type
		{
			get => load_option("Enhanced_ADS.ads_mode_type", OptionsProvider_ads_mode_type.Options.Adaptive_Sensitivity);
			set => save_option("Enhanced_ADS.ads_mode_type", value);
		}
		public static bool auto_reload
		{
			get => load_option("Enhanced_ADS.auto_reload", true);
			set => save_option("Enhanced_ADS.auto_reload", value);
		}
		public static Vector2 camera_offset = Vector2.zero;
		public static float delta = 0f;
		static ES3Settings es3_settings;
		public static TextMeshProUGUI? out_of_range;
		public static float prev_fov = 0f;
		static State()
		{
			es3_settings = new ES3Settings(true)
			{
				path = Path.Combine(SavesSystem.SavesFolder, "Mod.ES3"),
				location = ES3.Location.File
			};
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
