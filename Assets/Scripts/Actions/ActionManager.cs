using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using InControl;
using System.Xml.Serialization;

namespace Frontiers
{
	public class ActionManager <T> : Manager where T : struct, IConvertible, IComparable, IFormattable
	{
		public override string GameObjectName {
			get {
				return "Frontiers_InputManager";
			}
		}

		public static ActionReceiver <T> InterfaceReceiver = null;
		public static ActionReceiver <T> PlayerReceiver = null;
		//global info
		public static double TimeStamp = 0f;
		public static bool SoftwareMouse = false;
		public static Vector3 MousePosition;
		public static bool InvertRawMouseAxis = false;
		public static bool InvertRawMovementAxis = false;
		public static bool InvertRawInterfaceAxis = false;
		public static float RawMouseAxisX = 0.0f;
		public static float RawMouseAxisY = 0.0f;
		public static float RawMovementAxisX = 0.0f;
		public static float RawMovementAxisY = 0.0f;
		public static float RawScrollWheelAxis = 0.0f;
		public static float RawInterfaceAxisX = 0.0f;
		public static float RawInterfaceAxisY = 0.0f;
		#if UNITY_EDITOR
		public Vector3 mMousePosition;
		public float mRawMouseAxisX = 0.0f;
		public float mRawMouseAxisY = 0.0f;
		public float mRawMovementAxisX = 0.0f;
		public float mRawMovementAxisY = 0.0f;
		public float mRawScrollWheelAxis = 0.0f;
		public float mRawInterfaceAxisX = 0.0f;
		public float mRawInterfaceAxisY = 0.0f;
		#endif
		public static int LastMouseClick = 0;
		public static bool AvailableKeyDown = false;
		public static KeyCode LastKey = KeyCode.None;
		public static string LastInputString = string.Empty;
		public static InputControlType LastControllerAction = InputControlType.None;
		public static bool InputFieldActive = false;
		//used for tracking mouse & free look position
		public InputControlType MouseXAxis;
		public InputControlType MouseYAxis;
		public ActionSetting.MouseAction ForwardMouseAction;
		public ActionSetting.MouseAction BackMouseAction;
		public InputControlType MoveForward;
		public InputControlType MoveBack;
		public InputControlType MoveLeft;
		public InputControlType MoveRight;
		public InputControlType ScrollWheelAxis;
		public InputControlType InterfaceUp;
		public InputControlType InterfaceDown;
		public InputControlType InterfaceLeft;
		public InputControlType InterfaceRight;
		//used for NGUI events
		public InputControlType CursorClickAction;
		public InputControlType CursorRightClickAction;
		public bool CursorClickDown;
		public bool CursorClickHold;
		public bool CursorClickUp;
		public bool CursorRightClickDown;
		public bool CursorRightClickHold;
		public bool CursorRightClickUp;
		//key bindings
		public CustomInputDeviceProfile KeyboardAndMouseProfile;
		public List <ActionSetting> CurrentActionSettings;
		public List <ActionSetting> DefaultActionSettings;
		public List <KeyCode> DefaultAvailableKeys = new List<KeyCode> ();
		public List <InputControlType> DefaultAvailableAxis = new List<InputControlType> ();
		public List <InputControlType> DefaultAvailableActions = new List<InputControlType> ();
		public List <ActionSetting.MouseAction> DefaultAvailableMouseButtons = new List<ActionSetting.MouseAction> ();
		public InputDevice Device = InputDevice.Null;
		protected Dictionary <InputControlType,InputControlType[]> mOpposingControls = new Dictionary<InputControlType, InputControlType[]> ();

		public void GetAvailableBindings (List<ActionSetting> settings)
		{
			foreach (ActionSetting setting in settings) {
				if (setting.HasAvailableKeys) {
					setting.AvailableKeys = DefaultAvailableKeys;
				}
				if (setting.HasAvailableMouseButtons) {
					setting.AvailableMouseButtons = DefaultAvailableMouseButtons;
				}
				if (setting.HasAvailableControllerButtons) {
					if (setting.AxisSetting) {
						setting.AvailableControllerButtons = DefaultAvailableAxis;
					} else {
						setting.AvailableControllerButtons = DefaultAvailableActions;
					}
				}
			}
		}

		public void PushDeadZoneSettings ()
		{
			if (Profile.Get.CurrentPreferences.Controls.UseCustomDeadZoneSettings) {
				//make sure the dead zones are legit
				Profile.Get.CurrentPreferences.Controls.RefreshCustomDeadZoneSettings (Device);
				//set the dead zones on current controllers
				Device.LeftStick.UpperDeadZone = Profile.Get.CurrentPreferences.Controls.DeadZoneLStickUpper;
				Device.LeftStick.LowerDeadZone = Profile.Get.CurrentPreferences.Controls.DeadZoneLStickLower;
				Device.GetControl (InputControlType.LeftStickX).Sensitivity = Profile.Get.CurrentPreferences.Controls.SensitivityLStick;
				Device.GetControl (InputControlType.LeftStickY).Sensitivity = Profile.Get.CurrentPreferences.Controls.SensitivityLStick;

				Device.RightStick.LowerDeadZone = Profile.Get.CurrentPreferences.Controls.DeadZoneRStickLower;
				Device.RightStick.UpperDeadZone = Profile.Get.CurrentPreferences.Controls.DeadZoneRStickUpper;
				Device.GetControl (InputControlType.RightStickX).Sensitivity = Profile.Get.CurrentPreferences.Controls.SensitivityRStick;
				Device.GetControl (InputControlType.RightStickY).Sensitivity = Profile.Get.CurrentPreferences.Controls.SensitivityRStick;

				Device.DPad.LowerDeadZone = Profile.Get.CurrentPreferences.Controls.DeadZoneLStickLower;
				Device.DPad.UpperDeadZone = Profile.Get.CurrentPreferences.Controls.DeadZoneLStickLower;
				Device.GetControl (InputControlType.DPadLeft).Sensitivity = Profile.Get.CurrentPreferences.Controls.SensitivityDPad;
				Device.GetControl (InputControlType.DPadRight).Sensitivity = Profile.Get.CurrentPreferences.Controls.SensitivityDPad;
				Device.GetControl (InputControlType.DPadUp).Sensitivity = Profile.Get.CurrentPreferences.Controls.SensitivityDPad;
				Device.GetControl (InputControlType.DPadDown).Sensitivity = Profile.Get.CurrentPreferences.Controls.SensitivityDPad;
			}
		}

