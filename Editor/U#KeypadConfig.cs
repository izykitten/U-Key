using UnityEngine;
using UnityEditor;
using System.Collections;

namespace UwUtils
{
	public class UsharpKeypadConfig : ScriptableWizard
	{
		[Space]
		[Header("Infos")]
		public string Github = "https://github.com/Reava/U-Keypad";
		public string Twitter = "https://twitter.com/Reava_VR";
		public string Patreon = "https://www.patreon.com/Reava";
		public string Discord = "https://discord.gg/TxYwUFKbUS";

		[MenuItem("Tools/U#Keypad/Info")]

		static void CreateWizard()
		{
			ScriptableWizard.DisplayWizard<UwUtilsWizard>("Informations", "Close");
		}

		void OnWizardCreate()
		{
			Github = "https://github.com/Reava/ReavaUwUtils";
			Twitter = "https://twitter.com/Reava_VR";
			Patreon = "https://www.patreon.com/Reava";
			Discord = "https://discord.gg/TxYwUFKbUS";
		}
	}
}