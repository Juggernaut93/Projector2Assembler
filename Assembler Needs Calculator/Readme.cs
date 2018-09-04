/*
 *   R e a d m e
 *   -----------
 * 
 *   Assembler Needs Calculator by Juggernaut93
 *   
 *   Show info about missing ingots and ores for your assemblers.
 *   
 *   CHECK ALSO: Projector2LCD (also by Juggernaut93) to show info about missing materials for blueprints.
 *   Link: https://steamcommunity.com/sharedfiles/filedetails/?id=1500259551
 *   
 *   SETUP:
 *      - You need a programming block, assemblers and up to 3 LCD screens. Text panels, small and wide LCD
 *        panels are supported. The use of Monospace font is RECOMMENDED (but not mandatory).
 *      - Run the script with this argument: [AssemblerGroupName];[LCDName1];[LCDName2];[LCDName3];[yieldPorts]
 *          - [] indicates it's an optional parameter
 *          - AssemblerGroupName is the name of the group of assemblers you want to show info about. If you
 *              don't specify a group, the script will show info about all the assemblers on your grid
 *          - LCDName1 is the name of the LCD that will show info about which components are currently in
 *              inventory and the ones your assemblers are producing (see HOW IT WORKS). The list of elements
 *              shown can be configured (see ADDITIONAL CONFIGURATION)
 *          - LCDName2 is the name of the LCD that will show info about which ingots/refined ores are needed to
 *              build the components in your assemblers' queue (see HOW IT WORKS)
 *          - LCDName3 is the name of the LCD that will show info about which ores are needed to build the
 *              components in your assemblers' queue (see HOW IT WORKS). If no valid third LCD is specified,
 *              the script will try to fit the info on the second LCD (if specified) (see ADDITIONAL CONFIGURATION)
 *          - yieldPorts is an integer between 0 and 8 and specifies how many ports of your refineries should
 *              be considered as covered by a Yield Module. This value will affect calculations regarding how
 *              much of each ore is needed for your blueprint. If you don't specify yieldPorts, the script
 *              will use the average effectiveness of the ENABLED refineries on your grid
 *      - >>> IMPORTANT <<<
 *        You HAVE to set the ASSEMBLER_EFFICIENCY variable at the top of the script according to your world
 *        settings: if you have set the assembler efficiency to realistic, set the variable to 1; if you have set
 *        it to 3x, then set the variable to 3; if you have set it to 10x, then set the variable to 10;
 *      - The script will run indefinitely with the specified settings. To change settings, just re-run the
 *          script with the different parameters.
 *  
 *   ADDITIONAL CONFIGURATION:
 *      The script has a number of hardcoded parameters that you can change. The parameters are in the section
 *      "CONFIGURATION" at the top of the script. Such parameters are:
 *          - compWidth, ingotWidth, oreWidth: if the respective LCD has monospace font, these specify the width
 *              of each shown numerical fields (including dots, decimal digits and suffixes - k, M, G) for,
 *              respectively, the component LCD, the ingot LCD and the ore LCD. The script will try the show
 *              exact amounts if they fit in the specified space. If it's not possible, the script will show
 *              amounts in thousands (k), millions (M) or billions (G). If the number still cannot fit, its
 *              integer part will be shown anyway, so specifying too small widths (< 4-5) is useless.
 *          - ingotDecimals, oreDecimals: these specify the maximum number of decimal digits to show for ingots
 *              and ores. Components cannot have fractionary amounts.
 *          - inventoryFromSubgrids specifies if inventories on subgrids have to be considered when computing
 *              available materials
 *          - refineriesFromSubgrids specifies if refineries on subgrids have to be considered when computing
 *              average effectiveness
 *          - assemblersFromSubgrids specifies if assemblers on subgrids have to be considered when no
 *              assemblerGroupName is specified
 *          - autoResizeText specifies if text should be resized to fit the LCD screen. Only works if the LCD
 *              is set to Monospace font.
 *          - fitOn2IfPossible determines if the script can try to fit the information about missing ores
 *              on the seconds LCD when the third LCD is not specified or invalid
 *          - alwaysShowAmmos, alwaysShowTools: if true, the first LCD will show ammos/tools on screen even
 *              when no assembler is assembling/disassembling them. Tools also include rifles and H2/O2
 *              bottles. If false, ammos/tools will only be shown if they are being assembled or disassembled.
 *              Setting alwaysShowTools to true may clutter the screen with too much info. Note that the other
 *              types of components are always listed, regardless of their presence in an assembler's queue.
 *          - showAllIngotsOres will tell the script to show every type of ingot and ore available in the
 *              game even if it isn't needed to build objects in the assembler. Useful for monitoring purposes.
 *              Note that scrap metal will still be ignored if it's not present in inventory, to avoid
 *              unnecessary clutter.
 *          - onlyEnabledAssemblers: if true, only enabled assemblers will be considered (if no assembler
 *              group is specified)
 *      It is also possible to easily change the language of the text shown by modifying the strings in the
 *      section "LOCALIZATION STRINGS". Be careful not to remove the text in curly braces: it serves as
 *      a placeholder to be later filled with numerical or text values.
 *          
 *   HOW IT WORKS:
 *      - The script gets from the specified assemblers the components currently in queue. If the specified
 *          group does not exist, the script will consider all the assemblers on your grid.
 *      - The script then proceeds to compute the various ingots and ores needed, using the average
 *          refinery effectiveness at transforming ores to ingots (or the one you have manually specified with
 *          the yieldPorts parameter).
 *      - The computed info are then shown on the available LCDs. If one of the LCDs is not found or is not
 *          specified, the script will simply ignore it, except for when fitOn2IfPossible is true: as explained
 *          before, in this case the content of the third LCD can be shown on the second one, if the third LCD
 *          is not available and the second one is. Each LCD will show the name of the assembler group (if
 *          applicable) and the number of assemblers considered.
 *          Also, each LCD will highlight with a ">>" the missing ingots and ores, the components for which
 *          the available ores are insufficient (they cannot produce enough ingots to build all the components)
 *          and the components in disassembling queues where the disassembling amount is higher than the
 *          amount in inventory (the assembler will get stuck).
 *      - COMPONENT LCD CONTENT:
 *          - AVAILABLE column: the amount of each component that is currently in inventory
 *          - IN PRODUCTION column: the amount of each component that is currently in production (negative
 *              means disassembling)
 *      - INGOT LCD CONTENT:
 *          - AVAILABLE column: the amount of each ingot type that is currently in inventory
 *          - NEEDED column: the amount of ingots needed to build the components currently in production
 *              (negative means ingots will be available from disassembling)
 *          - MISSING column: the difference between NEEDED and AVAILABLE. Not shown if 0. It represents how
 *              many additional ingots have to be produced to build the components in the queue
 *      - ORE LCD CONTENT:
 *          - AVAILABLE column: the amount of each ore that is currently in inventory
 *          - NEEDED column: the amount of ores needed to build the MISSING ingots. A "-" sign means this ore
 *              is not used to build materials in the assembler (e.g. Ice) or a NEEDED value doesn't make
 *              sense (e.g. Scrap Metal)
 *          - MISSING column: the difference between NEEDED and AVAILABLE. Not shown if 0. It represents how
 *          many additional ores have to be mined to build the missing ingots
 *          The panel will also show how much iron ore the available scrap metal (if any) can save you and
 *          the refinery effectiveness percentage used to compute the needed ores (together with the equivalent
 *          amount of ports covered by yield modules - exact if specified, averaged if the effectiveness has
 *          been averaged).
 *            
 */