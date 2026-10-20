#!/usr/bin/env python3
# Unity Editor 없이 Unity 관리 DLL을 참조해 클라이언트 C# 스크립트를 컴파일만 검사하는 하네스
"""
asmdef 경계를 흉내 낸 csproj 들을 만들고 Docker 의 .NET SDK 로 빌드한다.
설정(config) 3가지를 차례로 컴파일한다.
  player-release : Android 플레이어, 정의 심볼 없음 (Release 빌드)
  player-qa      : Android 플레이어 + QA_BUILD, DEVELOPMENT_BUILD (QA 빌드)
  editor-qa      : UNITY_EDITOR + QA_BUILD + 테스트 어셈블리 + 에디터 스크립트

사용:
  UNITY_REF_DIR=<fetch-unity-refs.sh 결과> python3 tools/unity-compile-check/check.py [config ...]
"""
import os
import shutil
import subprocess
import sys
from pathlib import Path
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[2]
HERE = Path(__file__).resolve().parent
REFS = Path(os.environ.get("UNITY_REF_DIR", HERE / ".cache" / "6000.0.84f1")).resolve()
WORK = HERE / ".work"
DATA = "Editor/Data"
BUILTIN = f"{DATA}/Resources/PackageManager/BuiltInPackages"

COMMON_DEFINES = [
    "UNITY_6000_0_OR_NEWER", "UNITY_6000_0", "UNITY_2023_3_OR_NEWER", "UNITY_2023_2_OR_NEWER", "UNITY_2023_1_OR_NEWER",
    "UNITY_2022_3_OR_NEWER", "UNITY_2022_2_OR_NEWER", "UNITY_2022_1_OR_NEWER", "UNITY_2021_3_OR_NEWER",
    "UNITY_2021_2_OR_NEWER", "UNITY_2021_1_OR_NEWER", "UNITY_2020_3_OR_NEWER", "UNITY_2020_1_OR_NEWER",
    "UNITY_2019_4_OR_NEWER", "UNITY_2019_1_OR_NEWER", "UNITY_2018_4_OR_NEWER", "UNITY_5_6_OR_NEWER", "UNITY_5_3_OR_NEWER",
    "NET_STANDARD_2_1", "NET_STANDARD", "CSHARP_7_3_OR_NEWER", "ENABLE_LEGACY_INPUT_MANAGER", "UNITY_ANDROID",
    "UNITY_POST_PROCESSING_STACK_V2", "PACKAGE_PHYSICS", "PACKAGE_PHYSICS2D", "PACKAGE_ANIMATION", "PACKAGE_UITOOLKIT",
]

CONFIGS = {
    "player-release": {"defines": ["ENABLE_IL2CPP"], "editor": False},
    "player-qa": {"defines": ["ENABLE_IL2CPP", "QA_BUILD", "DEVELOPMENT_BUILD"], "editor": False},
    "editor-qa": {"defines": ["UNITY_EDITOR", "UNITY_EDITOR_64", "QA_BUILD", "UNITY_INCLUDE_TESTS", "ALTTESTER_SDK_PRESENT"],
                  "editor": True},
}

# name: (소스 폴더들, 종류, 참조, 옵션)
#  종류: pure(유니티 참조 없음) / engine / editor
ASSEMBLIES = [
    ("Ashveil.Shared", ["shared/Package/Runtime"], "pure", [], {}),
    ("Ashveil.Domain", ["client/Assets/Game/Domain"], "pure", ["Ashveil.Shared"], {}),
    ("UnityEngine.UI", [f"@{BUILTIN}/com.unity.ugui/Runtime/UGUI"], "engine", [], {"third_party": True}),
    ("UnityEngine.TestRunner", [f"@{BUILTIN}/com.unity.test-framework/UnityEngine.TestRunner"], "engine", [],
     {"third_party": True, "editor_only": True, "nunit": True}),
    ("Ashveil.Application", ["client/Assets/Game/Application"], "engine", ["Ashveil.Shared", "Ashveil.Domain"], {"newtonsoft": True}),
    ("Ashveil.Presentation", ["client/Assets/Game/Presentation"], "engine",
     ["Ashveil.Shared", "Ashveil.Domain", "Ashveil.Application", "UnityEngine.UI"], {"newtonsoft": True}),
    ("Ashveil.Editor", ["client/Assets/Game/Editor"], "editor",
     ["Ashveil.Shared", "Ashveil.Domain", "Ashveil.Application", "Ashveil.Presentation", "UnityEngine.UI"],
     {"editor_only": True, "exclude": ["client/Assets/Game/Editor/AltTester"]}),
    ("AltTesterStubs", ["tools/unity-compile-check/stubs"], "editor", [], {"editor_only": True, "third_party": True}),
    ("Ashveil.Editor.AltTester", ["client/Assets/Game/Editor/AltTester"], "editor", ["Ashveil.Editor", "AltTesterStubs"],
     {"editor_only": True}),
    ("Ashveil.Tests.EditMode", ["client/Assets/Tests/EditMode"], "editor",
     ["Ashveil.Shared", "Ashveil.Domain", "Ashveil.Application", "UnityEngine.TestRunner"],
     {"editor_only": True, "nunit": True, "newtonsoft": True}),
    ("Ashveil.Tests.PlayMode", ["client/Assets/Tests/PlayMode"], "engine",
     ["Ashveil.Shared", "Ashveil.Domain", "Ashveil.Application", "Ashveil.Presentation", "UnityEngine.UI", "UnityEngine.TestRunner"],
     {"editor_only": True, "nunit": True, "newtonsoft": True}),
]


