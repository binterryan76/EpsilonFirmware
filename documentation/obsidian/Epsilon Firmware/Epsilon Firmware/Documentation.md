You gotta document your documentation 😁
Documentation is split into two sections: [[#Doxygen]], and [[#Obsidian]].
## Doxygen
Doxygen is an open source tool to generate documentation based on the source code, it's xml comments, and additional markdown documentation (written with Obsidian). 
https://www.doxygen.nl/

### Generating Doxygen Documentation
To generate Doxygen documentation:
1. Make sure GraphViz is installed.
	1. https://graphviz.org/download/
2. Make sure doxygen is installed.
	1. https://www.doxygen.nl/
3. Make sure python3 is installed (Python version known to work - 3.14.7).
	1. https://www.python.org/downloads/
4. Make sure python.exe directory and dot.exe directory are in the PATH environment variable.
	1. On windows search and run "Edit the system environment variables".
	2. Click "Environment Variables..."
	3. Select "Path"
	4. Click "Edit..."
	5. Click "New"
	6. Add directory containing python.exe (usually "C:\Users\USERNAME\AppData\Local\Python\bin")
	7. Click "New" again.
	8. Add directory containing dot.exe (usually "C:\Program Files (x86)\Graphviz\bin")
	9. Click "OK" on all 3 windows.
5. Make sure doxyfile is setup correctly (documentation\doxygen\EpsilonFirmwareDoxyfile):
	1. Open doxywizard (usually located in C:\Program Files\doxygen\bin\doxywizard.exe\)
	2. Open the doxyfile (documentation\doxygen\EpsilonFirmwareDoxyfile)
	3. Click the Expert tab and use the search to validate the following settings:
	4. Make sure DOT_PATH is set to the GraphViz location which contains dot.exe (usually C:\Program Files (x86)\Graphviz\bin)
6. Run documentation\doxygen\GenerateDocs.bat 
	1. This will run a pre-processing python script to format and copy the obsidian markdown files so they show up in the Doxygen navigation panel with the proper structure.
	2. Then it will run Doxygen.
		1. This will run a python script to pre-process every \*.cs file because Doxygen doesn't like the default constructor syntax and doesn't like records which EpsilonFirmware uses a lot of. 
		2. This script is indicated in the EpsilonFirmwareDoxyfile with the FILTER_PATTERNS setting.
		3. Hopefully we can get rid of this script eventually once Doxygen is updated and these issues are resolved: 
			1. https://github.com/doxygen/doxygen/issues/8428
			2. https://github.com/doxygen/doxygen/issues/10087

### Viewing Doxygen Documentation
To view Doxygen documentation, run documentation\doxygen\LaunchDocs.bat
This is just a shortcut to documentation\doxygen\html\index.html

## Obsidian
Obsidian is a markdown editor/reader app. It is the preferred way to edit the additional markdown files added to Doxygen.
https://obsidian.md/

### Editing Markdown Documentation
1. Make sure obsidian is installed
	1. https://obsidian.md/download
2. Open Obsidian and click "Open folder as vault"
3. Select directory \documentation\obsidian\Epsilon Firmware
4. Edit the files as you normally would but note that for them to show up in Doxygen, you will have to do this: [[#Generating Doxygen Documentation]]