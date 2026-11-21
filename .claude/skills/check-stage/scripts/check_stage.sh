#!/usr/bin/env bash
# Runs every mechanical check on the VNP project and prints a summary at the end.
#
#   bash check_stage.sh                                  # the full check
#   bash check_stage.sh --scenario [--window] ARGS...   # only a fight set up by the scenario harness
#
# --scenario runs res://scripts/cheats/scenario.tscn with its arguments (see ScenarioRunner.cs), such as
#   bash check_stage.sh --scenario spawn=goo:4 drones=40 invulnerable frames=900
#   bash check_stage.sh --scenario --window spawn=all:2 shot=C:/path/shot.png@300
# It runs headless unless --window is given; a screenshot needs --window.
#
# GODOT can be set to another editor binary; the default is the project's source build.
# It never commits, reverts, or deletes project files. It only removes the temporary copy of its own
# .gd script that it puts in the project folder to run it.

SKILL_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT="$(cd "$SKILL_DIR/../../../.." && pwd)"
GODOT="${GODOT:-/f/GameDev/godot/bin/godot.windows.editor.x86_64.mono.console.exe}"
cd "$PROJECT" || exit 2

run_temp_script() {
	# Copies one of the skill's .gd scripts into the project, runs it, and removes it again.
	local name="$1"; shift
	cp "$SKILL_DIR/$name" "$PROJECT/_$name"
	"$@" "res://_$name"
	local status=$?
	rm -f "$PROJECT/_$name" "$PROJECT/_$name.uid"
	return $status
}

# A headless run reports the sounds that played as leaked at exit, because its dummy audio driver never
# finishes them. A windowed run of the same fight exits clean, so these two lines are not problems.
KNOWN_EXIT_NOISE='ObjectDB instances were leaked at exit|resources still in use at exit|ObjectDB::cleanup|ResourceCache::clear'

run_scenario() {
	# Runs the scenario harness. The first argument is "window" or "headless"; the rest go to the harness.
	local mode="$1"; shift
	local display=(--headless)
	[ "$mode" = "window" ] && display=()
	timeout 600 "$GODOT" "${display[@]}" --path . --fixed-fps 60 res://scripts/cheats/scenario.tscn -- "$@" 2>&1 \
		| grep -vE "^Godot Engine|^$" | grep -vE "$KNOWN_EXIT_NOISE"
}

if [ "$1" = "--scenario" ]; then
	shift
	mode=headless
	if [ "$1" = "--window" ]; then mode=window; shift; fi
	dotnet build VNP.sln -nologo -v q > /dev/null 2>&1 || { echo "Debug build failed; fix it first."; exit 1; }
	run_scenario "$mode" "$@"
	exit 0
fi

summary=()
problems=0
note() { summary+=("$1"); }
fail() { summary+=("PROBLEM: $1"); problems=$((problems + 1)); }

echo "== Builds"
# Export configurations first and Debug last, so the editor is left with the Debug build it runs.
for config in ExportDebug ExportRelease Debug; do
	output="$(dotnet build VNP.sln -c "$config" -nologo 2>&1)"
	errors="$(echo "$output" | grep -E ": error " | sort -u)"
	warnings="$(echo "$output" | grep -E ": warning " | sort -u | wc -l)"
	if [ -n "$errors" ]; then
		echo "$errors" | head -20
		fail "$config build has errors"
	else
		note "$config build: OK ($warnings warnings)"
	fi
	[ "$warnings" -gt 0 ] && echo "$output" | grep -E ": warning " | sort -u | head -10
done

echo "== Editor rescan (makes .uid files for new scripts)"
timeout 300 "$GODOT" --headless --path . --editor --quit > /dev/null 2>&1
new_uids="$(git status --porcelain -uall | grep -E '^\?\? .*\.uid$' | sed 's/^?? //')"
if [ -n "$new_uids" ]; then
	note "New .uid files to commit with their scripts: $(echo $new_uids | tr '\n' ' ')"
fi
orphans=""
for uid in $(git ls-files --others --cached '*.uid'); do
	[ -e "${uid%.uid}" ] || orphans+="$uid "
done
[ -n "$orphans" ] && fail ".uid files whose script is gone (moved without its .uid?): $orphans"

echo "== Data files"
data="$(run_temp_script check_data.gd timeout 120 "$GODOT" --headless --path . -s 2>&1)"
echo "$data" | grep -vE "^Godot Engine|^$"
if echo "$data" | grep -q "DATA CHECK: OK"; then
	note "Data check: OK. $(echo "$data" | grep -E '^upgrade pool|^sounds' | tr '\n' ' ')"
