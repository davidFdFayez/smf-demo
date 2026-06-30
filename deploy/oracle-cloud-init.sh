#!/bin/bash
# Paste this into Oracle Cloud → Create Instance → "Cloud-init script" (Advanced options).
# Replace YOUR_GITHUB_REPO_URL before pasting, e.g. https://github.com/you/smf-demo.git
#
# After VM boots (~5 min), your site is live at:
#   https://YOUR-PUBLIC-IP-WITH-DASHES.sslip.io/watch?match=match-002

set -euo pipefail
REPO_URL="${SMF_REPO_URL:-YOUR_GITHUB_REPO_URL}"

export DEBIAN_FRONTEND=noninteractive
apt-get update -qq
apt-get install -y ca-certificates curl gnupg git rsync unzip

if ! command -v docker >/dev/null 2>&1; then
  install -m 0755 -d /etc/apt/keyrings
  curl -fsSL https://download.docker.com/linux/ubuntu/gpg | gpg --dearmor -o /etc/apt/keyrings/docker.gpg
  chmod a+r /etc/apt/keyrings/docker.gpg
  echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo "$VERSION_CODENAME") stable" > /etc/apt/sources.list.d/docker.list
  apt-get update -qq
  apt-get install -y docker-ce docker-ce-cli containerd.io docker-compose-plugin
fi

mkdir -p /opt/smf
if [ "$REPO_URL" != "YOUR_GITHUB_REPO_URL" ]; then
  git clone --depth 1 "$REPO_URL" /opt/smf
else
  echo "ERROR: Set SMF_REPO_URL or edit REPO_URL in this script before pasting." > /var/log/smf-cloud-init.err
  exit 1
fi

bash /opt/smf/deploy/bootstrap-vps.sh /opt/smf
echo "SMF cloud deploy finished" >> /var/log/smf-cloud-init.log