		public virtual void PushSettings (List <ActionSetting> newSettings)
		{
			if (newSettings == null || newSettings.Count == 0) {
				Debug.Log ("Not pushing settings in " + name + " , was null or empty");
				return;
			}
			//Debug.Log("Pushing " + newSettings.Count.ToString() + " settings in " + name);
			//start over
			ClearSettings ();

			List <ActionSetting> settingsNotFound = new List<ActionSetting> ();
			//get the default settings and make sure all bindings are accounted for
			for (int i = 0; i < DefaultActionSettings.Count; i++) {
				bool foundInNewSettings = false;
				for (int j = 0; j < newSettings.Count; j++) {
					if (DefaultActionSettings [i].ActionDescription.Equals (newSettings [j].ActionDescription)) {
						foundInNewSettings = true;
						break;
					}
				}
				if (!foundInNewSettings) {
					Debug.Log ("Didn't find action setting " + DefaultActionSettings [i].ActionDescription + " in preferences, adding now");
					settingsNotFound.Add (DefaultActionSettings [i]);
				}
			}

			CurrentActionSettings.AddRange (newSettings);
			CurrentActionSettings.AddRange (settingsNotFound);
			PushDeadZoneSettings ();

			//check for mouse and movement axis
			//also add available keys for when we want to rebind them
			for (int i = 0; i < CurrentActionSettings.Count; i++) {
				ActionSetting a = CurrentActionSettings [i];

				switch (a.Axis) {
				case ActionSetting.InputAxis.None:
				default:
					break;

				case ActionSetting.InputAxis.MouseX:
					MouseXAxis = a.Controller;
					break;

				case ActionSetting.InputAxis.MouseY:
					MouseYAxis = a.Controller;
					break;

				case ActionSetting.InputAxis.ScrollWheel:
					ScrollWheelAxis = a.Controller;
					break;

				case ActionSetting.InputAxis.MoveForward:
					MoveForward = a.Controller;
					break;

				case ActionSetting.InputAxis.MoveBack:
					MoveBack = a.Controller;
					break;

				case ActionSetting.InputAxis.MoveLeft:
					MoveLeft = a.Controller;
					break;

				case ActionSetting.InputAxis.MoveRight:
					MoveRight = a.Controller;
					break;

				case ActionSetting.InputAxis.InterfaceUp:
					InterfaceUp = a.Controller;
					break;
					
				case ActionSetting.InputAxis.InterfaceDown:
					InterfaceDown = a.Controller;
					break;
					
				case ActionSetting.InputAxis.InterfaceLeft:
					InterfaceLeft = a.Controller;
					break;
					
				case ActionSetting.InputAxis.InterfaceRight:
					InterfaceRight = a.Controller;
					break;
				}

				switch (a.Cursor) {
				case ActionSetting.CursorAction.None:
				default:
					break;

				case ActionSetting.CursorAction.Click:
					CursorClickAction = a.Controller;
					break;

				case ActionSetting.CursorAction.RightClick:
					CursorRightClickAction = a.Controller;
					break;
				}
			}
			//do regular bindings
			AddBindings ();
			AddDaisyChains ();
			//create the new device that uses these settings
			InputManager.DetachDevice (Device);
			CreateKeyboardAndMouseProfile ();
			if (KeyboardAndMouseProfile != null) {
				Device = new UnityInputDevice (KeyboardAndMouseProfile);
				InputManager.AttachDevice (Device);
			}
			OnPushSettings ();
		}

		protected void ClearSettings ()
		{
			mKeyDownMappings.Clear ();
			mKeyUpMappings.Clear ();
			mKeyHoldMappings.Clear ();
			mAxisChangeMappings.Clear ();
			mDaisyChains.Clear ();
			KeyboardAndMouseProfile = null;
			CurrentActionSettings.Clear ();
			mOpposingControls.Clear ();

			MouseXAxis = InputControlType.None;
			MouseYAxis = InputControlType.None;
			CursorClickAction = InputControlType.None;
			CursorRightClickAction = InputControlType.None;
			ScrollWheelAxis = InputControlType.None;
			ForwardMouseAction = ActionSetting.MouseAction.None;
			BackMouseAction = ActionSetting.MouseAction.None;
			MoveForward = InputControlType.None;
			MoveBack = InputControlType.None;
			MoveLeft = InputControlType.None;
			MoveRight = InputControlType.None;
			ScrollWheelAxis = InputControlType.None;
			InterfaceUp = InputControlType.None;
			InterfaceDown = InputControlType.None;
			InterfaceLeft = InputControlType.None;
			InterfaceRight = InputControlType.None;
		}
		//used by GUI to display / edit key bindings
		public virtual List <ActionSetting> GenerateDefaultActionSettings ()
		{
			return new List<ActionSetting> ();
		}

		protected void PushDefaulSettings ()
		{
			DefaultActionSettings = GenerateDefaultActionSettings ();
			PushSettings (DefaultActionSettings);
		}

		protected virtual void AddDaisyChains ()
		{
			return;
		}

		protected virtual void CreateKeyboardAndMouseProfile ()
		{
			KeyboardAndMouseProfile = new UserKeyboardAndMouseProfile <T> (this);
		}

