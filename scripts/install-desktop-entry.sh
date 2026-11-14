#!/usr/bin/env bash
# Adds Groundwork to the app menu for the current user, with its icon, so it can be searched for and pinned.
# Run it from the extracted release folder; run it again after moving the folder. --remove takes the entry out.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
data="${XDG_DATA_HOME:-$HOME/.local/share}"
entry="$data/applications/groundwork.desktop"
icon="$data/icons/hicolor/256x256/apps/groundwork.png"

if [[ "${1:-}" == "--remove" ]]; then
    rm -f -- "${entry:?}" "${icon:?}"
    echo "Removed Groundwork from the app menu."
    exit 0
fi

[[ -x "$here/Groundwork" && -f "$here/icon.png" ]] || { echo "Run this from the Groundwork folder (Groundwork and icon.png not found in $here)." >&2; exit 1; }
# ponytail: paths with characters the desktop-entry Exec key would need escaped are refused, not escaped
if [[ "$here" == *[\"\`\$\\%]* || "$here" == *$'\n'* ]]; then
    echo "The folder path contains \" \` \$ \\ % or a line break; move Groundwork to a plainer path and run this again." >&2
    exit 1
fi

mkdir -p "$(dirname "$entry")" "$(dirname "$icon")"
install -m 644 "$here/icon.png" "$icon"
cat > "$entry" <<EOF
[Desktop Entry]
Type=Application
Name=Groundwork
Comment=A world editor for Unreal Engine: shape the ground, paint it, place objects, live in the Unreal editor
Exec="$here/Groundwork"
TryExec=$here/Groundwork
Icon=groundwork
Terminal=false
Categories=Graphics;3DGraphics;
StartupWMClass=Groundwork
EOF
command -v update-desktop-database >/dev/null && update-desktop-database -q "$(dirname "$entry")" || true
echo "Added Groundwork to the app menu ($entry)."
