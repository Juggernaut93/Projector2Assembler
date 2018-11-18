import os
import xml.etree.ElementTree as ET
import winreg
import vdf
from tkinter import *
from tkinter import messagebox, filedialog
import sys
import urllib.request
import zipfile
import tempfile

steam_key = winreg.OpenKey(winreg.HKEY_CURRENT_USER, "Software\Valve\Steam")
steam_path = winreg.QueryValueEx(steam_key, "SteamPath")[0]
default_lib_path = os.path.join(steam_path, "steamapps", "common")
libraries_file_path = os.path.join(steam_path, "steamapps", "libraryfolders.vdf")
with open(libraries_file_path, 'r') as f:
    libraries_dic = vdf.load(f)['LibraryFolders']

libraries = [default_lib_path]
i = 1
while str(i) in libraries_dic:
    libraries.append(os.path.join(libraries_dic[str(i)].replace("\\\\", "\\"), "steamapps", "common"))
    i += 1

found = False
for lib in libraries:
    cur = os.path.join(lib, "SpaceEngineers")
    if os.path.exists(os.path.join(cur, "Bin64", "SpaceEngineers.exe")):
        SE_install_path = cur
        print("Space Engineers install path detected.")
        found = True

if not found:
    while not found:
        SE_install_path = input("Couldn't find Space Engineers folder. Insert Space Engineers path here: ")
        if os.path.exists(os.path.join(SE_install_path, "Bin64", "SpaceEngineers.exe")):
            found = True

SE_user_path = os.path.join(os.getenv("APPDATA"), "SpaceEngineers")
SE_mod_path = os.path.join(SE_user_path, "Mods")
SE_save_path = os.path.join(SE_user_path, "Saves")

def getMaxSaveLength(users):
    maxLength = 0
    for u in users:
        saves = [save for save in os.listdir(os.path.join(SE_save_path, u)) if os.path.isdir(os.path.join(SE_save_path, u, save))]
        for i in saves:
            if len(i) > maxLength:
                maxLength = len(i)
    return maxLength

prev_val = ''
def updateSaveList(optMenu, folder, var):
    global prev_val
    if prev_val == folder:
        return # ignore
    var.set('')
    optMenu['menu'].delete(0, 'end')
    userpath = os.path.join(SE_save_path, folder)
    saves = [save for save in os.listdir(userpath) if os.path.isdir(os.path.join(userpath, save))]
    saves.sort(key=lambda s: os.path.getmtime(os.path.join(userpath, s)), reverse=True)
    for i in saves:
        optMenu['menu'].add_command(label=i, command=lambda x=i: var.set(x))
    var.set(saves[0])
    prev_val = folder

def centerWindow(w):
    w.update()
    # Gets the requested values of the height and widht.
    windowWidth = w.winfo_width()
    windowHeight = w.winfo_height()
    # print("Width",windowWidth,"Height",windowHeight)
    
    # Gets both half the screen width/height and window width/height
    positionRight = int(w.winfo_screenwidth() / 2 - windowWidth / 2)
    positionDown = int(w.winfo_screenheight() / 2 - windowHeight / 2)
    
    # Positions the window in the center of the page.
    w.geometry("+{}+{}".format(positionRight, positionDown))

def showSelectSaveDialog(w, optionsUser, optionsSave):
    w.title('Select save file')
    w.resizable(False, False)

    lUser = Label(w, text = "Player ID:")
    lUser.grid(row = 0, column = 0)

    userIds = [id for id in os.listdir(SE_save_path) if os.path.isdir(os.path.join(SE_save_path, id))]
    optionsUser.set(userIds[0])
    menuUser = OptionMenu(w, optionsUser, *userIds)
    menuUser.grid(row = 0, column = 1, sticky = "ew")

    lSaves = Label(w, text = "Save:")
    lSaves.grid(row = 1, column = 0)

    menuSaves = OptionMenu(w, optionsSave, '')
    updateSaveList(menuSaves, optionsUser.get(), optionsSave)
    menuSaves.grid(row = 1, column = 1, sticky = "ew")
    menuSaves.configure(width = getMaxSaveLength(userIds))
    
    optionsUser.trace_add('write', lambda *args: updateSaveList(menuSaves, optionsUser.get(), optionsSave))

    confirmed = False
    def conf(bool):
        w.destroy()
        nonlocal confirmed
        confirmed = bool
    #bCancel = Button(w, text="Cancel", command = lambda *args: conf(False))
    #bCancel.grid(row = 2, column = 0)
    bOK = Button(w, text="Confirm", width = 8, command = lambda *args: conf(True))
    bOK.grid(row = 2, column = 0, columnspan = 2, pady = 10, padx = 20, sticky = "e")

    w.deiconify()
    centerWindow(w)
    mainloop()
    return confirmed

