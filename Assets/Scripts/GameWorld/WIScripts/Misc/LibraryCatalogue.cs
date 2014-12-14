using UnityEngine;
using System.Collections;
using System;
using Frontiers;
using Frontiers.GUI;
using System.Collections.Generic;
using Frontiers.World.Gameplay;

namespace Frontiers.World
{
		public class LibraryCatalogue : WIScript
		{
				public LibraryCatalogueState State = new LibraryCatalogueState();

				public override void PopulateOptionsList(List <GUIListOption> options, List <string> message)
				{
						if (mBrowseOption == null) {
								mBrowseOption = new GUIListOption("Browse Catalogue", "Browse");
						}

						Library library = null;
						if (Books.Get.Library(State.LibraryName, out library)) {
								//can we browse this catalogue?
								Skill learnedSkill = null;
								if (!string.IsNullOrEmpty(library.RequiredSkill)) {
										//match the icon to the skill
										bool hasLearnedSkill = Skills.Get.HasLearnedSkill(library.RequiredSkill, out learnedSkill);
										mBrowseOption.IconName = learnedSkill.Info.IconName;
										mBrowseOption.IconColor = learnedSkill.SkillIconColor;
										mBrowseOption.BackgroundColor = learnedSkill.SkillBorderColor;
								} else {
										mBrowseOption.BackgroundColor = Colors.Get.MenuButtonBackgroundColorDefault;
										mBrowseOption.IconName = string.Empty;
										mBrowseOption.Disabled = false;
								}
								options.Add(mBrowseOption);
						}
				}

				public void OnPlayerUseWorldItemSecondary(object result)
				{
						OptionsListDialogResult secondaryResult = result as OptionsListDialogResult;
						switch (secondaryResult.SecondaryResult) {
								case "Browse":
										GameObject browserGameObject = GUIManager.SpawnNGUIChildEditor(gameObject, GUIManager.Get.Dialog("NGUILibraryCatalogueBrowser"));
										GUILibraryCatalogueBrowser browser = browserGameObject.GetComponent <GUILibraryCatalogueBrowser>();
										browser.SetLibraryName(State.LibraryName);
										break;

								default:
										break;
						}
				}

				protected GUIListOption mBrowseOption = null;
		}

		[Serializable]
		public class LibraryCatalogueState
		{
				public string LibraryName = "GuildLibrary";
		}
}