using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using Frontiers;
using Pathfinding;
using Pathfinding.RVO;
using ExtensionMethods;

namespace Frontiers.World.WIScripts
{
	public class Character : WIScript, IInventory, IBodyOwner
	{
		public CharacterTemplate Template;
		//set by Characters on spawn
		public CharacterState State = new CharacterState ();
		public GameObject FocusObject;
		public WIGroup CharacterInventoryGroup;
		//brain behavior
		public Action OnRefreshBehavior;
		public Action OnCollectiveThoughtStart;
		public Action OnCollectiveThoughtEnd;
		public CollectiveThought CurrentThought = new CollectiveThought ();
		//stunned/revived
		public Action OnStunned;
		public Action OnRevived;
		public Photosensitive photosensitive;
		public CharacterAnimator animator;
		public Damageable damageable;
		//movement
		public ITerritoryBase TerritoryBase;
		public bool Ghost = false;

		public override bool CanBeCarried {
			get {
				return false;
			}
		}

		public override bool CanEnterInventory {
			get {
				return false;
			}
		}

		public override bool SaveItemOnUnloaded {
			get {	//characters are spawned by action nodes
				//they don't load and save like other worlditems
				return false;
			}
		}

		public override string GenerateUniqueFileName (int increment)
		{
			return State.Name.FileName;
		}

		public override string DisplayNamer (int increment)
		{
			return FullName;
		}

		public override bool AutoIncrementFileName {
			get {	//we're named by Characters manager
				return false;
			}
		}

		public void OnPlayerCollide () {

			if (State.KnowsPlayer) {
				return;
			}

			Talkative t = worlditem.Get <Talkative> ();
			string dtsSpeechName = t.State.DTSSpeechName;
			if (string.IsNullOrEmpty (dtsSpeechName)) {
				//get a dts based on reputation
				int rep = Profile.Get.CurrentGame.Character.Rep.GetPersonalReputation (worlditem.FileName);
				if (rep > 90) {
					dtsSpeechName = Globals.SpeechOnPlayerEncounterRepPerfect;
				} else if (rep > 75) {
					dtsSpeechName = Globals.SpeechOnPlayerEncounterRepHigh;
				} else if (rep > 5) {
					dtsSpeechName = Globals.SpeechOnPlayerEncounterRepDecent;
				} else if (rep > 25) {
					dtsSpeechName = Globals.SpeechOnPlayerEncounterRepLow;
				} else {
					dtsSpeechName = Globals.SpeechOnPlayerEncounterRepTerrible;
				}
			}
			t.GiveSpeech (dtsSpeechName, null);
		}

		public override int OnRefreshHud (int lastHudPriority)
		{
			if (IsDead || IsStunned || IsSleeping) {
				return lastHudPriority;
			}
			Talkative talkative = null;
			if (worlditem.Is <Talkative> (out talkative)) {
				lastHudPriority++;
				GUI.GUIHud.Get.ShowAction (worlditem, UserActionType.ItemUse, "Talk to " + State.Name.FirstName, worlditem.HudTarget, GameManager.Get.GameCamera);
			}
			return lastHudPriority;
		}

		public override Transform HudTargeter ()
		{
			return Body.Transforms.HeadTop;
		}

		public string FullName {
			get {
				string name = string.Empty;
				if (!string.IsNullOrEmpty (State.Name.Prefix)) {
					name = State.Name.Prefix + " ";
				}
				if (!string.IsNullOrEmpty (State.Name.FirstName)) {
					return name + State.Name.FirstName + " " + State.Name.LastName;
				} else {
					return State.Name.GenericIdentifier;
				}
			}
		}

		#region initialization

		public void OnPlayerEnterResidence ()
		{
			if (!State.KnowsPlayer) {
				worlditem.Get <Talkative> ().GiveSpeech (Globals.SpeechOnPlayerEnterResidenceUnknown, null);
				Profile.Get.CurrentGame.Character.Rep.LoseGlobalReputation (Globals.ReputationChangeSmall);
				Profile.Get.CurrentGame.Character.Rep.LosePersonalReputation (
					worlditem.FileName,
					worlditem.DisplayName,
					Globals.ReputationChangeHuge);
			} else {
				worlditem.Get <Talkative> ().GiveSpeech (Globals.SpeechOnPlayerEnterResidenceKnown, null);
			}
		}

		public override void OnInitializedFirstTime ()
		{
			//set the states of our looker & listener using our template
			//this will only happen the first time the character is created
			//after that it will pull this info from its stack item state
			Looker looker = null;
			if (worlditem.Is <Looker> (out looker)) {
				Reflection.CopyProperties (Template.LookerTemplate, looker.State);
				//looker.State = ObjectClone.Clone <LookerState>(Template.LookerTemplate);
			}
			Listener listener = null;
			if (worlditem.Is <Listener> (out listener)) {
				Reflection.CopyProperties (Template.ListenerTemplate, listener.State);
				//listener.State = ObjectClone.Clone <ListenerState>(Template.ListenerTemplate);
			}
			//and we're done!
		}

