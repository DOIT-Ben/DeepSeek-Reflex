"""Install only the Reflex static route; run on the authorized HK host."""
import hashlib
import os
from pathlib import Path
import re
import shutil
import subprocess
import uuid


def run(*args):
    subprocess.run(args, check=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE)


def install():
    source = Path('/opt/reflex-updates/nginx.conf')
    snippet = Path('/etc/nginx/reflex-updates.conf')
    site = Path('/etc/nginx/sites-enabled/test-doitbenai').resolve(strict=True)
    if not str(site).startswith('/etc/nginx/') or site.is_symlink():
        raise RuntimeError('Unexpected nginx site target')
    original = site.read_bytes()
    text = original.decode('utf-8')
    if 'server_name test.doitbenai.cloud;' not in text:
        raise RuntimeError('Wrong virtual host')
    incoming = source.read_bytes()
    if snippet.exists() and snippet.read_bytes() != incoming:
        raise RuntimeError('Existing snippet differs; refusing overwrite')
    include = 'include /etc/nginx/reflex-updates.conf;'
    if include in text:
        run('nginx', '-t')
        print('PASS route already installed; no reload needed')
        return
    marker = re.compile(r'^(\s*)location = /reflex/feedback \{', re.M)
    if len(marker.findall(text)) != 1:
        raise RuntimeError('Expected exactly one HTTPS feedback location')
    candidate = marker.sub(lambda m: m[1] + include + '\n' + m.group(0), text, count=1)
    task = uuid.uuid4().hex
    backup = Path('/opt/reflex-updates') / ('site-before-' + task + '.conf')
    backup.write_bytes(original)
    temporary = site.parent / ('.reflex-' + task)
    had_snippet = snippet.exists()
    try:
        Path('/var/www/reflex-updates').mkdir(mode=0o755, exist_ok=True)
        if Path('/var/www/reflex-updates').is_symlink():
            raise RuntimeError('Static root must not be a symlink')
        snippet.write_bytes(incoming)
        os.chmod(snippet, 0o644)
        temporary.write_text(candidate, encoding='utf-8')
        shutil.copymode(site, temporary)
        os.replace(temporary, site)
        run('nginx', '-t')
        run('systemctl', 'reload', 'nginx')
    except Exception:
        temporary.write_bytes(original)
        shutil.copymode(site, temporary)
        os.replace(temporary, site)
        if not had_snippet:
            snippet.unlink(missing_ok=True)
        run('nginx', '-t')
        run('systemctl', 'reload', 'nginx')
        raise
    finally:
        temporary.unlink(missing_ok=True)
    print('PASS isolated static route enabled; prior site SHA256=' + hashlib.sha256(original).hexdigest())


if __name__ == '__main__':
    install()
