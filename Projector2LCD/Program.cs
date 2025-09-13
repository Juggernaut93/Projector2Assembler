using Sandbox.ModAPI.Ingame;
using System.Collections.Generic;
using System.Linq;
using System;
using VRage.Game.ModAPI.Ingame;
using VRage;
using VRage.Game.GUI.TextPanel;

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

		private enum Ingots { Cobalt, Gold, Iron, Magnesium, Nickel, Platinum, Silicon, Silver, Stone, Uranium }
		private enum Ores { Cobalt, Gold, Ice, Iron, Magnesium, Nickel, Platinum, Scrap, Silicon, Silver, Stone, Uranium }

		private static MyFixedPoint FP(string v) => MyFixedPoint.DeserializeString(v);

		private readonly Dictionary<string, Dictionary<Ingots, MyFixedPoint>> componentsToIngots =
			new Dictionary<string, Dictionary<Ingots, MyFixedPoint>>()
			{
				["BulletproofGlass"] = new Dictionary<Ingots, MyFixedPoint>() { [Ingots.Silicon] = 15 },
				["ComputerComponent"] = new Dictionary<Ingots, MyFixedPoint>() { [Ingots.Iron] = FP("0.5"), [Ingots.Silicon] = FP("0.2") },
				["ConstructionComponent"] = new Dictionary<Ingots, MyFixedPoint>() { [Ingots.Iron] = 8 },
				["DetectorComponent"] = new Dictionary<Ingots, MyFixedPoint>() { [Ingots.Iron] = 5, [Ingots.Nickel] = 15 },
				["Display"] = new Dictionary<Ingots, MyFixedPoint>() { [Ingots.Iron] = 1, [Ingots.Silicon] = 5 },
				["EngineerPlushie"] = new Dictionary<Ingots, MyFixedPoint>() { },
				["ExplosivesComponent"] = new Dictionary<Ingots, MyFixedPoint>() { [Ingots.Silicon] = FP("0.5"), [Ingots.Magnesium] = 2 },
				["GirderComponent"] = new Dictionary<Ingots, MyFixedPoint>() { [Ingots.Iron] = 6 },
				["GravityGeneratorComponent"] = new Dictionary<Ingots, MyFixedPoint>() { [Ingots.Iron] = 600, [Ingots.Silver] = 5, [Ingots.Gold] = 10, [Ingots.Cobalt] = 220 },
				["InteriorPlate"] = new Dictionary<Ingots, MyFixedPoint>() { [Ingots.Iron] = 3 },
				["LargeTube"] = new Dictionary<Ingots, MyFixedPoint>() { [Ingots.Iron] = 30 },
				["MedicalComponent"] = new Dictionary<Ingots, MyFixedPoint>() { [Ingots.Iron] = 60, [Ingots.Nickel] = 70, [Ingots.Silver] = 20 },
				["MetalGrid"] = new Dictionary<Ingots, MyFixedPoint>() { [Ingots.Iron] = 12, [Ingots.Nickel] = 5, [Ingots.Cobalt] = 3 },
				["MotorComponent"] = new Dictionary<Ingots, MyFixedPoint>() { [Ingots.Iron] = 20, [Ingots.Nickel] = 5 },
				["PowerCell"] = new Dictionary<Ingots, MyFixedPoint>() { [Ingots.Iron] = 10, [Ingots.Nickel] = 2, [Ingots.Silicon] = 1 },
				["RadioCommunicationComponent"] = new Dictionary<Ingots, MyFixedPoint>() { [Ingots.Iron] = 8, [Ingots.Silicon] = 1 },
				["ReactorComponent"] = new Dictionary<Ingots, MyFixedPoint>() { [Ingots.Iron] = 15, [Ingots.Stone] = 20, [Ingots.Silver] = 5 },
				["SabiroidPlushie"] = new Dictionary<Ingots, MyFixedPoint>() { },
				["SmallTube"] = new Dictionary<Ingots, MyFixedPoint>() { [Ingots.Iron] = 5 },
				["SolarCell"] = new Dictionary<Ingots, MyFixedPoint>() { [Ingots.Nickel] = 3, [Ingots.Silicon] = 6 },
				["SteelPlate"] = new Dictionary<Ingots, MyFixedPoint>() { [Ingots.Iron] = 21 },
				["Superconductor"] = new Dictionary<Ingots, MyFixedPoint>() { [Ingots.Iron] = 10, [Ingots.Gold] = 2 },
				["ThrustComponent"] = new Dictionary<Ingots, MyFixedPoint>() { [Ingots.Iron] = 30, [Ingots.Cobalt] = 10, [Ingots.Gold] = 1, [Ingots.Platinum] = FP("0.4") },
				["ZoneChip"] = null,
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

		private readonly Ores[] basicRefineryOres = new[] { Ores.Iron, Ores.Nickel, Ores.Cobalt, Ores.Silicon, Ores.Magnesium, Ores.Stone, Ores.Scrap };

		private readonly Dictionary<Ingots, Ores[]> ingotToOres = new Dictionary<Ingots, Ores[]>()
		{
			[Ingots.Cobalt] = new[] { Ores.Cobalt },
			[Ingots.Gold] = new[] { Ores.Gold },
			[Ingots.Iron] = new[] { Ores.Iron, Ores.Scrap },
			[Ingots.Magnesium] = new[] { Ores.Magnesium },
			[Ingots.Nickel] = new[] { Ores.Nickel },
			[Ingots.Platinum] = new[] { Ores.Platinum },
			[Ingots.Silicon] = new[] { Ores.Silicon },
			[Ingots.Silver] = new[] { Ores.Silver },
			[Ingots.Stone] = new[] { Ores.Stone },
			[Ingots.Uranium] = new[] { Ores.Uranium },
		};

		private readonly Dictionary<Ores, MyFixedPoint> conversionRates = new Dictionary<Ores, MyFixedPoint>()
		{
			[Ores.Cobalt] = FP("0.3"),
			[Ores.Gold] = FP("0.01"),
			[Ores.Ice] = 0,
			[Ores.Iron] = FP("0.7"),
			[Ores.Magnesium] = FP("0.007"),
			[Ores.Nickel] = FP("0.4"),
			[Ores.Platinum] = FP("0.005"),
			[Ores.Scrap] = FP("0.8"),
			[Ores.Silicon] = FP("0.7"),
			[Ores.Silver] = FP("0.1"),
			[Ores.Stone] = FP("0.014"),
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
			maxComponentLength = componentTranslation.Values.Max(n => n.Length);
			maxIngotLength = ingotTranslation.Values.Max(n => n.Length);
			maxOreLength = Math.Max(
				oreTranslation.Values.Max(n => n.Length),
				basicRefineryOres.Max(o => oreTranslation[o].Length + 1)
			);
			if (oreTranslation[Ores.Scrap].Length == maxOreLength) maxOreLength++;

			// parse blockDefinitionData
			var splitted = blockDefinitionData.Split('$');
			var componentNames = splitted[0].Split('*');
			for (var i = 0; i < componentNames.Length; i++)
				componentNames[i] = "MyObjectBuilder_BlueprintDefinition/" + componentNames[i];

			for (var i = 1; i < splitted.Length; i++)
			{
				var blocks = splitted[i].Split('*');
				var typeName = "MyObjectBuilder_" + blocks[0];

				for (var j = 1; j < blocks.Length; j++)
				{
					var compSplit = blocks[j].Split('=');
					var blockName = typeName + '/' + compSplit[0];

					try { blueprints.Add(blockName, new Dictionary<string, int>()); }
					catch { Echo("Error adding block: " + blockName); }

					foreach (var component in compSplit[1].Split(','))
					{
						var a = component.Split(':');
						int idx = Convert.ToInt32(a[0]), amount = Convert.ToInt32(a[1]);
						blueprints[blockName].Add(componentNames[idx], amount);
					}
				}
			}

			if (ingotDecimals < 0 || oreDecimals < 0)
			{
				Echo("Error: *Decimals cannot be negative. Script needs to be restarted.");
				Runtime.UpdateFrequency = UpdateFrequency.None; return;
			}
			if (ingotWidth < ingotDecimals)
			{
				Echo("Error: ingotDigits cannot be less than ingotDecimals. Script needs to be restarted.");
				Runtime.UpdateFrequency = UpdateFrequency.None; return;
			}
			if (oreWidth < oreDecimals)
			{
				Echo("Error: oreDigits cannot be less than oreDecimals. Script needs to be restarted.");
				Runtime.UpdateFrequency = UpdateFrequency.None; return;
			}

			if (!string.IsNullOrEmpty(Storage))
			{
				var props = Storage.Split(';'); Storage = "";
				try
				{
					projectorName = props[0]; lcdName1 = props[1]; lcdName2 = props[2]; lcdName3 = props[3];
					lightArmor = bool.Parse(props[4]);
					Runtime.UpdateFrequency = (UpdateFrequency)Enum.Parse(typeof(UpdateFrequency), props[5]);
					effectivenessMultiplier = double.Parse(props[6]);
					averageEffectivenesses = bool.Parse(props[7]);
				}
				catch
				{
					Echo("Error while trying to restore previous configuration. Script needs to be restarted.");
					projectorName = lcdName1 = lcdName2 = lcdName3 = "";
					lightArmor = true; Runtime.UpdateFrequency = UpdateFrequency.None;
					effectivenessMultiplier = 1; averageEffectivenesses = true; return;
				}
			}
		}

		public Dictionary<string, int> GetComponents(string definition)
		{
			if (!blueprints.ContainsKey(definition))
			{
				string errorText = "Unknown Blocks in Blueprint. Go to https://github.com/Juggernaut93/Projector2Assembler, and follow instructions for BlockDefinitionExtractor.";
				WriteToAll(errorText); throw new Exception(errorText);
			}
			return blueprints[definition];
		}

		public void AddComponents(Dictionary<string, int> addTo, Dictionary<string, int> addFrom, int times = 1)
		{
			foreach (var kv in addFrom)
				addTo[kv.Key] = (addTo.ContainsKey(kv.Key) ? addTo[kv.Key] : 0) + kv.Value * times;
		}

		private void SaveProperty(string s) => Storage += s + ";";

		public void Save()
		{
			Storage = string.Join(";", new[]
			{
				projectorName, lcdName1, lcdName2, lcdName3,
				lightArmor.ToString(), Runtime.UpdateFrequency.ToString(),
				effectivenessMultiplier.ToString(), averageEffectivenesses.ToString()
			}) + ";";
		}

		private void AddCountToDict<T>(Dictionary<T, MyFixedPoint> dic, T key, MyFixedPoint amount)
			=> dic[key] = GetCountFromDic(dic, key) + amount;

		private MyFixedPoint GetCountFromDic<T>(Dictionary<T, MyFixedPoint> dic, T key)
		{
			MyFixedPoint v;
			return dic.TryGetValue(key, out v) ? v : default(MyFixedPoint);
		}

		private void WriteToAll(string s)
		{
			ShowAndSetFontSize(lcd1, s);
			ShowAndSetFontSize(lcd2, s);
			ShowAndSetFontSize(lcd3, s);
		}

		private List<KeyValuePair<string, int>> GetTotalComponents(IMyProjector projector)
		{
			var blocks = projector.RemainingBlocksPerType;
			var total = new Dictionary<string, int>();
			foreach (var item in blocks)
			{
				var info = item.ToString().Trim('[', ']').Split(',');
				string blockName = info[0].Replace(" ", "");
				int amount = Convert.ToInt32(info[1]);
				AddComponents(total, GetComponents(blockName), amount);
			}

			bool large = projector.BlockDefinition.SubtypeId == "LargeProjector";
			string armorType = "MyObjectBuilder_CubeBlock/" +
				(large
					? (lightArmor ? "LargeBlockArmorBlock" : "LargeHeavyBlockArmorBlock")
					: (lightArmor ? "SmallBlockArmorBlock" : "SmallHeavyBlockArmorBlock"));
			AddComponents(total, GetComponents(armorType), projector.RemainingArmorBlocks);

			var list = total.ToList();
			list.Sort((x, y) => string.Compare(TranslateDef(x.Key), TranslateDef(y.Key)));
			return list;
		}

		private string TranslateDef(string d) => componentTranslation[d.Replace("MyObjectBuilder_BlueprintDefinition/", "")];
		private string StripDef(string s) => s.Replace("MyObjectBuilder_BlueprintDefinition/", "");

		private int GetWholeDigits(MyFixedPoint amt)
		{
			string s = amt.ToString(); int i = s.IndexOf('.'); return i > -1 ? i : s.Length;
		}

		private string FormatNumber(MyFixedPoint amt, int maxWidth, int maxDecimalPlaces)
		{
			int wholeDigits = GetWholeDigits(amt);
			string mul = " ";

			if (amt.ToString().Length > maxWidth - 1 && amt >= 1000)
			{
				mul = thousands; amt *= (1 / 1000f); wholeDigits = GetWholeDigits(amt);
				if (amt.ToString().Length > maxWidth - 1 && amt >= 1000)
				{
					mul = millions; amt *= (1 / 1000f); wholeDigits = GetWholeDigits(amt);
					if (amt.ToString().Length > maxWidth - 1 && amt >= 1000)
					{ mul = billions; amt *= (1 / 1000f); wholeDigits = GetWholeDigits(amt); }
				}
			}
			string s = amt.ToString(); int p = s.IndexOf('.');
			maxDecimalPlaces = p == -1 ? 0 : Math.Min(maxDecimalPlaces, s.Length - p - 1);
			return string.Format("{0," + (maxWidth - 1) + ":F" + Math.Max(0, Math.Min(maxWidth - wholeDigits - 2, maxDecimalPlaces)) + "}" + mul, (decimal)amt);
		}

		private List<KeyValuePair<Ingots, MyFixedPoint>> GetTotalIngots(List<KeyValuePair<string, int>> components)
		{
			var need = new Dictionary<Ingots, MyFixedPoint>();
			foreach (var pair in components)
			{
				var ing = componentsToIngots[StripDef(pair.Key)];
				if (ing != null)
					foreach (var k in ing)
						AddCountToDict(need, k.Key, k.Value * (pair.Value / (float)ASSEMBLER_EFFICIENCY));
			}
			var list = need.ToList();
			list.Sort((x, y) => string.Compare(ingotTranslation[x.Key], ingotTranslation[y.Key]));
			return list;
		}

		private List<KeyValuePair<Ores, MyFixedPoint>> GetTotalOres(List<KeyValuePair<Ingots, MyFixedPoint>> ingots)
		{
			var need = new Dictionary<Ores, MyFixedPoint>();
			foreach (Ores o in Enum.GetValues(typeof(Ores))) conversionData[o] = GetConversionData(o);
			foreach (var p in ingots)
				foreach (var o in ingotToOres[p.Key])
					AddCountToDict(need, o, p.Value * (1 / conversionData[o].conversionRate));
			var list = need.ToList();
			list.Sort((x, y) => string.Compare(oreTranslation[x.Key], oreTranslation[y.Key]));
			return list;
		}

		private struct ConversionData { public float conversionRate; public bool basicRefinery; }

		private ConversionData GetConversionData(Ores ore)
		{
			var refConv = Math.Min(1f, 1.0f * (float)conversionRates[ore] * (float)effectivenessMultiplier);
			var ret = new ConversionData { conversionRate = refConv, basicRefinery = false };
			if (basicRefineryOres.Contains(ore))
			{
				var arcConv = Math.Min(1f, 0.7f * (float)conversionRates[ore]);
				if ((arcConv > refConv && (atLeastOnebasicRefinery == atLeastOneRefinery)) || (atLeastOnebasicRefinery && !atLeastOneRefinery))
				{ ret.conversionRate = arcConv; ret.basicRefinery = true; }
			}
			return ret;
		}

		private double GetRefineryEffectiveness(IMyRefinery r)
		{
			string info = r.DetailedInfo;
			int start = info.IndexOf(effectivenessString) + effectivenessString.Length;
			string perc = info.Substring(start, info.IndexOf("%", start) - start).Trim();
			if (effectivenessMapping.ContainsKey(perc)) return effectivenessMapping[perc];

			perc = System.Text.RegularExpressions.Regex.Matches(info, @"\d+")[5].Value;
			return effectivenessMapping.ContainsKey(perc) ? effectivenessMapping[perc] : effectivenessMapping["100"];
		}

		private struct Size { public int Width, Height; }

		private Size GetOutputSize(string text)
		{
			var lines = text.Split('\n');
			int i = lines.Length - 1; while (i >= 0 && string.IsNullOrWhiteSpace(lines[i])) i--;
			var ret = new Size { Height = i + 1, Width = 0 };
			foreach (var line in lines) if (line.Length > ret.Width) ret.Width = line.Length;
			return ret;
		}

		private enum LCDType { NORMAL, WIDE, OTHER }

		private LCDType GetLCDType(IMyTextPanel lcd)
			=> smallLCDs.Contains(lcd.BlockDefinition.SubtypeName) ? LCDType.NORMAL
			 : wideLCDs.Contains(lcd.BlockDefinition.SubtypeName) ? LCDType.WIDE
			 : LCDType.OTHER;

		private LCDType CheckLCD(IMyTextPanel lcd)
		{
			if (lcd == null) return LCDType.OTHER;
			var t = GetLCDType(lcd);
			if (t == LCDType.OTHER) Echo($"Warning: {lcd.CustomName} is an unsupported type of text panel (too small).");
			return t;
		}

		private void ShowAndSetFontSize(IMyTextPanel lcd, string text)
		{
			if (lcd == null) return;
			lcd.ContentType = ContentType.TEXT_AND_IMAGE;
			lcd.WriteText(text);
			if (!autoResizeText) return;
			lcd.Font = monospaceFontName;

			var size = GetOutputSize(text);
			if (size.Width == 0) return;

			var type = GetLCDType(lcd);
			float maxWidth = (type == LCDType.WIDE ? wideLCDWidth : LCDWidth) * (1 - lcd.TextPadding * 0.02f);
			float maxHeight = (type == LCDType.WIDE ? wideLCDHeight : LCDHeight) * (1 - lcd.TextPadding * 0.02f);

			float fsW = maxWidth / size.Width, fsH = maxHeight / size.Height;
			lcd.FontSize = Math.Min(fsW, fsH);
		}

		/* VARIABLES TO SAVE */
		private string projectorName = "", lcdName1 = "", lcdName2 = "", lcdName3 = "";
		private bool lightArmor = true;
		private double effectivenessMultiplier = 1;
		private bool averageEffectivenesses = true;
		/* END OF VARIABLES TO SAVE */

		private IMyTextPanel lcd1, lcd2, lcd3;
		private readonly double log2 = Math.Log(2);
		private const float lcdSizeCorrection = 0.15f;
		private readonly string[] smallLCDs = new[] { "SmallTextPanel", "SmallLCDPanel", "LargeTextPanel", "LargeLCDPanel" };
		private readonly string[] wideLCDs = new[] { "SmallLCDPanelWide", "LargeLCDPanelWide" };
		private const float wideLCDWidth = 52.75f - lcdSizeCorrection, wideLCDHeight = 17.75f - lcdSizeCorrection, LCDWidth = wideLCDWidth / 2, LCDHeight = wideLCDHeight;
		private bool atLeastOneRefinery = false, atLeastOnebasicRefinery = false;
		private Dictionary<Ores, ConversionData> conversionData = new Dictionary<Ores, ConversionData>();

		public void Main(string argument, UpdateType updateReason)
		{
			if (updateReason != UpdateType.Update100 && !string.IsNullOrEmpty(argument))
			{
				try
				{
					var spl = argument.Split(';');
					projectorName = spl[0];
					if (spl.Length > 1) lcdName1 = spl[1];
					if (spl.Length > 2) lcdName2 = spl[2];
					if (spl.Length > 3) lcdName3 = spl[3];
					if (spl.Length > 4 && spl[4] != "") lightArmor = bool.Parse(spl[4]); else lightArmor = true;
					if (spl.Length > 5 && spl[5] != "")
					{ effectivenessMultiplier = Math.Pow(2, int.Parse(spl[5]) / 8d); averageEffectivenesses = false; }
					else { effectivenessMultiplier = 1; averageEffectivenesses = true; }
				}
				catch
				{
					Echo("Wrong argument(s). Format: [ProjectorName];[LCDName1];[LCDName2];[LCDName3];[lightArmor];[yieldPorts]. See Readme for more info.");
					Runtime.UpdateFrequency = UpdateFrequency.None; return;
				}
			}

			lcd1 = GridTerminalSystem.GetBlockWithName(lcdName1) as IMyTextPanel;
			lcd2 = GridTerminalSystem.GetBlockWithName(lcdName2) as IMyTextPanel;
			lcd3 = GridTerminalSystem.GetBlockWithName(lcdName3) as IMyTextPanel;

			if (lcd1 == null && lcd2 == null && lcd3 == null)
			{
				Echo("Error: at least one valid LCD Panel must be specified.");
				Runtime.UpdateFrequency = UpdateFrequency.None; return;
			}

			if (CheckLCD(lcd1) == LCDType.OTHER) lcd1 = null;
			if (CheckLCD(lcd2) == LCDType.OTHER) lcd2 = null;
			if (CheckLCD(lcd3) == LCDType.OTHER) lcd3 = null;

			Runtime.UpdateFrequency = UpdateFrequency.Update100;

			var projector = GridTerminalSystem.GetBlockWithName(projectorName) as IMyProjector;
			if (projector == null)
			{
				var projectors = new List<IMyProjector>();
				GridTerminalSystem.GetBlocksOfType(projectors, p => p.IsProjecting);
				if (projectors.Count > 0) projector = projectors[0];
				else { Echo(noProjectors + "."); WriteToAll(noProjectors); return; }
			}

			if (!projector.IsProjecting) { WriteToAll(projector.CustomName + notProjecting); return; }

			var cubeBlocks = new List<IMyCubeBlock>();
			GridTerminalSystem.GetBlocksOfType(cubeBlocks, b => b.CubeGrid == Me.CubeGrid || inventoryFromSubgrids);

			var componentAmounts = new Dictionary<string, MyFixedPoint>();
			var ingotAmounts = new Dictionary<Ingots, MyFixedPoint>();
			var oreAmounts = new Dictionary<Ores, MyFixedPoint>();
			bool moddedIngotsOres = false;

			foreach (var b in cubeBlocks)
			{
				if (!b.HasInventory) continue;
				for (int i = 0; i < b.InventoryCount; i++)
				{
					var items = new List<MyInventoryItem>();
					b.GetInventory(i).GetItems(items);
					foreach (var item in items)
					{
						if (item.Type.TypeId.Equals("MyObjectBuilder_Component"))
							AddCountToDict(componentAmounts, item.Type.SubtypeId, item.Amount);
						else if (item.Type.TypeId.Equals("MyObjectBuilder_Ingot"))
						{
							try { AddCountToDict(ingotAmounts, (Ingots)Enum.Parse(typeof(Ingots), item.Type.SubtypeId), item.Amount); }
							catch { moddedIngotsOres = true; }
						}
						else if (item.Type.TypeId.Equals("MyObjectBuilder_Ore"))
						{
							try { AddCountToDict(oreAmounts, (Ores)Enum.Parse(typeof(Ores), item.Type.SubtypeId), item.Amount); }
							catch { moddedIngotsOres = true; }
						}
					}
				}
			}
			if (moddedIngotsOres) Echo("WARNING: detected non-vanilla ores or ingots. Modded ores and ingots are ignored by this script.");

			Me.CustomData = "";

			var compList = GetTotalComponents(projector);
			var missingComponents = new List<KeyValuePair<string, int>>();
			string output = projector.CustomName + "\n" + (onlyShowNeeded ? lcd1TitleShort : lcd1Title).ToUpper() + "\n\n";
			foreach (var component in compList)
			{
				string subTypeId = component.Key.Replace("MyObjectBuilder_BlueprintDefinition/", "");
				var present = GetCountFromDic(componentAmounts, subTypeId.Replace("Component", ""));
				string name = componentTranslation[subTypeId];
				string amountStr = present.ToString(), neededStr = component.Value.ToString();
				var missing = component.Value - present;
				missingComponents.Add(new KeyValuePair<string, int>(component.Key, Math.Max(0, missing.ToIntSafe())));
				string missingStr = missing > 0 ? missing.ToString() : "";
				string warn = ">>", ok = "";

				if (lcd1 != null && lcd1.Font.Equals(monospaceFontName))
				{
					name = string.Format("{0,-" + maxComponentLength + "}", name);
					amountStr = FormatNumber(present, compWidth, 0);
					neededStr = FormatNumber(component.Value, compWidth, 0);
					missingStr = missing > 0 ? FormatNumber(missing, compWidth, 0) : new string(' ', compWidth);
					warn = ">> "; ok = "   ";
				}

				output += onlyShowNeeded
					? string.Format("{0}{1} {2}\n", (missing > 0 ? warn : ok), name, neededStr)
					: string.Format("{0}{1} {2}|{3}|{4}\n", (missing > 0 ? warn : ok), name, amountStr, neededStr, missingStr);
			}
			ShowAndSetFontSize(lcd1, output);
			Me.CustomData += output + "\n\n";

			var ingotsList = GetTotalIngots(missingComponents);
			var ingotsTotalNeeded = GetTotalIngots(compList);
			var missingIngots = new List<KeyValuePair<Ingots, MyFixedPoint>>();
			var output2 = projector.CustomName + "\n" + (onlyShowNeeded ? lcd2TitleShort : lcd2Title).ToUpper() + "\n\n";
			string decimalFmt = (ingotDecimals > 0 ? "." : "") + new string('0', ingotDecimals);

			for (int i = 0; i < ingotsList.Count; i++)
			{
				var ing = ingotsList[i];
				var present = GetCountFromDic(ingotAmounts, ing.Key);
				string name = ingotTranslation[ing.Key], sep = " | ", nf = "{0:0" + decimalFmt + "}";
				string amountStr = string.Format(nf, (decimal)present);
				string neededStr = string.Format(nf, (decimal)ing.Value);
				string totalNeededStr = string.Format(nf, (decimal)ingotsTotalNeeded[i].Value);
				var missing = ing.Value - present;
				missingIngots.Add(new KeyValuePair<Ingots, MyFixedPoint>(ing.Key, MyFixedPoint.Max(0, missing)));
				string missingStr = missing > 0 ? string.Format(nf, (decimal)missing) : "";
				string warn = ">>", ok = "";

				if (lcd2 != null && lcd2.Font.Equals(monospaceFontName))
				{
					name = string.Format("{0,-" + maxIngotLength + "}", name);
					sep = "|";
					amountStr = FormatNumber(present, ingotWidth, ingotDecimals);
					neededStr = FormatNumber(ing.Value, ingotWidth, ingotDecimals);
					totalNeededStr = FormatNumber(ingotsTotalNeeded[i].Value, ingotWidth, ingotDecimals);
					missingStr = missing > 0 ? FormatNumber(missing, ingotWidth, ingotDecimals) : new string(' ', ingotWidth);
					warn = ">> "; ok = "   ";
				}

				output2 += onlyShowNeeded
					? string.Format("{0}{1} {2}/{3}\n", (missing > 0 ? warn : ok), name, neededStr, totalNeededStr)
					: string.Format("{0}{1} {2}{3}{4}/{5}{3}{6}\n", (missing > 0 ? warn : ok), name, amountStr, sep, neededStr, totalNeededStr, missingStr);
			}
			ShowAndSetFontSize(lcd2, output2);
			Me.CustomData += output2 + "\n\n";

			var oresList = GetTotalOres(missingIngots);
			var oresTotalNeeded = GetTotalOres(ingotsTotalNeeded);

			string output3 = (lcd3 == null && fitOn2IfPossible)
				? "\n" + (onlyShowNeeded ? lcd3TitleShort : lcd3Title).ToUpper() + "\n\n"
				: projector.CustomName + "\n" + (onlyShowNeeded ? lcd3TitleShort : lcd3Title).ToUpper() + "\n\n";

			decimalFmt = (oreDecimals > 0 ? "." : "") + new string('0', oreDecimals);
			string scrapOutput = ""; bool arcPreferred = false;

			for (int i = 0; i < oresList.Count; i++)
			{
				var ores = oresList[i];
				var present = GetCountFromDic(oreAmounts, ores.Key);
				string name = oreTranslation[ores.Key] + (ores.Key == Ores.Scrap ? "*" : "");
				if (conversionData[ores.Key].basicRefinery) { name += "^"; arcPreferred = true; }
				string sep = " | ", nf = "{0:0" + decimalFmt + "}";
				string amountStr = string.Format(nf, (decimal)present);
				string neededStr = string.Format(nf, (decimal)ores.Value);
				string totalNeededStr = string.Format(nf, (decimal)oresTotalNeeded[i].Value);
				var missing = ores.Value - present;
				string missingStr = missing > 0 ? string.Format(nf, (decimal)missing) : "";
				string warn = ">>", ok = "";
				string na = "-", endNa = "";

				bool mono = (lcd3 != null && lcd3.Font.Equals(monospaceFontName)) || (lcd3 == null && fitOn2IfPossible && lcd2 != null && lcd2.Font.Equals(monospaceFontName));
				if (mono)
				{
					name = string.Format("{0,-" + maxOreLength + "}", name);
					sep = "|";
					amountStr = FormatNumber(present, oreWidth, oreDecimals);
					neededStr = FormatNumber(ores.Value, oreWidth, oreDecimals);
					totalNeededStr = FormatNumber(oresTotalNeeded[i].Value, oreWidth, oreDecimals);
					missingStr = missing > 0 ? FormatNumber(missing, oreWidth, oreDecimals) : new string(' ', oreWidth);
					warn = ">> "; ok = "   ";
					na = new string(' ', (oreWidth - 1) / 2) + "-" + new string(' ', oreWidth - 1 - (oreWidth - 1) / 2);
					endNa = new string(' ', oreWidth);
				}

				if (ores.Key == Ores.Scrap)
				{
					if (present > 0)
					{
						output3 += onlyShowNeeded
							? string.Format("{0}{1} {2}/{3}\n", ok, name, na, na)
							: string.Format("{0}{1} {2}{3}{4}/{5}{3}{6}\n", ok, name, amountStr, sep, na, na, endNa);
						var savedIron = present * conversionData[Ores.Scrap].conversionRate * (1f / conversionData[Ores.Iron].conversionRate);
						scrapOutput = "\n*" + string.Format(scrapMetalMessage, FormatNumber(present, oreWidth, oreDecimals).Trim(), oreTranslation[Ores.Scrap], FormatNumber(savedIron, oreWidth, oreDecimals).Trim(), oreTranslation[Ores.Iron]) + "\n";
					}
				}
				else
				{
					output3 += onlyShowNeeded
						? string.Format("{0}{1} {2}/{3}\n", (missing > 0 ? warn : ok), name, neededStr, totalNeededStr)
						: string.Format("{0}{1} {2}{3}{4}/{5}{3}{6}\n", (missing > 0 ? warn : ok), name, amountStr, sep, neededStr, totalNeededStr, missingStr);
				}
			}

			output3 += scrapOutput;
			var allRefineries = new List<IMyRefinery>();
			GridTerminalSystem.GetBlocksOfType(allRefineries, r => (r.CubeGrid == Me.CubeGrid || refineriesFromSubgrids) && r.Enabled);
			var refineries = new List<IMyRefinery>(); var basicRefinerys = new List<IMyRefinery>();
			foreach (var x in allRefineries) if (x.BlockDefinition.SubtypeName == "Blast Furnace") basicRefinerys.Add(x); else refineries.Add(x);
			atLeastOneRefinery = refineries.Count > 0; atLeastOnebasicRefinery = basicRefinerys.Count > 0;

			if (averageEffectivenesses)
			{
				if (refineries.Count == 0) effectivenessMultiplier = 1;
				else
				{
					double sumEff = 0;
					foreach (var r in refineries) sumEff += GetRefineryEffectiveness(r);
					effectivenessMultiplier = sumEff / refineries.Count;
				}
			}


			if (arcPreferred)
				output3 += (scrapOutput == "" ? "\n" : "") + basicRefineryEffUsed + (refineries.Count == 0 ? noRefineryFound : betterYield) + "\n";

			double avgPorts = 8 * Math.Log(effectivenessMultiplier) / log2;
			string avgPortsStr = averageEffectivenesses ? avgPorts.ToString("F1") : Math.Round(avgPorts).ToString();
			output3 += string.Format("\n" + refineryMessage + "\n",
				effectivenessMultiplier * 100,
				averageEffectivenesses ? "~" : "", avgPortsStr,
				averageEffectivenesses ? refineryMessageCauseAvg : refineryMessageCauseUser);

			if (lcd3 != null) ShowAndSetFontSize(lcd3, output3);
			else if (fitOn2IfPossible && lcd2 != null) ShowAndSetFontSize(lcd2, output2 + output3);
			Me.CustomData += output3 + "\n\n";
		}

		string blockDefinitionData = "SteelPlate*ConstructionComponent*LargeTube*MotorComponent*ComputerComponent*SmallTube*BulletproofGlass*InteriorPlate*GravityGeneratorComponent*MedicalComponent*Display*DetectorComponent*MetalGrid*RadioCommunicationComponent*Superconductor*GirderComponent*SolarCell*ExplosivesComponent*ZoneChip*PowerCell*ReactorComponent*EngineerPlushie*SabiroidPlushie*PrototechFrame*PrototechCapacitor*PrototechCircuitry*PrototechPanel*PrototechCoolingUnit*PrototechPropulsionUnit*PrototechMachinery*ThrustComponent$Drill*LargeBlockDrillReskin=0:300,1:40,2:12,3:5,4:5*SmallBlockDrillReskin=0:32,1:30,2:4,3:1,4:1*LargeBlockPrototechDrill=23:1,1:200,2:120,29:20,27:3,12:80,4:20,26:200*SmallBlockDrill=0:32,1:30,2:4,3:1,4:1*LargeBlockDrill=0:300,1:40,2:12,3:5,4:5$ShipGrinder*LargeShipGrinderReskin=0:20,1:30,2:1,3:4,4:2*SmallShipGrinderReskin=0:12,1:17,5:4,3:4,4:2*LargeShipGrinder=0:20,1:30,2:1,3:4,4:2*SmallShipGrinder=0:12,1:17,5:4,3:4,4:2$ShipWelder*LargeShipWelderReskin=0:20,1:30,2:1,3:2,4:2*SmallShipWelderReskin=0:12,1:17,5:6,3:2,4:2*LargeShipWelder=0:20,1:30,2:1,3:2,4:2*SmallShipWelder=0:12,1:17,5:6,3:2,4:2$FunctionalBlock*LargeBlockAlgaeFarmReskin=0:30,6:60,5:30,1:30,4:10*LargeBlockConduitDamaged=1:10,5:20*LargeBlockAlgaeFarm=0:30,6:60,5:30,1:30,4:10*LargeBlockFarmPlot=7:40,1:40,5:10$OxygenFarm*LargeBlockOxygenFarmReskin=0:30,6:60,5:30,1:30,4:10*LargeBlockOxygenFarm=0:30,6:60,5:30,1:30,4:10$InteriorLight*LargeBlockInsetTerrariumDesert=7:30,1:30,3:1,5:10,8:1,6:10*LargeBlockInsetTerrariumForest=7:30,1:30,3:1,5:10,8:1,6:10*LargeInsetPlanter=7:30,1:30,3:2,5:10,6:30*LargeBlockConduitLight=1:15,5:20*LargeBlockInsetAquarium=7:30,1:30,3:1,5:10,8:1,6:10*LargeBlockInsetKitchen=7:30,1:30,2:8,3:6,6:6*CorridorRoundLight=7:100,1:30*LabEquipment2=7:40,1:50,3:4,2:4,6:100*LargeBlockInsetLight=0:10,1:10,7:20*SmallBlockInsetLight=0:1,1:2,7:1*AirDuctLight=7:20,1:30,2:4,0:10*SmallLight=1:2*SmallBlockSmallLight=1:2*LargeBlockLight_1corner=1:3*LargeBlockLight_2corner=1:6*SmallBlockLight_1corner=1:2*SmallBlockLight_2corner=1:4*OffsetLight=1:2*LargeBlockInsetWallLight=7:25,1:10*CorridorLight=7:100,1:30*CorridorNarrowStowage=7:100,1:30*TrussPillarLight=7:12,1:8,5:2*TrussPillarLightSmall=7:1,1:2,5:1*PassageSciFiLight=7:74,1:20,5:48*LargeLightPanel=1:10,7:5*SmallLightPanel=1:2,7:1$SurvivalKit*SurvivalKitLargeReskin=0:30,1:2,9:3,3:4,10:1,4:5*SurvivalKitSmallReskin=0:6,1:2,9:3,3:4,10:1,4:5*SurvivalKitLarge=0:30,1:2,9:3,3:4,10:1,4:5*SurvivalKit=0:6,1:2,9:3,3:4,10:1,4:5$CubeBlock*LargeBlockConduitBoxes=1:20,5:20*LargeBlockConduitCorner=1:6,5:10*LargeBlockConduitDown=1:2,5:1*LargeBlockConduitUp=1:20,5:40*LargeBlockConduitJunction=1:15,5:20*LargeBlockConduitJunctionCorner=1:20,5:40*LargeBlockConduitStraight=1:10,5:20*LargeBlockConduitTransition=1:10,5:30*LargeBlockConduitTransitionInv=1:10,5:30*LargeStorageBin1=7:5,1:5*LargeStorageBin2=7:15,1:15*LargeStorageBin3=7:40,1:40*LargeWarningSign14=1:4,7:4*LargeWarningSign15=1:4,7:4*LargeWarningSign16=1:4,7:2*SmallWarningSign14=1:2,7:1*SmallWarningSign15=1:2,7:1*SmallWarningSign16=1:4,7:2*LargeBlockArmorBlock=0:25*LargeBlockArmorSlope=0:13*LargeBlockArmorCorner=0:4*LargeBlockArmorCornerInv=0:21*LargeRoundArmor_Slope=0:13*LargeRoundArmor_Corner=0:4*LargeRoundArmor_CornerInv=0:21*LargeHeavyBlockArmorBlock=0:150,12:50*LargeHeavyBlockArmorSlope=0:75,12:25*LargeHeavyBlockArmorCorner=0:25,12:10*LargeHeavyBlockArmorCornerInv=0:125,12:50*SmallBlockArmorBlock=0:1*SmallBlockArmorSlope=0:1*SmallBlockArmorCorner=0:1*SmallBlockArmorCornerInv=0:1*SmallHeavyBlockArmorBlock=0:5,12:2*SmallHeavyBlockArmorSlope=0:3,12:1*SmallHeavyBlockArmorCorner=0:2,12:1*SmallHeavyBlockArmorCornerInv=0:4,12:1*LargeHalfArmorBlock=0:12*LargeHeavyHalfArmorBlock=0:75,12:25*LargeHalfSlopeArmorBlock=0:4*LargeHeavyHalfSlopeArmorBlock=0:19,12:6*HalfArmorBlock=0:1*HeavyHalfArmorBlock=0:3,12:1*HalfSlopeArmorBlock=0:1*HeavyHalfSlopeArmorBlock=0:2,12:1*LargeBlockArmorRoundSlope=0:13*LargeBlockArmorRoundCorner=0:4*LargeBlockArmorRoundCornerInv=0:21*LargeHeavyBlockArmorRoundSlope=0:130,12:50*LargeHeavyBlockArmorRoundCorner=0:125,12:40*LargeHeavyBlockArmorRoundCornerInv=0:140,12:50*SmallBlockArmorRoundSlope=0:1*SmallBlockArmorRoundCorner=0:1*SmallBlockArmorRoundCornerInv=0:1*SmallHeavyBlockArmorRoundSlope=0:4,12:1*SmallHeavyBlockArmorRoundCorner=0:4,12:1*SmallHeavyBlockArmorRoundCornerInv=0:5,12:1*LargeBlockArmorSlope2Base=0:19*LargeBlockArmorSlope2Tip=0:6*LargeBlockArmorCorner2Base=0:10*LargeBlockArmorCorner2Tip=0:3*LargeBlockArmorInvCorner2Base=0:22*LargeBlockArmorInvCorner2Tip=0:16*LargeHeavyBlockArmorSlope2Base=0:112,12:40*LargeHeavyBlockArmorSlope2Tip=0:40,12:12*LargeHeavyBlockArmorCorner2Base=0:55,12:15*LargeHeavyBlockArmorCorner2Tip=0:19,12:6*LargeHeavyBlockArmorInvCorner2Base=0:133,12:45*LargeHeavyBlockArmorInvCorner2Tip=0:94,12:25*SmallBlockArmorSlope2Base=0:1*SmallBlockArmorSlope2Tip=0:1*SmallBlockArmorCorner2Base=0:1*SmallBlockArmorCorner2Tip=0:1*SmallBlockArmorInvCorner2Base=0:1*SmallBlockArmorInvCorner2Tip=0:1*SmallHeavyBlockArmorSlope2Base=0:4,12:2*SmallHeavyBlockArmorSlope2Tip=0:2,12:1*SmallHeavyBlockArmorCorner2Base=0:3,12:1*SmallHeavyBlockArmorCorner2Tip=0:2,12:1*SmallHeavyBlockArmorInvCorner2Base=0:5,12:1*SmallHeavyBlockArmorInvCorner2Tip=0:5,12:1*LargeArmorPanelLight=0:5*LargeArmorCenterPanelLight=0:5*LargeArmorSlopedSidePanelLight=0:3*LargeArmorSlopedPanelLight=0:6*LargeArmorHalfPanelLight=0:3*LargeArmorHalfCenterPanelLight=0:3*LargeArmorQuarterPanelLight=0:2*LargeArmor2x1SlopedPanelLight=0:5*LargeArmor2x1SlopedPanelTipLight=0:5*LargeArmor2x1SlopedSideBasePanelLight=0:5*LargeArmor2x1SlopedSideTipPanelLight=0:3*LargeArmor2x1SlopedSideBasePanelLightInv=0:5*LargeArmor2x1SlopedSideTipPanelLightInv=0:3*LargeArmorHalfSlopedPanelLight=0:4*LargeArmor2x1HalfSlopedPanelLightRight=0:3*LargeArmor2x1HalfSlopedTipPanelLightRight=0:3*LargeArmor2x1HalfSlopedPanelLightLeft=0:3*LargeArmor2x1HalfSlopedTipPanelLightLeft=0:3*LargeRoundArmorPanelLight=0:8*LargeRoundArmorPanelCornerLight=0:5*LargeRoundArmorPanelFaceLight=0:4*LargeRoundArmorPanelInvertedCornerLight=0:5*LargeArmorPanelHeavy=0:15,12:5*LargeArmorCenterPanelHeavy=0:15,12:5*LargeArmorSlopedSidePanelHeavy=0:8,12:3*LargeArmorSlopedPanelHeavy=0:21,12:7*LargeArmorHalfPanelHeavy=0:8,12:3*LargeArmorHalfCenterPanelHeavy=0:8,12:3*LargeArmorQuarterPanelHeavy=0:5,12:2*LargeArmor2x1SlopedPanelHeavy=0:18,12:6*LargeArmor2x1SlopedPanelTipHeavy=0:18,12:6*LargeArmor2x1SlopedSideBasePanelHeavy=0:12,12:4*LargeArmor2x1SlopedSideTipPanelHeavy=0:6,12:2*LargeArmor2x1SlopedSideBasePanelHeavyInv=0:12,12:4*LargeArmor2x1SlopedSideTipPanelHeavyInv=0:6,12:2*LargeArmorHalfSlopedPanelHeavy=0:9,12:3*LargeArmor2x1HalfSlopedPanelHeavyRight=0:9,12:3*LargeArmor2x1HalfSlopedTipPanelHeavyRight=0:9,12:3*LargeArmor2x1HalfSlopedPanelHeavyLeft=0:9,12:3*LargeArmor2x1HalfSlopedTipPanelHeavyLeft=0:9,12:3*LargeRoundArmorPanelHeavy=0:24,12:8*LargeRoundArmorPanelCornerHeavy=0:15,12:5*LargeRoundArmorPanelFaceHeavy=0:12,12:4*LargeRoundArmorPanelInvertedCornerHeavy=0:15,12:5*SmallArmorPanelLight=0:1*SmallArmorCenterPanelLight=0:1*SmallArmorSlopedSidePanelLight=0:1*SmallArmorSlopedPanelLight=0:1*SmallArmorHalfPanelLight=0:1*SmallArmorHalfCenterPanelLight=0:1*SmallArmorQuarterPanelLight=0:1*SmallArmor2x1SlopedPanelLight=0:1*SmallArmor2x1SlopedPanelTipLight=0:1*SmallArmor2x1SlopedSideBasePanelLight=0:1*SmallArmor2x1SlopedSideTipPanelLight=0:1*SmallArmor2x1SlopedSideBasePanelLightInv=0:1*SmallArmor2x1SlopedSideTipPanelLightInv=0:1*SmallArmorHalfSlopedPanelLight=0:1*SmallArmor2x1HalfSlopedPanelLightRight=0:1*SmallArmor2x1HalfSlopedTipPanelLightRight=0:1*SmallArmor2x1HalfSlopedPanelLightLeft=0:1*SmallArmor2x1HalfSlopedTipPanelLightLeft=0:1*SmallRoundArmorPanelLight=0:1*SmallRoundArmorPanelCornerLight=0:1*SmallRoundArmorPanelFaceLight=0:1*SmallRoundArmorPanelInvertedCornerLight=0:1*SmallArmorPanelHeavy=0:3,12:1*SmallArmorCenterPanelHeavy=0:3,12:1*SmallArmorSlopedSidePanelHeavy=0:2,12:1*SmallArmorSlopedPanelHeavy=0:3,12:1*SmallArmorHalfPanelHeavy=0:2,12:1*SmallArmorHalfCenterPanelHeavy=0:2,12:1*SmallArmorQuarterPanelHeavy=0:2,12:1*SmallArmor2x1SlopedPanelHeavy=0:3,12:1*SmallArmor2x1SlopedPanelTipHeavy=0:3,12:1*SmallArmor2x1SlopedSideBasePanelHeavy=0:3,12:1*SmallArmor2x1SlopedSideTipPanelHeavy=0:2,12:1*SmallArmor2x1SlopedSideBasePanelHeavyInv=0:3,12:1*SmallArmor2x1SlopedSideTipPanelHeavyInv=0:2,12:1*SmallArmorHalfSlopedPanelHeavy=0:2,12:1*SmallArmor2x1HalfSlopedPanelHeavyRight=0:2,12:1*SmallArmor2x1HalfSlopedTipPanelHeavyRight=0:2,12:1*SmallArmor2x1HalfSlopedPanelHeavyLeft=0:2,12:1*SmallArmor2x1HalfSlopedTipPanelHeavyLeft=0:2,12:1*SmallRoundArmorPanelHeavy=0:3,12:1*SmallRoundArmorPanelCornerHeavy=0:2,12:1*SmallRoundArmorPanelFaceHeavy=0:3,12:1*SmallRoundArmorPanelInvertedCornerHeavy=0:3,12:1*LargeBlockArmorCornerSquare=0:7*SmallBlockArmorCornerSquare=0:1*LargeBlockHeavyArmorCornerSquare=0:40,12:15*SmallBlockHeavyArmorCornerSquare=0:2,12:1*LargeBlockArmorCornerSquareInverted=0:19*SmallBlockArmorCornerSquareInverted=0:1*LargeBlockHeavyArmorCornerSquareInverted=0:112,12:40*SmallBlockHeavyArmorCornerSquareInverted=0:4,12:1*LargeBlockArmorHalfCorner=0:6*SmallBlockArmorHalfCorner=0:1*LargeBlockHeavyArmorHalfCorner=0:40,12:12*SmallBlockHeavyArmorHalfCorner=0:2,12:1*LargeBlockArmorHalfSlopeCorner=0:2*SmallBlockArmorHalfSlopeCorner=0:1*LargeBlockHeavyArmorHalfSlopeCorner=0:12,12:4*SmallBlockHeavyArmorHalfSlopeCorner=0:2,12:1*LargeBlockArmorHalfSlopeCornerInverted=0:23*SmallBlockArmorHalfSlopeCornerInverted=0:1*LargeBlockHeavyArmorHalfSlopeCornerInverted=0:139,12:45*SmallBlockHeavyArmorHalfSlopeCornerInverted=0:5,12:1*LargeBlockArmorHalfSlopedCorner=0:11*SmallBlockArmorHalfSlopedCorner=0:1*LargeBlockHeavyArmorHalfSlopedCorner=0:45,12:5*SmallBlockHeavyArmorHalfSlopedCorner=0:3,12:1*LargeBlockArmorHalfSlopedCornerBase=0:11*SmallBlockArmorHalfSlopedCornerBase=0:1*LargeBlockHeavyArmorHalfSlopedCornerBase=0:45,12:15*SmallBlockHeavyArmorHalfSlopedCornerBase=0:3,12:1*LargeBlockArmorHalfSlopeInverted=0:22*SmallBlockArmorHalfSlopeInverted=0:1*LargeBlockHeavyArmorHalfSlopeInverted=0:133,12:45*SmallBlockHeavyArmorHalfSlopeInverted=0:5,12:1*LargeBlockArmorSlopedCorner=0:13*SmallBlockArmorSlopedCorner=0:1*LargeBlockHeavyArmorSlopedCorner=0:75,12:25*SmallBlockHeavyArmorSlopedCorner=0:3,12:1*LargeBlockArmorSlopedCornerBase=0:20*SmallBlockArmorSlopedCornerBase=0:1*LargeBlockHeavyArmorSlopedCornerBase=0:127,12:40*SmallBlockHeavyArmorSlopedCornerBase=0:5,12:1*LargeBlockArmorSlopedCornerTip=0:5*SmallBlockArmorSlopedCornerTip=0:1*LargeBlockHeavyArmorSlopedCornerTip=0:23,12:6*SmallBlockHeavyArmorSlopedCornerTip=0:2,12:1*LargeBlockArmorRaisedSlopedCorner=0:17*SmallBlockArmorRaisedSlopedCorner=0:1*LargeBlockHeavyArmorRaisedSlopedCorner=0:100,12:30*SmallBlockHeavyArmorRaisedSlopedCorner=0:4,12:2*LargeBlockArmorSlopeTransition=0:10*SmallBlockArmorSlopeTransition=0:1*LargeBlockHeavyArmorSlopeTransition=0:60,12:18*SmallBlockHeavyArmorSlopeTransition=0:3,12:1*LargeBlockArmorSlopeTransitionBase=0:16*SmallBlockArmorSlopeTransitionBase=0:1*LargeBlockHeavyArmorSlopeTransitionBase=0:95,12:35*SmallBlockHeavyArmorSlopeTransitionBase=0:4,12:2*LargeBlockArmorSlopeTransitionBaseMirrored=0:16*SmallBlockArmorSlopeTransitionBaseMirrored=0:1*LargeBlockHeavyArmorSlopeTransitionBaseMirrored=0:95,12:35*SmallBlockHeavyArmorSlopeTransitionBaseMirrored=0:4,12:2*LargeBlockArmorSlopeTransitionMirrored=0:10*SmallBlockArmorSlopeTransitionMirrored=0:1*LargeBlockHeavyArmorSlopeTransitionMirrored=0:60,12:18*SmallBlockHeavyArmorSlopeTransitionMirrored=0:3,12:1*LargeBlockArmorSlopeTransitionTip=0:5*SmallBlockArmorSlopeTransitionTip=0:1*LargeBlockHeavyArmorSlopeTransitionTip=0:30,12:9*SmallBlockHeavyArmorSlopeTransitionTip=0:2,12:1*LargeBlockArmorSlopeTransitionTipMirrored=0:5*SmallBlockArmorSlopeTransitionTipMirrored=0:1*LargeBlockHeavyArmorSlopeTransitionTipMirrored=0:30,12:9*SmallBlockHeavyArmorSlopeTransitionTipMirrored=0:2,12:1*LargeBlockArmorSquareSlopedCornerBase=0:18*SmallBlockArmorSquareSlopedCornerBase=0:1*LargeBlockHeavyArmorSquareSlopedCornerBase=0:105,12:35*SmallBlockHeavyArmorSquareSlopedCornerBase=0:4,12:2*LargeBlockArmorSquareSlopedCornerTip=0:6*SmallBlockArmorSquareSlopedCornerTip=0:1*LargeBlockHeavyArmorSquareSlopedCornerTip=0:30,12:9*SmallBlockHeavyArmorSquareSlopedCornerTip=0:2,12:1*LargeBlockArmorSquareSlopedCornerTipInv=0:9*SmallBlockArmorSquareSlopedCornerTipInv=0:1*LargeBlockHeavyArmorSquareSlopedCornerTipInv=0:55,12:18*SmallBlockHeavyArmorSquareSlopedCornerTipInv=0:3,12:1*LargeBlockModularBridgeCorner=15:5,0:20,6:20*LargeBlockModularBridgeCornerFloorless=15:5,0:20,6:20*LargeBlockModularBridgeRaisedSlopedCorner=15:5,0:24,6:20*LargeBlockModularBridgeRaisedSlopedCornerFloorless=15:5,0:24,6:20*LargeBlockModularBridgeHalfSlopedCorner=15:5,0:16,6:20*LargeBlockModularBridgeHalfSlopedCornerFloorless=15:5,0:16,6:20*LargeBlockModularBridgeCorner2x1BaseL=15:4,0:10,6:10*LargeBlockModularBridgeCorner2x1BaseLFloorless=15:4,0:10,6:10*LargeBlockModularBridgeCorner2x1BaseR=15:4,0:10,6:10*LargeBlockModularBridgeCorner2x1BaseRFloorless=15:4,0:10,6:10*LargeBlockModularBridgeEmpty=15:6,0:10,6:30*LargeBlockModularBridgeFloor=15:6,0:10,6:30*LargeBlockModularBridgeSideL=15:4,0:8,6:10*LargeBlockModularBridgeSideR=15:4,0:8,6:10*LargeBlockModularBridgeSlopedCornerBase=15:5,0:20,6:20*LargeBlockModularBridgeSlopedCornerBaseFloorless=15:5,0:20,6:20*SmallBlockKitchenSink=7:4,1:6,5:2*SmallBlockKitchenCoffeeMachine=7:4,1:6,3:1*LargeBlockDeskChairless=7:30,1:30*LargeBlockDeskChairlessCorner=7:20,1:20*LargeBlockDeskChairlessCornerInv=7:60,1:60*Shower=7:20,1:20,5:12,6:8*WindowWall=0:8,1:10,6:10*WindowWallLeft=0:10,1:10,6:8*WindowWallRight=0:10,1:10,6:8*Catwalk=1:16,15:4,5:20*CatwalkCorner=1:24,15:4,5:32*CatwalkStraight=1:24,15:4,5:32*CatwalkWall=1:20,15:4,5:26*CatwalkRailingEnd=1:28,15:4,5:38*CatwalkRailingHalfRight=1:28,15:4,5:36*CatwalkRailingHalfLeft=1:28,15:4,5:36*CatwalkHalf=1:10,15:2,5:10*CatwalkHalfRailing=1:18,15:2,5:22*CatwalkHalfCenterRailing=1:14,15:2,5:16*CatwalkHalfOuterRailing=1:14,15:2,5:16*GratedStairs=1:22,5:12,7:16*GratedHalfStairs=1:20,5:6,7:8*GratedHalfStairsMirrored=1:20,5:6,7:8*RailingStraight=1:8,5:6*RailingDouble=1:16,5:12*RailingCorner=1:16,5:12*RailingDiagonal=1:12,5:9*RailingHalfRight=1:8,5:4*RailingHalfLeft=1:8,5:4*RailingCenter=1:8,5:6*Railing2x1Right=1:10,5:7*Railing2x1Left=1:10,5:7*Freight1=7:6,1:8*Freight2=7:12,1:16*Freight3=7:18,1:24*Truss=15:20,5:10*TrussSmall=15:2,5:1*TrussFrame=15:10,5:5*TrussSlopedFrame=15:6,5:3*TrussSloped=15:10,5:5*TrussSlopedSmall=15:1,5:1*TrussAngled=15:20,5:10*TrussAngledSmall=15:2,5:1*TrussHalf=15:10,5:5*TrussHalfSmall=15:1,5:1*TrussFloor=15:24,1:16,5:30*TrussFloorT=15:22,1:16,5:30*TrussFloorX=15:20,1:16,5:30*TrussFloorAngled=15:24,1:16,5:30*TrussFloorAngledInverted=15:24,1:16,5:30*TrussFloorHalf=15:12,1:14,5:20*LargeBarrel=0:5,2:1,1:6*SmallBarrel=0:5,2:1,1:6*LargeBarrelThree=0:15,2:3,1:18*LargeBarrelStack=0:50,2:10,12:4,15:8,1:60*ArmorCenter=0:140*ArmorCorner=0:120*ArmorInvCorner=0:135*ArmorSide=0:130*SmallArmorCenter=0:5*SmallArmorCorner=0:5*SmallArmorInvCorner=0:5*SmallArmorSide=0:5*LargeRailStraight=0:12,1:8,2:4*Monolith=0:130,14:130*Stereolith=0:130,14:130*DeadAstronaut=0:13,14:13*LargeDeadAstronaut=0:13,14:13*EngineerPlushie=21:1*SabiroidPlushie=22:1*LargeWarningSignEaster2=1:4,7:6*SmallWarningSignEaster2=1:4,7:6*LargeWarningSignEaster3=1:4,7:6*SmallWarningSignEaster3=1:4,7:6*LargeWarningSignEaster9=1:4,7:4*SmallWarningSignEaster9=1:2,7:1*LargeWarningSignEaster10=1:4,7:4*SmallWarningSignEaster10=1:2,7:1*LargeWarningSignEaster11=1:4,7:4*SmallWarningSignEaster11=1:2,7:1*LargeWarningSignEaster13=1:4,7:4*SmallWarningSignEaster13=1:2,7:1*LargeBlockStatueEngineer=15:30,0:60,1:30*CorridorRound=7:100,1:30*CorridorRoundCorner=7:100,1:30*CorridorRoundT=7:70,1:25*CorridorRoundX=7:40,1:20*CorridorRoundTransition=7:100,1:30*LargeBlockFloorCenter=7:10,0:20,1:10*LargeBlockFloorCenterMirrored=7:10,0:20,1:10*LargeBlockFloorEdge=7:10,0:20,1:10*LargeBlockFloorEdgeMirrored=7:10,0:20,1:10*LargeBlockFloorPassage=7:10,0:20,1:10*LargeBlockFloorPassageMirrored=7:10,0:20,1:10*LargeBlockFloorDecal=7:10,0:20,1:10*LargeBlockFloorDecalMirrored=7:10,0:20,1:10*LargeBlockFloorSlab=7:80,0:160,1:80*SmallBlockFloorCenter=7:1,0:2,1:1*SmallBlockFloorCenterMirrored=7:1,0:2,1:1*SmallBlockFloorSlab=7:8,0:16,1:8*LargeBlockLabDesk=7:30,1:30,3:2,11:4*LargeBlockLabSink=7:30,1:30,3:2*LargeBlockPipesStraight1=0:5,1:20,2:12*LargeBlockPipesStraight2=0:10,1:20,2:12*LargeBlockPipesEnd=0:5,1:20,2:12*LargeBlockPipesJunction=0:10,1:30,2:14*LargeBlockPipesCornerOuter=0:1,1:10,2:6*LargeBlockPipesCorner=0:5,1:20,2:12*LargeBlockPipesCornerInner=0:10,1:30,2:18*DeadBody01=6:1,13:1,10:1*DeadBody02=6:1,13:1,10:1*DeadBody03=6:1,13:1,10:1*DeadBody04=6:1,13:1,10:1*DeadBody05=6:1,13:1,10:1*DeadBody06=6:1,13:1,10:1*AngledInteriorWallA=7:25,1:10*AngledInteriorWallB=7:25,1:10*PipeWorkBlockA=7:20,1:20,2:10*PipeWorkBlockB=7:20,1:20,2:10*LargeWarningSign1=1:4,7:6*LargeWarningSign2=1:4,7:6*LargeWarningSign3=1:4,7:6*LargeWarningSign4=1:4,7:2*LargeWarningSign5=1:4,7:6*LargeWarningSign6=1:4,7:6*LargeWarningSign7=1:4,7:2*LargeWarningSign8=1:4,7:4*LargeWarningSign9=1:4,7:4*LargeWarningSign10=1:4,7:4*LargeWarningSign11=1:4,7:4*LargeWarningSign12=1:4,7:4*LargeWarningSign13=1:4,7:4*SmallWarningSign1=1:4,7:6*SmallWarningSign2=1:4,7:2*SmallWarningSign3=1:4,7:6*SmallWarningSign4=1:4,7:2*SmallWarningSign5=1:4,7:6*SmallWarningSign6=1:4,7:2*SmallWarningSign7=1:4,7:2*SmallWarningSign8=1:2,7:1*SmallWarningSign9=1:2,7:1*SmallWarningSign10=1:2,7:1*SmallWarningSign11=1:2,7:1*SmallWarningSign12=1:2,7:1*SmallWarningSign13=1:2,7:1*LargeBlockConveyorPipeCap=7:10,1:10*LargeBlockCylindricalColumn=7:25,1:10*SmallBlockCylindricalColumn=7:5,1:3*LargeGridBeamBlock=0:25*LargeGridBeamBlockSlope=0:13*LargeGridBeamBlockRound=0:13*LargeGridBeamBlockSlope2x1Base=0:19*LargeGridBeamBlockSlope2x1Tip=0:7*LargeGridBeamBlockHalf=0:12*LargeGridBeamBlockHalfSlope=0:7*LargeGridBeamBlockEnd=0:25*LargeGridBeamBlockJunction=0:25*LargeGridBeamBlockTJunction=0:25*SmallGridBeamBlock=0:1*SmallGridBeamBlockSlope=0:1*SmallGridBeamBlockRound=0:1*SmallGridBeamBlockSlope2x1Base=0:1*SmallGridBeamBlockSlope2x1Tip=0:1*SmallGridBeamBlockHalf=0:1*SmallGridBeamBlockHalfSlope=0:1*SmallGridBeamBlockEnd=0:1*SmallGridBeamBlockJunction=0:1*SmallGridBeamBlockTJunction=0:1*Passage2=7:74,1:20,5:48*Passage2Wall=7:50,1:14,5:32*LargeStairs=7:50,1:30*LargeRamp=7:70,1:16*LargeSteelCatwalk=7:27,1:5,5:20*LargeSteelCatwalk2Sides=7:32,1:7,5:25*LargeSteelCatwalkCorner=7:32,1:7,5:25*LargeSteelCatwalkPlate=7:23,1:7,5:17*LargeCoverWall=0:4,1:10*LargeCoverWallHalf=0:2,1:6*LargeCoverWallHalfMirrored=0:2,1:6*LargeBlockInteriorWall=7:25,1:10*LargeInteriorPillar=7:25,1:10,5:4*AirDuct1=7:20,1:30,2:4,0:10*AirDuct2=7:20,1:30,2:4,0:10*AirDuctCorner=7:20,1:30,2:4,0:10*AirDuctT=7:15,1:30,2:4,0:8*AirDuctX=7:10,1:30,2:4,0:5*AirDuctRamp=7:20,1:30,2:4,0:10*AirDuctGrate=1:10,7:10*LargeBlockConveyorCap=7:10,1:10*SmallBlockConveyorCapMedium=7:10,1:10*SmallBlockConveyorCap=7:2,1:2*Viewport1=0:10,1:10,6:8*Viewport2=0:10,1:10,6:8*BarredWindow=15:1,1:4*BarredWindowSlope=15:1,1:4*BarredWindowSide=15:1,1:4*BarredWindowFace=15:1,1:4*StorageShelf1=15:10,0:50,1:50,5:50,7:50*StorageShelf2=15:30,19:20,3:20,12:20*StorageShelf3=15:10,20:10,14:10,30:10,8:2*LargeBlockInsetWall=7:25,1:10*LargeBlockInsetWallPillar=7:25,1:10*LargeBlockInsetWallCorner=7:25,1:10*LargeBlockInsetWallCornerInverted=7:25,1:10*LargeBlockInsetWallSlope=7:19,1:7*LargeBlockConsoleModule=7:50,1:50*LargeBlockConsoleModuleCorner=7:30,1:30*SmallBlockConsoleModule=7:6,1:6*SmallBlockConsoleModuleCorner=7:3,1:3*SmallBlockConsoleModuleInvertedCorner=7:3,1:3*ExtendedWindow=15:10,6:25*ExtendedWindowRailing=15:10,6:25*ExtendedWindowCorner=15:4,6:10*ExtendedWindowCornerInverted=15:15,6:50*ExtendedWindowCornerInvertedRailing=15:15,6:50*ExtendedWindowDiagonal=15:12,6:35*ExtendedWindowDiagonalRailing=15:12,6:35*ExtendedWindowEnd=15:10,6:25*ExtendedWindowDome=15:10,6:25*SmallBlockExtendedWindow=15:1,6:3*SmallBlockExtendedWindowCorner=15:1,6:2*SmallBlockExtendedWindowCornerInverted=15:2,6:6*SmallBlockExtendedWindowDiagonal=15:1,6:4*SmallBlockExtendedWindowEnd=15:1,6:3*SmallBlockExtendedWindowDome=15:2,6:6*Corridor=7:100,1:30*CorridorCorner=7:100,1:30*CorridorT=7:70,1:25*CorridorX=7:40,1:20*CorridorWindow=7:80,1:30,6:6*CorridorDoubleWindow=7:65,1:25,6:12*CorridorWindowRoof=7:80,1:30,6:6*CorridorNarrow=7:100,1:30*TrussPillar=7:25,1:10,5:4*TrussPillarCorner=7:25,1:10,5:4*TrussPillarSlanted=7:25,1:10,5:4*TrussPillarT=7:30,1:12,5:6*TrussPillarX=7:35,1:15,5:8*TrussPillarDiagonal=7:30,1:12,5:6*TrussPillarSmall=7:25,1:10,5:4*TrussPillarOffset=7:25,1:10,5:4*LargeBlockSciFiWall=7:25,1:10*LargeBlockBarCounter=7:16,1:10,3:1,6:6*LargeBlockBarCounterCorner=7:24,1:14,3:2,6:10*LargeSymbolA=0:4*LargeSymbolB=0:4*LargeSymbolC=0:4*LargeSymbolD=0:4*LargeSymbolE=0:4*LargeSymbolF=0:4*LargeSymbolG=0:4*LargeSymbolH=0:4*LargeSymbolI=0:4*LargeSymbolJ=0:4*LargeSymbolK=0:4*LargeSymbolL=0:4*LargeSymbolM=0:4*LargeSymbolN=0:4*LargeSymbolO=0:4*LargeSymbolP=0:4*LargeSymbolQ=0:4*LargeSymbolR=0:4*LargeSymbolS=0:4*LargeSymbolT=0:4*LargeSymbolU=0:4*LargeSymbolV=0:4*LargeSymbolW=0:4*LargeSymbolX=0:4*LargeSymbolY=0:4*LargeSymbolZ=0:4*SmallSymbolA=0:1*SmallSymbolB=0:1*SmallSymbolC=0:1*SmallSymbolD=0:1*SmallSymbolE=0:1*SmallSymbolF=0:1*SmallSymbolG=0:1*SmallSymbolH=0:1*SmallSymbolI=0:1*SmallSymbolJ=0:1*SmallSymbolK=0:1*SmallSymbolL=0:1*SmallSymbolM=0:1*SmallSymbolN=0:1*SmallSymbolO=0:1*SmallSymbolP=0:1*SmallSymbolQ=0:1*SmallSymbolR=0:1*SmallSymbolS=0:1*SmallSymbolT=0:1*SmallSymbolU=0:1*SmallSymbolV=0:1*SmallSymbolW=0:1*SmallSymbolX=0:1*SmallSymbolY=0:1*SmallSymbolZ=0:1*LargeSymbol0=0:4*LargeSymbol1=0:4*LargeSymbol2=0:4*LargeSymbol3=0:4*LargeSymbol4=0:4*LargeSymbol5=0:4*LargeSymbol6=0:4*LargeSymbol7=0:4*LargeSymbol8=0:4*LargeSymbol9=0:4*SmallSymbol0=0:1*SmallSymbol1=0:1*SmallSymbol2=0:1*SmallSymbol3=0:1*SmallSymbol4=0:1*SmallSymbol5=0:1*SmallSymbol6=0:1*SmallSymbol7=0:1*SmallSymbol8=0:1*SmallSymbol9=0:1*LargeSymbolHyphen=0:4*LargeSymbolUnderscore=0:4*LargeSymbolDot=0:4*LargeSymbolApostrophe=0:4*LargeSymbolAnd=0:4*LargeSymbolColon=0:4*LargeSymbolExclamationMark=0:4*LargeSymbolQuestionMark=0:4*SmallSymbolHyphen=0:1*SmallSymbolUnderscore=0:1*SmallSymbolDot=0:1*SmallSymbolApostrophe=0:1*SmallSymbolAnd=0:1*SmallSymbolColon=0:1*SmallSymbolExclamationMark=0:1*SmallSymbolQuestionMark=0:1*FireCover=0:4,1:10*FireCoverCorner=0:8,1:20*HalfWindow=15:4,0:10,6:10*HalfWindowInv=15:4,0:10,6:10*HalfWindowCorner=15:8,0:20,6:20*HalfWindowCornerInv=15:8,0:20,6:20*HalfWindowDiagonal=15:6,0:14,6:14*HalfWindowRound=15:7,0:16,6:18*Embrasure=0:30,1:20,12:10*PassageSciFi=7:74,1:20,5:48*PassageSciFiWall=7:50,1:14,5:32*PassageSciFiIntersection=7:35,1:10,5:25*PassageSciFiGate=7:35,1:10,5:25*PassageScifiCorner=7:74,1:20,5:48*PassageSciFiTjunction=7:55,1:16,5:38*PassageSciFiWindow=7:60,1:16,6:16,5:38*BridgeWindow1x1Slope=15:8,0:5,7:10,6:25*BridgeWindow1x1Face=15:8,0:2,7:4,6:18*BridgeWindow1x1FaceInverted=15:5,0:6,7:12,6:12*LargeWindowSquare=7:12,1:8,5:4*LargeWindowEdge=7:16,1:12,5:6*Window1x2Slope=15:16,6:55*Window1x2Inv=15:15,6:40*Window1x2Face=15:15,6:40*Window1x2SideLeft=15:13,6:26*Window1x2SideLeftInv=15:13,6:26*Window1x2SideRight=15:13,6:26*Window1x2SideRightInv=15:13,6:26*Window1x1Slope=15:12,6:35*Window1x1Face=15:11,6:24*Window1x1Side=15:9,6:17*Window1x1SideInv=15:9,6:17*Window1x1Inv=15:11,6:24*Window1x2Flat=15:15,6:50*Window1x2FlatInv=15:15,6:50*Window1x1Flat=15:10,6:25*Window1x1FlatInv=15:10,6:25*Window3x3Flat=15:40,6:196*Window3x3FlatInv=15:40,6:196*Window2x3Flat=15:25,6:140*Window2x3FlatInv=15:25,6:140*SmallWindow1x2Slope=15:1,6:3*SmallWindow1x2Inv=15:1,6:3*SmallWindow1x2Face=15:1,6:3*SmallWindow1x2SideLeft=15:1,6:3*SmallWindow1x2SideLeftInv=15:1,6:3*SmallWindow1x2SideRight=15:1,6:3*SmallWindow1x2SideRightInv=15:1,6:3*SmallWindow1x1Slope=15:1,6:2*SmallWindow1x1Face=15:1,6:2*SmallWindow1x1Side=15:1,6:2*SmallWindow1x1SideInv=15:1,6:2*SmallWindow1x1Inv=15:1,6:2*SmallWindow1x2Flat=15:1,6:3*SmallWindow1x2FlatInv=15:1,6:3*SmallWindow1x1Flat=15:1,6:2*SmallWindow1x1FlatInv=15:1,6:2*SmallWindow3x3Flat=15:3,6:12*SmallWindow3x3FlatInv=15:3,6:12*SmallWindow2x3Flat=15:2,6:8*SmallWindow2x3FlatInv=15:2,6:8*WindowRound=15:15,6:45*WindowRoundInv=15:15,6:45*WindowRoundCorner=15:13,6:33*WindowRoundCornerInv=15:13,6:33*WindowRoundFace=15:9,6:21*WindowRoundFaceInv=15:9,6:21*WindowRoundInwardsCorner=15:13,6:20*WindowRoundInwardsCornerInv=15:13,6:20*SmallWindowRound=15:1,6:2*SmallWindowRoundInv=15:1,6:2*SmallWindowRoundCorner=15:1,6:2*SmallWindowRoundCornerInv=15:1,6:2*SmallWindowRoundFace=15:1,6:2*SmallWindowRoundFaceInv=15:1,6:2*SmallWindowRoundInwardsCorner=15:1,6:2*SmallWindowRoundInwardsCornerInv=15:1,6:2$OreDetector*LargeOreDetectorReskin=0:50,1:40,3:5,4:25,11:20*SmallOreDetectorReskin=0:3,1:2,3:1,4:1,11:1*LargeOreDetector=0:50,1:40,3:5,4:25,11:20*SmallBlockOreDetector=0:3,1:2,3:1,4:1,11:1$MyProgrammableBlock*SmallProgrammableBlock=0:2,1:2,2:2,3:1,10:1,4:2*LargeProgrammableBlock=0:21,1:4,2:2,3:1,10:1,4:2*LargeProgrammableBlockReskin=0:21,1:4,2:2,3:1,10:1,4:2*SmallProgrammableBlockReskin=0:2,1:2,2:2,3:1,10:1,4:2$Projector*LargeProjector=0:21,1:4,2:2,3:1,4:2*SmallProjector=0:2,1:2,2:2,3:1,4:2*LargeBlockConsole=7:20,1:30,4:8,10:10$SensorBlock*SmallBlockSensor=7:5,1:5,4:6,13:4,11:6,0:2*LargeBlockSensor=7:5,1:5,4:6,13:4,11:6,0:2*SmallBlockSensorReskin=7:5,1:5,4:6,13:4,11:6,0:2*LargeBlockSensorReskin=7:5,1:5,4:6,13:4,11:6,0:2$TargetDummyBlock*TargetDummy=0:15,5:10,3:2,4:4,10:1$SoundBlock*SmallBlockSoundBlock=7:4,1:6,4:3*LargeBlockSoundBlock=7:4,1:6,4:3$ButtonPanel*ButtonPanelLarge=7:10,1:20,4:20*ButtonPanelSmall=7:2,1:2,4:1*LargeButtonPanelPedestal=7:5,1:10,4:5*SmallButtonPanelPedestal=7:5,1:10,4:5*LargeBlockModularBridgeButtonPanel=15:6,0:10,1:5,4:5,6:30*LargeBlockInsetButtonPanel=7:20,1:20,4:20,10:10*LargeBlockAccessPanel3=1:8,4:3,7:3*VerticalButtonPanelLarge=7:5,1:10,4:5*VerticalButtonPanelSmall=7:5,1:10,4:5*LargeBlockConsoleModuleButtons=7:50,1:50,10:6,4:10*SmallBlockConsoleModuleButtons=7:6,1:6,4:2*LargeSciFiButtonTerminal=7:5,1:10,4:4,10:4*LargeSciFiButtonPanel=7:10,1:20,4:20,10:5$TimerBlock*TimerBlockLarge=7:6,1:30,4:5*TimerBlockSmall=7:2,1:3,4:1*TimerBlockReskinLarge=7:6,1:30,4:5*TimerBlockReskinSmall=7:2,1:3,4:1$TurretControlBlock*LargeTurretControlBlock=7:20,1:30,11:20,3:4,10:6,4:20,0:20*SmallTurretControlBlock=7:4,1:10,11:4,3:2,10:1,4:10,0:4$EventControllerBlock*EventControllerLarge=7:10,1:30,4:10,10:4*EventControllerSmall=7:2,1:3,4:2,10:1$PathRecorderBlock*LargePathRecorderBlock=7:20,1:30,11:20,3:4,4:20,0:20*SmallPathRecorderBlock=7:2,1:5,11:4,3:2,4:10,0:2$BasicMissionBlock*LargeBasicMission=7:20,1:30,11:20,3:4,4:20,0:20*SmallBasicMission=7:2,1:5,11:4,3:2,4:10,0:2$FlightMovementBlock*LargeFlightMovement=7:20,1:30,11:20,3:4,4:20,0:20*SmallFlightMovement=7:2,1:5,11:4,3:2,4:10,0:2$DefensiveCombatBlock*LargeDefensiveCombat=7:20,1:30,11:20,3:4,4:20,0:20*SmallDefensiveCombat=7:2,1:5,11:4,3:2,4:10,0:2$OffensiveCombatBlock*LargeOffensiveCombat=7:20,1:30,11:20,3:4,4:20,0:20*SmallOffensiveCombat=7:2,1:5,11:4,3:2,4:10,0:2$RadioAntenna*LargeBlockRadioAntenna=0:80,2:40,5:60,1:30,4:8,13:40*LargeBlockCompactRadioAntenna=0:40,2:20,5:30,1:20,4:8,13:40*SmallBlockRadioAntenna=0:2,5:1,1:1,4:1,13:4*LargeBlockCompactRadioAntennaReskin=0:40,7:80,5:30,1:20,4:8,13:40*SmallBlockCompactRadioAntennaReskin=0:2,7:3,5:1,1:1,4:1,13:4*LargeBlockRadioAntennaDish=1:40,15:120,0:80,4:8,13:40$Beacon*LargeBlockBeacon=0:80,1:30,2:20,4:10,13:40*SmallBlockBeacon=0:2,1:1,5:1,4:1,13:4*LargeBlockBeaconReskin=0:80,1:30,2:20,4:10,13:40*SmallBlockBeaconReskin=0:2,1:1,5:1,4:1,13:4$RemoteControl*LargeBlockRemoteControl=7:10,1:10,3:1,4:15*SmallBlockRemoteControl=7:2,1:1,3:1,4:1$LaserAntenna*LargeBlockLaserAntenna=0:50,1:40,3:16,11:30,13:20,14:100,4:50,6:4*SmallBlockLaserAntenna=0:10,5:10,1:10,3:5,13:5,14:10,4:30,6:2$BroadcastController*LargeBlockBroadcastController=7:10,1:30,13:5,4:10,10:4*SmallBlockBroadcastController=7:2,1:3,13:1,4:2,10:1$TransponderBlock*LargeBlockTransponder=0:30,1:20,4:10,13:5*SmallBlockTransponder=0:3,1:2,4:2,13:1$ExtendedPistonBase*LargePistonBaseReskin=0:15,1:10,2:4,3:4,4:2*SmallPistonBaseReskin=0:4,1:4,5:4,3:2,4:1*LargePistonBase=0:15,1:10,2:4,3:4,4:2*SmallPistonBase=0:4,1:4,5:4,3:2,4:1$PistonTop*LargePistonTopReskin=0:10,2:8*SmallPistonTopReskin=0:4,2:2*LargePistonTop=0:10,2:8*SmallPistonTop=0:4,2:2$Door*LargeBlockSmallGate=0:300,1:70,5:60,3:10,4:6*LargeBlockEvenWideDoor=0:300,1:70,5:60,3:10,4:6*(null)=7:10,1:40,5:4,3:2,10:1,4:2,0:8*SmallDoor=7:8,1:30,5:4,3:2,10:1,4:2,0:6*CorridorRoundDoor=0:45,1:50,5:10,3:4,10:2,4:2*CorridorRoundDoorInv=0:45,1:50,5:10,3:4,10:2,4:2*LargeBlockLabDoor=15:10,6:20,1:20,3:2,10:2,4:2*LargeBlockLabDoorInv=15:10,6:20,1:20,3:2,10:2,4:2*LargeBlockGate=0:800,1:100,5:100,3:20,4:10*LargeBlockOffsetDoor=0:25,1:35,5:4,3:4,10:1,4:2,6:6*LargeBlockNarrowDoor=0:35,1:40,5:10,3:4,10:1,4:2*LargeBlockNarrowDoorHalf=0:25,1:40,5:10,3:4,10:1,4:2*SmallSideDoor=7:10,1:26,6:4,3:2,10:1,4:2,0:8*SlidingHatchDoor=0:40,1:50,5:10,3:4,10:2,4:2,6:10*SlidingHatchDoorHalf=0:30,1:50,5:10,3:4,10:2,4:2,6:10$Cockpit*LargeBlockModularBridgeCockpit=0:15,1:15,3:1,10:4,4:100,6:30,7:30*LargeBlockCaptainDesk=7:50,1:50,4:6,10:4*LargeBlockCockpit=7:20,1:20,3:2,4:100,10:10*LargeBlockCockpitSeat=0:30,1:20,3:1,10:8,4:100,6:60*SmallBlockCockpit=0:10,1:10,3:1,10:5,4:15,6:30*DBSmallBlockFighterCockpit=1:20,3:1,0:20,12:10,7:15,10:4,4:20,6:40*CockpitOpen=7:20,1:20,3:2,4:100,10:4*RoverCockpit=7:30,1:25,3:2,4:20,10:4*OpenCockpitSmall=7:20,1:20,3:1,4:15,10:2*OpenCockpitLarge=7:30,1:30,3:2,4:100,10:6*SmallBlockFlushCockpit=0:20,1:20,3:2,10:5,4:20,6:40*LargeBlockDesk=7:30,1:30*LargeBlockDeskCorner=7:20,1:20*LargeBlockDeskCornerInv=7:60,1:60*LargeBlockCouch=7:30,1:30*LargeBlockCouchCorner=7:35,1:35*LargeBlockBathroomOpen=7:30,1:30,5:8,3:4,2:2*LargeBlockBathroom=7:30,1:40,5:8,3:4,2:2*LargeBlockToilet=7:10,1:15,5:2,3:2,2:1*SmallBlockCockpitIndustrial=0:10,1:20,12:10,3:2,10:6,4:20,6:60,5:10*LargeBlockCockpitIndustrial=0:20,1:30,12:15,3:2,10:10,4:60,6:80,5:10*SmallBlockCapCockpit=0:20,1:10,3:1,10:4,4:15,6:10*LargeBlockInsetPlantCouch=7:30,1:30,5:10,6:20*LargeBlockLabDeskSeat=7:30,1:30,4:6,10:4*SpeederCockpit=7:30,1:25,3:2,4:20,10:4*SpeederCockpitCompact=7:30,1:25,3:2,4:20,10:4*PassengerSeatLarge=7:20,1:20*PassengerSeatSmall=7:20,1:20*PassengerSeatSmallNew=7:20,1:20*PassengerSeatSmallOffset=7:20,1:20*BuggyCockpit=7:30,1:25,3:2,4:20,10:4*LargeBlockConsoleModuleInvertedCorner=7:80,1:80*LargeBlockConsoleModuleScreens=7:50,1:50,10:12,4:10*PassengerBench=7:20,1:20*SmallBlockStandingCockpit=7:20,1:20,3:1,4:20,10:2*LargeBlockStandingCockpit=7:20,1:20,3:1,4:20,10:2$ReflectorLight*LargeBlockFloodlight=0:8,15:10,7:10,1:20,6:4*LargeBlockFloodlightAngled=0:8,15:10,7:10,1:20,6:4*LargeBlockFloodlightCornerL=0:8,15:10,7:10,1:20,6:4*LargeBlockFloodlightCornerR=0:8,15:10,7:10,1:20,6:4*SmallBlockFloodlight=0:1,15:4,7:1,1:1,6:2*SmallBlockFloodlightAngled=0:1,15:4,7:1,1:1,6:2*SmallBlockFloodlightCornerL=0:1,15:4,7:1,1:1,6:2*SmallBlockFloodlightCornerR=0:1,15:4,7:1,1:1,6:2*SmallBlockFloodlightDown=0:1,15:4,7:1,1:1,6:2*SmallBlockFloodlightAngledRotated=0:1,15:4,7:1,1:1,6:2*RotatingLightLarge=1:3,3:1*RotatingLightSmall=1:3,3:1*LargeBlockFrontLight=0:8,2:2,7:20,1:15,6:4*SmallBlockFrontLight=0:1,2:1,7:1,1:1,6:2*OffsetSpotlight=1:2,6:1$LargeMissileTurret*LargeMissileTurretReskin=0:40,1:50,12:15,2:6,3:16,4:10*SmallMissileTurretReskin=0:15,1:40,12:5,2:2,3:8,4:10*(null)=0:40,1:50,12:15,2:6,3:16,4:10*SmallMissileTurret=0:15,1:40,12:5,2:2,3:8,4:10*LargeCalibreTurret=0:450,1:400,12:50,2:40,3:30,4:20*LargeBlockMediumCalibreTurret=0:300,1:280,12:30,2:30,3:20,4:20*SmallBlockMediumCalibreTurret=0:50,1:100,12:10,2:6,3:10,4:20$LargeGatlingTurret*LargeGatlingTurretReskin=0:40,1:40,12:15,5:6,3:8,4:10*SmallGatlingTurretReskin=0:15,1:30,12:5,5:6,3:4,4:10*(null)=0:40,1:40,12:15,5:6,3:8,4:10*SmallGatlingTurret=0:15,1:30,12:5,5:6,3:4,4:10*AutoCannonTurret=0:20,1:40,12:6,5:4,3:4,4:10$Jukebox*SmallBlockJukeboxReskin=7:4,1:2,4:2,10:1*Jukebox=7:15,1:10,4:4,10:4*LargeBlockInsetEntertainmentCorner=7:30,1:20,4:10,10:8$TerminalBlock*SmallBlockFirstAidCabinet=7:3,1:3*SmallBlockKitchenOven=7:8,1:12,3:1,6:4*SmallBlockKitchenMicrowave=7:4,1:6,3:1,6:2*SmallBlockKitchenFridge=7:4,1:6,3:1,6:2*ControlPanel=0:1,1:1,4:1,10:1*SmallControlPanel=0:1,1:1,4:1,10:1*LargeControlPanelPedestal=7:5,1:10,4:1,10:1*SmallControlPanelPedestal=7:5,1:10,4:1,10:1*LargeCrate=0:20,5:8,3:4,1:24*LargeFreezer=7:20,1:20,5:10,6:10*LargeBlockAccessPanel1=1:15,4:5,7:5*LargeBlockAccessPanel2=1:15,5:10,7:5*LargeBlockAccessPanel4=1:10,7:10*SmallBlockAccessPanel1=1:8,5:2,7:2*SmallBlockAccessPanel2=1:2,5:1,7:1*SmallBlockAccessPanel3=1:4,5:1,7:1*SmallBlockAccessPanel4=1:10,7:10*LargeBlockSciFiTerminal=1:4,4:2,10:4,7:2$CryoChamber*SmallBlockBunkBed=7:20,1:15,5:5*LargeBlockBed=7:30,1:30,5:8,6:10*LargeBlockCryoRoom=7:40,1:20,3:8,10:8,9:3,4:30,6:10*LargeBlockHalfBed=7:14,1:16,5:6,10:3*LargeBlockHalfBedOffset=7:14,1:16,5:6,10:3*LargeBlockInsetBed=7:60,1:30,5:8*LargeBlockCryoLabVat=7:30,1:10,3:8,6:20,9:3,4:30*LargeBlockCryoChamber=7:40,1:20,3:8,10:8,9:3,4:30,6:10*SmallBlockCryoChamber=7:20,1:10,3:4,10:4,9:3,4:15,6:5$CargoContainer*SmallBlockModularContainer=7:50,1:20,4:5,3:6,10:1*LargeBlockLockerRoom=7:30,1:30,10:4,6:10*LargeBlockLockerRoomCorner=7:25,1:30,10:4,6:10*LargeBlockLockers=7:20,1:20,10:3,4:2*LargeBlockInsetBookshelf=7:30,1:30*LargeBlockCargoTerminal=7:40,1:40,12:4,5:20,3:4,10:1,4:2*LargeBlockCargoTerminalHalf=7:30,1:30,12:2,5:10,3:4,10:1,4:2*LargeBlockLabCornerDesk=7:30,1:30,11:4*LargeBlockLabCabinet=7:20,1:30,5:8,3:4,10:1,6:2*LargeBlockLargeIndustrialContainer=7:360,1:80,12:24,5:60,3:20,10:1,4:8*SmallBlockSmallContainer=7:3,1:1,4:1,3:1,10:1*SmallBlockMediumContainer=7:30,1:10,4:4,3:4,10:1*SmallBlockLargeContainer=7:75,1:25,4:6,3:8,10:1*LargeBlockSmallContainer=7:40,1:40,12:4,5:20,3:4,10:1,4:2*LargeBlockLargeContainer=7:360,1:80,12:24,5:60,3:20,10:1,4:8*LargeBlockWeaponRack=7:30,1:20*SmallBlockWeaponRack=7:3,1:3$Gyro*LargeBlockGyro=0:600,1:40,2:4,12:50,3:4,4:5*SmallBlockGyro=0:25,1:5,2:1,3:2,4:3*LargeBlockPrototechGyro=23:1,26:300,1:40,25:1,29:2,2:16,12:50,4:5*SmallBlockPrototechGyro=23:1,26:70,1:20,25:1,29:2,2:4,12:5,4:3$Kitchen*LargeBlockKitchen=7:20,1:30,2:6,3:6,6:4$Planter*LargeBlockPlanters=7:10,1:20,5:8,6:8$VendingMachine*FoodDispenser=7:20,1:10,3:4,10:10,4:10*VendingMachine=7:20,1:10,3:4,10:4,4:10$LCDPanelsBlock*LabEquipment=7:15,1:15,3:1,6:4*MedicalStation=7:15,1:15,3:2,9:1,10:2*LargeBlockLabDeskMicroscope=7:20,1:20,4:6,11:8,10:4,6:6*LabEquipment1=7:20,1:30,3:4,11:4,6:40*LabEquipment3=7:60,1:50,3:12,20:2,14:8,6:16$TextPanel*TransparentLCDLarge=1:8,4:6,10:10,6:10*TransparentLCDSmall=1:4,4:4,10:3,6:1*HoloLCDLarge=1:10,3:1,4:8*HoloLCDSmall=1:5,3:1,4:8*LargeFullBlockLCDPanel=7:20,1:20,4:6,10:10,6:6*SmallFullBlockLCDPanel=7:4,1:4,4:4,10:3,6:1*LargeDiagonalLCDPanel=7:10,1:10,4:6,10:10,6:8*SmallDiagonalLCDPanel=7:4,1:4,4:4,10:3,6:1*LargeCurvedLCDPanel=7:20,1:20,4:6,10:10,6:10*SmallCurvedLCDPanel=7:4,1:4,4:4,10:3,6:1*SmallTextPanel=7:1,1:4,4:4,10:3,6:1*SmallLCDPanelWide=7:1,1:8,4:8,10:6,6:2*SmallLCDPanel=7:1,1:4,4:4,10:3,6:2*LargeBlockCorner_LCD_1=1:5,4:3,10:1*LargeBlockCorner_LCD_2=1:5,4:3,10:1*LargeBlockCorner_LCD_Flat_1=1:5,4:3,10:1*LargeBlockCorner_LCD_Flat_2=1:5,4:3,10:1*SmallBlockCorner_LCD_1=1:3,4:2,10:1*SmallBlockCorner_LCD_2=1:3,4:2,10:1*SmallBlockCorner_LCD_Flat_1=1:3,4:2,10:1*SmallBlockCorner_LCD_Flat_2=1:3,4:2,10:1*LargeTextPanel=7:1,1:6,4:6,10:10,6:2*LargeLCDPanel=7:1,1:6,4:6,10:10,6:6*LargeLCDPanelWide=7:2,1:12,4:12,10:20,6:12*SmallBlockConsoleModuleScreens=7:6,1:6,10:2,4:2*LargeLCDPanel5x5=7:25,1:150,4:25,10:250,6:150*LargeLCDPanel5x3=7:15,1:90,4:15,10:150,6:90*LargeLCDPanel3x3=7:10,1:50,4:10,10:90,6:50$SolarPanel*LargeBlockColorableSolarPanel=0:4,1:14,15:12,4:4,16:32,6:4*LargeBlockColorableSolarPanelCorner=0:2,1:7,15:6,4:4,16:16,6:2*LargeBlockColorableSolarPanelCornerInverted=0:2,1:7,15:6,4:4,16:16,6:2*SmallBlockColorableSolarPanel=0:2,1:2,15:4,4:1,16:8,6:1*SmallBlockColorableSolarPanelCorner=0:1,1:2,15:2,4:1,16:4,6:1*SmallBlockColorableSolarPanelCornerInverted=0:1,1:2,15:2,4:1,16:4,6:1*LargeBlockSolarPanel=0:4,1:14,15:12,4:4,16:32,6:4*SmallBlockSolarPanel=0:2,1:2,15:4,4:1,16:8,6:1$WindTurbine*LargeBlockWindTurbineReskin=7:40,3:8,1:20,15:24,4:2*LargeBlockWindTurbine=7:40,3:8,1:20,15:24,4:2$MedicalRoom*LargeMedicalRoomReskin=7:240,1:80,12:60,5:20,2:5,10:10,4:10,9:15*LargeMedicalRoom=7:240,1:80,12:60,5:20,2:5,10:10,4:10,9:15*InsetRefillStation=7:20,1:30,12:2,3:6,10:4*LargeRefillStation=7:9,1:10,12:2,3:6,10:4*SmallRefillStation=7:9,1:10,12:2,3:6,10:4$Ladder2*TrussLadder=15:20,7:10,1:20,5:30*(null)=7:10,1:20,5:10*LadderShaft=7:80,1:40,5:50*LadderSmall=7:10,1:20,5:10$Warhead*LargeExplosiveBarrel=0:5,2:1,1:6,5:2,4:1,17:2*SmallExplosiveBarrel=0:5,2:1,1:6,5:2,4:1,17:2*LargeWarhead=0:20,15:24,1:12,5:12,4:2,17:6*SmallWarhead=0:4,15:1,1:1,5:2,4:1,17:2$AirtightHangarDoor*(null)=0:350,1:40,5:40,3:16,4:2*AirtightHangarDoorWarfare2A=0:350,1:40,5:40,3:16,4:2*AirtightHangarDoorWarfare2B=0:350,1:40,5:40,3:16,4:2*AirtightHangarDoorWarfare2C=0:350,1:40,5:40,3:16,4:2$AirtightSlideDoor*LargeBlockSlideDoor=0:20,1:40,5:4,3:4,10:1,4:2,6:15$StoreBlock*StoreBlock=0:30,1:20,3:6,10:4,4:10*AtmBlock=0:20,1:20,3:2,4:10,10:4$SafeZoneBlock*SafeZoneBlock=0:800,1:180,8:10,18:5,12:80,4:120$ContractBlock*ContractBlock=0:30,1:20,3:6,10:4,4:10$BatteryBlock*LargeBlockBatteryBlock=0:80,1:30,19:80,4:25*SmallBlockBatteryBlock=0:25,1:5,19:20,4:2*SmallBlockSmallBatteryBlock=0:4,1:2,19:2,4:2*LargeBlockPrototechBattery=23:1,1:30,25:3,24:20,12:16,4:25,26:60*SmallBlockPrototechBattery=23:1,1:5,25:1,24:6,12:4,4:2,26:6*LargeBlockBatteryBlockWarfare2=0:80,1:30,19:80,4:25*SmallBlockBatteryBlockWarfare2=0:25,1:5,19:20,4:2$Reactor*SmallBlockSmallGenerator=0:3,1:10,12:2,2:1,20:3,3:1,4:10*SmallBlockLargeGenerator=0:60,1:9,12:9,2:3,20:95,3:5,4:25*LargeBlockSmallGenerator=0:80,1:40,12:4,2:8,20:100,3:6,4:25*LargeBlockLargeGenerator=0:1000,1:70,12:40,2:40,14:100,20:2000,3:20,4:75*LargeBlockSmallGeneratorWarfare2=0:80,1:40,12:4,2:8,20:100,3:6,4:25*LargeBlockLargeGeneratorWarfare2=0:1000,1:70,12:40,2:40,14:100,20:2000,3:20,4:75*SmallBlockSmallGeneratorWarfare2=0:3,1:10,12:2,2:1,20:3,3:1,4:10*SmallBlockLargeGeneratorWarfare2=0:60,1:9,12:9,2:3,20:95,3:5,4:25$HydrogenEngine*LargeHydrogenEngine=0:100,1:70,2:12,5:20,3:12,4:4,19:1*SmallHydrogenEngine=0:30,1:20,2:4,5:6,3:4,4:1,19:1*LargePrototechReactor=23:1,26:400,1:200,24:10,27:30,14:400,20:1000,4:100$DebugSphere1*DebugSphereLarge=0:10,4:20$DebugSphere2*DebugSphereLarge=0:10,4:20$DebugSphere3*DebugSphereLarge=0:10,4:20$OxygenTank*LargeHydrogenTankSmallLab=0:80,2:40,5:60,4:8,1:40*SmallHydrogenTankLab=0:40,2:20,5:30,4:4,1:20*LargeBlockOxygenTankLab=0:80,2:40,5:60,4:8,1:40*LargeHydrogenTankIndustrial=0:280,2:80,5:60,4:8,1:40*OxygenTankSmall=0:16,2:8,5:10,4:8,1:10*SmallOxygenTankSmall=0:2,2:1,5:1,4:4,1:1*(null)=0:80,2:40,5:60,4:8,1:40*LargeHydrogenTank=0:280,2:80,5:60,4:8,1:40*LargeHydrogenTankSmall=0:80,2:40,5:60,4:8,1:40*SmallHydrogenTank=0:40,2:20,5:30,4:4,1:20*SmallHydrogenTankSmall=0:3,2:1,5:2,4:4,1:2$OxygenGenerator*LargeBlockOxygenGeneratorLab=0:160,1:20,2:4,3:4,4:5,6:40*SmallBlockOxygenGeneratorLab=0:6,1:8,2:2,3:1,4:3,6:3*(null)=0:120,1:5,2:2,3:4,4:5*OxygenGeneratorSmall=0:8,1:8,2:2,3:1,4:3*IrrigationSystem=0:100,1:20,2:10,3:6,4:5$ExhaustBlock*LargeExhaustCap=7:10,1:8,3:2*SmallExhaustCap=7:2,1:2,3:1*SmallExhaustPipe=0:2,1:1,5:2,3:2*LargeExhaustPipe=0:15,1:10,2:2,3:4$GravityGenerator*(null)=0:150,8:6,1:60,2:4,3:6,4:40$GravityGeneratorSphere*(null)=0:150,8:6,1:60,2:4,3:6,4:40$VirtualMass*VirtualMassLarge=0:90,14:20,1:30,4:20,8:9*VirtualMassSmall=0:3,14:2,1:2,4:2,8:1$SpaceBall*SpaceBallLarge=0:225,1:30,4:20,8:3*SpaceBallSmall=0:70,1:10,4:7,8:1$AirVent*AirVentFan=0:30,1:20,3:10,4:5*AirVentFanFull=0:45,1:30,3:10,4:5*SmallAirVentFan=0:3,1:10,3:2,4:5*SmallAirVentFanFull=0:5,1:15,3:2,4:5*(null)=0:30,1:20,3:10,4:5*AirVentFull=0:45,1:30,3:10,4:5*SmallAirVent=0:3,1:10,3:2,4:5*SmallAirVentFull=0:5,1:15,3:2,4:5$CameraBlock*LargeCameraTopMounted=0:2,4:3*SmallCameraTopMounted=0:2,4:3*SmallCameraBlock=0:2,4:3*LargeCameraBlock=0:2,4:3$EmotionControllerBlock*EmotionControllerLarge=7:10,1:30,4:20,10:12,6:6*EmotionControllerSmall=7:1,1:3,4:5,10:1,6:1$LandingGear*LargeBlockMagneticPlate=0:450,1:60,3:20*SmallBlockMagneticPlate=0:6,1:15,3:3*LargeBlockLandingGear=0:150,1:20,3:6*SmallBlockLandingGear=0:2,1:5,3:1*LargeBlockSmallMagneticPlate=0:15,1:3,3:1*SmallBlockSmallMagneticPlate=0:2,1:1,3:1$ConveyorConnector*LargeBlockConveyorPipeSeamless=7:14,1:20,5:12,3:6*LargeBlockConveyorPipeCorner=7:14,1:20,5:12,3:6*LargeBlockConveyorPipeFlange=7:14,1:20,5:12,3:6*LargeBlockConveyorPipeEnd=7:14,1:20,5:12,3:6*ConveyorTube=7:14,1:20,5:12,3:6*ConveyorTubeDuct=0:25,7:14,1:20,5:12,3:6*ConveyorTubeDuctCurved=0:25,7:14,1:20,5:12,3:6*ConveyorTubeSmall=7:1,3:1,1:1*ConveyorTubeDuctSmall=0:2,7:1,3:1,1:1*ConveyorTubeDuctSmallCurved=0:2,7:1,3:1,1:1*ConveyorTubeMedium=7:10,1:20,5:10,3:6*ConveyorFrameMedium=7:5,1:12,5:5,3:2*ConveyorTubeCurved=7:14,1:20,5:12,3:6*ConveyorTubeSmallCurved=7:1,3:1,1:1*ConveyorTubeCurvedMedium=7:7,1:20,5:10,3:6$Conveyor*LargeBlockConveyorPipeJunction=7:20,1:30,5:20,3:6*LargeBlockConveyorPipeIntersection=7:18,1:20,5:16,3:6*LargeBlockConveyorPipeT=7:16,1:24,5:14,3:6*SmallBlockConveyor=7:4,1:4,3:1*SmallBlockConveyorConverter=7:6,1:8,5:6,3:2*LargeBlockConveyor=7:20,1:30,5:20,3:6*ConveyorTubeDuctT=0:22,7:16,1:24,5:14,3:6*ConveyorTubeDuctSmallT=0:2,7:2,3:1,1:2*SmallShipConveyorHub=7:15,1:20,5:15,3:2*ConveyorTubeSmallT=7:2,3:1,1:2*ConveyorTubeT=7:16,1:24,5:14,3:6$Assembler*LargeAssemblerIndustrial=0:140,1:80,3:20,10:10,12:10,4:160*LargeAssembler=0:140,1:80,3:20,10:10,12:10,4:160*BasicAssembler=0:80,1:40,3:10,10:4,4:80*FoodProcessor=0:30,1:40,3:4,10:2,4:20,6:10*LargePrototechAssembler=23:1,26:240,1:130,29:20,27:2,12:80,4:200,10:10$Refinery*LargeRefineryIndustrial=0:1200,1:40,2:20,3:16,12:20,4:20*LargeRefinery=0:1200,1:40,2:20,3:16,12:20,4:20*BlastFurnace=0:120,1:20,3:10,4:10*LargePrototechRefinery=23:1,26:675,1:40,29:10,27:5,2:20,12:20,4:20*SmallPrototechRefinery=23:1,26:70,1:20,29:3,27:2,2:16,12:20,4:20$ConveyorSorter*LargeBlockConveyorSorterIndustrial=7:50,1:120,5:50,4:20,3:2*LargeBlockConveyorSorter=7:50,1:120,5:50,4:20,3:2*MediumBlockConveyorSorter=7:5,1:12,5:5,4:5,3:2*SmallBlockConveyorSorter=7:5,1:12,5:5,4:5,3:2$Thrust*LargeBlockLargeHydrogenThrustIndustrial=0:150,1:180,12:250,2:40*LargeBlockSmallHydrogenThrustIndustrial=0:25,1:60,12:40,2:8*SmallBlockLargeHydrogenThrustIndustrial=0:30,1:30,12:22,2:10*SmallBlockSmallHydrogenThrustIndustrial=0:7,1:15,12:4,2:2*LargeBlockPrototechThruster=23:1,26:500,1:325,27:5,28:60,12:250,2:160*SmallBlockPrototechThruster=23:1,26:10,1:12,27:1,28:3,12:5,2:1*SmallBlockSmallThrustSciFi=0:2,1:2,2:1,30:1*SmallBlockLargeThrustSciFi=0:5,1:2,2:5,30:12*LargeBlockSmallThrustSciFi=0:25,1:60,2:8,30:80*LargeBlockLargeThrustSciFi=0:150,1:100,2:40,30:960*LargeBlockLargeAtmosphericThrustSciFi=0:230,1:60,2:50,12:40,3:1100*LargeBlockSmallAtmosphericThrustSciFi=0:35,1:50,2:8,12:10,3:110*SmallBlockLargeAtmosphericThrustSciFi=0:20,1:30,2:4,12:8,3:90*SmallBlockSmallAtmosphericThrustSciFi=0:3,1:22,2:1,12:1,3:18*SmallBlockSmallThrust=0:2,1:2,2:1,30:1*SmallBlockLargeThrust=0:5,1:2,2:5,30:12*LargeBlockSmallThrust=0:25,1:60,2:8,30:80*LargeBlockLargeThrust=0:150,1:100,2:40,30:960*LargeBlockLargeHydrogenThrust=0:150,1:180,12:250,2:40*LargeBlockSmallHydrogenThrust=0:25,1:60,12:40,2:8*SmallBlockLargeHydrogenThrust=0:30,1:30,12:22,2:10*SmallBlockSmallHydrogenThrust=0:7,1:15,12:4,2:2*LargeBlockLargeAtmosphericThrust=0:230,1:60,2:50,12:40,3:1100*LargeBlockSmallAtmosphericThrust=0:35,1:50,2:8,12:10,3:110*SmallBlockLargeAtmosphericThrust=0:20,1:30,2:4,12:8,3:90*SmallBlockSmallAtmosphericThrust=0:3,1:22,2:1,12:1,3:18*LargeBlockLargeFlatAtmosphericThrust=0:90,1:25,2:20,12:15,3:400*LargeBlockLargeFlatAtmosphericThrustDShape=0:90,1:25,2:20,12:15,3:400*LargeBlockSmallFlatAtmosphericThrust=0:15,1:20,2:3,12:3,3:30*LargeBlockSmallFlatAtmosphericThrustDShape=0:15,1:20,2:3,12:3,3:30*SmallBlockLargeFlatAtmosphericThrust=0:8,1:14,2:2,12:3,3:30*SmallBlockLargeFlatAtmosphericThrustDShape=0:8,1:14,2:2,12:3,3:30*SmallBlockSmallFlatAtmosphericThrust=0:2,1:11,2:1,12:1,3:6*SmallBlockSmallFlatAtmosphericThrustDShape=0:2,1:11,2:1,12:1,3:6*SmallBlockSmallModularThruster=0:2,1:2,2:1,30:1*SmallBlockLargeModularThruster=0:5,1:2,2:5,30:12*LargeBlockSmallModularThruster=0:25,1:60,2:8,30:80*LargeBlockLargeModularThruster=0:150,1:100,2:40,30:960$Passage*(null)=7:74,1:20,5:48$Collector*Collector=0:45,1:50,5:12,3:8,10:4,4:10*CollectorSmall=0:35,1:35,5:12,3:8,10:2,4:8$ShipConnector*Connector=0:150,1:40,5:12,3:8,4:20*ConnectorSmall=0:7,1:4,5:2,3:1,4:4*ConnectorMedium=0:21,1:12,5:6,3:6,4:6*LargeBlockInsetConnector=0:150,1:40,5:12,3:8,4:20*LargeBlockInsetConnectorSmall=0:150,1:40,5:12,3:8,4:20*SmallBlockInsetConnector=0:7,1:4,5:2,3:1,4:4*SmallBlockInsetConnectorMedium=0:21,1:12,5:6,3:6,4:6$PistonBase*LargePistonBase=0:15,1:10,2:4,3:4,4:2*SmallPistonBase=0:4,1:4,5:4,3:2,4:1$MotorStator*LargeStator=0:15,1:10,2:4,3:4,4:2*SmallStator=0:5,1:5,5:1,3:1,4:1$MotorRotor*LargeRotor=0:30,2:6*SmallRotor=0:12,5:6$MotorAdvancedStator*LargeAdvancedStator=0:15,1:10,2:4,3:4,4:2*SmallAdvancedStator=0:10,1:6,5:1,3:2,4:2*SmallAdvancedStatorSmall=0:5,1:5,5:1,3:1,4:1*LargeHinge=0:16,1:10,2:4,3:4,4:2*MediumHinge=0:10,1:6,2:2,3:2,4:2*SmallHinge=0:6,1:4,2:1,3:2,4:2$MotorAdvancedRotor*LargeAdvancedRotor=0:30,2:10*SmallAdvancedRotor=0:20,2:6*SmallAdvancedRotorSmall=0:12,5:6*LargeHingeHead=0:12,2:4,1:8*MediumHingeHead=0:6,2:2,1:4*SmallHingeHead=0:3,2:1,1:2$UpgradeModule*LargeProductivityModule=0:100,1:40,5:20,4:60,3:4*LargeEffectivenessModule=0:100,1:50,5:15,14:20,3:4*LargeEnergyModule=0:100,1:40,5:20,19:20,3:4$JumpDrive*LargePrototechJumpDrive=23:1,8:30,24:30,25:20,14:1400,4:300,1:180,26:200*SmallPrototechJumpDrive=23:1,8:4,24:8,25:4,14:100,4:20,1:20,26:15*LargeJumpDrive=0:60,12:50,8:20,11:20,19:120,14:1000,4:300,1:40$MotorSuspension*OffroadSuspension3x3=0:25,1:15,2:6,5:12,3:6*OffroadSuspension5x5=0:70,1:40,2:20,5:30,3:20*OffroadSuspension1x1=0:25,1:15,2:6,5:12,3:6*OffroadSuspension2x2=0:25,1:15,2:6,5:12,3:6*OffroadSmallSuspension3x3=0:8,1:7,5:2,3:1*OffroadSmallSuspension5x5=0:16,1:12,5:4,3:2*OffroadSmallSuspension1x1=0:8,1:7,5:2,3:1*OffroadSmallSuspension2x2=0:8,1:7,5:2,3:1*OffroadSuspension3x3mirrored=0:25,1:15,2:6,5:12,3:6*OffroadSuspension5x5mirrored=0:70,1:40,2:20,5:30,3:20*OffroadSuspension1x1mirrored=0:25,1:15,2:6,5:12,3:6*OffroadSuspension2x2Mirrored=0:25,1:15,2:6,5:12,3:6*OffroadSmallSuspension3x3mirrored=0:8,1:7,5:2,3:1*OffroadSmallSuspension5x5mirrored=0:16,1:12,5:4,3:2*OffroadSmallSuspension1x1mirrored=0:8,1:7,5:2,3:1*OffroadSmallSuspension2x2Mirrored=0:8,1:7,5:2,3:1*OffroadShortSuspension3x3=0:15,1:10,2:6,5:12,3:6*OffroadShortSuspension5x5=0:45,1:30,2:20,5:30,3:20*OffroadShortSuspension1x1=0:15,1:10,2:6,5:12,3:6*OffroadShortSuspension2x2=0:15,1:10,2:6,5:12,3:6*OffroadSmallShortSuspension3x3=0:5,1:5,5:2,3:1*OffroadSmallShortSuspension5x5=0:10,1:10,5:4,3:2*OffroadSmallShortSuspension1x1=0:5,1:5,5:2,3:1*OffroadSmallShortSuspension2x2=0:5,1:5,5:2,3:1*OffroadShortSuspension3x3mirrored=0:15,1:10,2:6,5:12,3:6*OffroadShortSuspension5x5mirrored=0:45,1:30,2:20,5:30,3:20*OffroadShortSuspension1x1mirrored=0:15,1:10,2:6,5:12,3:6*OffroadShortSuspension2x2Mirrored=0:15,1:10,2:6,5:12,3:6*OffroadSmallShortSuspension3x3mirrored=0:5,1:5,5:2,3:1*OffroadSmallShortSuspension5x5mirrored=0:10,1:10,5:4,3:2*OffroadSmallShortSuspension1x1mirrored=0:5,1:5,5:2,3:1*OffroadSmallShortSuspension2x2Mirrored=0:5,1:5,5:2,3:1*Suspension3x3=0:25,1:15,2:6,5:12,3:6*Suspension5x5=0:70,1:40,2:20,5:30,3:20*Suspension1x1=0:25,1:15,2:6,5:12,3:6*Suspension2x2=0:25,1:15,2:6,5:12,3:6*SmallSuspension3x3=0:8,1:7,5:2,3:1*SmallSuspension5x5=0:16,1:12,5:4,3:2*SmallSuspension1x1=0:8,1:7,5:2,3:1*SmallSuspension2x2=0:8,1:7,5:2,3:1*Suspension3x3mirrored=0:25,1:15,2:6,5:12,3:6*Suspension5x5mirrored=0:70,1:40,2:20,5:30,3:20*Suspension1x1mirrored=0:25,1:15,2:6,5:12,3:6*Suspension2x2Mirrored=0:25,1:15,2:6,5:12,3:6*SmallSuspension3x3mirrored=0:8,1:7,5:2,3:1*SmallSuspension5x5mirrored=0:16,1:12,5:4,3:2*SmallSuspension1x1mirrored=0:8,1:7,5:2,3:1*SmallSuspension2x2Mirrored=0:8,1:7,5:2,3:1*ShortSuspension3x3=0:15,1:10,2:6,5:12,3:6*ShortSuspension5x5=0:45,1:30,2:20,5:30,3:20*ShortSuspension1x1=0:15,1:10,2:6,5:12,3:6*ShortSuspension2x2=0:15,1:10,2:6,5:12,3:6*SmallShortSuspension3x3=0:5,1:5,5:2,3:1*SmallShortSuspension5x5=0:10,1:10,5:4,3:2*SmallShortSuspension1x1=0:5,1:5,5:2,3:1*SmallShortSuspension2x2=0:5,1:5,5:2,3:1*ShortSuspension3x3mirrored=0:15,1:10,2:6,5:12,3:6*ShortSuspension5x5mirrored=0:45,1:30,2:20,5:30,3:20*ShortSuspension1x1mirrored=0:15,1:10,2:6,5:12,3:6*ShortSuspension2x2Mirrored=0:15,1:10,2:6,5:12,3:6*SmallShortSuspension3x3mirrored=0:5,1:5,5:2,3:1*SmallShortSuspension5x5mirrored=0:10,1:10,5:4,3:2*SmallShortSuspension1x1mirrored=0:5,1:5,5:2,3:1*SmallShortSuspension2x2Mirrored=0:5,1:5,5:2,3:1$Wheel*OffroadSmallRealWheel1x1=0:2,1:5,2:1*OffroadSmallRealWheel2x2=0:8,1:15,2:3*OffroadSmallRealWheel=0:8,1:15,2:3*OffroadSmallRealWheel5x5=0:15,1:25,2:5*OffroadRealWheel1x1=0:30,1:30,2:10*OffroadRealWheel2x2=0:50,1:40,2:15*OffroadRealWheel=0:70,1:50,2:20*OffroadRealWheel5x5=0:130,1:70,2:30*OffroadSmallRealWheel1x1mirrored=0:2,1:5,2:1*OffroadSmallRealWheel2x2Mirrored=0:8,1:15,2:3*OffroadSmallRealWheelmirrored=0:8,1:15,2:3*OffroadSmallRealWheel5x5mirrored=0:15,1:25,2:5*OffroadRealWheel1x1mirrored=0:30,1:30,2:10*OffroadRealWheel2x2Mirrored=0:50,1:40,2:15*OffroadRealWheelmirrored=0:70,1:50,2:20*OffroadRealWheel5x5mirrored=0:130,1:70,2:30*OffroadWheel1x1=0:30,1:30,2:10*OffroadSmallWheel1x1=0:2,1:5,2:1*OffroadWheel3x3=0:70,1:50,2:20*OffroadSmallWheel3x3=0:8,1:15,2:3*OffroadWheel5x5=0:130,1:70,2:30*OffroadSmallWheel5x5=0:15,1:25,2:5*OffroadWheel2x2=0:50,1:40,2:15*OffroadSmallWheel2x2=0:5,1:10,2:2*SmallRealWheel1x1=0:2,1:5,2:1*SmallRealWheel2x2=0:8,1:15,2:3*SmallRealWheel=0:8,1:15,2:3*SmallRealWheel5x5=0:15,1:25,2:5*RealWheel1x1=0:30,1:30,2:10*RealWheel2x2=0:50,1:40,2:15*RealWheel=0:70,1:50,2:20*RealWheel5x5=0:130,1:70,2:30*SmallRealWheel1x1mirrored=0:2,1:5,2:1*SmallRealWheel2x2Mirrored=0:8,1:15,2:3*SmallRealWheelmirrored=0:8,1:15,2:3*SmallRealWheel5x5mirrored=0:15,1:25,2:5*RealWheel1x1mirrored=0:30,1:30,2:10*RealWheel2x2Mirrored=0:50,1:40,2:15*RealWheelmirrored=0:70,1:50,2:20*RealWheel5x5mirrored=0:130,1:70,2:30*Wheel1x1=0:30,1:30,2:10*SmallWheel1x1=0:2,1:5,2:1*Wheel3x3=0:70,1:50,2:20*SmallWheel3x3=0:8,1:15,2:3*Wheel5x5=0:130,1:70,2:30*SmallWheel5x5=0:15,1:25,2:5*Wheel2x2=0:50,1:40,2:15*SmallWheel2x2=0:5,1:10,2:2$Decoy*TrussPillarDecoy=0:30,1:10,4:10,13:1,2:2*LargeDecoy=0:30,1:10,4:10,13:1,2:2*SmallDecoy=0:2,1:1,4:1,13:1,5:2$EmissiveBlock*LargeNeonTubesStraight1=7:6,5:6,1:2*LargeNeonTubesStraight2=7:6,5:6,1:2*LargeNeonTubesCorner=7:6,5:6,1:2*LargeNeonTubesBendUp=7:12,5:12,1:4*LargeNeonTubesBendDown=7:3,5:3,1:1*LargeNeonTubesStraightEnd1=7:6,5:6,1:2*LargeNeonTubesStraightEnd2=7:10,5:6,1:4*LargeNeonTubesStraightDown=7:9,5:9,1:3*LargeNeonTubesU=7:18,5:18,1:6*LargeNeonTubesT=7:9,5:9,1:3*LargeNeonTubesCircle=7:12,5:12,1:4*SmallNeonTubesStraight1=7:1,5:1,1:1*SmallNeonTubesStraight2=7:1,5:1,1:1*SmallNeonTubesCorner=7:1,5:1,1:1*SmallNeonTubesBendUp=7:1,5:1,1:1*SmallNeonTubesBendDown=7:1,5:1,1:1*SmallNeonTubesStraightDown=7:1,5:1,1:1*SmallNeonTubesStraightEnd1=7:1,5:1,1:1*SmallNeonTubesU=7:1,5:1,1:1*SmallNeonTubesT=7:1,5:1,1:1*SmallNeonTubesCircle=7:1,5:1,1:1$MergeBlock*LargeShipMergeBlock=0:12,1:15,3:2,2:6,4:2*SmallShipMergeBlock=0:4,1:5,3:1,5:2,4:1*SmallShipSmallMergeBlock=0:2,1:3,3:1,5:1,4:1$Parachute*LgParachute=0:9,1:25,5:5,3:3,4:2*SmParachute=0:2,1:2,5:1,3:1,4:1$SmallMissileLauncher*SmallMissileLauncherWarfare2=0:4,1:2,12:1,2:4,3:1,4:1*(null)=0:4,1:2,12:1,2:4,3:1,4:1*LargeMissileLauncher=0:35,1:8,12:30,2:25,3:6,4:4*LargeBlockLargeCalibreGun=0:250,1:20,12:20,2:20,4:5*LargeFlareLauncher=0:20,1:10,2:10,4:4*SmallFlareLauncher=0:2,1:1,2:3,4:1$SmallGatlingGun*SmallGatlingGunWarfare2=0:4,1:1,12:2,5:6,3:1,4:1*(null)=0:4,1:1,12:2,5:6,3:1,4:1*SmallBlockAutocannon=0:6,1:2,12:2,5:2,3:1,4:1$Searchlight*SmallSearchlight=0:1,1:3,2:1,3:2,4:5,6:2*LargeSearchlight=0:5,1:20,2:2,3:4,4:5,6:4$HeatVentBlock*LargeHeatVentBlock=0:25,1:20,2:10,3:5*SmallHeatVentBlock=0:2,1:1,2:1,3:1$InteriorTurret*LargeInteriorTurret=7:6,1:20,5:1,3:2,4:5,0:4$SmallMissileLauncherReload*SmallRocketLauncherReload=5:50,7:50,1:24,2:8,12:10,3:4,4:2,0:8*SmallBlockMediumCalibreGun=0:25,1:10,12:5,2:10,4:1*LargeRailgun=0:350,1:150,14:150,2:60,19:100,4:100*SmallRailgun=0:25,1:20,14:20,2:6,19:10,4:20";
		// the line above this one is really long
	}
}
