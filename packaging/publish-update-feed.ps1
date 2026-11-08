param([Parameter(Mandatory)][string]$SignedDirectory)
$ErrorActionPreference='Stop'
$dir=(Resolve-Path -LiteralPath $SignedDirectory).Path
$receipt=Get-Content -LiteralPath (Join-Path $dir 'receipt.json') -Raw | ConvertFrom-Json
$envelope=Get-Content -LiteralPath (Join-Path $dir 'stable.json') -Raw | ConvertFrom-Json
$payload=[Convert]::FromBase64String($envelope.payload)
$publicSource=[IO.File]::ReadAllText((Join-Path $PSScriptRoot '../webview-host/UpdateSigningKey.cs'))
$public=[regex]::Match($publicSource,'PublicXml = @"([^"]+)"').Groups[1].Value
$rsa=[Security.Cryptography.RSA]::Create()
try{$rsa.FromXmlString($public);if(-not $rsa.VerifyData($payload,[Convert]::FromBase64String($envelope.signature),[Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.RSASignaturePadding]::Pkcs1)){throw 'Manifest signature does not match pinned client key'}}finally{$rsa.Dispose()}
$document=[Text.Encoding]::UTF8.GetString($payload) | ConvertFrom-Json
$version=[string]$receipt.Version
if($version -notmatch '^\d+\.\d+\.\d+$' -or $receipt.Sha256 -notmatch '^[a-f0-9]{64}$'){throw 'Invalid publication receipt'}
if($document.release.tag_name -ne "v$version" -or $document.release.assets[0].digest -ne "sha256:$($receipt.Sha256)" -or $document.release.assets[0].size -ne $receipt.Bytes -or $document.expires -le [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()){throw 'Manifest and receipt do not match'}
$zip=Join-Path $dir "releases/$version/DeepSeek-Reflex-$version-Windows-x64.zip"
if((Get-FileHash -LiteralPath $zip).Hash.ToLowerInvariant() -ne $receipt.Sha256 -or (Get-Item -LiteralPath $zip).Length -ne $receipt.Bytes){throw 'Signed package changed'}
$id=[Guid]::NewGuid().ToString('N')
$remote="/var/www/reflex-updates/.incoming-$id"
& ssh -o BatchMode=yes -o ConnectTimeout=10 hk "mkdir -m 755 '$remote'"
if($LASTEXITCODE -ne 0){throw 'Cannot prepare remote upload'}
& scp -q (Join-Path $dir 'stable.json') $zip "hk:$remote/"
if($LASTEXITCODE -ne 0){throw 'Upload failed; stable feed was not changed'}
$python=@'
import hashlib,json,os,pathlib,sys,time,base64,fcntl
root=pathlib.Path('/var/www/reflex-updates')
incoming=root/('.incoming-'+sys.argv[1])
assert root.is_dir() and not root.is_symlink() and not incoming.is_symlink()
lock=(root/'.publish.lock').open('a')
fcntl.flock(lock,fcntl.LOCK_EX)
manifest=(incoming/'stable.json').read_bytes()
outer=json.loads(manifest)
doc=json.loads(base64.b64decode(outer['payload'],validate=True))
version=doc['release']['tag_name'][1:]
assert version==sys.argv[2] and doc['expires']>time.time()
asset=doc['release']['assets'][0]
name='DeepSeek-Reflex-'+version+'-Windows-x64.zip'
assert asset['name']==name
source=incoming/name
assert source.stat().st_size==asset['size']<=64*1024*1024
digest=hashlib.file_digest(source.open('rb'),'sha256').hexdigest()
assert asset['digest']=='sha256:'+digest==sys.argv[3]
releases=root/'releases'
releases.mkdir(exist_ok=True)
assert not releases.is_symlink()
target=releases/version
assert not target.is_symlink()
if target.exists():
    assert (target/name).is_file() and not (target/name).is_symlink(), 'incomplete or linked existing version; preserve it for operator inspection'
    assert hashlib.file_digest((target/name).open('rb'),'sha256').hexdigest()==digest, 'version is immutable'
else:
    assert len(list(releases.iterdir()))<6, 'retention budget reached; no files deleted'
    staging=incoming/'package'
    staging.mkdir()
    os.replace(source,staging/name)
    os.rename(staging,target)
os.chmod(target/name,0o644)
if (root/'stable.json').exists():
    old=json.loads(base64.b64decode(json.loads((root/'stable.json').read_text())['payload']))
    def parts(v): return tuple(map(int,v.lstrip('v').split('.')))
    assert parts(old['release']['tag_name'])<=parts('v'+version), 'refusing feed downgrade'
    if old['release']['tag_name']=='v'+version:
        assert doc['issued']>old['issued'], 'renewal must have a newer issue time'
        assert doc['expires']>old['expires'], 'renewal must extend expiration'
    backup=root/('stable-before-'+sys.argv[1]+'.json')
    backup.write_bytes((root/'stable.json').read_bytes())
os.chmod(incoming/'stable.json',0o644)
os.replace(incoming/'stable.json',root/'stable.json')
print('PASS immutable package SHA verified and stable feed replaced atomically')
'@
$script=Join-Path $dir 'publish-remote.py';[IO.File]::WriteAllText($script,$python,[Text.UTF8Encoding]::new($false))
& scp -q $script "hk:$remote/publish.py"
if($LASTEXITCODE -ne 0){throw 'Publication helper upload failed'}
& ssh -o BatchMode=yes -o ConnectTimeout=10 hk "python3 '$remote/publish.py' '$id' '$version' 'sha256:$($receipt.Sha256)'"
if($LASTEXITCODE -ne 0){throw 'Remote verification failed; inspect task-owned staging'}