		public void AddBindings ()
		{
			foreach (ActionSetting a in CurrentActionSettings) {
				//ignore axis settings
				if (a.IsBindable) {
					if (a.AxisSetting) {
						//special case
						T actionXAsEnum = ConvertToEnum (a.ActionOnX);
						T actionYAsEnum = ConvertToEnum (a.ActionOnY);
						//DPadX and DPadY are added
						//to make mapping simpler
						//we have to parse them to u/d l/r
						switch (a.Controller) {
						case InputControlType.DPadX:
							AddKeyDown (InputControlType.DPadRight, actionXAsEnum);
							AddKeyDown (InputControlType.DPadLeft, actionYAsEnum);
							break;

						case InputControlType.DPadY:
							AddKeyDown (InputControlType.DPadUp, actionXAsEnum);
							AddKeyDown (InputControlType.DPadDown, actionYAsEnum);
							break;

						/*case InputControlType.LeftStickX:
							AddKeyDown (InputControlType.LeftStickLeft, actionXAsEnum);
							AddKeyDown (InputControlType.LeftStickRight, actionYAsEnum);
							break;

						case InputControlType.RightStickX:
							AddKeyDown (InputControlType.RightStickLeft, actionXAsEnum);
							AddKeyDown (InputControlType.RightStickRight, actionYAsEnum);
							break;

						case InputControlType.LeftStickY:
							AddKeyDown (InputControlType.LeftStickUp, actionXAsEnum);
							AddKeyDown (InputControlType.LeftStickDown, actionYAsEnum);
							break;

						case InputControlType.RightStickY:
							AddKeyDown (InputControlType.RightStickUp, actionXAsEnum);
							AddKeyDown (InputControlType.RightStickDown, actionYAsEnum);
							break;*/

						default:
							break;
						}
					} else {
						//add the 'key down' binding
						T actionAsEnum = ConvertToEnum (a.Action);
						AddKeyDown (a.Controller, actionAsEnum);
						if (a.ActionOnHold > 0) {
							//if it has a hold action add hold binding for the same key
							T holdActionAsEnum = ConvertToEnum (a.ActionOnHold);
							AddKeyHold (a.Controller, holdActionAsEnum);
						}
						if (a.ActionOnRelease > 0) {
							//if it has a hold action add release binding for the same key
							T releaseActionAsEnum = ConvertToEnum (a.ActionOnRelease);
							AddKeyUp (a.Controller, releaseActionAsEnum);
						}
						if (a.HasOpposingControls (CurrentActionSettings)) {
							if (mOpposingControls.ContainsKey (a.Controller)) {
								Debug.Log ("Attempting to add opposing controls to " + a.Controller.ToString () + " when it already exists");
							} else {
								mOpposingControls.Add (a.Controller, a.OpposingControls);
							}
						}
					}
				}
			}
		}

		public bool HasInterfaceReceiver {
			get {
				return InterfaceReceiver != null;
			}
		}

		public bool HasPlayerReceiver {
			get {
				return PlayerReceiver != null;
			}
		}

		public void AddDaisyChain (T action, T daisyChainedAction)
		{
			if (action.Equals (daisyChainedAction)) {
				//Debug.LogError("Can't daisy chain the same action");
				return;
			}
			//TODO check for third-level daisy chains?
			List <T> actionList;
			if (mDaisyChains.TryGetValue (action, out actionList)) {
				actionList.Add (daisyChainedAction);
			} else {
				actionList = new List<T> ();
				actionList.Add (daisyChainedAction);
				mDaisyChains.Add (action, actionList);
			}
		}
		//use this function sparingly
		public bool IsKeyDown (T action)
		{
			InputControlType control = GetActionBinding (Convert.ToInt32 (action));
			#if UNITY_EDITOR
			bool isDown = Device.GetControl (control).IsPressed || InputManager.ActiveDevice.GetControl (control).IsPressed;
			//Debug.Log("Input control:  " + control.ToString() + " is down? " + isDown.ToString());
			return isDown;
			#else
			return Device.GetControl (control).IsPressed || InputManager.ActiveDevice.GetControl (control).IsPressed;
			#endif
		}

		public void AddMapping (InputControlType input, ActionSettingType actionType, T action)
		{

			List <T> actionList;
			Dictionary <InputControlType, List <T>> mappings = mKeyDownMappings;

			switch (actionType) {
			case ActionSettingType.None:
			case ActionSettingType.Down:
				break;

			case ActionSettingType.Hold:
				mappings = mKeyHoldMappings;
				break;

			case ActionSettingType.Up:
				mappings = mKeyUpMappings;
				break;

			}

			if (mappings.TryGetValue (input, out actionList)) {
				actionList.SafeAdd (action);
			} else {
				actionList = new List <T> ();
				actionList.Add (action);
				mappings.Add (input, actionList);
			}
		}

		public void AddAxisChange (InputControlType axis, T action)
		{
			List <T> actionList;
			if (mAxisChangeMappings.TryGetValue (axis, out actionList)) {
				actionList.Add (action);
			} else {
				actionList = new List<T> ();
				actionList.Add (action);
				mAxisChangeMappings.Add (axis, actionList);
			}
		}

		public void AddKeyUp (InputControlType key, T action)
		{
			List <T> actionList;
			if (mKeyUpMappings.TryGetValue (key, out actionList)) {
				actionList.Add (action);
			} else {
				actionList = new List<T> ();
				actionList.Add (action);
				mKeyUpMappings.Add (key, actionList);
			}
		}

		public void AddKeyDown (InputControlType key, T action)
		{
			List <T> actionList;
			if (mKeyDownMappings.TryGetValue (key, out actionList)) {
				actionList.Add (action);
			} else {
				actionList = new List<T> ();
				actionList.Add (action);
				mKeyDownMappings.Add (key, actionList);
			}
		}

		public void AddKeyHold (InputControlType key, T action)
		{
			List <T> actionList;
			if (mKeyHoldMappings.TryGetValue (key, out actionList)) {
				actionList.Add (action);
			} else {
				actionList = new List<T> ();
				actionList.Add (action);
				mKeyHoldMappings.Add (key, actionList);
			}
		}

