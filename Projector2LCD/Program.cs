using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRageMath;

namespace IngameScript
{
	partial class Program : MyGridProgram
	{
		/***************************************/
		/************ CONFIGURATION ************/
		/***************************************/
		private const int ASSEMBLER_EFFICIENCY = 3; // 1 for realistic, 3 for 3x, 10 for 10x

		private readonly int compWidth = 7, ingotWidth = 7, oreWidth = 7; // width of shown numerical fields (including dots and suffixes - k, M, G)
		private readonly int ingotDecimals = 2, oreDecimals = 2; // max decimal digits to show
		private readonly bool inventoryFromSubgrids = false; // consider inventories on subgrids when computing available materials
		private readonly bool refineriesFromSubgrids = false; // consider refineries on subgrids when computing average effectiveness
		private readonly bool autoResizeText = true; // makes the text fit inside the LCD. If true, it forces the font to be Monospace
		private readonly bool fitOn2IfPossible = true; // when true, if no valid third LCD is specified, the script will fit ingots and ores on the second LCD
		private readonly bool onlyShowNeeded = false; // when true it only shows the needed amount of materials
		/**********************************************/
		/************ END OF CONFIGURATION ************/
		/**********************************************/

		/**********************************************/
		/************ LOCALIZATION STRINGS ************/
		/**********************************************/
		private const string lcd1Title = "Components: available | needed | missing";
		private const string lcd2Title = "Ingots: available | needed now/total | missing";
		private const string lcd3Title = "Ores: available | needed now/total | missing";
		private const string lcd1TitleShort = "Components: needed";
		private const string lcd2TitleShort = "Ingots: needed now/total";
		private const string lcd3TitleShort = "Ores: needed now/total";
		private const string monospaceFontName = "Monospace";
		private const string effectivenessString = "Effectiveness:"; // the text shown in terminal which says the current effectiveness (= yield bonus) of the selected refinery
		private const string refineryMessage = "Math done with ~{0:F2}% refinery effectiveness\n({1}{2} ports with yield modules) ({3})";
		private const string refineryMessageCauseUser = "user input";
		private const string refineryMessageCauseAvg = "grid average";
		private const string scrapMetalMessage = "{0} {1} can be used to save {2} {3}";
		private const string thousands = "k", millions = "M", billions = "G";
		private const string noProjectors = "No projecting projector found";
		private const string notProjecting = " is not projecting";
		private const string basicRefineryEffUsed = "^Basic refinery conversion rate";
		private const string noRefineryFound = " (no refinery found)";
		private const string betterYield = " (better yield)";
		private readonly Dictionary<string, string> componentTranslation = new Dictionary<string, string>()
		{
			["BulletproofGlass"] = "Bulletproof Glass",
			["ComputerComponent"] = "Computer",
			["ConstructionComponent"] = "Construction Component",
			["DetectorComponent"] = "Detector Components",
			["Display"] = "Display",
			["EngineerPlushie"] = "Engineer Plushie",
			["ExplosivesComponent"] = "Explosives",
			["GirderComponent"] = "Girder",
			["GravityGeneratorComponent"] = "Gravity Generator Components",
			["InteriorPlate"] = "Interior Plate",
			["LargeTube"] = "Large Steel Tube",
			["MedicalComponent"] = "Medical Components",
			["MetalGrid"] = "Metal Grid",
			["MotorComponent"] = "Motor Component",
			["PowerCell"] = "Power Cell",
			["RadioCommunicationComponent"] = "Radio-Communication Components",
			["ReactorComponent"] = "Reactor Components",
			["SabiroidPlushie"] = "Saberoid Plushie",
			["SmallTube"] = "Small Steel Tube",
			["SolarCell"] = "Solar Cell",
			["SteelPlate"] = "Steel Plate",
			["Superconductor"] = "Superconductor Component",
			["ThrustComponent"] = "Thruster Components",
			["ZoneChip"] = "Zone Chip",
			["PrototechFrame"] = "Prototech Frame",
			["PrototechPanel"] = "Prototech Panel",
			["PrototechCapacitor"] = "Prototech Capacitor",
			["PrototechPropulsionUnit"] = "Prototech Propulsion Unit",
			["PrototechMachinery"] = "Prototech Machinery",
			["PrototechCircuitry"] = "Prototech Circuitry",
			["PrototechCoolingUnit"] = "Prototech Cooling Unit"
		};
		private readonly Dictionary<Ingots, string> ingotTranslation = new Dictionary<Ingots, string>()
		{
			[Ingots.Cobalt] = "Cobalt Ingot",
			[Ingots.Gold] = "Gold Ingot",
			[Ingots.Iron] = "Iron Ingot",
			[Ingots.Magnesium] = "Magnesium Powder",
			[Ingots.Nickel] = "Nickel Ingot",
			[Ingots.Platinum] = "Platinum Ingot",
			[Ingots.Silicon] = "Silicon Wafer",
			[Ingots.Silver] = "Silver Ingot",
			[Ingots.Stone] = "Gravel",
			[Ingots.Uranium] = "Uranium Ingot",
		};
		private readonly Dictionary<Ores, string> oreTranslation = new Dictionary<Ores, string>()
		{
			[Ores.Cobalt] = "Cobalt Ore",
			[Ores.Gold] = "Gold Ore",
			[Ores.Ice] = "Ice Ore",
			[Ores.Iron] = "Iron Ore",
			[Ores.Magnesium] = "Magnesium Ore",
			[Ores.Nickel] = "Nickel Ore",
			[Ores.Platinum] = "Platinum Ore",
			[Ores.Scrap] = "Scrap Metal",
			[Ores.Silicon] = "Silicon Ore",
			[Ores.Silver] = "Silver Ore",
			[Ores.Stone] = "Stone",
			[Ores.Uranium] = "Uranium Ore",
		};
		/*****************************************************/
		/************ END OF LOCALIZATION STRINGS ************/
		/*****************************************************/

		private enum Ingots
		{
			Cobalt, Gold, Iron, Magnesium, Nickel, Platinum, Silicon, Silver, Stone, Uranium
		}

		private enum Ores
		{
			Cobalt, Gold, Ice, Iron, Magnesium, Nickel, Platinum, Scrap, Silicon, Silver, Stone, Uranium
		}

		private static VRage.MyFixedPoint FP(string val)
		{
			return VRage.MyFixedPoint.DeserializeString(val);
		}

		private readonly Dictionary<string, Dictionary<Ingots, VRage.MyFixedPoint>> componentsToIngots = new Dictionary<string, Dictionary<Ingots, VRage.MyFixedPoint>>()
		{
			["BulletproofGlass"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Silicon] = 15 },
			["ComputerComponent"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = FP("0.5"), [Ingots.Silicon] = FP("0.2") },
			["ConstructionComponent"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 8 },
			["DetectorComponent"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 5, [Ingots.Nickel] = 15 },
			["Display"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 1, [Ingots.Silicon] = 5 },
			["EngineerPlushie"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { },
			["ExplosivesComponent"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Silicon] = FP("0.5"), [Ingots.Magnesium] = 2 },
			["GirderComponent"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 6 },
			["GravityGeneratorComponent"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 600, [Ingots.Silver] = 5, [Ingots.Gold] = 10, [Ingots.Cobalt] = 220 },
			["InteriorPlate"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 3 },
			["LargeTube"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 30 },
			["MedicalComponent"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 60, [Ingots.Nickel] = 70, [Ingots.Silver] = 20 },
			["MetalGrid"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 12, [Ingots.Nickel] = 5, [Ingots.Cobalt] = 3 },
			["MotorComponent"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 20, [Ingots.Nickel] = 5 },
			["PowerCell"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 10, [Ingots.Nickel] = 2, [Ingots.Silicon] = 1 },
			["RadioCommunicationComponent"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 8, [Ingots.Silicon] = 1 },
			["ReactorComponent"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 15, [Ingots.Stone] = 20, [Ingots.Silver] = 5 },
			["SabiroidPlushie"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { },
			["SmallTube"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 5 },
			["SolarCell"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Nickel] = 3, [Ingots.Silicon] = 6 },
			["SteelPlate"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 21 },
			["Superconductor"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 10, [Ingots.Gold] = 2 },
			["ThrustComponent"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 30, [Ingots.Cobalt] = 10, [Ingots.Gold] = 1, [Ingots.Platinum] = FP("0.4") },
			// economy comps
			["ZoneChip"] = null, // cannot be assembled
			// prototech comps
			["PrototechFrame"] = null,
			["PrototechPanel"] = null,
			["PrototechCapacitor"] = null,
			["PrototechPropulsionUnit"] = null,
			["PrototechMachinery"] = null,
			["PrototechCircuitry"] = null,
			["PrototechCoolingUnit"] = null,
		};

		private readonly Dictionary<Ores, Ingots> oreToIngot = new Dictionary<Ores, Ingots>()
		{
			[Ores.Cobalt] = Ingots.Cobalt,
			[Ores.Gold] = Ingots.Gold,
			//[Ores.Ice] = null,
			[Ores.Iron] = Ingots.Iron,
			[Ores.Magnesium] = Ingots.Magnesium,
			[Ores.Nickel] = Ingots.Nickel,
			[Ores.Platinum] = Ingots.Platinum,
			[Ores.Scrap] = Ingots.Iron,
			[Ores.Silicon] = Ingots.Silicon,
			[Ores.Silver] = Ingots.Silver,
			[Ores.Stone] = Ingots.Stone,
			[Ores.Uranium] = Ingots.Uranium,
		};

		private readonly Ores[] basicRefineryOres = new Ores[] { Ores.Iron, Ores.Nickel, Ores.Cobalt, Ores.Silicon, Ores.Magnesium, Ores.Stone, Ores.Scrap };

		private readonly Dictionary<Ingots, Ores[]> ingotToOres = new Dictionary<Ingots, Ores[]>()
		{
			[Ingots.Cobalt] = new Ores[] { Ores.Cobalt },
			[Ingots.Gold] = new Ores[] { Ores.Gold },
			[Ingots.Iron] = new Ores[] { Ores.Iron, Ores.Scrap },
			[Ingots.Magnesium] = new Ores[] { Ores.Magnesium },
			[Ingots.Nickel] = new Ores[] { Ores.Nickel },
			[Ingots.Platinum] = new Ores[] { Ores.Platinum },
			[Ingots.Silicon] = new Ores[] { Ores.Silicon },
			[Ingots.Silver] = new Ores[] { Ores.Silver },
			[Ingots.Stone] = new Ores[] { Ores.Stone },
			[Ingots.Uranium] = new Ores[] { Ores.Uranium },
		};

		private readonly Dictionary<Ores, VRage.MyFixedPoint> conversionRates = new Dictionary<Ores, VRage.MyFixedPoint>()
		{
			[Ores.Cobalt] = FP("0.3"),
			[Ores.Gold] = FP("0.01"),
			[Ores.Ice] = 0, // ice is not refined in refinery or basic refinery
			[Ores.Iron] = FP("0.7"),
			[Ores.Magnesium] = FP("0.007"),
			[Ores.Nickel] = FP("0.4"),
			[Ores.Platinum] = FP("0.005"),
			[Ores.Scrap] = FP("0.8"),
			[Ores.Silicon] = FP("0.7"),
			[Ores.Silver] = FP("0.1"),
			[Ores.Stone] = FP("0.014"), // currently ignoring low-efficiency Iron, Nickel and Silicon production from Stone
			[Ores.Uranium] = FP("0.01"),
		};

		private readonly Dictionary<string, double> effectivenessMapping = new Dictionary<string, double>()
		{
			["100"] = 1,
			["109"] = Math.Pow(2, 1 / 8d),
			["119"] = Math.Pow(2, 2 / 8d),
			["130"] = Math.Pow(2, 3 / 8d),
			["141"] = Math.Pow(2, 4 / 8d),
			["154"] = Math.Pow(2, 5 / 8d),
			["168"] = Math.Pow(2, 6 / 8d),
			["183"] = Math.Pow(2, 7 / 8d),
			["200"] = Math.Pow(2, 8 / 8d),
		};

		Dictionary<string, Dictionary<string, int>> blueprints = new Dictionary<string, Dictionary<string, int>>();
		private int maxComponentLength, maxIngotLength, maxOreLength;

		public Program()
		{
			maxComponentLength = 0;
			foreach (var name in componentTranslation.Values)
			{
				if (name.Length > maxComponentLength)
					maxComponentLength = name.Length;
			}

			maxIngotLength = 0;
			foreach (var name in ingotTranslation.Values)
			{
				if (name.Length > maxIngotLength)
					maxIngotLength = name.Length;
			}

			maxOreLength = 0;
			foreach (var name in oreTranslation.Values)
			{
				if (name.Length > maxOreLength)
					maxOreLength = name.Length;
			}
			// account for possible '^' character for ores that can be refined in an Arc Furnace
			foreach (var ore in basicRefineryOres)
			{
				if (oreTranslation[ore].Length + 1 > maxOreLength)
					maxOreLength = oreTranslation[ore].Length + 1;
			}
			if (oreTranslation[Ores.Scrap].Length == maxOreLength)
			{
				maxOreLength++; //Scrap Metal needs 1 more character (asterisk) at the end
			}

			// get data from blockDefinitionData. splitted[0] is component names
			string[] splitted = blockDefinitionData.Split(new char[] { '$' });
			string[] componentNames = splitted[0].Split(new char[] { '*' });
			for (var i = 0; i < componentNames.Length; i++)
				componentNames[i] = "MyObjectBuilder_BlueprintDefinition/" + componentNames[i];

			//$SmallMissileLauncher*(null)=0:4,2:2,5:1,7:4,8:1,4:1*LargeMissileLauncher=0:35,2:8,5:30,7:25,8:6,4:4$
			char[] asterisk = new char[] { '*' };
			char[] equalsign = new char[] { '=' };
			char[] comma = new char[] { ',' };
			char[] colon = new char[] { ':' };

			for (var i = 1; i < splitted.Length; i++)
			{
				// splitted[1 to n] are type names and all associated subtypes
				// blocks[0] is the type name, blocks[1 to n] are subtypes and component amounts
				string[] blocks = splitted[i].Split(asterisk);
				string typeName = "MyObjectBuilder_" + blocks[0];

				for (var j = 1; j < blocks.Length; j++)
				{
					string[] compSplit = blocks[j].Split(equalsign);
					string blockName = typeName + '/' + compSplit[0];

					// add a new dict for the block
					try
					{
						blueprints.Add(blockName, new Dictionary<string, int>());
					}
					catch (Exception)
					{
						Echo("Error adding block: " + blockName);
					}
					var components = compSplit[1].Split(comma);
					foreach (var component in components)
					{
						string[] amounts = component.Split(colon);
						int idx = Convert.ToInt32(amounts[0]);
						int amount = Convert.ToInt32(amounts[1]);
						string compName = componentNames[idx];
						blueprints[blockName].Add(compName, amount);
					}
				}
			}

			if (ingotDecimals < 0)
			{
				Echo("Error: ingotDecimals cannot be negative. Script needs to be restarted.");
				Runtime.UpdateFrequency = UpdateFrequency.None;
				return;
			}

			if (oreDecimals < 0)
			{
				Echo("Error: oreDecimals cannot be negative. Script needs to be restarted.");
				Runtime.UpdateFrequency = UpdateFrequency.None;
				return;
			}

			if (ingotWidth < ingotDecimals)
			{
				Echo("Error: ingotDigits cannot be less than ingotDecimals. Script needs to be restarted.");
				Runtime.UpdateFrequency = UpdateFrequency.None;
				return;
			}

			if (oreWidth < oreDecimals)
			{
				Echo("Error: oreDigits cannot be less than oreDecimals. Script needs to be restarted.");
				Runtime.UpdateFrequency = UpdateFrequency.None;
				return;
			}

			if (!string.IsNullOrEmpty(Storage))
			{
				var props = Storage.Split(';');
				Storage = "";

				try
				{
					projectorName = props[0];
					lcdName1 = props[1];
					lcdName2 = props[2];
					lcdName3 = props[3];
					lightArmor = bool.Parse(props[4]);
					Runtime.UpdateFrequency = (UpdateFrequency)Enum.Parse(typeof(UpdateFrequency), props[5]);
					effectivenessMultiplier = double.Parse(props[6]);
					averageEffectivenesses = bool.Parse(props[7]);
				}
				catch (Exception)
				{
					Echo("Error while trying to restore previous configuration. Script needs to be restarted.");
					projectorName = lcdName1 = lcdName2 = lcdName3 = "";
					lightArmor = true;
					Runtime.UpdateFrequency = UpdateFrequency.None;
					effectivenessMultiplier = 1;
					averageEffectivenesses = true;
					return;
				}
			}
		}