def getModsFromSave(save_folder):
    save = ET.parse(os.path.join(save_folder, 'Sandbox.sbc'))
    root = save.getroot()
    mods = root.find('Mods').findall('ModItem')
    return [mod.find('Name').text for mod in mods]

def getModName(mod):
    if os.path.isdir(os.path.join(SE_mod_path, mod)):
        return mod

    with urllib.request.urlopen("https://steamcommunity.com/sharedfiles/filedetails/?id=" + mod.replace('.sbm', '')) as page:
        contents = page.read().decode("utf-8")
    idx = contents.find('<div class="workshopItemTitle">')
    if idx == -1:
        #with open("C:\\Users\\julia\\Desktop\\Output.html", "w") as text_file:
        #    text_file.write(contents)
        #print(idx)
        return mod
    return contents[idx + len('<div class="workshopItemTitle">') : contents.find('</div', idx)].strip()

def showSelectModsDialog(w):
    w.title('Select mods')
    w.resizable(False, False)

    #lSaves = Label(w, text = "Mods:")
    #lSaves.grid(row = 0, column = 0)

    listbox = Listbox(w, selectmode = MULTIPLE)
    mods = []
    maxWidth = 0
    for mod in os.listdir(SE_mod_path):
        print('Identifying', mod, '...')
        mods.append(mod)
        modname = getModName(mod)
        if len(modname) > maxWidth:
            maxWidth = len(modname)
        listbox.insert(END, modname)
    listbox.config(width = maxWidth)
    listbox.grid(row = 0, column = 0, columnspan = 3)

    bSelect = Button(w, text="Select all", command = lambda *args: listbox.select_set(0, END))
    bSelect.grid(row = 1, column = 0, pady = 10)
    bDeselect = Button(w, text="Deselect all", command = lambda *args: listbox.selection_clear(0, END))
    bDeselect.grid(row = 1, column = 1, pady = 10)
    
    confirmed = False
    ret = []
    def conf(bool):
        nonlocal confirmed, ret
        confirmed = bool
        ret = [mods[i] for i in listbox.curselection()]
        w.destroy()
    bOK = Button(w, text="Confirm", width = 8, command = lambda *args: conf(True))
    bOK.grid(row = 1, column = 2, pady = 10, padx = 20)
    
    w.deiconify()
    centerWindow(w)
    mainloop()
    return confirmed, ret

bpnames = {
    "SteelPlate": "SteelPlate",
    "Construction": "ConstructionComponent",
    "PowerCell": "PowerCell",
    "Computer": "ComputerComponent" ,
    "LargeTube": "LargeTube",
    "Motor": "MotorComponent",
    "Display": "Display",
    "MetalGrid": "MetalGrid",
    "InteriorPlate": "InteriorPlate",
    "SmallTube": "SmallTube",
    "RadioCommunication": "RadioCommunicationComponent",
    "BulletproofGlass": "BulletproofGlass",
    "Girder": "GirderComponent",
    "Explosives": "ExplosivesComponent",
    "Detector": "DetectorComponent",
    "Medical": "MedicalComponent",
    "GravityGenerator": "GravityGeneratorComponent",
    "Superconductor": "Superconductor",
    "Thrust": "ThrustComponent",
    "Reactor": "ReactorComponent",
    "SolarCell": "SolarCell"
}

tempdirs = []
def extractMod(m):
    with zipfile.ZipFile(m, 'r') as zip_ref:
        folder = tempfile.TemporaryDirectory()
        tempdirs.append(folder)
        zip_ref.extractall(folder.name)
    return folder.name

def closeAllTempDirs():
    for t in tempdirs:
        t.cleanup()

def getCubeBlocksTree(m):
    m = os.path.join(m, "Data")
    parseTrees = []
    if os.path.exists(m):
        files = [os.path.join(m, file) for file in os.listdir(m) if os.path.isfile(os.path.join(m, file)) and file.endswith(".sbc")]
        for f in files:
            tree = ET.parse(f)
            cb = tree.getroot().find('CubeBlocks')
            if cb is None:
                continue
            df = cb.find('Definition')
            if df is None:
                continue
            parseTrees.append(cb)
    return parseTrees


blockDefinitions = dict()
    
def pushDefinition(type, subType, comps):
    if type in blockDefinitions:
        blockDefinitions[type].append(subType + "=" + comps)
    else:
        blockDefinitions[type] = [subType + "=" + comps]

