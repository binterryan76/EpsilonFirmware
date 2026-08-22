# Documentation {#md_epsilon_firmware_epsilon_firmware_documentation}

You gotta document your documentation 😁
Documentation is split into two sections: [Doxygen](@ref md_epsilon_firmware_epsilon_firmware_documentation_doxygen), and [Obsidian](@ref md_epsilon_firmware_epsilon_firmware_documentation_obsidian).
## Doxygen {#md_epsilon_firmware_epsilon_firmware_documentation_doxygen}
Doxygen is an open source tool to generate documentation based on the source code, it's xml comments, and additional markdown documentation (written with Obsidian). 
https://www.doxygen.nl/

### Generating Doxygen Documentation {#md_epsilon_firmware_epsilon_firmware_documentation_generating_doxygen_documentation}
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
	6. Add directory containing python.exe (usually `C:\Users\USERNAME\AppData\Local\Python\bin`)
	7. Click "New" again.
	8. Add directory containing dot.exe (usually `C:\Program Files (x86)\Graphviz\bin`)
	9. Click "OK" on all 3 windows.
5. Make sure doxyfile is setup correctly (`documentation\doxygen\EpsilonFirmwareDoxyfile`):
	1. Open doxywizard (usually located in `C:\Program Files\doxygen\bin\doxywizard.exe`\)
	2. Open the doxyfile (`documentation\doxygen\EpsilonFirmwareDoxyfile`)
	3. Click the Expert tab and use the search to validate the following settings:
	4. Make sure DOT_PATH is set to the GraphViz location which contains dot.exe (usually `C:\Program Files (x86)\Graphviz\bin`)
6. Run `documentation\doxygen\GenerateDocs.bat` 
	1. This will run a pre-processing python script [generate_markdown_nav.py](@ref md_epsilon_firmware_epsilon_firmware_documentation_generate_markdown_nav_py) 
	2. Then it will run Doxygen which will run [doxygen_record_filter.py](@ref md_epsilon_firmware_epsilon_firmware_documentation_doxygen_record_filter_py) on every \*.cs file

#### Doxygen Python Scripts {#md_epsilon_firmware_epsilon_firmware_documentation_doxygen_python_scripts}
Generating the Doxygen documentation uses two python scripts to format the Doxygen pages.

##### generate_markdown_nav.py {#md_epsilon_firmware_epsilon_firmware_documentation_generate_markdown_nav_py}
This is the first python script run when generating Doxygen documentation. 
It does the following:
- Makes a copy of the Obsidian markdown files in `documentation\obsidian_formatted_for_doxygen`
- Creates the required files for Doxygen to have an expanding section for markdown documentation which matches the hierarchical folder structure of the Obsidian documentation inside the `obsidian_formatted_for_doxygen` directory.
- Reformats internal Obsidian links to specific headers by adding unique Doxygen tags at the end of every Obsidian header and changing links to reference those Doxygen tags.

##### doxygen_record_filter.py {#md_epsilon_firmware_epsilon_firmware_documentation_doxygen_record_filter_py}
This python script is ran on every \*.cs file because Doxygen doesn't understand C# default constructor syntax and also doesn't understand C# records which EpsilonFirmware uses a lot of. This script changes records to classes so they show up in the Doxygen classes section. It also adds constructors for classes and records so Doxygen doesn't interpret classes/records with default constructors as functions.
1. This script is pointed to by the EpsilonFirmwareDoxyfile with the FILTER_PATTERNS setting.
2. This script is called by both [qualify_wikilinks.py](@ref md_epsilon_firmware_epsilon_firmware_documentation_qualify_wikilinks_py) and [generate_markdown_nav.py](@ref md_epsilon_firmware_epsilon_firmware_documentation_generate_markdown_nav_py)
3. Hopefully we can get rid of this script eventually once Doxygen is updated and these issues are resolved: 
	 - https://github.com/doxygen/doxygen/issues/8428
	 - https://github.com/doxygen/doxygen/issues/10087

##### qualify_wikilinks.py {#md_epsilon_firmware_epsilon_firmware_documentation_qualify_wikilinks_py}
This python script is called inside `GenerateDocs.bat` and it alters the Obsidian markdown files to make links to classes using brackets \[\[ ClassName\]\] into fully qualified links so `generate_markdown_nav.py` detects them properly.

### Viewing Doxygen Documentation {#md_epsilon_firmware_epsilon_firmware_documentation_viewing_doxygen_documentation}
To view Doxygen documentation, run `documentation\doxygen\LaunchDocs.bat`
This is just a shortcut to `documentation\doxygen\html\index.html`

## Obsidian {#md_epsilon_firmware_epsilon_firmware_documentation_obsidian}
Obsidian is a markdown editor/reader app. It is the preferred way to edit the additional markdown files added to Doxygen.
https://obsidian.md/

### Editing Markdown Documentation {#md_epsilon_firmware_epsilon_firmware_documentation_editing_markdown_documentation}
1. Make sure obsidian is installed
	1. https://obsidian.md/download
2. Open Obsidian and click "Open folder as vault"
3. Select directory `\documentation\obsidian\Epsilon Firmware`
4. Edit the files as you normally would but note that for them to show up in Doxygen, you will have to do this: [Generating Doxygen Documentation](@ref md_epsilon_firmware_epsilon_firmware_documentation_generating_doxygen_documentation)

#### Additional Notes {#md_epsilon_firmware_epsilon_firmware_documentation_additional_notes}
- If you want to reference a class in Obsidian markdown documentation, enclose the class name in double square brackets \[\[ClassName\]\]
- If you want to reference a member in a class, follow this format: \[\[ClassName#MemberName\]\]
