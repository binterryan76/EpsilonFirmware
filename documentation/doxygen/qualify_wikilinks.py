#!/usr/bin/env python3
r"""
qualify_wikilinks.py

Rewrites [[ClassName]] wiki-links IN YOUR ACTUAL OBSIDIAN VAULT (not a copy)
so they carry the fully-qualified name Doxygen needs, e.g.:

    [[MachineQueue]]              ->  [[EpsilonCore::Engine::MachineQueue]]
    [[MachineQueue#spin]]         ->  [[EpsilonCore::Engine::MachineQueue#spin]]
    [[MachineQueue|the queue]]    ->  [[EpsilonCore::Engine::MachineQueue|the queue]]

Links that already resolve to a real note in your vault are left completely
alone - only links that DON'T match a note, but DO match exactly one
class/struct/namespace from your source, get rewritten. Already-qualified
links (containing "::"), same-file "[[#Header]]" links, and embeds
("![[...]]") are left alone too, so this is safe to re-run any time your
code changes - it will only ever touch links that are still unqualified.

Run this ONCE (or whenever your class names change), ahead of
generate_markdown_nav.py, so that by the time that script builds the
Doxygen-ready staging copy, [[...]] links are already unambiguous.

SAFETY: this script modifies markdown files in your vault in place.
  - By default it only PRINTS what it would change (dry run). Nothing is
    written unless you pass --write.
  - When --write is used, it backs up the entire vault to a sibling
    directory first (e.g. "obsidian_backup_20260822_101500"), unless you
    pass --no-backup.
  - It only ever rewrites the text inside [[...]] that it can positively
    match to exactly one class - anything ambiguous, unmatched, or already
    a valid note link is left completely untouched.

Usage:
    python3 qualify_wikilinks.py ../obsidian --code-dir ../src --write
    python3 qualify_wikilinks.py ../obsidian --code-dir ../src --filter-pattern "*.cs=python3 cs_record_filter.py" --write
"""
import argparse
import datetime
import shutil
import sys
from pathlib import Path

from generate_markdown_nav import (
    SKIP_DIR_NAMES,
    WIKILINK_RE,
    build_symbol_maps,
    parse_wikilink,
    resolve_class_name,
)


def die(msg: str):
    print(f"ERROR: {msg}", file=sys.stderr)
    sys.exit(1)


def collect_note_names(vault: Path):
    """
    Every existing note's filename (lowercase stem) and vault-relative path
    (lowercase, no extension) - so a [[...]] link that legitimately refers
    to a real note is never mistaken for a class reference.
    """
    stems = set()
    paths = set()
    for md in vault.rglob("*.md"):
        if any(part in SKIP_DIR_NAMES for part in md.relative_to(vault).parts):
            continue
        stems.add(md.stem.lower())
        paths.add(md.relative_to(vault).with_suffix("").as_posix().lower())
    return stems, paths


def qualify_text(text: str, rel_name: str, note_stems: set, note_paths: set,
                  short_name_map: dict, qualified_name_map: dict,
                  changes: list, warnings: list) -> str:
    def repl(match):
        note_part, header_part, alias = parse_wikilink(match.group(1))

        if not note_part:
            return match.group(0)  # [[#Header]] - same-file link, not a class reference

        if "::" in note_part:
            return match.group(0)  # already qualified - idempotent, leave alone

        ref = note_part.lower()
        if ref.endswith(".md"):
            ref = ref[:-3]
        if ref in note_paths or ref.rsplit("/", 1)[-1] in note_stems:
            return match.group(0)  # a real note - leave it as a note link, not a class

        qualified, ambiguous = resolve_class_name(note_part, short_name_map, qualified_name_map)
        if ambiguous:
            warnings.append(
                f'{rel_name}: "{note_part}" matches more than one symbol '
                f"({', '.join(ambiguous)}) - left as-is, qualify it yourself, e.g. "
                f"[[{ambiguous[0]}]]"
            )
            return match.group(0)
        if not qualified:
            return match.group(0)  # not a note, not a known class either - leave alone

        display = alias or (f"{note_part}.{header_part}" if header_part else note_part)

        new_inner = qualified
        if header_part:
            new_inner += f"#{header_part}"
        new_inner += f"|{display}"

        old_display = note_part + (f"#{header_part}" if header_part else "")
        changes.append(f"{rel_name}: [[{old_display}]] -> [[{new_inner}]]")
        return f"[[{new_inner}]]"

    return WIKILINK_RE.sub(repl, text)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("vault", metavar="VAULT_DIR", help="Path to your actual Obsidian vault (modified in place with --write)")
    ap.add_argument("--code-dir", action="append", default=[], metavar="DIR", required=True,
                     help="Source directory to scan for classes/structs/namespaces. Repeatable.")
    ap.add_argument("--filter-pattern", action="append", default=[], metavar="PATTERN",
                     help='Doxygen FILTER_PATTERNS entry, e.g. "*.cs=python3 cs_record_filter.py". Repeatable.')
    ap.add_argument("--write", action="store_true",
                     help="Actually modify files in the vault. Without this flag, only a dry-run report is printed.")
    ap.add_argument("--no-backup", action="store_true",
                     help="Skip the automatic vault backup before writing. Not recommended.")
    args = ap.parse_args()

    vault = Path(args.vault).resolve()
    print(f"vault: {args.vault}  ->  {vault}")
    if not vault.is_dir():
        die(f"vault directory not found: {vault}")

    warnings = []
    short_name_map, qualified_name_map = build_symbol_maps(args.code_dir, args.filter_pattern, warnings)
    if not short_name_map and not qualified_name_map:
        print("WARNING: no classes/structs/namespaces were found at all - check --code-dir, "
              "--filter-pattern, and that 'doxygen' is on PATH. Nothing will be qualified.",
              file=sys.stderr)

    note_stems, note_paths = collect_note_names(vault)

    md_files = sorted(
        p for p in vault.rglob("*.md")
        if not any(part in SKIP_DIR_NAMES for part in p.relative_to(vault).parts)
    )

    changes = []
    rewritten = {}  # md_path -> new_text
    for md in md_files:
        original = md.read_text(encoding="utf-8", errors="replace")
        new_text = qualify_text(original, md.name, note_stems, note_paths,
                                 short_name_map, qualified_name_map, changes, warnings)
        if new_text != original:
            rewritten[md] = new_text

    print(f"\nScanned {len(md_files)} notes; {len(rewritten)} file(s) have links to qualify.")
    for c in changes:
        print(f"  {c}")

    if warnings:
        print(f"\n{len(warnings)} warning(s):")
        for w in warnings:
            print(f"  - {w}")

    if not args.write:
        print("\nDRY RUN - no files were changed. Re-run with --write to apply these changes.")
        return

    if not rewritten:
        print("\nNothing to write.")
        return

    if not args.no_backup:
        stamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
        backup_dir = vault.parent / f"{vault.name}_backup_{stamp}"
        print(f"\nBacking up vault to {backup_dir} before writing...")
        shutil.copytree(vault, backup_dir, ignore=shutil.ignore_patterns(*SKIP_DIR_NAMES))
        print("Backup complete.")

    for md, new_text in rewritten.items():
        md.write_text(new_text, encoding="utf-8")

    print(f"\nWrote changes to {len(rewritten)} file(s).")


if __name__ == "__main__":
    main()
