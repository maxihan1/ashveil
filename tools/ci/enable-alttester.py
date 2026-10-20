#!/usr/bin/env python3
# QA 빌드 잡에서만 AltTester Unity SDK 패키지(OpenUPM)를 client/Packages/manifest.json 에 추가한다
"""
Release 빌드에 AltTester(GPL-3) 코드가 들어가지 않도록 저장소의 manifest 에는 넣지 않는다(decisions.md D-29).
사용: python3 tools/ci/enable-alttester.py [manifest 경로] [SDK 버전]
"""
import json
import sys
from pathlib import Path

REGISTRY = "https://package.openupm.com"
PACKAGE = "com.alttester.sdk"

path = Path(sys.argv[1] if len(sys.argv) > 1 else "client/Packages/manifest.json")
version = sys.argv[2] if len(sys.argv) > 2 else "2.3.2"
manifest = json.loads(path.read_text(encoding="utf-8"))
manifest.setdefault("dependencies", {})[PACKAGE] = version
registries = manifest.setdefault("scopedRegistries", [])
registry = next((r for r in registries if r.get("url") == REGISTRY), None)
if registry is None:
    registries.append({"name": "package.openupm.com", "url": REGISTRY, "scopes": [PACKAGE]})
elif PACKAGE not in registry.setdefault("scopes", []):
    registry["scopes"].append(PACKAGE)
path.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
print(f"{PACKAGE}@{version} 를 {path} 에 추가했습니다.")
