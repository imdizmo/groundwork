#!/usr/bin/env bash
# Builds a release package: scripts/package.sh <rid: linux-x64|win-x64> <version, v1.2.3> [out dir]
# The editor is published self-contained as one program (no .NET needed to run it); on Windows its native libraries
# stay beside the .exe (see the .csproj). Linux gets a .tar.gz with install-desktop-entry.sh and an AppImage; Windows
# a .zip.
set -euo pipefail
rid="${1:?rid}"; version="${2:?version}"; out="${3:-dist}"
root="$(cd "$(dirname "$0")/.." && pwd)"
if ! [[ "${version#v}" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?$ ]]; then
    echo "version must look like v1.2.3 or v1.2.3-beta.1 (got $version)" >&2
    exit 2
fi
name="Groundwork-${version}-${rid}"
mkdir -p "$out"; out="$(cd "$out" && pwd)"
stage="$out/$name"
rm -rf "${stage:?}"
dotnet publish "$root/editor/Desktop/Groundwork.Desktop.csproj" -c Release -r "$rid" --self-contained \
    -p:PublishSingleFile=true -p:DebugType=none -o "$stage"
rm -f "$stage"/*.xml "$stage"/*.pdb
cp "$root/LICENSE" "$root/NOTICE.md" "$root/CHANGELOG.md" "$stage/"
if [[ "$rid" == linux-* ]]; then
    cp "$root/scripts/install-desktop-entry.sh" "$root/editor/Desktop/Assets/icon.png" "$stage/"
fi
cd "$out"
if [[ "$rid" == win-* ]]; then
    rm -f "$name.zip"
    if command -v 7z >/dev/null; then 7z a -tzip "$name.zip" "$name" >/dev/null
    else python3 -c "import shutil,sys; shutil.make_archive(sys.argv[1], 'zip', '.', sys.argv[1])" "$name"; fi
    echo "$out/$name.zip"
else
    tar -czf "$name.tar.gz" "$name"
    echo "$out/$name.tar.gz"
fi

# Linux x64 also gets an AppImage: the same folder, plus the files an AppImage needs at its root. appimagetool and the
# runtime are pinned releases, checked by SHA-256, cached in $APPIMAGE_CACHE.
if [[ "$rid" == linux-x64 ]]; then
    cache="${APPIMAGE_CACHE:-$HOME/.cache/groundwork/appimage}"
    mkdir -p "$cache"
    fetch() { # url sha256 file
        if ! echo "$2  $cache/$3" | sha256sum -c --status 2>/dev/null; then
            curl -fsSL --retry 3 -o "$cache/$3.part" "$1"
            echo "$2  $cache/$3.part" | sha256sum -c --quiet
            mv "$cache/$3.part" "$cache/$3"
        fi
    }
    fetch https://github.com/AppImage/appimagetool/releases/download/1.9.1/appimagetool-x86_64.AppImage \
        ed4ce84f0d9caff66f50bcca6ff6f35aae54ce8135408b3fa33abfc3cb384eb0 appimagetool-1.9.1-x86_64.AppImage
    fetch https://github.com/AppImage/type2-runtime/releases/download/20251108/runtime-x86_64 \
        2fca8b443c92510f1483a883f60061ad09b46b978b2631c807cd873a47ec260d runtime-20251108-x86_64
    chmod +x "$cache/appimagetool-1.9.1-x86_64.AppImage"
    appdir="$name.AppDir"
    rm -rf "${appdir:?}"; cp -r "$name" "$appdir"
    rm -f "$appdir/install-desktop-entry.sh" "$appdir/icon.png"  # AppImage launchers add the menu entry themselves
    printf '#!/bin/sh\nexec "$(dirname "$(readlink -f "$0")")/Groundwork" "$@"\n' > "$appdir/AppRun"
    chmod +x "$appdir/AppRun"
    cp "$root/editor/Desktop/Assets/icon.png" "$appdir/groundwork.png"
    cp "$root/editor/Desktop/Assets/icon.png" "$appdir/.DirIcon"
    printf '%s\n' '[Desktop Entry]' 'Type=Application' 'Name=Groundwork' \
        'Comment=A world editor for Unreal Engine: shape the ground, paint it, place objects, live in the Unreal editor' \
        'Exec=Groundwork' 'Icon=groundwork' 'Terminal=false' 'Categories=Graphics;3DGraphics;' \
        'StartupWMClass=Groundwork' > "$appdir/groundwork.desktop"
    rm -f "$name.AppImage"
    ARCH=x86_64 APPIMAGE_EXTRACT_AND_RUN=1 "$cache/appimagetool-1.9.1-x86_64.AppImage" --no-appstream \
        --runtime-file "$cache/runtime-20251108-x86_64" "$appdir" "$name.AppImage" >&2
    rm -rf "${appdir:?}"
    echo "$out/$name.AppImage"
fi
