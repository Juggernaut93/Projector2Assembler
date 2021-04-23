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
        private readonly bool assemblersFromSubgrids = false; // consider assemblers on subgrids (if no assembler group is specified)
        private readonly bool autoResizeText = true; // makes the text fit inside the LCD. If true, it forces the font to be Monospace
        private readonly bool fitOn2IfPossible = true; // when true, if no valid third LCD is specified, the script will fit ingots and ores on the second LCD
        private readonly bool alwaysShowAmmos = true, alwaysShowTools = false; // show ammos/tools even when no assembler are producing them (beware the screen clutter - Datapads are considered tools)
        private readonly bool showAllIngotsOres = true; // show all ingots/ores, even if they are not used to build any components shown on the first LCD (scrap will still be ignored if not in inventory)
        private readonly bool onlyEnabledAssemblers = false; // if true, only enabled assemblers will be considered (if no assembler group is specified)
        /**********************************************/
        /************ END OF CONFIGURATION ************/
        /**********************************************/

        /**********************************************/
        /************ LOCALIZATION STRINGS ************/
        /**********************************************/
        private const string titleGroup = "Assembler group: {0} ({1} assemblers)";
        private const string titleGroupSmallLCD = "{0} ({1} assemblers)";
        private const string titleAuto = "{0} assemblers on grid";
        private const string lcd1Title = "Components: available | in production";
        private const string lcd2Title = "Ingots: available | needed | missing";
        private const string lcd3Title = "Ores: available | needed | missing";
        private const string monospaceFontName = "Monospace";
        private const string effectivenessString = "Effectiveness:"; // the text shown in terminal which says the current effectiveness (= yield bonus) of the selected refinery
        private const string refineryMessage = "Math done with ~{0:F2}% refinery effectiveness\n({1}{2} ports with yield modules) ({3})";
        private const string refineryMessageCauseUser = "user input";
        private const string refineryMessageCauseAvg = "grid average";
        private const string scrapMetalMessage = "{0} {1} can be used to save {2} {3}";
        private const string thousands = "k", millions = "M", billions = "G";
        private const string noAssembler = "No assemblers found";
        private const string basicRefineryEffUsed = "^Basic refinery conversion rate";
        private const string noRefineryFound = " (no refinery found)";
        private const string betterYield = " (better yield)";
        private readonly Dictionary<string, string> componentTranslation = new Dictionary<string, string>()
        {
            // components
            ["BulletproofGlass"] = "Bulletproof Glass",
            ["Canvas"] = "Canvas",
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
            ["MotorComponent"] = "Motor",
            ["PowerCell"] = "Power Cell",
            ["RadioCommunicationComponent"] = "Radio-communication Components",
            ["ReactorComponent"] = "Reactor Components",
            ["SmallTube"] = "Small Steel Tube",
            ["SolarCell"] = "Solar Cell",
            ["SteelPlate"] = "Steel Plate",
            ["Superconductor"] = "Superconductor Conduits",
            ["ThrustComponent"] = "Thruster Components",
            ["ZoneChip"] = "Zone Chip",
            // datapad
            ["Datapad"] = "Datapad",
            // ammos
            //["NATO_5p56x45mmMagazine"] = "5.56x45mm NATO Magazine",
            ["NATO_25x184mmMagazine"] = "25x184mm NATO ammo container",
            ["Missile200mm"] = "200mm missile container",
            ["SemiAutoPistolMagazine"] = "S-10 Magazine",
            ["FullAutoPistolMagazine"] = "S-20A Magazine",
            ["ElitePistolMagazine"] = "S-10E Magazine",
            ["AutomaticRifleGun_Mag_20rd"] = "MR-20 Magazine",
            ["RapidFireAutomaticRifleGun_Mag_50rd"] = "MR-50A Magazine",
            ["PreciseAutomaticRifleGun_Mag_5rd"] = "MR-8P Magazine",
            ["UltimateAutomaticRifleGun_Mag_30rd"] = "MR-30E Magazine",
            // tools
            ["OxygenBottle"] = "Oxygen Bottle",
            ["HydrogenBottle"] = "Hydrogen Bottle",
            ["AutomaticRifle"] = "MR-20",
            ["RapidFireAutomaticRifle"] = "MR-50A",
            ["PreciseAutomaticRifle"] = "MR-8P",
            ["UltimateAutomaticRifle"] = "MR-30E",
            ["Welder"] = "Welder",
            ["Welder2"] = "Enhanced Welder",
            ["Welder3"] = "Proficient Welder",
            ["Welder4"] = "Elite Welder",
            ["AngleGrinder"] = "Grinder",
            ["AngleGrinder2"] = "Enhanced Grinder",
            ["AngleGrinder3"] = "Proficient Grinder",
            ["AngleGrinder4"] = "Elite Grinder",
            ["HandDrill"] = "Hand Drill",
            ["HandDrill2"] = "Enhanced Hand Drill",
            ["HandDrill3"] = "Proficient Hand Drill",
            ["HandDrill4"] = "Elite Hand Drill",
            ["BasicHandHeldLauncher"] = "RO-1",
            ["AdvancedHandHeldLauncher"] = "PRO-1",
            ["SemiAutoPistol"] = "S-10",
            ["FullAutoPistol"] = "S-20A",
            ["EliteAutoPistol"] = "S-10E",
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
            [Ores.Ice] = "Ice",
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
            // components
            ["BulletproofGlass"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Silicon] = 15 },
            ["Canvas"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 2, [Ingots.Silicon] = 35 },
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
            ["Datapad"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 1, [Ingots.Silicon] = 5, [Ingots.Stone] = 1 },
            ["ZoneChip"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { }, // cannot be assembled
            // ammos
            //["NATO_5p56x45mmMagazine"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = FP("0.8"), [Ingots.Nickel] = FP("0.2"), [Ingots.Magnesium] = FP("0.15") },
            ["NATO_25x184mmMagazine"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 40, [Ingots.Nickel] = 5, [Ingots.Magnesium] = 3 },
            ["SemiAutoPistolMagazine"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = FP("0.25"), [Ingots.Nickel] = FP("0.05"), [Ingots.Magnesium] = FP("0.05") },
            ["FullAutoPistolMagazine"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = FP("0.5"), [Ingots.Nickel] = FP("0.1"), [Ingots.Magnesium] = FP("0.1") },
            ["ElitePistolMagazine"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = FP("0.3"), [Ingots.Nickel] = FP("0.1"), [Ingots.Magnesium] = FP("0.1") },
            ["AutomaticRifleGun_Mag_20rd"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = FP("0.8"), [Ingots.Nickel] = FP("0.2"), [Ingots.Magnesium] = FP("0.15") },
            ["RapidFireAutomaticRifleGun_Mag_50rd"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 2, [Ingots.Nickel] = FP("0.5"), [Ingots.Magnesium] = FP("0.4") },
            ["PreciseAutomaticRifleGun_Mag_5rd"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = FP("0.8"), [Ingots.Nickel] = FP("0.2"), [Ingots.Magnesium] = FP("0.15") },
            ["UltimateAutomaticRifleGun_Mag_30rd"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = FP("1.2"), [Ingots.Nickel] = FP("0.4"), [Ingots.Magnesium] = FP("0.25") },
            ["Missile200mm"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 55, [Ingots.Nickel] = 7, [Ingots.Magnesium] = FP("1.2"), [Ingots.Silicon] = FP("0.2"), [Ingots.Uranium] = FP("0.1"), [Ingots.Platinum] = FP("0.04") },
            // tools
            ["OxygenBottle"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 80, [Ingots.Nickel] = 30, [Ingots.Silicon] = 10 },
            ["HydrogenBottle"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 80, [Ingots.Nickel] = 30, [Ingots.Silicon] = 10 },
            ["AutomaticRifle"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 3, [Ingots.Nickel] = 1 },
            ["RapidFireAutomaticRifle"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 3, [Ingots.Nickel] = 8 },
            ["PreciseAutomaticRifle"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 3, [Ingots.Nickel] = 1, [Ingots.Cobalt] = 5 },
            ["UltimateAutomaticRifle"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 3, [Ingots.Nickel] = 1, [Ingots.Platinum] = 4, [Ingots.Silver] = 6 },
            ["Welder"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 5, [Ingots.Nickel] = 1, [Ingots.Stone] = 3 },
            ["Welder2"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 5, [Ingots.Nickel] = 1, [Ingots.Cobalt] = FP("0.2"), [Ingots.Silicon] = 2 },
            ["Welder3"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 5, [Ingots.Nickel] = 1, [Ingots.Cobalt] = FP("0.2"), [Ingots.Silver] = 2 },
            ["Welder4"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 5, [Ingots.Nickel] = 1, [Ingots.Cobalt] = FP("0.2"), [Ingots.Platinum] = 2 },
            ["AngleGrinder"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 3, [Ingots.Nickel] = 1, [Ingots.Stone] = 5, [Ingots.Silicon] = 1 },
            ["AngleGrinder2"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 3, [Ingots.Nickel] = 1, [Ingots.Cobalt] = 2, [Ingots.Silicon] = 6 },
            ["AngleGrinder3"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 3, [Ingots.Nickel] = 1, [Ingots.Cobalt] = 1, [Ingots.Silicon] = 2, [Ingots.Silver] = 2 },
            ["AngleGrinder4"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 3, [Ingots.Nickel] = 1, [Ingots.Cobalt] = 1, [Ingots.Silicon] = 2, [Ingots.Platinum] = 2 },
            ["HandDrill"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 20, [Ingots.Nickel] = 3, [Ingots.Silicon] = 3 },
            ["HandDrill2"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 20, [Ingots.Nickel] = 3, [Ingots.Silicon] = 5 },
            ["HandDrill3"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 20, [Ingots.Nickel] = 3, [Ingots.Silicon] = 3, [Ingots.Silver] = 2 },
            ["HandDrill4"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 20, [Ingots.Nickel] = 3, [Ingots.Silicon] = 3, [Ingots.Platinum] = 2 },
            ["BasicHandHeldLauncher"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 30, [Ingots.Nickel] = 10, [Ingots.Cobalt] = 5 },
            ["AdvancedHandHeldLauncher"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 30, [Ingots.Nickel] = 10, [Ingots.Cobalt] = 5, [Ingots.Platinum] = 5 },
            ["SemiAutoPistol"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 1, [Ingots.Nickel] = FP("0.3") },
            ["FullAutoPistol"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = FP("1.5"), [Ingots.Nickel] = FP("0.5") },
            ["EliteAutoPistol"] = new Dictionary<Ingots, VRage.MyFixedPoint>() { [Ingots.Iron] = 1, [Ingots.Nickel] = FP("0.4"), [Ingots.Platinum] = FP("0.5"), [Ingots.Silver] = 1 }
        };

        private readonly Dictionary<string, string> blueprintDefSubtypeToItemSubtype = new Dictionary<string, string>()
        {
            ["OxygenBottle"] = "OxygenBottle", //OxygenContainerObject
            ["HydrogenBottle"] = "HydrogenBottle", //GasContainerObject
            ["ConstructionComponent"] = "Construction", //Component
            ["GirderComponent"] = "Girder", //Component
            ["MetalGrid"] = "MetalGrid", //Component
            ["InteriorPlate"] = "InteriorPlate", //Component
            ["SteelPlate"] = "SteelPlate", //Component
            ["SmallTube"] = "SmallTube", //Component
            ["LargeTube"] = "LargeTube", //Component
            ["MotorComponent"] = "Motor", //Component
            ["Display"] = "Display", //Component
            ["BulletproofGlass"] = "BulletproofGlass", //Component
            ["ComputerComponent"] = "Computer", //Component
            ["ReactorComponent"] = "Reactor", //Component
            ["ThrustComponent"] = "Thrust", //Component
            ["GravityGeneratorComponent"] = "GravityGenerator", //Component
            ["MedicalComponent"] = "Medical", //Component
            ["RadioCommunicationComponent"] = "RadioCommunication", //Component
            ["DetectorComponent"] = "Detector", //Component
            ["Canvas"] = "Canvas", //Component
            ["ExplosivesComponent"] = "Explosives", //Component
            ["SolarCell"] = "SolarCell", //Component
            ["PowerCell"] = "PowerCell", //Component
            ["ZoneChip"] = "ZoneChip", //Component
            ["Datapad"] = "Datapad", //Datapad
            ["AutomaticRifle"] = "AutomaticRifleItem", //PhysicalGunObject
            ["RapidFireAutomaticRifle"] = "RapidFireAutomaticRifleItem", //PhysicalGunObject
            ["PreciseAutomaticRifle"] = "PreciseAutomaticRifleItem", //PhysicalGunObject
            ["UltimateAutomaticRifle"] = "UltimateAutomaticRifleItem", //PhysicalGunObject
            ["BasicHandHeldLauncher"] = "BasicHandHeldLauncherItem", //PhysicalGunObject
            ["AdvancedHandHeldLauncher"] = "AdvancedHandHeldLauncherItem", //PhysicalGunObject
            ["SemiAutoPistol"] = "SemiAutoPistolItem", //PhysicalGunObject
            ["FullAutoPistol"] = "FullAutoPistolItem", //PhysicalGunObject
            ["EliteAutoPistol"] = "ElitePistolItem", //PhysicalGunObject
            ["Welder"] = "WelderItem", //PhysicalGunObject
            ["Welder2"] = "Welder2Item", //PhysicalGunObject
            ["Welder3"] = "Welder3Item", //PhysicalGunObject
            ["Welder4"] = "Welder4Item", //PhysicalGunObject
            ["AngleGrinder"] = "AngleGrinderItem", //PhysicalGunObject
            ["AngleGrinder2"] = "AngleGrinder2Item", //PhysicalGunObject
            ["AngleGrinder3"] = "AngleGrinder3Item", //PhysicalGunObject
            ["AngleGrinder4"] = "AngleGrinder4Item", //PhysicalGunObject
            ["HandDrill"] = "HandDrillItem", //PhysicalGunObject
            ["HandDrill2"] = "HandDrill2Item", //PhysicalGunObject
            ["HandDrill3"] = "HandDrill3Item", //PhysicalGunObject
            ["HandDrill4"] = "HandDrill4Item", //PhysicalGunObject
            //["NATO_5p56x45mmMagazine"] = "NATO_5p56x45mm", //AmmoMagazine
            ["NATO_25x184mmMagazine"] = "NATO_25x184mm", //AmmoMagazine
            ["Missile200mm"] = "Missile200mm", //AmmoMagazine
            ["SemiAutoPistolMagazine"] = "SemiAutoPistolMagazine", //AmmoMagazine
            ["FullAutoPistolMagazine"] = "FullAutoPistolMagazine", //AmmoMagazine
            ["ElitePistolMagazine"] = "ElitePistolMagazine", //AmmoMagazine
            ["AutomaticRifleGun_Mag_20rd"] = "AutomaticRifleGun_Mag_20rd", //AmmoMagazine
            ["RapidFireAutomaticRifleGun_Mag_50rd"] = "RapidFireAutomaticRifleGun_Mag_50rd", //AmmoMagazine
            ["PreciseAutomaticRifleGun_Mag_5rd"] = "PreciseAutomaticRifleGun_Mag_5rd", //AmmoMagazine
            ["UltimateAutomaticRifleGun_Mag_30rd"] = "UltimateAutomaticRifleGun_Mag_30rd", //AmmoMagazine
            ["Superconductor"] = "Superconductor", //Component
        };

        private readonly Dictionary<string, string> getProductType = new Dictionary<string, string>()
        {
            ["OxygenBottle"] = "OxygenContainerObject",
            ["HydrogenBottle"] = "GasContainerObject",
            ["ConstructionComponent"] = "Component",
            ["GirderComponent"] = "Component",
            ["MetalGrid"] = "Component",
            ["InteriorPlate"] = "Component",
            ["SteelPlate"] = "Component",
            ["SmallTube"] = "Component",
            ["LargeTube"] = "Component",
            ["MotorComponent"] = "Component",
            ["Display"] = "Component",
            ["BulletproofGlass"] = "Component",
            ["ComputerComponent"] = "Component",
            ["ReactorComponent"] = "Component",
            ["ThrustComponent"] = "Component",
            ["GravityGeneratorComponent"] = "Component",
            ["MedicalComponent"] = "Component",
            ["RadioCommunicationComponent"] = "Component",
            ["DetectorComponent"] = "Component",
            ["Canvas"] = "Component",
            ["ExplosivesComponent"] = "Component",
            ["SolarCell"] = "Component",
            ["PowerCell"] = "Component",
            ["ZoneChip"] = "Component",
            ["Datapad"] = "Datapad",
            ["AutomaticRifle"] = "PhysicalGunObject",
            ["RapidFireAutomaticRifle"] = "PhysicalGunObject",
            ["PreciseAutomaticRifle"] = "PhysicalGunObject",
            ["UltimateAutomaticRifle"] = "PhysicalGunObject",
            ["BasicHandHeldLauncher"] = "PhysicalGunObject",
            ["AdvancedHandHeldLauncher"] = "PhysicalGunObject",
            ["SemiAutoPistol"] = "PhysicalGunObject",
            ["FullAutoPistol"] = "PhysicalGunObject",
            ["EliteAutoPistol"] = "PhysicalGunObject",
            ["Welder"] = "PhysicalGunObject",
            ["Welder2"] = "PhysicalGunObject",
            ["Welder3"] = "PhysicalGunObject",
            ["Welder4"] = "PhysicalGunObject",
            ["AngleGrinder"] = "PhysicalGunObject",
            ["AngleGrinder2"] = "PhysicalGunObject",
            ["AngleGrinder3"] = "PhysicalGunObject",
            ["AngleGrinder4"] = "PhysicalGunObject",
            ["HandDrill"] = "PhysicalGunObject",
            ["HandDrill2"] = "PhysicalGunObject",
            ["HandDrill3"] = "PhysicalGunObject",
            ["HandDrill4"] = "PhysicalGunObject",
            //["NATO_5p56x45mmMagazine"] = "AmmoMagazine",
            ["NATO_25x184mmMagazine"] = "AmmoMagazine",
            ["Missile200mm"] = "AmmoMagazine",
            ["SemiAutoPistolMagazine"] = "AmmoMagazine",
            ["FullAutoPistolMagazine"] = "AmmoMagazine",
            ["ElitePistolMagazine"] = "AmmoMagazine",
            ["AutomaticRifleGun_Mag_20rd"] = "AmmoMagazine",
            ["RapidFireAutomaticRifleGun_Mag_50rd"] = "AmmoMagazine",
            ["PreciseAutomaticRifleGun_Mag_5rd"] = "AmmoMagazine",
            ["UltimateAutomaticRifleGun_Mag_30rd"] = "AmmoMagazine",
            ["Superconductor"] = "Component",
        };

        private readonly string[] componentPrefixes = new string[] {
            "Component",
            "AmmoMagazine",
            "PhysicalGunObject",
            "OxygenContainerObject",
            "GasContainerObject",
            "Datapad"
        };

        private readonly string[] toolsPrefixes = new string[] {
            "PhysicalGunObject",
            "OxygenContainerObject",
            "GasContainerObject",
            "Datapad"
        };

        private readonly Ores[] basicRefineryOres = new Ores[] { Ores.Iron, Ores.Nickel, Ores.Cobalt, Ores.Silicon, Ores.Magnesium, Ores.Stone, Ores.Scrap };

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
            // account for possible '^' character for ores that can be refined in an Basic Refinery
            foreach (var ore in basicRefineryOres)
            {
                if (oreTranslation[ore].Length + 1 > maxOreLength)
                    maxOreLength = oreTranslation[ore].Length + 1;
            }
            if (oreTranslation[Ores.Scrap].Length == maxOreLength)
            {
                maxOreLength++; //Scrap Metal needs 1 more character (asterisk) at the end
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
                    assemblerGroupName = props[0];
                    lcdName1 = props[1];
                    lcdName2 = props[2];
                    lcdName3 = props[3];
                    Runtime.UpdateFrequency = (UpdateFrequency)Enum.Parse(typeof(UpdateFrequency), props[4]);
                    effectivenessMultiplier = double.Parse(props[5]);
                    averageEffectivenesses = bool.Parse(props[6]);
                }
                catch (Exception)
                {
                    Echo("Error while trying to restore previous configuration. Script needs to be restarted.");
                    assemblerGroupName = lcdName1 = lcdName2 = lcdName3 = "";
                    Runtime.UpdateFrequency = UpdateFrequency.None;
                    effectivenessMultiplier = 1;
                    averageEffectivenesses = true;
                    return;
                }
            }
        }

        private void SaveProperty(string s)
        {
            Storage += s + ";";
        }

        public void Save()
        {
            Storage = "";
            SaveProperty(assemblerGroupName);
            SaveProperty(lcdName1);
            SaveProperty(lcdName2);
            SaveProperty(lcdName3);
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

        private void AddCountToDict<T>(Dictionary<T, int> dic, T key, VRage.MyFixedPoint amount)
        {
            if (dic.ContainsKey(key))
            {
                dic[key] += amount.ToIntSafe();
            }
            else
            {
                dic[key] = amount.ToIntSafe();
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

        private List<KeyValuePair<string, int>> GetProductionComponents(List<IMyAssembler> assemblers)
        {
            Dictionary<string, int> totalComponents = new Dictionary<string, int>();

            // we first initialize the dictionary to have ALL components, so we show on screen all components
            // now considering alwaysShowAmmos and alwaysShowTools
            foreach (var x in getProductType)
            {
                if (x.Value == "Component" || (x.Value == "AmmoMagazine" && alwaysShowAmmos) || (toolsPrefixes.Contains(x.Value) && alwaysShowTools))
                    totalComponents["MyObjectBuilder_BlueprintDefinition/" + x.Key] = 0;
            }

            foreach (var assembler in assemblers)
            {
                List<MyProductionItem> items = new List<MyProductionItem>();
                assembler.GetQueue(items);
                foreach (var i in items)
                {
                    string key = i.BlueprintId.ToString();
                    if (getProductType.ContainsKey(StripDef(key)))
                        if (assembler.Mode == MyAssemblerMode.Assembly)
                            AddCountToDict(totalComponents, key, i.Amount);
                        else
                            AddCountToDict(totalComponents, key, -i.Amount); // disassembling = reclaiming resources
                }
            }

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

            if (amt.ToString().Length > maxWidth - 1 && Math.Abs((float)amt) >= 1000)
            {
                multiplier = thousands;
                amt = amt * (1 / 1000f);
                wholeDigits = GetWholeDigits(amt);

                if (amt.ToString().Length > maxWidth - 1 && Math.Abs((float)amt) >= 1000)
                {
                    multiplier = millions;
                    amt = amt * (1 / 1000f);
                    wholeDigits = GetWholeDigits(amt);

                    if (amt.ToString().Length > maxWidth - 1 && Math.Abs((float)amt) >= 1000)
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

            if (showAllIngotsOres)
            {
                foreach (Ingots ing in Enum.GetValues(typeof(Ingots)))
                    ingotsNeeded[ing] = 0;
            }

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
                if (showAllIngotsOres)
                {
                    oresNeeded[ore] = 0;
                }
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
                var arcConvRate = Math.Min(1f, 0.7f * (float)conversionRates[ore]); // Basic refinery has no yield ports and 0.7 material efficiency multiplier
                // if there are both refineries and basic refineries, or there is neither, we prefer the best yield
                // or we prefer basic refinery rate when there is one but no refinery
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
        private string assemblerGroupName = "", lcdName1 = "", lcdName2 = "", lcdName3 = "";
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
                    assemblerGroupName = spl[0];
                    if (spl.Length > 1)
                        lcdName1 = spl[1];
                    if (spl.Length > 2)
                        lcdName2 = spl[2];
                    if (spl.Length > 3)
                        lcdName3 = spl[3];
                    if (spl.Length > 4 && spl[4] != "")
                    {
                        effectivenessMultiplier = Math.Pow(2, int.Parse(spl[4]) / 8d); // 2^(n/8) - n=0 => 100% - n=8 => 200%
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
                    Echo("Wrong argument(s). Format: [AssemblerGroupName];[LCDName1];[LCDName2];[LCDName3];[yieldPorts]. See Readme for more info.");
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

            LCDType type1, type2, type3;
            // function already checks if null on the inside and returns OTHER in that case
            if ((type1 = CheckLCD(lcd1)) == LCDType.OTHER)
                lcd1 = null;
            if ((type2 = CheckLCD(lcd2)) == LCDType.OTHER)
                lcd2 = null;
            if ((type3 = CheckLCD(lcd3)) == LCDType.OTHER)
                lcd3 = null;

            // if no errors in arguments, then we can keep the script updating
            Runtime.UpdateFrequency = UpdateFrequency.Update100;

            bool fromGroup = true;

            var assemblersGroup = GridTerminalSystem.GetBlockGroupWithName(assemblerGroupName);
            List<IMyAssembler> assemblers = new List<IMyAssembler>();
            if (assemblersGroup != null)
            {
                assemblersGroup.GetBlocksOfType<IMyAssembler>(assemblers);
            }
            else
            {
                GridTerminalSystem.GetBlocksOfType<IMyAssembler>(assemblers, block => (block.CubeGrid == Me.CubeGrid || assemblersFromSubgrids) && (block.Enabled || !onlyEnabledAssemblers));
                fromGroup = false;
            }
            if (assemblers.Count == 0)
            {
                Echo(noAssembler + ".");
                WriteToAll(noAssembler);
                return;
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
                            var type = item.Type.TypeId;
                            if (componentPrefixes.Contains(type.Replace("MyObjectBuilder_", "")))
                            {
                                AddCountToDict(componentAmounts, item.Type.SubtypeId, item.Amount);
                            }
                            else if (type.Equals("MyObjectBuilder_Ingot"))
                            {
                                try
                                {
                                    AddCountToDict(ingotAmounts, (Ingots)Enum.Parse(typeof(Ingots), item.Type.SubtypeId), item.Amount);
                                }
                                catch (ArgumentException)
                                {
                                    moddedIngotsOres = true;
                                }
                            }
                            else if (type.Equals("MyObjectBuilder_Ore"))
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

            string smallTitle, wideTitle;
            if (fromGroup)
            {
                smallTitle = string.Format(titleGroupSmallLCD, assemblerGroupName, assemblers.Count);
                wideTitle = string.Format(titleGroup, assemblerGroupName, assemblers.Count);
            }
            else
            {
                smallTitle = wideTitle = string.Format(titleAuto, assemblers.Count);
            }


            var compList = GetProductionComponents(assemblers);


            var ingotsList = GetTotalIngots(compList);
            List<KeyValuePair<Ingots, VRage.MyFixedPoint>> missingIngots = new List<KeyValuePair<Ingots, VRage.MyFixedPoint>>();
            string output = (type2 == LCDType.WIDE ? wideTitle : smallTitle) + "\n" + lcd2Title.ToUpper() + "\n\n";
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
                    missingStr = missing > 0 ? FormatNumber(missing, ingotWidth, ingotDecimals) : new string(' ', ingotWidth);
                    warnStr = ">> ";
                    okStr = "   ";
                }

                output += String.Format("{0}{1} {2}{3}{4}{3}{5}\n", (missing > 0 ? warnStr : okStr), ingotName, amountStr, separator, neededStr, missingStr);
            }
            if (lcd2 != null)
            {
                ShowAndSetFontSize(lcd2, output);
            }
            Me.CustomData += output + "\n\n";
            var output_lcd2 = output;


            var oresList = GetTotalOres(missingIngots);
            //List<KeyValuePair<Ores, VRage.MyFixedPoint>> missingOres = new List<KeyValuePair<Ores, VRage.MyFixedPoint>>();
            List<Ores> missingOres = new List<Ores>();
            if (lcd3 == null && fitOn2IfPossible)
            {
                output = "\n" + lcd3Title.ToUpper() + "\n\n";
            }
            else
            {
                output = (type3 == LCDType.WIDE ? wideTitle : smallTitle) + "\n" + lcd3Title.ToUpper() + "\n\n";
            }
            //decimalFmt = (oreDecimals > 0 ? "." : "") + string.Concat(Enumerable.Repeat("0", oreDecimals));
            decimalFmt = (oreDecimals > 0 ? "." : "") + new string('0', oreDecimals);
            string scrapOutput = "";
            bool atLeastOneOrePrefersArc = false;
            for (int i = 0; i < oresList.Count; i++)
            {
                var ores = oresList[i];
                var amountPresent = GetCountFromDic(oreAmounts, ores.Key);
                string oreName = oreTranslation[ores.Key] + (ores.Key == Ores.Scrap ? "*" : ""); ;
                if (conversionData[ores.Key].basicRefinery)
                {
                    oreName += "^";
                    atLeastOneOrePrefersArc = true;
                }
                string separator = " | ";
                string normalFmt = "{0:0" + decimalFmt + "}";
                string amountStr = string.Format(normalFmt, (decimal)amountPresent);
                string neededStr = string.Format(normalFmt, (decimal)ores.Value);
                var missing = ores.Value - amountPresent;
                if (missing > 0)
                    missingOres.Add(ores.Key);
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
                    missingStr = missing > 0 ? FormatNumber(missing, oreWidth, oreDecimals) : new string(' ', oreWidth);
                    warnStr = ">> ";
                    okStr = "   ";
                    na = new string(' ', (oreWidth - 1) / 2) + "-" + new string(' ', oreWidth - 1 - (oreWidth - 1) / 2);
                    endNa = new string(' ', oreWidth);
                }
                switch (ores.Key)
                {
                    case Ores.Scrap:
                        if (amountPresent > 0) // if 0 scrap, ignore row
                        {
                            //string na = string.Concat(Enumerable.Repeat(" ", (oreWidth - 1) / 2)) + "-" + string.Concat(Enumerable.Repeat(" ", oreWidth - 1 - (oreWidth - 1) / 2));
                            output += String.Format("{0}{1} {2}{3}{4}{3}{5}\n", okStr, oreName, amountStr, separator, na, endNa);
                            var savedIron = amountPresent * conversionData[Ores.Scrap].conversionRate * (1f / conversionData[Ores.Iron].conversionRate);
                            scrapOutput = "\n*" + String.Format(scrapMetalMessage, FormatNumber(amountPresent, oreWidth, oreDecimals).Trim(), oreTranslation[Ores.Scrap], FormatNumber(savedIron, oreWidth, oreDecimals).Trim(), oreTranslation[Ores.Iron]) + "\n";
                        }
                        break;
                    case Ores.Ice: // if there is Ice, showAllIngotsOres must necessarily be true
                        output += String.Format("{0}{1} {2}{3}{4}{3}{5}\n", (missing > 0 ? warnStr : okStr), oreName, amountStr, separator, na, endNa);
                        break;
                    default:
                        output += String.Format("{0}{1} {2}{3}{4}{3}{5}\n", (missing > 0 ? warnStr : okStr), oreName, amountStr, separator, neededStr, missingStr);
                        break;
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


            output = (type1 == LCDType.WIDE ? wideTitle : smallTitle) + "\n" + lcd1Title.ToUpper() + "\n\n";
            foreach (var component in compList)
            {
                string subTypeId = component.Key.Replace("MyObjectBuilder_BlueprintDefinition/", "");
                var amountPresent = GetCountFromDic(componentAmounts, blueprintDefSubtypeToItemSubtype[subTypeId]);
                string componentName = componentTranslation[subTypeId];
                //string separator = "/";
                string amountStr = amountPresent.ToString();
                string neededStr = component.Value.ToString();
                bool missingOresForComponent = false;
                if (component.Value > 0)
                {
                    foreach (var ingot in componentsToIngots[subTypeId].Keys)
                    {
                        if (missingOres.Contains(ingotToOres[ingot][0])) // we take the first one to ignore missing scrap
                        {
                            missingOresForComponent = true;
                            break;
                        }
                    }
                }
                string warnStr = ">>", okStr = "";
                if (lcd1 != null && lcd1.Font.Equals(monospaceFontName))
                {
                    componentName = String.Format("{0,-" + maxComponentLength + "}", componentName);
                    //separator = "|";
                    amountStr = FormatNumber(amountPresent, compWidth, 0);
                    neededStr = FormatNumber(component.Value, compWidth, 0);
                    warnStr = ">> ";
                    okStr = "   ";
                }

                output += String.Format("{0}{1} {2}|{3}\n", (missingOresForComponent || amountPresent < -component.Value) ? warnStr : okStr, componentName, amountStr, neededStr);
            }
            if (lcd1 != null)
            {
                ShowAndSetFontSize(lcd1, output);
            }
            Me.CustomData += output + "\n\n";
        }

    }
}