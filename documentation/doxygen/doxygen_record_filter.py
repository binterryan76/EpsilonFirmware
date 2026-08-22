#!/usr/bin/env python3
"""
doxygen_record_filter.py

Doxygen INPUT_FILTER for C# source files. Doxygen's C# parser does not
understand the `record` keyword, positional/primary-constructor record
syntax, or primary constructors on ordinary classes/structs (C# 12)
(see https://github.com/doxygen/doxygen/issues/10087, open/unresolved).

This script rewrites:

    public record Point(int X, int Y);

    public record Point(int X, int Y)
    {
        public double Distance => Math.Sqrt(X * X + Y * Y);
    }

into an equivalent plain class Doxygen CAN parse:

    public class Point
    {
        public int X { get; init; }
        public int Y { get; init; }
        public Point(int X, int Y) { this.X = X; this.Y = Y; }
    }

    public class Point
    {
        public int X { get; init; }
        public int Y { get; init; }
        public Point(int X, int Y) { this.X = X; this.Y = Y; }

        public double Distance => Math.Sqrt(X * X + Y * Y);
    }

It also rewrites primary constructors on ordinary (non-record) classes
and structs, e.g.:

    public class Point(int X, int Y)
    {
        public double Distance => Math.Sqrt(X * X + Y * Y);
    }

into:

    public class Point
    {
        public Point(int X, int Y) { }

        public double Distance => Math.Sqrt(X * X + Y * Y);
    }

Note this case is deliberately handled differently from records: a plain
class's primary-constructor parameters do NOT automatically become public
properties (that's record-only behavior) - they're just constructor
parameters available inside the class body. So only the constructor
signature is synthesized, with an empty body; no properties are invented.
This keeps the documented API accurate to what the class actually exposes.

Usage (Doxyfile):
    FILTER_PATTERNS = *.cs="python doxygen_record_filter.py"

Doxygen calls this as: doxygen_record_filter.py <path-to-file>
The transformed source must be written to stdout.

Limitations: this uses regex, not a real C# parser. It correctly handles
the common cases (single-line and multi-line record/class headers, record
struct, record class, generics, base lists) but is not bulletproof
against exotic formatting. Only the record/class header is rewritten --
the body (if any) is left untouched and appended as-is.
"""

import re
import sys

# Matches a record declaration header, e.g.:
#   public sealed record Point(int X, int Y) : IShape
#   internal record struct Vector3<T>(T X, T Y, T Z)
#   public record class Named(string Name)          <- explicit "record class"
#   public record Settings { ... }                  <- non-positional record (no ctor params)
#   public record Marker;                            <- empty record, no params, no body
RECORD_HEADER_RE = re.compile(
    r"""
    (?P<modifiers>
        (?:\b(?:public|internal|protected|private|sealed|abstract|partial|static|readonly)\b\s*)*
    )
    \brecord\b\s*
    (?P<kindkw>\bstruct\b\s*|\bclass\b\s*)?
    (?P<name>\w+)
    (?P<generics><[^>{};]+>)?
    \s*
    (?:\(\s*(?P<params>[^)]*)\s*\))?
    \s*
    (?P<bases>:\s*[^{;]+)?
    \s*
    (?P<term>[{;])
    """,
    re.VERBOSE | re.DOTALL,
)


def split_params(params_str):
    """Split a parameter list on top-level commas (ignoring commas inside <>, (), []).

    Returns a list of (type, name) tuples, dropping any attributes like
    [Description("...")] prefixed on a parameter.
    """
    parts = []
    depth = 0
    current = []
    for ch in params_str:
        if ch in "<([":
            depth += 1
        elif ch in ">)]":
            depth -= 1
        if ch == "," and depth == 0:
            parts.append("".join(current).strip())
            current = []
        else:
            current.append(ch)
    if "".join(current).strip():
        parts.append("".join(current).strip())

    result = []
    for p in parts:
        if not p:
            continue
        # Strip leading attributes like [Description("First Name")]
        p = re.sub(r"^\s*(\[[^\]]*\]\s*)+", "", p).strip()
        # Strip default values: "int X = 0" -> keep default for ctor param
        # Split into type + name (+ optional default)
        m = re.match(r"^(?P<type>.+?)\s+(?P<pname>\w+)(?P<default>\s*=\s*.+)?$", p)
        if m:
            result.append(
                (m.group("type").strip(), m.group("pname").strip(), (m.group("default") or "").strip())
            )
    return result


