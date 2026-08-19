#!/usr/bin/env python3
r"""
generate_markdown_nav.py

Stages a copy of a markdown vault (e.g. an Obsidian notebook) and prepares
it for Doxygen so that the folder structure becomes a nested Doxygen page
tree (Doxygen has no built-in "auto-nest markdown pages by folder" feature,
so this fills that gap):

  * Copies the vault into a staging directory (the original is never touched).
  * Gives every .md file a stable Doxygen page id via an anchored heading
    ("# Title {#id}"). Files that already have one are left alone.
  * Writes one auto-generated "index.md" per folder that \subpages its
    child files and subfolders, so opening the folder's page shows a link
    tree of everything inside it.
  * The vault root becomes a single page with id "md_root" (customizable),
    which is the one entry you point the "Markdown" nav tab at.

Usage:
    python generate_markdown_nav.py <vault_dir> <staging_dir> [--root-title "Markdown"] [--root-id md_root]

Then in your Doxyfile:
    INPUT             += <staging_dir>
    FILE_PATTERNS     += *.md
    RECURSIVE          = YES
    MARKDOWN_SUPPORT    = YES

Re-run this script any time the vault changes, then re-run doxygen.
"""
import argparse
import re
import shutil
import sys
from pathlib import Path

SKIP_DIR_NAMES = {".obsidian", ".git", ".trash", ".smart-connections", "node_modules"}

# Dropped in the root of every staging dir this script creates. Its presence
# is the ONLY thing that makes the script willing to delete a directory - if
# it's missing, the target is treated as foreign and the script refuses to
# touch it, no matter what the path looks like.
MARKER_NAME = ".generated_by_generate_markdown_nav"

# Matches an ATX H1, optionally already carrying a Doxygen anchor: "# Title {#id}"
HEADING_RE = re.compile(r'^#\s+(?P<title>.*?)(\s*\{#(?P<id>[A-Za-z0-9_\-]+)\})?\s*$')

# Matches any ATX heading level 1-6, optionally already anchored.
HEADING_ANY_RE = re.compile(r'^(?P<hashes>#{1,6})\s+(?P<title>.*?)(\s*\{#(?P<id>[A-Za-z0-9_\-]+)\})?\s*$')

# Obsidian wiki-links: [[Note]], [[Note|Alias]], [[Note#Header]], [[#Header]], etc.
# The (?<!!) keeps this from matching image/note embeds, which start with "![[".
WIKILINK_RE = re.compile(r'(?<!!)\[\[([^\[\]]+)\]\]')


def slugify_text(text: str) -> str:
    s = text.strip().lower()
    s = re.sub(r"[^a-z0-9]+", "_", s)
    s = re.sub(r"_+", "_", s).strip("_")
    return s or "section"


def normalize_heading(text: str) -> str:
    return re.sub(r"\s+", " ", text.strip()).lower()


def slugify(rel_parts, prefix="md"):
    raw = "_".join(rel_parts)
    raw = re.sub(r"[^A-Za-z0-9_]+", "_", raw)
    raw = re.sub(r"_+", "_", raw).strip("_").lower()
    return f"{prefix}_{raw}" if raw else prefix


def title_from_stem(name: str) -> str:
    return name.replace("_", " ").replace("-", " ").strip() or name


def is_skipped(path: Path, staging_root: Path) -> bool:
    return any(part in SKIP_DIR_NAMES for part in path.relative_to(staging_root).parts)


def ensure_page_id(md_path: Path, desired_id: str, filename_title: str) -> tuple[str, str]:
    """
    Make sure the file has a Doxygen-titled H1 matching its filename, so the
    nav tab shows the note's name the way Obsidian does - not whatever the
    body's first heading happens to say.

    - If the first heading already carries an explicit {#id} anchor, that's
      treated as an intentional manual override and left untouched.
    - If the first heading's text already matches the filename, it's just
      anchored in place (no duplicate heading introduced).
    - Otherwise, a new "# <filename> {#id}" title is inserted, and any
      existing untagged H1 is demoted to "##" so it becomes a subsection
      of the new page title instead of competing with it.
    - If there's no leading heading at all, one is simply inserted.
    """
    text = md_path.read_text(encoding="utf-8", errors="replace")
    lines = text.splitlines()

    idx = 0
    while idx < len(lines) and lines[idx].strip() == "":
        idx += 1

    if idx < len(lines):
        m = HEADING_RE.match(lines[idx])
        if m:
            existing_title = m.group("title").strip()
            existing_id = m.group("id")

            if existing_id:
                return existing_id, existing_title  # manual override - leave as-is

            if existing_title.lower() == filename_title.lower():
                lines[idx] = f"# {existing_title} {{#{desired_id}}}"
                md_path.write_text("\n".join(lines) + "\n", encoding="utf-8")
                return desired_id, existing_title

            # Demote the existing H1 into a subsection, insert the real title above it
            lines[idx] = f"## {existing_title}"
            new_lines = [f"# {filename_title} {{#{desired_id}}}", ""] + lines
            md_path.write_text("\n".join(new_lines) + "\n", encoding="utf-8")
            return desired_id, filename_title

    # No heading found at the top of the file - insert one.
    new_lines = [f"# {filename_title} {{#{desired_id}}}", ""] + lines
    md_path.write_text("\n".join(new_lines) + "\n", encoding="utf-8")
    return desired_id, filename_title