		public void OnScriptAdded ()
		{
			//this may be added by other scripts (eg Student, Bandit)
			Looker looker = null;
			if (worlditem.Is <Looker> (out looker)) {
				//TODO should we change this?
				Reflection.CopyProperties (Characters.Get.DefaultLookerState, looker.State);
				//looker.State = ObjectClone.Clone <LookerState>(Characters.Get.DefaultLookerState);
				looker.OnSeeItemOfInterest += OnSeeItemOfInterest;
			}
			Listener listener = null;
			if (worlditem.Is <Listener> (out listener)) {
				Reflection.CopyProperties (Template.ListenerTemplate, listener.State);
				//listener.State = ObjectClone.Clone <ListenerState>(Template.ListenerTemplate);
				listener.OnHearItemOfInterest += OnHearItemOfInterest;
			}
		}

		public override void OnInitialized ()
		{
			CharacterBody body = (CharacterBody)mBody;
			body.HairLength = State.HairLength;
			body.HairColor = State.HairColor;

			animator = Body.GetComponent <CharacterAnimator> ();

			worlditem.OnGainPlayerFocus += OnGainPlayerFocus;
			worlditem.OnAddedToGroup += OnAddedToGroup;
			worlditem.OnScriptAdded += OnScriptAdded;
			worlditem.HudTargeter = new HudTargetSupplier (HudTargeter);
			worlditem.OnPlayerCollide += OnPlayerCollide;
			//set this so the body has something to lerp to
			if (mBody != null) {
				//this tells the body what to follow
				Motile motile = null;
				if (worlditem.Is <Motile> (out motile)) {
					if (Template.TemplateType == CharacterTemplateType.UniquePrimary || Template.TemplateType == CharacterTemplateType.UniqueAlternate) {
						motile.State.MotileProps.UseKinematicBody = false;
					}
					motile.BaseAction.WalkingSpeed = true;
					mBody.OnSpawn (motile);
				} else {
					mBody.OnSpawn (this);
				}
				mBody.transform.parent = worlditem.Group.transform;
				mBody.name = worlditem.FileName + "-Body";
				mBody.Initialize (worlditem);
				if (State.Flags.Gender == 1) {
					mBody.Sounds.MotionSoundType = MasterAudio.SoundType.CharacterVoiceMale;
				} else {
					mBody.Sounds.MotionSoundType = MasterAudio.SoundType.CharacterVoiceFemale;
				}
			}

			Container container = null;
			if (worlditem.Is <Container> (out container)) {
				container.CanOpen = false;
				container.CanUseToOpen = false;
				container.OpenText = "Search";
				FillStackContainer fillStackContainer = worlditem.Get <FillStackContainer> ();
				fillStackContainer.State.Flags = State.Flags;
				fillStackContainer.State.FillTime = ContainerFillTime.OnOpen;
			}

			Looker looker = null;
			if (worlditem.Is <Looker> (out looker)) {
				looker.OnSeeItemOfInterest += OnSeeItemOfInterest;
			}
			Listener listener = null;
			if (worlditem.Is <Listener> (out listener)) {
				listener.OnHearItemOfInterest += OnHearItemOfInterest;
			}

			damageable = worlditem.Get<Damageable> ();
			damageable.State.Result = DamageableResult.Die;
			damageable.OnTakeDamage += OnTakeDamage;
			damageable.OnTakeCriticalDamage += OnTakeCriticalDamage;
			damageable.OnTakeOverkillDamage += OnTakeOverkillDamage;
			damageable.OnDie += OnDie;

			mFollowAction = new MotileAction ();
			mFollowAction.Name = "Follow action by Character";
			mFollowAction.Type = MotileActionType.FollowTargetHolder;
			mFollowAction.Expiration = MotileExpiration.TargetOutOfRange;
			mFollowAction.Range = 10f;

			mSleepAction = new MotileAction ();
			mSleepAction.Name = "Sleep action by Character";
			mSleepAction.Type = MotileActionType.Wait;
			mSleepAction.Expiration = MotileExpiration.Never;
			mSleepAction.Range = 10f;

			mFleeThreatAction = new MotileAction ();
			mFleeThreatAction.Name = "Flee threat action by Character";
			mFleeThreatAction.Type = MotileActionType.FleeGoal;
			mFleeThreatAction.Expiration = MotileExpiration.TargetOutOfRange;
			mFleeThreatAction.YieldBehavior = MotileYieldBehavior.DoNotYield;
			mFleeThreatAction.OutOfRange = 10f;
			mFleeThreatAction.Range = Looker.AwarenessDistanceTypeToVisibleDistance (Template.LookerTemplate.AwarenessDistance);

			mPursueGoalAction = new MotileAction ();
			mPursueGoalAction.Name = "Pursue goal action by Character";
			mPursueGoalAction.Type = MotileActionType.FollowGoal;
			mPursueGoalAction.Expiration = MotileExpiration.TargetInRange;
			mPursueGoalAction.YieldBehavior = MotileYieldBehavior.YieldAndWait;
			mPursueGoalAction.Range = Template.MotileTemplate.MotileProps.RVORadius * 1.5f;

			mFocusAction = new MotileAction ();
			mFocusAction.Name = "Focus action by Character";
			mFocusAction.Type = MotileActionType.FocusOnTarget;
			mFocusAction.Expiration = MotileExpiration.TargetOutOfRange;
			mFocusAction.RTDuration = 5f;
			mFocusAction.Range = 10f;
			mFocusAction.OutOfRange = 15f;
			mFocusAction.WalkingSpeed = true;
			mFocusAction.YieldBehavior = MotileYieldBehavior.YieldAndFinish;
		}

