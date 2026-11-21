extends SceneTree

# Loads every data file in the project and reports anything broken. Run from the project folder:
#   godot --headless --path . -s res://_check_data.gd
# (check_stage.sh copies it in under that name, runs it, and removes it.)
#
# It does its work once, in _initialize, and quits, so a script error cannot leave it running.

const SKIP_DIRS = [".godot", ".claude", "addons"]

func _initialize() -> void:
	var problems := 0
	var loaded := 0

	# Every .tres outside placeholder/ is a tuning or content file, and must load with its C# script.
	# A missing script usually means a class was renamed or moved without updating path= in the file.
	for path in _walk("res://"):
		var resource = load(path)
		loaded += 1
		if resource == null:
			print("PROBLEM: could not load ", path)
			problems += 1
		elif not path.begins_with("res://placeholder/") and resource.get_script() == null:
			print("PROBLEM: no script on ", path)
			problems += 1
	print("data files loaded: ", loaded)

	# The upgrade pool: every entry must point at a real upgrade file.
	var levels = load("res://tuning/levels.tres")
	if levels != null:
		var missing := 0
		for upgrade in levels.Upgrades:
			if upgrade == null:
				missing += 1
		print("upgrade pool: ", levels.Upgrades.size(), " upgrades, ", missing, " missing")
		problems += missing

	# Sounds: every listed sound needs its file.
	var audio = load("res://audio/audio.tres")
	if audio != null:
		var silent := 0
		for sound in audio.Sounds:
			if sound == null or sound.Stream == null:
				silent += 1
				print("PROBLEM: sound without a file: ", sound.Id if sound != null else "(empty entry)")
		print("sounds: ", audio.Sounds.size(), " listed, ", silent, " without a file")
		problems += silent

	# Enemy types that can spawn, with their weights.
	var spawning = load("res://tuning/enemy_spawning.tres")
	if spawning != null:
		for weights_name in ["GroupWeights", "WaveGroupWeights"]:
			var weights = spawning.get(weights_name)
			if weights == null:
				continue
			var parts := []
			for enemy in weights:
				parts.append("%s %s" % [enemy.Id if enemy != null else "(missing)", weights[enemy]])
			print(weights_name, ": ", ", ".join(parts))

	print("DATA CHECK: ", "OK" if problems == 0 else "%d PROBLEM(S)" % problems)
	quit(1 if problems > 0 else 0)

func _walk(dir: String) -> Array:
	var found := []
	var access := DirAccess.open(dir)
	if access == null:
		return found
	for file in access.get_files():
		if file.ends_with(".tres"):
			found.append(dir.path_join(file))
	for sub in access.get_directories():
		if sub in SKIP_DIRS or sub.ends_with("-workspace"):
			continue
		found += _walk(dir.path_join(sub))
	return found
