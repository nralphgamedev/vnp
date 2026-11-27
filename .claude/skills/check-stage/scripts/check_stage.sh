#!/usr/bin/env bash
# Runs every mechanical check on the VNP project and prints a summary at the end.
#
#   bash check_stage.sh                                  # every check
#   bash check_stage.sh --full                           # every check, plus the full recording check and the
#                                                        # performance tests: run just before each push
#   bash check_stage.sh --full --crowd                   # also measures the tenfold crowd (report only)
#   bash check_stage.sh --scenario [--window] ARGS...   # only a fight set up by the scenario harness
#
# --scenario runs res://scripts/cheats/scenario.tscn with its arguments (see ScenarioRunner.cs), such as
#   bash check_stage.sh --scenario spawn=basic_goo:4 drones=40 invulnerable frames=900
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
	# The harness's exit code, not the filters': 2 means its arguments couldn't be used.
	return "${PIPESTATUS[0]}"
}

full=0
crowd=0
while [ "$1" = "--full" ] || [ "$1" = "--crowd" ]; do
	[ "$1" = "--full" ] && full=1
	[ "$1" = "--crowd" ] && crowd=1
	shift
done

if [ "$1" = "--scenario" ]; then
	shift
	mode=headless
	if [ "$1" = "--window" ]; then mode=window; shift; fi
	dotnet build VNP.sln -nologo -v q > /dev/null 2>&1 || { echo "Debug build failed; fix it first."; exit 1; }
	run_scenario "$mode" "$@"
	exit $?
fi

# Every run's whole output is kept, so a failure can be read in full afterwards, however little of it was shown
# at the time: playtests/checks/check_<date>.txt (the last 30 runs) and latest.txt. playtests/ is out of git.
checks_dir="playtests/checks"
mkdir -p "$checks_dir"
check_log="$checks_dir/check_$(date +%Y-%m-%d_%H%M%S).txt"
exec > >(tee "$check_log" "$checks_dir/latest.txt") 2>&1
ls -t "$checks_dir"/check_*.txt 2>/dev/null | tail -n +31 | xargs -r rm -f
echo "Full output: $check_log (and $checks_dir/latest.txt)"

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

echo "== Arbiter on its own"
# The pilots' decision library builds and passes its own checks with no game (arbiter/checks/), which also
# proves it doesn't depend on VNP's code or Godot's.
arbiter_out="$(dotnet run --project arbiter/checks/ArbiterChecks.csproj -nologo 2>&1)"
if [ $? -eq 0 ]; then
	note "Arbiter on its own: $(echo "$arbiter_out" | grep -c '^PASS') checks passed"
else
	echo "$arbiter_out" | grep -E "FAIL|error" | head -20
	fail "Arbiter's own checks failed (arbiter/checks/)"
fi
# No game words in Arbiter's code or training tools, so a change made for one VNP need can't quietly tie the
# library to VNP (docs/dev/PILOT_SPEC.md, Staying general). Generic words that happen to be VNP's too, such as
# "mine" or "weapon", are allowed; VNP's own names aren't. The README may name VNP, as where it came from.
game_words='vnp|drones?|goos?|grey goo|asteroids?|debris|harvest[a-z]*|lancers?|quillers?|spikers?|bursters?|devourers?|mimics?|shockwave|seeker|scatter shot|orbital|shards?|pulse laser|mine layer|pilots?|godot'
word_hits="$(grep -rniwE "$game_words" --include='*.cs' --include='*.py' arbiter 2>/dev/null | grep -v '/bin/\|/obj/')"
if [ -n "$word_hits" ]; then
	echo "$word_hits" | head -10
	fail "Arbiter's code names VNP's things (see above); keep game words in the game's code (arbiter/README.md)"
else
	note "Arbiter's words: none of VNP's in its code or training tools"
fi

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

echo "== Headless fights, all at once"
# Every check below plays its fights headless on a fixed time step, so they don't depend on each other or on how
# busy the computer is, and they all start together here; each is then judged in turn. The performance tests,
# which do depend on the computer being otherwise idle, run alone afterwards.
work="$(mktemp -d)"
rm -rf playtests/check playtests/check_repeat

# The headless fight: through the scenario harness, since a plain run would sit on the paused starting weapon
# menu and test nothing. It writes a fight log, to a folder of its own that is removed afterwards.
run_scenario headless spawn=all:2 invulnerable frames=1800 report=600 seed=1 log=res://playtests/check maps=yes strip=20 \
	> "$work/fight.txt" &

