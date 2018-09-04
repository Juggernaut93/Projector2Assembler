/*
 *   R e a d m e
 *   -----------
 * 
 *   Projector2LCD by Juggernaut93
 *   
 *   Show info about missing components, ingots and ores for your blueprint.
 *   
 *   With some code by nihilus from here:
 *   https://forum.keenswh.com/threads/adding-needed-projector-bp-components-to-assembler.7396730/#post-1287067721
 *   
 *   CHECK ALSO: Projector2Assembler to queue blueprint components to an assembler.
 *   Link: https://steamcommunity.com/sharedfiles/filedetails/?id=1488278599
 *   
 *   AND Assembler Needs Calculator to show info about missing materials for assemblers.
 *   Link: https://steamcommunity.com/sharedfiles/filedetails/?id=1501171322
 *   
 *   SETUP:
 *      - You need a programming block, a projector and up to 3 LCD screens. Text panels, small and wide LCD
 *        panels are supported. The use of Monospace font is RECOMMENDED (but not mandatory).
 *      - Run the script with this argument: [ProjectorName];[LCDName1];[LCDName2];[LCDName3];[lightArmor];[yieldPorts]
 *          - [] indicates it's an optional parameter
 *          - ProjectorName is the name of the projector with the blueprint you want to show info about. If you
 *              don't specify a projector, the script will continuously search for a currently active projector
 *              and show the related info
 *          - LCDName1 is the name of the LCD that will show info about which components are needed to build the
 *              blueprint (see HOW IT WORKS)
 *          - LCDName2 is the name of the LCD that will show info about which ingots/refined ores are needed to
 *              build the blueprint (see HOW IT WORKS)
 *          - LCDName3 is the name of the LCD that will show info about which ores are needed to build the
 *              blueprint (see HOW IT WORKS). If no valid third LCD is specified, the script will try to fit the
 *              info on the second LCD (if specified) (see ADDITIONAL CONFIGURATION)
 *          - lightArmor is true (default) or false and tells the script to assume all the armor blocks listed by
 *              the projector are respectively Light Armor Blocks or Heavy Armor Blocks
 *          - yieldPorts is an integer between 0 and 8 and specifies how many ports of your refineries should
 *              be considered as covered by a Yield Module. This value will affect calculations regarding how
 *              much of each ore is needed for your blueprint. If you don't specify yieldPorts, the script
 *              will use the average effectiveness of the ENABLED refineries on your grid. Note that the script
 *              will also account for available Arc Furnaces and will show the better option
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
 *          - autoResizeText specifies if text should be resized to fit the LCD screen. Only works if the LCD
 *              is set to Monospace font.
 *          - fitOn2IfPossible determines if the script can try to fit the information about missing ores
 *              on the seconds LCD when the third LCD is not specified or invalid
 *      It is also possible to easily change the language of the text shown by modifying the strings in the
 *      section "LOCALIZATION STRINGS". Be careful not to remove the text in curly braces: it serves as
 *      a placeholder to be later filled with numerical or text values.
 *          
 *   HOW IT WORKS:
 *      - The script gets from the projector the remaining blocks to build. Unfortunately, the projector is not
 *          precise about the type of armor blocks to build and only gives a generic "armor blocks". You can then
 *          specify if you want to assume all the blocks are light or heavy armor blocks, but keep in mind that
 *          the script will overproduce if you specify heavy blocks but not all your blocks are full cubes and/or
 *          you also have light blocks; it will (probably) underproduce if you specify light blocks but you have
 *          many heavy armor blocks.
 *      - The script then checks if the specified projector is available, otherwise it will search for one
 *          that is currently projecting.
 *      - The script then proceeds to compute the various components, ingots and ore needed, using the average
 *          refinery effectiveness at transforming ores to ingots (or the one you have manually specified with
 *          the yieldPorts parameter).
 *      - The computed info are then shown on the available LCDs. If one of the LCDs is not found or is not
 *          specified, the script will simply ignore it, except for when fitOn2IfPossible is true: as explained
 *          before, in this case the content of the third LCD can be shown on the second one, if the third LCD
 *          is not available and the second one is. Each LCD will show the name of the chosen projector.
 *          Also, each LCD will highlight with a ">>" the missing materials.
 *      - COMPONENT LCD CONTENT:
 *          - AVAILABLE column: the amount of each component that is currently in inventory
 *          - NEEDED column: the amount needed to build the blocks of the blueprint that still have to be built
 *          - MISSING column: the difference between NEEDED and AVAILABLE. Not shown if 0.
 *      - INGOT LCD CONTENT:
 *          - AVAILABLE column: the amount of each ingot type that is currently in inventory
 *          - NEEDED NOW/TOTAL column: the amount of ingots needed to build the MISSING components vs. the
 *              amount of ingots needed to build NEEDED components (i.e. all the remaining blocks)
 *          - MISSING column: the difference between NEEDED NOW and AVAILABLE. Not shown if 0. It represents
 *              how many additional ingots have to be produced to build the missing components
 *      - ORE LCD CONTENT:
 *          - AVAILABLE column: the amount of each ore that is currently in inventory
 *          - NEEDED NOW/TOTAL column: the amount of ores needed to build the MISSING ingots vs. the
 *              amount of ores needed to build NEEDED TOTAL ingots, aka NEEDED components
 *              (i.e. all the remaining blocks)
 *          - MISSING column: the difference between NEEDED NOW and AVAILABLE. Not shown if 0. It represents
 *              how many additional ores have to be mined to build the missing ingots
 *          The panel will also show how much iron ore the available scrap metal (if any) can save you and
 *          the refinery effectiveness percentage used to compute the needed ores (together with the equivalent
 *          amount of ports covered by yield modules - exact if specified, averaged if the effectiveness has
 *          been averaged). Moreover, ores that can be refined at higher yield on an available Arc Furnace
 *          will be marked with a '^'.
 */