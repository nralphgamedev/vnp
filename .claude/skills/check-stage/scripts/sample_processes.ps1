# Samples what else is using the computer while the stage check measures performance, so a slow frame can be
# matched against it (perf_context.py). Every -Interval seconds it writes one line per busy process:
#   <local time, ISO>  <CPU % of the whole machine>  <process name>
# It runs at idle priority so it takes CPU only when a core is free, and stops when -StopFile appears.
param(
	[Parameter(Mandatory = $true)][string]$Out,
	[Parameter(Mandatory = $true)][string]$StopFile,
	[double]$Interval = 0.5,
	[int]$Top = 6
)

try { (Get-Process -Id $PID).PriorityClass = 'Idle' } catch {}
$cores = [Environment]::ProcessorCount
$last = @{}
$lastTime = Get-Date
foreach ($p in Get-Process) { try { $last[$p.Id] = $p.TotalProcessorTime.TotalSeconds } catch {} }

$writer = [System.IO.StreamWriter]::new($Out, $false)
try {
	while (-not (Test-Path $StopFile)) {
		Start-Sleep -Milliseconds ([int]($Interval * 1000))
		$now = Get-Date
		$elapsed = ($now - $lastTime).TotalSeconds
		$lastTime = $now
		$current = @{}
		$busy = @()
		foreach ($p in Get-Process) {
			try { $cpu = $p.TotalProcessorTime.TotalSeconds } catch { continue }
			$current[$p.Id] = $cpu
			if ($last.ContainsKey($p.Id) -and $elapsed -gt 0) {
				$share = ($cpu - $last[$p.Id]) / $elapsed / $cores * 100
				if ($share -ge 0.5) { $busy += [pscustomobject]@{ Name = $p.ProcessName; Share = $share } }
			}
		}
		$last = $current
		$stamp = $now.ToString('yyyy-MM-ddTHH:mm:ss.fff')
		foreach ($b in ($busy | Sort-Object Share -Descending | Select-Object -First $Top)) {
			$writer.WriteLine(('{0} {1:0.0} {2}' -f $stamp, $b.Share, $b.Name))
		}
		$writer.Flush()
	}
}
finally {
	$writer.Close()
}