		public void OnAddedToGroup ()
		{
			CurrentThought.OnFleeFromIt += FleeFromThing;
			CurrentThought.OnKillIt += AttackThing;
			CurrentThought.OnEatIt += WatchThing;//TODO fix this
			CurrentThought.OnFollowIt += FollowThing;
			CurrentThought.OnWatchIt += WatchThing;

			Motile motile = null;
			if (worlditem.Is <Motile> (out motile)) {
				motile.StartMotileActions ();
			}
			Characters.Get.BodyTexturesAndMaterials (this, Ghost);
		}

		#endregion

		#region stun / sleep / revive

		public bool IsDead {
			get {
				if (damageable == null) {
					return State.IsDead;
				} else if (damageable.State.IsDead) {
					State.IsDead = true;
				} else {
					State.IsDead = false;
				}
				return State.IsDead;
			}
			set {
				if (damageable != null) {
					damageable.IsDead = value;
				}
				State.IsDead = value;
			}
		}

		public bool IsStunned {
			get {
				return mIsStunned;
			}
		}

		public bool IsSleeping {
			get {
				return mIsSleeping;
			}
		}

		public void TryToStun (float stunRTDuration)
		{
			if (mIsStunned || IsDead) {
				return;
			}

			mIsStunned = true;
			StartCoroutine (StunnedOverTime (stunRTDuration));
			OnStunned.SafeInvoke ();
		}

		public void TryToRevive ()
		{
			//doesn't matter if we're dead, we shouldn't be stunned anyway
			mIsStunned = false;
		}

		protected IEnumerator SleepingOverTime (Bed bed)
		{
			//first go to the bed sleep point
			//TODO make this a motile action
			worlditem.tr.position = bed.SleepingPosition;
			Motile motile = null;
			if (worlditem.Is <Motile> (out motile)) {
				mSleepAction.Reset ();
				mSleepAction.IdleAnimation = GameWorld.Get.FlagByName ("IdleAnimation", "Sleeping");
				motile.PushMotileAction (mSleepAction, MotileActionPriority.ForceTop);
				//let them sleep zzzzz
				yield return mSleepAction.WaitForActionToFinish (0.25f);
				//once we wake up, if the bed is not null
				//clear its occupant!
				if (bed != null) {
					bed.Occupant = null;
				}
			}
			mIsSleeping = false;
			yield break;
		}

		protected IEnumerator StunnedOverTime (float RTDuration)
		{
			Body.SetRagdoll (true, 0f);
			//don't think about stuff in the meantime
			enabled = false;
			Motile motile = null;
			if (worlditem.Is <Motile> (out motile)) {
				//stop doing stuff in the meantime
				motile.enabled = false;
			}

			double reviveTime = WorldClock.AdjustedRealTime + RTDuration;
			while (mIsStunned && WorldClock.AdjustedRealTime < reviveTime) {
				double waitUntil = Frontiers.WorldClock.AdjustedRealTime + 1f;
				while (Frontiers.WorldClock.AdjustedRealTime < waitUntil) {
					yield return null;
				}
			}
			//if we're not dead
			if (!IsDead) {
				//revive our body
				Body.SetRagdoll (false, 0f);
				//start thinking about stuff again
				if (motile != null) {
					//start doing stuff again
					motile.enabled = true;
				}
				enabled = true;
				mIsStunned = false;
				OnRevived.SafeInvoke ();
			}
			yield break;
		}

		protected bool mIsStunned = false;
		protected bool mIsSleeping = false;

		#endregion

		#region thinking

		public void FixedUpdate ()
		{
			if (mIsStunned) {
				CurrentThought.Reset ();
				enabled = false;
				return;
			}

			if (!CurrentThought.HasItemOfInterest) {
				CurrentThought.Reset ();
				enabled = false;
				return;
			}

			if (!CurrentThought.StartedThinking) {
				CurrentThought.StartThinking (WorldClock.AdjustedRealTime);
				OnCollectiveThoughtStart.SafeInvoke ();
			} else if (!CurrentThought.IsFinishedThinking (Creature.ShortTermMemoryToRT (State.ShortTermMemory))) {
				//let it keep thinking
				return;
			} else {
				//end the thought
				//use the results to call an action
				//then disable this script so FixedUpdate is no longer called
				OnCollectiveThoughtEnd.SafeInvoke ();
				CurrentThought.TryToSendThought ();
				CurrentThought.Reset ();
				enabled = false;
			}
		}