		public void Update ()
		{
			if (!mInitialized || (!HasInterfaceReceiver && !HasPlayerReceiver)) {
				return;
			}

			#if UNITY_EDITOR
			mMousePosition = MousePosition;
			mRawMouseAxisX = RawMouseAxisX;
			mRawMouseAxisY = RawMouseAxisY;
			mRawMovementAxisX = RawMovementAxisX;
			mRawMovementAxisY = RawMovementAxisY;
			mRawScrollWheelAxis = RawScrollWheelAxis;
			mRawInterfaceAxisX = RawInterfaceAxisX;
			mRawInterfaceAxisY = RawInterfaceAxisY;
			#endif

			if (!SoftwareMouse) {
				MousePosition = Input.mousePosition;
			}

			AvailableKeyDown = false;
			//failsafe
			CursorClickDown = Input.GetMouseButtonDown (0);
			CursorClickHold = Input.GetMouseButton (0);
			CursorClickUp = Input.GetMouseButtonUp (0);
			CursorRightClickDown = Input.GetMouseButtonDown (1);
			CursorRightClickHold = Input.GetMouseButton (1);
			CursorRightClickUp = Input.GetMouseButtonUp (1);
			//custom cursor clicks
			if (CursorClickAction != InputControlType.None && !InputFieldActive) {
				CursorClickDown |= (Device.GetControl (CursorClickAction).WasPressed | InputManager.ActiveDevice.GetControl (CursorClickAction).WasPressed);
				CursorClickHold |= (Device.GetControl (CursorClickAction).IsPressed | InputManager.ActiveDevice.GetControl (CursorClickAction).IsPressed);
				CursorClickUp |= (Device.GetControl (CursorClickAction).WasReleased | InputManager.ActiveDevice.GetControl (CursorClickAction).WasReleased);
			}
			if (CursorRightClickAction != InputControlType.None && !InputFieldActive) {
				CursorRightClickDown |= (Device.GetControl (CursorRightClickAction).WasPressed | InputManager.ActiveDevice.GetControl (CursorRightClickAction).WasPressed);
				CursorRightClickHold |= (Device.GetControl (CursorRightClickAction).IsPressed | InputManager.ActiveDevice.GetControl (CursorRightClickAction).IsPressed);
				CursorRightClickUp |= (Device.GetControl (CursorRightClickAction).WasReleased | InputManager.ActiveDevice.GetControl (CursorRightClickAction).WasReleased);
			}
			//left clicks take priority
			if (CursorClickDown) {
				LastMouseClick = 0;
			} else if (CursorRightClickDown) {
				LastMouseClick = 1;
			}

			RawScrollWheelAxis = 0f;
			RawMovementAxisX = 0f;
			RawMovementAxisY = 0f;
			RawInterfaceAxisX = 0f;
			RawInterfaceAxisY = 0f;

			CheckAxis (MouseXAxis, ref RawMouseAxisX, "mouse x", false);
			CheckAxis (MouseYAxis, ref RawMouseAxisY, "mouse y", InvertRawMouseAxis);
			CheckAxis (MoveRight, MoveLeft, ActionSetting.MouseAction.None, ActionSetting.MouseAction.None, ref RawMovementAxisX, string.Empty, false);
			CheckAxis (MoveForward, MoveBack, ForwardMouseAction, BackMouseAction, ref RawMovementAxisY,string.Empty, InvertRawMovementAxis);
			CheckAxis (InterfaceRight, InterfaceLeft, ActionSetting.MouseAction.None, ActionSetting.MouseAction.None, ref RawInterfaceAxisX, string.Empty, false);
			CheckAxis (InterfaceUp, InterfaceDown, ActionSetting.MouseAction.None, ActionSetting.MouseAction.None, ref RawInterfaceAxisY, string.Empty, InvertRawInterfaceAxis);

			if (ScrollWheelAxis != InputControlType.None) {
				RawScrollWheelAxis = (float)Device.GetControl (ScrollWheelAxis).Value + Input.GetAxis ("Mouse ScrollWheel");
			} else {
				RawScrollWheelAxis = Input.GetAxis ("Mouse ScrollWheel");
			}

			TimeStamp = WorldClock.AdjustedRealTime;

			//SURE WOULD BE NICE TO HAVE A 'LAST KEY PRESSED' OPTION
			//THAT DIDN'T REQUIRE ME TO PARSE AN ENUM FROM CHARS
			//THAT DON'T MATCH THE FUCKING ENUM NAMES, JESUS CHRIST UNITY
			for (int i = 0; i < DefaultAvailableKeys.Count; i++) {
				if (Input.GetKeyDown (DefaultAvailableKeys [i])) {
					AvailableKeyDown = true;
					LastKey = DefaultAvailableKeys [i];
					LastInputString = Input.inputString;
					break;
				}
			}

			if (mSuspended) {
				return;
			}

			if (!InputFieldActive) {
				CheckKeyDownMappings ();
				CheckKeyHoldMappings ();
				CheckKeyUpMappings ();
			}
			CheckAxisChanges ();
			OnUpdate ();
		}

		public void CheckAxis (InputControlType positive,
		                       InputControlType negative,
		                       ActionSetting.MouseAction positiveMouse,
		                       ActionSetting.MouseAction negativeMouse,
		                       ref float rawAxis,
		                       string failSafe,
		                       bool invert)
		{
			//the latest version of InControl has made this stuff obsolete
			//but I don't want to tear everything up so i'm keeping it as is
			//with just a few modifications
			if (positive != InputControlType.None) {
				rawAxis += (float)InputManager.ActiveDevice.GetControl (positive).Value;
			}
			if (negative != InputControlType.None) {
				rawAxis -= (float)InputManager.ActiveDevice.GetControl (negative).Value;
			}
			
			if (rawAxis == 0f && !string.IsNullOrEmpty (failSafe)) {
				rawAxis += Input.GetAxisRaw (failSafe);
			}

			switch (positiveMouse) {
			case ActionSetting.MouseAction.None:
			default:
				break;

			case ActionSetting.MouseAction.Left:
				if (Input.GetMouseButton (0)) {
					rawAxis += 1f;
				}
				break;

			case ActionSetting.MouseAction.Middle:
				if (Input.GetMouseButton (2)) {
					rawAxis += 1f;
				}
				break;

			case ActionSetting.MouseAction.Right:
				if (Input.GetMouseButton (3)) {
					rawAxis += 1f;
				}
				break;
			}

			switch (negativeMouse) {
			case ActionSetting.MouseAction.None:
			default:
				break;

			case ActionSetting.MouseAction.Left:
				if (Input.GetMouseButton (0)) {
					rawAxis -= 1f;
				}
				break;
				
			case ActionSetting.MouseAction.Middle:
				if (Input.GetMouseButton (2)) {
					rawAxis -= 1f;
				}
				break;
				
			case ActionSetting.MouseAction.Right:
				if (Input.GetMouseButton (3)) {
					rawAxis -= 1f;
				}
				break;
			}
			
			if (invert) {
				rawAxis *= -1;
			}
		}

