using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Frontiers.World.WIScripts
{
	public class FillStackContainer : WIScript
	{
		public FillStackContainerState State = new FillStackContainerState ();

		public override bool UnloadWhenStacked {
			get {
				return State.HasBeenFilled;
			}
		}

		public override void InitializeTemplate ()
		{
			State.NumTimesFilled = 0;
			mIsFilling = false;
		}

		public override void OnInitialized ()
		{
			if (worlditem.Props.Local.CraftedByPlayer) {
				//whoops, we're not suppposed to fill
				Finish ();
				return;
			}
			//temporarily change fill time to OnVisible
			//instead of OnOpen
			State.FillTime |= ContainerFillTime.OnVisible | ContainerFillTime.OnAddToPlayerInventory;

			Container container = null;
			if (worlditem.Is <Container> (out container)) {
				container.OnOpenContainer += OnOpenContainer;
			}

			State.Flags.Size = worlditem.Size;

			worlditem.OnAddedToGroup += OnAddedToGroup;
		}

		public void OnAddedToGroup ()
		{
			//see if there are any flags we need to inherit
			IStackOwner owner = null;
			if (worlditem.Group.HasOwner (out owner)) {
				if (owner == Player.Local) {
					//we've been added to the player inventory
					//don't bother to fill
					Finish ();
					return;
				}

				Structure structure = null;
				if (owner.IsWorldItem && owner.worlditem.Is <Structure> (out structure)) {
					State.Flags.Union (structure.State.StructureFlags);
				}
			}

			mIsFilling = false;
			worlditem.OnAddedToPlayerInventory += OnAddedToPlayerInventory;
			worlditem.OnModeChange += OnModeChange;
			worlditem.OnVisible += OnVisible;
			worlditem.OnActive += OnVisible;

			if (Flags.Check ((uint)ContainerFillTime.OnDie, (uint)State.FillTime, Flags.CheckType.MatchAny)) {
				Damageable damageable = null;
				if (worlditem.Is <Damageable> (out damageable)) {
					damageable.OnDie += OnDie;
				}
			}

			//if this is a character then we'll want to filter our flags with the character flags
			Character character = null;
			if (worlditem.Is <Character> (out character)) {
				State.Flags.Union (character.State.Flags);
				character.OnAccessInventory += OnOpenContainer;
				if (character.HasBank) {
					mBankToFill = character.InventoryBank;
				}
			}
		}

		public void OnDie ()
		{
			TryToFillContainer (false);
		}

		public void OnAddedToPlayerInventory ()
		{
			if (Flags.Check ((uint)ContainerFillTime.OnAddToPlayerInventory, (uint)State.FillTime, Flags.CheckType.MatchAny)) {
				TryToFillContainer (false);
			}
			//now that we're being added to the inventory
			//this script needs to be removed
			Finish ();
		}

		public void OnTrigger ()
		{
			if (Flags.Check ((uint)ContainerFillTime.OnTrigger, (uint)State.FillTime, Flags.CheckType.MatchAny)) {
				TryToFillContainer (false);
			}
		}

		public void OnVisible ()
		{
			if (Flags.Check ((uint)ContainerFillTime.OnVisible, (uint)State.FillTime, Flags.CheckType.MatchAny)) {
				TryToFillContainer (true);
			}
		}

		public void OnOpenContainer ()
		{
			if (Flags.Check ((uint)ContainerFillTime.OnOpen, (uint)State.FillTime, Flags.CheckType.MatchAny)) {
				TryToFillContainer (true);
			}
		}

		public override void OnFinish ()
		{
			Container container = null;
			if (worlditem.Is <Container> (out container)) {
				container.OnOpenContainer -= OnOpenContainer;
			}
			//TODO do we need to do this? we're done
			worlditem.OnAddedToPlayerInventory -= OnAddedToPlayerInventory;
			worlditem.OnModeChange -= OnModeChange;
			worlditem.OnVisible -= OnVisible;
			worlditem.OnActive -= OnVisible;
		}

		protected bool TimeToFill {
			get {
				if (!State.HasBeenFilled)
					return true;

				bool canFill = true;
				double fillTime = 0;
				switch (State.FillInterval) {
				case ContainerFillInterval.Once:
				default:
					canFill = false;
					break;

				case ContainerFillInterval.Hourly:
					fillTime = (State.LastFillTime + WorldClock.gHourCycleRT);
					break;

				case ContainerFillInterval.Daily:
					fillTime = (State.LastFillTime + WorldClock.gDayCycleRT);
					break;

				case ContainerFillInterval.Weekly:
					fillTime = (State.LastFillTime + WorldClock.gDayCycleRT * 7);
					break;

				case ContainerFillInterval.Monthly:
					fillTime = (State.LastFillTime + WorldClock.gMonthCycleRT);
					break;
				}

				return canFill && WorldClock.AdjustedRealTime > fillTime;
			}
		}

		public void TryToFillContainer (bool immediately)
		{
			if (mDestroyed) {
				Debug.Log ("Stack container is destroyed, not filling");
				return;
			}

			Purse purse = null;
			if (worlditem.Is <Purse> (out purse)) {
				//purses are simple
				FillPurse (purse);
				return;
			}

			if (!worlditem.IsStackContainer) {
				return;
			}

			if (mIsFilling) {
				return;
			}

			if (State.HasBeenFilled && !TimeToFill) {
				return;
			}

			mIsFilling = true;
			StartCoroutine (FillContainerOverTime (immediately));
		}

		protected void FillPurse (Purse purse)
		{
			if (State.FillMethod == ContainerFillMethod.SpecificItems) {
				purse.State.Bronze += State.PurseBronze;
				purse.State.Silver += State.PurseSilver;
				purse.State.Gold += State.PurseGold;
				purse.State.Lumen += State.PurseLumen;
				purse.State.Warlock += State.PurseWarlock;
			} else {
				//cumulative
				//TODO this is very temp - use game seed for random values and global settings for spawn values
				if (Flags.Check (State.Flags.Wealth, 0, Flags.CheckType.MatchAny)) {
					purse.State.Bronze += Mathf.FloorToInt (UnityEngine.Random.value * Globals.AveragePurseBronzeCurrency);
					purse.State.Silver += Mathf.FloorToInt (UnityEngine.Random.value * Globals.AveragePurseSilverCurrency);
				}
				if (Flags.Check (State.Flags.Wealth, 1, Flags.CheckType.MatchAny)) {
					purse.State.Bronze += Mathf.FloorToInt (UnityEngine.Random.value * Globals.AveragePurseBronzeCurrency);
					purse.State.Silver += Mathf.FloorToInt (UnityEngine.Random.value * Globals.AveragePurseSilverCurrency);
					purse.State.Gold += Mathf.FloorToInt (UnityEngine.Random.value * Globals.AveragePurseGoldCurrency);
				}
				if (Flags.Check (State.Flags.Wealth, 2, Flags.CheckType.MatchAny)) {
					purse.State.Silver += Mathf.FloorToInt (UnityEngine.Random.value * Globals.AveragePurseSilverCurrency);
					purse.State.Gold += Mathf.FloorToInt (UnityEngine.Random.value * Globals.AveragePurseGoldCurrency);
					purse.State.Lumen += Mathf.FloorToInt (UnityEngine.Random.value * Globals.AveragePurseLumenCurrency);
				}
				if (Flags.Check (State.Flags.Wealth, 4, Flags.CheckType.MatchAny)) {
					purse.State.Silver += Mathf.FloorToInt (UnityEngine.Random.value * Globals.AveragePurseSilverCurrency);
					purse.State.Gold += Mathf.FloorToInt (UnityEngine.Random.value * Globals.AveragePurseGoldCurrency);
					purse.State.Lumen += Mathf.FloorToInt (UnityEngine.Random.value * Globals.AveragePurseLumenCurrency);
				}
			}
			//update our local base currency value so the purse is worth what's really in it
			worlditem.Props.Local.BaseCurrencyValue = purse.State.TotalValue;
			Finish ();
		}

		static List <GenericWorldItem> gMinInstanceItems = new List<GenericWorldItem> ();

		IEnumerator FillContainerOverTime (bool immediately)
		{
			if (!immediately) {
				yield return null;//wait for a tick to let recepticles etc. update properties
			}

			State.LastFillTime = WorldClock.AdjustedRealTime;
			//fill container
			WIStackError error = WIStackError.None;
			WIStackContainer container = worlditem.StackContainer;
			int numDesired = State.NumberOfItems;
			int numAdded = 0;
			int lastItemIndex = 0;
			int maxDuplicates = 3;
			bool continueFilling = true;
			int hashCode = Mathf.Abs ((worlditem.Group.Props.UniqueID + worlditem.FileName).GetHashCode ());
			int numDuplicates = 0;
			bool belowDuplicateThreshold = true;
			GenericWorldItem genericItem = null;
			WICategory category = null;

			IBank bank = null;
			if (State.FillBank) {
				Character character = null;
				if (worlditem.Is <Character> (out character) && character.HasBank) {
					bank = character.InventoryBank;
					Bank.FillWithRandomCurrency (bank, character.State.Flags.Wealth);
				}
			}

			switch (State.NumberOfItemsRandomness) {
			case ContainerFillRandomness.Slight:
			default:
				numDesired = Mathf.Max (1, numDesired + UnityEngine.Random.Range (-1, 1));
				break;

			case ContainerFillRandomness.Moderate:
				numDesired = Mathf.Max (1, numDesired + UnityEngine.Random.Range (-5, 5));
				break;

			case ContainerFillRandomness.Extreme:
				numDesired = Mathf.Max (1, numDesired + UnityEngine.Random.Range (-10, 10));
				break;
			}

			yield return null;

			switch (State.FillMethod) {
			case ContainerFillMethod.AllRandomItemsFromCategory:
			default:
				if (WorldItems.Get.Category (State.WICategoryName, out category)) {
					Dictionary <string,int> itemsSoFar = new Dictionary <string, int> ();
					if (category.HasMinInstanceItems) {
						gMinInstanceItems.Clear ();
						category.GetMinInstanceItems (gMinInstanceItems);
						for (int i = 0; i < gMinInstanceItems.Count; i++) {
							if (itemsSoFar.TryGetValue (gMinInstanceItems [i].PrefabName, out numDuplicates)) {
								numDuplicates++;
								itemsSoFar [gMinInstanceItems [i].PrefabName] = numDuplicates;
							} else {
								itemsSoFar.Add (gMinInstanceItems [i].PrefabName, 1);
							}

							StackItem item = gMinInstanceItems [i].ToStackItem ();

							if (item != null) {
								if (State.AddCurrencyToBank && bank != null && item.Is ("Currency")) {
									bank.Add (Mathf.FloorToInt (item.BaseCurrencyValue), item.CurrencyType);
									item.Clear ();
								} else {
									//add the generic item to the container as a stack item
									if (!Stacks.Add.Items (item, container, ref error)) {
										continueFilling = false;
									} else {
										numAdded++;
									}
								}
							}
						}
						gMinInstanceItems.Clear ();
					}

					yield return null;

					while (continueFilling && category.GetItem (State.Flags, hashCode, ref lastItemIndex, out genericItem)) {
						//make sure we don't have a duplicate
						if (itemsSoFar.TryGetValue (genericItem.PrefabName, out numDuplicates)) {
							numDuplicates++;
							if (numDuplicates < maxDuplicates) {
								itemsSoFar [genericItem.PrefabName] = numDuplicates;
							} else {
								belowDuplicateThreshold = false;
							}
						} else {
							itemsSoFar.Add (genericItem.PrefabName, 1);
						}

						yield return null;

						if (belowDuplicateThreshold) {
							//this might be a currency item - if it is, add it to the bank
							StackItem item = genericItem.ToStackItem ();

							if (State.AddCurrencyToBank && bank != null && item.Is ("Currency")) {
								bank.Add (Mathf.FloorToInt (item.BaseCurrencyValue), item.CurrencyType);
								item.Clear ();
							} else {
								//add the generic item to the container as a stack item
								if (!Stacks.Add.Items (item, container, ref error)) {
									continueFilling = false;
								} else {
									numAdded++;
								}
							}
						}

						//are we done yet?
						if (numAdded >= numDesired || container.IsFull || !belowDuplicateThreshold) {
							continueFilling = false;
						}
						//wait a tick unless we need to finish this immediately
						//if (!immediately) {
						yield return null;
						//}
					}
				}
				break;

			case ContainerFillMethod.OneRandomItemFromCategory:
				if (WorldItems.Get.Category (State.WICategoryName, out category)) {
					if (category.GetItem (State.Flags, hashCode, ref lastItemIndex, out genericItem)) {
						for (int i = 0; i < numDesired; i++) {
							if (!Stacks.Add.Items (genericItem.ToStackItem (), container, ref error)) {
								break;
							} else {
								numAdded++;
							}

							if (container.IsFull) {
								break;
							}
						}
					}
				}
				break;

			case ContainerFillMethod.SpecificItems:
				if (WorldItems.Get.Category (State.WICategoryName, out category)) {
					for (int i = 0; i < State.SpecificItems.Count; i++) {
						GenericWorldItem specificItem = State.SpecificItems [i];
						Stacks.Add.Items (specificItem.ToStackItem (), container, ref error);
					}
				}
				break;
			}

			State.NumTimesFilled++;
			mIsFilling = false;

			yield return null;
			if (worlditem.Is <Stolen> ()) {
				//the goods in a stolen container are also marked as stolen
			Stacks.Add.ScriptToItems (container, "Stolen");
			}

			yield break;
		}

		protected bool mIsFilling = false;
		protected IBank mBankToFill = null;
	}

	[Serializable]
	public class FillStackContainerState
	{
		public int NumberOfItems = 10;
		public int NumTimesFilled = 0;

		public bool HasBeenFilled {
			get {
				return NumTimesFilled > 0;
			}
		}

		public double LastFillTime = -1f;
		public bool FillBank = false;
		public bool AddCurrencyToBank = false;
		public ContainerFillRandomness NumberOfItemsRandomness = ContainerFillRandomness.Slight;
		public ContainerFillInterval FillInterval = ContainerFillInterval.Once;
		public ContainerDuplicateTolerance DuplicateTolerance = ContainerDuplicateTolerance.Low;
		[BitMask (typeof(ContainerFillTime))]
		public ContainerFillTime FillTime = ContainerFillTime.OnOpen;
		public ContainerFillMethod FillMethod = ContainerFillMethod.AllRandomItemsFromCategory;
		[FrontiersCategoryNameAttribute]
		public string WICategoryName;
		public WIFlags Flags = new WIFlags ();
		public List <GenericWorldItem> SpecificItems = new List<GenericWorldItem> ();
		public int PurseBronze = 0;
		public int PurseSilver = 0;
		public int PurseGold = 0;
		public int PurseLumen = 0;
		public int PurseWarlock = 0;
	}
}