		#endregion

		public void OnGainPlayerFocus ()
		{
			Motile motile = null;
			if (worlditem.Is <Motile> (out motile)) {
				motile.StartMotileActions ();
			}
			if (State.BroadcastFocus) {
				Player.Get.AvatarActions.ReceiveAction (AvatarAction.NpcFocus, WorldClock.AdjustedRealTime);
			}
			enabled = true;
		}

		public void OnGainPlayerAttention ()
		{
			LookAtPlayer ();
			//create a 'focus on player' action and put it up top
			/*
						MotileAction newMotileAction = new MotileAction();
						newMotileAction.Type = MotileActionType.FocusOnTarget;
						newMotileAction.LiveTarget = Player.Local;
						newMotileAction.Target.FileName = "[Player]";
						newMotileAction.Expiration = MotileExpiration.Duration;
						newMotileAction.YieldBehavior = MotileYieldBehavior.YieldAndFinish;
						newMotileAction.RTDuration = 5.0f;
						//player attention is seldom less important than anything else
						//yield behavior will catch any exceptions to this
						PushMotileAction(newMotileAction, MotileActionPriority.ForceTop);
						*/
		}

		public void OnLosePlayerAttention ()
		{
			//if we're currently focusing on the player, finsih that action
			if (!mFocusAction.IsFinished && mFocusAction.LiveTarget == Player.Local) {//tell it to finish externally
				mFocusAction.TryToFinish ();
			}
		}

		//convenience
		public MotileAction LookAtPlayer ()
		{
			FXManager.Get.SpawnFX (Body.Transforms.HeadTop, "ListenEffect", UnityEngine.Random.value);
			MotileAction lookAtPlayerAction = WatchThingAction (Player.Local);
			lookAtPlayerAction.Name = "LookAtPlayer";
			if (!lookAtPlayerAction.HasStarted) {
				MasterAudio.PlaySound (MasterAudio.SoundType.PlayerInterface, "NpcNoticePlayer");
			}
			return lookAtPlayerAction;
		}

		#region helper functions

		//TODO some of these can be removed, clean this up
		public void ThinkAboutItemOfInterest (IItemOfInterest newItemOfInterest)
		{
			if (CurrentThought.HasItemOfInterest && CurrentThought.StartedThinking) {
				if (CurrentThought.CurrentItemOfInterest != newItemOfInterest) {
					return;
				}
			}
			CurrentThought.Reset (newItemOfInterest);
			if (CurrentThought.HasItemOfInterest) {
				//this will enable FixedUpdate
				//which will start the process of thinking
				enabled = true;
			}
		}

		public void OnSeeItemOfInterest ()
		{
			Looker looker = worlditem.Get <Looker> ();
			ThinkAboutItemOfInterest (looker.LastSeenItemOfInterest);
		}

		public void OnHearItemOfInterest ()
		{
			Listener listener = worlditem.Get <Listener> ();
			ThinkAboutItemOfInterest (listener.LastHeardItemOfInterest);
		}

		public void WatchThing (IItemOfInterest itemOfInterest)
		{
			WatchThingAction (itemOfInterest);
		}

		public void FollowThing (IItemOfInterest itemOfInterest)
		{
			FollowThingAction (itemOfInterest);
		}

		public void FleeFromThing (IItemOfInterest itemOfInterest)
		{
			FleeFromThingAction (itemOfInterest);
		}

		public void AttackThing (IItemOfInterest itemOfInterest)
		{
			AttackThingAction (itemOfInterest);
		}

		public MotileAction WatchThingAction (IItemOfInterest itemOfInterest)
		{
			Motile motile = null;
			if (worlditem.Is <Motile> (out motile)) {
				if (mFocusAction.IsFinished || mFocusAction.LiveTarget != itemOfInterest) {
					mFocusAction.Reset ();
					mFocusAction.LiveTarget = itemOfInterest;
					motile.PushMotileAction (mFocusAction, MotileActionPriority.ForceTop);
				}
			}
			return mFocusAction;
		}

		public MotileAction FollowThingAction (IItemOfInterest itemOfInterest)
		{
			Motile motile = null;
			if (worlditem.Is <Motile> (out motile)) {
				if (mFollowAction.IsFinished || mFollowAction.LiveTarget != itemOfInterest) {
					mFollowAction.Reset ();
					mFollowAction.LiveTarget = itemOfInterest;
					mFollowAction.TerritoryBase = TerritoryBase;//null typically
					motile.PushMotileAction (mFollowAction, MotileActionPriority.ForceTop);
				}
			}
			return mFollowAction;
		}