		public void CheckAxis (InputControlType axis, ref float rawAxis, string failSafe, bool invert)
		{
			rawAxis = 0f;
			//the latest version of InControl has made this stuff obsolete
			//but I don't want to tear everything up so i'm keeping it as is
			//with just a few modifications
			switch (axis) {
			case InputControlType.None:
			case InputControlType.LeftStickX:
				CheckAxis (
					InputControlType.LeftStickRight,
					InputControlType.LeftStickLeft,
					ActionSetting.MouseAction.None,
					ActionSetting.MouseAction.None,
					ref rawAxis,
					failSafe,
					false);
				return;
			case InputControlType.LeftStickY:
				CheckAxis (
					InputControlType.LeftStickDown,
					InputControlType.LeftStickUp,
					ActionSetting.MouseAction.None,
					ActionSetting.MouseAction.None,
					ref rawAxis,
					failSafe,
					invert);
				return;
			case InputControlType.RightStickX:
				CheckAxis (
					InputControlType.RightStickRight,
					InputControlType.RightStickLeft,
					ActionSetting.MouseAction.None,
					ActionSetting.MouseAction.None,
					ref rawAxis,
					failSafe,
					false);
				return;
			case InputControlType.RightStickY:
				CheckAxis (
					InputControlType.RightStickDown,
					InputControlType.RightStickUp,
					ActionSetting.MouseAction.None,
					ActionSetting.MouseAction.None,
					ref rawAxis,
					failSafe,
					invert);
				return;
			default:
				rawAxis = (float)InputManager.ActiveDevice.GetControl (axis).Value;
				break;
			}

			if (rawAxis == 0f && !string.IsNullOrEmpty (failSafe)) {
				rawAxis += Input.GetAxisRaw (failSafe);
			}
			
			if (invert) {
				rawAxis *= -1;
			}
		}

		protected T ConvertToEnum (int enumValue)
		{
			//LET ME CONSTRAIN TO TYPE ENUM C# FFS
			return EnumUtils.ParseEnum <T> (enumValue, false);
		}

		protected void Send (T action, double timeStamp)
		{
			if (HasInterfaceReceiver) {
				if (InterfaceReceiver (action, TimeStamp)) {//send to interface first
					if (HasPlayerReceiver) {
						PlayerReceiver (action, TimeStamp);//if that doesn't score a hit, send to player
						//see if any actions are supposed to be daisy-chained
						List <T> daisyChainedActions = null;
						//TODO prevent endless daisy chains!
						if (mDaisyChains.TryGetValue (action, out daisyChainedActions)) {
							for (int i = 0; i < daisyChainedActions.Count; i++) {
								Send (daisyChainedActions [i], timeStamp);
							}
						}
					}
				}
			}
		}

		InputControlType[] opposingControls = null;

		protected void CheckKeyDownMappings ()
		{
			var enumerator = mKeyDownMappings.GetEnumerator ();
			//first pass
			while (enumerator.MoveNext ()) {
				keyMapping = enumerator.Current;
				bool canPress = true;
				if (Device.GetControl (keyMapping.Key).WasPressed || InputManager.ActiveDevice.GetControl (keyMapping.Key).WasPressed) {
					if (mOpposingControls.TryGetValue (keyMapping.Key, out opposingControls)) {
						for (int i = 0; i < opposingControls.Length; i++) {
							float thisValue = Mathf.Max (Mathf.Abs (Device.GetControl (keyMapping.Key).Value), Mathf.Abs (InputManager.ActiveDevice.GetControl (keyMapping.Key)));
							float otherValue = Mathf.Max (Mathf.Abs (Device.GetControl (opposingControls [i]).Value), Mathf.Abs (InputManager.ActiveDevice.GetControl (opposingControls [i])));
							if (thisValue < otherValue) {
								//Debug.Log ("An opposing control was pressed and its value (" + opposingControls [i].ToString () + ") was greater than our value (" + keyMapping.Key.ToString ());
								canPress = false;
								break;
							}
						}
					}
					opposingControls = null;
					//Debug.Log("Key " + keyMapping.Key.ToString() + " was pressed in " + GetType().Name);
					if (canPress) {
						LastControllerAction = keyMapping.Key;
						for (int i = 0; i < keyMapping.Value.Count; i++) {
							//Debug.Log("Sending " + keyMapping.Value[i].ToString() + " in " + GetType().Name);
							Send (keyMapping.Value [i], TimeStamp);
						}
					}
				}
			}
		}

		protected void CheckKeyHoldMappings ()
		{
			var enumerator = mKeyHoldMappings.GetEnumerator ();
			while (enumerator.MoveNext ()) {
				//foreach (KeyValuePair<KeyCode, List<T>> keyMapping in mKeyHoldMappings) {
				keyMapping = enumerator.Current;
				if (Device.GetControl (keyMapping.Key).IsPressed || InputManager.ActiveDevice.GetControl (keyMapping.Key).IsPressed) {
					for (int i = 0; i < keyMapping.Value.Count; i++) {
						Send (keyMapping.Value [i], TimeStamp);
					}
				}
			}
		}

