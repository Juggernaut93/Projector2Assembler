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
        private readonly bool autoResizeText = true; // NOTE: it only works if monospace font is enabled, ignored otherwise
        private readonly bool fitOn2IfPossible = true; // when true, if no valid third LCD is specified, the script will fit ingots and ores on the second LCD
        /**********************************************/
        /************ END OF CONFIGURATION ************/
        /**********************************************/

        /**********************************************/
        /************ LOCALIZATION STRINGS ************/
        /**********************************************/
        private const string lcd1Title = "Components: available | needed | missing";
        private const string lcd2Title = "Ingots: available | needed now/total | missing";
        private const string lcd3Title = "Ores: available | needed now/total | missing";
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
            ["SmallTube"] = "Small Steel Tube",
            ["SolarCell"] = "Solar Cell",
            ["SteelPlate"] = "Steel Plate",
            ["Superconductor"] = "Superconductor Component",
            ["ThrustComponent"] = "Thruster Components",
            ["ZoneChip"] = "Zone Chip",
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
            ["SmallTube"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 5 },
            ["SolarCell"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Nickel] = 3, [Ingots.Silicon] = 6 },
            ["SteelPlate"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 21 },
            ["Superconductor"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 10, [Ingots.Gold] = 2 },
            ["ThrustComponent"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 30, [Ingots.Cobalt] = 10, [Ingots.Gold] = 1, [Ingots.Platinum] = FP("0.4") },
            // economy comps
            ["ZoneChip"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { }, // cannot be assembled
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
            bool LargeGrid = true;
            foreach (var item in blocks)
            {
                // blockInfo[0] is blueprint, blockInfo[1] is number of required item
                string[] blockInfo = item.ToString().Trim(remove).Split(delimiters, StringSplitOptions.None);

                string blockName = blockInfo[0].Replace(" ", ""); // data in blockDefinitionData is compressed removing spaces
                int amount = Convert.ToInt32(blockInfo[1]);

                if (blockName.StartsWith("SmallBlock"))
                {
                    LargeGrid = false;
                }

                AddComponents(totalComponents, GetComponents(blockName), amount);
            }

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
                foreach (var ing in componentsToIngots[StripDef(pair.Key)])
                {
                    AddCountToDict<Ingots>(ingotsNeeded, ing.Key, ing.Value * (pair.Value / (float)ASSEMBLER_EFFICIENCY));
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
            string perc = info.Substring(startIndex, info.IndexOf("%", startIndex) - startIndex);
            try
            {
                return effectivenessMapping[perc];
            }
            catch (Exception)
            {
                return int.Parse(perc) / 100d;
            }
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

            if (!autoResizeText || lcd.Font != monospaceFontName)
                return;

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
            string output = localProjectorName + "\n" + lcd1Title.ToUpper() + "\n\n";
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
            output = localProjectorName + "\n" + lcd2Title.ToUpper() + "\n\n";
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

                output += String.Format("{0}{1} {2}{3}{4}/{5}{3}{6}\n", (missing > 0 ? warnStr : okStr), ingotName, amountStr, separator, neededStr, totalNeededStr, missingStr);
            }
            if (lcd2 != null)
            {
                ShowAndSetFontSize(lcd2, output);
            }
            Me.CustomData += output + "\n\n";

            var oresList = GetTotalOres(missingIngots);
            var oresTotalNeeded = GetTotalOres(ingotsTotalNeeded);
            //List<KeyValuePair<Ores, VRage.MyFixedPoint>> missingOres = new List<KeyValuePair<Ores, VRage.MyFixedPoint>>();
            if (lcd3 == null && fitOn2IfPossible)
            {
                output = "\n" + lcd3Title.ToUpper() + "\n\n";
            }
            else
            {
                output = localProjectorName + "\n" + lcd3Title.ToUpper() + "\n\n";
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
                        output += String.Format("{0}{1} {2}{3}{4}/{5}{3}{6}\n", okStr, oreName, amountStr, separator, na, na, endNa);
                        var savedIron = amountPresent * conversionData[Ores.Scrap].conversionRate * (1f / conversionData[Ores.Iron].conversionRate);
                        scrapOutput = "\n*" + String.Format(scrapMetalMessage, FormatNumber(amountPresent, oreWidth, oreDecimals).Trim(), oreTranslation[Ores.Scrap], FormatNumber(savedIron, oreWidth, oreDecimals).Trim(), oreTranslation[Ores.Iron]) + "\n";
                    }
                }
                else
                {
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
                ShowAndSetFontSize(lcd2, lcd2.GetText() + output);
            }
            Me.CustomData += output + "\n\n";
        }

        string blockDefinitionData = "SteelPlate*ConstructionComponent*MotorComponent*Display*ComputerComponent*GravityGeneratorComponent*ZoneChip*MetalGrid*InteriorPlate*LargeTube*RadioCommunicationComponent*DetectorComponent*SmallTube*Superconductor*BulletproofGlass*PowerCell*ReactorComponent*GirderComponent*SolarCell*MedicalComponent*ThrustComponent*ExplosivesComponent$StoreBlock*StoreBlock=0:30,1:20,2:6,3:4,4:10*AtmBlock=0:20,1:20,2:2,4:10,3:4$SafeZoneBlock*SafeZoneBlock=0:800,1:180,5:10,6:5,7:80,4:120$ContractBlock*ContractBlock=0:30,1:20,2:6,3:4,4:10$VendingMachine*VendingMachine=8:20,1:10,2:4,3:4,4:10$CubeBlock*LargeRailStraight=0:12,1:8,9:4*LargeBlockArmorBlock=0:25*LargeBlockArmorSlope=0:13*LargeBlockArmorCorner=0:4*LargeBlockArmorCornerInv=0:21*LargeRoundArmor_Slope=0:13*LargeRoundArmor_Corner=0:4*LargeRoundArmor_CornerInv=0:21*LargeHeavyBlockArmorBlock=0:150,7:50*LargeHeavyBlockArmorSlope=0:75,7:25*LargeHeavyBlockArmorCorner=0:25,7:10*LargeHeavyBlockArmorCornerInv=0:125,7:50*SmallBlockArmorBlock=0:1*SmallBlockArmorSlope=0:1*SmallBlockArmorCorner=0:1*SmallBlockArmorCornerInv=0:1*SmallHeavyBlockArmorBlock=0:5,7:2*SmallHeavyBlockArmorSlope=0:3,7:1*SmallHeavyBlockArmorCorner=0:2,7:1*SmallHeavyBlockArmorCornerInv=0:4,7:1*LargeHalfArmorBlock=0:12*LargeHeavyHalfArmorBlock=0:75,7:25*LargeHalfSlopeArmorBlock=0:7*LargeHeavyHalfSlopeArmorBlock=0:45,7:15*HalfArmorBlock=0:1*HeavyHalfArmorBlock=0:3,7:1*HalfSlopeArmorBlock=0:1*HeavyHalfSlopeArmorBlock=0:2,7:1*LargeBlockArmorRoundSlope=0:13*LargeBlockArmorRoundCorner=0:4*LargeBlockArmorRoundCornerInv=0:21*LargeHeavyBlockArmorRoundSlope=0:130,7:50*LargeHeavyBlockArmorRoundCorner=0:125,7:40*LargeHeavyBlockArmorRoundCornerInv=0:140,7:50*SmallBlockArmorRoundSlope=0:1*SmallBlockArmorRoundCorner=0:1*SmallBlockArmorRoundCornerInv=0:1*SmallHeavyBlockArmorRoundSlope=0:4,7:1*SmallHeavyBlockArmorRoundCorner=0:4,7:1*SmallHeavyBlockArmorRoundCornerInv=0:5,7:1*LargeBlockArmorSlope2Base=0:19*LargeBlockArmorSlope2Tip=0:7*LargeBlockArmorCorner2Base=0:10*LargeBlockArmorCorner2Tip=0:4*LargeBlockArmorInvCorner2Base=0:22*LargeBlockArmorInvCorner2Tip=0:16*LargeHeavyBlockArmorSlope2Base=0:112,7:45*LargeHeavyBlockArmorSlope2Tip=0:35,7:6*LargeHeavyBlockArmorCorner2Base=0:55,7:15*LargeHeavyBlockArmorCorner2Tip=0:19,7:6*LargeHeavyBlockArmorInvCorner2Base=0:133,7:45*LargeHeavyBlockArmorInvCorner2Tip=0:94,7:25*SmallBlockArmorSlope2Base=0:1*SmallBlockArmorSlope2Tip=0:1*SmallBlockArmorCorner2Base=0:1*SmallBlockArmorCorner2Tip=0:1*SmallBlockArmorInvCorner2Base=0:1*SmallBlockArmorInvCorner2Tip=0:1*SmallHeavyBlockArmorSlope2Base=0:4,7:1*SmallHeavyBlockArmorSlope2Tip=0:2,7:1*SmallHeavyBlockArmorCorner2Base=0:3,7:1*SmallHeavyBlockArmorCorner2Tip=0:2,7:1*SmallHeavyBlockArmorInvCorner2Base=0:5,7:1*SmallHeavyBlockArmorInvCorner2Tip=0:5,7:1*LargeBlockDeskChairless=8:30,1:30*LargeBlockDeskChairlessCorner=8:20,1:20*ArmorCenter=0:140*ArmorCorner=0:120*ArmorInvCorner=0:135*ArmorSide=0:130*SmallArmorCenter=0:5*SmallArmorCorner=0:5*SmallArmorInvCorner=0:5*SmallArmorSide=0:5*Monolith=0:130,13:130*Stereolith=0:130,13:130*DeadAstronaut=0:13,13:13*LargeDeadAstronaut=0:13,13:13*LargeStairs=8:50,1:30*LargeRamp=8:70,1:16*LargeSteelCatwalk=8:27,1:5,12:20*LargeSteelCatwalk2Sides=8:32,1:7,12:25*LargeSteelCatwalkCorner=8:32,1:7,12:25*LargeSteelCatwalkPlate=8:23,1:7,12:17*LargeCoverWall=0:4,1:10*LargeCoverWallHalf=0:2,1:6*LargeBlockInteriorWall=8:25,1:10*LargeInteriorPillar=8:25,1:10,12:4*LargeWindowSquare=8:12,1:8,12:4*LargeWindowEdge=8:16,1:12,12:6*Window1x2Slope=17:16,14:55*Window1x2Inv=17:15,14:40*Window1x2Face=17:15,14:40*Window1x2SideLeft=17:13,14:26*Window1x2SideLeftInv=17:13,14:26*Window1x2SideRight=17:13,14:26*Window1x2SideRightInv=17:13,14:26*Window1x1Slope=17:12,14:35*Window1x1Face=17:11,14:24*Window1x1Side=17:9,14:17*Window1x1SideInv=17:9,14:17*Window1x1Inv=17:11,14:24*Window1x2Flat=17:15,14:50*Window1x2FlatInv=17:15,14:50*Window1x1Flat=17:10,14:25*Window1x1FlatInv=17:10,14:25*Window3x3Flat=17:40,14:196*Window3x3FlatInv=17:40,14:196*Window2x3Flat=17:25,14:140*Window2x3FlatInv=17:25,14:140$DebugSphere1*DebugSphereLarge=0:10,4:20$DebugSphere2*DebugSphereLarge=0:10,4:20$DebugSphere3*DebugSphereLarge=0:10,4:20$MyProgrammableBlock*SmallProgrammableBlock=0:2,1:2,9:2,2:1,3:1,4:2*LargeProgrammableBlock=0:21,1:4,9:2,2:1,3:1,4:2$Projector*LargeProjector=0:21,1:4,9:2,2:1,4:2*SmallProjector=0:2,1:2,9:2,2:1,4:2*LargeBlockConsole=8:20,1:30,4:8,3:10$SensorBlock*SmallBlockSensor=8:5,1:5,4:6,10:4,11:6,0:2*LargeBlockSensor=8:5,1:5,4:6,10:4,11:6,0:2$SoundBlock*SmallBlockSoundBlock=8:4,1:6,4:3*LargeBlockSoundBlock=8:4,1:6,4:3$ButtonPanel*ButtonPanelLarge=8:10,1:20,4:20*ButtonPanelSmall=8:2,1:2,4:1$TimerBlock*TimerBlockLarge=8:6,1:30,4:5*TimerBlockSmall=8:2,1:3,4:1$RadioAntenna*LargeBlockRadioAntenna=0:80,9:40,12:60,1:30,4:8,10:40*SmallBlockRadioAntenna=0:1,12:1,1:2,4:1,10:4$Beacon*LargeBlockBeacon=0:80,1:30,9:20,4:10,10:40*SmallBlockBeacon=0:2,1:1,12:1,4:1,10:4$RemoteControl*LargeBlockRemoteControl=8:10,1:10,2:1,4:15*SmallBlockRemoteControl=8:2,1:1,2:1,4:1$LaserAntenna*LargeBlockLaserAntenna=0:50,1:40,2:16,11:30,10:20,13:100,4:50,14:4*SmallBlockLaserAntenna=0:10,12:10,1:10,2:5,10:5,13:10,4:30,14:2$TerminalBlock*ControlPanel=0:1,1:1,4:1,3:1*SmallControlPanel=0:1,1:1,4:1,3:1$Cockpit*LargeBlockCockpit=8:20,1:20,2:2,4:100,3:10*LargeBlockCockpitSeat=0:30,1:20,2:1,3:8,4:100,14:60*SmallBlockCockpit=0:10,1:10,2:1,3:5,4:15,14:30*DBSmallBlockFighterCockpit=1:20,2:1,0:20,7:10,8:15,3:4,4:20,14:40*CockpitOpen=8:20,1:20,2:2,4:100,3:4*LargeBlockDesk=8:30,1:30*LargeBlockDeskCorner=8:20,1:20*LargeBlockCouch=8:30,1:30*LargeBlockCouchCorner=8:35,1:35*LargeBlockBathroomOpen=8:30,1:30,12:8,2:4,9:2*LargeBlockBathroom=8:30,1:40,12:8,2:4,9:2*LargeBlockToilet=8:10,1:15,12:2,2:2,9:1*SmallBlockCockpitIndustrial=0:10,1:20,7:10,2:2,3:6,4:20,14:60,12:10*LargeBlockCockpitIndustrial=0:20,1:30,7:15,2:2,3:10,4:60,14:80,12:10*PassengerSeatLarge=8:20,1:20*PassengerSeatSmall=8:20,1:20$Gyro*LargeBlockGyro=0:600,1:40,9:4,7:50,2:4,4:5*SmallBlockGyro=0:25,1:5,9:1,2:2,4:3$Kitchen*LargeBlockKitchen=8:20,1:30,9:6,2:6,14:4$CryoChamber*LargeBlockBed=8:30,1:30,12:8,14:10*LargeBlockCryoChamber=8:40,1:20,2:8,3:8,19:3,4:30,14:10*SmallBlockCryoChamber=8:20,1:10,2:4,3:4,19:3,4:15,14:5$CargoContainer*LargeBlockLockerRoom=8:30,1:30,3:4,14:10*LargeBlockLockerRoomCorner=8:25,1:30,3:4,14:10*LargeBlockLockers=8:20,1:20,3:3,4:2*SmallBlockSmallContainer=8:3,1:1,4:1,2:1,3:1*SmallBlockMediumContainer=8:30,1:10,4:4,2:4,3:1*SmallBlockLargeContainer=8:75,1:25,4:6,2:8,3:1*LargeBlockSmallContainer=8:40,1:40,7:4,12:20,2:4,3:1,4:2*LargeBlockLargeContainer=8:360,1:80,7:24,12:60,2:20,3:1,4:8$Planter*LargeBlockPlanters=8:10,1:20,12:8,14:8$Door*(null)=8:10,1:40,12:4,2:2,3:1,4:2,0:8$AirtightHangarDoor*(null)=0:350,1:40,12:40,2:16,4:2$AirtightSlideDoor*LargeBlockSlideDoor=0:20,1:40,12:4,2:4,3:1,4:2,14:15$BatteryBlock*LargeBlockBatteryBlock=0:80,1:30,15:80,4:25*SmallBlockBatteryBlock=0:25,1:5,15:20,4:2*SmallBlockSmallBatteryBlock=0:4,1:2,15:2,4:2$Reactor*SmallBlockSmallGenerator=0:3,1:10,7:2,9:1,16:3,2:1,4:10*SmallBlockLargeGenerator=0:60,1:9,7:9,9:3,16:95,2:5,4:25*LargeBlockSmallGenerator=0:80,1:40,7:4,9:8,16:100,2:6,4:25*LargeBlockLargeGenerator=0:1000,1:70,7:40,9:40,13:100,16:2000,2:20,4:75$HydrogenEngine*LargeHydrogenEngine=0:100,1:70,9:12,12:20,2:12,4:4,15:1*SmallHydrogenEngine=0:30,1:20,9:4,12:6,2:4,4:1,15:1$WindTurbine*LargeBlockWindTurbine=8:40,2:8,1:20,17:24,4:2$SolarPanel*LargeBlockSolarPanel=0:4,1:14,17:12,4:4,18:32,14:4*SmallBlockSolarPanel=0:2,1:2,17:4,4:1,18:8,14:1$GravityGenerator*(null)=0:150,5:6,1:60,9:4,2:6,4:40$GravityGeneratorSphere*(null)=0:150,5:6,1:60,9:4,2:6,4:40$VirtualMass*VirtualMassLarge=0:90,13:20,1:30,4:20,5:9*VirtualMassSmall=0:3,13:2,1:2,4:2,5:1$SpaceBall*SpaceBallLarge=0:225,1:30,4:20,5:3*SpaceBallSmall=0:70,1:10,4:7,5:1$Passage*(null)=8:74,1:20,12:48$Ladder2*(null)=8:10,1:20,12:10$TextPanel*SmallTextPanel=8:1,1:4,4:4,3:3,14:1*SmallLCDPanelWide=8:1,1:8,4:8,3:6,14:2*SmallLCDPanel=8:1,1:4,4:4,3:3,14:2*LargeBlockCorner_LCD_1=1:5,4:3,3:1*LargeBlockCorner_LCD_2=1:5,4:3,3:1*LargeBlockCorner_LCD_Flat_1=1:5,4:3,3:1*LargeBlockCorner_LCD_Flat_2=1:5,4:3,3:1*SmallBlockCorner_LCD_1=1:3,4:2,3:1*SmallBlockCorner_LCD_2=1:3,4:2,3:1*SmallBlockCorner_LCD_Flat_1=1:3,4:2,3:1*SmallBlockCorner_LCD_Flat_2=1:3,4:2,3:1*LargeTextPanel=8:1,1:6,4:6,3:10,14:2*LargeLCDPanel=8:1,1:6,4:6,3:10,14:6*LargeLCDPanelWide=8:2,1:12,4:12,3:20,14:12$ReflectorLight*LargeBlockFrontLight=0:8,9:2,8:20,1:15,14:4*SmallBlockFrontLight=0:1,9:1,8:1,1:1,14:2$InteriorLight*SmallLight=1:2*SmallBlockSmallLight=1:2*LargeBlockLight_1corner=1:3*LargeBlockLight_2corner=1:6*SmallBlockLight_1corner=1:2*SmallBlockLight_2corner=1:4$OxygenTank*OxygenTankSmall=0:16,9:8,12:10,4:8,1:10*(null)=0:80,9:40,12:60,4:8,1:40*LargeHydrogenTank=0:280,9:80,12:60,4:8,1:40*SmallHydrogenTank=0:80,9:40,12:60,4:8,1:40$AirVent*(null)=0:45,1:20,2:10,4:5*SmallAirVent=0:8,1:10,2:2,4:5$Conveyor*SmallBlockConveyor=8:4,1:4,2:1*LargeBlockConveyor=8:20,1:30,12:20,2:6*SmallShipConveyorHub=8:25,1:45,12:25,2:2$Collector*Collector=0:45,1:50,12:12,2:8,3:4,4:10*CollectorSmall=0:35,1:35,12:12,2:8,3:2,4:8$ShipConnector*Connector=0:150,1:40,12:12,2:8,4:20*ConnectorSmall=0:7,1:4,12:2,2:1,4:4*ConnectorMedium=0:21,1:12,12:6,2:6,4:6$ConveyorConnector*ConveyorTube=8:14,1:20,12:12,2:6*ConveyorTubeSmall=8:1,2:1,1:1*ConveyorTubeMedium=8:10,1:20,12:10,2:6*ConveyorFrameMedium=8:5,1:12,12:5,2:2*ConveyorTubeCurved=8:14,1:20,12:12,2:6*ConveyorTubeSmallCurved=8:1,2:1,1:1*ConveyorTubeCurvedMedium=8:7,1:20,12:10,2:6$ConveyorSorter*LargeBlockConveyorSorter=8:50,1:120,12:50,4:20,2:2*MediumBlockConveyorSorter=8:5,1:12,12:5,4:5,2:2*SmallBlockConveyorSorter=8:5,1:12,12:5,4:5,2:2$PistonBase*LargePistonBase=0:15,1:10,9:4,2:4,4:2*SmallPistonBase=0:4,1:4,12:4,2:2,4:1$ExtendedPistonBase*LargePistonBase=0:15,1:10,9:4,2:4,4:2*SmallPistonBase=0:4,1:4,12:4,2:2,4:1$PistonTop*LargePistonTop=0:10,9:8*SmallPistonTop=0:4,9:2$MotorStator*LargeStator=0:15,1:10,9:4,2:4,4:2*SmallStator=0:5,1:5,12:1,2:1,4:1$MotorRotor*LargeRotor=0:30,9:6*SmallRotor=0:12,12:6$MotorAdvancedStator*LargeAdvancedStator=0:15,1:10,9:4,2:4,4:2*SmallAdvancedStator=0:5,1:5,12:1,2:1,4:1$MotorAdvancedRotor*LargeAdvancedRotor=0:30,9:10*SmallAdvancedRotor=0:30,9:10$MedicalRoom*LargeMedicalRoom=8:240,1:80,7:60,12:20,9:5,3:10,4:10,19:15$Refinery*LargeRefinery=0:1200,1:40,9:20,2:16,7:20,4:20*BlastFurnace=0:120,1:20,2:10,4:10$OxygenGenerator*(null)=0:120,1:5,9:2,2:4,4:5*OxygenGeneratorSmall=0:8,1:8,9:2,2:1,4:3$Assembler*LargeAssembler=0:140,1:80,2:20,3:10,7:10,4:160*BasicAssembler=0:80,1:40,2:10,3:4,4:80$SurvivalKit*SurvivalKitLarge=0:30,1:2,19:3,2:4,3:1,4:5*SurvivalKit=0:6,1:2,19:3,2:4,3:1,4:5$OxygenFarm*LargeBlockOxygenFarm=0:40,14:100,9:20,12:10,1:20,4:20$UpgradeModule*LargeProductivityModule=0:100,1:40,12:20,4:60,2:4*LargeEffectivenessModule=0:100,1:50,12:15,13:20,2:4*LargeEnergyModule=0:100,1:40,12:20,15:20,2:4$Thrust*SmallBlockSmallThrust=0:2,1:2,9:1,20:1*SmallBlockLargeThrust=0:5,1:2,9:5,20:12*LargeBlockSmallThrust=0:25,1:60,9:8,20:80*LargeBlockLargeThrust=0:150,1:100,9:40,20:960*LargeBlockLargeHydrogenThrust=0:150,1:180,7:250,9:40*LargeBlockSmallHydrogenThrust=0:25,1:60,7:40,9:8*SmallBlockLargeHydrogenThrust=0:30,1:30,7:22,9:10*SmallBlockSmallHydrogenThrust=0:7,1:15,7:4,9:2*LargeBlockLargeAtmosphericThrust=0:230,1:60,9:50,7:40,2:1100*LargeBlockSmallAtmosphericThrust=0:35,1:50,9:8,7:10,2:110*SmallBlockLargeAtmosphericThrust=0:20,1:30,9:4,7:8,2:90*SmallBlockSmallAtmosphericThrust=0:3,1:22,9:1,7:1,2:18$Drill*SmallBlockDrill=0:32,1:30,9:4,2:1,4:1*LargeBlockDrill=0:300,1:40,9:12,2:5,4:5$ShipGrinder*LargeShipGrinder=0:20,1:30,9:1,2:4,4:2*SmallShipGrinder=0:12,1:17,12:4,2:4,4:2$ShipWelder*LargeShipWelder=0:20,1:30,9:1,2:2,4:2*SmallShipWelder=0:12,1:17,12:6,2:2,4:2$OreDetector*LargeOreDetector=0:50,1:40,2:5,4:25,11:20*SmallBlockOreDetector=0:3,1:2,2:1,4:1,11:1$LandingGear*LargeBlockLandingGear=0:150,1:20,2:6*SmallBlockLandingGear=0:2,1:5,2:1$JumpDrive*LargeJumpDrive=0:60,7:50,5:20,11:20,15:120,13:1000,4:300,1:40$CameraBlock*SmallCameraBlock=0:2,4:3*LargeCameraBlock=0:2,4:3$MergeBlock*LargeShipMergeBlock=0:12,1:15,2:2,9:6,4:2*SmallShipMergeBlock=0:4,1:5,2:1,12:2,4:1$Parachute*LgParachute=0:9,1:25,12:5,2:3,4:2*SmParachute=0:2,1:2,12:1,2:1,4:1$Warhead*LargeWarhead=0:20,17:24,1:12,12:12,4:2,21:6*SmallWarhead=0:4,17:1,1:1,12:2,4:1,21:2$Decoy*LargeDecoy=0:30,1:10,4:10,10:1,9:2*SmallDecoy=0:2,1:1,4:1,10:1,12:2$LargeGatlingTurret*(null)=0:20,1:30,7:15,12:6,2:8,4:10*SmallGatlingTurret=0:10,1:30,7:5,12:6,2:4,4:10$LargeMissileTurret*(null)=0:20,1:40,7:5,9:6,2:16,4:12*SmallMissileTurret=0:10,1:40,7:2,9:2,2:8,4:12$InteriorTurret*LargeInteriorTurret=8:6,1:20,12:1,2:2,4:5,0:4$SmallMissileLauncher*(null)=0:4,1:2,7:1,9:4,2:1,4:1*LargeMissileLauncher=0:35,1:8,7:30,9:25,2:6,4:4$SmallMissileLauncherReload*SmallRocketLauncherReload=12:50,8:50,1:24,9:8,7:10,2:4,4:2,0:8$SmallGatlingGun*(null)=0:4,1:1,7:2,12:6,2:1,4:1$MotorSuspension*Suspension3x3=0:25,1:15,9:6,12:12,2:6*Suspension5x5=0:70,1:40,9:20,12:30,2:20*Suspension1x1=0:25,1:15,9:6,12:12,2:6*SmallSuspension3x3=0:8,1:7,12:2,2:1*SmallSuspension5x5=0:16,1:12,12:4,2:2*SmallSuspension1x1=0:8,1:7,12:2,2:1*Suspension3x3mirrored=0:25,1:15,9:6,12:12,2:6*Suspension5x5mirrored=0:70,1:40,9:20,12:30,2:20*Suspension1x1mirrored=0:25,1:15,9:6,12:12,2:6*SmallSuspension3x3mirrored=0:8,1:7,12:2,2:1*SmallSuspension5x5mirrored=0:16,1:12,12:4,2:2*SmallSuspension1x1mirrored=0:8,1:7,12:2,2:1$Wheel*SmallRealWheel1x1=0:2,1:5,9:1*SmallRealWheel=0:5,1:10,9:1*SmallRealWheel5x5=0:7,1:15,9:2*RealWheel1x1=0:8,1:20,9:4*RealWheel=0:12,1:25,9:6*RealWheel5x5=0:16,1:30,9:8*SmallRealWheel1x1mirrored=0:2,1:5,9:1*SmallRealWheelmirrored=0:5,1:10,9:1*SmallRealWheel5x5mirrored=0:7,1:15,9:2*RealWheel1x1mirrored=0:8,1:20,9:4*RealWheelmirrored=0:12,1:25,9:6*RealWheel5x5mirrored=0:16,1:30,9:8*Wheel1x1=0:8,1:20,9:4*SmallWheel1x1=0:2,1:5,9:1*Wheel3x3=0:12,1:25,9:6*SmallWheel3x3=0:5,1:10,9:1*Wheel5x5=0:16,1:30,9:8*SmallWheel5x5=0:7,1:15,9:2";
        // the line above this one is really long
    }
}