		public MotileAction GoToThing (IItemOfInterest itemOfInterest)
		{
			Motile motile = null;
			if (worlditem.Is <Motile> (out motile)) {
				if (mPursueGoalAction.IsFinished || mPursueGoalAction.LiveTarget != itemOfInterest || itemOfInterest == null) {
					mPursueGoalAction.Reset ();
					mPursueGoalAction.LiveTarget = itemOfInterest;
					mPursueGoalAction.TerritoryBase = TerritoryBase;//null typically
					motile.PushMotileAction (mPursueGoalAction, MotileActionPriority.ForceTop);
				}
			}
			return mPursueGoalAction;
		}

		public void SleepInBed (Bed bed)
		{
			if (!mIsSleeping) {
				mIsSleeping = true;
				StartCoroutine (SleepingOverTime (bed));
			}
		}

		public MotileAction FleeFromThingAction (IItemOfInterest itemOfInterest)
		{
			Motile motile = null;
			if (worlditem.Is <Motile> (out motile)) {
				if (mFleeThreatAction.IsFinished || mFleeThreatAction.LiveTarget != itemOfInterest) {
					mFleeThreatAction.Reset ();
					mFleeThreatAction.LiveTarget = itemOfInterest;
					mFleeThreatAction.YieldBehavior = MotileYieldBehavior.DoNotYield;
					mFleeThreatAction.TerritoryBase = TerritoryBase;//null typically
					motile.PushMotileAction (mFleeThreatAction, MotileActionPriority.ForceTop);
				}
			}
			return mFleeThreatAction;
		}

		public bool AttackThingAction (IItemOfInterest itemOfInterest)
		{
			bool result = false;
			Hostile hostile = null;
			if (!worlditem.Is <Hostile> (out hostile)) {
				hostile = worlditem.GetOrAdd <Hostile> ();
				//copy the properties quickly
				Reflection.CopyProperties (Template.HostileTemplate, hostile.State);
				//copy the attacks directly
				hostile.TerritoryBase = TerritoryBase;
				hostile.State.Attack1 = ObjectClone.Clone <AttackStyle> (Template.HostileTemplate.Attack1);
				hostile.State.Attack2 = ObjectClone.Clone <AttackStyle> (Template.HostileTemplate.Attack2);
				hostile.OnAttack1Start += OnAttack1;
				hostile.OnAttack2Start += OnAttack2;
				hostile.OnWarn += OnWarn;
				hostile.OnCoolOff += OnCoolOff;
				result = true;
			}
			if (!hostile.HasPrimaryTarget || hostile.PrimaryTarget != itemOfInterest) {
				hostile.PrimaryTarget = itemOfInterest;
				result = true;
			}
			animator.Idling = false;
			animator.WeaponMode = CharacterWeaponMode.BareHands;
			return false;
		}

		public MotileAction FleeFromPlayer ()
		{
			Motile motile = null;
			if (worlditem.Is <Motile> (out motile)) {
				if (mFleeThreatAction.IsFinished || mFleeThreatAction.LiveTarget != Player.Local) {
					mFleeThreatAction.Reset ();
					mFleeThreatAction.LiveTarget = Player.Local;
					mFleeThreatAction.Range = 10f;
					mFleeThreatAction.YieldBehavior = MotileYieldBehavior.DoNotYield;
					motile.PushMotileAction (mFleeThreatAction, MotileActionPriority.ForceTop);
				}
			}
			return mFleeThreatAction;
		}

		public void StopFleeingFromPlayer ()
		{
			if (mFleeThreatAction.HasStarted && mFleeThreatAction.LiveTarget == Player.Local) {
				mFleeThreatAction.TryToFinish ();
			}
		}

		public void StopFleeingFromThing ()
		{
			if (!mFleeThreatAction.IsFinished) {
				mFleeThreatAction.TryToFinish ();
			}
		}

		#endregion

		#region hostile / damageable actions

		public void OnAttack1 ()
		{
			Body.Animator.Attack1 = true;
			Body.Sounds.Refresh ();
		}

		public void OnAttack2 ()
		{
			Body.Animator.Attack2 = true;
			Body.Sounds.Refresh ();
		}

		public void OnWarn ()
		{
			Body.Animator.Warn = true;
			Body.Sounds.Refresh ();
		}

		public void OnCoolOff ()
		{
			Body.Animator.Idling = true;
		}

		public void OnTakeDamage ()
		{
			Body.Animator.TakingDamage = true;
			Body.Sounds.Refresh ();
			Body.SetBloodOpacity (damageable.NormalizedDamage);
			//if the damage sender was the player
			//maybe it's time to freak out a little
			RespondToDamage (damageable.LastDamageSource, damageable.NormalizedDamage, damageable.IsDead);
		}

		public void OnTakeCriticalDamage ()
		{
			Body.Animator.TakingDamage = true;
			Body.Sounds.Refresh ();
			Body.SetBloodOpacity (damageable.NormalizedDamage);
			//if the damage sender was the player
			//maybe it's time to freak out a little
			RespondToDamage (damageable.LastDamageSource, damageable.NormalizedDamage, damageable.IsDead);
		}