def die(msg: str):
    print(f"ERROR: {msg}", file=sys.stderr)
    sys.exit(1)


def assign_heading_anchors(md_path: Path, page_id: str, dup_warnings: list) -> dict:
    """
    Walk every heading (H1-H6) in the file and make sure it has a Doxygen
    anchor, inserting one derived from the page id + heading text where
    missing. Returns {normalized_heading_text: anchor_id} for this file,
    used later to resolve both same-file and cross-file [[...]] links.

    If a page has two headings with the same text, [[#Heading]] is
    inherently ambiguous - the FIRST one found wins (matches how in-page
    anchors are conventionally expected to behave), and it's reported via
    dup_warnings rather than silently resolved.
    """
    text = md_path.read_text(encoding="utf-8", errors="replace")
    lines = text.splitlines()

    used_ids = set()
    headings = {}
    changed = False

    for i, line in enumerate(lines):
        m = HEADING_ANY_RE.match(line)
        if not m:
            continue
        title = m.group("title").strip()
        existing_id = m.group("id")

        if existing_id:
            anchor = existing_id
        else:
            base = f"{page_id}_{slugify_text(title)}" if title else page_id
            anchor = base
            n = 2
            while anchor in used_ids:
                anchor = f"{base}_{n}"
                n += 1
            lines[i] = f"{m.group('hashes')} {title} {{#{anchor}}}"
            changed = True

        used_ids.add(anchor)
        if title:
            norm = normalize_heading(title)
            if norm in headings:
                dup_warnings.append(
                    f'{md_path.name}: heading "{title}" appears more than once - '
                    f"[[#{title}]] links resolve to the first one"
                )
            else:
                headings[norm] = anchor

    if changed:
        md_path.write_text("\n".join(lines) + "\n", encoding="utf-8")

    return headings


def parse_wikilink(inner: str):
    """Split the inside of [[...]] into (note_part, header_part, alias)."""
    if "|" in inner:
        target, alias = inner.split("|", 1)
    else:
        target, alias = inner, None
    if "#" in target:
        note_part, header_part = target.split("#", 1)
    else:
        note_part, header_part = target, None
    return (
        note_part.strip(),
        header_part.strip() if header_part else None,
        alias.strip() if alias else None,
    )


def rewrite_wikilinks(md_path: Path, page_id: str, filename_to_pageid: dict, path_to_pageid: dict,
                       page_headings: dict, warnings: list):
    """Replace Obsidian [[...]] links with Doxygen [text](@ref anchor) links."""
    text = md_path.read_text(encoding="utf-8", errors="replace")
    rel = md_path.name

    def repl(match: re.Match) -> str:
        note_part, header_part, alias = parse_wikilink(match.group(1))

        if note_part:
            ref = note_part.lower()
            if ref.endswith(".md"):
                ref = ref[:-3]
            # Try an exact vault-relative path match first (handles [[Folder/Note]]),
            # then fall back to matching by bare filename (the common case).
            target_page_id = path_to_pageid.get(ref) or filename_to_pageid.get(ref.rsplit("/", 1)[-1])
            if not target_page_id:
                warnings.append(f"{rel}: couldn't resolve link to note \"{note_part}\" - left as-is")
                return match.group(0)
        else:
            target_page_id = page_id  # same-file link, e.g. [[#Header]]

        anchor = target_page_id
        if header_part:
            found = page_headings.get(target_page_id, {}).get(normalize_heading(header_part))
            if found:
                anchor = found
            else:
                where = f'"{note_part}"' if note_part else "this note"
                warnings.append(
                    f'{rel}: couldn\'t resolve header "{header_part}" in {where} - linking to the page instead'
                )

        if alias:
            display = alias
        elif header_part and note_part:
            display = f"{note_part} > {header_part}"
        elif header_part:
            display = header_part
        else:
            display = note_part

        return f"[{display}](@ref {anchor})"

    new_text = WIKILINK_RE.sub(repl, text)
    if new_text != text:
        md_path.write_text(new_text, encoding="utf-8")


def check_safe_to_write(vault: Path, staging: Path):
    if vault == staging:
        die(f"vault and staging resolve to the same path ({vault}). Refusing to run.")

    if staging in vault.parents or vault in staging.parents or vault == staging:
        die(
            f"staging ({staging}) and vault ({vault}) are nested inside one "
            "another. Refusing to run - pick a staging directory that is "
            "completely separate from the vault."
        )

    if staging.exists():
        marker = staging / MARKER_NAME
        if not marker.exists():
            die(
                f"staging directory ({staging}) already exists and was NOT "
                "created by this script (no marker file found). Refusing to "
                "delete or overwrite it - point --staging at an empty or "
                "not-yet-existing directory."
            )


