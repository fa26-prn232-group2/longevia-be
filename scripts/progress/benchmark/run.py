"""Isolated, setup-only seeding; measured traffic always uses real HTTP APIs."""
import argparse
import datetime as dt
import hashlib
import json
import os
from pathlib import Path
import platform
import socket
import subprocess
import threading
import time
import urllib.request
from evaluate import aggregate, from_file

ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent
OUT = ROOT / 'artifacts/progress-benchmark'
PROJECT = 'longevia-progress-sc006-benchmark'


def command(args, **kwargs):
    # Never log commands containing private keys/passwords; config lives only in ignored .env.
    kwargs.setdefault('timeout', 600)
    return subprocess.run(args, cwd=ROOT, text=True, check=True, **kwargs)


def capture(args):
    return command(args, stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=60).stdout.strip()


def compose(*args, **kwargs):
    return command(['docker', 'compose', '--project-name', PROJECT, '--env-file', str(OUT / '.env'),
                    '-f', str(HERE / 'compose.yml'), *args], **kwargs)


def port():
    with socket.socket() as connection:
        connection.bind(('127.0.0.1', 0))
        return connection.getsockname()[1]


def wait_http(url):
    end = time.monotonic() + 120
    while time.monotonic() < end:
        try:
            with urllib.request.urlopen(url, timeout=2) as response:
                if response.status == 200:
                    return
        except OSError:
            pass
        time.sleep(.25)
    raise TimeoutError('Benchmark HTTP host not ready within 120s')


def resources(ids):
    output = []
    for container in ids:
        inspected = json.loads(capture(['docker', 'inspect', container]))[0]
        config = inspected['HostConfig']
        output.append({'id': container, 'imageId': inspected['Image'],
                       'service': inspected['Config']['Labels']['com.docker.compose.service'],
                       'memoryBytes': config['Memory'], 'nanoCpus': config['NanoCpus'],
                       'oomKilled': inspected['State']['OOMKilled']})
    return output


def counters(ids):
    result = {}
    for container in ids:
        try:
            result[container] = capture(['docker', 'exec', container, 'cat', '/sys/fs/cgroup/cpu.stat'])
        except subprocess.CalledProcessError:
            # Chiseled application images have no cat; Docker stats still measures usage.
            result[container] = 'not accessible: chiseled image; see Docker stats'
    return result


def resource_sample(ids):
    listing = ['docker', 'ps', '--filter', f'label=com.docker.compose.project={PROJECT}', '-q']
    current = capture(listing).splitlines()
    if not set(ids).issubset(current):
        raise ValueError('Persistent benchmark container stopped during measurement')
    stats = ['docker', 'stats', '--no-stream', '--format', '{{json .}}']
    generator_exited = False
    try:
        raw = capture([*stats, *current])
    except subprocess.CalledProcessError:
        transient = set(current) - set(ids)
        # --rm may remove k6 between ps and stats at the end of the bounded drain.
        # Retry persistent services only after proving the transient container exited.
        if not transient or transient.intersection(capture(listing).splitlines()):
            raise
        raw = capture([*stats, *ids])
        generator_exited = True
    return {'utc': dt.datetime.now(dt.timezone.utc).isoformat(),
            'containers': [json.loads(line) for line in raw.splitlines()],
            'generatorExitedDuringSample': generator_exited}


def monitor(ids, stop, samples):
    while not stop.is_set():
        try:
            samples.append(resource_sample(ids))
        except (OSError, ValueError, subprocess.CalledProcessError, subprocess.TimeoutExpired):
            samples.append({'error': 'docker stats unavailable', 'utc': dt.datetime.now(dt.timezone.utc).isoformat()})
        stop.wait(2)