		public void OnTakeOverkillDamage ()
		{
			Body.SetBloodOpacity (damageable.NormalizedDamage);
			TryToStun (10f);
			RespondToDamage (damageable.LastDamageSource, damageable.NormalizedDamage, damageable.IsDead);
		}

		protected void RespondToDamage (IItemOfInterest lastDamageSource, float normalizedDamage, bool isDead)
		{
			if (lastDamageSource == null) {
				Debug.Log ("last damage source was null in character");
				return;
			}
			//see if we were damaged by the player
			if (lastDamageSource.IOIType == ItemOfInterestType.Player || (lastDamageSource.IOIType == ItemOfInterestType.WorldItem && WorldItems.IsOwnedByPlayer (lastDamageSource.worlditem))) {
				Profile.Get.CurrentGame.Character.Rep.LosePersonalReputation (worlditem.FileName, worlditem.DisplayName, Globals.ReputationChangeLarge);
				if (!isDead) {
					Talkative talkative = worlditem.Get <Talkative> ();
					talkative.GiveSpeech (Characters.Get.SpeechInResponseToDamage (
						normalizedDamage,
						State.KnowsPlayer,
						Profile.Get.CurrentGame.Character.Rep.GetPersonalReputation (worlditem.FileName),
						State.GlobalReputation), null);

					//become hostile
					if (normalizedDamage > 0.75f) {
						AttackThing (Player.Local);
					}
				}
			}
		}

		public void RespondToAttacker ()
		{
			Debug.Log ("Responding to attacker");
			FleeFromThing (Player.Local);
		}

		public void OnDie ()
		{
			Body.SetBloodOpacity (1f);
			try {
				Container container = null;
				if (worlditem.Is <Container> (out container)) {
					container.CanOpen = true;
					container.CanUseToOpen = true;
					container.OpenText = "Search";
				}
				//TODO link this to rep, not to being a bandit
				if (!worlditem.Is <Bandit> () && damageable.LastDamageSource != null) {
					if (damageable.LastDamageSource.IOIType == ItemOfInterestType.Player || (damageable.LastDamageSource.IOIType == ItemOfInterestType.WorldItem && WorldItems.IsOwnedByPlayer (damageable.LastDamageSource.worlditem))) {
						//MURDERER!
						Debug.Log ("You are now a murderer");
						Profile.Get.CurrentGame.Character.Rep.LoseGlobalReputation (Globals.ReputationChangeMurderer);
					}
				}
			} catch (Exception e) {
				Debug.LogError ("Error when killing character, proceeding normally: " + e.ToString ());
			}
		}

		#endregion

		public override void OnModeChange ()
		{
			if (worlditem.Is (WIMode.Destroyed)) {
				Player.Get.AvatarActions.ReceiveAction (AvatarAction.NpcDie, WorldClock.AdjustedRealTime);
				string deathMessage = worlditem.DisplayName + " has died";
				if (!string.IsNullOrEmpty (damageable.State.CauseOfDeath)) {
					deathMessage += (" of " + damageable.State.CauseOfDeath);
				}
			}
		}

		public void OnCheckingDirection ()
		{

		}

		public void OnEnterLocation (Location location)
		{
			if (location != null && mLastLocationEntered != location) {
				mLastLocationEntered = location;
			}
		}

		public void Leave ()
		{
			GUI.GUIManager.PostInfo (worlditem.DisplayName + " has left.");
			Characters.Get.DespawnCharacter (this, true);
		}

		public override void PopulateExamineList (List<WIExamineInfo> examine)
		{
			if (IsDead) {
				return;
			}
			examine.Add (Template.ExamineInfo);
			examine.Add (new WIExamineInfo ("Their reputation in general is " + ReputationState.ReputationToDescription (State.GlobalReputation)
			+ "; your reputation with them is " + ReputationState.ReputationToDescription (Profile.Get.CurrentGame.Character.Rep.GetPersonalReputation (worlditem.FileName))));
		}

		public override void PopulateRemoveItemSkills (HashSet<string> removeItemSkills)
		{
			removeItemSkills.Add ("Barter");
			removeItemSkills.Add ("Steal");
		}

		#region IInventory implementation

		public string InventoryOwnerName {
			get {
				return worlditem.DisplayName;
			}
		}

		public bool HasItem (IWIBase item, out WIStack stack)
		{
			stack = null;
			if (mContainerEnabler == null) {
				return false;
			}
			if (!mContainerEnabler.HasEnablerStack) {
				return false;
			}
			WIStack nextStack = null;
			List <WIStack> enablerStacks = mContainerEnabler.EnablerStacks;
			for (int i = 0; i < enablerStacks.Count; i++) {
				nextStack = enablerStacks [i];
				if (nextStack.Items.Contains (item)) {
					stack = nextStack;
					break;
				}
			}

			if (stack == null) {
				ShopOwner shopOwner = null;
				if (worlditem.Is <ShopOwner> (out shopOwner)) {
					//are we alive? if so, this can mean any containers that are owned by us in the group
					//start in the group that
					List<Container> containers = new List<Container> ();
					if (WIGroups.GetAllContainers (worlditem.Group, containers)) {
						foreach (Container container in containers) {
							if (Stacks.Find.Item (container.worlditem.StackContainer, item, out stack)) {
								Debug.Log ("Found item in shop owner container");
								break;
							}
						}
					}
				}
			}

			return stack != null;
		}