def unity_dlls(kind):
    managed = REFS / DATA / "Managed" / "UnityEngine"
    dlls = sorted(managed.glob("UnityEngine*.dll"))
    if kind == "editor":
        dlls += sorted(managed.glob("UnityEditor*.dll"))
        dlls += [p for p in (REFS / DATA / "Managed").glob("Unity*.dll") if p.name.startswith(("UnityEditor", "Unity.Legacy"))]
    return [d for d in dlls if d.name != "UnityEngine.dll" and d.name != "UnityEditor.dll"] + \
           [managed / "UnityEngine.dll"] + ([managed / "UnityEditor.dll"] if kind == "editor" else [])


def source_files(folders, excludes):
    files = []
    for folder in folders:
        base = REFS / folder[1:] if folder.startswith("@") else ROOT / folder
        if not base.exists():
            continue
        for path in sorted(base.rglob("*.cs")):
            rel = str(path)
            if any(str((ROOT / e).resolve()) in rel for e in excludes):
                continue
            files.append(path)
    return files


def write_csproj(name, folders, kind, refs, opts, config):
    cfg = CONFIGS[config]
    defines = COMMON_DEFINES + cfg["defines"]
    files = source_files(folders, opts.get("exclude", []))
    lines = [
        '<Project Sdk="Microsoft.NET.Sdk">',
        "  <PropertyGroup>",
        "    <TargetFramework>netstandard2.1</TargetFramework>",
        f"    <LangVersion>9.0</LangVersion>",
        "    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>",
        "    <Nullable>disable</Nullable>",
        "    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>",
        f"    <AssemblyName>{name}</AssemblyName>",
        f"    <DefineConstants>{';'.join(defines)}</DefineConstants>",
        "    <NoWarn>CS0618;CS0649;CS0414;CS0169;CS0067;CS1701;CS1702;CS0436;MSB3277;CS8632;NU1701</NoWarn>",
        f"    <TreatWarningsAsErrors>{'false' if opts.get('third_party') else 'true'}</TreatWarningsAsErrors>",
        "    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>",
        "    <ProduceReferenceAssembly>false</ProduceReferenceAssembly>",
        "  </PropertyGroup>",
        "  <ItemGroup>",
    ]
    for f in files:
        lines.append(f'    <Compile Include="{escape(str(f))}" />')
    lines.append("  </ItemGroup>")
    lines.append("  <ItemGroup>")
    if kind != "pure":
        # 에디터에서는 런타임 어셈블리도 UnityEditor 를 참조해 컴파일된다(#if UNITY_EDITOR 블록).
        for dll in unity_dlls("editor" if cfg["editor"] else kind):
            lines.append(f'    <Reference Include="{dll.stem}"><HintPath>{escape(str(dll))}</HintPath><Private>false</Private></Reference>')
    if opts.get("nunit"):
        nunit = REFS / BUILTIN / "com.unity.ext.nunit/net40/unity-custom/nunit.framework.dll"
        lines.append(f'    <Reference Include="nunit.framework"><HintPath>{escape(str(nunit))}</HintPath><Private>false</Private></Reference>')
    if opts.get("newtonsoft"):
        lines.append('    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />')
    for r in refs:
        lines.append(f'    <ProjectReference Include="../{r}/{r}.csproj" />')
    lines.append("  </ItemGroup>")
    lines.append("</Project>")
    out = WORK / config / name
    out.mkdir(parents=True, exist_ok=True)
    (out / f"{name}.csproj").write_text("\n".join(lines), encoding="utf-8")
    return len(files)


