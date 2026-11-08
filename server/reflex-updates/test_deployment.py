"""Isolated Linux publication/route fault tests; never target live paths."""
import base64
import hashlib
import json
import pathlib
import re
import runpy
import subprocess
import sys
import tempfile
import time
import unittest
import uuid

PUBLISH = pathlib.Path(sys.argv[1]).read_text(encoding='utf-8-sig')
ROUTE = pathlib.Path(sys.argv[2]).read_text(encoding='utf-8-sig')
sys.argv = sys.argv[:1]
REMOTE = re.search(r"\$python=@'\r?\n(.*?)\r?\n'@", PUBLISH, re.S).group(1)


class DeploymentTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='reflex-publish-test-')
        self.root = pathlib.Path(self.temp.name)

    def tearDown(self):
        self.temp.cleanup()

    def prepare(self, version, issue=0):
        task = uuid.uuid4().hex
        incoming = self.root / ('.incoming-' + task)
        incoming.mkdir()
        name = 'DeepSeek-Reflex-' + version + '-Windows-x64.zip'
        data = ('package fixture ' + version).encode()
        digest = hashlib.sha256(data).hexdigest()
        (incoming / name).write_bytes(data)
        doc = dict(issued=int(time.time()) + issue, expires=int(time.time()) + 86400 + issue,
                   release=dict(tag_name='v' + version, assets=[dict(name=name, size=len(data), digest='sha256:' + digest)]))
        (incoming / 'stable.json').write_text(json.dumps(dict(payload=base64.b64encode(json.dumps(doc).encode()).decode())))
        return task, version, 'sha256:' + digest

    def script(self, extra=''):
        source = REMOTE.replace("'/var/www/reflex-updates'", repr(str(self.root)))
        if extra:
            source = source.replace('fcntl.flock(lock,fcntl.LOCK_EX)', 'fcntl.flock(lock,fcntl.LOCK_EX)\n' + extra)
        file = self.root / ('worker-' + uuid.uuid4().hex + '.py')
        file.write_text(source)
        return file

    def run_worker(self, args, script=None):
        return subprocess.run([sys.executable, str(script or self.script()), *args], capture_output=True, timeout=8)

    def version(self):
        return json.loads(base64.b64decode(json.loads((self.root / 'stable.json').read_text())['payload']))['release']['tag_name']

    def test_crash_before_atomic_version_directory_then_retry(self):
        args = self.prepare('1.0.21')
        source = REMOTE.replace("'/var/www/reflex-updates'", repr(str(self.root))).replace('os.rename(staging,target)', 'os._exit(91)')
        crash = self.root / 'crash.py'; crash.write_text(source)
        self.assertEqual(self.run_worker(args, crash).returncode, 91)
        self.assertFalse((self.root / 'releases/1.0.21').exists())
        self.assertEqual(self.run_worker(self.prepare('1.0.21')).returncode, 0)
        self.assertEqual(self.version(), 'v1.0.21')

    def test_lock_serializes_new_then_old_and_releases_after_failure(self):
        held = self.root / 'lock-held'
        newer = self.prepare('1.0.22')
        new_script = self.script("pathlib.Path(" + repr(str(held)) + ").write_text('held')\ntime.sleep(0.5)")
        with subprocess.Popen([sys.executable, str(new_script), *newer], stdout=subprocess.PIPE, stderr=subprocess.PIPE) as process:
            deadline = time.monotonic() + 5
            while not held.exists():
                if time.monotonic() > deadline:
                    process.kill(); raise AssertionError('lock was not acquired')
                time.sleep(0.01)
            older = subprocess.Popen([sys.executable, str(self.script()), *self.prepare('1.0.21')], stdout=subprocess.PIPE, stderr=subprocess.PIPE)
            try:
                process.communicate(timeout=8)
                older.communicate(timeout=8)
                self.assertEqual(process.returncode, 0)
                self.assertNotEqual(older.returncode, 0)
            finally:
                if older.poll() is None:
                    older.kill(); older.communicate()
        self.assertEqual(self.version(), 'v1.0.22')
        self.assertEqual(self.run_worker(self.prepare('1.0.23')).returncode, 0)
        self.assertEqual(self.version(), 'v1.0.23')

    def test_same_version_different_bytes_and_incomplete_directory_preserved(self):
        self.assertEqual(self.run_worker(self.prepare('1.0.21')).returncode, 0)
        args = self.prepare('1.0.21', 2)
        target = self.root / 'releases/1.0.21/DeepSeek-Reflex-1.0.21-Windows-x64.zip'
        original = target.read_bytes()
        target.write_bytes(b'unknown external content')
        self.assertNotEqual(self.run_worker(args).returncode, 0)
        self.assertEqual(target.read_bytes(), b'unknown external content')
        target.write_bytes(original)
        incomplete = self.root / 'releases/1.0.22'; incomplete.mkdir()
        self.assertNotEqual(self.run_worker(self.prepare('1.0.22')).returncode, 0)
        self.assertTrue(incomplete.is_dir())
        self.assertEqual(self.version(), 'v1.0.21')

    def test_renewal_and_old_issue_rejection(self):
        self.assertEqual(self.run_worker(self.prepare('1.0.21')).returncode, 0)
        self.assertEqual(self.run_worker(self.prepare('1.0.21', 2)).returncode, 0)
        before = (self.root / 'stable.json').read_bytes()
        self.assertNotEqual(self.run_worker(self.prepare('1.0.21', -2)).returncode, 0)
        self.assertEqual((self.root / 'stable.json').read_bytes(), before)

    def test_renewal_expiration_equal_or_earlier_rejected(self):
        self.assertEqual(self.run_worker(self.prepare('1.0.21')).returncode, 0)
        stable = self.root / 'stable.json'
        before = stable.read_bytes()
        old = json.loads(base64.b64decode(json.loads(before)['payload']))
        for delta in (0, -1):
            args = self.prepare('1.0.21', 3)
            manifest = self.root / ('.incoming-' + args[0]) / 'stable.json'
            envelope = json.loads(manifest.read_text())
            doc = json.loads(base64.b64decode(envelope['payload']))
            doc['expires'] = old['expires'] + delta
            envelope['payload'] = base64.b64encode(json.dumps(doc).encode()).decode()
            manifest.write_text(json.dumps(envelope))
            self.assertNotEqual(self.run_worker(args).returncode, 0)
            self.assertEqual(stable.read_bytes(), before)
        self.assertEqual(self.run_worker(self.prepare('1.0.21', 5)).returncode, 0)

    def route_fixture(self):
        etc = self.root / 'etc/nginx'; site = etc / 'sites-enabled/test-doitbenai'
        site.parent.mkdir(parents=True)
        original = b'server {\n server_name test.doitbenai.cloud;\n location = /reflex/feedback { proxy_pass http://127.0.0.1:8787/feedback; }\n location / { proxy_pass http://127.0.0.1:18092; }\n}\n'
        site.write_bytes(original)
        opt = self.root / 'opt/reflex-updates'; opt.mkdir(parents=True)
        (opt / 'nginx.conf').write_text('location = /reflex/updates/stable.json { return 404; }')
        source = ROUTE.replace('/etc/nginx/', str(etc) + '/').replace('/opt/reflex-updates', str(opt)).replace('/var/www/reflex-updates', str(self.root / 'www'))
        file = self.root / 'route.py'; file.write_text(source)
        return runpy.run_path(str(file)), site, original, etc / 'reflex-updates.conf'

    def test_route_test_and_reload_failures_restore_original(self):
        for failure in [('nginx', '-t'), ('systemctl', 'reload', 'nginx')]:
            with self.subTest(failure=failure):
                namespace, site, before, snippet = self.route_fixture()
                failed = []
                def fake(*args):
                    if args == failure and not failed:
                        failed.append(True); raise RuntimeError('injected failure')
                namespace['install'].__globals__['run'] = fake
                with self.assertRaises(RuntimeError):
                    namespace['install']()
                self.assertEqual(site.read_bytes(), before)
                self.assertFalse(snippet.exists())
                # Reuse task-owned fixture paths only for the next subcase.
                import shutil
                for path in (self.root / 'etc', self.root / 'opt', self.root / 'www'):
                    if path.exists(): shutil.rmtree(path)

    def test_route_success_is_idempotent(self):
        namespace, site, before, snippet = self.route_fixture()
        calls = []
        namespace['install'].__globals__['run'] = lambda *args: calls.append(args)
        namespace['install'](); after = site.read_bytes(); namespace['install']()
        self.assertIn(b'proxy_pass http://127.0.0.1:18092;', after)
        self.assertEqual(site.read_bytes(), after)
        self.assertEqual(calls.count(('systemctl', 'reload', 'nginx')), 1)


if __name__ == '__main__':
    unittest.main(verbosity=2)
