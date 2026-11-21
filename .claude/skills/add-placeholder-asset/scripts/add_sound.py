"""Registers a baked placeholder sound with the game, in audio/audio.tres and audio/Sounds.cs.

    python add_sound.py ID --category Deaths --volume 0.5 [--max-copies 4] [--vary-pitch] [--loop] [--dry-run]

Run from the vnp project folder, after the .wav exists in placeholder/audio/ (bake first). It adds:
- an ext_resource for placeholder/audio/ID.wav and a SoundDefinition sub_resource in audio.tres,
  appends it to the Sounds list, and raises load_steps by 2;
- a constant to the Sound class in Sounds.cs, named in PascalCase (goo_absorb -> GooAbsorb).

The category is a SoundCategory name from audio/SoundDefinition.cs; its number is read from the enum
there, so the order is never guessed. It refuses a sound ID that is already registered, and writes
nothing if anything looks unexpected.
"""
import argparse
import os
import re
import sys

AUDIO = "audio/audio.tres"
SOUNDS_CS = "audio/Sounds.cs"
DEFINITION_CS = "audio/SoundDefinition.cs"


def read(path):
    with open(path, encoding="utf-8", newline="") as f:
        return f.read()


def write(path, text):
    with open(path, "w", encoding="utf-8", newline="") as f:
        f.write(text)


def categories():
    text = read(DEFINITION_CS)
    body = re.search(r"enum SoundCategory\s*\{(.*?)\}", text, re.S).group(1)
    body = re.sub(r"///[^\n]*", "", body)
    names = [n.strip() for n in body.split(",") if n.strip()]
    return {name: i for i, name in enumerate(names)}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("id")
    parser.add_argument("--category", required=True)
    parser.add_argument("--volume", type=float, required=True, help="0 to 1")
    parser.add_argument("--max-copies", type=int, default=0, help="0 uses DefaultMaxCopies")
    parser.add_argument("--vary-pitch", action="store_true")
    parser.add_argument("--loop", action="store_true")
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    sound_id = args.id
    if not re.fullmatch(r"[a-z][a-z0-9_]*", sound_id):
        sys.exit("the ID must be snake_case, matching the .wav file name")
    wav = f"placeholder/audio/{sound_id}.wav"
    if not os.path.exists(wav):
        sys.exit(f"{wav} does not exist: add its recipe to PlaceholderSounds.cs and bake first")
    cats = categories()
    if args.category not in cats:
        sys.exit(f"unknown category {args.category}; one of: {', '.join(cats)}")
    if not 0 <= args.volume <= 1:
        sys.exit("volume runs from 0 to 1")

    audio = read(AUDIO)
    nl = "\r\n" if "\r\n" in audio else "\n"
    if f'Id = "{sound_id}"' in audio:
        sys.exit(f"{sound_id} is already in {AUDIO}")

    numbers = [int(m.group(1)) for m in re.finditer(r'id="(\d+)_[^"]*"', audio)]
    stream_id = f"{max(numbers) + 1}_{sound_id}"
    ext_lines = list(re.finditer(r'^\[ext_resource [^\n]*\]\r?$', audio, re.M))
    sound_script = re.search(r'path="res://audio/SoundDefinition.cs" id="([^"]+)"', audio).group(1)

    end = ext_lines[-1].end()
    audio = audio[:end] + nl + f'[ext_resource type="AudioStream" path="res://{wav}" id="{stream_id}"]' + audio[end:]

    block = nl.join([
        f'[sub_resource type="Resource" id="Sound_{sound_id}"]',
        f'script = ExtResource("{sound_script}")',
        f'Id = "{sound_id}"',
        f'Stream = ExtResource("{stream_id}")',
        f"Category = {cats[args.category]}",
        f"Volume = {args.volume}",
        f"MaxCopies = {args.max_copies}",
        f"VaryPitch = {'true' if args.vary_pitch else 'false'}",
        f"Loop = {'true' if args.loop else 'false'}",
    ]) + nl + nl
    resource_at = audio.find(nl + "[resource]" + nl)
    if resource_at < 0:
        sys.exit("no [resource] section found")
    audio = audio[:resource_at + len(nl)] + block + audio[resource_at + len(nl):]

    sounds = re.search(r'^Sounds = Array\[[^\n]*?\]\(\[', audio, re.M)
    close_at = audio.find("])", sounds.end()) if sounds else -1
    if close_at < 0:
        sys.exit("could not find the Sounds list")
    audio = audio[:close_at] + f', SubResource("Sound_{sound_id}")' + audio[close_at:]

    steps = re.search(r"load_steps=(\d+)", audio)
    audio = audio[:steps.start(1)] + str(int(steps.group(1)) + 2) + audio[steps.end(1):]

    cs = read(SOUNDS_CS)
    const_name = "".join(part.capitalize() for part in sound_id.split("_"))
    if re.search(r"\bconst string " + const_name + r"\b", cs):
        sys.exit(f"Sound.{const_name} already exists in {SOUNDS_CS}")
    consts = list(re.finditer(r'^\tpublic const string \w+ = "[^"]+";\r?$', cs, re.M))
    if not consts:
        sys.exit(f"no Sound constants found in {SOUNDS_CS}")
    cs_nl = "\r\n" if "\r\n" in cs else "\n"
    at = consts[-1].end()
    cs = cs[:at] + cs_nl + f'\tpublic const string {const_name} = "{sound_id}";' + cs[at:]

    if args.dry_run:
        print(block)
        print(f'Sound.{const_name} = "{sound_id}"')
        return
    write(AUDIO, audio)
    write(SOUNDS_CS, cs)
    print(f"registered {sound_id}: Sound.{const_name}, category {args.category}, volume {args.volume}")


if __name__ == "__main__":
    main()
