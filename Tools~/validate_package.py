"""Fast package integrity checks. Does not replace Unity compilation or play tests."""
from pathlib import Path
import json
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
errors = []
def check(condition, message):
    if not condition:
        errors.append(message)

manifest = json.loads((ROOT / 'package.json').read_text(encoding='utf-8-sig'))
check(manifest['name'] == 'com.oreethehat.easy-combat', 'Unexpected package identity')
check(manifest['license'] == 'MIT', 'License must match LICENSE.md')
check((ROOT / 'LICENSE.md').read_text(encoding='utf-8-sig').startswith('MIT License'), 'Missing MIT license')
for sample in manifest.get('samples', []):
    check((ROOT / sample['path']).is_dir(), 'Missing sample: ' + sample['path'])
assemblies = {}
for path in ROOT.rglob('*.asmdef'):
    data = json.loads(path.read_text(encoding='utf-8-sig'))
    check(data['name'] not in assemblies, 'Duplicate assembly: ' + data['name'])
    assemblies[data['name']] = data
check(assemblies['EasyCombat.Runtime']['references'] == [], 'Core runtime acquired an assembly dependency')
check(assemblies['EasyCombat.Editor']['references'] == ['EasyCombat.Runtime'], 'Core editor acquired an external dependency')
library_source = (ROOT/'Editor/CombatAssetLibrary.cs').read_text(encoding='utf-8-sig')
category_sizes = {}
for field in ('Types', 'Titles', 'Folders', 'Codes', 'Colors'):
    match = re.search(r'\b' + field + r'\s*=\s*\{(.*?)\};', library_source, re.S)
    check(match is not None, 'Missing Studio category array: ' + field)
    if match:
        pattern = r'typeof\s*\(' if field == 'Types' else r'new\s+Color\s*\(' if field == 'Colors' else r'"(?:\\.|[^"\\])*"'
        category_sizes[field] = len(re.findall(pattern, match.group(1)))
check(len(set(category_sizes.values())) == 1, 'Studio category arrays differ: ' + str(category_sizes))
for path in (ROOT/'Runtime').rglob('*.cs'):
    check(not re.search(r'^\s*using\s+(UnityEditor|ECM2|EasyFeedbacks|PrimeTween|UnityEngine.InputSystem)\b', path.read_text(encoding='utf-8-sig'), re.M),
          'Forbidden runtime dependency: ' + str(path.relative_to(ROOT)))
guids = {}
for section in ('Runtime', 'Editor', 'Tests'):
    for path in (ROOT/section).rglob('*'):
        if path.suffix == '.meta':
            original = path.with_suffix('')
            check(original.exists(), 'Orphan .meta: ' + str(path.relative_to(ROOT)))
            match = re.search(r'^guid: ([a-f0-9]{32})$', path.read_text(), re.M)
            check(match is not None, 'Invalid GUID: ' + str(path.relative_to(ROOT)))
            if match:
                guid = match.group(1)
                check(guid not in guids, 'Duplicate GUID: ' + str(path.relative_to(ROOT)))
                guids[guid] = path
        else:
            check(Path(str(path)+'.meta').exists(), 'Missing .meta: ' + str(path.relative_to(ROOT)))
for path in ROOT.rglob('*'):
    if '.git' in path.parts:
        continue
    check(path.name not in ('.env', 'credentials.json', '.DS_Store'), 'Unwanted file: ' + str(path.relative_to(ROOT)))
if errors:
    print('\n'.join(errors), file=sys.stderr)
    sys.exit(1)
print(f'Package integrity OK: {len(assemblies)} assemblies, {len(guids)} GUIDs, {len(manifest.get("samples", []))} samples')