else
	fail "data check (see the lines above)"
fi

echo "== Headless fight, 30 seconds, with every enemy type"
# Through the scenario harness: a plain run would sit on the paused starting weapon menu and test nothing.
# It also writes a fight log, to a folder of its own that is removed afterwards, and checks the log adds up.
rm -rf playtests/check
fight="$(run_scenario headless spawn=all:2 invulnerable frames=1800 report=600 seed=1 log=res://playtests/check)"
echo "$fight" | grep -E "^SCENARIO|^t |^TIMING"
fight_errors="$(echo "$fight" | grep -iE "error|exception" | sort | uniq -c | head -20)"
if ! echo "$fight" | grep -q "^TIMING"; then
	echo "$fight" | tail -20
	fail "the fight did not finish (crashed, hung, or the harness failed)"
elif [ -n "$fight_errors" ]; then
	echo "$fight_errors"
	fail "the fight logged errors"
else
	note "Headless fight: no errors. $(echo "$fight" | grep '^TIMING' | sed 's/ (headless.*//')"
fi
log_file="$(ls playtests/check/*.json 2>/dev/null | head -1)"
if [ -z "$log_file" ]; then
	fail "the fight wrote no fight log"
else
	log_check="$(python "$SKILL_DIR/check_log.py" "$log_file" 2>&1)"
	if echo "$log_check" | grep -q "^FAIL\|Traceback"; then
		echo "$log_check" | grep -E "^FAIL|Traceback|Error"
		fail "the fight log has inconsistencies (see above)"
	else
		note "Fight log: consistent ($(echo "$log_check" | grep -o '[0-9]* checks passed'))"
	fi
fi
rm -rf playtests/check
fight_warnings="$(echo "$fight" | grep -E "^WARNING" | sort | uniq -c | head -10)"
[ -n "$fight_warnings" ] && { echo "$fight_warnings"; note "Fight warnings (check whether they are new): $(echo "$fight_warnings" | wc -l)"; }

echo "== Cheats kept out of export builds"
# Top-level classes only: nested ones go with their parent, and their short names (such as Page)
# turn up in other code too.
cheat_classes="$(grep -hoE "^(public |internal )?(static |sealed |abstract |partial )*class [A-Za-z0-9_]+" scripts/cheats/*.cs 2>/dev/null | awk '{print $NF}' | sort -u)"
for config in ExportDebug ExportRelease; do
	dll="$(find .godot/mono/temp/bin/$config -name 'VNP.dll' 2>/dev/null | head -1)"
	if [ -z "$dll" ]; then
		fail "no $config VNP.dll found to check"
		continue
	fi
	leaked=""
	for class in $cheat_classes; do
		# Type names are stored in the assembly's metadata as text ending in a zero byte. Matching the
		# whole name stops a longer name, such as Fight's StartingWeaponMenuOpen stub, counting as a leak.
		LC_ALL=C grep -aqP "$class\x00" "$dll" && leaked+="$class "
	done
	if [ -n "$leaked" ]; then
		fail "$config build contains cheat code: $leaked"
	else
		note "$config: no cheat classes ($(echo $cheat_classes | wc -w) checked)"
	fi
done

echo "== Working tree"
# Judged by file name only, so a folder such as playtesting/ doesn't make everything in it look temporary.
leftovers="$(git status --porcelain -uall | grep -E '^\?\? ' | sed 's/^?? //' | grep -v '^\.claude/' \
	| while read -r file; do name="${file##*/}"; echo "$name" | grep -qiE '^_|check|test|tmp|scratch|\.orig$' && echo "$file"; done)"
[ -n "$leftovers" ] && note "Untracked files that look temporary, check they belong: $(echo $leftovers | tr '\n' ' ')"
if git status --porcelain | grep -q 'placeholder/models/'; then
	if git diff placeholder/models | grep -E '^[+-][^+-]' | grep -vqE 'unique_id='; then
		note "placeholder/models changed beyond node IDs: check the change is intended"
	else
		note "placeholder/models changed only by node IDs (a rebake does this): revert them with git checkout"
	fi
fi
note "Changed files: $(git status --porcelain | wc -l)"

echo
echo "================ SUMMARY ================"
printf '%s\n' "${summary[@]}"
echo "Mechanical checks: $([ $problems -eq 0 ] && echo "PASSED" || echo "$problems PROBLEM(S)")"
exit $([ $problems -eq 0 ] && echo 0 || echo 1)
