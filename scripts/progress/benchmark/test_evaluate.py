import copy
import datetime as dt
import tempfile
import unittest
from pathlib import Path
from evaluate import ROUTES, aggregate, evaluate, from_file, timestamp


def points(read=149, write=249, status=None):
    rows = []
    epoch = dt.datetime(1970, 1, 1, tzinfo=dt.timezone.utc)
    keys = list(ROUTES)
    cycle = [keys[0], keys[1], keys[2], keys[2], keys[3], keys[4], keys[5], keys[5], keys[6], keys[6]]
    for i in range(7200):
        route = cycle[i % 10]
        kind, expected = ROUTES[route]
        tags = {'phase': 'steady', 'kind': kind, 'route': route, 'status': str(status or expected), 'window_start': '0'}
        for metric, value in [('business_latency', read if kind == 'read' else write), ('business_outcomes', 1)]:
            rows.append({'type': 'Point', 'metric': metric, 'data': {'value': value, 'tags': tags, 'time': (epoch + dt.timedelta(seconds=i / 120 + .001)).isoformat()}})
    for i in range(1800):
        rows.append({'type': 'Point', 'metric': 'business_outcomes', 'data': {'value': 1, 'time': (epoch + dt.timedelta(seconds=-20 + i / 120)).isoformat(), 'tags': {'phase': 'warmup', 'status': '200', 'route': keys[0], 'window_start': '-20000'}}})
    return rows


class EvaluationTests(unittest.TestCase):
    def test_actual_k6_nanosecond_timestamp(self):
        self.assertEqual(timestamp('2026-11-04T14:06:13.156159Z'),
                         timestamp('2026-11-04T14:06:13.156159061Z'))
        self.assertEqual(timestamp('2026-11-04T14:06:42.095750Z'),
                         timestamp('2026-11-04T14:06:42.09575Z'))

    def test_valid_full_result_passes(self):
        self.assertEqual('PASS', evaluate(points())['verdict'])

    def test_exact_read_boundary_fails(self):
        self.assertEqual('FAIL', evaluate(points(read=150))['verdict'])

    def test_exact_write_boundary_fails(self):
        self.assertEqual('FAIL', evaluate(points(write=250))['verdict'])

    def test_below_100_successful_rps_fails(self):
        rows = points()
        for row in rows[:2600]:
            row['data']['tags'] = dict(row['data']['tags'], status='500')
        result = evaluate(rows)
        self.assertLess(result['successfulRps'], 100)
        self.assertEqual('FAIL', result['verdict'])

    def test_missing_results_never_pass(self):
        self.assertEqual('INCONCLUSIVE', evaluate([])['verdict'])
        self.assertEqual('NOT_ACCEPTED', aggregate([]))

    def test_failure_is_counted_and_latency_not_discarded(self):
        result = evaluate(points(read=900, status=500))
        self.assertEqual(7200, result['failed'])
        self.assertEqual(900, result['read']['p95'])
        self.assertEqual('FAIL', result['verdict'])

    def test_dropped_arrivals_fail(self):
        rows = points() + [{'type': 'Point', 'metric': 'dropped_iterations', 'data': {'value': 1}}]
        self.assertEqual('FAIL', evaluate(rows)['verdict'])

    def test_late_completions_do_not_inflate_successful_rps(self):
        rows = copy.deepcopy(points())
        for row in rows:
            if row['data']['tags']['phase'] == 'steady':
                row['data']['time'] = (dt.datetime.fromisoformat(row['data']['time']) + dt.timedelta(seconds=61)).isoformat()
        result = evaluate(rows)
        self.assertEqual(0, result['successfulRps'])
        self.assertEqual('FAIL', result['verdict'])

    def test_bounded_drain_is_reported_without_counting_it_in_window(self):
        rows = points()
        rows[-1801]['data']['time'] = '1970-01-01T00:01:00.010Z'
        result = evaluate(rows)
        self.assertEqual(1, result['lateCompletions'])
        self.assertEqual(7199, result['successfulInWindow'])
        self.assertEqual('PASS', result['verdict'])

    def test_truncated_file_inconclusive(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / 'bad.json'
            path.write_text('{')
            self.assertEqual('INCONCLUSIVE', from_file(path)['verdict'])

    def test_requires_three_independent_passing_runs(self):
        self.assertEqual('PASS', aggregate([{'verdict': 'PASS'}] * 3))
        self.assertEqual('NOT_ACCEPTED', aggregate([{'verdict': 'PASS'}] * 2))

    def test_warmup_failures_are_visible_and_do_not_get_silent_pass(self):
        rows = points()
        rows[-1]['data']['tags']['status'] = '503'
        result = evaluate(rows)
        self.assertEqual(1, result['warmupFailed'])
        self.assertEqual('FAIL', result['verdict'])

    def test_truncated_warmup_cannot_pass(self):
        self.assertEqual('INCONCLUSIVE', evaluate(points()[:-1000])['verdict'])

    def test_short_measurement_cannot_claim_full_window(self):
        rows = points()
        for row in rows:
            if row['data']['tags']['phase'] == 'steady':
                row['data']['time'] = '1970-01-01T00:00:01Z'
        self.assertEqual('INCONCLUSIVE', evaluate(rows)['verdict'])


if __name__ == '__main__':
    unittest.main()