		protected void CheckKeyUpMappings ()
		{
			var enumerator = mKeyUpMappings.GetEnumerator ();
			while (enumerator.MoveNext ()) {
				//foreach (KeyValuePair <KeyCode, List <T>> keyMapping in mKeyUpMappings) {
				keyMapping = enumerator.Current;
				if (Device.GetControl (keyMapping.Key).WasReleased || InputManager.ActiveDevice.GetControl (keyMapping.Key).WasReleased) {
					for (int i = 0; i < keyMapping.Value.Count; i++) {
						Send (keyMapping.Value [i], TimeStamp);
					}
				}
			}
		}

		protected void CheckAxisChanges ()
		{
			var enumerator = mAxisChangeMappings.GetEnumerator ();
			while (enumerator.MoveNext ()) {
				keyMapping = enumerator.Current;
				InputControl d = InputManager.ActiveDevice.GetControl (keyMapping.Key);
				InputControl c = Device.GetControl (keyMapping.Key);
				if ((c.HasChanged || c.Value != 0f) || (d.HasChanged || d.Value != null)) {
					for (int i = 0; i < keyMapping.Value.Count; i++) {
						Send (keyMapping.Value [i], TimeStamp);
					}
				}
			}
		}

		public static float gMinAxisChange = 0.005f;

		protected virtual void OnUpdate ()
		{
			return;
		}

		protected virtual void OnPushSettings ()
		{
			return;
		}

		public override void Initialize ()
		{
			DefaultAvailableKeys.Add (KeyCode.A);
			DefaultAvailableKeys.Add (KeyCode.B);
			DefaultAvailableKeys.Add (KeyCode.C);
			DefaultAvailableKeys.Add (KeyCode.D);
			DefaultAvailableKeys.Add (KeyCode.E);
			DefaultAvailableKeys.Add (KeyCode.F);
			DefaultAvailableKeys.Add (KeyCode.G);
			DefaultAvailableKeys.Add (KeyCode.H);
			DefaultAvailableKeys.Add (KeyCode.I);
			DefaultAvailableKeys.Add (KeyCode.J);
			DefaultAvailableKeys.Add (KeyCode.K);
			DefaultAvailableKeys.Add (KeyCode.L);
			DefaultAvailableKeys.Add (KeyCode.M);
			DefaultAvailableKeys.Add (KeyCode.N);
			DefaultAvailableKeys.Add (KeyCode.O);
			DefaultAvailableKeys.Add (KeyCode.P);
			DefaultAvailableKeys.Add (KeyCode.Q);
			DefaultAvailableKeys.Add (KeyCode.R);
			DefaultAvailableKeys.Add (KeyCode.S);
			DefaultAvailableKeys.Add (KeyCode.T);
			DefaultAvailableKeys.Add (KeyCode.U);
			DefaultAvailableKeys.Add (KeyCode.V);
			DefaultAvailableKeys.Add (KeyCode.W);
			DefaultAvailableKeys.Add (KeyCode.X);
			DefaultAvailableKeys.Add (KeyCode.Y);
			DefaultAvailableKeys.Add (KeyCode.Z);
			DefaultAvailableKeys.Add (KeyCode.Alpha0);
			DefaultAvailableKeys.Add (KeyCode.Alpha1);
			DefaultAvailableKeys.Add (KeyCode.Alpha2);
			DefaultAvailableKeys.Add (KeyCode.Alpha3);
			DefaultAvailableKeys.Add (KeyCode.Alpha4);
			DefaultAvailableKeys.Add (KeyCode.Alpha5);
			DefaultAvailableKeys.Add (KeyCode.Alpha6);
			DefaultAvailableKeys.Add (KeyCode.Alpha7);
			DefaultAvailableKeys.Add (KeyCode.Alpha8);
			DefaultAvailableKeys.Add (KeyCode.Alpha9);

			DefaultAvailableKeys.Add (KeyCode.Keypad0);
			DefaultAvailableKeys.Add (KeyCode.Keypad1);
			DefaultAvailableKeys.Add (KeyCode.Keypad2);
			DefaultAvailableKeys.Add (KeyCode.Keypad3);
			DefaultAvailableKeys.Add (KeyCode.Keypad4);
			DefaultAvailableKeys.Add (KeyCode.Keypad5);
			DefaultAvailableKeys.Add (KeyCode.Keypad6);
			DefaultAvailableKeys.Add (KeyCode.Keypad7);
			DefaultAvailableKeys.Add (KeyCode.Keypad8);
			DefaultAvailableKeys.Add (KeyCode.Keypad9);
			DefaultAvailableKeys.Add (KeyCode.KeypadDivide);
			DefaultAvailableKeys.Add (KeyCode.KeypadEnter);
			DefaultAvailableKeys.Add (KeyCode.KeypadEquals);
			DefaultAvailableKeys.Add (KeyCode.KeypadMinus);
			DefaultAvailableKeys.Add (KeyCode.KeypadMultiply);
			DefaultAvailableKeys.Add (KeyCode.KeypadPeriod);
			DefaultAvailableKeys.Add (KeyCode.KeypadPlus);

			DefaultAvailableKeys.Add (KeyCode.Semicolon);
			DefaultAvailableKeys.Add (KeyCode.Quote);
			DefaultAvailableKeys.Add (KeyCode.Comma);
			DefaultAvailableKeys.Add (KeyCode.Period);

			DefaultAvailableKeys.Add (KeyCode.Space);
			DefaultAvailableKeys.Add (KeyCode.Escape);
			DefaultAvailableKeys.Add (KeyCode.Return);
			DefaultAvailableKeys.Add (KeyCode.LeftArrow);
			DefaultAvailableKeys.Add (KeyCode.RightArrow);
			DefaultAvailableKeys.Add (KeyCode.UpArrow);
			DefaultAvailableKeys.Add (KeyCode.DownArrow);
			DefaultAvailableKeys.Add (KeyCode.Tab);
			DefaultAvailableKeys.Add (KeyCode.Delete);
			DefaultAvailableKeys.Add (KeyCode.PageUp);
			DefaultAvailableKeys.Add (KeyCode.PageDown);
			DefaultAvailableKeys.Add (KeyCode.End);
			DefaultAvailableKeys.Add (KeyCode.Home);
			DefaultAvailableKeys.Add (KeyCode.Minus);
			DefaultAvailableKeys.Add (KeyCode.Equals);

			DefaultAvailableKeys.Add (KeyCode.LeftShift);
			DefaultAvailableKeys.Add (KeyCode.LeftAlt);
			DefaultAvailableKeys.Add (KeyCode.LeftBracket);
			DefaultAvailableKeys.Add (KeyCode.LeftControl);
			DefaultAvailableKeys.Add (KeyCode.LeftShift);
			DefaultAvailableKeys.Add (KeyCode.RightShift);
			DefaultAvailableKeys.Add (KeyCode.RightAlt);
			DefaultAvailableKeys.Add (KeyCode.RightBracket);
			DefaultAvailableKeys.Add (KeyCode.RightControl);
			DefaultAvailableKeys.Add (KeyCode.RightShift);

			DefaultAvailableKeys.Add (KeyCode.Delete);
			DefaultAvailableKeys.Add (KeyCode.Backspace);

			DefaultAvailableAxis.Add (InputControlType.None);
			DefaultAvailableAxis.Add (InputControlType.DPadX);
			DefaultAvailableAxis.Add (InputControlType.DPadY);
			DefaultAvailableAxis.Add (InputControlType.LeftStickX);
			DefaultAvailableAxis.Add (InputControlType.LeftStickY);
			DefaultAvailableAxis.Add (InputControlType.RightStickX);
			DefaultAvailableAxis.Add (InputControlType.RightStickY);
			//DefaultAvailableAxis.Add(InputControlType.ScrollWheel);

			DefaultAvailableActions.Add (InputControlType.None);
			DefaultAvailableActions.Add (InputControlType.Action1);
			DefaultAvailableActions.Add (InputControlType.Action2);
			DefaultAvailableActions.Add (InputControlType.Action3);
			DefaultAvailableActions.Add (InputControlType.Action4);
			DefaultAvailableActions.Add (InputControlType.LeftTrigger);
			DefaultAvailableActions.Add (InputControlType.LeftBumper);
			DefaultAvailableActions.Add (InputControlType.LeftStickButton);
			DefaultAvailableActions.Add (InputControlType.RightTrigger);
			DefaultAvailableActions.Add (InputControlType.RightBumper);
			DefaultAvailableActions.Add (InputControlType.RightStickButton);
			DefaultAvailableActions.Add (InputControlType.Menu);
			DefaultAvailableActions.Add (InputControlType.Start);
			DefaultAvailableActions.Add (InputControlType.Button1);
			DefaultAvailableActions.Add (InputControlType.Button2);
			DefaultAvailableActions.Add (InputControlType.Button3);
			DefaultAvailableActions.Add (InputControlType.Button4);
			DefaultAvailableActions.Add (InputControlType.Button5);
			DefaultAvailableActions.Add (InputControlType.Button6);
			DefaultAvailableActions.Add (InputControlType.Button7);
			DefaultAvailableActions.Add (InputControlType.Button8);
			DefaultAvailableActions.Add (InputControlType.Button9);
			DefaultAvailableActions.Add (InputControlType.Button10);

			DefaultAvailableMouseButtons.Add (ActionSetting.MouseAction.None);
			DefaultAvailableMouseButtons.Add (ActionSetting.MouseAction.Left);
			DefaultAvailableMouseButtons.Add (ActionSetting.MouseAction.Right);
			DefaultAvailableMouseButtons.Add (ActionSetting.MouseAction.Middle);

			PushDefaulSettings ();

			base.Initialize ();
		}

