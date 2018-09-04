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
        private const string arcFurnaceEffUsed = "^Arc Furnace conversion rate";
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
            ["ConstructionComponent"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 10 },
            ["DetectorComponent"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 15, [Ingots.Nickel] = 5 },
            ["Display"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 1, [Ingots.Silicon] = 5 },
            ["ExplosivesComponent"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Silicon] = FP("0.5"), [Ingots.Magnesium] = 2 },
            ["GirderComponent"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 7 },
            ["GravityGeneratorComponent"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 600, [Ingots.Silver] = 5, [Ingots.Gold] = 10, [Ingots.Cobalt] = 220 },
            ["InteriorPlate"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = FP("3.5") },
            ["LargeTube"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 30 },
            ["MedicalComponent"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 60, [Ingots.Nickel] = 70, [Ingots.Silver] = 20 },
            ["MetalGrid"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 12, [Ingots.Nickel] = 5, [Ingots.Cobalt] = 3 },
            ["MotorComponent"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 20, [Ingots.Nickel] = 5 },
            ["PowerCell"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 10, [Ingots.Nickel] = 2, [Ingots.Silicon] = 1 },
            ["RadioCommunicationComponent"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 8, [Ingots.Silicon] = 1 },
            ["ReactorComponent"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 15, [Ingots.Stone] = 20, [Ingots.Silver] = 5 },
            ["SmallTube"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 5 },
            ["SolarCell"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Nickel] = 10, [Ingots.Silicon] = 8 },
            ["SteelPlate"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 21 },
            ["Superconductor"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 10, [Ingots.Gold] = 2 },
            ["ThrustComponent"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 30, [Ingots.Cobalt] = 10, [Ingots.Gold] = 1, [Ingots.Platinum] = FP("0.4") },
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

        private readonly Ores[] arcFurnaceOres = new Ores[] { Ores.Iron, Ores.Nickel, Ores.Cobalt, Ores.Scrap };
        
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
            [Ores.Ice] = 0, // ice conversion rate is not refined in refinery or arc furnace
            [Ores.Iron] = FP("0.7"),
            [Ores.Magnesium] = FP("0.007"),
            [Ores.Nickel] = FP("0.4"),
            [Ores.Platinum] = FP("0.005"),
            [Ores.Scrap] = FP("0.8"),
            [Ores.Silicon] = FP("0.7"),
            [Ores.Silver] = FP("0.1"),
            [Ores.Stone] = FP("0.9"),
            [Ores.Uranium] = FP("0.007"),
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
            foreach (var ore in arcFurnaceOres)
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

                string blockName = blockInfo[0];
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
            public bool arcFurnace;
        }

        private ConversionData GetConversionData(Ores ore)
        {
            var refConvRate = Math.Min(1f, 0.8f * (float)conversionRates[ore] * (float)effectivenessMultiplier);
            var ret = new ConversionData { conversionRate = refConvRate, arcFurnace = false };
            if (arcFurnaceOres.Contains(ore))
            {
                var arcConvRate = Math.Min(1f, 0.9f * (float)conversionRates[ore]); // Arc Furnace has no yield ports
                // if there are both refineries and arc furnace, or there is neither, we prefer the best yield
                // or we prefer arc furnace rate when there is one but no refinery
                if ((arcConvRate > refConvRate && (atLeastOneArcFurnace == atLeastOneRefinery)) || (atLeastOneArcFurnace && !atLeastOneRefinery))
                {
                    ret.conversionRate = arcConvRate;
                    ret.arcFurnace = true;
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
            lcd.WritePublicText(text);
            lcd.ShowPublicTextOnScreen();

            if (!autoResizeText || lcd.Font != monospaceFontName)
                return;

            Size size = GetOutputSize(text);
            if (size.Width == 0)
                return;

            LCDType type = GetLCDType(lcd);
            float maxWidth = type == LCDType.WIDE ? wideLCDWidth : LCDWidth;
            float maxHeight = type == LCDType.WIDE ? wideLCDHeight : LCDHeight;

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
        //private bool arcFurnaceWithNoRefinery = false;
        private bool atLeastOneRefinery = false, atLeastOneArcFurnace = false;
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
            List<IMyRefinery> arcFurnaces = new List<IMyRefinery>();
            foreach (var x in allRefineries)
                if (x.BlockDefinition.SubtypeName == "Blast Furnace")
                    arcFurnaces.Add(x);
                else
                    refineries.Add(x);

            atLeastOneRefinery = refineries.Count > 0;
            atLeastOneArcFurnace = arcFurnaces.Count > 0;

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
            foreach (var b in cubeBlocks)
            {
                if (b.HasInventory)
                {
                    for (int i = 0; i < b.InventoryCount; i++)
                    {
                        var itemList = b.GetInventory(i).GetItems();
                        foreach (var item in itemList)
                        {
                            if (item.Content.TypeId.ToString().Equals("MyObjectBuilder_Component"))
                            {
                                AddCountToDict(componentAmounts, item.Content.SubtypeId.ToString(), item.Amount);
                            }
                            else if (item.Content.TypeId.ToString().Equals("MyObjectBuilder_Ingot"))
                            {
                                AddCountToDict(ingotAmounts, (Ingots)Enum.Parse(typeof(Ingots), item.Content.SubtypeId.ToString()), item.Amount);
                            }
                            else if (item.Content.TypeId.ToString().Equals("MyObjectBuilder_Ore"))
                            {
                                AddCountToDict(oreAmounts, (Ores)Enum.Parse(typeof(Ores), item.Content.SubtypeId.ToString()), item.Amount);
                            }
                        }
                    }
                }
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
                if (conversionData[ores.Key].arcFurnace)
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
                output += (scrapOutput == "" ? "\n" : "") + arcFurnaceEffUsed + (refineries.Count == 0 ? noRefineryFound : betterYield) + "\n";

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
                ShowAndSetFontSize(lcd2, lcd2.GetPublicText() + output);
            }
            Me.CustomData += output + "\n\n";
        }

        string blockDefinitionData = "SteelPlate*Superconductor*ConstructionComponent*PowerCell*ComputerComponent*MetalGrid*Display*LargeTube*MotorComponent*InteriorPlate*SmallTube*RadioCommunicationComponent*BulletproofGlass*GirderComponent*ExplosivesComponent*DetectorComponent*MedicalComponent*GravityGeneratorComponent*ThrustComponent*ReactorComponent*SolarCell$CubeBlock*Monolith=0:130,1:130*Stereolith=0:130,1:130*DeadAstronaut=0:13,1:13*LargeBlockArmorBlock=0:25*LargeBlockArmorSlope=0:13*LargeBlockArmorCorner=0:4*LargeBlockArmorCornerInv=0:21*LargeRoundArmor_Slope=0:13*LargeRoundArmor_Corner=0:4*LargeRoundArmor_CornerInv=0:21*LargeHeavyBlockArmorBlock=0:150,5:50*LargeHeavyBlockArmorSlope=0:75,5:25*LargeHeavyBlockArmorCorner=0:25,5:10*LargeHeavyBlockArmorCornerInv=0:125,5:50*SmallBlockArmorBlock=0:1*SmallBlockArmorSlope=0:1*SmallBlockArmorCorner=0:1*SmallBlockArmorCornerInv=0:1*SmallHeavyBlockArmorBlock=0:5,5:2*SmallHeavyBlockArmorSlope=0:3,5:1*SmallHeavyBlockArmorCorner=0:2,5:1*SmallHeavyBlockArmorCornerInv=0:4,5:1*LargeBlockArmorRoundedSlope=0:25*LargeBlockArmorRoundedCorner=0:25*LargeBlockArmorAngledSlope=0:13*LargeBlockArmorAngledCorner=0:4*LargeHeavyBlockArmorRoundedSlope=0:130,5:50*LargeHeavyBlockArmorRoundedCorner=0:125,5:40*LargeHeavyBlockArmorAngledSlope=0:75,5:25*LargeHeavyBlockArmorAngledCorner=0:30,5:8*SmallBlockArmorRoundedSlope=0:1*SmallBlockArmorRoundedCorner=0:1*SmallBlockArmorAngledSlope=0:1*SmallBlockArmorAngledCorner=0:1*SmallHeavyBlockArmorRoundedSlope=0:4,5:1*SmallHeavyBlockArmorRoundedCorner=0:4,5:1*SmallHeavyBlockArmorAngledSlope=0:3,5:1*SmallHeavyBlockArmorAngledCorner=0:3,5:1*LargeBlockArmorRoundSlope=0:13*LargeBlockArmorRoundCorner=0:4*LargeBlockArmorRoundCornerInv=0:21*LargeHeavyBlockArmorRoundSlope=0:130,5:50*LargeHeavyBlockArmorRoundCorner=0:125,5:40*LargeHeavyBlockArmorRoundCornerInv=0:140,5:50*SmallBlockArmorRoundSlope=0:1*SmallBlockArmorRoundCorner=0:1*SmallBlockArmorRoundCornerInv=0:1*SmallHeavyBlockArmorRoundSlope=0:4,5:1*SmallHeavyBlockArmorRoundCorner=0:4,5:1*SmallHeavyBlockArmorRoundCornerInv=0:5,5:1*LargeBlockArmorSlope2BaseSmooth=0:19*LargeBlockArmorSlope2TipSmooth=0:7*LargeBlockArmorCorner2BaseSmooth=0:10*LargeBlockArmorCorner2TipSmooth=0:4*LargeBlockArmorInvCorner2BaseSmooth=0:22*LargeBlockArmorInvCorner2TipSmooth=0:16*LargeHeavyBlockArmorSlope2BaseSmooth=0:112,5:40*LargeHeavyBlockArmorSlope2TipSmooth=0:35,5:10*LargeHeavyBlockArmorCorner2BaseSmooth=0:55,5:15*LargeHeavyBlockArmorCorner2TipSmooth=0:19,5:6*LargeHeavyBlockArmorInvCorner2BaseSmooth=0:133,5:50*LargeHeavyBlockArmorInvCorner2TipSmooth=0:94,5:25*SmallBlockArmorSlope2BaseSmooth=0:1*SmallBlockArmorSlope2TipSmooth=0:1*SmallBlockArmorCorner2BaseSmooth=0:1*SmallBlockArmorCorner2TipSmooth=0:1*SmallBlockArmorInvCorner2BaseSmooth=0:1*SmallBlockArmorInvCorner2TipSmooth=0:1*SmallHeavyBlockArmorSlope2BaseSmooth=0:5,5:1*SmallHeavyBlockArmorSlope2TipSmooth=0:2,5:1*SmallHeavyBlockArmorCorner2BaseSmooth=0:3,5:1*SmallHeavyBlockArmorCorner2TipSmooth=0:2,5:1*SmallHeavyBlockArmorInvCorner2BaseSmooth=0:5,5:1*SmallHeavyBlockArmorInvCorner2TipSmooth=0:2,5:1*LargeBlockArmorSlope2Base=0:19*LargeBlockArmorSlope2Tip=0:7*LargeBlockArmorCorner2Base=0:10*LargeBlockArmorCorner2Tip=0:4*LargeBlockArmorInvCorner2Base=0:22*LargeBlockArmorInvCorner2Tip=0:16*LargeHeavyBlockArmorSlope2Base=0:112,5:45*LargeHeavyBlockArmorSlope2Tip=0:35,5:6*LargeHeavyBlockArmorCorner2Base=0:55,5:15*LargeHeavyBlockArmorCorner2Tip=0:19,5:6*LargeHeavyBlockArmorInvCorner2Base=0:133,5:45*LargeHeavyBlockArmorInvCorner2Tip=0:94,5:25*SmallBlockArmorSlope2Base=0:1*SmallBlockArmorSlope2Tip=0:1*SmallBlockArmorCorner2Base=0:1*SmallBlockArmorCorner2Tip=0:1*SmallBlockArmorInvCorner2Base=0:1*SmallBlockArmorInvCorner2Tip=0:1*SmallHeavyBlockArmorSlope2Base=0:4,5:1*SmallHeavyBlockArmorSlope2Tip=0:2,5:1*SmallHeavyBlockArmorCorner2Base=0:3,5:1*SmallHeavyBlockArmorCorner2Tip=0:2,5:1*SmallHeavyBlockArmorInvCorner2Base=0:5,5:1*SmallHeavyBlockArmorInvCorner2Tip=0:5,5:1*LargeWindowSquare=9:12,2:8,10:4*LargeWindowEdge=9:16,2:12,10:6*LargeStairs=9:50,2:30*LargeRamp=9:70,2:16*LargeSteelCatwalk=9:27,2:5,10:20*LargeSteelCatwalk2Sides=9:32,2:7,10:25*LargeSteelCatwalkCorner=9:32,2:7,10:25*LargeSteelCatwalkPlate=9:23,2:7,10:17*LargeCoverWall=0:4,2:10*LargeCoverWallHalf=0:2,2:6*LargeBlockInteriorWall=9:25,2:10*LargeInteriorPillar=9:25,2:10,10:4*LargeRailStraight=0:12,2:8,7:4*Window1x2Slope=13:16,12:55*Window1x2Inv=13:15,12:40*Window1x2Face=13:15,12:40*Window1x2SideLeft=13:13,12:26*Window1x2SideLeftInv=13:13,12:26*Window1x2SideRight=13:13,12:26*Window1x2SideRightInv=13:13,12:26*Window1x1Slope=13:12,12:35*Window1x1Face=13:11,12:24*Window1x1Side=13:9,12:17*Window1x1SideInv=13:9,12:17*Window1x1Inv=13:11,12:24*Window1x2Flat=13:15,12:50*Window1x2FlatInv=13:15,12:50*Window1x1Flat=13:10,12:25*Window1x1FlatInv=13:10,12:25*Window3x3Flat=13:40,12:196*Window3x3FlatInv=13:40,12:196*Window2x3Flat=13:25,12:140*Window2x3FlatInv=13:25,12:140*ArmorAlpha=0:8,5:8,2:40,7:4*ArmorCenter=0:140*ArmorCorner=0:120*ArmorInvCorner=0:135*ArmorSide=0:130*SmallArmorCenter=0:5*SmallArmorCorner=0:5*SmallArmorInvCorner=0:5*SmallArmorSide=0:5$BatteryBlock*LargeBlockBatteryBlock=0:80,2:30,3:120,4:25*SmallBlockBatteryBlock=0:25,2:5,3:20,4:2$TerminalBlock*ControlPanel=0:1,2:1,4:1,6:1*SmallControlPanel=0:1,2:1,4:1,6:1$MyProgrammableBlock*SmallProgrammableBlock=0:2,2:2,7:2,8:1,6:1,4:2*LargeProgrammableBlock=0:21,2:4,7:2,8:1,6:1,4:2$LargeGatlingTurret*(null)=0:20,2:30,5:15,7:1,8:8,4:10*SmallGatlingTurret=0:10,2:30,5:5,7:1,8:4,4:10$LargeMissileTurret*(null)=0:20,2:40,5:5,7:6,8:16,4:12*SmallMissileTurret=0:10,2:40,5:2,7:2,8:8,4:12$InteriorTurret*LargeInteriorTurret=9:6,2:20,10:2,7:1,8:2,4:5,0:4$Passage*(null)=9:74,2:20,10:48$Door*(null)=0:8,9:10,2:40,10:4,8:2,6:1,4:2$RadioAntenna*LargeBlockRadioAntenna=0:80,2:40,10:60,7:40,4:8,11:40*SmallBlockRadioAntenna=0:1,10:1,4:1,2:2,11:4$Beacon*LargeBlockBeacon=0:80,2:40,10:60,7:40,4:8,11:40*SmallBlockBeacon=0:2,2:1,10:1,4:1$ReflectorLight*LargeBlockFrontLight=0:8,9:20,2:20,7:2,12:2*SmallBlockFrontLight=0:1,2:1,9:1$InteriorLight*SmallLight=0:1,2:1*SmallBlockSmallLight=0:1,2:1*LargeBlockLight_1corner=2:3*LargeBlockLight_2corner=2:6*SmallBlockLight_1corner=2:2*SmallBlockLight_2corner=2:4$Warhead*LargeWarhead=0:20,13:24,2:12,10:12,4:2,14:6*SmallWarhead=0:4,13:1,2:1,10:2,4:1,14:2$Decoy*LargeDecoy=0:3,2:1,4:2,11:1,7:2*SmallDecoy=0:1,2:1,4:1,11:1,10:2,13:1$LandingGear*LargeBlockLandingGear=0:150,2:10,7:4,8:6*SmallBlockLandingGear=0:2,2:1,7:1,8:1$Projector*LargeProjector=0:21,2:4,7:2,8:1,4:2*SmallProjector=0:2,2:2,7:2,8:1,4:2$Refinery*LargeRefinery=0:1200,2:40,7:20,8:16,4:20*BlastFurnace=0:120,2:5,7:2,8:4,4:5$OxygenGenerator*(null)=0:120,2:5,7:2,8:4,4:5*OxygenGeneratorSmall=0:8,2:8,8:1,7:2,4:3$Assembler*LargeAssembler=0:150,2:40,8:8,6:4,4:80$OreDetector*LargeOreDetector=0:50,2:40,8:5,4:25,15:25*SmallBlockOreDetector=0:3,2:2,8:1,4:1,15:1$MedicalRoom*LargeMedicalRoom=9:240,2:80,5:60,10:20,7:5,6:10,4:10,16:15$GravityGenerator*(null)=0:150,17:6,2:60,7:4,8:6,4:40$GravityGeneratorSphere*(null)=0:150,17:6,2:60,7:4,8:6,4:40$JumpDrive*LargeJumpDrive=0:40,7:40,5:50,17:20,15:20,3:120,1:1000,4:300,2:40$Cockpit*LargeBlockCockpit=9:20,2:20,8:2,4:100,6:10,12:10*LargeBlockCockpitSeat=0:30,2:20,8:1,6:8,4:100,12:60*SmallBlockCockpit=0:10,2:10,8:1,6:5,4:15,12:30*DBSmallBlockFighterCockpit=0:20,2:20,8:1,5:10,9:15,6:4,4:20,12:40*CockpitOpen=9:20,2:20,8:2,4:100,6:4*PassengerSeatLarge=9:20,2:20*PassengerSeatSmall=9:20,2:20$CryoChamber*LargeBlockCryoChamber=9:40,2:20,8:8,6:8,4:30,12:10$SmallMissileLauncher*(null)=0:4,2:2,5:1,7:4,8:1,4:1*LargeMissileLauncher=0:35,2:8,5:30,7:25,8:6,4:4$SmallMissileLauncherReload*SmallRocketLauncherReload=0:8,10:50,9:50,2:24,7:8,5:10,8:4,4:2$SmallGatlingGun*(null)=0:4,2:1,5:2,10:3,8:1,4:1$Drill*SmallBlockDrill=0:32,2:30,7:4,8:1,4:1*LargeBlockDrill=0:300,2:40,10:24,7:12,8:5,4:5$SensorBlock*SmallBlockSensor=9:5,2:5,4:6,11:4,15:6,0:2*LargeBlockSensor=9:5,2:5,4:6,11:4,15:6,0:2$SoundBlock*SmallBlockSoundBlock=9:4,2:6,4:3*LargeBlockSoundBlock=9:15,2:10,4:15$TextPanel*SmallTextPanel=9:1,2:4,4:4,6:3*SmallLCDPanelWide=9:1,2:8,4:8,6:6,12:2*SmallLCDPanel=9:1,2:4,4:4,6:3*LargeBlockCorner_LCD_1=2:5,4:3,6:1*LargeBlockCorner_LCD_2=2:5,4:3,6:1*LargeBlockCorner_LCD_Flat_1=2:5,4:3,6:1*LargeBlockCorner_LCD_Flat_2=2:5,4:3,6:1*SmallBlockCorner_LCD_1=2:3,4:2,6:1*SmallBlockCorner_LCD_2=2:3,4:2,6:1*SmallBlockCorner_LCD_Flat_1=2:3,4:2,6:1*SmallBlockCorner_LCD_Flat_2=2:3,4:2,6:1*LargeTextPanel=9:1,2:6,4:6,6:10*LargeLCDPanel=9:1,2:6,4:6,6:10,12:6*LargeLCDPanelWide=9:2,2:12,4:12,6:20,12:12$OxygenTank*OxygenTankSmall=0:14,2:10,10:10,7:2,4:3*(null)=0:80,7:40,10:60,4:8,2:40*LargeHydrogenTank=0:280,7:80,10:60,4:8,2:40*SmallHydrogenTank=0:80,7:40,10:60,4:8,2:40$RemoteControl*LargeBlockRemoteControl=9:10,2:10,8:1,4:15*SmallBlockRemoteControl=9:2,2:1,8:1,4:1$AirVent*(null)=0:80,2:30,5:5,4:5*SmallAirVent=0:8,2:10,5:2,4:5$UpgradeModule*LargeProductivityModule=0:100,2:40,10:20,7:10,8:2*LargeEffectivenessModule=0:100,2:50,10:15,5:10,8:5*LargeEnergyModule=0:100,2:40,10:20,7:10,8:2$CargoContainer*SmallBlockSmallContainer=9:3,2:1,4:1,8:1,6:1*SmallBlockMediumContainer=9:30,2:10,4:4,8:4,6:1*SmallBlockLargeContainer=9:75,2:25,4:6,8:8,6:1*LargeBlockSmallContainer=9:40,2:40,5:4,10:20,8:4,6:1,4:2*LargeBlockLargeContainer=9:360,2:80,5:24,10:60,8:20,6:1,4:8$Thrust*SmallBlockSmallThrust=0:2,7:1,18:1,2:1*SmallBlockLargeThrust=0:5,2:2,7:5,18:12*LargeBlockSmallThrust=0:25,2:60,7:8,18:80*LargeBlockLargeThrust=0:150,2:100,7:40,18:960*LargeBlockLargeHydrogenThrust=0:150,2:180,5:250,7:40*LargeBlockSmallHydrogenThrust=0:25,2:60,5:40,7:8*SmallBlockLargeHydrogenThrust=0:30,2:30,5:22,7:10*SmallBlockSmallHydrogenThrust=0:7,2:15,5:4,7:2*LargeBlockLargeAtmosphericThrust=0:230,2:60,7:50,5:40,8:1136*LargeBlockSmallAtmosphericThrust=0:35,2:50,7:8,5:10,8:113*SmallBlockLargeAtmosphericThrust=0:20,2:30,7:4,5:8,8:144*SmallBlockSmallAtmosphericThrust=0:3,7:1,5:1,8:18,2:2$CameraBlock*SmallCameraBlock=0:2,4:3*LargeCameraBlock=0:2,4:3$Gyro*LargeBlockGyro=0:600,2:40,7:4,5:50,8:4,4:5*SmallBlockGyro=0:25,2:5,7:1,8:2,4:3$Reactor*SmallBlockSmallGenerator=0:3,2:10,5:2,7:1,19:3,8:1,4:10*SmallBlockLargeGenerator=0:60,2:9,5:9,7:3,19:95,8:5,4:25*LargeBlockSmallGenerator=0:80,2:40,5:4,7:8,19:100,8:6,4:25*LargeBlockLargeGenerator=0:1000,2:70,5:40,7:40,1:100,19:2000,8:20,4:75$PistonBase*LargePistonBase=0:15,2:10,7:4,8:4,4:2*SmallPistonBase=0:4,2:4,10:4,8:2,4:1$ExtendedPistonBase*LargePistonBase=0:15,2:10,7:4,8:4,4:2*SmallPistonBase=0:4,2:4,10:4,8:2,4:1$PistonTop*LargePistonTop=0:10,7:8*SmallPistonTop=0:4,7:2$MotorStator*LargeStator=0:15,2:10,7:4,8:4,4:2*SmallStator=0:5,2:5,10:1,8:1,4:1$MotorSuspension*Suspension3x3=0:25,2:15,7:6,10:12,8:6*Suspension5x5=0:70,2:40,7:20,10:30,8:20*Suspension1x1=0:25,2:15,7:6,10:12,8:6*SmallSuspension3x3=0:8,2:7,10:2,8:1*SmallSuspension5x5=0:16,2:12,10:4,8:2*SmallSuspension1x1=0:8,2:7,10:2,8:1*Suspension3x3mirrored=0:25,2:15,7:6,10:12,8:6*Suspension5x5mirrored=0:70,2:40,7:20,10:30,8:20*Suspension1x1mirrored=0:25,2:15,7:6,10:12,8:6*SmallSuspension3x3mirrored=0:8,2:7,10:2,8:1*SmallSuspension5x5mirrored=0:16,2:12,10:4,8:2*SmallSuspension1x1mirrored=0:8,2:7,10:2,8:1$MotorRotor*LargeRotor=0:30,7:24*SmallRotor=0:12,10:6$MotorAdvancedStator*LargeAdvancedStator=0:15,2:10,7:4,8:4,4:2*SmallAdvancedStator=0:5,2:5,10:1,8:1,4:1$MotorAdvancedRotor*LargeAdvancedRotor=0:5,7:4*SmallAdvancedRotor=0:1,10:1$ButtonPanel*ButtonPanelLarge=9:10,2:20,4:20*ButtonPanelSmall=0:2,4:1,9:1$TimerBlock*TimerBlockLarge=9:6,2:30,4:5*TimerBlockSmall=9:2,2:3,4:1$SolarPanel*LargeBlockSolarPanel=0:4,2:10,7:1,4:2,20:64*SmallBlockSolarPanel=0:2,2:2,13:4,4:1,20:16,12:1$OxygenFarm*LargeBlockOxygenFarm=0:40,12:100,7:20,5:10,2:20,4:20$Conveyor*SmallBlockConveyor=9:4,2:4,8:1*LargeBlockConveyor=9:20,2:30,10:20,8:6*SmallShipConveyorHub=9:25,2:70,10:25,8:2$Collector*Collector=0:45,2:50,10:12,8:8,6:4,4:10*CollectorSmall=0:35,2:35,10:12,8:8,6:2,4:8$ShipConnector*Connector=0:150,2:40,10:12,8:8,4:20*ConnectorSmall=0:7,2:4,10:2,8:1,4:4*ConnectorMedium=0:21,2:12,10:6,8:6,4:6$ConveyorConnector*ConveyorTube=9:14,2:20,10:12,8:6*ConveyorTubeSmall=9:1,2:1,8:1*ConveyorTubeMedium=9:8,2:20,10:10,8:6*ConveyorFrameMedium=9:4,2:12,10:5,8:2*ConveyorTubeCurved=9:14,2:20,10:12,8:6*ConveyorTubeSmallCurved=9:1,8:1,2:1*ConveyorTubeCurvedMedium=9:7,2:20,10:10,8:6$ConveyorSorter*LargeBlockConveyorSorter=9:50,2:120,10:50,4:20,8:2*MediumBlockConveyorSorter=9:5,2:12,10:5,4:5,8:2*SmallBlockConveyorSorter=9:5,2:12,10:5,4:5,8:2$VirtualMass*VirtualMassLarge=0:90,1:20,2:30,4:20,17:9*VirtualMassSmall=0:3,1:2,2:2,4:2,17:1$SpaceBall*SpaceBallLarge=0:225,2:30,4:20,17:3*SpaceBallSmall=0:70,2:10,4:7,17:1$Wheel*SmallRealWheel1x1=0:2,2:5,7:1*SmallRealWheel=0:5,2:10,7:1*SmallRealWheel5x5=0:7,2:15,7:2*RealWheel1x1=0:8,2:20,7:4*RealWheel=0:12,2:25,7:6*RealWheel5x5=0:16,2:30,7:8*SmallRealWheel1x1mirrored=0:2,2:5,7:1*SmallRealWheelmirrored=0:5,2:10,7:1*SmallRealWheel5x5mirrored=0:7,2:15,7:2*RealWheel1x1mirrored=0:8,2:20,7:4*RealWheelmirrored=0:12,2:25,7:6*RealWheel5x5mirrored=0:16,2:30,7:8*Wheel1x1=0:8,2:20,7:4*SmallWheel1x1=0:2,2:5,7:1*Wheel3x3=0:12,2:25,7:6*SmallWheel3x3=0:5,2:10,7:1*Wheel5x5=0:16,2:30,7:8*SmallWheel5x5=0:7,2:15,7:2$ShipGrinder*LargeShipGrinder=0:20,2:30,7:1,8:4,4:2*SmallShipGrinder=0:12,2:17,10:4,7:1,8:4,4:2$ShipWelder*LargeShipWelder=0:20,2:30,7:1,8:2,4:2*SmallShipWelder=0:12,2:17,10:6,7:1,8:2,4:2$MergeBlock*LargeShipMergeBlock=0:12,2:17,8:2,10:6,7:1,4:2*SmallShipMergeBlock=0:4,2:5,8:1,10:2,4:1$LaserAntenna*LargeBlockLaserAntenna=0:50,2:40,8:16,15:30,11:20,1:100,4:50,12:4*SmallBlockLaserAntenna=0:10,10:10,2:10,8:5,11:5,1:10,4:30,12:2$AirtightHangarDoor*(null)=0:350,2:40,10:40,8:16,4:2$AirtightSlideDoor*LargeBlockSlideDoor=0:20,2:40,10:4,8:4,6:1,4:2,12:15$Parachute*LgParachute=0:9,2:25,10:5,8:3,4:2*SmParachute=0:2,2:2,10:1,8:1,4:1$DebugSphere1*DebugSphereLarge=0:10,4:20$DebugSphere2*DebugSphereLarge=0:10,4:20$DebugSphere3*DebugSphereLarge=0:10,4:20";
        // the line above this one is really long
    }
}