# Repeatability: two copies of the same pilot fight print the fight's exact state every half second; any
# difference means something outside the seed changes how a fight plays. Past causes: the camera shake and the
# window size moving where enemies spawn. One copy writes a fight log, which also runs the run review's
# detectors, so anything the log or a detector does that changes the fight is caught too.
repeat_args=(seed=7 pilot=average build=greedy frames=5400 report=100000 trace=30)
run_scenario headless "${repeat_args[@]}" log=none | grep "^TRACE" > "$work/repeat_a.txt" &
run_scenario headless "${repeat_args[@]}" log=res://playtests/check_repeat | grep "^TRACE" > "$work/repeat_b.txt" &

# Clean quit: a full pilot fight that wins, carries on into endless and ends, which must quit cleanly. A long
# fight that ends leaves the most garbage for .NET to clean up, and quitting once crashed with it (see
# ShutdownCleanup.cs). Exit code 0 is a clean quit; 139 was that crash.
( timeout 600 "$GODOT" --headless --path . --fixed-fps 60 res://scripts/cheats/scenario.tscn -- \
	seed=3 pilot=average build=greedy frames=14400 report=100000 log=none > "$work/exit.txt" 2>&1
  echo $? > "$work/exit_code.txt" ) &

# The quick batch: a few short pilot fights through the batch command. It skips its own build, since the
# builds above have just made it and other fights are running from it.
( python tools/batches/batch.py --size quick --builds greedy --cap 60 --name check-stage --no-build > "$work/batch.txt" 2>&1
  echo $? > "$work/batch_code.txt" ) &

# Recording covers every weapon: one short recorded fight per starting VNP Weapon (see check_recording.py).
( python "$SKILL_DIR/check_recording.py" "$GODOT" > "$work/record.txt" 2>&1
  echo $? > "$work/record_code.txt" ) &

# The full recording check, with --full: a few recorded fights into escalation that between them use every
# weapon, Drone Weapon, mod and asteroid amount, each checked field by field (see check_recording_full.py).
if [ $full -eq 1 ]; then
	( python "$SKILL_DIR/check_recording_full.py" "$GODOT" > "$work/full.txt" 2>&1
	  echo $? > "$work/full_code.txt" ) &
fi
wait

echo "== Headless fight, 30 seconds, with every enemy type"
fight="$(cat "$work/fight.txt")"
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
	# The run review's maps: maps=yes draws an overview, and strip=20 asks for at least one strip.
	overview_png="$(ls playtests/check/maps/*_overview.png 2>/dev/null | head -1)"
	strip_count="$(ls playtests/check/maps/*_strip*.png 2>/dev/null | wc -l)"
	if [ -z "$overview_png" ] || [ "$strip_count" -lt 1 ]; then
		fail "the run review maps were not drawn (overview: ${overview_png:-none}, strips: $strip_count)"
	else
		note "Run review maps: an overview and $strip_count strip(s) drawn"
	fi
fi
rm -rf playtests/check
fight_warnings="$(echo "$fight" | grep -E "^WARNING" | sort | uniq -c | head -10)"
[ -n "$fight_warnings" ] && { echo "$fight_warnings"; note "Fight warnings (check whether they are new): $(echo "$fight_warnings" | wc -l)"; }

echo "== Repeatability: the same seed must give the same fight"
trace_lines="$(wc -l < "$work/repeat_a.txt")"
exit_code="$(cat "$work/exit_code.txt" 2>/dev/null || echo missing)"
if [ "$trace_lines" -lt 100 ]; then
	fail "the repeatability fights didn't run long enough to compare ($trace_lines trace lines)"
elif ! diff -q "$work/repeat_a.txt" "$work/repeat_b.txt" > /dev/null; then
	diff "$work/repeat_a.txt" "$work/repeat_b.txt" | head -4
	fail "the same seed gave two different fights; the first differing line above shows when they parted"
else
	note "Repeatability: two runs of the same 90-second pilot fight, one with its fight log and detectors, matched at all $trace_lines checkpoints"
fi
rm -rf playtests/check_repeat
if [ "$exit_code" = "0" ]; then
	note "Clean quit: a full pilot fight that ended quit cleanly"
else
	grep -m3 "Fatal\|   at " "$work/exit.txt"
	fail "a full pilot fight that ended didn't quit cleanly (exit code $exit_code)"