		public Dictionary<string, int> GetComponents(string definition)
		{
			if (!blueprints.ContainsKey(definition))
			{
				string errorText = "Unknown Blocks in Blueprint. Go to https://github.com/Juggernaut93/Projector2Assembler, and follow instructions for BlockDefinitionExtractor.";
				WriteToAll(errorText);
				throw new Exception(errorText);
			}
			return blueprints[definition];
		}

		public void AddComponents(Dictionary<string, int> addTo, Dictionary<string, int> addFrom, int times = 1)
		{
			foreach (KeyValuePair<string, int> component in addFrom)
			{
				if (addTo.ContainsKey(component.Key))
					addTo[component.Key] += component.Value * times;
				else
					addTo[component.Key] = component.Value * times;
			}
		}

		private void SaveProperty(string s)
		{
			Storage += s + ";";
		}

		public void Save()
		{
			Storage = "";
			SaveProperty(projectorName);
			SaveProperty(lcdName1);
			SaveProperty(lcdName2);
			SaveProperty(lcdName3);
			SaveProperty(lightArmor.ToString());
			SaveProperty(Runtime.UpdateFrequency.ToString());
			SaveProperty(effectivenessMultiplier.ToString());
			SaveProperty(averageEffectivenesses.ToString());
		}

		private void AddCountToDict<T>(Dictionary<T, VRage.MyFixedPoint> dic, T key, VRage.MyFixedPoint amount)
		{
			if (dic.ContainsKey(key))
			{
				dic[key] += amount;
			}
			else
			{
				dic[key] = amount;
			}
		}

		private VRage.MyFixedPoint GetCountFromDic<T>(Dictionary<T, VRage.MyFixedPoint> dic, T key)
		{
			if (dic.ContainsKey(key))
			{
				return dic[key];
			}
			return 0;
		}

		private void WriteToAll(string s)
		{
			if (lcd1 != null)
			{
				ShowAndSetFontSize(lcd1, s);
			}
			if (lcd2 != null)
			{
				ShowAndSetFontSize(lcd2, s);
			}
			if (lcd3 != null)
			{
				ShowAndSetFontSize(lcd3, s);
			}
		}

		private List<KeyValuePair<string, int>> GetTotalComponents(IMyProjector projector)
		{
			var blocks = projector.RemainingBlocksPerType;
			char[] delimiters = new char[] { ',' };
			char[] remove = new char[] { '[', ']' };
			Dictionary<string, int> totalComponents = new Dictionary<string, int>();
			foreach (var item in blocks)
			{
				// blockInfo[0] is blueprint, blockInfo[1] is number of required item
				string[] blockInfo = item.ToString().Trim(remove).Split(delimiters, StringSplitOptions.None);

				string blockName = blockInfo[0].Replace(" ", ""); // data in blockDefinitionData is compressed removing spaces
				int amount = Convert.ToInt32(blockInfo[1]);

				AddComponents(totalComponents, GetComponents(blockName), amount);
			}

			bool LargeGrid = projector.BlockDefinition.SubtypeId == "LargeProjector";

			string armorType = "MyObjectBuilder_CubeBlock/";
			if (LargeGrid)
				if (lightArmor)
					armorType += "LargeBlockArmorBlock";
				else
					armorType += "LargeHeavyBlockArmorBlock";
			else
				if (lightArmor)
				armorType += "SmallBlockArmorBlock";
			else
				armorType += "SmallHeavyBlockArmorBlock";

			int armors = projector.RemainingArmorBlocks;
			AddComponents(totalComponents, GetComponents(armorType), armors);

			var compList = totalComponents.ToList();
			compList.Sort((x, y) => string.Compare(TranslateDef(x.Key), TranslateDef(y.Key)));

			return compList;
		}

		private string TranslateDef(string definition)
		{
			return componentTranslation[definition.Replace("MyObjectBuilder_BlueprintDefinition/", "")];
		}

		private string StripDef(string str)
		{
			return str.Replace("MyObjectBuilder_BlueprintDefinition/", "");
		}

		private int GetWholeDigits(VRage.MyFixedPoint amt)
		{
			string amtStr = amt.ToString();
			int pointIdx = amtStr.IndexOf('.');
			if (pointIdx > -1)
			{
				return pointIdx;
			}
			return amtStr.Length;
		}

		private string FormatNumber(VRage.MyFixedPoint amt, int maxWidth, int maxDecimalPlaces)
		{
			//int maxWholeDigits = maxWidth - maxDecimalPlaces - 2;

			int wholeDigits = GetWholeDigits(amt);
			string multiplier = " ";

			if (amt.ToString().Length > maxWidth - 1 && amt >= 1000)
			{
				multiplier = thousands;
				amt = amt * (1 / 1000f);
				wholeDigits = GetWholeDigits(amt);

				if (amt.ToString().Length > maxWidth - 1 && amt >= 1000)
				{
					multiplier = millions;
					amt = amt * (1 / 1000f);
					wholeDigits = GetWholeDigits(amt);

					if (amt.ToString().Length > maxWidth - 1 && amt >= 1000)
					{
						multiplier = billions;
						amt = amt * (1 / 1000f);
						wholeDigits = GetWholeDigits(amt);
					}
				}
			}
			string amtStr = amt.ToString();
			int pointIdx = amtStr.IndexOf('.');
			maxDecimalPlaces = pointIdx == -1 ? 0 : Math.Min(maxDecimalPlaces, amtStr.Length - pointIdx - 1);
			string ret = string.Format("{0," + (maxWidth - 1) + ":F" + Math.Max(0, Math.Min(maxWidth - wholeDigits - 2, maxDecimalPlaces)) + "}" + multiplier, (decimal)amt); // - 1 because of the multiplier
			return ret;
		}

		private List<KeyValuePair<Ingots, VRage.MyFixedPoint>> GetTotalIngots(List<KeyValuePair<string, int>> components)
		{
			Dictionary<Ingots, VRage.MyFixedPoint> ingotsNeeded = new Dictionary<Ingots, VRage.MyFixedPoint>();

			foreach (var pair in components)
			{
				Dictionary<Ingots, VRage.MyFixedPoint> ingots = componentsToIngots[StripDef(pair.Key)];
				if (ingots != null)
				{ 
					foreach (var ing in ingots)
					{
						AddCountToDict<Ingots>(ingotsNeeded, ing.Key, ing.Value * (pair.Value / (float)ASSEMBLER_EFFICIENCY));
					}
				}
			}

			var ingotsList = ingotsNeeded.ToList();
			ingotsList.Sort((x, y) => string.Compare(ingotTranslation[x.Key], ingotTranslation[y.Key]));
			return ingotsList;
		}

		private List<KeyValuePair<Ores, VRage.MyFixedPoint>> GetTotalOres(List<KeyValuePair<Ingots, VRage.MyFixedPoint>> ingots)
		{
			Dictionary<Ores, VRage.MyFixedPoint> oresNeeded = new Dictionary<Ores, VRage.MyFixedPoint>();

			foreach (Ores ore in Enum.GetValues(typeof(Ores)))
			{
				conversionData[ore] = GetConversionData(ore);
			}

			foreach (var pair in ingots)
			{
				foreach (var ore in ingotToOres[pair.Key])
				{
					AddCountToDict<Ores>(oresNeeded, ore, pair.Value * (1 / conversionData[ore].conversionRate));
				}
			}

			var oreList = oresNeeded.ToList();
			oreList.Sort((x, y) => string.Compare(oreTranslation[x.Key], oreTranslation[y.Key]));
			return oreList;
		}

		private struct ConversionData
		{
			public float conversionRate;
			public bool basicRefinery;
		}

		private ConversionData GetConversionData(Ores ore)
		{
			var refConvRate = Math.Min(1f, 1.0f * (float)conversionRates[ore] * (float)effectivenessMultiplier); // refinery now has 1.0 material efficiency multiplier
			var ret = new ConversionData { conversionRate = refConvRate, basicRefinery = false };
			if (basicRefineryOres.Contains(ore))
			{
				var arcConvRate = Math.Min(1f, 0.7f * (float)conversionRates[ore]); // Arc Furnace has no yield ports and 0.7 material efficiency multiplier
				// if there are both refineries and arc furnace, or there is neither, we prefer the best yield
				// or we prefer arc furnace rate when there is one but no refinery
				if ((arcConvRate > refConvRate && (atLeastOnebasicRefinery == atLeastOneRefinery)) || (atLeastOnebasicRefinery && !atLeastOneRefinery))
				{
					ret.conversionRate = arcConvRate;
					ret.basicRefinery = true;
				}
			}
			return ret;
		}

		private double GetRefineryEffectiveness(IMyRefinery r)
		{
			string info = r.DetailedInfo;

			int startIndex = info.IndexOf(effectivenessString) + effectivenessString.Length;
			string perc = info.Substring(startIndex, info.IndexOf("%", startIndex) - startIndex).Trim();
			if (effectivenessMapping.ContainsKey(perc))
				return effectivenessMapping[perc];

			// find sixth numerical value (assumes other languages use non-numerical decimal separator)
			perc = System.Text.RegularExpressions.Regex.Matches(info, @"\d+")[5].Value;
			if (effectivenessMapping.ContainsKey(perc))
				return effectivenessMapping[perc];

			return effectivenessMapping["100"]; // default
		}

		private struct Size
		{
			public int Width, Height;
		}

		private Size GetOutputSize(string text)
		{
			string[] lines = text.Split('\n');
			int i = lines.Length - 1;
			while (string.IsNullOrWhiteSpace(lines[i]))
				i--;
			Size ret = new Size
			{
				Height = i + 1,
				Width = 0
			};
			foreach (var line in lines)
			{
				int len = line.Length;
				if (len > ret.Width)
					ret.Width = len;
			}
			return ret;
		}

		private enum LCDType
		{
			NORMAL, WIDE, OTHER
		}

		private LCDType GetLCDType(IMyTextPanel lcd)
		{
			if (smallLCDs.Contains(lcd.BlockDefinition.SubtypeName))
				return LCDType.NORMAL;
			if (wideLCDs.Contains(lcd.BlockDefinition.SubtypeName))
				return LCDType.WIDE;
			return LCDType.OTHER;
		}

		private LCDType CheckLCD(IMyTextPanel lcd)
		{
			if (lcd == null)
				return LCDType.OTHER;
			var type = GetLCDType(lcd);
			if (type == LCDType.OTHER)
			{
				Echo(string.Format("Warning: {0} is an unsupported type of text panel (too small).", lcd.CustomName));
			}
			return type;
		}

		private void ShowAndSetFontSize(IMyTextPanel lcd, string text)
		{
			lcd.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;
			lcd.WriteText(text);

			if (!autoResizeText)
				return;
			lcd.Font = monospaceFontName;

			Size size = GetOutputSize(text);
			if (size.Width == 0)
				return;

			LCDType type = GetLCDType(lcd);
			float maxWidth = (type == LCDType.WIDE ? wideLCDWidth : LCDWidth) * (1 - lcd.TextPadding * 0.02f); // padding is in percentage, 0.02 = 1/100 * 2 (from both sides)
			float maxHeight = (type == LCDType.WIDE ? wideLCDHeight : LCDHeight) * (1 - lcd.TextPadding * 0.02f); // padding is in percentage, 0.02 = 1/100 * 2 (from both sides)

			float maxFontSizeByWidth = maxWidth / size.Width;
			float maxFontSizeByHeight = maxHeight / size.Height;
			lcd.FontSize = Math.Min(maxFontSizeByWidth, maxFontSizeByHeight);
		}

		/*
		 * VARIABLES TO SAVE
		 */
		private string projectorName = "", lcdName1 = "", lcdName2 = "", lcdName3 = "";
		private bool lightArmor = true;
		private double effectivenessMultiplier = 1;
		private bool averageEffectivenesses = true;
		/*
		 * END OF VARIABLES TO SAVE
		 */

		private IMyTextPanel lcd1, lcd2, lcd3;
		private readonly double log2 = Math.Log(2);
		private const float lcdSizeCorrection = 0.15f;
		private readonly string[] smallLCDs = new string[] { "SmallTextPanel", "SmallLCDPanel", "LargeTextPanel", "LargeLCDPanel" };
		private readonly string[] wideLCDs = new string[] { "SmallLCDPanelWide", "LargeLCDPanelWide" };
		private const float wideLCDWidth = 52.75f - lcdSizeCorrection, wideLCDHeight = 17.75f - lcdSizeCorrection, LCDWidth = wideLCDWidth / 2, LCDHeight = wideLCDHeight;
		//private bool basicRefineryWithNoRefinery = false;
		private bool atLeastOneRefinery = false, atLeastOnebasicRefinery = false;
		private Dictionary<Ores, ConversionData> conversionData = new Dictionary<Ores, ConversionData>();