alreadyExaminedBlocks = []
subTypes = []
warnings = False
def examinePackage(m):
    if os.path.isfile(m):
        m = extractMod(m)
    if not os.path.isdir(m):
        raise Exception("Invalid mod detected: " + m)

    trees = getCubeBlocksTree(m)
    global alreadyExaminedBlocks, subTypes, warnings
    
    for cubeBlocks in trees:
        blocks = cubeBlocks.findall('Definition')
        for cur in blocks:
            type = cur.find('Id').find('TypeId').text
            if type[0:16] == "MyObjectBuilder_":
                type = type[16:]
            subType = cur.find('Id').find('SubtypeId').text
            if subType is None:
                subType = "(null)"
            subType = subType.replace(" ", "")
            
            conciseName = type + '/' + subType
            if (conciseName) in alreadyExaminedBlocks:
                continue
            
            components = cur.find('Components')
            if components is None:
                print("WARNING: mod {} has malformed CubeBlock definition: no Components element found for {}. Game will crash when using this block.".format(m, conciseName))
                warnings = True
                continue
            components = components.findall('Component')
            if len(components) == 0:
                print("WARNING: mod {} has malformed CubeBlock definition: no needed Component elements found for {}. Game will crash when using this block.".format(m, conciseName))
                warnings = True
                continue
            alreadyExaminedBlocks.append(conciseName)
            
            comps = dict()
            for curComp in components:
                compSubType = curComp.get('Subtype')
                if compSubType in subTypes:
                    foundSubType = subTypes.index(compSubType)
                else:
                    subTypes.append(compSubType)
                    foundSubType = subTypes.index(compSubType)
        
                # add same component requirements together
                # (steel plate to start a block, then more steel plates to weld it to
                # 100%)
                if foundSubType in comps:
                    comps[foundSubType] += int(curComp.get('Count'))
                else:
                    comps[foundSubType] = int(curComp.get('Count'))
            strComps = []
            for key, value in comps.items():
                strComps.append('{}:{}'.format(key, value))
            pushDefinition(type, subType, ','.join(strComps))

def assembleBlockDefinitionData():
    global bpnames, subTypes, blockDefinitions
    subTypes = list(map(lambda x: bpnames[x], subTypes))
    ret = '*'.join(subTypes)
    
    for key, value in blockDefinitions.items():
        ret += '$' + key + '*' + '*'.join(value)
    return ret

def main():
    w = Tk()
    w.withdraw() # to avoid showing window with messagebox
    
    useSave = messagebox.askyesno(title = "Mod list source", message = "Do you want to get the mod list from a save file (recommended)?")
    
    if useSave:
        # res = filedialog.askdirectory(initialdir = SE_save_path)
        optionsUser = StringVar()
        optionsSave = StringVar()
        confirmed = showSelectSaveDialog(w, optionsUser, optionsSave)
        if not confirmed:
            print("Operation cancelled by the user.")
            sys.exit()
        save_folder = os.path.join(SE_save_path, optionsUser.get(), optionsSave.get())
        mods = getModsFromSave(save_folder)
    else:
        confirmed, mods = showSelectModsDialog(w)
        if not confirmed:
            print("Operation cancelled by the user.")
            sys.exit()
    
    print("Computing block definitions. Please wait...")
    # last element in list has the highest priority = it has to be analyzed
    # first
    mods.reverse()
    for m in mods:
        examinePackage(os.path.join(SE_mod_path, m))
        # first add components from mods, maintain a list of already examined
        # blocks, then read cubeblocks from main CubeBlocks.sbc and add remaining
        # blocks
    
    examinePackage(os.path.join(SE_install_path, "Content"))

    blockDefinitionData = assembleBlockDefinitionData()
    print("Block definitions collected.")

    w = Tk()
    w.withdraw() # to avoid showing window with messagebox
    global warnings
    if warnings:
        go_on = messagebox.askyesno(title = "Warning", message = "Process completed with warnings: do you still want to generate the output file?", icon = messagebox.WARNING)
    else:
        go_on = True

    if not go_on:
        print("Operation cancelled by the user.")
        sys.exit()
    
    outFileName = filedialog.asksaveasfilename(initialdir = os.path.join(os.environ["HOMEPATH"], "Desktop"), title = "Select output file path", initialfile = "blockDefinitionData.txt", filetypes = [("Text file", ".txt")])
    if outFileName == "":
        print("Operation cancelled by the user.")
        sys.exit()
    outStr = 'string blockDefinitionData = "{}";\r\n'.format(blockDefinitionData)
    with open(outFileName, "w") as f:
        f.write(outStr)
    openfile = messagebox.askyesno(title = "Success", message = "Block definitions string successfully saved to {}.\n\nCopy-paste this string at the bottom of the Projector2Assembler/Projector2LCD script replacing the line starting with 'string blockDefinitionData'.\n\nDo you want to open the output file?"
                        .format(outFileName), icon = messagebox.INFO)
    if openfile:
        os.startfile(outFileName)
    print("Operation completed successfully.")
            
    closeAllTempDirs()

# run main program
main()