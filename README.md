A set of Space Engineers scripts to produce and track info about blueprint and components building.

Import the solution in Visual Studio 2019 with [Malware's Development Kit for Space Engineers (MDK-SE)](https://github.com/malware-dev/MDK-SE) installed. Version 16.8 or higher is required if you want to use the most recent version of MDK-SE that supports VS 2019. Note that the most recent MDK-SE requires VS 2022, but I haven't updated the projects yet for VS 2022 (it should be trivial anyway if you want to try).

[**Projector2Assembler**](ProjectorResourceBuilder): queue all the components needed to build a blueprint on your assembler.

Available here: https://steamcommunity.com/sharedfiles/filedetails/?id=1488278599

[**Projector2LCD**](Projector2LCD): show info about missing components, ingots and ores for your blueprint.

Available here: https://steamcommunity.com/sharedfiles/filedetails/?id=1500259551

[**Assembler Needs Calculator**](Assembler%20Needs%20Calculator): show info about missing ingots and ores for your assemblers.

Available here: https://steamcommunity.com/sharedfiles/filedetails/?id=1501171322

[**BlockDefinitionExtractor**](BlockDefinitionExtractor): a Python script to extract block definitions necessary to use Projector2Assembler and Projector2LCD with **modded** games that include non-vanilla blocks. You can download a self-contained version of the script in the [Releases](https://github.com/Juggernaut93/Projector2Assembler/releases) page. Installing Python is *NOT* needed to run the exe version. [Visual C++ Redistributable for Visual Studio 2015](https://www.microsoft.com/en-US/download/details.aspx?id=48145) may be needed if you are using Windows versions older than Windows 10 (i.e. Win 7/8, etc.). If you have problems, you can still run the script manually with Python on command line.
