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
                    catch (Exception e)
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
                        var itemList = b.GetInventory(i).GetItems();
                        foreach (var item in itemList)
                        {
                            if (item.Content.TypeId.ToString().Equals("MyObjectBuilder_Component"))
                            {
                                AddCountToDict(componentAmounts, item.Content.SubtypeId.ToString(), item.Amount.ToIntSafe());
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

        string blockDefinitionData = "SteelPlate*Superconductor*ConstructionComponent*PowerCell*ComputerComponent*MetalGrid*Display*LargeTube*MotorComponent*InteriorPlate*SmallTube*RadioCommunicationComponent*BulletproofGlass*GirderComponent*ExplosivesComponent*DetectorComponent*MedicalComponent*GravityGeneratorComponent*ThrustComponent*ReactorComponent*SolarCell$CubeBlock*Monolith=0:130,1:130*Stereolith=0:130,1:130*DeadAstronaut=0:13,1:13*LargeBlockArmorBlock=0:25*LargeBlockArmorSlope=0:13*LargeBlockArmorCorner=0:4*LargeBlockArmorCornerInv=0:21*LargeRoundArmor_Slope=0:13*LargeRoundArmor_Corner=0:4*LargeRoundArmor_CornerInv=0:21*LargeHeavyBlockArmorBlock=0:150,5:50*LargeHeavyBlockArmorSlope=0:75,5:25*LargeHeavyBlockArmorCorner=0:25,5:10*LargeHeavyBlockArmorCornerInv=0:125,5:50*SmallBlockArmorBlock=0:1*SmallBlockArmorSlope=0:1*SmallBlockArmorCorner=0:1*SmallBlockArmorCornerInv=0:1*SmallHeavyBlockArmorBlock=0:5,5:2*SmallHeavyBlockArmorSlope=0:3,5:1*SmallHeavyBlockArmorCorner=0:2,5:1*SmallHeavyBlockArmorCornerInv=0:4,5:1*LargeHalfArmorBlock=0:12*LargeHeavyHalfArmorBlock=0:75,5:25*LargeHalfSlopeArmorBlock=0:7*LargeHeavyHalfSlopeArmorBlock=0:45,5:15*HalfArmorBlock=0:1*HeavyHalfArmorBlock=0:3,5:1*HalfSlopeArmorBlock=0:1*HeavyHalfSlopeArmorBlock=0:2,5:1*LargeBlockArmorRoundSlope=0:13*LargeBlockArmorRoundCorner=0:4*LargeBlockArmorRoundCornerInv=0:21*LargeHeavyBlockArmorRoundSlope=0:130,5:50*LargeHeavyBlockArmorRoundCorner=0:125,5:40*LargeHeavyBlockArmorRoundCornerInv=0:140,5:50*SmallBlockArmorRoundSlope=0:1*SmallBlockArmorRoundCorner=0:1*SmallBlockArmorRoundCornerInv=0:1*SmallHeavyBlockArmorRoundSlope=0:4,5:1*SmallHeavyBlockArmorRoundCorner=0:4,5:1*SmallHeavyBlockArmorRoundCornerInv=0:5,5:1*LargeBlockArmorSlope2Base=0:19*LargeBlockArmorSlope2Tip=0:7*LargeBlockArmorCorner2Base=0:10*LargeBlockArmorCorner2Tip=0:4*LargeBlockArmorInvCorner2Base=0:22*LargeBlockArmorInvCorner2Tip=0:16*LargeHeavyBlockArmorSlope2Base=0:112,5:45*LargeHeavyBlockArmorSlope2Tip=0:35,5:6*LargeHeavyBlockArmorCorner2Base=0:55,5:15*LargeHeavyBlockArmorCorner2Tip=0:19,5:6*LargeHeavyBlockArmorInvCorner2Base=0:133,5:45*LargeHeavyBlockArmorInvCorner2Tip=0:94,5:25*SmallBlockArmorSlope2Base=0:1*SmallBlockArmorSlope2Tip=0:1*SmallBlockArmorCorner2Base=0:1*SmallBlockArmorCorner2Tip=0:1*SmallBlockArmorInvCorner2Base=0:1*SmallBlockArmorInvCorner2Tip=0:1*SmallHeavyBlockArmorSlope2Base=0:4,5:1*SmallHeavyBlockArmorSlope2Tip=0:2,5:1*SmallHeavyBlockArmorCorner2Base=0:3,5:1*SmallHeavyBlockArmorCorner2Tip=0:2,5:1*SmallHeavyBlockArmorInvCorner2Base=0:5,5:1*SmallHeavyBlockArmorInvCorner2Tip=0:5,5:1*LargeWindowSquare=9:12,2:8,10:4*LargeWindowEdge=9:16,2:12,10:6*LargeStairs=9:50,2:30*LargeRamp=9:70,2:16*LargeSteelCatwalk=9:27,2:5,10:20*LargeSteelCatwalk2Sides=9:32,2:7,10:25*LargeSteelCatwalkCorner=9:32,2:7,10:25*LargeSteelCatwalkPlate=9:23,2:7,10:17*LargeCoverWall=0:4,2:10*LargeCoverWallHalf=0:2,2:6*LargeBlockInteriorWall=9:25,2:10*LargeInteriorPillar=9:25,2:10,10:4*LargeRailStraight=0:12,2:8,7:4*Window1x2Slope=13:16,12:55*Window1x2Inv=13:15,12:40*Window1x2Face=13:15,12:40*Window1x2SideLeft=13:13,12:26*Window1x2SideLeftInv=13:13,12:26*Window1x2SideRight=13:13,12:26*Window1x2SideRightInv=13:13,12:26*Window1x1Slope=13:12,12:35*Window1x1Face=13:11,12:24*Window1x1Side=13:9,12:17*Window1x1SideInv=13:9,12:17*Window1x1Inv=13:11,12:24*Window1x2Flat=13:15,12:50*Window1x2FlatInv=13:15,12:50*Window1x1Flat=13:10,12:25*Window1x1FlatInv=13:10,12:25*Window3x3Flat=13:40,12:196*Window3x3FlatInv=13:40,12:196*Window2x3Flat=13:25,12:140*Window2x3FlatInv=13:25,12:140*ArmorAlpha=0:8,5:8,2:40,7:4*ArmorCenter=0:140*ArmorCorner=0:120*ArmorInvCorner=0:135*ArmorSide=0:130*SmallArmorCenter=0:5*SmallArmorCorner=0:5*SmallArmorInvCorner=0:5*SmallArmorSide=0:5$BatteryBlock*LargeBlockBatteryBlock=0:80,2:30,3:120,4:25*SmallBlockBatteryBlock=0:25,2:5,3:20,4:2$TerminalBlock*ControlPanel=0:1,2:1,4:1,6:1*SmallControlPanel=0:1,2:1,4:1,6:1$MyProgrammableBlock*SmallProgrammableBlock=0:2,2:2,7:2,8:1,6:1,4:2*LargeProgrammableBlock=0:21,2:4,7:2,8:1,6:1,4:2$LargeGatlingTurret*(null)=0:20,2:30,5:15,7:1,8:8,4:10*SmallGatlingTurret=0:10,2:30,5:5,7:1,8:4,4:10$LargeMissileTurret*(null)=0:20,2:40,5:5,7:6,8:16,4:12*SmallMissileTurret=0:10,2:40,5:2,7:2,8:8,4:12$InteriorTurret*LargeInteriorTurret=9:6,2:20,10:2,7:1,8:2,4:5,0:4$Passage*(null)=9:74,2:20,10:48$Door*(null)=0:8,9:10,2:40,10:4,8:2,6:1,4:2$RadioAntenna*LargeBlockRadioAntenna=0:80,2:40,10:60,7:40,4:8,11:40*SmallBlockRadioAntenna=0:1,10:1,4:1,2:2,11:4$Beacon*LargeBlockBeacon=0:80,2:40,10:60,7:40,4:8,11:40*SmallBlockBeacon=0:2,2:1,10:1,4:1$ReflectorLight*LargeBlockFrontLight=0:8,9:20,2:20,7:2,12:2*SmallBlockFrontLight=0:1,2:1,9:1$InteriorLight*SmallLight=0:1,2:1*SmallBlockSmallLight=0:1,2:1*LargeBlockLight_1corner=2:3*LargeBlockLight_2corner=2:6*SmallBlockLight_1corner=2:2*SmallBlockLight_2corner=2:4$Warhead*LargeWarhead=0:20,13:24,2:12,10:12,4:2,14:6*SmallWarhead=0:4,13:1,2:1,10:2,4:1,14:2$Decoy*LargeDecoy=0:3,2:1,4:2,11:1,7:2*SmallDecoy=0:1,2:1,4:1,11:1,10:2,13:1$LandingGear*LargeBlockLandingGear=0:150,2:10,7:4,8:6*SmallBlockLandingGear=0:2,2:1,7:1,8:1$Projector*LargeProjector=0:21,2:4,7:2,8:1,4:2*SmallProjector=0:2,2:2,7:2,8:1,4:2$Refinery*LargeRefinery=0:1200,2:40,7:20,8:16,4:20*BlastFurnace=0:120,2:5,7:2,8:4,4:5$OxygenGenerator*(null)=0:120,2:5,7:2,8:4,4:5*OxygenGeneratorSmall=0:8,2:8,8:1,7:2,4:3$Assembler*LargeAssembler=0:150,2:40,8:8,6:4,4:80$OreDetector*LargeOreDetector=0:50,2:40,8:5,4:25,15:25*SmallBlockOreDetector=0:3,2:2,8:1,4:1,15:1$MedicalRoom*LargeMedicalRoom=9:240,2:80,5:60,10:20,7:5,6:10,4:10,16:15$GravityGenerator*(null)=0:150,17:6,2:60,7:4,8:6,4:40$GravityGeneratorSphere*(null)=0:150,17:6,2:60,7:4,8:6,4:40$JumpDrive*LargeJumpDrive=0:40,7:40,5:50,17:20,15:20,3:120,1:1000,4:300,2:40$Cockpit*LargeBlockCockpit=9:20,2:20,8:2,4:100,6:10,12:10*LargeBlockCockpitSeat=0:30,2:20,8:1,6:8,4:100,12:60*SmallBlockCockpit=0:10,2:10,8:1,6:5,4:15,12:30*DBSmallBlockFighterCockpit=0:20,2:20,8:1,5:10,9:15,6:4,4:20,12:40*CockpitOpen=9:20,2:20,8:2,4:100,6:4*PassengerSeatLarge=9:20,2:20*PassengerSeatSmall=9:20,2:20$CryoChamber*LargeBlockCryoChamber=9:40,2:20,8:8,6:8,4:30,12:10$SmallMissileLauncher*(null)=0:4,2:2,5:1,7:4,8:1,4:1*LargeMissileLauncher=0:35,2:8,5:30,7:25,8:6,4:4$SmallMissileLauncherReload*SmallRocketLauncherReload=0:8,10:50,9:50,2:24,7:8,5:10,8:4,4:2$SmallGatlingGun*(null)=0:4,2:1,5:2,10:3,8:1,4:1$Drill*SmallBlockDrill=0:32,2:30,7:4,8:1,4:1*LargeBlockDrill=0:300,2:40,10:24,7:12,8:5,4:5$SensorBlock*SmallBlockSensor=9:5,2:5,4:6,11:4,15:6,0:2*LargeBlockSensor=9:5,2:5,4:6,11:4,15:6,0:2$SoundBlock*SmallBlockSoundBlock=9:4,2:6,4:3*LargeBlockSoundBlock=9:15,2:10,4:15$TextPanel*SmallTextPanel=9:1,2:4,4:4,6:3*SmallLCDPanelWide=9:1,2:8,4:8,6:6,12:2*SmallLCDPanel=9:1,2:4,4:4,6:3*LargeBlockCorner_LCD_1=2:5,4:3,6:1*LargeBlockCorner_LCD_2=2:5,4:3,6:1*LargeBlockCorner_LCD_Flat_1=2:5,4:3,6:1*LargeBlockCorner_LCD_Flat_2=2:5,4:3,6:1*SmallBlockCorner_LCD_1=2:3,4:2,6:1*SmallBlockCorner_LCD_2=2:3,4:2,6:1*SmallBlockCorner_LCD_Flat_1=2:3,4:2,6:1*SmallBlockCorner_LCD_Flat_2=2:3,4:2,6:1*LargeTextPanel=9:1,2:6,4:6,6:10*LargeLCDPanel=9:1,2:6,4:6,6:10,12:6*LargeLCDPanelWide=9:2,2:12,4:12,6:20,12:12$OxygenTank*OxygenTankSmall=0:14,2:10,10:10,7:2,4:3*(null)=0:80,7:40,10:60,4:8,2:40*LargeHydrogenTank=0:280,7:80,10:60,4:8,2:40*SmallHydrogenTank=0:80,7:40,10:60,4:8,2:40$RemoteControl*LargeBlockRemoteControl=9:10,2:10,8:1,4:15*SmallBlockRemoteControl=9:2,2:1,8:1,4:1$AirVent*(null)=0:80,2:30,5:5,4:5*SmallAirVent=0:8,2:10,5:2,4:5$UpgradeModule*LargeProductivityModule=0:100,2:40,10:20,7:10,8:2*LargeEffectivenessModule=0:100,2:50,10:15,5:10,8:5*LargeEnergyModule=0:100,2:40,10:20,7:10,8:2$CargoContainer*SmallBlockSmallContainer=9:3,2:1,4:1,8:1,6:1*SmallBlockMediumContainer=9:30,2:10,4:4,8:4,6:1*SmallBlockLargeContainer=9:75,2:25,4:6,8:8,6:1*LargeBlockSmallContainer=9:40,2:40,5:4,10:20,8:4,6:1,4:2*LargeBlockLargeContainer=9:360,2:80,5:24,10:60,8:20,6:1,4:8$Thrust*SmallBlockSmallThrust=0:2,7:1,18:1,2:1*SmallBlockLargeThrust=0:5,2:2,7:5,18:12*LargeBlockSmallThrust=0:25,2:60,7:8,18:80*LargeBlockLargeThrust=0:150,2:100,7:40,18:960*LargeBlockLargeHydrogenThrust=0:150,2:180,5:250,7:40*LargeBlockSmallHydrogenThrust=0:25,2:60,5:40,7:8*SmallBlockLargeHydrogenThrust=0:30,2:30,5:22,7:10*SmallBlockSmallHydrogenThrust=0:7,2:15,5:4,7:2*LargeBlockLargeAtmosphericThrust=0:230,2:60,7:50,5:40,8:1136*LargeBlockSmallAtmosphericThrust=0:35,2:50,7:8,5:10,8:113*SmallBlockLargeAtmosphericThrust=0:20,2:30,7:4,5:8,8:144*SmallBlockSmallAtmosphericThrust=0:3,7:1,5:1,8:18,2:2$CameraBlock*SmallCameraBlock=0:2,4:3*LargeCameraBlock=0:2,4:3$Gyro*LargeBlockGyro=0:600,2:40,7:4,5:50,8:4,4:5*SmallBlockGyro=0:25,2:5,7:1,8:2,4:3$Reactor*SmallBlockSmallGenerator=0:3,2:10,5:2,7:1,19:3,8:1,4:10*SmallBlockLargeGenerator=0:60,2:9,5:9,7:3,19:95,8:5,4:25*LargeBlockSmallGenerator=0:80,2:40,5:4,7:8,19:100,8:6,4:25*LargeBlockLargeGenerator=0:1000,2:70,5:40,7:40,1:100,19:2000,8:20,4:75$PistonBase*LargePistonBase=0:15,2:10,7:4,8:4,4:2*SmallPistonBase=0:4,2:4,10:4,8:2,4:1$ExtendedPistonBase*LargePistonBase=0:15,2:10,7:4,8:4,4:2*SmallPistonBase=0:4,2:4,10:4,8:2,4:1$PistonTop*LargePistonTop=0:10,7:8*SmallPistonTop=0:4,7:2$MotorStator*LargeStator=0:15,2:10,7:4,8:4,4:2*SmallStator=0:5,2:5,10:1,8:1,4:1$MotorSuspension*Suspension3x3=0:25,2:15,7:6,10:12,8:6*Suspension5x5=0:70,2:40,7:20,10:30,8:20*Suspension1x1=0:25,2:15,7:6,10:12,8:6*SmallSuspension3x3=0:8,2:7,10:2,8:1*SmallSuspension5x5=0:16,2:12,10:4,8:2*SmallSuspension1x1=0:8,2:7,10:2,8:1*Suspension3x3mirrored=0:25,2:15,7:6,10:12,8:6*Suspension5x5mirrored=0:70,2:40,7:20,10:30,8:20*Suspension1x1mirrored=0:25,2:15,7:6,10:12,8:6*SmallSuspension3x3mirrored=0:8,2:7,10:2,8:1*SmallSuspension5x5mirrored=0:16,2:12,10:4,8:2*SmallSuspension1x1mirrored=0:8,2:7,10:2,8:1$MotorRotor*LargeRotor=0:30,7:24*SmallRotor=0:12,10:6$MotorAdvancedStator*LargeAdvancedStator=0:15,2:10,7:4,8:4,4:2*SmallAdvancedStator=0:5,2:5,10:1,8:1,4:1$MotorAdvancedRotor*LargeAdvancedRotor=0:5,7:4*SmallAdvancedRotor=0:1,10:1$ButtonPanel*ButtonPanelLarge=9:10,2:20,4:20*ButtonPanelSmall=0:2,4:1,9:1$TimerBlock*TimerBlockLarge=9:6,2:30,4:5*TimerBlockSmall=9:2,2:3,4:1$SolarPanel*LargeBlockSolarPanel=0:4,2:10,7:1,4:2,20:64*SmallBlockSolarPanel=0:2,2:2,13:4,4:1,20:16,12:1$OxygenFarm*LargeBlockOxygenFarm=0:40,12:100,7:20,5:10,2:20,4:20$Conveyor*SmallBlockConveyor=9:4,2:4,8:1*LargeBlockConveyor=9:20,2:30,10:20,8:6*SmallShipConveyorHub=9:25,2:70,10:25,8:2$Collector*Collector=0:45,2:50,10:12,8:8,6:4,4:10*CollectorSmall=0:35,2:35,10:12,8:8,6:2,4:8$ShipConnector*Connector=0:150,2:40,10:12,8:8,4:20*ConnectorSmall=0:7,2:4,10:2,8:1,4:4*ConnectorMedium=0:21,2:12,10:6,8:6,4:6$ConveyorConnector*ConveyorTube=9:14,2:20,10:12,8:6*ConveyorTubeSmall=9:1,2:1,8:1*ConveyorTubeMedium=9:8,2:20,10:10,8:6*ConveyorFrameMedium=9:4,2:12,10:5,8:2*ConveyorTubeCurved=9:14,2:20,10:12,8:6*ConveyorTubeSmallCurved=9:1,8:1,2:1*ConveyorTubeCurvedMedium=9:7,2:20,10:10,8:6$ConveyorSorter*LargeBlockConveyorSorter=9:50,2:120,10:50,4:20,8:2*MediumBlockConveyorSorter=9:5,2:12,10:5,4:5,8:2*SmallBlockConveyorSorter=9:5,2:12,10:5,4:5,8:2$VirtualMass*VirtualMassLarge=0:90,1:20,2:30,4:20,17:9*VirtualMassSmall=0:3,1:2,2:2,4:2,17:1$SpaceBall*SpaceBallLarge=0:225,2:30,4:20,17:3*SpaceBallSmall=0:70,2:10,4:7,17:1$Wheel*SmallRealWheel1x1=0:2,2:5,7:1*SmallRealWheel=0:5,2:10,7:1*SmallRealWheel5x5=0:7,2:15,7:2*RealWheel1x1=0:8,2:20,7:4*RealWheel=0:12,2:25,7:6*RealWheel5x5=0:16,2:30,7:8*SmallRealWheel1x1mirrored=0:2,2:5,7:1*SmallRealWheelmirrored=0:5,2:10,7:1*SmallRealWheel5x5mirrored=0:7,2:15,7:2*RealWheel1x1mirrored=0:8,2:20,7:4*RealWheelmirrored=0:12,2:25,7:6*RealWheel5x5mirrored=0:16,2:30,7:8*Wheel1x1=0:8,2:20,7:4*SmallWheel1x1=0:2,2:5,7:1*Wheel3x3=0:12,2:25,7:6*SmallWheel3x3=0:5,2:10,7:1*Wheel5x5=0:16,2:30,7:8*SmallWheel5x5=0:7,2:15,7:2$ShipGrinder*LargeShipGrinder=0:20,2:30,7:1,8:4,4:2*SmallShipGrinder=0:12,2:17,10:4,7:1,8:4,4:2$ShipWelder*LargeShipWelder=0:20,2:30,7:1,8:2,4:2*SmallShipWelder=0:12,2:17,10:6,7:1,8:2,4:2$MergeBlock*LargeShipMergeBlock=0:12,2:17,8:2,10:6,7:1,4:2*SmallShipMergeBlock=0:4,2:5,8:1,10:2,4:1$LaserAntenna*LargeBlockLaserAntenna=0:50,2:40,8:16,15:30,11:20,1:100,4:50,12:4*SmallBlockLaserAntenna=0:10,10:10,2:10,8:5,11:5,1:10,4:30,12:2$AirtightHangarDoor*(null)=0:350,2:40,10:40,8:16,4:2$AirtightSlideDoor*LargeBlockSlideDoor=0:20,2:40,10:4,8:4,6:1,4:2,12:15$Parachute*LgParachute=0:9,2:25,10:5,8:3,4:2*SmParachute=0:2,2:2,10:1,8:1,4:1$DebugSphere1*DebugSphereLarge=0:10,4:20$DebugSphere2*DebugSphereLarge=0:10,4:20$DebugSphere3*DebugSphereLarge=0:10,4:20";
        // the line above this one is really long
    }
}