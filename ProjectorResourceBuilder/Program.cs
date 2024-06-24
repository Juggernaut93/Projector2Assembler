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
        private readonly bool inventoryFromSubgrids = false; // consider inventories on subgrids when computing available materials
        /**********************************************/
        /************ END OF CONFIGURATION ************/
        /**********************************************/

        Dictionary<string, Dictionary<string, int>> blueprints = new Dictionary<string, Dictionary<string, int>>();

        public Program()
        {
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
        }

        public Dictionary<string, int> GetComponents(string definition)
        {
            if (!blueprints.ContainsKey(definition))
            {
                throw new Exception("Unknown Blocks in Blueprint. Go to https://github.com/Juggernaut93/Projector2Assembler, and follow instructions for BlockDefinitionExtractor.");
            }
            return blueprints[definition];
        }

        public void AddComponents(Dictionary<string, int> addTo, Dictionary<string, int> addFrom, int times = 1)
        {
            foreach (KeyValuePair<string, int> component in addFrom)
            {
                if (component.Key.Contains("EngineerPlushie") || component.Key.Contains("SabiroidPlushie")) // Plushie components can't be assembled
                    continue;
                if (addTo.ContainsKey(component.Key))
                    addTo[component.Key] += component.Value * times;
                else
                    addTo[component.Key] = component.Value * times;
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
            //compList.Sort((x, y) => string.Compare(TranslateDef(x.Key), TranslateDef(y.Key)));
            compList.Sort((x, y) => string.Compare(x.Key, y.Key));

            return compList;
        }

        private void AddCountToDict<T>(Dictionary<T, int> dic, T key, int amount)
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

        private int GetCountFromDic<T>(Dictionary<T, int> dic, T key)
        {
            if (dic.ContainsKey(key))
            {
                return dic[key];
            }
            return 0;
        }

        private List<KeyValuePair<string, int>> SubtractPresentComponents(List<KeyValuePair<string, int>> compList)
        {
            var cubeBlocks = new List<IMyCubeBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyCubeBlock>(cubeBlocks, block => block.CubeGrid == Me.CubeGrid || inventoryFromSubgrids);

            Dictionary<string, int> componentAmounts = new Dictionary<string, int>();
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
                                AddCountToDict(componentAmounts, item.Type.SubtypeId, item.Amount.ToIntSafe());
                            }
                        }
                    }
                }
            }

            List<KeyValuePair<string, int>> ret = new List<KeyValuePair<string, int>>();
            foreach (var comp in compList)
            {
                string subTypeId = comp.Key.Replace("MyObjectBuilder_BlueprintDefinition/", "").Replace("Component", "");
                ret.Add(new KeyValuePair<string, int>(comp.Key, Math.Max(0, comp.Value - GetCountFromDic(componentAmounts, subTypeId))));
            }
            return ret;
        }

        private bool lightArmor;

        public void Main(string argument)
        {
            string projectorName = "Projector", assemblerName = "Assembler";
            int staggeringFactor = 10; // set 1 to not stagger
            bool fewFirst = true;
            lightArmor = true;
            bool onlyRemaining = false;

            if (!String.IsNullOrEmpty(argument))
            {
                try
                {
                    var spl = argument.Split(';');
                    if (spl[0] != "")
                        projectorName = spl[0];
                    if (spl.Length > 1)
                        if (spl[1] != "")
                            assemblerName = spl[1];
                    if (spl.Length > 2)
                        if (spl[2] != "")
                            lightArmor = bool.Parse(spl[2]);
                    if (spl.Length > 3)
                        if (spl[3] != "")
                            staggeringFactor = int.Parse(spl[3]);
                    if (spl.Length > 4)
                        if (spl[4] != "")
                            fewFirst = bool.Parse(spl[4]);
                    if (spl.Length > 5)
                        if (spl[5] != "")
                            onlyRemaining = bool.Parse(spl[5]);
                }
                catch (Exception)
                {
                    Echo("Wrong argument(s). Format: [ProjectorName];[AssemblerName];[lightArmor];[staggeringFactor];[fewFirst];[onlyRemaining]. See Readme for more info.");
                    return;
                }
            }

            if (staggeringFactor <= 0)
            {
                Echo("Invalid staggeringFactor: must be an integer greater than 0.");
                return;
            }
            IMyProjector projector = GridTerminalSystem.GetBlockWithName(projectorName) as IMyProjector;
            if (projector == null)
            {
                Echo("The specified projector name is not valid. No projector found.");
                return;
            }

            var compList = GetTotalComponents(projector);
            
            if (onlyRemaining)
            {
                compList = SubtractPresentComponents(compList);
            }

            string output = "";
            foreach (var component in compList)
                output += component.Key.Replace("MyObjectBuilder_BlueprintDefinition/", "") + " " + component.Value.ToString() + "\n";
            Me.CustomData = output;

            IMyAssembler assembler = GridTerminalSystem.GetBlockWithName(assemblerName) as IMyAssembler;
            if (assembler == null)
            {
                Echo("The specified assembler name is not valid. No assembler found.");
                return;
            }

            //var compList = totalComponents.ToList();

            if (fewFirst)
            {
                compList.Sort((pair1, pair2) => pair1.Value.CompareTo(pair2.Value));
            }
            for (int i = 0; i < staggeringFactor; i++)
            {
                foreach (var x in compList)
                {
                    if (x.Key.Contains("ZoneChip"))
                    {
                        continue; // Zone Chips cannot be assembled
                    }
                    int amount = x.Value / staggeringFactor;
                    // if the total amount of the component is not divisible by staggeringFactor, we add the remainder to the first batch
                    // so that if we have < staggeringFactor (or few, in general) units we get them right at the start
                    if (i == 0)
                    {
                        amount += x.Value % staggeringFactor;
                    }
                    if (amount > 0)
                        assembler.AddQueueItem(MyDefinitionId.Parse(x.Key), (decimal)amount);
                }
            }
        }

        string blockDefinitionData = "SteelPlate*ConstructionComponent*LargeTube*ComputerComponent*MetalGrid*MotorComponent*Display*InteriorPlate*RadioCommunicationComponent*DetectorComponent*SmallTube*Superconductor*BulletproofGlass*MedicalComponent*GirderComponent*SolarCell*GravityGeneratorComponent*ExplosivesComponent*ZoneChip*PowerCell*ReactorComponent*EngineerPlushie*SabiroidPlushie*ThrustComponent$CubeBlock*LargeRailStraight=0:12,1:8,2:4*LargeBlockArmorBlock=0:25*LargeBlockArmorSlope=0:13*LargeBlockArmorCorner=0:4*LargeBlockArmorCornerInv=0:21*LargeRoundArmor_Slope=0:13*LargeRoundArmor_Corner=0:4*LargeRoundArmor_CornerInv=0:21*LargeHeavyBlockArmorBlock=0:150,4:50*LargeHeavyBlockArmorSlope=0:75,4:25*LargeHeavyBlockArmorCorner=0:25,4:10*LargeHeavyBlockArmorCornerInv=0:125,4:50*SmallBlockArmorBlock=0:1*SmallBlockArmorSlope=0:1*SmallBlockArmorCorner=0:1*SmallBlockArmorCornerInv=0:1*SmallHeavyBlockArmorBlock=0:5,4:2*SmallHeavyBlockArmorSlope=0:3,4:1*SmallHeavyBlockArmorCorner=0:2,4:1*SmallHeavyBlockArmorCornerInv=0:4,4:1*LargeHalfArmorBlock=0:12*LargeHeavyHalfArmorBlock=0:75,4:25*LargeHalfSlopeArmorBlock=0:4*LargeHeavyHalfSlopeArmorBlock=0:19,4:6*HalfArmorBlock=0:1*HeavyHalfArmorBlock=0:3,4:1*HalfSlopeArmorBlock=0:1*HeavyHalfSlopeArmorBlock=0:2,4:1*LargeBlockArmorRoundSlope=0:13*LargeBlockArmorRoundCorner=0:4*LargeBlockArmorRoundCornerInv=0:21*LargeHeavyBlockArmorRoundSlope=0:130,4:50*LargeHeavyBlockArmorRoundCorner=0:125,4:40*LargeHeavyBlockArmorRoundCornerInv=0:140,4:50*SmallBlockArmorRoundSlope=0:1*SmallBlockArmorRoundCorner=0:1*SmallBlockArmorRoundCornerInv=0:1*SmallHeavyBlockArmorRoundSlope=0:4,4:1*SmallHeavyBlockArmorRoundCorner=0:4,4:1*SmallHeavyBlockArmorRoundCornerInv=0:5,4:1*LargeBlockArmorSlope2Base=0:19*LargeBlockArmorSlope2Tip=0:6*LargeBlockArmorCorner2Base=0:10*LargeBlockArmorCorner2Tip=0:3*LargeBlockArmorInvCorner2Base=0:22*LargeBlockArmorInvCorner2Tip=0:16*LargeHeavyBlockArmorSlope2Base=0:112,4:40*LargeHeavyBlockArmorSlope2Tip=0:40,4:12*LargeHeavyBlockArmorCorner2Base=0:55,4:15*LargeHeavyBlockArmorCorner2Tip=0:19,4:6*LargeHeavyBlockArmorInvCorner2Base=0:133,4:45*LargeHeavyBlockArmorInvCorner2Tip=0:94,4:25*SmallBlockArmorSlope2Base=0:1*SmallBlockArmorSlope2Tip=0:1*SmallBlockArmorCorner2Base=0:1*SmallBlockArmorCorner2Tip=0:1*SmallBlockArmorInvCorner2Base=0:1*SmallBlockArmorInvCorner2Tip=0:1*SmallHeavyBlockArmorSlope2Base=0:4,4:2*SmallHeavyBlockArmorSlope2Tip=0:2,4:1*SmallHeavyBlockArmorCorner2Base=0:3,4:1*SmallHeavyBlockArmorCorner2Tip=0:2,4:1*SmallHeavyBlockArmorInvCorner2Base=0:5,4:1*SmallHeavyBlockArmorInvCorner2Tip=0:5,4:1*LargeArmorPanelLight=0:5*LargeArmorCenterPanelLight=0:5*LargeArmorSlopedSidePanelLight=0:3*LargeArmorSlopedPanelLight=0:6*LargeArmorHalfPanelLight=0:3*LargeArmorHalfCenterPanelLight=0:3*LargeArmorQuarterPanelLight=0:2*LargeArmor2x1SlopedPanelLight=0:5*LargeArmor2x1SlopedPanelTipLight=0:5*LargeArmor2x1SlopedSideBasePanelLight=0:5*LargeArmor2x1SlopedSideTipPanelLight=0:3*LargeArmor2x1SlopedSideBasePanelLightInv=0:5*LargeArmor2x1SlopedSideTipPanelLightInv=0:3*LargeArmorHalfSlopedPanelLight=0:4*LargeArmor2x1HalfSlopedPanelLightRight=0:3*LargeArmor2x1HalfSlopedTipPanelLightRight=0:3*LargeArmor2x1HalfSlopedPanelLightLeft=0:3*LargeArmor2x1HalfSlopedTipPanelLightLeft=0:3*LargeRoundArmorPanelLight=0:8*LargeRoundArmorPanelCornerLight=0:5*LargeRoundArmorPanelFaceLight=0:4*LargeRoundArmorPanelInvertedCornerLight=0:5*LargeArmorPanelHeavy=0:15,4:5*LargeArmorCenterPanelHeavy=0:15,4:5*LargeArmorSlopedSidePanelHeavy=0:8,4:3*LargeArmorSlopedPanelHeavy=0:21,4:7*LargeArmorHalfPanelHeavy=0:8,4:3*LargeArmorHalfCenterPanelHeavy=0:8,4:3*LargeArmorQuarterPanelHeavy=0:5,4:2*LargeArmor2x1SlopedPanelHeavy=0:18,4:6*LargeArmor2x1SlopedPanelTipHeavy=0:18,4:6*LargeArmor2x1SlopedSideBasePanelHeavy=0:12,4:4*LargeArmor2x1SlopedSideTipPanelHeavy=0:6,4:2*LargeArmor2x1SlopedSideBasePanelHeavyInv=0:12,4:4*LargeArmor2x1SlopedSideTipPanelHeavyInv=0:6,4:2*LargeArmorHalfSlopedPanelHeavy=0:9,4:3*LargeArmor2x1HalfSlopedPanelHeavyRight=0:9,4:3*LargeArmor2x1HalfSlopedTipPanelHeavyRight=0:9,4:3*LargeArmor2x1HalfSlopedPanelHeavyLeft=0:9,4:3*LargeArmor2x1HalfSlopedTipPanelHeavyLeft=0:9,4:3*LargeRoundArmorPanelHeavy=0:24,4:8*LargeRoundArmorPanelCornerHeavy=0:15,4:5*LargeRoundArmorPanelFaceHeavy=0:12,4:4*LargeRoundArmorPanelInvertedCornerHeavy=0:15,4:5*SmallArmorPanelLight=0:1*SmallArmorCenterPanelLight=0:1*SmallArmorSlopedSidePanelLight=0:1*SmallArmorSlopedPanelLight=0:1*SmallArmorHalfPanelLight=0:1*SmallArmorHalfCenterPanelLight=0:1*SmallArmorQuarterPanelLight=0:1*SmallArmor2x1SlopedPanelLight=0:1*SmallArmor2x1SlopedPanelTipLight=0:1*SmallArmor2x1SlopedSideBasePanelLight=0:1*SmallArmor2x1SlopedSideTipPanelLight=0:1*SmallArmor2x1SlopedSideBasePanelLightInv=0:1*SmallArmor2x1SlopedSideTipPanelLightInv=0:1*SmallArmorHalfSlopedPanelLight=0:1*SmallArmor2x1HalfSlopedPanelLightRight=0:1*SmallArmor2x1HalfSlopedTipPanelLightRight=0:1*SmallArmor2x1HalfSlopedPanelLightLeft=0:1*SmallArmor2x1HalfSlopedTipPanelLightLeft=0:1*SmallRoundArmorPanelLight=0:1*SmallRoundArmorPanelCornerLight=0:1*SmallRoundArmorPanelFaceLight=0:1*SmallRoundArmorPanelInvertedCornerLight=0:1*SmallArmorPanelHeavy=0:3,4:1*SmallArmorCenterPanelHeavy=0:3,4:1*SmallArmorSlopedSidePanelHeavy=0:2,4:1*SmallArmorSlopedPanelHeavy=0:3,4:1*SmallArmorHalfPanelHeavy=0:2,4:1*SmallArmorHalfCenterPanelHeavy=0:2,4:1*SmallArmorQuarterPanelHeavy=0:2,4:1*SmallArmor2x1SlopedPanelHeavy=0:3,4:1*SmallArmor2x1SlopedPanelTipHeavy=0:3,4:1*SmallArmor2x1SlopedSideBasePanelHeavy=0:3,4:1*SmallArmor2x1SlopedSideTipPanelHeavy=0:2,4:1*SmallArmor2x1SlopedSideBasePanelHeavyInv=0:3,4:1*SmallArmor2x1SlopedSideTipPanelHeavyInv=0:2,4:1*SmallArmorHalfSlopedPanelHeavy=0:2,4:1*SmallArmor2x1HalfSlopedPanelHeavyRight=0:2,4:1*SmallArmor2x1HalfSlopedTipPanelHeavyRight=0:2,4:1*SmallArmor2x1HalfSlopedPanelHeavyLeft=0:2,4:1*SmallArmor2x1HalfSlopedTipPanelHeavyLeft=0:2,4:1*SmallRoundArmorPanelHeavy=0:3,4:1*SmallRoundArmorPanelCornerHeavy=0:2,4:1*SmallRoundArmorPanelFaceHeavy=0:3,4:1*SmallRoundArmorPanelInvertedCornerHeavy=0:3,4:1*LargeBlockArmorCornerSquare=0:7*SmallBlockArmorCornerSquare=0:1*LargeBlockHeavyArmorCornerSquare=0:40,4:15*SmallBlockHeavyArmorCornerSquare=0:2,4:1*LargeBlockArmorCornerSquareInverted=0:19*SmallBlockArmorCornerSquareInverted=0:1*LargeBlockHeavyArmorCornerSquareInverted=0:112,4:40*SmallBlockHeavyArmorCornerSquareInverted=0:4,4:1*LargeBlockArmorHalfCorner=0:6*SmallBlockArmorHalfCorner=0:1*LargeBlockHeavyArmorHalfCorner=0:40,4:12*SmallBlockHeavyArmorHalfCorner=0:2,4:1*LargeBlockArmorHalfSlopeCorner=0:2*SmallBlockArmorHalfSlopeCorner=0:1*LargeBlockHeavyArmorHalfSlopeCorner=0:12,4:4*SmallBlockHeavyArmorHalfSlopeCorner=0:2,4:1*LargeBlockArmorHalfSlopeCornerInverted=0:23*SmallBlockArmorHalfSlopeCornerInverted=0:1*LargeBlockHeavyArmorHalfSlopeCornerInverted=0:139,4:45*SmallBlockHeavyArmorHalfSlopeCornerInverted=0:5,4:1*LargeBlockArmorHalfSlopedCorner=0:11*SmallBlockArmorHalfSlopedCorner=0:1*LargeBlockHeavyArmorHalfSlopedCorner=0:45,4:5*SmallBlockHeavyArmorHalfSlopedCorner=0:3,4:1*LargeBlockArmorHalfSlopedCornerBase=0:11*SmallBlockArmorHalfSlopedCornerBase=0:1*LargeBlockHeavyArmorHalfSlopedCornerBase=0:45,4:15*SmallBlockHeavyArmorHalfSlopedCornerBase=0:3,4:1*LargeBlockArmorHalfSlopeInverted=0:22*SmallBlockArmorHalfSlopeInverted=0:1*LargeBlockHeavyArmorHalfSlopeInverted=0:133,4:45*SmallBlockHeavyArmorHalfSlopeInverted=0:5,4:1*LargeBlockArmorSlopedCorner=0:13*SmallBlockArmorSlopedCorner=0:1*LargeBlockHeavyArmorSlopedCorner=0:75,4:25*SmallBlockHeavyArmorSlopedCorner=0:3,4:1*LargeBlockArmorSlopedCornerBase=0:20*SmallBlockArmorSlopedCornerBase=0:1*LargeBlockHeavyArmorSlopedCornerBase=0:127,4:40*SmallBlockHeavyArmorSlopedCornerBase=0:5,4:1*LargeBlockArmorSlopedCornerTip=0:5*SmallBlockArmorSlopedCornerTip=0:1*LargeBlockHeavyArmorSlopedCornerTip=0:23,4:6*SmallBlockHeavyArmorSlopedCornerTip=0:2,4:1*LargeBlockArmorRaisedSlopedCorner=0:17*SmallBlockArmorRaisedSlopedCorner=0:1*LargeBlockHeavyArmorRaisedSlopedCorner=0:100,4:30*SmallBlockHeavyArmorRaisedSlopedCorner=0:4,4:2*LargeBlockArmorSlopeTransition=0:10*SmallBlockArmorSlopeTransition=0:1*LargeBlockHeavyArmorSlopeTransition=0:60,4:18*SmallBlockHeavyArmorSlopeTransition=0:3,4:1*LargeBlockArmorSlopeTransitionBase=0:16*SmallBlockArmorSlopeTransitionBase=0:1*LargeBlockHeavyArmorSlopeTransitionBase=0:95,4:35*SmallBlockHeavyArmorSlopeTransitionBase=0:4,4:2*LargeBlockArmorSlopeTransitionBaseMirrored=0:16*SmallBlockArmorSlopeTransitionBaseMirrored=0:1*LargeBlockHeavyArmorSlopeTransitionBaseMirrored=0:95,4:35*SmallBlockHeavyArmorSlopeTransitionBaseMirrored=0:4,4:2*LargeBlockArmorSlopeTransitionMirrored=0:10*SmallBlockArmorSlopeTransitionMirrored=0:1*LargeBlockHeavyArmorSlopeTransitionMirrored=0:60,4:18*SmallBlockHeavyArmorSlopeTransitionMirrored=0:3,4:1*LargeBlockArmorSlopeTransitionTip=0:5*SmallBlockArmorSlopeTransitionTip=0:1*LargeBlockHeavyArmorSlopeTransitionTip=0:30,4:9*SmallBlockHeavyArmorSlopeTransitionTip=0:2,4:1*LargeBlockArmorSlopeTransitionTipMirrored=0:5*SmallBlockArmorSlopeTransitionTipMirrored=0:1*LargeBlockHeavyArmorSlopeTransitionTipMirrored=0:30,4:9*SmallBlockHeavyArmorSlopeTransitionTipMirrored=0:2,4:1*LargeBlockArmorSquareSlopedCornerBase=0:18*SmallBlockArmorSquareSlopedCornerBase=0:1*LargeBlockHeavyArmorSquareSlopedCornerBase=0:105,4:35*SmallBlockHeavyArmorSquareSlopedCornerBase=0:4,4:2*LargeBlockArmorSquareSlopedCornerTip=0:6*SmallBlockArmorSquareSlopedCornerTip=0:1*LargeBlockHeavyArmorSquareSlopedCornerTip=0:30,4:9*SmallBlockHeavyArmorSquareSlopedCornerTip=0:2,4:1*LargeBlockArmorSquareSlopedCornerTipInv=0:9*SmallBlockArmorSquareSlopedCornerTipInv=0:1*LargeBlockHeavyArmorSquareSlopedCornerTipInv=0:55,4:18*SmallBlockHeavyArmorSquareSlopedCornerTipInv=0:3,4:1*LargeBlockDeskChairless=7:30,1:30*LargeBlockDeskChairlessCorner=7:20,1:20*LargeBlockDeskChairlessCornerInv=7:60,1:60*Shower=7:20,1:20,10:12,12:8*WindowWall=0:8,1:10,12:10*WindowWallLeft=0:10,1:10,12:8*WindowWallRight=0:10,1:10,12:8*Catwalk=1:16,14:4,10:20*CatwalkCorner=1:24,14:4,10:32*CatwalkStraight=1:24,14:4,10:32*CatwalkWall=1:20,14:4,10:26*CatwalkRailingEnd=1:28,14:4,10:38*CatwalkRailingHalfRight=1:28,14:4,10:36*CatwalkRailingHalfLeft=1:28,14:4,10:36*CatwalkHalf=1:10,14:2,10:10*CatwalkHalfRailing=1:18,14:2,10:22*CatwalkHalfCenterRailing=1:14,14:2,10:16*CatwalkHalfOuterRailing=1:14,14:2,10:16*GratedStairs=1:22,10:12,7:16*GratedHalfStairs=1:20,10:6,7:8*GratedHalfStairsMirrored=1:20,10:6,7:8*RailingStraight=1:8,10:6*RailingDouble=1:16,10:12*RailingCorner=1:16,10:12*RailingDiagonal=1:12,10:9*RailingHalfRight=1:8,10:4*RailingHalfLeft=1:8,10:4*RailingCenter=1:8,10:6*Railing2x1Right=1:10,10:7*Railing2x1Left=1:10,10:7*Freight1=7:6,1:8*Freight2=7:12,1:16*Freight3=7:18,1:24*Truss=14:20,10:10*TrussSmall=14:2,10:1*TrussFrame=14:10,10:5*TrussSlopedFrame=14:6,10:3*TrussSloped=14:10,10:5*TrussSlopedSmall=14:1,10:1*TrussAngled=14:20,10:10*TrussAngledSmall=14:2,10:1*TrussHalf=14:10,10:5*TrussHalfSmall=14:1,10:1*TrussFloor=14:24,1:16,10:30*TrussFloorT=14:22,1:16,10:30*TrussFloorX=14:20,1:16,10:30*TrussFloorAngled=14:24,1:16,10:30*TrussFloorAngledInverted=14:24,1:16,10:30*TrussFloorHalf=14:12,1:14,10:20*LargeBarrel=0:5,2:1,1:6*SmallBarrel=0:5,2:1,1:6*LargeBarrelThree=0:15,2:3,1:18*LargeBarrelStack=0:50,2:10,4:4,14:8,1:60*ArmorCenter=0:140*ArmorCorner=0:120*ArmorInvCorner=0:135*ArmorSide=0:130*SmallArmorCenter=0:5*SmallArmorCorner=0:5*SmallArmorInvCorner=0:5*SmallArmorSide=0:5*Monolith=0:130,11:130*Stereolith=0:130,11:130*DeadAstronaut=0:13,11:13*LargeDeadAstronaut=0:13,11:13*EngineerPlushie=21:1*SabiroidPlushie=22:1*LargeWarningSignEaster2=1:4,7:6*SmallWarningSignEaster2=1:4,7:6*LargeWarningSignEaster3=1:4,7:6*SmallWarningSignEaster3=1:4,7:6*LargeWarningSignEaster9=1:4,7:4*SmallWarningSignEaster9=1:2,7:1*LargeWarningSignEaster10=1:4,7:4*SmallWarningSignEaster10=1:2,7:1*LargeWarningSignEaster11=1:4,7:4*SmallWarningSignEaster11=1:2,7:1*LargeWarningSignEaster13=1:4,7:4*SmallWarningSignEaster13=1:2,7:1*LargeBlockStatueEngineer=14:30,0:60,1:30*DeadBody01=12:1,8:1,6:1*DeadBody02=12:1,8:1,6:1*DeadBody03=12:1,8:1,6:1*DeadBody04=12:1,8:1,6:1*DeadBody05=12:1,8:1,6:1*DeadBody06=12:1,8:1,6:1*AngledInteriorWallA=7:25,1:10*AngledInteriorWallB=7:25,1:10*PipeWorkBlockA=7:20,1:20,2:10*PipeWorkBlockB=7:20,1:20,2:10*LargeWarningSign1=1:4,7:6*LargeWarningSign2=1:4,7:6*LargeWarningSign3=1:4,7:6*LargeWarningSign4=1:4,7:2*LargeWarningSign5=1:4,7:6*LargeWarningSign6=1:4,7:6*LargeWarningSign7=1:4,7:2*LargeWarningSign8=1:4,7:4*LargeWarningSign9=1:4,7:4*LargeWarningSign10=1:4,7:4*LargeWarningSign11=1:4,7:4*LargeWarningSign12=1:4,7:4*LargeWarningSign13=1:4,7:4*SmallWarningSign1=1:4,7:6*SmallWarningSign2=1:4,7:2*SmallWarningSign3=1:4,7:6*SmallWarningSign4=1:4,7:2*SmallWarningSign5=1:4,7:6*SmallWarningSign6=1:4,7:2*SmallWarningSign7=1:4,7:2*SmallWarningSign8=1:2,7:1*SmallWarningSign9=1:2,7:1*SmallWarningSign10=1:2,7:1*SmallWarningSign11=1:2,7:1*SmallWarningSign12=1:2,7:1*SmallWarningSign13=1:2,7:1*LargeBlockConveyorPipeCap=7:10,1:10*LargeBlockCylindricalColumn=7:25,1:10*SmallBlockCylindricalColumn=7:5,1:3*LargeGridBeamBlock=0:25*LargeGridBeamBlockSlope=0:13*LargeGridBeamBlockRound=0:13*LargeGridBeamBlockSlope2x1Base=0:19*LargeGridBeamBlockSlope2x1Tip=0:7*LargeGridBeamBlockHalf=0:12*LargeGridBeamBlockHalfSlope=0:7*LargeGridBeamBlockEnd=0:25*LargeGridBeamBlockJunction=0:25*LargeGridBeamBlockTJunction=0:25*SmallGridBeamBlock=0:1*SmallGridBeamBlockSlope=0:1*SmallGridBeamBlockRound=0:1*SmallGridBeamBlockSlope2x1Base=0:1*SmallGridBeamBlockSlope2x1Tip=0:1*SmallGridBeamBlockHalf=0:1*SmallGridBeamBlockHalfSlope=0:1*SmallGridBeamBlockEnd=0:1*SmallGridBeamBlockJunction=0:1*SmallGridBeamBlockTJunction=0:1*Passage2=7:74,1:20,10:48*Passage2Wall=7:50,1:14,10:32*LargeStairs=7:50,1:30*LargeRamp=7:70,1:16*LargeSteelCatwalk=7:27,1:5,10:20*LargeSteelCatwalk2Sides=7:32,1:7,10:25*LargeSteelCatwalkCorner=7:32,1:7,10:25*LargeSteelCatwalkPlate=7:23,1:7,10:17*LargeCoverWall=0:4,1:10*LargeCoverWallHalf=0:2,1:6*LargeCoverWallHalfMirrored=0:2,1:6*LargeBlockInteriorWall=7:25,1:10*LargeInteriorPillar=7:25,1:10,10:4*AirDuct1=7:20,1:30,2:4,0:10*AirDuct2=7:20,1:30,2:4,0:10*AirDuctCorner=7:20,1:30,2:4,0:10*AirDuctT=7:15,1:30,2:4,0:8*AirDuctX=7:10,1:30,2:4,0:5*AirDuctRamp=7:20,1:30,2:4,0:10*AirDuctGrate=1:10,7:10*LargeBlockConveyorCap=7:10,1:10*SmallBlockConveyorCapMedium=7:10,1:10*SmallBlockConveyorCap=7:2,1:2*Viewport1=0:10,1:10,12:8*Viewport2=0:10,1:10,12:8*BarredWindow=14:1,1:4*BarredWindowSlope=14:1,1:4*BarredWindowSide=14:1,1:4*BarredWindowFace=14:1,1:4*StorageShelf1=14:10,0:50,1:50,10:50,7:50*StorageShelf2=14:30,19:20,5:20,4:20*StorageShelf3=14:10,20:10,11:10,23:10,16:2*LargeBlockInsetWall=7:25,1:10*LargeBlockInsetWallPillar=7:25,1:10*LargeBlockInsetWallCorner=7:25,1:10*LargeBlockInsetWallCornerInverted=7:25,1:10*LargeBlockInsetWallSlope=7:19,1:7*LargeBlockConsoleModule=7:50,1:50*LargeBlockConsoleModuleCorner=7:30,1:30*SmallBlockConsoleModule=7:6,1:6*SmallBlockConsoleModuleCorner=7:3,1:3*SmallBlockConsoleModuleInvertedCorner=7:3,1:3*ExtendedWindow=14:10,12:25*ExtendedWindowRailing=14:10,12:25*ExtendedWindowCorner=14:4,12:10*ExtendedWindowEnd=14:10,12:25*ExtendedWindowDome=14:10,12:25*SmallBlockExtendedWindow=14:1,12:3*SmallBlockExtendedWindowCorner=14:1,12:2*SmallBlockExtendedWindowEnd=14:1,12:3*SmallBlockExtendedWindowDome=14:2,12:6*Corridor=7:100,1:30*CorridorCorner=7:100,1:30*CorridorT=7:70,1:25*CorridorX=7:40,1:20*CorridorWindow=7:80,1:30,12:6*CorridorDoubleWindow=7:65,1:25,12:12*CorridorWindowRoof=7:80,1:30,12:6*CorridorNarrow=7:100,1:30*TrussPillar=7:25,1:10,10:4*TrussPillarT=7:30,1:12,10:6*TrussPillarX=7:35,1:15,10:8*TrussPillarDiagonal=7:30,1:12,10:6*TrussPillarSmall=7:25,1:10,10:4*TrussPillarOffset=7:25,1:10,10:4*LargeBlockSciFiWall=7:25,1:10*LargeBlockBarCounter=7:16,1:10,5:1,12:6*LargeBlockBarCounterCorner=7:24,1:14,5:2,12:10*LargeSymbolA=0:4*LargeSymbolB=0:4*LargeSymbolC=0:4*LargeSymbolD=0:4*LargeSymbolE=0:4*LargeSymbolF=0:4*LargeSymbolG=0:4*LargeSymbolH=0:4*LargeSymbolI=0:4*LargeSymbolJ=0:4*LargeSymbolK=0:4*LargeSymbolL=0:4*LargeSymbolM=0:4*LargeSymbolN=0:4*LargeSymbolO=0:4*LargeSymbolP=0:4*LargeSymbolQ=0:4*LargeSymbolR=0:4*LargeSymbolS=0:4*LargeSymbolT=0:4*LargeSymbolU=0:4*LargeSymbolV=0:4*LargeSymbolW=0:4*LargeSymbolX=0:4*LargeSymbolY=0:4*LargeSymbolZ=0:4*SmallSymbolA=0:1*SmallSymbolB=0:1*SmallSymbolC=0:1*SmallSymbolD=0:1*SmallSymbolE=0:1*SmallSymbolF=0:1*SmallSymbolG=0:1*SmallSymbolH=0:1*SmallSymbolI=0:1*SmallSymbolJ=0:1*SmallSymbolK=0:1*SmallSymbolL=0:1*SmallSymbolM=0:1*SmallSymbolN=0:1*SmallSymbolO=0:1*SmallSymbolP=0:1*SmallSymbolQ=0:1*SmallSymbolR=0:1*SmallSymbolS=0:1*SmallSymbolT=0:1*SmallSymbolU=0:1*SmallSymbolV=0:1*SmallSymbolW=0:1*SmallSymbolX=0:1*SmallSymbolY=0:1*SmallSymbolZ=0:1*LargeSymbol0=0:4*LargeSymbol1=0:4*LargeSymbol2=0:4*LargeSymbol3=0:4*LargeSymbol4=0:4*LargeSymbol5=0:4*LargeSymbol6=0:4*LargeSymbol7=0:4*LargeSymbol8=0:4*LargeSymbol9=0:4*SmallSymbol0=0:1*SmallSymbol1=0:1*SmallSymbol2=0:1*SmallSymbol3=0:1*SmallSymbol4=0:1*SmallSymbol5=0:1*SmallSymbol6=0:1*SmallSymbol7=0:1*SmallSymbol8=0:1*SmallSymbol9=0:1*LargeSymbolHyphen=0:4*LargeSymbolUnderscore=0:4*LargeSymbolDot=0:4*LargeSymbolApostrophe=0:4*LargeSymbolAnd=0:4*LargeSymbolColon=0:4*LargeSymbolExclamationMark=0:4*LargeSymbolQuestionMark=0:4*SmallSymbolHyphen=0:1*SmallSymbolUnderscore=0:1*SmallSymbolDot=0:1*SmallSymbolApostrophe=0:1*SmallSymbolAnd=0:1*SmallSymbolColon=0:1*SmallSymbolExclamationMark=0:1*SmallSymbolQuestionMark=0:1*FireCover=0:4,1:10*FireCoverCorner=0:8,1:20*HalfWindow=14:4,0:10,12:10*HalfWindowInv=14:4,0:10,12:10*HalfWindowCorner=14:8,0:20,12:20*HalfWindowCornerInv=14:8,0:20,12:20*HalfWindowDiagonal=14:6,0:14,12:14*HalfWindowRound=14:7,0:16,12:18*Embrasure=0:30,1:20,4:10*PassageSciFi=7:74,1:20,10:48*PassageSciFiWall=7:50,1:14,10:32*PassageSciFiIntersection=7:35,1:10,10:25*PassageSciFiGate=7:35,1:10,10:25*PassageScifiCorner=7:74,1:20,10:48*PassageSciFiTjunction=7:55,1:16,10:38*PassageSciFiWindow=7:60,1:16,12:16,10:38*BridgeWindow1x1Slope=14:8,0:5,7:10,12:25*BridgeWindow1x1Face=14:8,0:2,7:4,12:18*BridgeWindow1x1FaceInverted=14:5,0:6,7:12,12:12*LargeWindowSquare=7:12,1:8,10:4*LargeWindowEdge=7:16,1:12,10:6*Window1x2Slope=14:16,12:55*Window1x2Inv=14:15,12:40*Window1x2Face=14:15,12:40*Window1x2SideLeft=14:13,12:26*Window1x2SideLeftInv=14:13,12:26*Window1x2SideRight=14:13,12:26*Window1x2SideRightInv=14:13,12:26*Window1x1Slope=14:12,12:35*Window1x1Face=14:11,12:24*Window1x1Side=14:9,12:17*Window1x1SideInv=14:9,12:17*Window1x1Inv=14:11,12:24*Window1x2Flat=14:15,12:50*Window1x2FlatInv=14:15,12:50*Window1x1Flat=14:10,12:25*Window1x1FlatInv=14:10,12:25*Window3x3Flat=14:40,12:196*Window3x3FlatInv=14:40,12:196*Window2x3Flat=14:25,12:140*Window2x3FlatInv=14:25,12:140*SmallWindow1x2Slope=14:1,12:3*SmallWindow1x2Inv=14:1,12:3*SmallWindow1x2Face=14:1,12:3*SmallWindow1x2SideLeft=14:1,12:3*SmallWindow1x2SideLeftInv=14:1,12:3*SmallWindow1x2SideRight=14:1,12:3*SmallWindow1x2SideRightInv=14:1,12:3*SmallWindow1x1Slope=14:1,12:2*SmallWindow1x1Face=14:1,12:2*SmallWindow1x1Side=14:1,12:2*SmallWindow1x1SideInv=14:1,12:2*SmallWindow1x1Inv=14:1,12:2*SmallWindow1x2Flat=14:1,12:3*SmallWindow1x2FlatInv=14:1,12:3*SmallWindow1x1Flat=14:1,12:2*SmallWindow1x1FlatInv=14:1,12:2*SmallWindow3x3Flat=14:3,12:12*SmallWindow3x3FlatInv=14:3,12:12*SmallWindow2x3Flat=14:2,12:8*SmallWindow2x3FlatInv=14:2,12:8*WindowRound=14:15,12:45*WindowRoundInv=14:15,12:45*WindowRoundCorner=14:13,12:33*WindowRoundCornerInv=14:13,12:33*WindowRoundFace=14:9,12:21*WindowRoundFaceInv=14:9,12:21*WindowRoundInwardsCorner=14:13,12:20*WindowRoundInwardsCornerInv=14:13,12:20*SmallWindowRound=14:1,12:2*SmallWindowRoundInv=14:1,12:2*SmallWindowRoundCorner=14:1,12:2*SmallWindowRoundCornerInv=14:1,12:2*SmallWindowRoundFace=14:1,12:2*SmallWindowRoundFaceInv=14:1,12:2*SmallWindowRoundInwardsCorner=14:1,12:2*SmallWindowRoundInwardsCornerInv=14:1,12:2$DebugSphere1*DebugSphereLarge=0:10,3:20$DebugSphere2*DebugSphereLarge=0:10,3:20$DebugSphere3*DebugSphereLarge=0:10,3:20$MyProgrammableBlock*SmallProgrammableBlock=0:2,1:2,2:2,5:1,6:1,3:2*LargeProgrammableBlock=0:21,1:4,2:2,5:1,6:1,3:2*LargeProgrammableBlockReskin=0:21,1:4,2:2,5:1,6:1,3:2*SmallProgrammableBlockReskin=0:2,1:2,2:2,5:1,6:1,3:2$Projector*LargeProjector=0:21,1:4,2:2,5:1,3:2*SmallProjector=0:2,1:2,2:2,5:1,3:2*LargeBlockConsole=7:20,1:30,3:8,6:10$SensorBlock*SmallBlockSensor=7:5,1:5,3:6,8:4,9:6,0:2*LargeBlockSensor=7:5,1:5,3:6,8:4,9:6,0:2*SmallBlockSensorReskin=7:5,1:5,3:6,8:4,9:6,0:2*LargeBlockSensorReskin=7:5,1:5,3:6,8:4,9:6,0:2$TargetDummyBlock*TargetDummy=0:15,10:10,5:2,3:4,6:1$SoundBlock*SmallBlockSoundBlock=7:4,1:6,3:3*LargeBlockSoundBlock=7:4,1:6,3:3$ButtonPanel*ButtonPanelLarge=7:10,1:20,3:20*ButtonPanelSmall=7:2,1:2,3:1*LargeButtonPanelPedestal=7:5,1:10,3:5*SmallButtonPanelPedestal=7:5,1:10,3:5*LargeBlockInsetButtonPanel=7:20,1:20,3:20,6:10*LargeBlockAccessPanel3=1:8,3:3,7:3*VerticalButtonPanelLarge=7:5,1:10,3:5*VerticalButtonPanelSmall=7:5,1:10,3:5*LargeBlockConsoleModuleButtons=7:50,1:50,6:6,3:10*SmallBlockConsoleModuleButtons=7:6,1:6,3:2*LargeSciFiButtonTerminal=7:5,1:10,3:4,6:4*LargeSciFiButtonPanel=7:10,1:20,3:20,6:5$TimerBlock*TimerBlockLarge=7:6,1:30,3:5*TimerBlockSmall=7:2,1:3,3:1*TimerBlockReskinLarge=7:6,1:30,3:5*TimerBlockReskinSmall=7:2,1:3,3:1$TurretControlBlock*LargeTurretControlBlock=7:20,1:30,9:20,5:4,6:6,3:20,0:20*SmallTurretControlBlock=7:4,1:10,9:4,5:2,6:1,3:10,0:4$EventControllerBlock*EventControllerLarge=7:10,1:30,3:10,6:4*EventControllerSmall=7:2,1:3,3:2,6:1$PathRecorderBlock*LargePathRecorderBlock=7:20,1:30,9:20,5:4,3:20,0:20*SmallPathRecorderBlock=7:2,1:5,9:4,5:2,3:10,0:2$BasicMissionBlock*LargeBasicMission=7:20,1:30,9:20,5:4,3:20,0:20*SmallBasicMission=7:2,1:5,9:4,5:2,3:10,0:2$FlightMovementBlock*LargeFlightMovement=7:20,1:30,9:20,5:4,3:20,0:20*SmallFlightMovement=7:2,1:5,9:4,5:2,3:10,0:2$DefensiveCombatBlock*LargeDefensiveCombat=7:20,1:30,9:20,5:4,3:20,0:20*SmallDefensiveCombat=7:2,1:5,9:4,5:2,3:10,0:2$OffensiveCombatBlock*LargeOffensiveCombat=7:20,1:30,9:20,5:4,3:20,0:20*SmallOffensiveCombat=7:2,1:5,9:4,5:2,3:10,0:2$RadioAntenna*LargeBlockRadioAntenna=0:80,2:40,10:60,1:30,3:8,8:40*LargeBlockCompactRadioAntenna=0:40,2:20,10:30,1:20,3:8,8:40*SmallBlockRadioAntenna=0:1,10:1,1:2,3:1,8:4*LargeBlockRadioAntennaDish=1:40,14:120,0:80,3:8,8:40$Beacon*LargeBlockBeacon=0:80,1:30,2:20,3:10,8:40*SmallBlockBeacon=0:2,1:1,10:1,3:1,8:4*LargeBlockBeaconReskin=0:80,1:30,2:20,3:10,8:40*SmallBlockBeaconReskin=0:2,1:1,10:1,3:1,8:4$RemoteControl*LargeBlockRemoteControl=7:10,1:10,5:1,3:15*SmallBlockRemoteControl=7:2,1:1,5:1,3:1$LaserAntenna*LargeBlockLaserAntenna=0:50,1:40,5:16,9:30,8:20,11:100,3:50,12:4*SmallBlockLaserAntenna=0:10,10:10,1:10,5:5,8:5,11:10,3:30,12:2$BroadcastController*LargeBlockBroadcastController=7:10,1:30,8:5,3:10,6:4*SmallBlockBroadcastController=7:2,1:3,8:1,3:2,6:1$TransponderBlock*LargeBlockTransponder=0:30,1:20,3:10,8:5*SmallBlockTransponder=0:3,1:2,3:2,8:1$TerminalBlock*ControlPanel=0:1,1:1,3:1,6:1*SmallControlPanel=0:1,1:1,3:1,6:1*LargeControlPanelPedestal=7:5,1:10,3:1,6:1*SmallControlPanelPedestal=7:5,1:10,3:1,6:1*LargeCrate=0:20,10:8,5:4,1:24*LargeBlockAccessPanel1=1:15,3:5,7:5*LargeBlockAccessPanel2=1:15,10:10,7:5*LargeBlockAccessPanel4=1:10,7:10*SmallBlockAccessPanel1=1:8,10:2,7:2*SmallBlockAccessPanel2=1:2,10:1,7:1*SmallBlockAccessPanel3=1:4,10:1,7:1*SmallBlockAccessPanel4=1:10,7:10*LargeBlockSciFiTerminal=1:4,3:2,6:4,7:2$Cockpit*LargeBlockCockpit=7:20,1:20,5:2,3:100,6:10*LargeBlockCockpitSeat=0:30,1:20,5:1,6:8,3:100,12:60*SmallBlockCockpit=0:10,1:10,5:1,6:5,3:15,12:30*DBSmallBlockFighterCockpit=1:20,5:1,0:20,4:10,7:15,6:4,3:20,12:40*CockpitOpen=7:20,1:20,5:2,3:100,6:4*RoverCockpit=7:30,1:25,5:2,3:20,6:4*OpenCockpitSmall=7:20,1:20,5:1,3:15,6:2*OpenCockpitLarge=7:30,1:30,5:2,3:100,6:6*LargeBlockDesk=7:30,1:30*LargeBlockDeskCorner=7:20,1:20*LargeBlockDeskCornerInv=7:60,1:60*LargeBlockCouch=7:30,1:30*LargeBlockCouchCorner=7:35,1:35*LargeBlockBathroomOpen=7:30,1:30,10:8,5:4,2:2*LargeBlockBathroom=7:30,1:40,10:8,5:4,2:2*LargeBlockToilet=7:10,1:15,10:2,5:2,2:1*SmallBlockCockpitIndustrial=0:10,1:20,4:10,5:2,6:6,3:20,12:60,10:10*LargeBlockCockpitIndustrial=0:20,1:30,4:15,5:2,6:10,3:60,12:80,10:10*SmallBlockCapCockpit=0:20,1:10,5:1,6:4,3:15,12:10*LargeBlockInsetPlantCouch=7:30,1:30,10:10,12:20*SpeederCockpit=7:30,1:25,5:2,3:20,6:4*SpeederCockpitCompact=7:30,1:25,5:2,3:20,6:4*PassengerSeatLarge=7:20,1:20*PassengerSeatSmall=7:20,1:20*PassengerSeatSmallNew=7:20,1:20*PassengerSeatSmallOffset=7:20,1:20*BuggyCockpit=7:30,1:25,5:2,3:20,6:4*LargeBlockConsoleModuleInvertedCorner=7:80,1:80*LargeBlockConsoleModuleScreens=7:50,1:50,6:12,3:10*PassengerBench=7:20,1:20*SmallBlockStandingCockpit=7:20,1:20,5:1,3:20,6:2*LargeBlockStandingCockpit=7:20,1:20,5:1,3:20,6:2$Gyro*LargeBlockGyro=0:600,1:40,2:4,4:50,5:4,3:5*SmallBlockGyro=0:25,1:5,2:1,5:2,3:3$Kitchen*LargeBlockKitchen=7:20,1:30,2:6,5:6,12:4$CryoChamber*LargeBlockBed=7:30,1:30,10:8,12:10*LargeBlockCryoRoom=7:40,1:20,5:8,6:8,13:3,3:30,12:10*LargeBlockHalfBed=7:14,1:16,10:6,6:3*LargeBlockHalfBedOffset=7:14,1:16,10:6,6:3*LargeBlockInsetBed=7:60,1:30,10:8*LargeBlockCryoChamber=7:40,1:20,5:8,6:8,13:3,3:30,12:10*SmallBlockCryoChamber=7:20,1:10,5:4,6:4,13:3,3:15,12:5$CargoContainer*LargeBlockLockerRoom=7:30,1:30,6:4,12:10*LargeBlockLockerRoomCorner=7:25,1:30,6:4,12:10*LargeBlockLockers=7:20,1:20,6:3,3:2*LargeBlockInsetBookshelf=7:30,1:30*LargeBlockLargeIndustrialContainer=7:360,1:80,4:24,10:60,5:20,6:1,3:8*SmallBlockSmallContainer=7:3,1:1,3:1,5:1,6:1*SmallBlockMediumContainer=7:30,1:10,3:4,5:4,6:1*SmallBlockLargeContainer=7:75,1:25,3:6,5:8,6:1*LargeBlockSmallContainer=7:40,1:40,4:4,10:20,5:4,6:1,3:2*LargeBlockLargeContainer=7:360,1:80,4:24,10:60,5:20,6:1,3:8*LargeBlockWeaponRack=7:30,1:20*SmallBlockWeaponRack=7:3,1:3$Planter*LargeBlockPlanters=7:10,1:20,10:8,12:8$VendingMachine*FoodDispenser=7:20,1:10,5:4,6:10,3:10*VendingMachine=7:20,1:10,5:4,6:4,3:10$Jukebox*Jukebox=7:15,1:10,3:4,6:4*LargeBlockInsetEntertainmentCorner=7:30,1:20,3:10,6:8$LCDPanelsBlock*LabEquipment=7:15,1:15,5:1,12:4*MedicalStation=7:15,1:15,5:2,13:1,6:2$TextPanel*TransparentLCDLarge=1:8,3:6,6:10,12:10*TransparentLCDSmall=1:4,3:4,6:3,12:1*HoloLCDLarge=1:10,5:1,3:8*HoloLCDSmall=1:5,5:1,3:8*LargeFullBlockLCDPanel=7:20,1:20,3:6,6:10,12:6*SmallFullBlockLCDPanel=7:4,1:4,3:4,6:3,12:1*LargeDiagonalLCDPanel=7:10,1:10,3:6,6:10,12:8*SmallDiagonalLCDPanel=7:4,1:4,3:4,6:3,12:1*LargeCurvedLCDPanel=7:20,1:20,3:6,6:10,12:10*SmallCurvedLCDPanel=7:4,1:4,3:4,6:3,12:1*SmallTextPanel=7:1,1:4,3:4,6:3,12:1*SmallLCDPanelWide=7:1,1:8,3:8,6:6,12:2*SmallLCDPanel=7:1,1:4,3:4,6:3,12:2*LargeBlockCorner_LCD_1=1:5,3:3,6:1*LargeBlockCorner_LCD_2=1:5,3:3,6:1*LargeBlockCorner_LCD_Flat_1=1:5,3:3,6:1*LargeBlockCorner_LCD_Flat_2=1:5,3:3,6:1*SmallBlockCorner_LCD_1=1:3,3:2,6:1*SmallBlockCorner_LCD_2=1:3,3:2,6:1*SmallBlockCorner_LCD_Flat_1=1:3,3:2,6:1*SmallBlockCorner_LCD_Flat_2=1:3,3:2,6:1*LargeTextPanel=7:1,1:6,3:6,6:10,12:2*LargeLCDPanel=7:1,1:6,3:6,6:10,12:6*LargeLCDPanelWide=7:2,1:12,3:12,6:20,12:12*SmallBlockConsoleModuleScreens=7:6,1:6,6:2,3:2*LargeLCDPanel5x5=7:25,1:150,3:25,6:250,12:150*LargeLCDPanel5x3=7:15,1:90,3:15,6:150,12:90*LargeLCDPanel3x3=7:10,1:50,3:10,6:90,12:50$ReflectorLight*RotatingLightLarge=1:3,5:1*RotatingLightSmall=1:3,5:1*LargeBlockFrontLight=0:8,2:2,7:20,1:15,12:4*SmallBlockFrontLight=0:1,2:1,7:1,1:1,12:2*OffsetSpotlight=1:2,12:1$SolarPanel*LargeBlockColorableSolarPanel=0:4,1:14,14:12,3:4,15:32,12:4*LargeBlockColorableSolarPanelCorner=0:2,1:7,14:6,3:4,15:16,12:2*LargeBlockColorableSolarPanelCornerInverted=0:2,1:7,14:6,3:4,15:16,12:2*SmallBlockColorableSolarPanel=0:2,1:2,14:4,3:1,15:8,12:1*SmallBlockColorableSolarPanelCorner=0:1,1:2,14:2,3:1,15:4,12:1*SmallBlockColorableSolarPanelCornerInverted=0:1,1:2,14:2,3:1,15:4,12:1*LargeBlockSolarPanel=0:4,1:14,14:12,3:4,15:32,12:4*SmallBlockSolarPanel=0:2,1:2,14:4,3:1,15:8,12:1$WindTurbine*LargeBlockWindTurbineReskin=7:40,5:8,1:20,14:24,3:2*LargeBlockWindTurbine=7:40,5:8,1:20,14:24,3:2$MedicalRoom*LargeMedicalRoomReskin=7:240,1:80,4:60,10:20,2:5,6:10,3:10,13:15*LargeMedicalRoom=7:240,1:80,4:60,10:20,2:5,6:10,3:10,13:15$InteriorLight*LargeBlockInsetAquarium=7:30,1:30,5:1,10:10,16:1,12:10*LargeBlockInsetKitchen=7:30,1:30,2:8,5:6,12:6*LargeBlockInsetLight=0:10,1:10,7:20*SmallBlockInsetLight=0:1,1:2,7:1*AirDuctLight=7:20,1:30,2:4,0:10*SmallLight=1:2*SmallBlockSmallLight=1:2*LargeBlockLight_1corner=1:3*LargeBlockLight_2corner=1:6*SmallBlockLight_1corner=1:2*SmallBlockLight_2corner=1:4*OffsetLight=1:2*LargeBlockInsetWallLight=7:25,1:10*CorridorLight=7:100,1:30*CorridorNarrowStowage=7:100,1:30*TrussPillarLight=7:12,1:8,10:2*TrussPillarLightSmall=7:1,1:2,10:1*PassageSciFiLight=7:74,1:20,10:48*LargeLightPanel=1:10,7:5*SmallLightPanel=1:2,7:1$Ladder2*TrussLadder=14:20,7:10,1:20,10:30*(null)=7:10,1:20,10:10*LadderShaft=7:80,1:40,10:50*LadderSmall=7:10,1:20,10:10$Warhead*LargeExplosiveBarrel=0:5,2:1,1:6,10:2,3:1,17:2*SmallExplosiveBarrel=0:5,2:1,1:6,10:2,3:1,17:2*LargeWarhead=0:20,14:24,1:12,10:12,3:2,17:6*SmallWarhead=0:4,14:1,1:1,10:2,3:1,17:2$Door*(null)=7:10,1:40,10:4,5:2,6:1,3:2,0:8*SmallDoor=7:8,1:30,10:4,5:2,6:1,3:2,0:6*LargeBlockGate=0:800,1:100,10:100,5:20,3:10*LargeBlockOffsetDoor=0:25,1:35,10:4,5:4,6:1,3:2,12:6*LargeBlockNarrowDoor=0:35,1:40,10:10,5:4,6:1,3:2*LargeBlockNarrowDoorHalf=0:25,1:40,10:10,5:4,6:1,3:2*SmallSideDoor=7:10,1:26,12:4,5:2,6:1,3:2,0:8*SlidingHatchDoor=0:40,1:50,10:10,5:4,6:2,3:2,12:10*SlidingHatchDoorHalf=0:30,1:50,10:10,5:4,6:2,3:2,12:10$AirtightHangarDoor*(null)=0:350,1:40,10:40,5:16,3:2*AirtightHangarDoorWarfare2A=0:350,1:40,10:40,5:16,3:2*AirtightHangarDoorWarfare2B=0:350,1:40,10:40,5:16,3:2*AirtightHangarDoorWarfare2C=0:350,1:40,10:40,5:16,3:2$AirtightSlideDoor*LargeBlockSlideDoor=0:20,1:40,10:4,5:4,6:1,3:2,12:15$StoreBlock*StoreBlock=0:30,1:20,5:6,6:4,3:10*AtmBlock=0:20,1:20,5:2,3:10,6:4$SafeZoneBlock*SafeZoneBlock=0:800,1:180,16:10,18:5,4:80,3:120$ContractBlock*ContractBlock=0:30,1:20,5:6,6:4,3:10$BatteryBlock*LargeBlockBatteryBlock=0:80,1:30,19:80,3:25*SmallBlockBatteryBlock=0:25,1:5,19:20,3:2*SmallBlockSmallBatteryBlock=0:4,1:2,19:2,3:2*LargeBlockBatteryBlockWarfare2=0:80,1:30,19:80,3:25*SmallBlockBatteryBlockWarfare2=0:25,1:5,19:20,3:2$Reactor*SmallBlockSmallGenerator=0:3,1:10,4:2,2:1,20:3,5:1,3:10*SmallBlockLargeGenerator=0:60,1:9,4:9,2:3,20:95,5:5,3:25*LargeBlockSmallGenerator=0:80,1:40,4:4,2:8,20:100,5:6,3:25*LargeBlockLargeGenerator=0:1000,1:70,4:40,2:40,11:100,20:2000,5:20,3:75*LargeBlockSmallGeneratorWarfare2=0:80,1:40,4:4,2:8,20:100,5:6,3:25*LargeBlockLargeGeneratorWarfare2=0:1000,1:70,4:40,2:40,11:100,20:2000,5:20,3:75*SmallBlockSmallGeneratorWarfare2=0:3,1:10,4:2,2:1,20:3,5:1,3:10*SmallBlockLargeGeneratorWarfare2=0:60,1:9,4:9,2:3,20:95,5:5,3:25$HydrogenEngine*LargeHydrogenEngine=0:100,1:70,2:12,10:20,5:12,3:4,19:1*SmallHydrogenEngine=0:30,1:20,2:4,10:6,5:4,3:1,19:1$GravityGenerator*(null)=0:150,16:6,1:60,2:4,5:6,3:40$GravityGeneratorSphere*(null)=0:150,16:6,1:60,2:4,5:6,3:40$VirtualMass*VirtualMassLarge=0:90,11:20,1:30,3:20,16:9*VirtualMassSmall=0:3,11:2,1:2,3:2,16:1$SpaceBall*SpaceBallLarge=0:225,1:30,3:20,16:3*SpaceBallSmall=0:70,1:10,3:7,16:1$AirVent*AirVentFan=0:30,1:20,5:10,3:5*AirVentFanFull=0:45,1:30,5:10,3:5*SmallAirVentFan=0:3,1:10,5:2,3:5*SmallAirVentFanFull=0:5,1:15,5:2,3:5*(null)=0:30,1:20,5:10,3:5*AirVentFull=0:45,1:30,5:10,3:5*SmallAirVent=0:3,1:10,5:2,3:5*SmallAirVentFull=0:5,1:15,5:2,3:5$CameraBlock*LargeCameraTopMounted=0:2,3:3*SmallCameraTopMounted=0:2,3:3*SmallCameraBlock=0:2,3:3*LargeCameraBlock=0:2,3:3$EmotionControllerBlock*EmotionControllerLarge=7:10,1:30,3:20,6:12,12:6*EmotionControllerSmall=7:1,1:3,3:5,6:1,12:1$LandingGear*LargeBlockMagneticPlate=0:450,1:60,5:20*SmallBlockMagneticPlate=0:6,1:15,5:3*LargeBlockLandingGear=0:150,1:20,5:6*SmallBlockLandingGear=0:2,1:5,5:1*LargeBlockSmallMagneticPlate=0:15,1:3,5:1*SmallBlockSmallMagneticPlate=0:2,1:1,5:1$ConveyorConnector*LargeBlockConveyorPipeSeamless=7:14,1:20,10:12,5:6*LargeBlockConveyorPipeCorner=7:14,1:20,10:12,5:6*LargeBlockConveyorPipeFlange=7:14,1:20,10:12,5:6*LargeBlockConveyorPipeEnd=7:14,1:20,10:12,5:6*ConveyorTube=7:14,1:20,10:12,5:6*ConveyorTubeDuct=0:25,7:14,1:20,10:12,5:6*ConveyorTubeDuctCurved=0:25,7:14,1:20,10:12,5:6*ConveyorTubeSmall=7:1,5:1,1:1*ConveyorTubeDuctSmall=0:2,7:1,5:1,1:1*ConveyorTubeDuctSmallCurved=0:2,7:1,5:1,1:1*ConveyorTubeMedium=7:10,1:20,10:10,5:6*ConveyorFrameMedium=7:5,1:12,10:5,5:2*ConveyorTubeCurved=7:14,1:20,10:12,5:6*ConveyorTubeSmallCurved=7:1,5:1,1:1*ConveyorTubeCurvedMedium=7:7,1:20,10:10,5:6$Conveyor*LargeBlockConveyorPipeJunction=7:20,1:30,10:20,5:6*LargeBlockConveyorPipeIntersection=7:18,1:20,10:16,5:6*LargeBlockConveyorPipeT=7:16,1:24,10:14,5:6*SmallBlockConveyor=7:4,1:4,5:1*SmallBlockConveyorConverter=7:6,1:8,10:6,5:2*LargeBlockConveyor=7:20,1:30,10:20,5:6*ConveyorTubeDuctT=0:22,7:16,1:24,10:14,5:6*ConveyorTubeDuctSmallT=0:2,7:2,5:1,1:2*SmallShipConveyorHub=7:15,1:20,10:15,5:2*ConveyorTubeSmallT=7:2,5:1,1:2*ConveyorTubeT=7:16,1:24,10:14,5:6$OxygenTank*LargeHydrogenTankIndustrial=0:280,2:80,10:60,3:8,1:40*OxygenTankSmall=0:16,2:8,10:10,3:8,1:10*(null)=0:80,2:40,10:60,3:8,1:40*LargeHydrogenTank=0:280,2:80,10:60,3:8,1:40*LargeHydrogenTankSmall=0:80,2:40,10:60,3:8,1:40*SmallHydrogenTank=0:40,2:20,10:30,3:4,1:20*SmallHydrogenTankSmall=0:3,2:1,10:2,3:4,1:2$Assembler*LargeAssemblerIndustrial=0:140,1:80,5:20,6:10,4:10,3:160*LargeAssembler=0:140,1:80,5:20,6:10,4:10,3:160*BasicAssembler=0:80,1:40,5:10,6:4,3:80$Refinery*LargeRefineryIndustrial=0:1200,1:40,2:20,5:16,4:20,3:20*LargeRefinery=0:1200,1:40,2:20,5:16,4:20,3:20*BlastFurnace=0:120,1:20,5:10,3:10$ConveyorSorter*LargeBlockConveyorSorterIndustrial=7:50,1:120,10:50,3:20,5:2*LargeBlockConveyorSorter=7:50,1:120,10:50,3:20,5:2*MediumBlockConveyorSorter=7:5,1:12,10:5,3:5,5:2*SmallBlockConveyorSorter=7:5,1:12,10:5,3:5,5:2$Thrust*LargeBlockLargeHydrogenThrustIndustrial=0:150,1:180,4:250,2:40*LargeBlockSmallHydrogenThrustIndustrial=0:25,1:60,4:40,2:8*SmallBlockLargeHydrogenThrustIndustrial=0:30,1:30,4:22,2:10*SmallBlockSmallHydrogenThrustIndustrial=0:7,1:15,4:4,2:2*SmallBlockSmallThrustSciFi=0:2,1:2,2:1,23:1*SmallBlockLargeThrustSciFi=0:5,1:2,2:5,23:12*LargeBlockSmallThrustSciFi=0:25,1:60,2:8,23:80*LargeBlockLargeThrustSciFi=0:150,1:100,2:40,23:960*LargeBlockLargeAtmosphericThrustSciFi=0:230,1:60,2:50,4:40,5:1100*LargeBlockSmallAtmosphericThrustSciFi=0:35,1:50,2:8,4:10,5:110*SmallBlockLargeAtmosphericThrustSciFi=0:20,1:30,2:4,4:8,5:90*SmallBlockSmallAtmosphericThrustSciFi=0:3,1:22,2:1,4:1,5:18*SmallBlockSmallThrust=0:2,1:2,2:1,23:1*SmallBlockLargeThrust=0:5,1:2,2:5,23:12*LargeBlockSmallThrust=0:25,1:60,2:8,23:80*LargeBlockLargeThrust=0:150,1:100,2:40,23:960*LargeBlockLargeHydrogenThrust=0:150,1:180,4:250,2:40*LargeBlockSmallHydrogenThrust=0:25,1:60,4:40,2:8*SmallBlockLargeHydrogenThrust=0:30,1:30,4:22,2:10*SmallBlockSmallHydrogenThrust=0:7,1:15,4:4,2:2*LargeBlockLargeAtmosphericThrust=0:230,1:60,2:50,4:40,5:1100*LargeBlockSmallAtmosphericThrust=0:35,1:50,2:8,4:10,5:110*SmallBlockLargeAtmosphericThrust=0:20,1:30,2:4,4:8,5:90*SmallBlockSmallAtmosphericThrust=0:3,1:22,2:1,4:1,5:18*LargeBlockLargeFlatAtmosphericThrust=0:90,1:25,2:20,4:15,5:400*LargeBlockLargeFlatAtmosphericThrustDShape=0:90,1:25,2:20,4:15,5:400*LargeBlockSmallFlatAtmosphericThrust=0:15,1:20,2:3,4:3,5:30*LargeBlockSmallFlatAtmosphericThrustDShape=0:15,1:20,2:3,4:3,5:30*SmallBlockLargeFlatAtmosphericThrust=0:8,1:14,2:2,4:3,5:30*SmallBlockLargeFlatAtmosphericThrustDShape=0:8,1:14,2:2,4:3,5:30*SmallBlockSmallFlatAtmosphericThrust=0:2,1:11,2:1,4:1,5:6*SmallBlockSmallFlatAtmosphericThrustDShape=0:2,1:11,2:1,4:1,5:6*SmallBlockSmallModularThruster=0:2,1:2,2:1,23:1*SmallBlockLargeModularThruster=0:5,1:2,2:5,23:12*LargeBlockSmallModularThruster=0:25,1:60,2:8,23:80*LargeBlockLargeModularThruster=0:150,1:100,2:40,23:960$Passage*(null)=7:74,1:20,10:48$Collector*Collector=0:45,1:50,10:12,5:8,6:4,3:10*CollectorSmall=0:35,1:35,10:12,5:8,6:2,3:8$ShipConnector*Connector=0:150,1:40,10:12,5:8,3:20*ConnectorSmall=0:7,1:4,10:2,5:1,3:4*ConnectorMedium=0:21,1:12,10:6,5:6,3:6$PistonBase*LargePistonBase=0:15,1:10,2:4,5:4,3:2*SmallPistonBase=0:4,1:4,10:4,5:2,3:1$ExtendedPistonBase*LargePistonBase=0:15,1:10,2:4,5:4,3:2*SmallPistonBase=0:4,1:4,10:4,5:2,3:1$PistonTop*LargePistonTop=0:10,2:8*SmallPistonTop=0:4,2:2$MotorStator*LargeStator=0:15,1:10,2:4,5:4,3:2*SmallStator=0:5,1:5,10:1,5:1,3:1$MotorRotor*LargeRotor=0:30,2:6*SmallRotor=0:12,10:6$MotorAdvancedStator*LargeAdvancedStator=0:15,1:10,2:4,5:4,3:2*SmallAdvancedStator=0:10,1:6,10:1,5:2,3:2*SmallAdvancedStatorSmall=0:5,1:5,10:1,5:1,3:1*LargeHinge=0:16,1:10,2:4,5:4,3:2*MediumHinge=0:10,1:6,2:2,5:2,3:2*SmallHinge=0:6,1:4,2:1,5:2,3:2$MotorAdvancedRotor*LargeAdvancedRotor=0:30,2:10*SmallAdvancedRotor=0:20,2:6*SmallAdvancedRotorSmall=0:12,10:6*LargeHingeHead=0:12,2:4,1:8*MediumHingeHead=0:6,2:2,1:4*SmallHingeHead=0:3,2:1,1:2$OxygenGenerator*(null)=0:120,1:5,2:2,5:4,3:5*OxygenGeneratorSmall=0:8,1:8,2:2,5:1,3:3$SurvivalKit*SurvivalKitLarge=0:30,1:2,13:3,5:4,6:1,3:5*SurvivalKit=0:6,1:2,13:3,5:4,6:1,3:5$OxygenFarm*LargeBlockOxygenFarm=0:40,12:100,2:20,10:10,1:20,3:20$UpgradeModule*LargeProductivityModule=0:100,1:40,10:20,3:60,5:4*LargeEffectivenessModule=0:100,1:50,10:15,11:20,5:4*LargeEnergyModule=0:100,1:40,10:20,19:20,5:4$ExhaustBlock*SmallExhaustPipe=0:2,1:1,10:2,5:2*LargeExhaustPipe=0:15,1:10,2:2,5:4$MotorSuspension*OffroadSuspension3x3=0:25,1:15,2:6,10:12,5:6*OffroadSuspension5x5=0:70,1:40,2:20,10:30,5:20*OffroadSuspension1x1=0:25,1:15,2:6,10:12,5:6*OffroadSuspension2x2=0:25,1:15,2:6,10:12,5:6*OffroadSmallSuspension3x3=0:8,1:7,10:2,5:1*OffroadSmallSuspension5x5=0:16,1:12,10:4,5:2*OffroadSmallSuspension1x1=0:8,1:7,10:2,5:1*OffroadSmallSuspension2x2=0:8,1:7,10:2,5:1*OffroadSuspension3x3mirrored=0:25,1:15,2:6,10:12,5:6*OffroadSuspension5x5mirrored=0:70,1:40,2:20,10:30,5:20*OffroadSuspension1x1mirrored=0:25,1:15,2:6,10:12,5:6*OffroadSuspension2x2Mirrored=0:25,1:15,2:6,10:12,5:6*OffroadSmallSuspension3x3mirrored=0:8,1:7,10:2,5:1*OffroadSmallSuspension5x5mirrored=0:16,1:12,10:4,5:2*OffroadSmallSuspension1x1mirrored=0:8,1:7,10:2,5:1*OffroadSmallSuspension2x2Mirrored=0:8,1:7,10:2,5:1*OffroadShortSuspension3x3=0:15,1:10,2:6,10:12,5:6*OffroadShortSuspension5x5=0:45,1:30,2:20,10:30,5:20*OffroadShortSuspension1x1=0:15,1:10,2:6,10:12,5:6*OffroadShortSuspension2x2=0:15,1:10,2:6,10:12,5:6*OffroadSmallShortSuspension3x3=0:5,1:5,10:2,5:1*OffroadSmallShortSuspension5x5=0:10,1:10,10:4,5:2*OffroadSmallShortSuspension1x1=0:5,1:5,10:2,5:1*OffroadSmallShortSuspension2x2=0:5,1:5,10:2,5:1*OffroadShortSuspension3x3mirrored=0:15,1:10,2:6,10:12,5:6*OffroadShortSuspension5x5mirrored=0:45,1:30,2:20,10:30,5:20*OffroadShortSuspension1x1mirrored=0:15,1:10,2:6,10:12,5:6*OffroadShortSuspension2x2Mirrored=0:15,1:10,2:6,10:12,5:6*OffroadSmallShortSuspension3x3mirrored=0:5,1:5,10:2,5:1*OffroadSmallShortSuspension5x5mirrored=0:10,1:10,10:4,5:2*OffroadSmallShortSuspension1x1mirrored=0:5,1:5,10:2,5:1*OffroadSmallShortSuspension2x2Mirrored=0:5,1:5,10:2,5:1*Suspension3x3=0:25,1:15,2:6,10:12,5:6*Suspension5x5=0:70,1:40,2:20,10:30,5:20*Suspension1x1=0:25,1:15,2:6,10:12,5:6*Suspension2x2=0:25,1:15,2:6,10:12,5:6*SmallSuspension3x3=0:8,1:7,10:2,5:1*SmallSuspension5x5=0:16,1:12,10:4,5:2*SmallSuspension1x1=0:8,1:7,10:2,5:1*SmallSuspension2x2=0:8,1:7,10:2,5:1*Suspension3x3mirrored=0:25,1:15,2:6,10:12,5:6*Suspension5x5mirrored=0:70,1:40,2:20,10:30,5:20*Suspension1x1mirrored=0:25,1:15,2:6,10:12,5:6*Suspension2x2Mirrored=0:25,1:15,2:6,10:12,5:6*SmallSuspension3x3mirrored=0:8,1:7,10:2,5:1*SmallSuspension5x5mirrored=0:16,1:12,10:4,5:2*SmallSuspension1x1mirrored=0:8,1:7,10:2,5:1*SmallSuspension2x2Mirrored=0:8,1:7,10:2,5:1*ShortSuspension3x3=0:15,1:10,2:6,10:12,5:6*ShortSuspension5x5=0:45,1:30,2:20,10:30,5:20*ShortSuspension1x1=0:15,1:10,2:6,10:12,5:6*ShortSuspension2x2=0:15,1:10,2:6,10:12,5:6*SmallShortSuspension3x3=0:5,1:5,10:2,5:1*SmallShortSuspension5x5=0:10,1:10,10:4,5:2*SmallShortSuspension1x1=0:5,1:5,10:2,5:1*SmallShortSuspension2x2=0:5,1:5,10:2,5:1*ShortSuspension3x3mirrored=0:15,1:10,2:6,10:12,5:6*ShortSuspension5x5mirrored=0:45,1:30,2:20,10:30,5:20*ShortSuspension1x1mirrored=0:15,1:10,2:6,10:12,5:6*ShortSuspension2x2Mirrored=0:15,1:10,2:6,10:12,5:6*SmallShortSuspension3x3mirrored=0:5,1:5,10:2,5:1*SmallShortSuspension5x5mirrored=0:10,1:10,10:4,5:2*SmallShortSuspension1x1mirrored=0:5,1:5,10:2,5:1*SmallShortSuspension2x2Mirrored=0:5,1:5,10:2,5:1$Wheel*OffroadSmallRealWheel1x1=0:2,1:5,2:1*OffroadSmallRealWheel2x2=0:8,1:15,2:3*OffroadSmallRealWheel=0:8,1:15,2:3*OffroadSmallRealWheel5x5=0:15,1:25,2:5*OffroadRealWheel1x1=0:30,1:30,2:10*OffroadRealWheel2x2=0:50,1:40,2:15*OffroadRealWheel=0:70,1:50,2:20*OffroadRealWheel5x5=0:130,1:70,2:30*OffroadSmallRealWheel1x1mirrored=0:2,1:5,2:1*OffroadSmallRealWheel2x2Mirrored=0:8,1:15,2:3*OffroadSmallRealWheelmirrored=0:8,1:15,2:3*OffroadSmallRealWheel5x5mirrored=0:15,1:25,2:5*OffroadRealWheel1x1mirrored=0:30,1:30,2:10*OffroadRealWheel2x2Mirrored=0:50,1:40,2:15*OffroadRealWheelmirrored=0:70,1:50,2:20*OffroadRealWheel5x5mirrored=0:130,1:70,2:30*OffroadWheel1x1=0:30,1:30,2:10*OffroadSmallWheel1x1=0:2,1:5,2:1*OffroadWheel3x3=0:70,1:50,2:20*OffroadSmallWheel3x3=0:8,1:15,2:3*OffroadWheel5x5=0:130,1:70,2:30*OffroadSmallWheel5x5=0:15,1:25,2:5*OffroadWheel2x2=0:50,1:40,2:15*OffroadSmallWheel2x2=0:5,1:10,2:2*SmallRealWheel1x1=0:2,1:5,2:1*SmallRealWheel2x2=0:8,1:15,2:3*SmallRealWheel=0:8,1:15,2:3*SmallRealWheel5x5=0:15,1:25,2:5*RealWheel1x1=0:30,1:30,2:10*RealWheel2x2=0:50,1:40,2:15*RealWheel=0:70,1:50,2:20*RealWheel5x5=0:130,1:70,2:30*SmallRealWheel1x1mirrored=0:2,1:5,2:1*SmallRealWheel2x2Mirrored=0:8,1:15,2:3*SmallRealWheelmirrored=0:8,1:15,2:3*SmallRealWheel5x5mirrored=0:15,1:25,2:5*RealWheel1x1mirrored=0:30,1:30,2:10*RealWheel2x2Mirrored=0:50,1:40,2:15*RealWheelmirrored=0:70,1:50,2:20*RealWheel5x5mirrored=0:130,1:70,2:30*Wheel1x1=0:30,1:30,2:10*SmallWheel1x1=0:2,1:5,2:1*Wheel3x3=0:70,1:50,2:20*SmallWheel3x3=0:8,1:15,2:3*Wheel5x5=0:130,1:70,2:30*SmallWheel5x5=0:15,1:25,2:5*Wheel2x2=0:50,1:40,2:15*SmallWheel2x2=0:5,1:10,2:2$Decoy*TrussPillarDecoy=0:30,1:10,3:10,8:1,2:2*LargeDecoy=0:30,1:10,3:10,8:1,2:2*SmallDecoy=0:2,1:1,3:1,8:1,10:2$EmissiveBlock*LargeNeonTubesStraight1=7:6,10:6,1:2*LargeNeonTubesStraight2=7:6,10:6,1:2*LargeNeonTubesCorner=7:6,10:6,1:2*LargeNeonTubesBendUp=7:12,10:12,1:4*LargeNeonTubesBendDown=7:3,10:3,1:1*LargeNeonTubesStraightEnd1=7:6,10:6,1:2*LargeNeonTubesStraightEnd2=7:10,10:6,1:4*LargeNeonTubesStraightDown=7:9,10:9,1:3*LargeNeonTubesU=7:18,10:18,1:6*LargeNeonTubesT=7:9,10:9,1:3*LargeNeonTubesCircle=7:12,10:12,1:4*SmallNeonTubesStraight1=7:1,10:1,1:1*SmallNeonTubesStraight2=7:1,10:1,1:1*SmallNeonTubesCorner=7:1,10:1,1:1*SmallNeonTubesBendUp=7:1,10:1,1:1*SmallNeonTubesBendDown=7:1,10:1,1:1*SmallNeonTubesStraightDown=7:1,10:1,1:1*SmallNeonTubesStraightEnd1=7:1,10:1,1:1*SmallNeonTubesU=7:1,10:1,1:1*SmallNeonTubesT=7:1,10:1,1:1*SmallNeonTubesCircle=7:1,10:1,1:1$Drill*SmallBlockDrill=0:32,1:30,2:4,5:1,3:1*LargeBlockDrill=0:300,1:40,2:12,5:5,3:5$ShipGrinder*LargeShipGrinder=0:20,1:30,2:1,5:4,3:2*SmallShipGrinder=0:12,1:17,10:4,5:4,3:2$ShipWelder*LargeShipWelder=0:20,1:30,2:1,5:2,3:2*SmallShipWelder=0:12,1:17,10:6,5:2,3:2$OreDetector*LargeOreDetector=0:50,1:40,5:5,3:25,9:20*SmallBlockOreDetector=0:3,1:2,5:1,3:1,9:1$JumpDrive*LargeJumpDrive=0:60,4:50,16:20,9:20,19:120,11:1000,3:300,1:40$MergeBlock*LargeShipMergeBlock=0:12,1:15,5:2,2:6,3:2*SmallShipMergeBlock=0:4,1:5,5:1,10:2,3:1*SmallShipSmallMergeBlock=0:2,1:3,5:1,10:1,3:1$Parachute*LgParachute=0:9,1:25,10:5,5:3,3:2*SmParachute=0:2,1:2,10:1,5:1,3:1$SmallMissileLauncher*SmallMissileLauncherWarfare2=0:4,1:2,4:1,2:4,5:1,3:1*(null)=0:4,1:2,4:1,2:4,5:1,3:1*LargeMissileLauncher=0:35,1:8,4:30,2:25,5:6,3:4*LargeBlockLargeCalibreGun=0:250,1:20,4:20,2:20,3:5*LargeFlareLauncher=0:20,1:10,2:10,3:4*SmallFlareLauncher=0:2,1:1,2:3,3:1$SmallGatlingGun*SmallGatlingGunWarfare2=0:4,1:1,4:2,10:6,5:1,3:1*(null)=0:4,1:1,4:2,10:6,5:1,3:1*SmallBlockAutocannon=0:6,1:2,4:2,10:2,5:1,3:1$Searchlight*SmallSearchlight=0:1,1:3,2:1,5:2,3:5,12:2*LargeSearchlight=0:5,1:20,2:2,5:4,3:5,12:4$HeatVentBlock*LargeHeatVentBlock=0:25,1:20,2:10,5:5*SmallHeatVentBlock=0:2,1:1,2:1,5:1$LargeGatlingTurret*(null)=0:40,1:40,4:15,10:6,5:8,3:10*SmallGatlingTurret=0:15,1:30,4:5,10:6,5:4,3:10*AutoCannonTurret=0:20,1:40,4:6,10:4,5:4,3:10$LargeMissileTurret*(null)=0:40,1:50,4:15,2:6,5:16,3:10*SmallMissileTurret=0:15,1:40,4:5,2:2,5:8,3:10*LargeCalibreTurret=0:450,1:400,4:50,2:40,5:30,3:20*LargeBlockMediumCalibreTurret=0:300,1:280,4:30,2:30,5:20,3:20*SmallBlockMediumCalibreTurret=0:50,1:100,4:10,2:6,5:10,3:20$InteriorTurret*LargeInteriorTurret=7:6,1:20,10:1,5:2,3:5,0:4$SmallMissileLauncherReload*SmallRocketLauncherReload=10:50,7:50,1:24,2:8,4:10,5:4,3:2,0:8*SmallBlockMediumCalibreGun=0:25,1:10,4:5,2:10,3:1*LargeRailgun=0:350,1:150,11:150,2:60,19:100,3:100*SmallRailgun=0:25,1:20,11:20,2:6,19:10,3:20";
        // the line above this one is really long
    }
}