def query_plan():
    sql = '''EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON)
SELECT "LoggedAt" FROM body_metric_logs WHERE "UserId" =
(SELECT "UserId" FROM body_metric_logs GROUP BY "UserId" ORDER BY count(*) DESC LIMIT 1);
EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON)
SELECT * FROM body_metric_logs WHERE "UserId" =
(SELECT "UserId" FROM body_metric_logs GROUP BY "UserId" ORDER BY count(*) DESC LIMIT 1)
ORDER BY "LoggedAt" DESC, "Id" DESC LIMIT 20;'''
    return compose('exec', '-T', 'progress-db', 'psql', '-U', 'progress_user', '-d', 'progress_db',
                   '-c', sql, stdout=subprocess.PIPE).stdout


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--cleanup', action='store_true')
    args = parser.parse_args()
    if args.cleanup:
        # Exact dedicated project only; never use the team's regular Compose file/project.
        compose('down', '--volumes', '--remove-orphans')
        (OUT / 'users.json').unlink(missing_ok=True)
        return
    OUT.mkdir(parents=True, exist_ok=True)
    docker_info = json.loads(capture(['docker', 'info', '--format', '{{json .}}']))
    sha = capture(['git', 'rev-parse', 'HEAD'])
    with (OUT / 'setup.log').open('w', encoding='utf-8') as log:
        seed = ['dotnet', 'run', '--project', str(HERE / 'Seed/Seed.csproj'), '-c', 'Release', '--']
        if not (OUT / '.env').exists():
            command([*seed, 'init', str(OUT), str(port()), str(port()), str(port()), sha], stdout=log, stderr=log)
        else:
            env = (OUT / '.env').read_text().splitlines()
            (OUT / '.env').write_text('\n'.join(f'GIT_SHA={sha}' if line.startswith('GIT_SHA=') else line for line in env) + '\n')
        compose('build', stdout=log, stderr=log)
        compose('up', '-d', '--wait', '--wait-timeout', '180', stdout=log, stderr=log)
        compose('pull', 'generator', stdout=log, stderr=log)
    settings = dict(line.split('=', 1) for line in (OUT / '.env').read_text().splitlines())
    wait_http(f'http://127.0.0.1:{settings["PROGRESS_PORT"]}/health')
    wait_http(f'http://127.0.0.1:{settings["IDENTITY_PORT"]}/health')
    ids = capture(['docker', 'ps', '--filter', f'label=com.docker.compose.project={PROJECT}', '-q']).splitlines()
    metadata = {
        'timestampUtc': dt.datetime.now(dt.timezone.utc).isoformat(), 'gitSha': sha,
        'workingTree': capture(['git', 'status', '--porcelain']),
        'host': {'os': platform.platform(), 'architecture': platform.machine(), 'logicalCpu': os.cpu_count()},
        'docker': docker_info,
        'dotnet': capture(['dotnet', '--info']), 'containers': resources(ids),
        'serverRuntimes': {service: compose('exec', '-T', service, 'dotnet', '--info', stdout=subprocess.PIPE).stdout
                           for service in ('progress', 'identity')},
        'postgresVersion': compose('exec', '-T', 'progress-db', 'psql', '-U', 'progress_user', '-d', 'progress_db',
                                    '-Atc', 'SELECT version()', stdout=subprocess.PIPE).stdout.strip(),
        'tool': 'grafana/k6:1.3.0', 'instances': {'progress': 1, 'identity': 1},
        'placement': 'generator, Progress, real Identity and both PG containers on same Docker host/project',
        'pools': {'progressMax': 80, 'identityMax': 40},
        'vus': {'preallocatedPerScenario': 240, 'maxPerScenario': 240},
        'transport': 'direct HTTP/1 Kestrel; Identity HTTP/2 actual provider; JWT ES256 validated; no Gateway measurement',
    }
    # Docker info contains no benchmark env; keep a compact, non-sensitive subset.
    metadata['docker'] = {k: metadata['docker'].get(k) for k in ('ServerVersion', 'NCPU', 'MemTotal', 'OperatingSystem', 'Architecture', 'CgroupVersion')}
    if platform.system() == 'Windows':
        metadata['host']['hardware'] = json.loads(capture(['pwsh', '-NoProfile', '-Command',
            '$c=Get-CimInstance Win32_Processor; $o=Get-CimInstance Win32_OperatingSystem; @{cpu=$c.Name;physicalCores=$c.NumberOfCores;logicalCpus=$c.NumberOfLogicalProcessors;ramKiB=$o.TotalVisibleMemorySize;availableRamKiB=$o.FreePhysicalMemory}|ConvertTo-Json -Compress']))
    runs = []
    for number in range(1, 4):
        with (OUT / f'seed-{number}.log').open('w', encoding='utf-8') as log:
            command([*seed, 'seed', str(OUT)], stdout=log, stderr=log)
        dataset = json.loads((OUT / 'dataset.json').read_text())
        (OUT / f'query-plan-{number}.txt').write_text(query_plan(), encoding='utf-8')
        samples = []
        stop = threading.Event()
        sampler = threading.Thread(target=monitor, args=(ids, stop, samples), daemon=True)
        before = counters(ids)
        sampler.start()
        begin = time.monotonic()
        raw = f'raw-{number}.json'
        with (OUT / f'k6-{number}.log').open('w', encoding='utf-8') as log:
            try:
                compose('run', '--rm', '-T', 'generator', 'run', '--out', f'json=/output/{raw}', '/scripts/load.js', stdout=log, stderr=log)
                exit_code = 0
            except subprocess.CalledProcessError as error:
                exit_code = error.returncode
        elapsed = time.monotonic() - begin
        stop.set()
        sampler.join(timeout=15)
        with (OUT / f'verify-{number}.log').open('w', encoding='utf-8') as log:
            command([*seed, 'verify', str(OUT)], stdout=log, stderr=log)
        result = from_file(OUT / raw)
        result.update({'run': number, 'generatorExitCode': exit_code, 'elapsedMonotonicSeconds': elapsed,
                       'persistence': json.loads((OUT / 'persistence.json').read_text()),
                       'dataset': dataset, 'resourceSamples': samples, 'cpuCountersBefore': before,
                       'cpuCountersAfter': counters(ids), 'containersAfter': resources(ids)})
        expected_metrics = result.get('statuses', {}).get('201', 0) + result.get('warmupStatuses', {}).get('201', 0)
        generator_samples = sum('generator' in container.get('Name', '')
                                for sample in samples for container in sample.get('containers', []))
        result['generatorResourceSamples'] = generator_samples
        if (exit_code != 0 or not samples or generator_samples == 0 or any('error' in sample for sample in samples)
                or result['persistence']['measuredMetrics'] != expected_metrics
                or any(x['oomKilled'] for x in result['containersAfter'])):
            result['verdict'] = 'INCONCLUSIVE'
        result['rawSha256'] = hashlib.sha256((OUT / raw).read_bytes()).hexdigest() if (OUT / raw).exists() else None
        runs.append(result)
        (OUT / f'run-{number}.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
        print(json.dumps({k: result.get(k) for k in ('run', 'verdict', 'read', 'write', 'successfulRps', 'failed', 'droppedIterations')}), flush=True)
    report = {'metadata': metadata, 'runs': runs, 'verdict': aggregate(runs)}
    (OUT / 'results.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    lines = ['# Progress SC-006 measured report', '', f'Commit: {sha}; verdict: {report["verdict"]}', '',
             '| Run | Read p95 ms | Write p95 ms | Successful RPS | Failed | Verdict |', '|---|---:|---:|---:|---:|---|']
    for run in runs:
        lines.append(f'|{run["run"]}|{run.get("read",{}).get("p95")}|{run.get("write",{}).get("p95")}|{run.get("successfulRps")}|{run.get("failed")}|{run["verdict"]}|')
    (OUT / 'report.md').write_text('\n'.join(lines), encoding='utf-8')
    raise SystemExit(0 if report['verdict'] == 'PASS' else 2)


if __name__ == '__main__':
    try:
        main()
    except (OSError, subprocess.CalledProcessError, subprocess.TimeoutExpired, TimeoutError) as error:
        OUT.mkdir(parents=True, exist_ok=True)
        # No raw command/config/error contents: dependency commands may include sensitive data.
        result = {'verdict': 'INCONCLUSIVE', 'errorType': type(error).__name__,
                  'timestampUtc': dt.datetime.now(dt.timezone.utc).isoformat(), 'runs': []}
        (OUT / 'results.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
        (OUT / 'report.md').write_text('# Benchmark INCONCLUSIVE\n\nSetup/execution unavailable; no measured acceptance. See local setup.log.\n', encoding='utf-8')
        print(json.dumps(result), flush=True)
        raise SystemExit(4)
