import subprocess
import unittest
from unittest.mock import patch
from run import resource_sample


class ResourceSamplingTests(unittest.TestCase):
    def test_missing_persistent_server_invalidates_sampling(self):
        with patch('run.capture', return_value='generator'):
            with self.assertRaises(ValueError):
                resource_sample(['server'])

    def test_removed_generator_retries_and_preserves_server_sample(self):
        failure = subprocess.CalledProcessError(1, ['docker', 'stats'])
        with patch('run.capture', side_effect=['server\ngenerator', failure, 'server', '{"Name":"progress"}']):
            sample = resource_sample(['server'])
        self.assertTrue(sample['generatorExitedDuringSample'])
        self.assertEqual([{'Name': 'progress'}], sample['containers'])

    def test_persistent_server_failure_is_not_hidden(self):
        failure = subprocess.CalledProcessError(1, ['docker', 'stats'])
        with patch('run.capture', side_effect=['server', failure]):
            with self.assertRaises(subprocess.CalledProcessError):
                resource_sample(['server'])

    def test_live_generator_failure_is_not_classified_as_normal_exit(self):
        failure = subprocess.CalledProcessError(1, ['docker', 'stats'])
        with patch('run.capture', side_effect=['server\ngenerator', failure, 'server\ngenerator']):
            with self.assertRaises(subprocess.CalledProcessError):
                resource_sample(['server'])