def run_editmode_tests():
    """EditMode 테스트 중 Unity 엔진 호출이 없는 것(Domain·Application)을 일반 .NET + NUnit 으로 실제 실행한다."""
    sources = []
    for folder in ["shared/Package/Runtime", "client/Assets/Game/Domain", "client/Assets/Game/Application", "client/Assets/Tests/EditMode"]:
        sources += sorted((ROOT / folder).rglob("*.cs"))
    managed = REFS / DATA / "Managed" / "UnityEngine"
    refs = [managed / "UnityEngine.CoreModule.dll", managed / "UnityEngine.UnityWebRequestModule.dll", managed / "UnityEngine.dll"]
    lines = [
        '<Project Sdk="Microsoft.NET.Sdk">',
        "  <PropertyGroup>",
        "    <TargetFramework>net10.0</TargetFramework>",
        "    <LangVersion>9.0</LangVersion>",
        "    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>",
        "    <Nullable>disable</Nullable>",
        "    <IsPackable>false</IsPackable>",
        "    <NoWarn>CS0618;CS1701;CS1702;MSB3277;NU1701;CS8632</NoWarn>",
        "    <DefineConstants>UNITY_INCLUDE_TESTS</DefineConstants>",
        "  </PropertyGroup>",
        "  <ItemGroup>",
        '    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />',
        '    <PackageReference Include="NUnit" Version="3.14.0" />',
        '    <PackageReference Include="NUnit3TestAdapter" Version="4.6.0" />',
        '    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />',
    ]
    lines += [f'    <Compile Include="{escape(str(s))}" />' for s in sources]
    lines += [f'    <Reference Include="{d.stem}"><HintPath>{escape(str(d))}</HintPath></Reference>' for d in refs]
    lines += ["  </ItemGroup>", "</Project>"]
    out = WORK / "editmode-run"
    out.mkdir(parents=True, exist_ok=True)
    (out / "EditModeRunner.csproj").write_text("\n".join(lines), encoding="utf-8")
    print("== editmode-run (Unity 엔진 없이 실행)", flush=True)
    result = subprocess.run([
        "docker", "run", "--rm",
        "-v", f"{ROOT}:{ROOT}", "-v", f"{REFS}:{REFS}", "-v", f"{WORK}:/work",
        "-v", "ashveil-nuget:/root/.nuget/packages",
        "-e", "DOTNET_CLI_TELEMETRY_OPTOUT=1", "-e", "DOTNET_NOLOGO=1",
        "mcr.microsoft.com/dotnet/sdk:10.0", "bash", "-c",
        "cd /work/editmode-run && dotnet test -nologo -v q 2>&1 | grep -E 'error|Failed|Passed!|Failed!|Total tests' | sort -u; exit ${PIPESTATUS[0]}",
    ])
    if result.returncode != 0:
        sys.exit("EditMode 테스트 실패")


def main():
    if sys.argv[1:] == ["editmode-run"]:
        run_editmode_tests()
        return
    configs = sys.argv[1:] or list(CONFIGS)
    if not (REFS / DATA / "Managed").exists():
        sys.exit(f"Unity 참조 DLL이 없습니다: {REFS}\n먼저 tools/unity-compile-check/fetch-unity-refs.sh 를 실행하세요.")
    shutil.rmtree(WORK, ignore_errors=True)
    failed = []
    for config in configs:
        editor = CONFIGS[config]["editor"]
        names = []
        for name, folders, kind, refs, opts in ASSEMBLIES:
            if opts.get("editor_only") and not editor:
                continue
            count = write_csproj(name, folders, kind, refs, opts, config)
            if count:
                names.append(name)
        # 빈 어셈블리를 참조하는 경우를 막기 위해 소스가 없는 프로젝트는 솔루션에서 빼고 참조도 지운다.
        sln_projects = " ".join(f"{n}/{n}.csproj" for n in names)
        for n in names:
            proj = WORK / config / n / f"{n}.csproj"
            text = proj.read_text(encoding="utf-8")
            for name, *_ in ASSEMBLIES:
                if name not in names:
                    text = text.replace(f'    <ProjectReference Include="../{name}/{name}.csproj" />\n', "")
            proj.write_text(text, encoding="utf-8")
        print(f"== {config}: {', '.join(names)}", flush=True)
        cmd = (
            f"cd /work/{config} && dotnet new sln -n all >/dev/null 2>&1; "
            f"dotnet sln all.slnx add {sln_projects} >/dev/null && "
            "dotnet build all.slnx -nologo -v q -clp:NoSummary 2>&1 | grep -E 'error|warning CS' | sort -u; "
            "exit ${PIPESTATUS[0]}"
        )
        result = subprocess.run([
            "docker", "run", "--rm",
            "-v", f"{ROOT}:{ROOT}", "-v", f"{REFS}:{REFS}", "-v", f"{WORK}:/work",
            "-v", "ashveil-nuget:/root/.nuget/packages",
            "-e", "DOTNET_CLI_TELEMETRY_OPTOUT=1", "-e", "DOTNET_NOLOGO=1",
            "mcr.microsoft.com/dotnet/sdk:10.0", "bash", "-c", cmd,
        ])
        if result.returncode != 0:
            failed.append(config)
    if failed:
        sys.exit(f"컴파일 실패: {', '.join(failed)}")
    print("모든 설정 컴파일 성공")


if __name__ == "__main__":
    main()
