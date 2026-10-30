#!/usr/bin/env bash
# Sustituye los perfiles del emulador (o móvil conectado) por los de prueba: Lucía (personal) y
# Álex (entrenador, con cinco clientes; Lucía es una de ellas). BORRA los perfiles que haya. Ver tools/SteelProgress.DemoData.
#
# Uso, desde la raíz del repo y con la app instalada y abierta al menos una vez:
#   bash tools/demo-data.sh
set -euo pipefail
export MSYS_NO_PATHCONV=1 # Git Bash: que no reescriba las rutas del dispositivo

ADB="${ADB:-/c/Program Files (x86)/Android/android-sdk/platform-tools/adb.exe}"
PKG=com.steelprogress.app
DB=files/steelprogress.db
LOCAL="$(mktemp -d)/steelprogress.db"

"$ADB" shell am force-stop "$PKG"
# Que todo lo pendiente del WAL pase al archivo antes de copiarlo
"$ADB" shell "run-as $PKG sqlite3 $DB 'PRAGMA wal_checkpoint(TRUNCATE);'" > /dev/null
"$ADB" exec-out run-as "$PKG" cat "$DB" > "$LOCAL"

# dotnet y adb son de Windows: en Git Bash necesitan la ruta en formato Windows
LOCAL_WIN="$LOCAL"
command -v cygpath > /dev/null && LOCAL_WIN="$(cygpath -w "$LOCAL")"
dotnet run --project "$(dirname "$0")/SteelProgress.DemoData" -- "$LOCAL_WIN"

"$ADB" push "$LOCAL_WIN" /data/local/tmp/steelprogress-demo.db > /dev/null
"$ADB" shell "run-as $PKG sh -c 'cp /data/local/tmp/steelprogress-demo.db $DB && rm -f $DB-wal $DB-shm files/profiles/*'"
"$ADB" shell rm -f /data/local/tmp/steelprogress-demo.db
rm -f "$LOCAL"
echo "Datos de prueba cargados. Abre la app."
