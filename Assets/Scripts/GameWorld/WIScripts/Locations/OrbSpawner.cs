using UnityEngine;
using System.Collections;
using System;

namespace Frontiers.World
{
		public class OrbSpawner : WIScript
		{
				public OrbSpawnerState State = new OrbSpawnerState();
				public Animation DispenseAnimator;
				public ActionNode SpawnerActionNode;
				public Transform SpawnerParent;
				public string DispenseAnimationName;
				public CreatureDen Den;

				public override void OnInitialized()
				{
						Den = worlditem.Get <CreatureDen>();
						WorldClock.Get.TimeActions.Subscribe(TimeActionType.DaytimeStart, new ActionListener(DaytimeStart));
						WorldClock.Get.TimeActions.Subscribe(TimeActionType.NightTimeStart, new ActionListener(NightTimeStart));

						if (WorldClock.IsNight) {
								if (!mDispensingOrbs && !mCallingOrbsHome) {
										mDispensingOrbs = true;
										StartCoroutine(DispenseOrbs());
								}
						}
				}

				public bool DaytimeStart(double timeStamp)
				{
						if (!mInitialized) {
								return true;
						}

						if (!mCallingOrbsHome && !mDispensingOrbs) {
								mCallingOrbsHome = true;
								StartCoroutine(CallOrbsHome());
						}
						return true;
				}

				public bool NightTimeStart(double timeStamp)
				{
						if (!mInitialized) {
								return true;
						}

						if (!mDispensingOrbs && !mCallingOrbsHome) {
								mDispensingOrbs = true;
								StartCoroutine(DispenseOrbs());
						}
						return true;
				}

				protected IEnumerator DispenseOrbs()
				{
						Location location = worlditem.Get <Location>();
						while (location.LocationGroup == null || !location.LocationGroup.Is(WIGroupLoadState.Loaded)) {
								yield return new WaitForSeconds(0.5f);
						}
						while (Den.SpawnedCreatures.Count < State.NumOrbs) {
								//spawn an orb and immobilize it
								Creature orb = null;
								if (!Creatures.SpawnCreature(Den, location.LocationGroup, SpawnerParent.position, out orb)) {
										yield break;
								}
								Motile motile = orb.worlditem.Get <Motile>();
								motile.IsImmobilized = true;
								//start the animation and move the orb along with the spawner parent object
								//the body will follow it automatically
								yield return null;
								try {
										DispenseAnimator[DispenseAnimationName].normalizedTime = 0f;
										DispenseAnimator.Play(DispenseAnimationName);
								} catch (Exception e) {
										Debug.LogError(e.ToString());
										yield break;
								}
								while (DispenseAnimator[DispenseAnimationName].normalizedTime < 1f) {
										orb.worlditem.tr.position = SpawnerParent.position;
										orb.worlditem.tr.rotation = SpawnerParent.rotation;
										yield return null;
								}
								//now release the orb and let it do whatever
								motile.IsImmobilized = false;
								yield return null;
								DispenseAnimator.Stop();
						}
						mDispensingOrbs = false;
						yield break;
				}

				protected IEnumerator CallOrbsHome()
				{
						mCallingOrbsHome = false;
						yield break;
				}

				protected bool mCallingOrbsHome = false;
				protected bool mDispensingOrbs = false;
		}

		[Serializable]
		public class OrbSpawnerState
		{
				public int NumOrbs = 2;
		}
}