		#region binding search for creating device profile

		public InputControlType GetActionAxis (ActionSetting.InputAxis axis)
		{
			InputControlType control = InputControlType.None;
			for (int i = 0; i < CurrentActionSettings.Count; i++) {
				ActionSetting a = CurrentActionSettings [i];
				if (a.Axis == axis) {
					control = a.Controller;
					break;
				}
			}
			return control;
		}

		public InputControlType GetActionBinding (int action)
		{
			InputControlType control = InputControlType.None;
			for (int i = 0; i < CurrentActionSettings.Count; i++) {
				ActionSetting a = CurrentActionSettings [i];
				if (a.Action == action) {
					control = a.Controller;
					break;
				}
			}
			return control;
		}

		public bool GetMouseBinding (InputControlType controllerAction, ref ActionSetting.MouseAction mouseBinding)
		{
			mouseBinding = ActionSetting.MouseAction.None;
			for (int i = 0; i < CurrentActionSettings.Count; i++) {
				ActionSetting a = CurrentActionSettings [i];
				if (a.Action > 0 && a.Controller == controllerAction) {
					mouseBinding = a.Mouse;
					break;
				}
			}
			return mouseBinding != ActionSetting.MouseAction.None;
		}

		public bool GetKeyBinding (InputControlType controllerAction, bool isAxis, bool axisX, ref KeyCode keyBinding)
		{
			keyBinding = KeyCode.None;
			for (int i = 0; i < CurrentActionSettings.Count; i++) {
				ActionSetting a = CurrentActionSettings [i];
				if (isAxis && a.AxisSetting) {
					if (axisX) {
						if (a.Controller == controllerAction) {
							keyBinding = a.KeyX;
							break;
						}
					} else {
						if (a.Controller == controllerAction) {
							keyBinding = a.KeyY;
							break;
						}
					}
				} else if (a.Action > 0 && a.Controller == controllerAction) {
					keyBinding = a.Key;
					break;
				}
			}
			return keyBinding != KeyCode.None;
		}

		public bool GetKeyBinding (InputControlType controllerAction, ref KeyCode keyBinding)
		{
			return GetKeyBinding (controllerAction, false, false, ref keyBinding);
		}

		public string GetFailsafeAxis (InputControlType axis)
		{
			return string.Empty;
		}

