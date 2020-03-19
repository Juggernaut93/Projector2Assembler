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

        string blockDefinitionData = "SteelPlate*ConstructionComponent*LargeTube*ComputerComponent*MetalGrid*MotorComponent*Display*InteriorPlate*RadioCommunicationComponent*DetectorComponent*SmallTube*Superconductor*BulletproofGlass*MedicalComponent*GirderComponent*GravityGeneratorComponent*ZoneChip*PowerCell*ReactorComponent*SolarCell*ThrustComponent*ExplosivesComponent$CubeBlock*LargeRailStraight=0:12,1:8,2:4*LargeBlockArmorBlock=0:25*LargeBlockArmorSlope=0:13*LargeBlockArmorCorner=0:4*LargeBlockArmorCornerInv=0:21*LargeRoundArmor_Slope=0:13*LargeRoundArmor_Corner=0:4*LargeRoundArmor_CornerInv=0:21*LargeHeavyBlockArmorBlock=0:150,4:50*LargeHeavyBlockArmorSlope=0:75,4:25*LargeHeavyBlockArmorCorner=0:25,4:10*LargeHeavyBlockArmorCornerInv=0:125,4:50*SmallBlockArmorBlock=0:1*SmallBlockArmorSlope=0:1*SmallBlockArmorCorner=0:1*SmallBlockArmorCornerInv=0:1*SmallHeavyBlockArmorBlock=0:5,4:2*SmallHeavyBlockArmorSlope=0:3,4:1*SmallHeavyBlockArmorCorner=0:2,4:1*SmallHeavyBlockArmorCornerInv=0:4,4:1*LargeHalfArmorBlock=0:12*LargeHeavyHalfArmorBlock=0:75,4:25*LargeHalfSlopeArmorBlock=0:7*LargeHeavyHalfSlopeArmorBlock=0:45,4:15*HalfArmorBlock=0:1*HeavyHalfArmorBlock=0:3,4:1*HalfSlopeArmorBlock=0:1*HeavyHalfSlopeArmorBlock=0:2,4:1*LargeBlockArmorRoundSlope=0:13*LargeBlockArmorRoundCorner=0:4*LargeBlockArmorRoundCornerInv=0:21*LargeHeavyBlockArmorRoundSlope=0:130,4:50*LargeHeavyBlockArmorRoundCorner=0:125,4:40*LargeHeavyBlockArmorRoundCornerInv=0:140,4:50*SmallBlockArmorRoundSlope=0:1*SmallBlockArmorRoundCorner=0:1*SmallBlockArmorRoundCornerInv=0:1*SmallHeavyBlockArmorRoundSlope=0:4,4:1*SmallHeavyBlockArmorRoundCorner=0:4,4:1*SmallHeavyBlockArmorRoundCornerInv=0:5,4:1*LargeBlockArmorSlope2Base=0:19*LargeBlockArmorSlope2Tip=0:7*LargeBlockArmorCorner2Base=0:10*LargeBlockArmorCorner2Tip=0:4*LargeBlockArmorInvCorner2Base=0:22*LargeBlockArmorInvCorner2Tip=0:16*LargeHeavyBlockArmorSlope2Base=0:112,4:45*LargeHeavyBlockArmorSlope2Tip=0:35,4:6*LargeHeavyBlockArmorCorner2Base=0:55,4:15*LargeHeavyBlockArmorCorner2Tip=0:19,4:6*LargeHeavyBlockArmorInvCorner2Base=0:133,4:45*LargeHeavyBlockArmorInvCorner2Tip=0:94,4:25*SmallBlockArmorSlope2Base=0:1*SmallBlockArmorSlope2Tip=0:1*SmallBlockArmorCorner2Base=0:1*SmallBlockArmorCorner2Tip=0:1*SmallBlockArmorInvCorner2Base=0:1*SmallBlockArmorInvCorner2Tip=0:1*SmallHeavyBlockArmorSlope2Base=0:4,4:1*SmallHeavyBlockArmorSlope2Tip=0:2,4:1*SmallHeavyBlockArmorCorner2Base=0:3,4:1*SmallHeavyBlockArmorCorner2Tip=0:2,4:1*SmallHeavyBlockArmorInvCorner2Base=0:5,4:1*SmallHeavyBlockArmorInvCorner2Tip=0:5,4:1*LargeBlockDeskChairless=7:30,1:30*LargeBlockDeskChairlessCorner=7:20,1:20*Shower=7:20,1:20,10:12,12:8*WindowWall=0:8,1:10,12:10*WindowWallLeft=0:10,1:10,12:8*WindowWallRight=0:10,1:10,12:8*Catwalk=1:16,14:4,10:20*CatwalkCorner=1:24,14:4,10:32*CatwalkStraight=1:24,14:4,10:32*CatwalkWall=1:20,14:4,10:26*CatwalkRailingEnd=1:28,14:4,10:38*CatwalkRailingHalfRight=1:28,14:4,10:36*CatwalkRailingHalfLeft=1:28,14:4,10:36*GratedStairs=1:22,10:12,7:16*GratedHalfStairs=1:20,10:6,7:8*GratedHalfStairsMirrored=1:20,10:6,7:8*RailingStraight=1:8,10:6*RailingDouble=1:16,10:12*RailingCorner=1:16,10:12*RailingDiagonal=1:12,10:9*RailingHalfRight=1:8,10:4*RailingHalfLeft=1:8,10:4*Freight1=7:6,1:8*Freight2=7:12,1:16*Freight3=7:18,1:24*ArmorCenter=0:140*ArmorCorner=0:120*ArmorInvCorner=0:135*ArmorSide=0:130*SmallArmorCenter=0:5*SmallArmorCorner=0:5*SmallArmorInvCorner=0:5*SmallArmorSide=0:5*Monolith=0:130,11:130*Stereolith=0:130,11:130*DeadAstronaut=0:13,11:13*LargeDeadAstronaut=0:13,11:13*DeadBody01=12:1,8:1,6:1*DeadBody02=12:1,8:1,6:1*DeadBody03=12:1,8:1,6:1*DeadBody04=12:1,8:1,6:1*DeadBody05=12:1,8:1,6:1*DeadBody06=12:1,8:1,6:1*LargeStairs=7:50,1:30*LargeRamp=7:70,1:16*LargeSteelCatwalk=7:27,1:5,10:20*LargeSteelCatwalk2Sides=7:32,1:7,10:25*LargeSteelCatwalkCorner=7:32,1:7,10:25*LargeSteelCatwalkPlate=7:23,1:7,10:17*LargeCoverWall=0:4,1:10*LargeCoverWallHalf=0:2,1:6*LargeBlockInteriorWall=7:25,1:10*LargeInteriorPillar=7:25,1:10,10:4*LargeWindowSquare=7:12,1:8,10:4*LargeWindowEdge=7:16,1:12,10:6*Window1x2Slope=14:16,12:55*Window1x2Inv=14:15,12:40*Window1x2Face=14:15,12:40*Window1x2SideLeft=14:13,12:26*Window1x2SideLeftInv=14:13,12:26*Window1x2SideRight=14:13,12:26*Window1x2SideRightInv=14:13,12:26*Window1x1Slope=14:12,12:35*Window1x1Face=14:11,12:24*Window1x1Side=14:9,12:17*Window1x1SideInv=14:9,12:17*Window1x1Inv=14:11,12:24*Window1x2Flat=14:15,12:50*Window1x2FlatInv=14:15,12:50*Window1x1Flat=14:10,12:25*Window1x1FlatInv=14:10,12:25*Window3x3Flat=14:40,12:196*Window3x3FlatInv=14:40,12:196*Window2x3Flat=14:25,12:140*Window2x3FlatInv=14:25,12:140*SmallWindow1x2Slope=14:1,12:3*SmallWindow1x2Inv=14:1,12:3*SmallWindow1x2Face=14:1,12:3*SmallWindow1x2SideLeft=14:1,12:3*SmallWindow1x2SideLeftInv=14:1,12:3*SmallWindow1x2SideRight=14:1,12:3*SmallWindow1x2SideRightInv=14:1,12:3*SmallWindow1x1Slope=14:1,12:2*SmallWindow1x1Face=14:1,12:2*SmallWindow1x1Side=14:1,12:2*SmallWindow1x1SideInv=14:1,12:2*SmallWindow1x1Inv=14:1,12:2*SmallWindow1x2Flat=14:1,12:3*SmallWindow1x2FlatInv=14:1,12:3*SmallWindow1x1Flat=14:1,12:2*SmallWindow1x1FlatInv=14:1,12:2*SmallWindow3x3Flat=14:3,12:12*SmallWindow3x3FlatInv=14:3,12:12*SmallWindow2x3Flat=14:2,12:8*SmallWindow2x3FlatInv=14:2,12:8$DebugSphere1*DebugSphereLarge=0:10,3:20$DebugSphere2*DebugSphereLarge=0:10,3:20$DebugSphere3*DebugSphereLarge=0:10,3:20$MyProgrammableBlock*SmallProgrammableBlock=0:2,1:2,2:2,5:1,6:1,3:2*LargeProgrammableBlock=0:21,1:4,2:2,5:1,6:1,3:2$Projector*LargeProjector=0:21,1:4,2:2,5:1,3:2*SmallProjector=0:2,1:2,2:2,5:1,3:2*LargeBlockConsole=7:20,1:30,3:8,6:10$SensorBlock*SmallBlockSensor=7:5,1:5,3:6,8:4,9:6,0:2*LargeBlockSensor=7:5,1:5,3:6,8:4,9:6,0:2$SoundBlock*SmallBlockSoundBlock=7:4,1:6,3:3*LargeBlockSoundBlock=7:4,1:6,3:3$ButtonPanel*ButtonPanelLarge=7:10,1:20,3:20*ButtonPanelSmall=7:2,1:2,3:1$TimerBlock*TimerBlockLarge=7:6,1:30,3:5*TimerBlockSmall=7:2,1:3,3:1$RadioAntenna*LargeBlockRadioAntenna=0:80,2:40,10:60,1:30,3:8,8:40*SmallBlockRadioAntenna=0:1,10:1,1:2,3:1,8:4*LargeBlockRadioAntennaDish=1:40,14:120,0:80,3:8,8:40$Beacon*LargeBlockBeacon=0:80,1:30,2:20,3:10,8:40*SmallBlockBeacon=0:2,1:1,10:1,3:1,8:4$RemoteControl*LargeBlockRemoteControl=7:10,1:10,5:1,3:15*SmallBlockRemoteControl=7:2,1:1,5:1,3:1$LaserAntenna*LargeBlockLaserAntenna=0:50,1:40,5:16,9:30,8:20,11:100,3:50,12:4*SmallBlockLaserAntenna=0:10,10:10,1:10,5:5,8:5,11:10,3:30,12:2$TerminalBlock*ControlPanel=0:1,1:1,3:1,6:1*SmallControlPanel=0:1,1:1,3:1,6:1$Cockpit*LargeBlockCockpit=7:20,1:20,5:2,3:100,6:10*LargeBlockCockpitSeat=0:30,1:20,5:1,6:8,3:100,12:60*SmallBlockCockpit=0:10,1:10,5:1,6:5,3:15,12:30*DBSmallBlockFighterCockpit=1:20,5:1,0:20,4:10,7:15,6:4,3:20,12:40*CockpitOpen=7:20,1:20,5:2,3:100,6:4*OpenCockpitSmall=7:20,1:20,5:1,3:15,6:2*OpenCockpitLarge=7:30,1:30,5:2,3:100,6:6*LargeBlockDesk=7:30,1:30*LargeBlockDeskCorner=7:20,1:20*LargeBlockCouch=7:30,1:30*LargeBlockCouchCorner=7:35,1:35*LargeBlockBathroomOpen=7:30,1:30,10:8,5:4,2:2*LargeBlockBathroom=7:30,1:40,10:8,5:4,2:2*LargeBlockToilet=7:10,1:15,10:2,5:2,2:1*SmallBlockCockpitIndustrial=0:10,1:20,4:10,5:2,6:6,3:20,12:60,10:10*LargeBlockCockpitIndustrial=0:20,1:30,4:15,5:2,6:10,3:60,12:80,10:10*PassengerSeatLarge=7:20,1:20*PassengerSeatSmall=7:20,1:20$Gyro*LargeBlockGyro=0:600,1:40,2:4,4:50,5:4,3:5*SmallBlockGyro=0:25,1:5,2:1,5:2,3:3$Kitchen*LargeBlockKitchen=7:20,1:30,2:6,5:6,12:4$CryoChamber*LargeBlockBed=7:30,1:30,10:8,12:10*LargeBlockCryoChamber=7:40,1:20,5:8,6:8,13:3,3:30,12:10*SmallBlockCryoChamber=7:20,1:10,5:4,6:4,13:3,3:15,12:5$CargoContainer*LargeBlockLockerRoom=7:30,1:30,6:4,12:10*LargeBlockLockerRoomCorner=7:25,1:30,6:4,12:10*LargeBlockLockers=7:20,1:20,6:3,3:2*SmallBlockSmallContainer=7:3,1:1,3:1,5:1,6:1*SmallBlockMediumContainer=7:30,1:10,3:4,5:4,6:1*SmallBlockLargeContainer=7:75,1:25,3:6,5:8,6:1*LargeBlockSmallContainer=7:40,1:40,4:4,10:20,5:4,6:1,3:2*LargeBlockLargeContainer=7:360,1:80,4:24,10:60,5:20,6:1,3:8$Planter*LargeBlockPlanters=7:10,1:20,10:8,12:8$VendingMachine*FoodDispenser=7:20,1:10,5:4,6:10,3:10*VendingMachine=7:20,1:10,5:4,6:4,3:10$Jukebox*Jukebox=7:15,1:10,3:4,6:4$LCDPanelsBlock*LabEquipment=7:15,1:15,5:1,12:4*MedicalStation=7:15,1:15,5:2,13:1,6:2$TextPanel*TransparentLCDLarge=1:8,3:6,6:10,12:10*TransparentLCDSmall=1:4,3:4,6:3,12:1*SmallTextPanel=7:1,1:4,3:4,6:3,12:1*SmallLCDPanelWide=7:1,1:8,3:8,6:6,12:2*SmallLCDPanel=7:1,1:4,3:4,6:3,12:2*LargeBlockCorner_LCD_1=1:5,3:3,6:1*LargeBlockCorner_LCD_2=1:5,3:3,6:1*LargeBlockCorner_LCD_Flat_1=1:5,3:3,6:1*LargeBlockCorner_LCD_Flat_2=1:5,3:3,6:1*SmallBlockCorner_LCD_1=1:3,3:2,6:1*SmallBlockCorner_LCD_2=1:3,3:2,6:1*SmallBlockCorner_LCD_Flat_1=1:3,3:2,6:1*SmallBlockCorner_LCD_Flat_2=1:3,3:2,6:1*LargeTextPanel=7:1,1:6,3:6,6:10,12:2*LargeLCDPanel=7:1,1:6,3:6,6:10,12:6*LargeLCDPanelWide=7:2,1:12,3:12,6:20,12:12$ReflectorLight*RotatingLightLarge=1:3,5:1*RotatingLightSmall=1:3,5:1*LargeBlockFrontLight=0:8,2:2,7:20,1:15,12:4*SmallBlockFrontLight=0:1,2:1,7:1,1:1,12:2$Door*(null)=7:10,1:40,10:4,5:2,6:1,3:2,0:8*LargeBlockGate=0:800,1:100,10:100,5:20,3:10*LargeBlockOffsetDoor=0:25,1:35,10:4,5:4,6:1,3:2,12:6$AirtightHangarDoor*(null)=0:350,1:40,10:40,5:16,3:2$AirtightSlideDoor*LargeBlockSlideDoor=0:20,1:40,10:4,5:4,6:1,3:2,12:15$StoreBlock*StoreBlock=0:30,1:20,5:6,6:4,3:10*AtmBlock=0:20,1:20,5:2,3:10,6:4$SafeZoneBlock*SafeZoneBlock=0:800,1:180,15:10,16:5,4:80,3:120$ContractBlock*ContractBlock=0:30,1:20,5:6,6:4,3:10$BatteryBlock*LargeBlockBatteryBlock=0:80,1:30,17:80,3:25*SmallBlockBatteryBlock=0:25,1:5,17:20,3:2*SmallBlockSmallBatteryBlock=0:4,1:2,17:2,3:2$Reactor*SmallBlockSmallGenerator=0:3,1:10,4:2,2:1,18:3,5:1,3:10*SmallBlockLargeGenerator=0:60,1:9,4:9,2:3,18:95,5:5,3:25*LargeBlockSmallGenerator=0:80,1:40,4:4,2:8,18:100,5:6,3:25*LargeBlockLargeGenerator=0:1000,1:70,4:40,2:40,11:100,18:2000,5:20,3:75$HydrogenEngine*LargeHydrogenEngine=0:100,1:70,2:12,10:20,5:12,3:4,17:1*SmallHydrogenEngine=0:30,1:20,2:4,10:6,5:4,3:1,17:1$WindTurbine*LargeBlockWindTurbine=7:40,5:8,1:20,14:24,3:2$SolarPanel*LargeBlockSolarPanel=0:4,1:14,14:12,3:4,19:32,12:4*SmallBlockSolarPanel=0:2,1:2,14:4,3:1,19:8,12:1$GravityGenerator*(null)=0:150,15:6,1:60,2:4,5:6,3:40$GravityGeneratorSphere*(null)=0:150,15:6,1:60,2:4,5:6,3:40$VirtualMass*VirtualMassLarge=0:90,11:20,1:30,3:20,15:9*VirtualMassSmall=0:3,11:2,1:2,3:2,15:1$SpaceBall*SpaceBallLarge=0:225,1:30,3:20,15:3*SpaceBallSmall=0:70,1:10,3:7,15:1$Passage*(null)=7:74,1:20,10:48$Ladder2*(null)=7:10,1:20,10:10*LadderSmall=7:10,1:20,10:10$InteriorLight*SmallLight=1:2*SmallBlockSmallLight=1:2*LargeBlockLight_1corner=1:3*LargeBlockLight_2corner=1:6*SmallBlockLight_1corner=1:2*SmallBlockLight_2corner=1:4$OxygenTank*OxygenTankSmall=0:16,2:8,10:10,3:8,1:10*(null)=0:80,2:40,10:60,3:8,1:40*LargeHydrogenTank=0:280,2:80,10:60,3:8,1:40*LargeHydrogenTankSmall=0:80,2:40,10:60,3:8,1:40*SmallHydrogenTank=0:80,2:40,10:60,3:8,1:40*SmallHydrogenTankSmall=0:8,2:2,10:4,3:8,1:6$AirVent*(null)=0:45,1:20,5:10,3:5*SmallAirVent=0:8,1:10,5:2,3:5$Conveyor*SmallBlockConveyor=7:4,1:4,5:1*LargeBlockConveyor=7:20,1:30,10:20,5:6*SmallShipConveyorHub=7:25,1:45,10:25,5:2$Collector*Collector=0:45,1:50,10:12,5:8,6:4,3:10*CollectorSmall=0:35,1:35,10:12,5:8,6:2,3:8$ShipConnector*Connector=0:150,1:40,10:12,5:8,3:20*ConnectorSmall=0:7,1:4,10:2,5:1,3:4*ConnectorMedium=0:21,1:12,10:6,5:6,3:6$ConveyorConnector*ConveyorTube=7:14,1:20,10:12,5:6*ConveyorTubeSmall=7:1,5:1,1:1*ConveyorTubeMedium=7:10,1:20,10:10,5:6*ConveyorFrameMedium=7:5,1:12,10:5,5:2*ConveyorTubeCurved=7:14,1:20,10:12,5:6*ConveyorTubeSmallCurved=7:1,5:1,1:1*ConveyorTubeCurvedMedium=7:7,1:20,10:10,5:6$ConveyorSorter*LargeBlockConveyorSorter=7:50,1:120,10:50,3:20,5:2*MediumBlockConveyorSorter=7:5,1:12,10:5,3:5,5:2*SmallBlockConveyorSorter=7:5,1:12,10:5,3:5,5:2$PistonBase*LargePistonBase=0:15,1:10,2:4,5:4,3:2*SmallPistonBase=0:4,1:4,10:4,5:2,3:1$ExtendedPistonBase*LargePistonBase=0:15,1:10,2:4,5:4,3:2*SmallPistonBase=0:4,1:4,10:4,5:2,3:1$PistonTop*LargePistonTop=0:10,2:8*SmallPistonTop=0:4,2:2$MotorStator*LargeStator=0:15,1:10,2:4,5:4,3:2*SmallStator=0:5,1:5,10:1,5:1,3:1$MotorRotor*LargeRotor=0:30,2:6*SmallRotor=0:12,10:6$MotorAdvancedStator*LargeAdvancedStator=0:15,1:10,2:4,5:4,3:2*SmallAdvancedStator=0:5,1:5,10:1,5:1,3:1$MotorAdvancedRotor*LargeAdvancedRotor=0:30,2:10*SmallAdvancedRotor=0:30,2:10$MedicalRoom*LargeMedicalRoom=7:240,1:80,4:60,10:20,2:5,6:10,3:10,13:15$Refinery*LargeRefinery=0:1200,1:40,2:20,5:16,4:20,3:20*BlastFurnace=0:120,1:20,5:10,3:10$OxygenGenerator*(null)=0:120,1:5,2:2,5:4,3:5*OxygenGeneratorSmall=0:8,1:8,2:2,5:1,3:3$Assembler*LargeAssembler=0:140,1:80,5:20,6:10,4:10,3:160*BasicAssembler=0:80,1:40,5:10,6:4,3:80$SurvivalKit*SurvivalKitLarge=0:30,1:2,13:3,5:4,6:1,3:5*SurvivalKit=0:6,1:2,13:3,5:4,6:1,3:5$OxygenFarm*LargeBlockOxygenFarm=0:40,12:100,2:20,10:10,1:20,3:20$UpgradeModule*LargeProductivityModule=0:100,1:40,10:20,3:60,5:4*LargeEffectivenessModule=0:100,1:50,10:15,11:20,5:4*LargeEnergyModule=0:100,1:40,10:20,17:20,5:4$Thrust*SmallBlockSmallThrust=0:2,1:2,2:1,20:1*SmallBlockLargeThrust=0:5,1:2,2:5,20:12*LargeBlockSmallThrust=0:25,1:60,2:8,20:80*LargeBlockLargeThrust=0:150,1:100,2:40,20:960*LargeBlockLargeHydrogenThrust=0:150,1:180,4:250,2:40*LargeBlockSmallHydrogenThrust=0:25,1:60,4:40,2:8*SmallBlockLargeHydrogenThrust=0:30,1:30,4:22,2:10*SmallBlockSmallHydrogenThrust=0:7,1:15,4:4,2:2*LargeBlockLargeAtmosphericThrust=0:230,1:60,2:50,4:40,5:1100*LargeBlockSmallAtmosphericThrust=0:35,1:50,2:8,4:10,5:110*SmallBlockLargeAtmosphericThrust=0:20,1:30,2:4,4:8,5:90*SmallBlockSmallAtmosphericThrust=0:3,1:22,2:1,4:1,5:18$Drill*SmallBlockDrill=0:32,1:30,2:4,5:1,3:1*LargeBlockDrill=0:300,1:40,2:12,5:5,3:5$ShipGrinder*LargeShipGrinder=0:20,1:30,2:1,5:4,3:2*SmallShipGrinder=0:12,1:17,10:4,5:4,3:2$ShipWelder*LargeShipWelder=0:20,1:30,2:1,5:2,3:2*SmallShipWelder=0:12,1:17,10:6,5:2,3:2$OreDetector*LargeOreDetector=0:50,1:40,5:5,3:25,9:20*SmallBlockOreDetector=0:3,1:2,5:1,3:1,9:1$LandingGear*LargeBlockLandingGear=0:150,1:20,5:6*SmallBlockLandingGear=0:2,1:5,5:1$JumpDrive*LargeJumpDrive=0:60,4:50,15:20,9:20,17:120,11:1000,3:300,1:40$CameraBlock*SmallCameraBlock=0:2,3:3*LargeCameraBlock=0:2,3:3$MergeBlock*LargeShipMergeBlock=0:12,1:15,5:2,2:6,3:2*SmallShipMergeBlock=0:4,1:5,5:1,10:2,3:1$Parachute*LgParachute=0:9,1:25,10:5,5:3,3:2*SmParachute=0:2,1:2,10:1,5:1,3:1$Warhead*LargeWarhead=0:20,14:24,1:12,10:12,3:2,21:6*SmallWarhead=0:4,14:1,1:1,10:2,3:1,21:2$Decoy*LargeDecoy=0:30,1:10,3:10,8:1,2:2*SmallDecoy=0:2,1:1,3:1,8:1,10:2$LargeGatlingTurret*(null)=0:20,1:30,4:15,10:6,5:8,3:10*SmallGatlingTurret=0:10,1:30,4:5,10:6,5:4,3:10$LargeMissileTurret*(null)=0:20,1:40,4:5,2:6,5:16,3:12*SmallMissileTurret=0:10,1:40,4:2,2:2,5:8,3:12$InteriorTurret*LargeInteriorTurret=7:6,1:20,10:1,5:2,3:5,0:4$SmallMissileLauncher*(null)=0:4,1:2,4:1,2:4,5:1,3:1*LargeMissileLauncher=0:35,1:8,4:30,2:25,5:6,3:4$SmallMissileLauncherReload*SmallRocketLauncherReload=10:50,7:50,1:24,2:8,4:10,5:4,3:2,0:8$SmallGatlingGun*(null)=0:4,1:1,4:2,10:6,5:1,3:1$MotorSuspension*Suspension3x3=0:25,1:15,2:6,10:12,5:6*Suspension5x5=0:70,1:40,2:20,10:30,5:20*Suspension1x1=0:25,1:15,2:6,10:12,5:6*SmallSuspension3x3=0:8,1:7,10:2,5:1*SmallSuspension5x5=0:16,1:12,10:4,5:2*SmallSuspension1x1=0:8,1:7,10:2,5:1*Suspension3x3mirrored=0:25,1:15,2:6,10:12,5:6*Suspension5x5mirrored=0:70,1:40,2:20,10:30,5:20*Suspension1x1mirrored=0:25,1:15,2:6,10:12,5:6*SmallSuspension3x3mirrored=0:8,1:7,10:2,5:1*SmallSuspension5x5mirrored=0:16,1:12,10:4,5:2*SmallSuspension1x1mirrored=0:8,1:7,10:2,5:1$Wheel*SmallRealWheel1x1=0:2,1:5,2:1*SmallRealWheel=0:5,1:10,2:1*SmallRealWheel5x5=0:7,1:15,2:2*RealWheel1x1=0:8,1:20,2:4*RealWheel=0:12,1:25,2:6*RealWheel5x5=0:16,1:30,2:8*SmallRealWheel1x1mirrored=0:2,1:5,2:1*SmallRealWheelmirrored=0:5,1:10,2:1*SmallRealWheel5x5mirrored=0:7,1:15,2:2*RealWheel1x1mirrored=0:8,1:20,2:4*RealWheelmirrored=0:12,1:25,2:6*RealWheel5x5mirrored=0:16,1:30,2:8*Wheel1x1=0:8,1:20,2:4*SmallWheel1x1=0:2,1:5,2:1*Wheel3x3=0:12,1:25,2:6*SmallWheel3x3=0:5,1:10,2:1*Wheel5x5=0:16,1:30,2:8*SmallWheel5x5=0:7,1:15,2:2";
        // the line above this one is really long
    }
}