		public IEnumerator GetInventoryContainer (int currentIndex, bool forward, GetInventoryContainerResult result)
		{
			if (result == null) {
				yield break;
			}

			mOnAccessInventory.SafeInvoke ();

			if (mContainerEnabler == null) {
				//if we don't have an enabler, we need one now
				if (CharacterInventoryGroup == null) {
					//if we don't have a character group, we'll need to create it to store our stuff
					CharacterInventoryGroup = WIGroups.GetOrAdd (worlditem.FileName, WIGroups.Get.World, worlditem);
				}
				//make sure the group is loaded
				CharacterInventoryGroup.Load ();
				//create a new container enabler for our stuff
				mContainerEnabler = Stacks.Create.StackEnabler (CharacterInventoryGroup);
			}

			int totalContainers = 1;
			ShopOwner shopOwner = null;
			if (worlditem.Is <ShopOwner> (out shopOwner)) {
				//are we alive? if so, this can mean any containers that are owned by us in the group
				//start in the group that
				int nextIndex = currentIndex;
				List<Container> containers = new List<Container> ();
				if (WIGroups.GetAllContainers (worlditem.Group, containers)) {
					if (forward) {
						nextIndex = containers.NextIndex <Container> (currentIndex);
					} else {
						nextIndex = containers.PrevIndex <Container> (currentIndex);
					}
					//tell the container that we're opening it, then wait a tick for it to fill
					try {
						containers [nextIndex].OnOpenContainer ();
					} catch (Exception e) {
						Debug.LogException (e);
						yield break;
					}
					yield return null;
					mContainerEnabler.EnablerStack.Items.Clear ();
					Stacks.Display.ItemInEnabler (containers [nextIndex].worlditem, mContainerEnabler);
					result.ContainerEnabler = mContainerEnabler;
					result.ContainerIndex = nextIndex;
					result.TotalContainers = containers.Count;
				}
			} else {
				//if we're not a shop owner
				//then there's exactly one container, our inventory container
				//if we're holding a temporary item then there are two
				if (IsHoldingTemporaryItem) {
					Debug.Log ("We're holding item so we'll return 2 containers");
					//index 0 is our container enabler
					//index 1 is our temporary item
					result.TotalContainers = 2;
					if (currentIndex == 0) {//toggle to our held item
						//Stacks.Display.ItemInEnabler (worlditem, mTemporaryItemEnabler);
						result.ContainerIndex = 1;
						result.ContainerEnabler = mTemporaryItemEnabler;
					} else {//toggle to our container enabler
						Stacks.Display.ItemInEnabler (worlditem, mContainerEnabler);
						result.ContainerIndex = 0;
						result.ContainerEnabler = mContainerEnabler;
					}
				} else {
					Stacks.Display.ItemInEnabler (worlditem, mContainerEnabler);
					result.ContainerIndex = 0;
					result.TotalContainers = 1;
					result.ContainerEnabler = mContainerEnabler;
				}
				//this is always the same
				result.InventoryBank = InventoryBank;
			}
			yield break;
		}

		public IEnumerator AddItems (WIStack stack, int numItems)
		{
			for (int i = 0; i < numItems; i++) {
				Stacks.Pop.Force (stack, true);//TEMP TODO make it not destroy shit!
			}
			yield break;
		}

		public IEnumerator AddItem (IWIBase item)
		{
			yield break;
		}

		public bool HasBank { get { return State.CharacterBank != null; } }

		public Bank InventoryBank { get { return State.CharacterBank; } }

		public Action OnAccessInventory { get { return mOnAccessInventory; } set { mOnAccessInventory = value; } }

		protected Action mOnAccessInventory;
		protected static List <string> mWiScriptTypes = new List <string> () { "Container" };
		protected List <WorldItem> mChildrenOfType = new List <WorldItem> ();
		protected Container mContainer = null;

		#endregion

		#region item holding

		public WIStack HoldTemporaryItem (IWIBase itemToHold)
		{
			//create a temporary item stack if we don't have one
			if (mTemporaryItemEnabler == null) {
				if (CharacterInventoryGroup == null) {
					//if we don't have a character group, we'll need to create it to store our stuff
					CharacterInventoryGroup = WIGroups.GetOrAdd (worlditem.FileName, WIGroups.Get.World, worlditem);
				}
				//make sure the group is loaded
				CharacterInventoryGroup.Load ();
				mTemporaryItemEnabler = Stacks.Create.StackEnabler (CharacterInventoryGroup);
				mTemporaryItemEnabler.UseRawContainer = true;
			} else {
				//drop anything we're holding currently
				DropTemporaryItem ();
			}
			return Stacks.Display.ItemInContainer (itemToHold, mTemporaryItemEnabler.EnablerContainer);
		}