fi

echo "== Quick batch (smoke test of the batch tools)"
# Every fight must finish without errors and write its log, and the report must be written. Its balance numbers
# are shown, not judged.
batch_out="$(cat "$work/batch.txt")"
batch_code="$(cat "$work/batch_code.txt" 2>/dev/null || echo 1)"
batch_dir="$(ls -td playtests/batches/*_check-stage 2>/dev/null | head -1)"
if [ "$batch_code" = "0" ] && [ -f "$batch_dir/report.md" ]; then
	note "Quick batch: $(echo "$batch_out" | grep -o '[0-9]* of [0-9]* fights ok'), report written"
else
	echo "$batch_out" | tail -8
	fail "the quick batch failed (see above)"
fi
[ -n "$batch_dir" ] && rm -rf "$batch_dir"

echo "== Recording covers every weapon"
# Every weapon must show up in the whole-fight recording (a shot, or something from its CollectPlaced), so the
# designer's fights can be drawn in full.
record_out="$(cat "$work/record.txt")"
if [ "$(cat "$work/record_code.txt" 2>/dev/null)" = "0" ]; then
	note "Recording: every weapon shows up ($(echo "$record_out" | grep -c '^PASS') checks)"
else
	echo "$record_out" | grep -E "FAIL|Traceback|Error"
	fail "a weapon doesn't show up in the recording (see above)"
fi

if [ $full -eq 1 ]; then
	echo "== Full recording check (just before each push)"
	full_out="$(cat "$work/full.txt")"
	if [ "$(cat "$work/full_code.txt" 2>/dev/null)" = "0" ]; then
		note "Full recording check: $(echo "$full_out" | grep -c '^  PASS') checks passed in $(echo "$full_out" | grep -c '^== fight') fights"
	else
		echo "$full_out" | grep -E "^==|FAIL|Traceback|Error"
		fail "the full recording check failed (see above)"
	fi
fi
rm -rf "$work"

# A performance test, run with what else is using the computer sampled alongside it (sample_processes.ps1), then
# each slow frame explained by perf_context.py: garbage collection in it, and which programs were busy then. A
# failure is also added to playtests/checks/perf_failures.txt, one line each, to compare across runs. Leaves the
# harness's output in perf_out.
perf_test() {
	local name="$1"; shift
	local stop="$perf_tmp/stop_$name" samples="$perf_tmp/procs_$name.txt"
	rm -f "$stop"
	powershell -NoProfile -ExecutionPolicy Bypass -File "$(cygpath -w "$SKILL_DIR/sample_processes.ps1")" \
		-Out "$(cygpath -w "$samples")" -StopFile "$(cygpath -w "$stop")" > /dev/null 2>&1 &
	local sampler=$!
	perf_out="$(run_scenario window "$@")"
	touch "$stop"
	wait "$sampler" 2>/dev/null
	printf '%s\n' "$perf_out" > "$perf_tmp/out_$name.txt"
	perf_context="$(python "$SKILL_DIR/perf_context.py" "$name" "$perf_tmp/out_$name.txt" "$samples" "$checks_dir/perf_failures.txt")"
}

if [ $full -eq 1 ] || [ $crowd -eq 1 ]; then
	perf_tmp="$(mktemp -d)"
fi

