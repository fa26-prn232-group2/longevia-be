"""Strict evaluator of raw k6 points; no dependency on k6 summary percentiles."""
import collections
import datetime as dt
import json
import math
import re
from pathlib import Path

ROUTES = {
    '/metrics/history?pageNumber=1&pageSize=20': ('read', 200),
    '/metrics/history?pageNumber=2&pageSize=20': ('read', 200),
    '/dashboard/summary': ('read', 200), '/water/today': ('read', 200),
    '/fasting/current': ('read', 200), '/water/log': ('write', 200), '/metrics': ('write', 201),
}
WEIGHTS = dict(zip(ROUTES, (.1, .1, .2, .1, .1, .2, .2)))


def timestamp(value):
    # k6 emits nanoseconds; Python 3.10 accepts at most microseconds.
    # Truncation loses <1 microsecond and never extends the measured window.
    value = re.sub(r'\.(\d+)(?=Z|[+-])', lambda match: '.' + (match[1] + '000000')[:6], value)
    return dt.datetime.fromisoformat(value.replace('Z', '+00:00')).timestamp()


def percentile(values, fraction):
    """Nearest-rank percentile over ALL samples, including failed requests."""
    return sorted(values)[max(0, math.ceil(len(values) * fraction) - 1)] if values else None


def evaluate(points, rate=120, duration=60):
    groups = collections.defaultdict(list)
    statuses = collections.Counter()
    completed = collections.Counter()
    counts = collections.Counter()
    failed = 0
    late = 0
    dropped = 0
    starts = set()
    warmup = collections.Counter()
    warmup_starts = set()
    warmup_failed = 0
    invalid = []
    completion_times = collections.defaultdict(list)
    for point in points:
        if point.get('type') != 'Point':
            continue
        data = point['data']
        tags = data.get('tags', {})
        metric = point['metric']
        if metric == 'dropped_iterations':
            dropped += data['value']
        if metric == 'business_outcomes' and tags.get('phase') == 'warmup':
            warmup[tags['status']] += data['value']
            completion_times['warmup'].append(timestamp(data['time']))
            warmup_starts.add(float(tags['window_start']) / 1000)
            if tags.get('route') not in ROUTES or tags['status'] != str(ROUTES[tags['route']][1]):
                warmup_failed += data['value']
        if tags.get('phase') != 'steady' or metric not in ('business_latency', 'business_outcomes'):
            continue
        route = tags.get('route')
        if route not in ROUTES or tags.get('kind') != ROUTES[route][0]:
            invalid.append('unknown route/kind')
            continue
        if metric == 'business_latency':
            value = data['value']
            if not isinstance(value, (int, float)) or not math.isfinite(value) or value < 0:
                invalid.append('invalid latency')
                continue
            groups[route].append(value)
            groups[tags['kind']].append(value)
        else:
            if data['value'] != 1:
                invalid.append('non-unit outcome')
                continue
            start = float(tags['window_start']) / 1000
            starts.add(start)
            end = timestamp(data['time'])
            completion_times['steady'].append(end)
            inside = start <= end < start + duration
            ok = tags['status'] == str(ROUTES[route][1])
            statuses[tags['status']] += 1
            counts[route] += 1
            if not ok:
                failed += 1
            if not inside:
                late += 1
            if ok and inside:
                completed[route] += 1
    attempted = sum(counts.values())
    successful = sum(completed.values())
    all_latencies = len(groups['read']) + len(groups['write'])
    spans = {phase: max(times) - min(times) for phase, times in completion_times.items()}
    warmup_end = (max(completion_times['warmup']) - next(iter(warmup_starts))) if len(warmup_starts) == 1 else 0
    complete = (not invalid and len(starts) == 1 and attempted >= rate * duration - 2
                and all_latencies == attempted and set(counts) == set(ROUTES)
                and sum(warmup.values()) >= rate * 15 - 2 and warmup_end >= 14.9
                and spans.get('steady', 0) >= duration - .1
                and all(abs(counts[route] - rate * duration * WEIGHTS[route]) <= 2 for route in ROUTES))
    read_p95 = percentile(groups['read'], .95)
    write_p95 = percentile(groups['write'], .95)
    passed = (complete and read_p95 < 150 and write_p95 < 250
              and successful / duration >= 100 and failed == 0 and dropped == 0 and warmup_failed == 0)
    result = {
        'verdict': 'PASS' if passed else ('FAIL' if complete else 'INCONCLUSIVE'),
        'requestedRps': rate, 'measurementSeconds': duration, 'warmupSeconds': 15,
        'attempted': attempted, 'successfulInWindow': successful, 'failed': failed,
        'lateCompletions': late, 'droppedIterations': dropped, 'statuses': dict(statuses),
        'warmupStatuses': dict(warmup), 'warmupFailed': warmup_failed, 'attemptedRps': attempted / duration,
        'successfulRps': successful / duration, 'invalidEvidence': invalid,
        'percentileMethod': 'nearest-rank; all steady samples including failures/late completions',
        'windowStartUtc': dt.datetime.fromtimestamp(next(iter(starts)), dt.timezone.utc).isoformat() if starts else None,
        'observedCompletionSpanSeconds': spans,
        'warmupLastCompletionSecondsFromScenarioStart': warmup_end,
        'read': {f'p{int(p*100)}': percentile(groups['read'], p) for p in (.5, .95, .99)},
        'write': {f'p{int(p*100)}': percentile(groups['write'], p) for p in (.5, .95, .99)},
        'routes': {route: {'p95': percentile(groups[route], .95), 'attempted': counts[route],
                           'successfulRps': completed[route] / duration} for route in ROUTES},
    }
    return result


def from_file(path):
    try:
        with Path(path).open(encoding='utf-8') as source:
            return evaluate(json.loads(line) for line in source if line.strip())
    except (OSError, ValueError, KeyError, TypeError, OverflowError) as error:
        return {'verdict': 'INCONCLUSIVE', 'reason': type(error).__name__}


def aggregate(runs):
    return 'PASS' if len(runs) == 3 and all(r.get('verdict') == 'PASS' for r in runs) else 'NOT_ACCEPTED'