def rewrite_record(match):
    modifiers = match.group("modifiers")
    kindkw = (match.group("kindkw") or "").strip()
    name = match.group("name")
    generics = match.group("generics") or ""
    params_str = match.group("params")  # None if record had no (...) at all
    bases = match.group("bases") or ""
    term = match.group("term")

    kind = "struct" if kindkw == "struct" else "class"
    params = split_params(params_str) if params_str is not None else []

    prop_lines = []
    ctor_assigns = []
    ctor_params = []
    for ptype, pname, default in params:
        prop_lines.append(f"    public {ptype} {pname} {{ get; init; }}")
        ctor_params.append(f"{ptype} {pname}{(' ' + default) if default else ''}")
        ctor_assigns.append(f"this.{pname} = {pname};")

    header = f"{modifiers}{kind} {name}{generics} {bases}".rstrip()

    # Build class body prefix (properties + generated constructor)
    body_prefix_lines = []
    if prop_lines:
        body_prefix_lines.extend(prop_lines)
        body_prefix_lines.append("")
        ctor_sig = ", ".join(ctor_params)
        ctor_body = " ".join(ctor_assigns)
        body_prefix_lines.append(f"    public {name}({ctor_sig}) {{ {ctor_body} }}")
        body_prefix_lines.append("")

    body_prefix = "\n".join(body_prefix_lines)

    if term == ";":
        # e.g. `public record Point(int X, int Y);` -> full class with braces
        return f"{header}\n{{\n{body_prefix}}}"
    else:
        # term == '{' : keep the opening brace, inject generated members right after it
        return f"{header}\n{{\n{body_prefix}"


# Matches a primary constructor on an ORDINARY (non-record) class/struct, e.g.:
#   public class Point(int X, int Y)
#   internal struct Vector3<T>(T X, T Y, T Z) : IVector
# Deliberately requires the "(...)" to be present (unlike RECORD_HEADER_RE,
# where it's optional) - a class/struct with no parens after its name is
# already something Doxygen parses fine, and must be left completely alone.
CLASS_PRIMARY_CTOR_RE = re.compile(
    r"""
    (?P<modifiers>
        (?:\b(?:public|internal|protected|private|sealed|abstract|partial|static|readonly)\b\s*)*
    )
    \b(?P<kindkw>class|struct)\b\s+
    (?P<name>\w+)
    (?P<generics><[^>{};]+>)?
    \s*
    \(\s*(?P<params>[^)]*)\s*\)
    \s*
    (?P<bases>:\s*[^{;]+)?
    \s*
    (?P<term>[{;])
    """,
    re.VERBOSE | re.DOTALL,
)


def rewrite_class_primary_ctor(match):
    modifiers = match.group("modifiers")
    kindkw = match.group("kindkw")
    name = match.group("name")
    generics = match.group("generics") or ""
    params_str = match.group("params").strip()
    bases = match.group("bases") or ""
    term = match.group("term")

    header = f"{modifiers}{kindkw} {name}{generics} {bases}".rstrip()
    # Unlike a record, primary-constructor parameters on a plain class/struct
    # do NOT become public properties - they're only constructor parameters.
    # So only the constructor signature is synthesized; the original
    # parameter list (including any attributes/defaults) is passed through
    # verbatim rather than split apart, since nothing needs to be turned
    # into separate property declarations here.
    ctor_line = f"    public {name}({params_str}) {{ }}"

    if term == ";":
        # Not valid C# for a plain class in practice, but handled for
        # symmetry with the record case rather than risking a corrupt rewrite.
        return f"{header}\n{{\n{ctor_line}\n}}"
    else:
        return f"{header}\n{{\n{ctor_line}\n"


def filter_source(text):
    text = RECORD_HEADER_RE.sub(rewrite_record, text)
    text = CLASS_PRIMARY_CTOR_RE.sub(rewrite_class_primary_ctor, text)
    return text


def main():
    # Force UTF-8 on stdout/stderr regardless of the Windows console's
    # active codepage (often cp1252), otherwise any non-ASCII character
    # in the source (Greek letters, symbols, accented names, etc.) raises
    # UnicodeEncodeError when this script's output is piped to Doxygen.
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

    if len(sys.argv) < 2:
        sys.stderr.write("usage: doxygen_record_filter.py <file>\n")
        sys.exit(1)

    path = sys.argv[1]
    with open(path, "r", encoding="utf-8-sig", errors="replace") as f:
        text = f.read()

    sys.stdout.write(filter_source(text))


if __name__ == "__main__":
    main()
