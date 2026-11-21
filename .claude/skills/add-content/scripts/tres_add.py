"""Adds a resource to a list or a weighted dictionary in a Godot .tres file.

    python tres_add.py FILE PROPERTY RES_PATH [--weight W] [--dry-run]

Examples, run from the vnp project folder:
    python tres_add.py tuning/levels.tres Upgrades res://weapons/x/upgrades/damage.tres
    python tres_add.py scripts/cheats/cheats.tres StartingWeaponChoices res://weapons/x/x.tres
    python tres_add.py tuning/enemy_spawning.tres GroupWeights res://enemies/x/x.tres --weight 1.0

It adds an ext_resource line for RES_PATH (or reuses the one already there), raises load_steps to match,
and appends the entry to PROPERTY, which must already exist as Array[...]([...]) or
Dictionary[...]({...}). It refuses to add an entry that is already in the list, and stops without
writing anything if the file doesn't look as expected. Godot reformats the file the next time it saves
it, which is fine.
"""
import argparse
import os
import re
import sys


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("file")
    parser.add_argument("property")
    parser.add_argument("res_path")
    parser.add_argument("--weight", type=float, help="for a Dictionary property: the entry's weight")
    parser.add_argument("--dry-run", action="store_true", help="print the result instead of writing it")
    args = parser.parse_args()

    if not args.res_path.startswith("res://"):
        sys.exit("RES_PATH must be a res:// path")
    on_disk = args.res_path[len("res://"):]
    if not os.path.exists(on_disk):
        sys.exit(f"{on_disk} does not exist (run this from the project folder)")

    with open(args.file, encoding="utf-8", newline="") as f:
        text = f.read()
    nl = "\r\n" if "\r\n" in text else "\n"

    # Reuse the ext_resource for this path if there is one, otherwise add a new one after the last.
    ext_lines = list(re.finditer(r'^\[ext_resource [^\n]*\]\r?$', text, re.M))
    if not ext_lines:
        sys.exit("no ext_resource lines found")
    existing = re.search(r'^\[ext_resource [^\n]*path="' + re.escape(args.res_path) + r'"[^\n]*id="([^"]+)"', text, re.M)
    if existing:
        res_id = existing.group(1)
        added_ext = False
    else:
        numbers = [int(m.group(1)) for m in re.finditer(r'id="(\d+)_[^"]*"', text)]
        base = os.path.splitext(os.path.basename(on_disk))[0]
        parent = os.path.basename(os.path.dirname(on_disk))
        # Upgrade files are named by what they change (damage.tres), so name the ID after their owner too.
        if parent == "upgrades":
            owner = os.path.basename(os.path.dirname(os.path.dirname(on_disk)))
            name = f"{owner}_{base}"
        elif parent and base != parent:
            name = f"{parent}_{base}"
        else:
            name = base
        res_id = f"{max(numbers, default=0) + 1}_{name}"
        res_type = "Script" if on_disk.endswith((".cs", ".gd")) else "Resource"
        new_line = f'[ext_resource type="{res_type}" path="{args.res_path}" id="{res_id}"]'
        end = ext_lines[-1].end()
        text = text[:end] + nl + new_line + text[end:]
        added_ext = True

    ref = f'ExtResource("{res_id}")'

    prop = re.search(r'^' + re.escape(args.property) + r' = (Array|Dictionary)\[', text, re.M)
    if not prop:
        sys.exit(f"{args.property} is not an Array[...] or Dictionary[...] in {args.file}")
    kind = prop.group(1)
    closer = "])" if kind == "Array" else "})"
    close_at = text.find(closer, prop.end())
    if close_at < 0:
        sys.exit(f"could not find the end of {args.property}")
    body_start = text.find("([" if kind == "Array" else "({", prop.end()) + 2
    body = text[body_start:close_at]
    if ref in body:
        sys.exit(f"{args.res_path} is already in {args.property}")

    if kind == "Array":
        entry = ref if body.strip() == "" else ", " + ref
        text = text[:close_at] + entry + text[close_at:]
    else:
        if args.weight is None:
            sys.exit(f"{args.property} is a Dictionary: give the entry's value with --weight")
        stripped = body.rstrip()
        entry = f"{ref}: {args.weight}"
        new_body = (stripped + "," + nl + entry + nl) if stripped.strip() else (nl + entry + nl)
        text = text[:body_start] + new_body + text[close_at:]

    if added_ext:
        steps = re.search(r'load_steps=(\d+)', text)
        if steps:
            text = text[:steps.start(1)] + str(int(steps.group(1)) + 1) + text[steps.end(1):]

    if args.dry_run:
        print(text)
        return
    with open(args.file, "w", encoding="utf-8", newline="") as f:
        f.write(text)
    print(f"added {args.res_path} to {args.property} in {args.file} as {ref}")


if __name__ == "__main__":
    main()
