/*
 *   R e a d m e
 *   -----------
 * 
 *   Projector2Assembler by Juggernaut93
 *   
 *   Queue all the components needed to build a blueprint on your assembler.
 *   
 *   With some code by nihilus from here:
 *   https://forum.keenswh.com/threads/adding-needed-projector-bp-components-to-assembler.7396730/#post-1287067721
 *   
 *   CHECK ALSO: Projector2LCD (also by Juggernaut93) to show info about missing components, ingots and ores.
 *   Link: https://steamcommunity.com/sharedfiles/filedetails/?id=1500259551
 *   
 *   MODS COMPATIBILITY:
 *      - By default the script is not compatible with mods adding new blocks or modifying their needed components.
 *      - The script can be made compatible with mods that modify or add new block definitions (but without adding new
 *          kinds of components to be assembled in an Assembler) running the following app:
 *          https://github.com/Juggernaut93/Projector2Assembler/releases
 *      - Run the .exe file and follow the instructions on screen. A file will be created with the line of text
 *          that needs to be added to the script.
 *      - The app should be runnable on Windows 10 without additional dependencies.
 *      - On older versions of Windows this package might be needed:
 *          https://www.microsoft.com/en-US/download/details.aspx?id=48145
 *      - If you have problems running the .exe app, you can run the .py executable using Python on the command line.
 *   
 *   SETUP:
 *      - You obviously need a programming block, a projector, an assembler
 *      - Run the script with this argument: [ProjectorName];[AssemblerName];[lightArmor];[staggeringFactor];[fewFirst];[onlyRemaining]
 *          - [] indicates it's an optional parameter
 *          - ProjectorName (default: Projector) is the name of the projector with the blueprint you want to build
 *          - AssemblerName (default: Assembler) is the name of the assembler that will produce the needed components
 *          - lightArmor is true (default) or false and tells the script to assume all the armor blocks listed by
 *              the projector are respectively Light Armor Blocks or Heavy Armor Blocks
 *          - staggeringFactor is a non-negative integer (10 by default) that tells the script how to stagger the
 *              production
 *          - fewFirst is true (default) or false and tells the script to queue the components in the assembler
 *              sorted by amount. If false, the order is undefined (currently it's alphabetical, but it's not
 *              guaranteed to stay the same in the future)
 *          - onlyRemaining is true or false (default) and tells the script whether to check for the components
 *              already in stock in the entire grid and to only queue the missing components in the assembler.
 *              By default the script doesn't check for subgrids. You can turn the feature on by setting the variable
 *              inventoryFromSubgrids to true. WARNING: queuing more than one blueprint at a time with this option
 *              on would result in the available components being subtracted by each blueprint, use CAREFULLY
 *  
 *   HOW IT WORKS:
 *      - The script gets from the projector the remaining blocks to build. Unfortunately, the projector is not
 *          precise about the type of armor blocks to build and only gives a generic "armor blocks". You can then
 *          specify if you want to assume all the blocks are light or heavy armor blocks, but keep in mind that
 *          the script will overproduce if you specify heavy blocks but not all your blocks are full cubes and/or
 *          you also have light blocks; it will (probably) underproduce if you specify light blocks but you have
 *          many heavy armor blocks.
 *      - The script then sorts the components needed to build such blocks (if you set fewFirst to true) by
 *          ascending amount and will divide the amount of components by staggeringFactor. It will then tell the
 *          assembler to produce the obtained amount of components staggeringFactor times.
 *      - EXAMPLE:
 *          - your blueprint requires 3000 steel plates, 1500 construction components and 300 metal grids
 *          - you run the script with fewFirst = true and staggeringFactor = 10
 *          - the script will tell the assembler to build 30 metal grids, 150 construction components and
 *            300 steel plates 10 times
 *          - This will ensure your welding process won't be blocked by the lack of a single component
 *            that might have ended up at the end of the queue, and you will get a nice proportion of each
 *            component without waiting too much
 *            
 */