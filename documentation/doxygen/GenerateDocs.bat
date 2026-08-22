python3 qualify_wikilinks.py ../obsidian --code-dir ../../src --filter-pattern "*.cs=python3 doxygen_record_filter.py" --write --no-backup
python3 generate_markdown_nav.py ../obsidian ../obsidian_formatted_for_doxygen --code-dir ../../src --root-title "Markdown Documentation" --filter-pattern "*.cs=python3 doxygen_record_filter.py"
doxygen EpsilonFirmwareDoxyfile