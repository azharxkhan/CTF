#!/usr/bin/env bash
# Provision the player host: toolkit, light desktop, per-player accounts.
# Run BEFORE lockdown-network.sh (this step needs the internet to install packages;
# lock the network down only once provisioning is done).
set -euo pipefail

if [[ $EUID -ne 0 ]]; then echo "run as root" >&2; exit 1; fi

SRC_DIR="${SRC_DIR:-/opt/ctf}"          # read-only mount: field manual + white-box source
PLAYERS_FILE="${PLAYERS_FILE:-players.txt}"

echo "== Installing toolkit and light desktop =="
apt-get update
apt-get install -y --no-install-recommends \
  xfce4 xfce4-terminal firefox-esr \
  nmap netcat-openbsd curl jq git \
  john hashcat exiftool binutils file \
  python3 python3-pip micro \
  xrdp                                   # RDP endpoint that Guacamole connects to

# hashid + jwt_tool via pip (do this now, while internet is still allowed)
pip3 install --no-cache-dir hashid jwt_tool || true

# Local rockyou for W3 / C8 (ship it with the repo or fetch here pre-lockdown)
if [[ ! -f /usr/share/wordlists/rockyou.txt ]]; then
  echo "WARNING: place rockyou.txt at /usr/share/wordlists/rockyou.txt before lockdown"
fi

echo "== Locking Firefox to internal hosts only =="
install -d /etc/firefox/policies
install -m 0644 firefox-policies.json /etc/firefox/policies/policies.json

echo "== Creating per-player accounts from $PLAYERS_FILE =="
while IFS= read -r user; do
  [[ -z "$user" || "$user" == \#* ]] && continue
  if ! id "$user" &>/dev/null; then
    # No sudo, no docker group. Locked-down shell user with a desktop.
    useradd -m -s /bin/bash "$user"
    # Force a password reset on first login; admin sets a temp password out of band.
    passwd -e "$user" || true
  fi
  chmod 700 "/home/$user"                 # players cannot read each other's homes
  # Read-only bind of field manual + challenge source into each home
  install -d "/home/$user/ctf"
  if ! mountpoint -q "/home/$user/ctf"; then
    mount --bind -o ro "$SRC_DIR" "/home/$user/ctf" || \
      echo "  (mount $SRC_DIR -> /home/$user/ctf skipped; set SRC_DIR)"
  fi
done < "$PLAYERS_FILE"

echo
echo "Provisioning done. NEXT: run ./lockdown-network.sh, then VERIFY the internet is blocked."
echo "Reminder: players get NO sudo and are NOT in the docker group — keep it that way."