if [ $full -eq 1 ]; then
	echo "== Performance requirement (just before each push; opens a window)"
	# The stress case for CLAUDE.md's performance requirement: a Rich field, a full swarm of Beam drones, Orbital
	# Shards with extra shards and Mine Layer for effects, eight groups of every enemy type plus six more of Goos,
	# and an expert pilot flying, kept alive, for a minute of fight, in a window with vsync off (perf=). plain judges
	# the frame budget and hitches on clock time with nothing extra measuring; detail adds the logic budget and
	# shows where the time goes. Frames run uncapped, so each takes a few seconds.
	stress=(pilot=expert build=greedy seed=4 asteroids=rich weapon=orbital_shards drone_weapon=beam
		take=orbital_shards_count,orbital_shards_count,orbital_shards_area,mine_layer,drone_cap,drone_cap
		spawn=all:8 spawn=basic_goo:6 drones=40 invulnerable maxtime=60 mute=yes log=none close=yes)
	for mode in plain detail; do
		perf_test "stress_$mode" "${stress[@]}" perf=$mode
		echo "$perf_out" | grep "^PERF"
		[ -n "$perf_context" ] && echo "$perf_context"
		if echo "$perf_out" | grep -q "^PERF: requirement MET"; then
			note "Performance ($mode): $(echo "$perf_out" | grep -o 'slowest 1% [0-9.]* ms ([0-9]* fps), worst [0-9.]* ms')"
		else
			fail "the performance requirement is not met in the stress test ($mode; see the PERF lines above). $(echo "$perf_context" | grep 'slow frame(s);' | sed 's/^PERF CONTEXT: //')"
		fi
	done

	# The late fight: an invulnerable expert plays a Rich field to 6:00, fast forwarded (perfstart=), then a
	# minute of escalated fight is measured, with recording on as in the designer's fights. It catches costs
	# that grow over a long fight, such as asteroids piling up as the VNP explores, which the stress minute
	# doesn't reach. About 100 seconds.
	late=(pilot=expert build=designer seed=8 asteroids=rich weapon=orbital_shards drone_weapon=beam invulnerable
		maxtime=420 perfstart=360 record=yes perf=detail profile=yes mute=yes log=none close=yes)
	perf_test late "${late[@]}"
	echo "$perf_out" | grep -E "^PERF|^PROFILE"
	[ -n "$perf_context" ] && echo "$perf_context"
	if echo "$perf_out" | grep -q "^PERF: requirement MET"; then
		note "Performance (late fight, 6:00 to 7:00): $(echo "$perf_out" | grep -o 'slowest 1% [0-9.]* ms ([0-9]* fps), worst [0-9.]* ms'); $(echo "$perf_out" | grep -o 'game logic [0-9.]* ms (budget [0-9.]*)')"
	else
		fail "the performance requirement is not met in the late fight (see the PERF lines above). $(echo "$perf_context" | grep 'slow frame(s);' | sed 's/^PERF CONTEXT: //')"
	fi

fi

if [ $crowd -eq 1 ]; then
	echo "== Tenfold crowd (report only; opens a window)"
	# About ten times a late fight's enemies (360 groups of each type, about 2,500), for where crowds may go (the
	# designer's "full Vampire Survivors"). It doesn't meet the logic budget yet and doesn't fail the check; it shows
	# progress until it becomes a requirement. Only with --crowd, at the designer's choice: it took 40% of --full's
	# time and can never fail it, so it runs when performance work is under way.
	crowd_args=(pilot=expert build=greedy seed=4 asteroids=rich weapon=orbital_shards drone_weapon=beam spawn=all:360
		drones=40 invulnerable maxtime=30 perf=detail profile=yes mute=yes log=none close=yes)
	perf_out="$(run_scenario window "${crowd_args[@]}")"
	alive="$(echo "$perf_out" | grep -E "^t [0-9.]+s" | tail -1 | grep -oE "enemies [0-9]+" | awk '{print $2}')"
	note "Tenfold crowd (report only, ${alive:-?} enemies alive at the end): $(echo "$perf_out" | grep -o 'slowest 1% [0-9.]* ms ([0-9]* fps)' | head -1); $(echo "$perf_out" | grep -o 'game logic [0-9.]* ms (budget [0-9.]*)'); enemies $(echo "$perf_out" | grep -oE 'PROFILE: +enemies +[0-9.]+ / +[0-9.]+' | grep -oE '[0-9.]+ / +[0-9.]+') ms; pilot $(echo "$perf_out" | grep -oE 'PROFILE: +pilot +[0-9.]+ / +[0-9.]+' | grep -oE '[0-9.]+ / +[0-9.]+') ms (average / slowest 1%)"
fi

echo "== Cheats kept out of export builds"
# Top-level classes only: nested ones go with their parent, and their short names (such as Page)
# turn up in other code too.
cheat_classes="$(grep -hoE "^(public |internal )?(static |sealed |abstract |partial )*class [A-Za-z0-9_]+" -r --include=*.cs scripts/cheats 2>/dev/null | awk '{print $NF}' | sort -u)"
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

[ -n "$perf_tmp" ] && rm -rf "$perf_tmp"

echo
echo "================ SUMMARY ================"
printf '%s\n' "${summary[@]}"
echo "Mechanical checks: $([ $problems -eq 0 ] && echo "PASSED" || echo "$problems PROBLEM(S)")"
exit $([ $problems -eq 0 ] && echo 0 || echo 1)