		public void Main(string argument, UpdateType updateReason)
		{
			if (updateReason != UpdateType.Update100 && !String.IsNullOrEmpty(argument))
			{
				try
				{
					var spl = argument.Split(';');
					projectorName = spl[0];
					if (spl.Length > 1)
						lcdName1 = spl[1];
					if (spl.Length > 2)
						lcdName2 = spl[2];
					if (spl.Length > 3)
						lcdName3 = spl[3];
					if (spl.Length > 4 && spl[4] != "")
						lightArmor = bool.Parse(spl[4]);
					else
						lightArmor = true;
					if (spl.Length > 5 && spl[5] != "")
					{
						effectivenessMultiplier = Math.Pow(2, int.Parse(spl[5]) / 8d); // 2^(n/8) - n=0 => 100% - n=8 => 200%
						averageEffectivenesses = false;
					}
					else
					{
						effectivenessMultiplier = 1;
						averageEffectivenesses = true;
					}
				}
				catch (Exception)
				{
					Echo("Wrong argument(s). Format: [ProjectorName];[LCDName1];[LCDName2];[LCDName3];[lightArmor];[yieldPorts]. See Readme for more info.");
					Runtime.UpdateFrequency = UpdateFrequency.None;
					return;
				}
			}

			lcd1 = GridTerminalSystem.GetBlockWithName(lcdName1) as IMyTextPanel;
			lcd2 = GridTerminalSystem.GetBlockWithName(lcdName2) as IMyTextPanel;
			lcd3 = GridTerminalSystem.GetBlockWithName(lcdName3) as IMyTextPanel;

			if (lcd1 == null && lcd2 == null && lcd3 == null)
			{
				Echo("Error: at least one valid LCD Panel must be specified.");
				Runtime.UpdateFrequency = UpdateFrequency.None;
				return;
			}

			// function already checks if null on the inside and returns OTHER in that case
			if (CheckLCD(lcd1) == LCDType.OTHER)
				lcd1 = null;
			if (CheckLCD(lcd2) == LCDType.OTHER)
				lcd2 = null;
			if (CheckLCD(lcd3) == LCDType.OTHER)
				lcd3 = null;

			// if no errors in arguments, then we can keep the script updating
			Runtime.UpdateFrequency = UpdateFrequency.Update100;

			IMyProjector projector = GridTerminalSystem.GetBlockWithName(projectorName) as IMyProjector;
			if (projector == null)
			{
				// if no proj found by name, search for projecting projectors
				List<IMyProjector> projectors = new List<IMyProjector>();
				GridTerminalSystem.GetBlocksOfType<IMyProjector>(projectors, proj => proj.IsProjecting);

				if (projectors.Count > 0)
				{
					projector = projectors[0];
				}
				else
				{
					Echo(noProjectors + ".");
					WriteToAll(noProjectors);
					return;
				}
			}

			List<IMyRefinery> allRefineries = new List<IMyRefinery>();
			GridTerminalSystem.GetBlocksOfType<IMyRefinery>(allRefineries, refinery => (refinery.CubeGrid == Me.CubeGrid || refineriesFromSubgrids) && refinery.Enabled);
			List<IMyRefinery> refineries = new List<IMyRefinery>();
			List<IMyRefinery> basicRefinerys = new List<IMyRefinery>();
			foreach (var x in allRefineries)
				if (x.BlockDefinition.SubtypeName == "Blast Furnace")
					basicRefinerys.Add(x);
				else
					refineries.Add(x);

			atLeastOneRefinery = refineries.Count > 0;
			atLeastOnebasicRefinery = basicRefinerys.Count > 0;

			if (averageEffectivenesses) // dynamically update average refinery efficiency
			{
				if (refineries.Count == 0)
				{
					effectivenessMultiplier = 1; // no active refineries found; use default
				}
				else
				{
					double sumEff = 0;
					foreach (var r in refineries)
					{
						sumEff += GetRefineryEffectiveness(r);
					}
					effectivenessMultiplier = sumEff / refineries.Count;
				}
			}

			string localProjectorName = projector.CustomName;

			// if projector name is manually specified we have to check if it's projecting
			if (!projector.IsProjecting)
			{
				WriteToAll(localProjectorName + notProjecting);
				return;
			}

			var cubeBlocks = new List<IMyCubeBlock>();
			GridTerminalSystem.GetBlocksOfType<IMyCubeBlock>(cubeBlocks, block => block.CubeGrid == Me.CubeGrid || inventoryFromSubgrids);

			Dictionary<string, VRage.MyFixedPoint> componentAmounts = new Dictionary<string, VRage.MyFixedPoint>();
			Dictionary<Ingots, VRage.MyFixedPoint> ingotAmounts = new Dictionary<Ingots, VRage.MyFixedPoint>();
			Dictionary<Ores, VRage.MyFixedPoint> oreAmounts = new Dictionary<Ores, VRage.MyFixedPoint>();
			bool moddedIngotsOres = false;
			foreach (var b in cubeBlocks)
			{
				if (b.HasInventory)
				{
					for (int i = 0; i < b.InventoryCount; i++)
					{
						var itemList = new List<MyInventoryItem>();
						b.GetInventory(i).GetItems(itemList);
						foreach (var item in itemList)
						{
							if (item.Type.TypeId.Equals("MyObjectBuilder_Component"))
							{
								AddCountToDict(componentAmounts, item.Type.SubtypeId, item.Amount);
							}
							else if (item.Type.TypeId.Equals("MyObjectBuilder_Ingot"))
							{
								try
								{
									AddCountToDict(ingotAmounts, (Ingots)Enum.Parse(typeof(Ingots), item.Type.SubtypeId), item.Amount);
								}
								catch (Exception)
								{
									moddedIngotsOres = true;
								}
							}
							else if (item.Type.TypeId.Equals("MyObjectBuilder_Ore"))
							{
								try
								{
									AddCountToDict(oreAmounts, (Ores)Enum.Parse(typeof(Ores), item.Type.SubtypeId), item.Amount);
								}
								catch (Exception)
								{
									moddedIngotsOres = true;
								}
							}
						}
					}
				}
			}
			if (moddedIngotsOres)
			{
				Echo("WARNING: detected non-vanilla ores or ingots. Modded ores and ingots are ignored by this script.");
			}

			Me.CustomData = "";

			var compList = GetTotalComponents(projector);
			List<KeyValuePair<string, int>> missingComponents = new List<KeyValuePair<string, int>>();
			string output = localProjectorName + "\n" + (onlyShowNeeded ? lcd1TitleShort : lcd1Title).ToUpper() + "\n\n";
			foreach (var component in compList)
			{
				string subTypeId = component.Key.Replace("MyObjectBuilder_BlueprintDefinition/", "");
				var amountPresent = GetCountFromDic(componentAmounts, subTypeId.Replace("Component", ""));
				string componentName = componentTranslation[subTypeId];
				//string separator = "/";
				string amountStr = amountPresent.ToString();
				string neededStr = component.Value.ToString();
				var missing = component.Value - amountPresent;
				missingComponents.Add(new KeyValuePair<string, int>(component.Key, Math.Max(0, missing.ToIntSafe())));
				string missingStr = missing > 0 ? missing.ToString() : "";
				string warnStr = ">>", okStr = "";
				if (lcd1 != null && lcd1.Font.Equals(monospaceFontName))
				{
					componentName = String.Format("{0,-" + maxComponentLength + "}", componentName);
					//separator = "|";
					amountStr = FormatNumber(amountPresent, compWidth, 0);
					neededStr = FormatNumber(component.Value, compWidth, 0);
					missingStr = missing > 0 ? FormatNumber(missing, compWidth, 0) : new string(' ', compWidth);
					warnStr = ">> ";
					okStr = "   ";
				}

				if (onlyShowNeeded)
					output += String.Format("{0}{1} {2}\n", (missing > 0 ? warnStr : okStr), componentName, neededStr);
				else
					output += String.Format("{0}{1} {2}|{3}|{4}\n", (missing > 0 ? warnStr : okStr), componentName, amountStr, neededStr, missingStr);
			}
			if (lcd1 != null)
			{
				ShowAndSetFontSize(lcd1, output);
			}
			Me.CustomData += output + "\n\n";

			var ingotsList = GetTotalIngots(missingComponents);
			var ingotsTotalNeeded = GetTotalIngots(compList);
			List<KeyValuePair<Ingots, VRage.MyFixedPoint>> missingIngots = new List<KeyValuePair<Ingots, VRage.MyFixedPoint>>();
			output = localProjectorName + "\n" + (onlyShowNeeded ? lcd2TitleShort : lcd2Title).ToUpper() + "\n\n";
			//string decimalFmt = (ingotDecimals > 0 ? "." : "") + string.Concat(Enumerable.Repeat("0", ingotDecimals));
			string decimalFmt = (ingotDecimals > 0 ? "." : "") + new string('0', ingotDecimals);
			for (int i = 0; i < ingotsList.Count; i++)
			{
				var ingot = ingotsList[i];
				var amountPresent = GetCountFromDic(ingotAmounts, ingot.Key);
				string ingotName = ingotTranslation[ingot.Key];
				string separator = " | ";
				string normalFmt = "{0:0" + decimalFmt + "}";
				string amountStr = string.Format(normalFmt, (decimal)amountPresent);
				string neededStr = string.Format(normalFmt, (decimal)ingot.Value);
				string totalNeededStr = string.Format(normalFmt, (decimal)ingotsTotalNeeded[i].Value);
				var missing = ingot.Value - amountPresent;
				missingIngots.Add(new KeyValuePair<Ingots, VRage.MyFixedPoint>(ingot.Key, VRage.MyFixedPoint.Max(0, missing)));
				string missingStr = missing > 0 ? string.Format(normalFmt, (decimal)missing) : "";
				string warnStr = ">>", okStr = "";
				if (lcd2 != null && lcd2.Font.Equals(monospaceFontName))
				{
					ingotName = String.Format("{0,-" + maxIngotLength + "}", ingotName);
					separator = "|";
					amountStr = FormatNumber(amountPresent, ingotWidth, ingotDecimals);
					neededStr = FormatNumber(ingot.Value, ingotWidth, ingotDecimals);
					totalNeededStr = FormatNumber(ingotsTotalNeeded[i].Value, ingotWidth, ingotDecimals);
					missingStr = missing > 0 ? FormatNumber(missing, ingotWidth, ingotDecimals) : new string(' ', ingotWidth);
					warnStr = ">> ";
					okStr = "   ";
				}

				if (onlyShowNeeded)
					output += String.Format("{0}{1} {2}/{3}\n", (missing > 0 ? warnStr : okStr), ingotName, neededStr, totalNeededStr);
				else
					output += String.Format("{0}{1} {2}{3}{4}/{5}{3}{6}\n", (missing > 0 ? warnStr : okStr), ingotName, amountStr, separator, neededStr, totalNeededStr, missingStr);
			}
			if (lcd2 != null)
			{
				ShowAndSetFontSize(lcd2, output);
			}
			Me.CustomData += output + "\n\n";
			var output_lcd2 = output;

			var oresList = GetTotalOres(missingIngots);
			var oresTotalNeeded = GetTotalOres(ingotsTotalNeeded);
			//List<KeyValuePair<Ores, VRage.MyFixedPoint>> missingOres = new List<KeyValuePair<Ores, VRage.MyFixedPoint>>();
			if (lcd3 == null && fitOn2IfPossible)
			{
				output = "\n" + (onlyShowNeeded ? lcd3TitleShort : lcd3Title).ToUpper() + "\n\n";
			}
			else
			{
				output = localProjectorName + "\n" + (onlyShowNeeded ? lcd3TitleShort : lcd3Title).ToUpper() + "\n\n";
			}
			//decimalFmt = (oreDecimals > 0 ? "." : "") + string.Concat(Enumerable.Repeat("0", oreDecimals));
			decimalFmt = (oreDecimals > 0 ? "." : "") + new string('0', oreDecimals);
			string scrapOutput = "";
			bool atLeastOneOrePrefersArc = false;
			for (int i = 0; i < oresList.Count; i++)
			{
				var ores = oresList[i];
				var amountPresent = GetCountFromDic(oreAmounts, ores.Key);
				string oreName = oreTranslation[ores.Key] + (ores.Key == Ores.Scrap ? "*" : "");
				if (conversionData[ores.Key].basicRefinery)
				{
					oreName += "^";
					atLeastOneOrePrefersArc = true;
				}
				string separator = " | ";
				string normalFmt = "{0:0" + decimalFmt + "}";
				string amountStr = string.Format(normalFmt, (decimal)amountPresent);
				string neededStr = string.Format(normalFmt, (decimal)ores.Value);
				string totalNeededStr = string.Format(normalFmt, (decimal)oresTotalNeeded[i].Value);
				var missing = ores.Value - amountPresent;
				//missingOres.Add(new KeyValuePair<Ores, VRage.MyFixedPoint>(ores.Key, VRage.MyFixedPoint.Max(0, missing)));
				string missingStr = missing > 0 ? string.Format(normalFmt, (decimal)missing) : "";
				string warnStr = ">>", okStr = "";
				string na = "-", endNa = "";
				if ((lcd3 != null && lcd3.Font.Equals(monospaceFontName)) || (lcd3 == null && fitOn2IfPossible && lcd2 != null && lcd2.Font.Equals(monospaceFontName)))
				{
					oreName = String.Format("{0,-" + maxOreLength + "}", oreName);
					separator = "|";
					amountStr = FormatNumber(amountPresent, oreWidth, oreDecimals);
					neededStr = FormatNumber(ores.Value, oreWidth, oreDecimals);
					totalNeededStr = FormatNumber(oresTotalNeeded[i].Value, oreWidth, oreDecimals);
					missingStr = missing > 0 ? FormatNumber(missing, oreWidth, oreDecimals) : new string(' ', oreWidth);
					warnStr = ">> ";
					okStr = "   ";
					na = new string(' ', (oreWidth - 1) / 2) + "-" + new string(' ', oreWidth - 1 - (oreWidth - 1) / 2);
					endNa = new string(' ', oreWidth);
				}
				if (ores.Key == Ores.Scrap)
				{
					if (amountPresent > 0) // if 0 scrap, ignore row
					{
						//string na = string.Concat(Enumerable.Repeat(" ", (oreWidth - 1) / 2)) + "-" + string.Concat(Enumerable.Repeat(" ", oreWidth - 1 - (oreWidth - 1) / 2));
						if (onlyShowNeeded)
							output += String.Format("{0}{1} {2}/{3}\n", okStr, oreName, na, na);
						else
							output += String.Format("{0}{1} {2}{3}{4}/{5}{3}{6}\n", okStr, oreName, amountStr, separator, na, na, endNa);
						var savedIron = amountPresent * conversionData[Ores.Scrap].conversionRate * (1f / conversionData[Ores.Iron].conversionRate);
						scrapOutput = "\n*" + String.Format(scrapMetalMessage, FormatNumber(amountPresent, oreWidth, oreDecimals).Trim(), oreTranslation[Ores.Scrap], FormatNumber(savedIron, oreWidth, oreDecimals).Trim(), oreTranslation[Ores.Iron]) + "\n";
					}
				}
				else
				{
					if (onlyShowNeeded)
						output += String.Format("{0}{1} {2}/{3}\n", (missing > 0 ? warnStr : okStr), oreName, neededStr, totalNeededStr);
					else
						output += String.Format("{0}{1} {2}{3}{4}/{5}{3}{6}\n", (missing > 0 ? warnStr : okStr), oreName, amountStr, separator, neededStr, totalNeededStr, missingStr);
				}
			}

			output += scrapOutput;
			if (atLeastOneOrePrefersArc)
				output += (scrapOutput == "" ? "\n" : "") + basicRefineryEffUsed + (refineries.Count == 0 ? noRefineryFound : betterYield) + "\n";

			double avgPorts = 8 * Math.Log(effectivenessMultiplier) / log2;
			string avgPortsStr;
			if (!averageEffectivenesses)
			{
				avgPortsStr = Math.Round(avgPorts).ToString();
			}
			else
			{
				avgPortsStr = avgPorts.ToString("F1");
			}
			output += String.Format("\n" + refineryMessage + "\n",
				effectivenessMultiplier * 100,
				averageEffectivenesses ? "~" : "",
				avgPortsStr,
				averageEffectivenesses ? refineryMessageCauseAvg : refineryMessageCauseUser);
			if (lcd3 != null)
			{
				ShowAndSetFontSize(lcd3, output);
			}
			else if (fitOn2IfPossible && lcd2 != null)
			{
				ShowAndSetFontSize(lcd2, output_lcd2 + output);
			}
			Me.CustomData += output + "\n\n";
		}

