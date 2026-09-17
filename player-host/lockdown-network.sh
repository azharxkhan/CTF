#!/usr/bin/env bash
# Block all outbound internet from the player host, allowing only:
#   - DNS to the local resolver
#   - traffic to the internal CTF network (CTFd + challenge endpoints)
#   - loopback
# This is the anti-AI-cheat boundary. Run it LAST and VERIFY it holds.
#
# Assumes the internal CTF services are reachable on one CIDR. Adjust INTERNAL_CIDR
# and CTFD_HOST to match your deployment. Uses nftables.
set -euo pipefail

INTERNAL_CIDR="${INTERNAL_CIDR:-10.10.0.0/16}"   # CTFd + challenge Docker network
LOCAL_DNS="${LOCAL_DNS:-10.10.0.2}"              # internal resolver only

if [[ $EUID -ne 0 ]]; then echo "run as root" >&2; exit 1; fi

nft flush ruleset

nft add table inet ctf
nft add chain inet ctf output '{ type filter hook output priority 0; policy drop; }'

# Loopback
nft add rule inet ctf output oif lo accept
# Established/related return traffic
nft add rule inet ctf output ct state established,related accept
# DNS to the internal resolver only
nft add rule inet ctf output ip daddr $LOCAL_DNS udp dport 53 accept
nft add rule inet ctf output ip daddr $LOCAL_DNS tcp dport 53 accept
# The internal CTF network (scoreboard + challenges)
nft add rule inet ctf output ip daddr $INTERNAL_CIDR accept
# Everything else (i.e. the internet, incl. every AI service) is DROPPED by policy.

echo "Lockdown applied. Outbound internet is now blocked."
echo
echo "VERIFY before trusting it — from a *player* account, all of these must FAIL/time out:"
echo "  curl -m 5 https://example.com        # must time out"
echo "  curl -m 5 https://chatgpt.com        # must time out"
echo "  curl -m 5 https://claude.ai          # must time out"
echo "  getent hosts openai.com              # resolution alone is fine; the connect must fail"
echo
echo "And this must SUCCEED (reach the scoreboard):"
echo "  curl -m 5 http://ctfd.internal:8000  # must connect"