		public WIStack HoldTemporaryItem (WIStack stackToHold)
		{
			//create a temporary item stack if we don't have one
			if (mTemporaryItemEnabler == null) {
				if (CharacterInventoryGroup == null) {
					//if we don't have a character group, we'll need to create it to store our stuff
					CharacterInventoryGroup = WIGroups.GetOrAdd (worlditem.FileName, WIGroups.Get.World, worlditem);
				}
				//make sure the group is loaded
				CharacterInventoryGroup.Load ();
				mTemporaryItemEnabler = Stacks.Create.StackEnabler (CharacterInventoryGroup);
				mTemporaryItemEnabler.UseRawContainer = true;
			} else {
				//drop anything we're holding currently
				DropTemporaryItem ();
			}
			return Stacks.Display.ItemsInContainer (stackToHold, mTemporaryItemEnabler.EnablerContainer);
		}

		public void DropTemporaryItem ()
		{
			if (IsHoldingTemporaryItem) {
				mTemporaryItemEnabler.EnablerContainer.StackList [0].Items.Clear ();//TODO move this operation into stacks class so it's clear what's happening
			}
		}

		public bool IsHoldingTemporaryItem {
			get {
				return mTemporaryItemEnabler != null && mTemporaryItemEnabler.EnablerContainer.HasTopItem;
			}
		}

		public IWIBase HeldItem {
			get {
				return mTemporaryItemEnabler.EnablerStack.TopItem;
			}
		}

		#endregion

		#region IBodyOwner implementation
		public Vector3 Position { get { return worlditem.Position; } set { worlditem.tr.position = value; } }
		public Quaternion Rotation { get { return worlditem.tr.rotation; } }
		public WorldBody Body { get { return mBody; } set { mBody = value; } }
		public bool IsKinematic { get { return true; } }
		public bool Initialized { get { return mInitialized; } }
		public bool IsImmobilized { get { return true; } }
		public bool IsGrounded { get { return true; } }
		public bool UseGravity { get { return false; } }
		public bool IsRagdoll { get { return false; } }
		public bool IsDestroyed { get { return mFinished; } }
		public double CurrentMovementSpeed { get { return 0; } set { } }
		public double CurrentRotationSpeed { get { return 0; } set { } }
		public int CurrentIdleAnimation { get { return 0; } set { } }
		public bool ForceWalk { get { return true; } }

		protected WorldBody mBody;
		#endregion

		protected MotileAction mReturnToDenAction = null;
		protected MotileAction mPursueGoalAction = null;
		protected MotileAction mFleeThreatAction = null;
		protected MotileAction mFocusAction = null;
		protected MotileAction mFollowAction = null;
		protected MotileAction mEatAction = null;
		protected MotileAction mSleepAction = null;
		protected WIStackEnabler mTemporaryItemEnabler;
		protected WIStackEnabler mContainerEnabler;
		protected Location mLastLocationEntered;
		protected Vector3 mPositionLastFrame;
		protected Vector3 mLocalVelocityLastFrame;
		protected Vector3 mGlobalVelocityLastFrame;
	}

	[Serializable]
	public class CharacterState
	{
		public CharacterName Name = new CharacterName ();
		public string OnInitializedActionNode = "Spawn";
		public string LastActionNode = string.Empty;
		public bool IsInsideStructure = false;
		public bool KnowsPlayer = false;
		public int GlobalReputation = 50;
		public int AgeInYears = 25;
		public bool RelatedToPlayer = false;
		public EmotionalState Emotion = EmotionalState.Neutral;
		public bool IsDead = false;
		public bool OwnsParentStructure = false;
		public string ParentStructureName = string.Empty;
		public float AssessmentInterval = 1.0f;
		public float CheckDirectionInterval = 1.0f;
		public string BodyName = string.Empty;
		[FrontiersAvailableModsAttribute ("Character/Body")]
		public string BodyTextureName = string.Empty;
		public string BodyMaskTextureName = string.Empty;
		public string FaceTextureName = string.Empty;
		public string FaceMaskTextureName = string.Empty;
		public string TemplateName = string.Empty;
		public bool BroadcastFocus = false;
		public CharacterHairColor HairColor = CharacterHairColor.Gray;
		public CharacterHairLength HairLength = CharacterHairLength.Short;
		public CharacterEyeColor EyeColor = CharacterEyeColor.Black;
		public CharacterFacialHair FacialHair = CharacterFacialHair.NoBeard;
		public List <string> BodyAccessories = new List <string> ();
		public List <string> ActionNodesVisited = new List <string> ();
		public CharacterFlags Flags = new CharacterFlags ();
		public Bank CharacterBank = null;
		public ShortTermMemoryLength ShortTermMemory = ShortTermMemoryLength.Long;
	}
}