		string blockDefinitionData = "SteelPlate*MetalGrid*ConstructionComponent*LargeTube*MotorComponent*Display*ComputerComponent*InteriorPlate*RadioCommunicationComponent*DetectorComponent*SmallTube*Superconductor*BulletproofGlass*GirderComponent*MedicalComponent*SolarCell*GravityGeneratorComponent*ExplosivesComponent*ZoneChip*PowerCell*ReactorComponent*EngineerPlushie*SabiroidPlushie*PrototechFrame*PrototechCapacitor*PrototechCircuitry*PrototechPanel*PrototechCoolingUnit*PrototechPropulsionUnit*PrototechMachinery*ThrustComponent$CubeBlock*LargeBlockArmorBlock=0:25*LargeBlockArmorSlope=0:13*LargeBlockArmorCorner=0:4*LargeBlockArmorCornerInv=0:21*LargeRoundArmor_Slope=0:13*LargeRoundArmor_Corner=0:4*LargeRoundArmor_CornerInv=0:21*LargeHeavyBlockArmorBlock=0:150,1:50*LargeHeavyBlockArmorSlope=0:75,1:25*LargeHeavyBlockArmorCorner=0:25,1:10*LargeHeavyBlockArmorCornerInv=0:125,1:50*SmallBlockArmorBlock=0:1*SmallBlockArmorSlope=0:1*SmallBlockArmorCorner=0:1*SmallBlockArmorCornerInv=0:1*SmallHeavyBlockArmorBlock=0:5,1:2*SmallHeavyBlockArmorSlope=0:3,1:1*SmallHeavyBlockArmorCorner=0:2,1:1*SmallHeavyBlockArmorCornerInv=0:4,1:1*LargeHalfArmorBlock=0:12*LargeHeavyHalfArmorBlock=0:75,1:25*LargeHalfSlopeArmorBlock=0:4*LargeHeavyHalfSlopeArmorBlock=0:19,1:6*HalfArmorBlock=0:1*HeavyHalfArmorBlock=0:3,1:1*HalfSlopeArmorBlock=0:1*HeavyHalfSlopeArmorBlock=0:2,1:1*LargeBlockArmorRoundSlope=0:13*LargeBlockArmorRoundCorner=0:4*LargeBlockArmorRoundCornerInv=0:21*LargeHeavyBlockArmorRoundSlope=0:130,1:50*LargeHeavyBlockArmorRoundCorner=0:125,1:40*LargeHeavyBlockArmorRoundCornerInv=0:140,1:50*SmallBlockArmorRoundSlope=0:1*SmallBlockArmorRoundCorner=0:1*SmallBlockArmorRoundCornerInv=0:1*SmallHeavyBlockArmorRoundSlope=0:4,1:1*SmallHeavyBlockArmorRoundCorner=0:4,1:1*SmallHeavyBlockArmorRoundCornerInv=0:5,1:1*LargeBlockArmorSlope2Base=0:19*LargeBlockArmorSlope2Tip=0:6*LargeBlockArmorCorner2Base=0:10*LargeBlockArmorCorner2Tip=0:3*LargeBlockArmorInvCorner2Base=0:22*LargeBlockArmorInvCorner2Tip=0:16*LargeHeavyBlockArmorSlope2Base=0:112,1:40*LargeHeavyBlockArmorSlope2Tip=0:40,1:12*LargeHeavyBlockArmorCorner2Base=0:55,1:15*LargeHeavyBlockArmorCorner2Tip=0:19,1:6*LargeHeavyBlockArmorInvCorner2Base=0:133,1:45*LargeHeavyBlockArmorInvCorner2Tip=0:94,1:25*SmallBlockArmorSlope2Base=0:1*SmallBlockArmorSlope2Tip=0:1*SmallBlockArmorCorner2Base=0:1*SmallBlockArmorCorner2Tip=0:1*SmallBlockArmorInvCorner2Base=0:1*SmallBlockArmorInvCorner2Tip=0:1*SmallHeavyBlockArmorSlope2Base=0:4,1:2*SmallHeavyBlockArmorSlope2Tip=0:2,1:1*SmallHeavyBlockArmorCorner2Base=0:3,1:1*SmallHeavyBlockArmorCorner2Tip=0:2,1:1*SmallHeavyBlockArmorInvCorner2Base=0:5,1:1*SmallHeavyBlockArmorInvCorner2Tip=0:5,1:1*LargeArmorPanelLight=0:5*LargeArmorCenterPanelLight=0:5*LargeArmorSlopedSidePanelLight=0:3*LargeArmorSlopedPanelLight=0:6*LargeArmorHalfPanelLight=0:3*LargeArmorHalfCenterPanelLight=0:3*LargeArmorQuarterPanelLight=0:2*LargeArmor2x1SlopedPanelLight=0:5*LargeArmor2x1SlopedPanelTipLight=0:5*LargeArmor2x1SlopedSideBasePanelLight=0:5*LargeArmor2x1SlopedSideTipPanelLight=0:3*LargeArmor2x1SlopedSideBasePanelLightInv=0:5*LargeArmor2x1SlopedSideTipPanelLightInv=0:3*LargeArmorHalfSlopedPanelLight=0:4*LargeArmor2x1HalfSlopedPanelLightRight=0:3*LargeArmor2x1HalfSlopedTipPanelLightRight=0:3*LargeArmor2x1HalfSlopedPanelLightLeft=0:3*LargeArmor2x1HalfSlopedTipPanelLightLeft=0:3*LargeRoundArmorPanelLight=0:8*LargeRoundArmorPanelCornerLight=0:5*LargeRoundArmorPanelFaceLight=0:4*LargeRoundArmorPanelInvertedCornerLight=0:5*LargeArmorPanelHeavy=0:15,1:5*LargeArmorCenterPanelHeavy=0:15,1:5*LargeArmorSlopedSidePanelHeavy=0:8,1:3*LargeArmorSlopedPanelHeavy=0:21,1:7*LargeArmorHalfPanelHeavy=0:8,1:3*LargeArmorHalfCenterPanelHeavy=0:8,1:3*LargeArmorQuarterPanelHeavy=0:5,1:2*LargeArmor2x1SlopedPanelHeavy=0:18,1:6*LargeArmor2x1SlopedPanelTipHeavy=0:18,1:6*LargeArmor2x1SlopedSideBasePanelHeavy=0:12,1:4*LargeArmor2x1SlopedSideTipPanelHeavy=0:6,1:2*LargeArmor2x1SlopedSideBasePanelHeavyInv=0:12,1:4*LargeArmor2x1SlopedSideTipPanelHeavyInv=0:6,1:2*LargeArmorHalfSlopedPanelHeavy=0:9,1:3*LargeArmor2x1HalfSlopedPanelHeavyRight=0:9,1:3*LargeArmor2x1HalfSlopedTipPanelHeavyRight=0:9,1:3*LargeArmor2x1HalfSlopedPanelHeavyLeft=0:9,1:3*LargeArmor2x1HalfSlopedTipPanelHeavyLeft=0:9,1:3*LargeRoundArmorPanelHeavy=0:24,1:8*LargeRoundArmorPanelCornerHeavy=0:15,1:5*LargeRoundArmorPanelFaceHeavy=0:12,1:4*LargeRoundArmorPanelInvertedCornerHeavy=0:15,1:5*SmallArmorPanelLight=0:1*SmallArmorCenterPanelLight=0:1*SmallArmorSlopedSidePanelLight=0:1*SmallArmorSlopedPanelLight=0:1*SmallArmorHalfPanelLight=0:1*SmallArmorHalfCenterPanelLight=0:1*SmallArmorQuarterPanelLight=0:1*SmallArmor2x1SlopedPanelLight=0:1*SmallArmor2x1SlopedPanelTipLight=0:1*SmallArmor2x1SlopedSideBasePanelLight=0:1*SmallArmor2x1SlopedSideTipPanelLight=0:1*SmallArmor2x1SlopedSideBasePanelLightInv=0:1*SmallArmor2x1SlopedSideTipPanelLightInv=0:1*SmallArmorHalfSlopedPanelLight=0:1*SmallArmor2x1HalfSlopedPanelLightRight=0:1*SmallArmor2x1HalfSlopedTipPanelLightRight=0:1*SmallArmor2x1HalfSlopedPanelLightLeft=0:1*SmallArmor2x1HalfSlopedTipPanelLightLeft=0:1*SmallRoundArmorPanelLight=0:1*SmallRoundArmorPanelCornerLight=0:1*SmallRoundArmorPanelFaceLight=0:1*SmallRoundArmorPanelInvertedCornerLight=0:1*SmallArmorPanelHeavy=0:3,1:1*SmallArmorCenterPanelHeavy=0:3,1:1*SmallArmorSlopedSidePanelHeavy=0:2,1:1*SmallArmorSlopedPanelHeavy=0:3,1:1*SmallArmorHalfPanelHeavy=0:2,1:1*SmallArmorHalfCenterPanelHeavy=0:2,1:1*SmallArmorQuarterPanelHeavy=0:2,1:1*SmallArmor2x1SlopedPanelHeavy=0:3,1:1*SmallArmor2x1SlopedPanelTipHeavy=0:3,1:1*SmallArmor2x1SlopedSideBasePanelHeavy=0:3,1:1*SmallArmor2x1SlopedSideTipPanelHeavy=0:2,1:1*SmallArmor2x1SlopedSideBasePanelHeavyInv=0:3,1:1*SmallArmor2x1SlopedSideTipPanelHeavyInv=0:2,1:1*SmallArmorHalfSlopedPanelHeavy=0:2,1:1*SmallArmor2x1HalfSlopedPanelHeavyRight=0:2,1:1*SmallArmor2x1HalfSlopedTipPanelHeavyRight=0:2,1:1*SmallArmor2x1HalfSlopedPanelHeavyLeft=0:2,1:1*SmallArmor2x1HalfSlopedTipPanelHeavyLeft=0:2,1:1*SmallRoundArmorPanelHeavy=0:3,1:1*SmallRoundArmorPanelCornerHeavy=0:2,1:1*SmallRoundArmorPanelFaceHeavy=0:3,1:1*SmallRoundArmorPanelInvertedCornerHeavy=0:3,1:1*LargeBlockArmorCornerSquare=0:7*SmallBlockArmorCornerSquare=0:1*LargeBlockHeavyArmorCornerSquare=0:40,1:15*SmallBlockHeavyArmorCornerSquare=0:2,1:1*LargeBlockArmorCornerSquareInverted=0:19*SmallBlockArmorCornerSquareInverted=0:1*LargeBlockHeavyArmorCornerSquareInverted=0:112,1:40*SmallBlockHeavyArmorCornerSquareInverted=0:4,1:1*LargeBlockArmorHalfCorner=0:6*SmallBlockArmorHalfCorner=0:1*LargeBlockHeavyArmorHalfCorner=0:40,1:12*SmallBlockHeavyArmorHalfCorner=0:2,1:1*LargeBlockArmorHalfSlopeCorner=0:2*SmallBlockArmorHalfSlopeCorner=0:1*LargeBlockHeavyArmorHalfSlopeCorner=0:12,1:4*SmallBlockHeavyArmorHalfSlopeCorner=0:2,1:1*LargeBlockArmorHalfSlopeCornerInverted=0:23*SmallBlockArmorHalfSlopeCornerInverted=0:1*LargeBlockHeavyArmorHalfSlopeCornerInverted=0:139,1:45*SmallBlockHeavyArmorHalfSlopeCornerInverted=0:5,1:1*LargeBlockArmorHalfSlopedCorner=0:11*SmallBlockArmorHalfSlopedCorner=0:1*LargeBlockHeavyArmorHalfSlopedCorner=0:45,1:5*SmallBlockHeavyArmorHalfSlopedCorner=0:3,1:1*LargeBlockArmorHalfSlopedCornerBase=0:11*SmallBlockArmorHalfSlopedCornerBase=0:1*LargeBlockHeavyArmorHalfSlopedCornerBase=0:45,1:15*SmallBlockHeavyArmorHalfSlopedCornerBase=0:3,1:1*LargeBlockArmorHalfSlopeInverted=0:22*SmallBlockArmorHalfSlopeInverted=0:1*LargeBlockHeavyArmorHalfSlopeInverted=0:133,1:45*SmallBlockHeavyArmorHalfSlopeInverted=0:5,1:1*LargeBlockArmorSlopedCorner=0:13*SmallBlockArmorSlopedCorner=0:1*LargeBlockHeavyArmorSlopedCorner=0:75,1:25*SmallBlockHeavyArmorSlopedCorner=0:3,1:1*LargeBlockArmorSlopedCornerBase=0:20*SmallBlockArmorSlopedCornerBase=0:1*LargeBlockHeavyArmorSlopedCornerBase=0:127,1:40*SmallBlockHeavyArmorSlopedCornerBase=0:5,1:1*LargeBlockArmorSlopedCornerTip=0:5*SmallBlockArmorSlopedCornerTip=0:1*LargeBlockHeavyArmorSlopedCornerTip=0:23,1:6*SmallBlockHeavyArmorSlopedCornerTip=0:2,1:1*LargeBlockArmorRaisedSlopedCorner=0:17*SmallBlockArmorRaisedSlopedCorner=0:1*LargeBlockHeavyArmorRaisedSlopedCorner=0:100,1:30*SmallBlockHeavyArmorRaisedSlopedCorner=0:4,1:2*LargeBlockArmorSlopeTransition=0:10*SmallBlockArmorSlopeTransition=0:1*LargeBlockHeavyArmorSlopeTransition=0:60,1:18*SmallBlockHeavyArmorSlopeTransition=0:3,1:1*LargeBlockArmorSlopeTransitionBase=0:16*SmallBlockArmorSlopeTransitionBase=0:1*LargeBlockHeavyArmorSlopeTransitionBase=0:95,1:35*SmallBlockHeavyArmorSlopeTransitionBase=0:4,1:2*LargeBlockArmorSlopeTransitionBaseMirrored=0:16*SmallBlockArmorSlopeTransitionBaseMirrored=0:1*LargeBlockHeavyArmorSlopeTransitionBaseMirrored=0:95,1:35*SmallBlockHeavyArmorSlopeTransitionBaseMirrored=0:4,1:2*LargeBlockArmorSlopeTransitionMirrored=0:10*SmallBlockArmorSlopeTransitionMirrored=0:1*LargeBlockHeavyArmorSlopeTransitionMirrored=0:60,1:18*SmallBlockHeavyArmorSlopeTransitionMirrored=0:3,1:1*LargeBlockArmorSlopeTransitionTip=0:5*SmallBlockArmorSlopeTransitionTip=0:1*LargeBlockHeavyArmorSlopeTransitionTip=0:30,1:9*SmallBlockHeavyArmorSlopeTransitionTip=0:2,1:1*LargeBlockArmorSlopeTransitionTipMirrored=0:5*SmallBlockArmorSlopeTransitionTipMirrored=0:1*LargeBlockHeavyArmorSlopeTransitionTipMirrored=0:30,1:9*SmallBlockHeavyArmorSlopeTransitionTipMirrored=0:2,1:1*LargeBlockArmorSquareSlopedCornerBase=0:18*SmallBlockArmorSquareSlopedCornerBase=0:1*LargeBlockHeavyArmorSquareSlopedCornerBase=0:105,1:35*SmallBlockHeavyArmorSquareSlopedCornerBase=0:4,1:2*LargeBlockArmorSquareSlopedCornerTip=0:6*SmallBlockArmorSquareSlopedCornerTip=0:1*LargeBlockHeavyArmorSquareSlopedCornerTip=0:30,1:9*SmallBlockHeavyArmorSquareSlopedCornerTip=0:2,1:1*LargeBlockArmorSquareSlopedCornerTipInv=0:9*SmallBlockArmorSquareSlopedCornerTipInv=0:1*LargeBlockHeavyArmorSquareSlopedCornerTipInv=0:55,1:18*SmallBlockHeavyArmorSquareSlopedCornerTipInv=0:3,1:1*LargeBlockModularBridgeCorner=13:5,0:20,12:20*LargeBlockModularBridgeCornerFloorless=13:5,0:20,12:20*LargeBlockModularBridgeRaisedSlopedCorner=13:5,0:24,12:20*LargeBlockModularBridgeRaisedSlopedCornerFloorless=13:5,0:24,12:20*LargeBlockModularBridgeHalfSlopedCorner=13:5,0:16,12:20*LargeBlockModularBridgeHalfSlopedCornerFloorless=13:5,0:16,12:20*LargeBlockModularBridgeCorner2x1BaseL=13:4,0:10,12:10*LargeBlockModularBridgeCorner2x1BaseLFloorless=13:4,0:10,12:10*LargeBlockModularBridgeCorner2x1BaseR=13:4,0:10,12:10*LargeBlockModularBridgeCorner2x1BaseRFloorless=13:4,0:10,12:10*LargeBlockModularBridgeEmpty=13:6,0:10,12:30*LargeBlockModularBridgeFloor=13:6,0:10,12:30*LargeBlockModularBridgeSideL=13:4,0:8,12:10*LargeBlockModularBridgeSideR=13:4,0:8,12:10*LargeBlockModularBridgeSlopedCornerBase=13:5,0:20,12:20*LargeBlockModularBridgeSlopedCornerBaseFloorless=13:5,0:20,12:20*SmallBlockKitchenSink=7:4,2:6,10:2*SmallBlockKitchenCoffeeMachine=7:4,2:6,4:1*LargeBlockDeskChairless=7:30,2:30*LargeBlockDeskChairlessCorner=7:20,2:20*LargeBlockDeskChairlessCornerInv=7:60,2:60*Shower=7:20,2:20,10:12,12:8*WindowWall=0:8,2:10,12:10*WindowWallLeft=0:10,2:10,12:8*WindowWallRight=0:10,2:10,12:8*Catwalk=2:16,13:4,10:20*CatwalkCorner=2:24,13:4,10:32*CatwalkStraight=2:24,13:4,10:32*CatwalkWall=2:20,13:4,10:26*CatwalkRailingEnd=2:28,13:4,10:38*CatwalkRailingHalfRight=2:28,13:4,10:36*CatwalkRailingHalfLeft=2:28,13:4,10:36*CatwalkHalf=2:10,13:2,10:10*CatwalkHalfRailing=2:18,13:2,10:22*CatwalkHalfCenterRailing=2:14,13:2,10:16*CatwalkHalfOuterRailing=2:14,13:2,10:16*GratedStairs=2:22,10:12,7:16*GratedHalfStairs=2:20,10:6,7:8*GratedHalfStairsMirrored=2:20,10:6,7:8*RailingStraight=2:8,10:6*RailingDouble=2:16,10:12*RailingCorner=2:16,10:12*RailingDiagonal=2:12,10:9*RailingHalfRight=2:8,10:4*RailingHalfLeft=2:8,10:4*RailingCenter=2:8,10:6*Railing2x1Right=2:10,10:7*Railing2x1Left=2:10,10:7*Freight1=7:6,2:8*Freight2=7:12,2:16*Freight3=7:18,2:24*Truss=13:20,10:10*TrussSmall=13:2,10:1*TrussFrame=13:10,10:5*TrussSlopedFrame=13:6,10:3*TrussSloped=13:10,10:5*TrussSlopedSmall=13:1,10:1*TrussAngled=13:20,10:10*TrussAngledSmall=13:2,10:1*TrussHalf=13:10,10:5*TrussHalfSmall=13:1,10:1*TrussFloor=13:24,2:16,10:30*TrussFloorT=13:22,2:16,10:30*TrussFloorX=13:20,2:16,10:30*TrussFloorAngled=13:24,2:16,10:30*TrussFloorAngledInverted=13:24,2:16,10:30*TrussFloorHalf=13:12,2:14,10:20*LargeBarrel=0:5,3:1,2:6*SmallBarrel=0:5,3:1,2:6*LargeBarrelThree=0:15,3:3,2:18*LargeBarrelStack=0:50,3:10,1:4,13:8,2:60*ArmorCenter=0:140*ArmorCorner=0:120*ArmorInvCorner=0:135*ArmorSide=0:130*SmallArmorCenter=0:5*SmallArmorCorner=0:5*SmallArmorInvCorner=0:5*SmallArmorSide=0:5*LargeRailStraight=0:12,2:8,3:4*Monolith=0:130,11:130*Stereolith=0:130,11:130*DeadAstronaut=0:13,11:13*LargeDeadAstronaut=0:13,11:13*EngineerPlushie=21:1*SabiroidPlushie=22:1*LargeWarningSignEaster2=2:4,7:6*SmallWarningSignEaster2=2:4,7:6*LargeWarningSignEaster3=2:4,7:6*SmallWarningSignEaster3=2:4,7:6*LargeWarningSignEaster9=2:4,7:4*SmallWarningSignEaster9=2:2,7:1*LargeWarningSignEaster10=2:4,7:4*SmallWarningSignEaster10=2:2,7:1*LargeWarningSignEaster11=2:4,7:4*SmallWarningSignEaster11=2:2,7:1*LargeWarningSignEaster13=2:4,7:4*SmallWarningSignEaster13=2:2,7:1*LargeBlockStatueEngineer=13:30,0:60,2:30*CorridorRound=7:100,2:30*CorridorRoundCorner=7:100,2:30*CorridorRoundT=7:70,2:25*CorridorRoundX=7:40,2:20*CorridorRoundTransition=7:100,2:30*LargeBlockFloorCenter=7:10,0:20,2:10*LargeBlockFloorCenterMirrored=7:10,0:20,2:10*LargeBlockFloorEdge=7:10,0:20,2:10*LargeBlockFloorEdgeMirrored=7:10,0:20,2:10*LargeBlockFloorPassage=7:10,0:20,2:10*LargeBlockFloorPassageMirrored=7:10,0:20,2:10*LargeBlockFloorDecal=7:10,0:20,2:10*LargeBlockFloorDecalMirrored=7:10,0:20,2:10*LargeBlockFloorSlab=7:80,0:160,2:80*SmallBlockFloorCenter=7:1,0:2,2:1*SmallBlockFloorCenterMirrored=7:1,0:2,2:1*SmallBlockFloorSlab=7:8,0:16,2:8*LargeBlockLabDesk=7:30,2:30,4:2,9:4*LargeBlockLabSink=7:30,2:30,4:2*LargeBlockPipesStraight1=0:5,2:20,3:12*LargeBlockPipesStraight2=0:10,2:20,3:12*LargeBlockPipesEnd=0:5,2:20,3:12*LargeBlockPipesJunction=0:10,2:30,3:14*LargeBlockPipesCornerOuter=0:1,2:10,3:6*LargeBlockPipesCorner=0:5,2:20,3:12*LargeBlockPipesCornerInner=0:10,2:30,3:18*DeadBody01=12:1,8:1,5:1*DeadBody02=12:1,8:1,5:1*DeadBody03=12:1,8:1,5:1*DeadBody04=12:1,8:1,5:1*DeadBody05=12:1,8:1,5:1*DeadBody06=12:1,8:1,5:1*AngledInteriorWallA=7:25,2:10*AngledInteriorWallB=7:25,2:10*PipeWorkBlockA=7:20,2:20,3:10*PipeWorkBlockB=7:20,2:20,3:10*LargeWarningSign1=2:4,7:6*LargeWarningSign2=2:4,7:6*LargeWarningSign3=2:4,7:6*LargeWarningSign4=2:4,7:2*LargeWarningSign5=2:4,7:6*LargeWarningSign6=2:4,7:6*LargeWarningSign7=2:4,7:2*LargeWarningSign8=2:4,7:4*LargeWarningSign9=2:4,7:4*LargeWarningSign10=2:4,7:4*LargeWarningSign11=2:4,7:4*LargeWarningSign12=2:4,7:4*LargeWarningSign13=2:4,7:4*SmallWarningSign1=2:4,7:6*SmallWarningSign2=2:4,7:2*SmallWarningSign3=2:4,7:6*SmallWarningSign4=2:4,7:2*SmallWarningSign5=2:4,7:6*SmallWarningSign6=2:4,7:2*SmallWarningSign7=2:4,7:2*SmallWarningSign8=2:2,7:1*SmallWarningSign9=2:2,7:1*SmallWarningSign10=2:2,7:1*SmallWarningSign11=2:2,7:1*SmallWarningSign12=2:2,7:1*SmallWarningSign13=2:2,7:1*LargeBlockConveyorPipeCap=7:10,2:10*LargeBlockCylindricalColumn=7:25,2:10*SmallBlockCylindricalColumn=7:5,2:3*LargeGridBeamBlock=0:25*LargeGridBeamBlockSlope=0:13*LargeGridBeamBlockRound=0:13*LargeGridBeamBlockSlope2x1Base=0:19*LargeGridBeamBlockSlope2x1Tip=0:7*LargeGridBeamBlockHalf=0:12*LargeGridBeamBlockHalfSlope=0:7*LargeGridBeamBlockEnd=0:25*LargeGridBeamBlockJunction=0:25*LargeGridBeamBlockTJunction=0:25*SmallGridBeamBlock=0:1*SmallGridBeamBlockSlope=0:1*SmallGridBeamBlockRound=0:1*SmallGridBeamBlockSlope2x1Base=0:1*SmallGridBeamBlockSlope2x1Tip=0:1*SmallGridBeamBlockHalf=0:1*SmallGridBeamBlockHalfSlope=0:1*SmallGridBeamBlockEnd=0:1*SmallGridBeamBlockJunction=0:1*SmallGridBeamBlockTJunction=0:1*Passage2=7:74,2:20,10:48*Passage2Wall=7:50,2:14,10:32*LargeStairs=7:50,2:30*LargeRamp=7:70,2:16*LargeSteelCatwalk=7:27,2:5,10:20*LargeSteelCatwalk2Sides=7:32,2:7,10:25*LargeSteelCatwalkCorner=7:32,2:7,10:25*LargeSteelCatwalkPlate=7:23,2:7,10:17*LargeCoverWall=0:4,2:10*LargeCoverWallHalf=0:2,2:6*LargeCoverWallHalfMirrored=0:2,2:6*LargeBlockInteriorWall=7:25,2:10*LargeInteriorPillar=7:25,2:10,10:4*AirDuct1=7:20,2:30,3:4,0:10*AirDuct2=7:20,2:30,3:4,0:10*AirDuctCorner=7:20,2:30,3:4,0:10*AirDuctT=7:15,2:30,3:4,0:8*AirDuctX=7:10,2:30,3:4,0:5*AirDuctRamp=7:20,2:30,3:4,0:10*AirDuctGrate=2:10,7:10*LargeBlockConveyorCap=7:10,2:10*SmallBlockConveyorCapMedium=7:10,2:10*SmallBlockConveyorCap=7:2,2:2*Viewport1=0:10,2:10,12:8*Viewport2=0:10,2:10,12:8*BarredWindow=13:1,2:4*BarredWindowSlope=13:1,2:4*BarredWindowSide=13:1,2:4*BarredWindowFace=13:1,2:4*StorageShelf1=13:10,0:50,2:50,10:50,7:50*StorageShelf2=13:30,19:20,4:20,1:20*StorageShelf3=13:10,20:10,11:10,30:10,16:2*LargeBlockInsetWall=7:25,2:10*LargeBlockInsetWallPillar=7:25,2:10*LargeBlockInsetWallCorner=7:25,2:10*LargeBlockInsetWallCornerInverted=7:25,2:10*LargeBlockInsetWallSlope=7:19,2:7*LargeBlockConsoleModule=7:50,2:50*LargeBlockConsoleModuleCorner=7:30,2:30*SmallBlockConsoleModule=7:6,2:6*SmallBlockConsoleModuleCorner=7:3,2:3*SmallBlockConsoleModuleInvertedCorner=7:3,2:3*ExtendedWindow=13:10,12:25*ExtendedWindowRailing=13:10,12:25*ExtendedWindowCorner=13:4,12:10*ExtendedWindowCornerInverted=13:15,12:50*ExtendedWindowCornerInvertedRailing=13:15,12:50*ExtendedWindowDiagonal=13:12,12:35*ExtendedWindowDiagonalRailing=13:12,12:35*ExtendedWindowEnd=13:10,12:25*ExtendedWindowDome=13:10,12:25*SmallBlockExtendedWindow=13:1,12:3*SmallBlockExtendedWindowCorner=13:1,12:2*SmallBlockExtendedWindowCornerInverted=13:2,12:6*SmallBlockExtendedWindowDiagonal=13:1,12:4*SmallBlockExtendedWindowEnd=13:1,12:3*SmallBlockExtendedWindowDome=13:2,12:6*Corridor=7:100,2:30*CorridorCorner=7:100,2:30*CorridorT=7:70,2:25*CorridorX=7:40,2:20*CorridorWindow=7:80,2:30,12:6*CorridorDoubleWindow=7:65,2:25,12:12*CorridorWindowRoof=7:80,2:30,12:6*CorridorNarrow=7:100,2:30*TrussPillar=7:25,2:10,10:4*TrussPillarCorner=7:25,2:10,10:4*TrussPillarSlanted=7:25,2:10,10:4*TrussPillarT=7:30,2:12,10:6*TrussPillarX=7:35,2:15,10:8*TrussPillarDiagonal=7:30,2:12,10:6*TrussPillarSmall=7:25,2:10,10:4*TrussPillarOffset=7:25,2:10,10:4*LargeBlockSciFiWall=7:25,2:10*LargeBlockBarCounter=7:16,2:10,4:1,12:6*LargeBlockBarCounterCorner=7:24,2:14,4:2,12:10*LargeSymbolA=0:4*LargeSymbolB=0:4*LargeSymbolC=0:4*LargeSymbolD=0:4*LargeSymbolE=0:4*LargeSymbolF=0:4*LargeSymbolG=0:4*LargeSymbolH=0:4*LargeSymbolI=0:4*LargeSymbolJ=0:4*LargeSymbolK=0:4*LargeSymbolL=0:4*LargeSymbolM=0:4*LargeSymbolN=0:4*LargeSymbolO=0:4*LargeSymbolP=0:4*LargeSymbolQ=0:4*LargeSymbolR=0:4*LargeSymbolS=0:4*LargeSymbolT=0:4*LargeSymbolU=0:4*LargeSymbolV=0:4*LargeSymbolW=0:4*LargeSymbolX=0:4*LargeSymbolY=0:4*LargeSymbolZ=0:4*SmallSymbolA=0:1*SmallSymbolB=0:1*SmallSymbolC=0:1*SmallSymbolD=0:1*SmallSymbolE=0:1*SmallSymbolF=0:1*SmallSymbolG=0:1*SmallSymbolH=0:1*SmallSymbolI=0:1*SmallSymbolJ=0:1*SmallSymbolK=0:1*SmallSymbolL=0:1*SmallSymbolM=0:1*SmallSymbolN=0:1*SmallSymbolO=0:1*SmallSymbolP=0:1*SmallSymbolQ=0:1*SmallSymbolR=0:1*SmallSymbolS=0:1*SmallSymbolT=0:1*SmallSymbolU=0:1*SmallSymbolV=0:1*SmallSymbolW=0:1*SmallSymbolX=0:1*SmallSymbolY=0:1*SmallSymbolZ=0:1*LargeSymbol0=0:4*LargeSymbol1=0:4*LargeSymbol2=0:4*LargeSymbol3=0:4*LargeSymbol4=0:4*LargeSymbol5=0:4*LargeSymbol6=0:4*LargeSymbol7=0:4*LargeSymbol8=0:4*LargeSymbol9=0:4*SmallSymbol0=0:1*SmallSymbol1=0:1*SmallSymbol2=0:1*SmallSymbol3=0:1*SmallSymbol4=0:1*SmallSymbol5=0:1*SmallSymbol6=0:1*SmallSymbol7=0:1*SmallSymbol8=0:1*SmallSymbol9=0:1*LargeSymbolHyphen=0:4*LargeSymbolUnderscore=0:4*LargeSymbolDot=0:4*LargeSymbolApostrophe=0:4*LargeSymbolAnd=0:4*LargeSymbolColon=0:4*LargeSymbolExclamationMark=0:4*LargeSymbolQuestionMark=0:4*SmallSymbolHyphen=0:1*SmallSymbolUnderscore=0:1*SmallSymbolDot=0:1*SmallSymbolApostrophe=0:1*SmallSymbolAnd=0:1*SmallSymbolColon=0:1*SmallSymbolExclamationMark=0:1*SmallSymbolQuestionMark=0:1*FireCover=0:4,2:10*FireCoverCorner=0:8,2:20*HalfWindow=13:4,0:10,12:10*HalfWindowInv=13:4,0:10,12:10*HalfWindowCorner=13:8,0:20,12:20*HalfWindowCornerInv=13:8,0:20,12:20*HalfWindowDiagonal=13:6,0:14,12:14*HalfWindowRound=13:7,0:16,12:18*Embrasure=0:30,2:20,1:10*PassageSciFi=7:74,2:20,10:48*PassageSciFiWall=7:50,2:14,10:32*PassageSciFiIntersection=7:35,2:10,10:25*PassageSciFiGate=7:35,2:10,10:25*PassageScifiCorner=7:74,2:20,10:48*PassageSciFiTjunction=7:55,2:16,10:38*PassageSciFiWindow=7:60,2:16,12:16,10:38*BridgeWindow1x1Slope=13:8,0:5,7:10,12:25*BridgeWindow1x1Face=13:8,0:2,7:4,12:18*BridgeWindow1x1FaceInverted=13:5,0:6,7:12,12:12*LargeWindowSquare=7:12,2:8,10:4*LargeWindowEdge=7:16,2:12,10:6*Window1x2Slope=13:16,12:55*Window1x2Inv=13:15,12:40*Window1x2Face=13:15,12:40*Window1x2SideLeft=13:13,12:26*Window1x2SideLeftInv=13:13,12:26*Window1x2SideRight=13:13,12:26*Window1x2SideRightInv=13:13,12:26*Window1x1Slope=13:12,12:35*Window1x1Face=13:11,12:24*Window1x1Side=13:9,12:17*Window1x1SideInv=13:9,12:17*Window1x1Inv=13:11,12:24*Window1x2Flat=13:15,12:50*Window1x2FlatInv=13:15,12:50*Window1x1Flat=13:10,12:25*Window1x1FlatInv=13:10,12:25*Window3x3Flat=13:40,12:196*Window3x3FlatInv=13:40,12:196*Window2x3Flat=13:25,12:140*Window2x3FlatInv=13:25,12:140*SmallWindow1x2Slope=13:1,12:3*SmallWindow1x2Inv=13:1,12:3*SmallWindow1x2Face=13:1,12:3*SmallWindow1x2SideLeft=13:1,12:3*SmallWindow1x2SideLeftInv=13:1,12:3*SmallWindow1x2SideRight=13:1,12:3*SmallWindow1x2SideRightInv=13:1,12:3*SmallWindow1x1Slope=13:1,12:2*SmallWindow1x1Face=13:1,12:2*SmallWindow1x1Side=13:1,12:2*SmallWindow1x1SideInv=13:1,12:2*SmallWindow1x1Inv=13:1,12:2*SmallWindow1x2Flat=13:1,12:3*SmallWindow1x2FlatInv=13:1,12:3*SmallWindow1x1Flat=13:1,12:2*SmallWindow1x1FlatInv=13:1,12:2*SmallWindow3x3Flat=13:3,12:12*SmallWindow3x3FlatInv=13:3,12:12*SmallWindow2x3Flat=13:2,12:8*SmallWindow2x3FlatInv=13:2,12:8*WindowRound=13:15,12:45*WindowRoundInv=13:15,12:45*WindowRoundCorner=13:13,12:33*WindowRoundCornerInv=13:13,12:33*WindowRoundFace=13:9,12:21*WindowRoundFaceInv=13:9,12:21*WindowRoundInwardsCorner=13:13,12:20*WindowRoundInwardsCornerInv=13:13,12:20*SmallWindowRound=13:1,12:2*SmallWindowRoundInv=13:1,12:2*SmallWindowRoundCorner=13:1,12:2*SmallWindowRoundCornerInv=13:1,12:2*SmallWindowRoundFace=13:1,12:2*SmallWindowRoundFaceInv=13:1,12:2*SmallWindowRoundInwardsCorner=13:1,12:2*SmallWindowRoundInwardsCornerInv=13:1,12:2$MyProgrammableBlock*SmallProgrammableBlock=0:2,2:2,3:2,4:1,5:1,6:2*LargeProgrammableBlock=0:21,2:4,3:2,4:1,5:1,6:2*LargeProgrammableBlockReskin=0:21,2:4,3:2,4:1,5:1,6:2*SmallProgrammableBlockReskin=0:2,2:2,3:2,4:1,5:1,6:2$Projector*LargeProjector=0:21,2:4,3:2,4:1,6:2*SmallProjector=0:2,2:2,3:2,4:1,6:2*LargeBlockConsole=7:20,2:30,6:8,5:10$SensorBlock*SmallBlockSensor=7:5,2:5,6:6,8:4,9:6,0:2*LargeBlockSensor=7:5,2:5,6:6,8:4,9:6,0:2*SmallBlockSensorReskin=7:5,2:5,6:6,8:4,9:6,0:2*LargeBlockSensorReskin=7:5,2:5,6:6,8:4,9:6,0:2$TargetDummyBlock*TargetDummy=0:15,10:10,4:2,6:4,5:1$SoundBlock*SmallBlockSoundBlock=7:4,2:6,6:3*LargeBlockSoundBlock=7:4,2:6,6:3$ButtonPanel*ButtonPanelLarge=7:10,2:20,6:20*ButtonPanelSmall=7:2,2:2,6:1*LargeButtonPanelPedestal=7:5,2:10,6:5*SmallButtonPanelPedestal=7:5,2:10,6:5*LargeBlockModularBridgeButtonPanel=13:6,0:10,2:5,6:5,12:30*LargeBlockInsetButtonPanel=7:20,2:20,6:20,5:10*LargeBlockAccessPanel3=2:8,6:3,7:3*VerticalButtonPanelLarge=7:5,2:10,6:5*VerticalButtonPanelSmall=7:5,2:10,6:5*LargeBlockConsoleModuleButtons=7:50,2:50,5:6,6:10*SmallBlockConsoleModuleButtons=7:6,2:6,6:2*LargeSciFiButtonTerminal=7:5,2:10,6:4,5:4*LargeSciFiButtonPanel=7:10,2:20,6:20,5:5$TimerBlock*TimerBlockLarge=7:6,2:30,6:5*TimerBlockSmall=7:2,2:3,6:1*TimerBlockReskinLarge=7:6,2:30,6:5*TimerBlockReskinSmall=7:2,2:3,6:1$TurretControlBlock*LargeTurretControlBlock=7:20,2:30,9:20,4:4,5:6,6:20,0:20*SmallTurretControlBlock=7:4,2:10,9:4,4:2,5:1,6:10,0:4$EventControllerBlock*EventControllerLarge=7:10,2:30,6:10,5:4*EventControllerSmall=7:2,2:3,6:2,5:1$PathRecorderBlock*LargePathRecorderBlock=7:20,2:30,9:20,4:4,6:20,0:20*SmallPathRecorderBlock=7:2,2:5,9:4,4:2,6:10,0:2$BasicMissionBlock*LargeBasicMission=7:20,2:30,9:20,4:4,6:20,0:20*SmallBasicMission=7:2,2:5,9:4,4:2,6:10,0:2$FlightMovementBlock*LargeFlightMovement=7:20,2:30,9:20,4:4,6:20,0:20*SmallFlightMovement=7:2,2:5,9:4,4:2,6:10,0:2$DefensiveCombatBlock*LargeDefensiveCombat=7:20,2:30,9:20,4:4,6:20,0:20*SmallDefensiveCombat=7:2,2:5,9:4,4:2,6:10,0:2$OffensiveCombatBlock*LargeOffensiveCombat=7:20,2:30,9:20,4:4,6:20,0:20*SmallOffensiveCombat=7:2,2:5,9:4,4:2,6:10,0:2$RadioAntenna*LargeBlockRadioAntenna=0:80,3:40,10:60,2:30,6:8,8:40*LargeBlockCompactRadioAntenna=0:40,3:20,10:30,2:20,6:8,8:40*SmallBlockRadioAntenna=0:2,10:1,2:1,6:1,8:4*LargeBlockCompactRadioAntennaReskin=0:40,7:80,10:30,2:20,6:8,8:40*SmallBlockCompactRadioAntennaReskin=0:2,7:3,10:1,2:1,6:1,8:4*LargeBlockRadioAntennaDish=2:40,13:120,0:80,6:8,8:40$Beacon*LargeBlockBeacon=0:80,2:30,3:20,6:10,8:40*SmallBlockBeacon=0:2,2:1,10:1,6:1,8:4*LargeBlockBeaconReskin=0:80,2:30,3:20,6:10,8:40*SmallBlockBeaconReskin=0:2,2:1,10:1,6:1,8:4$RemoteControl*LargeBlockRemoteControl=7:10,2:10,4:1,6:15*SmallBlockRemoteControl=7:2,2:1,4:1,6:1$LaserAntenna*LargeBlockLaserAntenna=0:50,2:40,4:16,9:30,8:20,11:100,6:50,12:4*SmallBlockLaserAntenna=0:10,10:10,2:10,4:5,8:5,11:10,6:30,12:2$BroadcastController*LargeBlockBroadcastController=7:10,2:30,8:5,6:10,5:4*SmallBlockBroadcastController=7:2,2:3,8:1,6:2,5:1$TransponderBlock*LargeBlockTransponder=0:30,2:20,6:10,8:5*SmallBlockTransponder=0:3,2:2,6:2,8:1$ExtendedPistonBase*LargePistonBaseReskin=0:15,2:10,3:4,4:4,6:2*SmallPistonBaseReskin=0:4,2:4,10:4,4:2,6:1*LargePistonBase=0:15,2:10,3:4,4:4,6:2*SmallPistonBase=0:4,2:4,10:4,4:2,6:1$PistonTop*LargePistonTopReskin=0:10,3:8*SmallPistonTopReskin=0:4,3:2*LargePistonTop=0:10,3:8*SmallPistonTop=0:4,3:2$Door*LargeBlockSmallGate=0:300,2:70,10:60,4:10,6:6*LargeBlockEvenWideDoor=0:300,2:70,10:60,4:10,6:6*(null)=7:10,2:40,10:4,4:2,5:1,6:2,0:8*SmallDoor=7:8,2:30,10:4,4:2,5:1,6:2,0:6*CorridorRoundDoor=0:45,2:50,10:10,4:4,5:2,6:2*LargeBlockLabDoor=13:10,12:20,2:20,4:2,5:2,6:2*LargeBlockLabDoorInv=13:10,12:20,2:20,4:2,5:2,6:2*LargeBlockGate=0:800,2:100,10:100,4:20,6:10*LargeBlockOffsetDoor=0:25,2:35,10:4,4:4,5:1,6:2,12:6*LargeBlockNarrowDoor=0:35,2:40,10:10,4:4,5:1,6:2*LargeBlockNarrowDoorHalf=0:25,2:40,10:10,4:4,5:1,6:2*SmallSideDoor=7:10,2:26,12:4,4:2,5:1,6:2,0:8*SlidingHatchDoor=0:40,2:50,10:10,4:4,5:2,6:2,12:10*SlidingHatchDoorHalf=0:30,2:50,10:10,4:4,5:2,6:2,12:10$Cockpit*LargeBlockModularBridgeCockpit=0:15,2:15,4:1,5:4,6:100,12:30,7:30*LargeBlockCaptainDesk=7:50,2:50,6:6,5:4*LargeBlockCockpit=7:20,2:20,4:2,6:100,5:10*LargeBlockCockpitSeat=0:30,2:20,4:1,5:8,6:100,12:60*SmallBlockCockpit=0:10,2:10,4:1,5:5,6:15,12:30*DBSmallBlockFighterCockpit=2:20,4:1,0:20,1:10,7:15,5:4,6:20,12:40*CockpitOpen=7:20,2:20,4:2,6:100,5:4*RoverCockpit=7:30,2:25,4:2,6:20,5:4*OpenCockpitSmall=7:20,2:20,4:1,6:15,5:2*OpenCockpitLarge=7:30,2:30,4:2,6:100,5:6*SmallBlockFlushCockpit=0:20,2:20,4:2,5:5,6:20,12:40*LargeBlockDesk=7:30,2:30*LargeBlockDeskCorner=7:20,2:20*LargeBlockDeskCornerInv=7:60,2:60*LargeBlockCouch=7:30,2:30*LargeBlockCouchCorner=7:35,2:35*LargeBlockBathroomOpen=7:30,2:30,10:8,4:4,3:2*LargeBlockBathroom=7:30,2:40,10:8,4:4,3:2*LargeBlockToilet=7:10,2:15,10:2,4:2,3:1*SmallBlockCockpitIndustrial=0:10,2:20,1:10,4:2,5:6,6:20,12:60,10:10*LargeBlockCockpitIndustrial=0:20,2:30,1:15,4:2,5:10,6:60,12:80,10:10*SmallBlockCapCockpit=0:20,2:10,4:1,5:4,6:15,12:10*LargeBlockInsetPlantCouch=7:30,2:30,10:10,12:20*LargeBlockLabDeskSeat=7:30,2:30,6:6,5:4*SpeederCockpit=7:30,2:25,4:2,6:20,5:4*SpeederCockpitCompact=7:30,2:25,4:2,6:20,5:4*PassengerSeatLarge=7:20,2:20*PassengerSeatSmall=7:20,2:20*PassengerSeatSmallNew=7:20,2:20*PassengerSeatSmallOffset=7:20,2:20*BuggyCockpit=7:30,2:25,4:2,6:20,5:4*LargeBlockConsoleModuleInvertedCorner=7:80,2:80*LargeBlockConsoleModuleScreens=7:50,2:50,5:12,6:10*PassengerBench=7:20,2:20*SmallBlockStandingCockpit=7:20,2:20,4:1,6:20,5:2*LargeBlockStandingCockpit=7:20,2:20,4:1,6:20,5:2$ReflectorLight*LargeBlockFloodlight=0:8,13:10,7:10,2:20,12:4*LargeBlockFloodlightAngled=0:8,13:10,7:10,2:20,12:4*LargeBlockFloodlightCornerL=0:8,13:10,7:10,2:20,12:4*LargeBlockFloodlightCornerR=0:8,13:10,7:10,2:20,12:4*SmallBlockFloodlight=0:1,13:4,7:1,2:1,12:2*SmallBlockFloodlightAngled=0:1,13:4,7:1,2:1,12:2*SmallBlockFloodlightCornerL=0:1,13:4,7:1,2:1,12:2*SmallBlockFloodlightCornerR=0:1,13:4,7:1,2:1,12:2*SmallBlockFloodlightDown=0:1,13:4,7:1,2:1,12:2*SmallBlockFloodlightAngledRotated=0:1,13:4,7:1,2:1,12:2*RotatingLightLarge=2:3,4:1*RotatingLightSmall=2:3,4:1*LargeBlockFrontLight=0:8,3:2,7:20,2:15,12:4*SmallBlockFrontLight=0:1,3:1,7:1,2:1,12:2*OffsetSpotlight=2:2,12:1$LargeMissileTurret*LargeMissileTurretReskin=0:40,2:50,1:15,3:6,4:16,6:10*SmallMissileTurretReskin=0:15,2:40,1:5,3:2,4:8,6:10*(null)=0:40,2:50,1:15,3:6,4:16,6:10*SmallMissileTurret=0:15,2:40,1:5,3:2,4:8,6:10*LargeCalibreTurret=0:450,2:400,1:50,3:40,4:30,6:20*LargeBlockMediumCalibreTurret=0:300,2:280,1:30,3:30,4:20,6:20*SmallBlockMediumCalibreTurret=0:50,2:100,1:10,3:6,4:10,6:20$LargeGatlingTurret*LargeGatlingTurretReskin=0:40,2:40,1:15,10:6,4:8,6:10*SmallGatlingTurretReskin=0:15,2:30,1:5,10:6,4:4,6:10*(null)=0:40,2:40,1:15,10:6,4:8,6:10*SmallGatlingTurret=0:15,2:30,1:5,10:6,4:4,6:10*AutoCannonTurret=0:20,2:40,1:6,10:4,4:4,6:10$Jukebox*SmallBlockJukeboxReskin=7:4,2:2,6:2,5:1*Jukebox=7:15,2:10,6:4,5:4*LargeBlockInsetEntertainmentCorner=7:30,2:20,6:10,5:8$TerminalBlock*SmallBlockFirstAidCabinet=7:3,2:3*SmallBlockKitchenOven=7:8,2:12,4:1,12:4*SmallBlockKitchenMicrowave=7:4,2:6,4:1,12:2*SmallBlockKitchenFridge=7:4,2:6,4:1,12:2*ControlPanel=0:1,2:1,6:1,5:1*SmallControlPanel=0:1,2:1,6:1,5:1*LargeControlPanelPedestal=7:5,2:10,6:1,5:1*SmallControlPanelPedestal=7:5,2:10,6:1,5:1*LargeCrate=0:20,10:8,4:4,2:24*LargeFreezer=7:20,2:20,10:10,12:10*LargeBlockAccessPanel1=2:15,6:5,7:5*LargeBlockAccessPanel2=2:15,10:10,7:5*LargeBlockAccessPanel4=2:10,7:10*SmallBlockAccessPanel1=2:8,10:2,7:2*SmallBlockAccessPanel2=2:2,10:1,7:1*SmallBlockAccessPanel3=2:4,10:1,7:1*SmallBlockAccessPanel4=2:10,7:10*LargeBlockSciFiTerminal=2:4,6:2,5:4,7:2$CryoChamber*SmallBlockBunkBed=7:20,2:15,10:5*LargeBlockBed=7:30,2:30,10:8,12:10*LargeBlockCryoRoom=7:40,2:20,4:8,5:8,14:3,6:30,12:10*LargeBlockHalfBed=7:14,2:16,10:6,5:3*LargeBlockHalfBedOffset=7:14,2:16,10:6,5:3*LargeBlockInsetBed=7:60,2:30,10:8*LargeBlockCryoLabVat=7:30,2:10,4:8,12:20,14:3,6:30*LargeBlockCryoChamber=7:40,2:20,4:8,5:8,14:3,6:30,12:10*SmallBlockCryoChamber=7:20,2:10,4:4,5:4,14:3,6:15,12:5$CargoContainer*SmallBlockModularContainer=7:50,2:20,6:5,4:6,5:1*LargeBlockLockerRoom=7:30,2:30,5:4,12:10*LargeBlockLockerRoomCorner=7:25,2:30,5:4,12:10*LargeBlockLockers=7:20,2:20,5:3,6:2*LargeBlockInsetBookshelf=7:30,2:30*LargeBlockCargoTerminal=7:40,2:40,1:4,10:20,4:4,5:1,6:2*LargeBlockCargoTerminalHalf=7:30,2:30,1:2,10:10,4:4,5:1,6:2*LargeBlockLabCornerDesk=7:30,2:30,9:4*LargeBlockLabCabinet=7:20,2:30,10:8,4:4,5:1,12:2*LargeBlockLargeIndustrialContainer=7:360,2:80,1:24,10:60,4:20,5:1,6:8*SmallBlockSmallContainer=7:3,2:1,6:1,4:1,5:1*SmallBlockMediumContainer=7:30,2:10,6:4,4:4,5:1*SmallBlockLargeContainer=7:75,2:25,6:6,4:8,5:1*LargeBlockSmallContainer=7:40,2:40,1:4,10:20,4:4,5:1,6:2*LargeBlockLargeContainer=7:360,2:80,1:24,10:60,4:20,5:1,6:8*LargeBlockWeaponRack=7:30,2:20*SmallBlockWeaponRack=7:3,2:3$Gyro*LargeBlockGyro=0:600,2:40,3:4,1:50,4:4,6:5*SmallBlockGyro=0:25,2:5,3:1,4:2,6:3*LargeBlockPrototechGyro=23:1,26:300,2:40,25:1,29:2,3:16,1:50,6:5*SmallBlockPrototechGyro=23:1,26:70,2:20,25:1,29:2,3:4,1:5,6:3$Kitchen*LargeBlockKitchen=7:20,2:30,3:6,4:6,12:4$Planter*LargeBlockPlanters=7:10,2:20,10:8,12:8$VendingMachine*FoodDispenser=7:20,2:10,4:4,5:10,6:10*VendingMachine=7:20,2:10,4:4,5:4,6:10$LCDPanelsBlock*LabEquipment=7:15,2:15,4:1,12:4*MedicalStation=7:15,2:15,4:2,14:1,5:2*LargeBlockLabDeskMicroscope=7:20,2:20,6:6,9:8,5:4,12:6*LabEquipment1=7:20,2:30,4:4,9:4,12:40*LabEquipment3=7:60,2:50,4:12,20:2,11:8,12:16$TextPanel*TransparentLCDLarge=2:8,6:6,5:10,12:10*TransparentLCDSmall=2:4,6:4,5:3,12:1*HoloLCDLarge=2:10,4:1,6:8*HoloLCDSmall=2:5,4:1,6:8*LargeFullBlockLCDPanel=7:20,2:20,6:6,5:10,12:6*SmallFullBlockLCDPanel=7:4,2:4,6:4,5:3,12:1*LargeDiagonalLCDPanel=7:10,2:10,6:6,5:10,12:8*SmallDiagonalLCDPanel=7:4,2:4,6:4,5:3,12:1*LargeCurvedLCDPanel=7:20,2:20,6:6,5:10,12:10*SmallCurvedLCDPanel=7:4,2:4,6:4,5:3,12:1*SmallTextPanel=7:1,2:4,6:4,5:3,12:1*SmallLCDPanelWide=7:1,2:8,6:8,5:6,12:2*SmallLCDPanel=7:1,2:4,6:4,5:3,12:2*LargeBlockCorner_LCD_1=2:5,6:3,5:1*LargeBlockCorner_LCD_2=2:5,6:3,5:1*LargeBlockCorner_LCD_Flat_1=2:5,6:3,5:1*LargeBlockCorner_LCD_Flat_2=2:5,6:3,5:1*SmallBlockCorner_LCD_1=2:3,6:2,5:1*SmallBlockCorner_LCD_2=2:3,6:2,5:1*SmallBlockCorner_LCD_Flat_1=2:3,6:2,5:1*SmallBlockCorner_LCD_Flat_2=2:3,6:2,5:1*LargeTextPanel=7:1,2:6,6:6,5:10,12:2*LargeLCDPanel=7:1,2:6,6:6,5:10,12:6*LargeLCDPanelWide=7:2,2:12,6:12,5:20,12:12*SmallBlockConsoleModuleScreens=7:6,2:6,5:2,6:2*LargeLCDPanel5x5=7:25,2:150,6:25,5:250,12:150*LargeLCDPanel5x3=7:15,2:90,6:15,5:150,12:90*LargeLCDPanel3x3=7:10,2:50,6:10,5:90,12:50$SolarPanel*LargeBlockColorableSolarPanel=0:4,2:14,13:12,6:4,15:32,12:4*LargeBlockColorableSolarPanelCorner=0:2,2:7,13:6,6:4,15:16,12:2*LargeBlockColorableSolarPanelCornerInverted=0:2,2:7,13:6,6:4,15:16,12:2*SmallBlockColorableSolarPanel=0:2,2:2,13:4,6:1,15:8,12:1*SmallBlockColorableSolarPanelCorner=0:1,2:2,13:2,6:1,15:4,12:1*SmallBlockColorableSolarPanelCornerInverted=0:1,2:2,13:2,6:1,15:4,12:1*LargeBlockSolarPanel=0:4,2:14,13:12,6:4,15:32,12:4*SmallBlockSolarPanel=0:2,2:2,13:4,6:1,15:8,12:1$WindTurbine*LargeBlockWindTurbineReskin=7:40,4:8,2:20,13:24,6:2*LargeBlockWindTurbine=7:40,4:8,2:20,13:24,6:2$MedicalRoom*LargeMedicalRoomReskin=7:240,2:80,1:60,10:20,3:5,5:10,6:10,14:15*LargeMedicalRoom=7:240,2:80,1:60,10:20,3:5,5:10,6:10,14:15*InsetRefillStation=7:20,2:30,1:2,4:6,11:4,5:4*LargeRefillStation=7:9,2:10,1:2,4:6,11:4,5:4*SmallRefillStation=7:9,2:10,1:2,4:6,11:4,5:4$InteriorLight*LargeBlockInsetAquarium=7:30,2:30,4:1,10:10,16:1,12:10*LargeBlockInsetKitchen=7:30,2:30,3:8,4:6,12:6*CorridorRoundLight=7:100,2:30*LabEquipment2=7:40,2:50,4:4,3:4,12:100*LargeBlockInsetLight=0:10,2:10,7:20*SmallBlockInsetLight=0:1,2:2,7:1*AirDuctLight=7:20,2:30,3:4,0:10*SmallLight=2:2*SmallBlockSmallLight=2:2*LargeBlockLight_1corner=2:3*LargeBlockLight_2corner=2:6*SmallBlockLight_1corner=2:2*SmallBlockLight_2corner=2:4*OffsetLight=2:2*LargeBlockInsetWallLight=7:25,2:10*CorridorLight=7:100,2:30*CorridorNarrowStowage=7:100,2:30*TrussPillarLight=7:12,2:8,10:2*TrussPillarLightSmall=7:1,2:2,10:1*PassageSciFiLight=7:74,2:20,10:48*LargeLightPanel=2:10,7:5*SmallLightPanel=2:2,7:1$Ladder2*TrussLadder=13:20,7:10,2:20,10:30*(null)=7:10,2:20,10:10*LadderShaft=7:80,2:40,10:50*LadderSmall=7:10,2:20,10:10$Warhead*LargeExplosiveBarrel=0:5,3:1,2:6,10:2,6:1,17:2*SmallExplosiveBarrel=0:5,3:1,2:6,10:2,6:1,17:2*LargeWarhead=0:20,13:24,2:12,10:12,6:2,17:6*SmallWarhead=0:4,13:1,2:1,10:2,6:1,17:2$AirtightHangarDoor*(null)=0:350,2:40,10:40,4:16,6:2*AirtightHangarDoorWarfare2A=0:350,2:40,10:40,4:16,6:2*AirtightHangarDoorWarfare2B=0:350,2:40,10:40,4:16,6:2*AirtightHangarDoorWarfare2C=0:350,2:40,10:40,4:16,6:2$AirtightSlideDoor*LargeBlockSlideDoor=0:20,2:40,10:4,4:4,5:1,6:2,12:15$StoreBlock*StoreBlock=0:30,2:20,4:6,5:4,6:10*AtmBlock=0:20,2:20,4:2,6:10,5:4$SafeZoneBlock*SafeZoneBlock=0:800,2:180,16:10,18:5,1:80,6:120$ContractBlock*ContractBlock=0:30,2:20,4:6,5:4,6:10$BatteryBlock*LargeBlockBatteryBlock=0:80,2:30,19:80,6:25*SmallBlockBatteryBlock=0:25,2:5,19:20,6:2*SmallBlockSmallBatteryBlock=0:4,2:2,19:2,6:2*LargeBlockPrototechBattery=23:1,2:30,25:3,24:20,1:16,6:25,26:60*SmallBlockPrototechBattery=23:1,2:5,25:1,24:6,1:4,6:2,26:6*LargeBlockBatteryBlockWarfare2=0:80,2:30,19:80,6:25*SmallBlockBatteryBlockWarfare2=0:25,2:5,19:20,6:2$Reactor*SmallBlockSmallGenerator=0:3,2:10,1:2,3:1,20:3,4:1,6:10*SmallBlockLargeGenerator=0:60,2:9,1:9,3:3,20:95,4:5,6:25*LargeBlockSmallGenerator=0:80,2:40,1:4,3:8,20:100,4:6,6:25*LargeBlockLargeGenerator=0:1000,2:70,1:40,3:40,11:100,20:2000,4:20,6:75*LargeBlockSmallGeneratorWarfare2=0:80,2:40,1:4,3:8,20:100,4:6,6:25*LargeBlockLargeGeneratorWarfare2=0:1000,2:70,1:40,3:40,11:100,20:2000,4:20,6:75*SmallBlockSmallGeneratorWarfare2=0:3,2:10,1:2,3:1,20:3,4:1,6:10*SmallBlockLargeGeneratorWarfare2=0:60,2:9,1:9,3:3,20:95,4:5,6:25$HydrogenEngine*LargeHydrogenEngine=0:100,2:70,3:12,10:20,4:12,6:4,19:1*SmallHydrogenEngine=0:30,2:20,3:4,10:6,4:4,6:1,19:1*LargePrototechReactor=23:1,26:400,2:200,24:10,27:30,11:400,20:1000,6:100$DebugSphere1*DebugSphereLarge=0:10,6:20$DebugSphere2*DebugSphereLarge=0:10,6:20$DebugSphere3*DebugSphereLarge=0:10,6:20$OxygenTank*LargeHydrogenTankSmallLab=0:80,3:40,10:60,6:8,2:40*SmallHydrogenTankLab=0:40,3:20,10:30,6:4,2:20*LargeBlockOxygenTankLab=0:80,3:40,10:60,6:8,2:40*LargeHydrogenTankIndustrial=0:280,3:80,10:60,6:8,2:40*OxygenTankSmall=0:16,3:8,10:10,6:8,2:10*SmallOxygenTankSmall=0:2,3:1,10:1,6:4,2:1*(null)=0:80,3:40,10:60,6:8,2:40*LargeHydrogenTank=0:280,3:80,10:60,6:8,2:40*LargeHydrogenTankSmall=0:80,3:40,10:60,6:8,2:40*SmallHydrogenTank=0:40,3:20,10:30,6:4,2:20*SmallHydrogenTankSmall=0:3,3:1,10:2,6:4,2:2$OxygenGenerator*LargeBlockOxygenGeneratorLab=0:160,2:20,3:4,4:4,6:5,12:40*SmallBlockOxygenGeneratorLab=0:6,2:8,3:2,4:1,6:3,12:3*(null)=0:120,2:5,3:2,4:4,6:5*OxygenGeneratorSmall=0:8,2:8,3:2,4:1,6:3$ExhaustBlock*LargeExhaustCap=7:10,2:8,4:2*SmallExhaustCap=7:2,2:2,4:1*SmallExhaustPipe=0:2,2:1,10:2,4:2*LargeExhaustPipe=0:15,2:10,3:2,4:4$GravityGenerator*(null)=0:150,16:6,2:60,3:4,4:6,6:40$GravityGeneratorSphere*(null)=0:150,16:6,2:60,3:4,4:6,6:40$VirtualMass*VirtualMassLarge=0:90,11:20,2:30,6:20,16:9*VirtualMassSmall=0:3,11:2,2:2,6:2,16:1$SpaceBall*SpaceBallLarge=0:225,2:30,6:20,16:3*SpaceBallSmall=0:70,2:10,6:7,16:1$AirVent*AirVentFan=0:30,2:20,4:10,6:5*AirVentFanFull=0:45,2:30,4:10,6:5*SmallAirVentFan=0:3,2:10,4:2,6:5*SmallAirVentFanFull=0:5,2:15,4:2,6:5*(null)=0:30,2:20,4:10,6:5*AirVentFull=0:45,2:30,4:10,6:5*SmallAirVent=0:3,2:10,4:2,6:5*SmallAirVentFull=0:5,2:15,4:2,6:5$CameraBlock*LargeCameraTopMounted=0:2,6:3*SmallCameraTopMounted=0:2,6:3*SmallCameraBlock=0:2,6:3*LargeCameraBlock=0:2,6:3$EmotionControllerBlock*EmotionControllerLarge=7:10,2:30,6:20,5:12,12:6*EmotionControllerSmall=7:1,2:3,6:5,5:1,12:1$LandingGear*LargeBlockMagneticPlate=0:450,2:60,4:20*SmallBlockMagneticPlate=0:6,2:15,4:3*LargeBlockLandingGear=0:150,2:20,4:6*SmallBlockLandingGear=0:2,2:5,4:1*LargeBlockSmallMagneticPlate=0:15,2:3,4:1*SmallBlockSmallMagneticPlate=0:2,2:1,4:1$ConveyorConnector*LargeBlockConveyorPipeSeamless=7:14,2:20,10:12,4:6*LargeBlockConveyorPipeCorner=7:14,2:20,10:12,4:6*LargeBlockConveyorPipeFlange=7:14,2:20,10:12,4:6*LargeBlockConveyorPipeEnd=7:14,2:20,10:12,4:6*ConveyorTube=7:14,2:20,10:12,4:6*ConveyorTubeDuct=0:25,7:14,2:20,10:12,4:6*ConveyorTubeDuctCurved=0:25,7:14,2:20,10:12,4:6*ConveyorTubeSmall=7:1,4:1,2:1*ConveyorTubeDuctSmall=0:2,7:1,4:1,2:1*ConveyorTubeDuctSmallCurved=0:2,7:1,4:1,2:1*ConveyorTubeMedium=7:10,2:20,10:10,4:6*ConveyorFrameMedium=7:5,2:12,10:5,4:2*ConveyorTubeCurved=7:14,2:20,10:12,4:6*ConveyorTubeSmallCurved=7:1,4:1,2:1*ConveyorTubeCurvedMedium=7:7,2:20,10:10,4:6$Conveyor*LargeBlockConveyorPipeJunction=7:20,2:30,10:20,4:6*LargeBlockConveyorPipeIntersection=7:18,2:20,10:16,4:6*LargeBlockConveyorPipeT=7:16,2:24,10:14,4:6*SmallBlockConveyor=7:4,2:4,4:1*SmallBlockConveyorConverter=7:6,2:8,10:6,4:2*LargeBlockConveyor=7:20,2:30,10:20,4:6*ConveyorTubeDuctT=0:22,7:16,2:24,10:14,4:6*ConveyorTubeDuctSmallT=0:2,7:2,4:1,2:2*SmallShipConveyorHub=7:15,2:20,10:15,4:2*ConveyorTubeSmallT=7:2,4:1,2:2*ConveyorTubeT=7:16,2:24,10:14,4:6$Assembler*LargeAssemblerIndustrial=0:140,2:80,4:20,5:10,1:10,6:160*LargeAssembler=0:140,2:80,4:20,5:10,1:10,6:160*BasicAssembler=0:80,2:40,4:10,5:4,6:80*LargePrototechAssembler=23:1,26:240,2:130,29:20,27:2,1:80,6:200,5:10$Refinery*LargeRefineryIndustrial=0:1200,2:40,3:20,4:16,1:20,6:20*LargeRefinery=0:1200,2:40,3:20,4:16,1:20,6:20*BlastFurnace=0:120,2:20,4:10,6:10*LargePrototechRefinery=23:1,26:675,2:40,29:10,27:5,3:20,1:20,6:20*SmallPrototechRefinery=23:1,26:70,2:20,29:3,27:2,3:16,1:20,6:20$ConveyorSorter*LargeBlockConveyorSorterIndustrial=7:50,2:120,10:50,6:20,4:2*LargeBlockConveyorSorter=7:50,2:120,10:50,6:20,4:2*MediumBlockConveyorSorter=7:5,2:12,10:5,6:5,4:2*SmallBlockConveyorSorter=7:5,2:12,10:5,6:5,4:2$Thrust*LargeBlockLargeHydrogenThrustIndustrial=0:150,2:180,1:250,3:40*LargeBlockSmallHydrogenThrustIndustrial=0:25,2:60,1:40,3:8*SmallBlockLargeHydrogenThrustIndustrial=0:30,2:30,1:22,3:10*SmallBlockSmallHydrogenThrustIndustrial=0:7,2:15,1:4,3:2*LargeBlockPrototechThruster=23:1,26:500,2:325,27:5,28:60,1:250,3:160*SmallBlockPrototechThruster=23:1,26:10,2:12,27:1,28:3,1:5,3:1*SmallBlockSmallThrustSciFi=0:2,2:2,3:1,30:1*SmallBlockLargeThrustSciFi=0:5,2:2,3:5,30:12*LargeBlockSmallThrustSciFi=0:25,2:60,3:8,30:80*LargeBlockLargeThrustSciFi=0:150,2:100,3:40,30:960*LargeBlockLargeAtmosphericThrustSciFi=0:230,2:60,3:50,1:40,4:1100*LargeBlockSmallAtmosphericThrustSciFi=0:35,2:50,3:8,1:10,4:110*SmallBlockLargeAtmosphericThrustSciFi=0:20,2:30,3:4,1:8,4:90*SmallBlockSmallAtmosphericThrustSciFi=0:3,2:22,3:1,1:1,4:18*SmallBlockSmallThrust=0:2,2:2,3:1,30:1*SmallBlockLargeThrust=0:5,2:2,3:5,30:12*LargeBlockSmallThrust=0:25,2:60,3:8,30:80*LargeBlockLargeThrust=0:150,2:100,3:40,30:960*LargeBlockLargeHydrogenThrust=0:150,2:180,1:250,3:40*LargeBlockSmallHydrogenThrust=0:25,2:60,1:40,3:8*SmallBlockLargeHydrogenThrust=0:30,2:30,1:22,3:10*SmallBlockSmallHydrogenThrust=0:7,2:15,1:4,3:2*LargeBlockLargeAtmosphericThrust=0:230,2:60,3:50,1:40,4:1100*LargeBlockSmallAtmosphericThrust=0:35,2:50,3:8,1:10,4:110*SmallBlockLargeAtmosphericThrust=0:20,2:30,3:4,1:8,4:90*SmallBlockSmallAtmosphericThrust=0:3,2:22,3:1,1:1,4:18*LargeBlockLargeFlatAtmosphericThrust=0:90,2:25,3:20,1:15,4:400*LargeBlockLargeFlatAtmosphericThrustDShape=0:90,2:25,3:20,1:15,4:400*LargeBlockSmallFlatAtmosphericThrust=0:15,2:20,3:3,1:3,4:30*LargeBlockSmallFlatAtmosphericThrustDShape=0:15,2:20,3:3,1:3,4:30*SmallBlockLargeFlatAtmosphericThrust=0:8,2:14,3:2,1:3,4:30*SmallBlockLargeFlatAtmosphericThrustDShape=0:8,2:14,3:2,1:3,4:30*SmallBlockSmallFlatAtmosphericThrust=0:2,2:11,3:1,1:1,4:6*SmallBlockSmallFlatAtmosphericThrustDShape=0:2,2:11,3:1,1:1,4:6*SmallBlockSmallModularThruster=0:2,2:2,3:1,30:1*SmallBlockLargeModularThruster=0:5,2:2,3:5,30:12*LargeBlockSmallModularThruster=0:25,2:60,3:8,30:80*LargeBlockLargeModularThruster=0:150,2:100,3:40,30:960$Passage*(null)=7:74,2:20,10:48$Collector*Collector=0:45,2:50,10:12,4:8,5:4,6:10*CollectorSmall=0:35,2:35,10:12,4:8,5:2,6:8$ShipConnector*Connector=0:150,2:40,10:12,4:8,6:20*ConnectorSmall=0:7,2:4,10:2,4:1,6:4*ConnectorMedium=0:21,2:12,10:6,4:6,6:6*LargeBlockInsetConnector=0:150,2:40,10:12,4:8,6:20*LargeBlockInsetConnectorSmall=0:150,2:40,10:12,4:8,6:20*SmallBlockInsetConnector=0:7,2:4,10:2,4:1,6:4*SmallBlockInsetConnectorMedium=0:21,2:12,10:6,4:6,6:6$PistonBase*LargePistonBase=0:15,2:10,3:4,4:4,6:2*SmallPistonBase=0:4,2:4,10:4,4:2,6:1$MotorStator*LargeStator=0:15,2:10,3:4,4:4,6:2*SmallStator=0:5,2:5,10:1,4:1,6:1$MotorRotor*LargeRotor=0:30,3:6*SmallRotor=0:12,10:6$MotorAdvancedStator*LargeAdvancedStator=0:15,2:10,3:4,4:4,6:2*SmallAdvancedStator=0:10,2:6,10:1,4:2,6:2*SmallAdvancedStatorSmall=0:5,2:5,10:1,4:1,6:1*LargeHinge=0:16,2:10,3:4,4:4,6:2*MediumHinge=0:10,2:6,3:2,4:2,6:2*SmallHinge=0:6,2:4,3:1,4:2,6:2$MotorAdvancedRotor*LargeAdvancedRotor=0:30,3:10*SmallAdvancedRotor=0:20,3:6*SmallAdvancedRotorSmall=0:12,10:6*LargeHingeHead=0:12,3:4,2:8*MediumHingeHead=0:6,3:2,2:4*SmallHingeHead=0:3,3:1,2:2$SurvivalKit*SurvivalKitLarge=0:30,2:2,14:3,4:4,5:1,6:5*SurvivalKit=0:6,2:2,14:3,4:4,5:1,6:5$OxygenFarm*LargeBlockOxygenFarm=0:40,12:100,3:20,10:10,2:20,6:20$UpgradeModule*LargeProductivityModule=0:100,2:40,10:20,6:60,4:4*LargeEffectivenessModule=0:100,2:50,10:15,11:20,4:4*LargeEnergyModule=0:100,2:40,10:20,19:20,4:4$JumpDrive*LargePrototechJumpDrive=23:1,16:30,24:30,25:20,11:1400,6:300,2:180,26:200*SmallPrototechJumpDrive=23:1,16:4,24:8,25:4,11:100,6:20,2:20,26:15*LargeJumpDrive=0:60,1:50,16:20,9:20,19:120,11:1000,6:300,2:40$Drill*LargeBlockPrototechDrill=23:1,2:200,3:120,29:20,27:3,1:80,6:20,26:200*SmallBlockDrill=0:32,2:30,3:4,4:1,6:1*LargeBlockDrill=0:300,2:40,3:12,4:5,6:5$MotorSuspension*OffroadSuspension3x3=0:25,2:15,3:6,10:12,4:6*OffroadSuspension5x5=0:70,2:40,3:20,10:30,4:20*OffroadSuspension1x1=0:25,2:15,3:6,10:12,4:6*OffroadSuspension2x2=0:25,2:15,3:6,10:12,4:6*OffroadSmallSuspension3x3=0:8,2:7,10:2,4:1*OffroadSmallSuspension5x5=0:16,2:12,10:4,4:2*OffroadSmallSuspension1x1=0:8,2:7,10:2,4:1*OffroadSmallSuspension2x2=0:8,2:7,10:2,4:1*OffroadSuspension3x3mirrored=0:25,2:15,3:6,10:12,4:6*OffroadSuspension5x5mirrored=0:70,2:40,3:20,10:30,4:20*OffroadSuspension1x1mirrored=0:25,2:15,3:6,10:12,4:6*OffroadSuspension2x2Mirrored=0:25,2:15,3:6,10:12,4:6*OffroadSmallSuspension3x3mirrored=0:8,2:7,10:2,4:1*OffroadSmallSuspension5x5mirrored=0:16,2:12,10:4,4:2*OffroadSmallSuspension1x1mirrored=0:8,2:7,10:2,4:1*OffroadSmallSuspension2x2Mirrored=0:8,2:7,10:2,4:1*OffroadShortSuspension3x3=0:15,2:10,3:6,10:12,4:6*OffroadShortSuspension5x5=0:45,2:30,3:20,10:30,4:20*OffroadShortSuspension1x1=0:15,2:10,3:6,10:12,4:6*OffroadShortSuspension2x2=0:15,2:10,3:6,10:12,4:6*OffroadSmallShortSuspension3x3=0:5,2:5,10:2,4:1*OffroadSmallShortSuspension5x5=0:10,2:10,10:4,4:2*OffroadSmallShortSuspension1x1=0:5,2:5,10:2,4:1*OffroadSmallShortSuspension2x2=0:5,2:5,10:2,4:1*OffroadShortSuspension3x3mirrored=0:15,2:10,3:6,10:12,4:6*OffroadShortSuspension5x5mirrored=0:45,2:30,3:20,10:30,4:20*OffroadShortSuspension1x1mirrored=0:15,2:10,3:6,10:12,4:6*OffroadShortSuspension2x2Mirrored=0:15,2:10,3:6,10:12,4:6*OffroadSmallShortSuspension3x3mirrored=0:5,2:5,10:2,4:1*OffroadSmallShortSuspension5x5mirrored=0:10,2:10,10:4,4:2*OffroadSmallShortSuspension1x1mirrored=0:5,2:5,10:2,4:1*OffroadSmallShortSuspension2x2Mirrored=0:5,2:5,10:2,4:1*Suspension3x3=0:25,2:15,3:6,10:12,4:6*Suspension5x5=0:70,2:40,3:20,10:30,4:20*Suspension1x1=0:25,2:15,3:6,10:12,4:6*Suspension2x2=0:25,2:15,3:6,10:12,4:6*SmallSuspension3x3=0:8,2:7,10:2,4:1*SmallSuspension5x5=0:16,2:12,10:4,4:2*SmallSuspension1x1=0:8,2:7,10:2,4:1*SmallSuspension2x2=0:8,2:7,10:2,4:1*Suspension3x3mirrored=0:25,2:15,3:6,10:12,4:6*Suspension5x5mirrored=0:70,2:40,3:20,10:30,4:20*Suspension1x1mirrored=0:25,2:15,3:6,10:12,4:6*Suspension2x2Mirrored=0:25,2:15,3:6,10:12,4:6*SmallSuspension3x3mirrored=0:8,2:7,10:2,4:1*SmallSuspension5x5mirrored=0:16,2:12,10:4,4:2*SmallSuspension1x1mirrored=0:8,2:7,10:2,4:1*SmallSuspension2x2Mirrored=0:8,2:7,10:2,4:1*ShortSuspension3x3=0:15,2:10,3:6,10:12,4:6*ShortSuspension5x5=0:45,2:30,3:20,10:30,4:20*ShortSuspension1x1=0:15,2:10,3:6,10:12,4:6*ShortSuspension2x2=0:15,2:10,3:6,10:12,4:6*SmallShortSuspension3x3=0:5,2:5,10:2,4:1*SmallShortSuspension5x5=0:10,2:10,10:4,4:2*SmallShortSuspension1x1=0:5,2:5,10:2,4:1*SmallShortSuspension2x2=0:5,2:5,10:2,4:1*ShortSuspension3x3mirrored=0:15,2:10,3:6,10:12,4:6*ShortSuspension5x5mirrored=0:45,2:30,3:20,10:30,4:20*ShortSuspension1x1mirrored=0:15,2:10,3:6,10:12,4:6*ShortSuspension2x2Mirrored=0:15,2:10,3:6,10:12,4:6*SmallShortSuspension3x3mirrored=0:5,2:5,10:2,4:1*SmallShortSuspension5x5mirrored=0:10,2:10,10:4,4:2*SmallShortSuspension1x1mirrored=0:5,2:5,10:2,4:1*SmallShortSuspension2x2Mirrored=0:5,2:5,10:2,4:1$Wheel*OffroadSmallRealWheel1x1=0:2,2:5,3:1*OffroadSmallRealWheel2x2=0:8,2:15,3:3*OffroadSmallRealWheel=0:8,2:15,3:3*OffroadSmallRealWheel5x5=0:15,2:25,3:5*OffroadRealWheel1x1=0:30,2:30,3:10*OffroadRealWheel2x2=0:50,2:40,3:15*OffroadRealWheel=0:70,2:50,3:20*OffroadRealWheel5x5=0:130,2:70,3:30*OffroadSmallRealWheel1x1mirrored=0:2,2:5,3:1*OffroadSmallRealWheel2x2Mirrored=0:8,2:15,3:3*OffroadSmallRealWheelmirrored=0:8,2:15,3:3*OffroadSmallRealWheel5x5mirrored=0:15,2:25,3:5*OffroadRealWheel1x1mirrored=0:30,2:30,3:10*OffroadRealWheel2x2Mirrored=0:50,2:40,3:15*OffroadRealWheelmirrored=0:70,2:50,3:20*OffroadRealWheel5x5mirrored=0:130,2:70,3:30*OffroadWheel1x1=0:30,2:30,3:10*OffroadSmallWheel1x1=0:2,2:5,3:1*OffroadWheel3x3=0:70,2:50,3:20*OffroadSmallWheel3x3=0:8,2:15,3:3*OffroadWheel5x5=0:130,2:70,3:30*OffroadSmallWheel5x5=0:15,2:25,3:5*OffroadWheel2x2=0:50,2:40,3:15*OffroadSmallWheel2x2=0:5,2:10,3:2*SmallRealWheel1x1=0:2,2:5,3:1*SmallRealWheel2x2=0:8,2:15,3:3*SmallRealWheel=0:8,2:15,3:3*SmallRealWheel5x5=0:15,2:25,3:5*RealWheel1x1=0:30,2:30,3:10*RealWheel2x2=0:50,2:40,3:15*RealWheel=0:70,2:50,3:20*RealWheel5x5=0:130,2:70,3:30*SmallRealWheel1x1mirrored=0:2,2:5,3:1*SmallRealWheel2x2Mirrored=0:8,2:15,3:3*SmallRealWheelmirrored=0:8,2:15,3:3*SmallRealWheel5x5mirrored=0:15,2:25,3:5*RealWheel1x1mirrored=0:30,2:30,3:10*RealWheel2x2Mirrored=0:50,2:40,3:15*RealWheelmirrored=0:70,2:50,3:20*RealWheel5x5mirrored=0:130,2:70,3:30*Wheel1x1=0:30,2:30,3:10*SmallWheel1x1=0:2,2:5,3:1*Wheel3x3=0:70,2:50,3:20*SmallWheel3x3=0:8,2:15,3:3*Wheel5x5=0:130,2:70,3:30*SmallWheel5x5=0:15,2:25,3:5*Wheel2x2=0:50,2:40,3:15*SmallWheel2x2=0:5,2:10,3:2$Decoy*TrussPillarDecoy=0:30,2:10,6:10,8:1,3:2*LargeDecoy=0:30,2:10,6:10,8:1,3:2*SmallDecoy=0:2,2:1,6:1,8:1,10:2$EmissiveBlock*LargeNeonTubesStraight1=7:6,10:6,2:2*LargeNeonTubesStraight2=7:6,10:6,2:2*LargeNeonTubesCorner=7:6,10:6,2:2*LargeNeonTubesBendUp=7:12,10:12,2:4*LargeNeonTubesBendDown=7:3,10:3,2:1*LargeNeonTubesStraightEnd1=7:6,10:6,2:2*LargeNeonTubesStraightEnd2=7:10,10:6,2:4*LargeNeonTubesStraightDown=7:9,10:9,2:3*LargeNeonTubesU=7:18,10:18,2:6*LargeNeonTubesT=7:9,10:9,2:3*LargeNeonTubesCircle=7:12,10:12,2:4*SmallNeonTubesStraight1=7:1,10:1,2:1*SmallNeonTubesStraight2=7:1,10:1,2:1*SmallNeonTubesCorner=7:1,10:1,2:1*SmallNeonTubesBendUp=7:1,10:1,2:1*SmallNeonTubesBendDown=7:1,10:1,2:1*SmallNeonTubesStraightDown=7:1,10:1,2:1*SmallNeonTubesStraightEnd1=7:1,10:1,2:1*SmallNeonTubesU=7:1,10:1,2:1*SmallNeonTubesT=7:1,10:1,2:1*SmallNeonTubesCircle=7:1,10:1,2:1$ShipGrinder*LargeShipGrinder=0:20,2:30,3:1,4:4,6:2*SmallShipGrinder=0:12,2:17,10:4,4:4,6:2$ShipWelder*LargeShipWelder=0:20,2:30,3:1,4:2,6:2*SmallShipWelder=0:12,2:17,10:6,4:2,6:2$OreDetector*LargeOreDetector=0:50,2:40,4:5,6:25,9:20*SmallBlockOreDetector=0:3,2:2,4:1,6:1,9:1$MergeBlock*LargeShipMergeBlock=0:12,2:15,4:2,3:6,6:2*SmallShipMergeBlock=0:4,2:5,4:1,10:2,6:1*SmallShipSmallMergeBlock=0:2,2:3,4:1,10:1,6:1$Parachute*LgParachute=0:9,2:25,10:5,4:3,6:2*SmParachute=0:2,2:2,10:1,4:1,6:1$SmallMissileLauncher*SmallMissileLauncherWarfare2=0:4,2:2,1:1,3:4,4:1,6:1*(null)=0:4,2:2,1:1,3:4,4:1,6:1*LargeMissileLauncher=0:35,2:8,1:30,3:25,4:6,6:4*LargeBlockLargeCalibreGun=0:250,2:20,1:20,3:20,6:5*LargeFlareLauncher=0:20,2:10,3:10,6:4*SmallFlareLauncher=0:2,2:1,3:3,6:1$SmallGatlingGun*SmallGatlingGunWarfare2=0:4,2:1,1:2,10:6,4:1,6:1*(null)=0:4,2:1,1:2,10:6,4:1,6:1*SmallBlockAutocannon=0:6,2:2,1:2,10:2,4:1,6:1$Searchlight*SmallSearchlight=0:1,2:3,3:1,4:2,6:5,12:2*LargeSearchlight=0:5,2:20,3:2,4:4,6:5,12:4$HeatVentBlock*LargeHeatVentBlock=0:25,2:20,3:10,4:5*SmallHeatVentBlock=0:2,2:1,3:1,4:1$InteriorTurret*LargeInteriorTurret=7:6,2:20,10:1,4:2,6:5,0:4$SmallMissileLauncherReload*SmallRocketLauncherReload=10:50,7:50,2:24,3:8,1:10,4:4,6:2,0:8*SmallBlockMediumCalibreGun=0:25,2:10,1:5,3:10,6:1*LargeRailgun=0:350,2:150,11:150,3:60,19:100,6:100*SmallRailgun=0:25,2:20,11:20,3:6,19:10,6:20";
		// the line above this one is really long
	}
}