def build(vault: Path, staging: Path, root_title: str, root_id: str):
    if not vault.exists():
        die(f"vault directory does not exist: {vault}")

    check_safe_to_write(vault, staging)

    if staging.exists():
        shutil.rmtree(staging)  # safe: check_safe_to_write() confirmed our marker is present
    shutil.copytree(vault, staging, ignore=shutil.ignore_patterns(*SKIP_DIR_NAMES))
    (staging / MARKER_NAME).write_text(
        "This directory is generated output from generate_markdown_nav.py.\n"
        "It is safe to delete. Do not store anything here you want to keep -\n"
        "it gets wiped and regenerated every run.\n"
    )

    all_dirs = [staging] + sorted(
        p for p in staging.rglob("*") if p.is_dir() and not is_skipped(p, staging)
    )
    children = {d: [] for d in all_dirs}  # dir -> list of (sort_key, id, title, is_dir)

    md_files = sorted(
        p for p in staging.rglob("*.md")
        if p.is_file() and not is_skipped(p.parent, staging)
    )

    dir_ids = {staging: root_id}
    dir_titles = {staging: root_title}

    def dir_id_for(d: Path) -> str:
        if d not in dir_ids:
            dir_ids[d] = slugify(d.relative_to(staging).parts)
        return dir_ids[d]

    for d in all_dirs:
        if d != staging:
            dir_titles.setdefault(d, title_from_stem(d.name))
            dir_id_for(d)

    page_ids = {}
    for md in md_files:
        rel = md.relative_to(staging).with_suffix("")
        desired_id = slugify(rel.parts)
        fallback_title = title_from_stem(md.stem)
        pid, title = ensure_page_id(md, desired_id, fallback_title)
        page_ids[md] = pid
        children[md.parent].append((title.lower(), pid, title, False))

    # Map note references -> page id, for resolving [[Note Name]] links.
    # filename_to_pageid keys on the bare filename (what most links use);
    # path_to_pageid keys on the vault-relative path (what Obsidian falls
    # back to when names collide). Later files win on filename collisions;
    # collisions are reported as warnings.
    filename_to_pageid = {}
    path_to_pageid = {}
    name_collisions = set()
    for md, pid in page_ids.items():
        key = md.stem.lower()
        if key in filename_to_pageid and filename_to_pageid[key] != pid:
            name_collisions.add(md.stem)
        filename_to_pageid[key] = pid
        path_to_pageid[md.relative_to(staging).with_suffix("").as_posix().lower()] = pid

    # Anchor every heading (not just the page title) so header-level links resolve.
    link_warnings = []
    page_headings = {pid: assign_heading_anchors(md, pid, link_warnings) for md, pid in page_ids.items()}

    for md, pid in page_ids.items():
        rewrite_wikilinks(md, pid, filename_to_pageid, path_to_pageid, page_headings, link_warnings)

    for d in all_dirs:
        if d == staging:
            continue
        parent = d.parent
        children.setdefault(parent, [])
        children[parent].append((dir_titles[d].lower(), dir_id_for(d), dir_titles[d], True))

    for d in all_dirs:
        pid = dir_id_for(d) if d != staging else root_id
        title = dir_titles.get(d, root_title)
        ordered = sorted(children.get(d, []), key=lambda c: (not c[3], c[0]))

        lines = [f"# {title} {{#{pid}}}", ""]
        if ordered:
            for _, cid, ctitle, _is_dir in ordered:
                lines.append(f'\\subpage {cid} "{ctitle}"')
        else:
            lines.append("*(empty folder)*")

        index_path = d / "index.md"
        if index_path.exists():
            # Don't clobber a real note the user happens to have named index.md
            index_path = d / "_folder_index.md"
        index_path.write_text("\n".join(lines) + "\n", encoding="utf-8")

    print(f"Staged {len(md_files)} markdown files under: {staging}")
    print(f"Root page id: {root_id}  (title: \"{root_title}\")")
    if name_collisions:
        print(f"NOTE: {len(name_collisions)} note name(s) exist in more than one folder "
              f"({', '.join(sorted(name_collisions))}); [[links]] to them use whichever "
              "was processed last. Consider renaming, or use [[Folder/Note]] style paths.")
    if link_warnings:
        print(f"NOTE: {len(link_warnings)} [[...]] link(s) could not be fully resolved:")
        for w in link_warnings:
            print(f"  - {w}")
    print("Point your Doxyfile's INPUT at the staging directory and re-run doxygen.")


if __name__ == "__main__":
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("vault", metavar="VAULT_DIR", help="Path to the source Obsidian vault / markdown folder")
    ap.add_argument("staging", metavar="STAGING_DIR", help="Path to write the staged, Doxygen-ready copy")
    ap.add_argument("--root-title", default="Markdown", help="Title of the root page (default: Markdown)")
    ap.add_argument("--root-id", default="md_root", help="Doxygen page id for the root page (default: md_root)")
    args = ap.parse_args()

    vault_path = Path(args.vault).resolve()
    staging_path = Path(args.staging).resolve()
    print(f"vault:   {args.vault}  ->  {vault_path}")
    print(f"staging: {args.staging}  ->  {staging_path}")

    build(vault_path, staging_path, args.root_title, args.root_id)