		public float GetOpposingAxisValue (InputControlType input, string failSafe)
		{
			float opposingValue = 0f;
			float device1 = 0f;
			float device2 = 0f;
			switch (input) {
			case InputControlType.LeftStickX:
				device1 = Mathf.Abs (Device.LeftStick.X);
				device2 = Mathf.Abs (InputManager.ActiveDevice.LeftStick.X);
				opposingValue = Mathf.Max (device1, device2);
				break;

			case InputControlType.LeftStickY:
				device1 = Mathf.Abs (Device.LeftStick.Y);
				device2 = Mathf.Abs (InputManager.ActiveDevice.LeftStick.Y);
				opposingValue = Mathf.Max (device1, device2);
				break;

			case InputControlType.RightStickX:
				device1 = Mathf.Abs (Device.RightStick.X);
				device2 = Mathf.Abs (InputManager.ActiveDevice.RightStick.X);
				opposingValue = Mathf.Max (device1, device2);
				break;

			case InputControlType.RightStickY:
				device1 = Mathf.Abs (Device.RightStick.Y);
				device2 = Mathf.Abs (InputManager.ActiveDevice.RightStick.Y);
				opposingValue = Mathf.Max (device1, device2);
				break;

			case InputControlType.DPadUp:
			case InputControlType.DPadDown:
			case InputControlType.DPadY:
				CheckAxis (InputControlType.DPadX, ref opposingValue, failSafe, false);
				opposingValue = Mathf.Abs (opposingValue);
				break;

			case InputControlType.DPadLeft:
			case InputControlType.DPadRight:
			case InputControlType.DPadX:
				CheckAxis (InputControlType.DPadY, ref opposingValue, failSafe, false);
				opposingValue = Mathf.Abs (opposingValue);
				break;

			default:
				break;
			}
			return opposingValue;
		}

		public bool GetKeyAxis (InputControlType axis, ref KeyCode keyX, ref KeyCode keyY)
		{
			keyX = KeyCode.None;
			keyY = KeyCode.None;
			for (int i = 0; i < CurrentActionSettings.Count; i++) {
				ActionSetting a = CurrentActionSettings [i];
				if (a.AxisSetting && a.Controller == axis) {
					keyX = a.KeyX;
					keyY = a.KeyY;
					break;
				}
			}
			return keyX != KeyCode.None && keyY != KeyCode.None;
		}

		#endregion

		protected KeyValuePair <InputControlType, List <T>> keyMapping;
		protected Dictionary <InputControlType, List <T>> mKeyDownMappings = new Dictionary <InputControlType, List <T>> ();
		protected Dictionary <InputControlType, List <T>> mKeyUpMappings = new Dictionary <InputControlType, List <T>> ();
		protected Dictionary <InputControlType, List <T>> mKeyHoldMappings = new Dictionary <InputControlType, List <T>> ();
		protected Dictionary <InputControlType, List <T>> mAxisChangeMappings = new Dictionary<InputControlType, List<T>> ();
		protected Dictionary <T, List <T>> mDaisyChains = new Dictionary<T, List<T>> ();
		//event listners for NGUI
		protected bool mHover = false;
		protected bool mHoverChanged = false;
		protected bool mClick = false;
		protected bool mDoubleClick = false;
		protected bool mFallThroughEvent = false;
		protected bool mClickEventSent = false;
		protected bool mSuspended = false;
		//protected Dictionary <T, List <ActionReceiver <T>>> mListeners = new Dictionary <T, List <ActionReceiver <T>>> ( );

		#region NGUI functions

		//TODO actually implement these
		public void OnHover (bool isOver)
		{
			if (isOver != mHover) {
				mHover = isOver;
				mHoverChanged = true;
				mFallThroughEvent = true;
			}
		}
		//– Sent out when the mouse hovers over the collider or moves away from it. Not sent on touch-based devices.
		public void OnPress (bool isDown)
		{
			//– Sent when a mouse button (or touch event) gets pressed over the collider (with ‘true’) and when it gets released (with ‘false’, sent to the same collider even if it’s released elsewhere).
		}

		public void OnClick ()
		{
			mClick = true;
			mFallThroughEvent = true;
		}
		//— Sent to a mouse button or touch event gets released on the same collider as OnPress. UICamera.currentTouchID tells you which button was clicked.
		public void OnDoubleClick ()
		{
			mDoubleClick = true;
			mFallThroughEvent = true;
		}
		//— Sent when the click happens twice within a fourth of a second. UICamera.currentTouchID tells you which button was clicked.
		public void OnSelect (bool selected)
		{
			//Debug.Log ("OnSelect");
		}
		//– Same as OnClick, but once a collider is selected it will not receive any further OnSelect events until you select some other collider.
		public void OnDrag (Vector2 delta)
		{
			//Debug.Log ("OnDrag");
		}
		//– Sent when the mouse or touch is moving in between of OnPress(true) and OnPress(false).
		public void OnDrop (GameObject drag)
		{
			//Debug.Log ("OnDrop");
		}
		//– Sent out to the collider under the mouse or touch when OnPress(false) is called over a different collider than triggered the OnPress(true) event. The passed parameter is the game object of the collider that received the OnPress(true) event.
		public void OnInput (string text)
		{
			//Debug.Log ("OnInput: " + text);
		}
		//– Sent to the same collider that received OnSelect(true) message after typing something. You likely won’t need this, but it’s used by UIInput
		public void OnTooltip (bool show)
		{
			//Debug.Log ("OnTooltip: " + show);
		}
		//– Sent after the mouse hovers over a collider without moving for longer than tooltipDelay, and when the tooltip should be hidden. Not sent on touch-based devices.
		public void OnScroll (float delta)
		{
			//Debug.Log ("OnScroll: " + delta);
		}
		//is sent out when the mouse scroll wheel is moved.
		public void OnKey (KeyCode key)
		{
			////Debug.Log ("OnKey: " + key);
		}
		//is sent when keyboard or controller input is used.

		#endregion

	}
}
