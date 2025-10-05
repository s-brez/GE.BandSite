# Requires: PowerShell 7+, OpenSSH (ssh/scp), git
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$HostName,

    [string]$Branch = "master",

    [string]$SshUser = "ubuntu",

    [string]$KeyPath = "$env:USERPROFILE\.ssh\ge_band_site.pem",

    [string]$RepositoryUrl,

    [switch]$SkipPrerequisites,

    [switch]$SkipBackup
)

set-strictmode -version latest
$ErrorActionPreference = "Stop"

function Write-UnixFile {
    param(
        [string]$Path,
        [string]$Content
    )

    $encoding = New-Object System.Text.UTF8Encoding($false)
    $normalized = $Content -replace "`r?`n", "`n"
    [System.IO.File]::WriteAllText($Path, $normalized, $encoding)
}

function Assert-Tool {
    param([string]$Name)
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required tool '$Name' was not found on PATH."
    }
}

Assert-Tool -Name "ssh"
Assert-Tool -Name "scp"
Assert-Tool -Name "git"
Assert-Tool -Name "dotnet"

if (-not $RepositoryUrl) {
    $RepositoryUrl = (git config --get remote.origin.url).Trim()
    if (-not $RepositoryUrl) {
        throw "Unable to infer repository URL. Pass -RepositoryUrl explicitly."
    }
}

if (-not (Test-Path -Path $KeyPath -PathType Leaf)) {
    throw "SSH key not found at '$KeyPath'."
}

$secretsPath = Join-Path ([Environment]::GetFolderPath('ApplicationData')) "Microsoft\UserSecrets\524d13f4-f0de-4587-839c-835f8aa9f4fb\secrets.json"
if (-not (Test-Path -Path $secretsPath -PathType Leaf)) {
    throw "User secrets file not found at '$secretsPath'."
}

function Flatten-Secrets {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Node,
        [string]$Prefix,
        [System.Collections.Generic.Dictionary[string,string]]$Accumulator
    )

    switch ($Node) {
        { $_ -is [System.Collections.IDictionary] } {
            foreach ($key in $_.Keys) {
                $value = $_[$key]
                $childPrefix = if ($Prefix) { "$Prefix__$key" } else { [string]$key }
                Flatten-Secrets -Node $value -Prefix $childPrefix -Accumulator $Accumulator
            }
        }
        { $_ -is [System.Collections.IEnumerable] -and -not ($_ -is [string]) } {
            $index = 0
            foreach ($item in $_) {
                $childPrefix = if ($Prefix) { "$Prefix__$index" } else { [string]$index }
                Flatten-Secrets -Node $item -Prefix $childPrefix -Accumulator $Accumulator
                $index++
            }
        }
        default {
            if ($null -ne $Node -and $Prefix) {
                $value = switch ($Node) {
                    { $_ -is [bool] } { if ($_ ) { "true" } else { "false" } }
                    default { [string]$_ }
                }
                $Accumulator[$Prefix] = $value
            }
        }
    }
}

$secretsJson = Get-Content -LiteralPath $secretsPath -Raw | ConvertFrom-Json -Depth 100
$flat = [System.Collections.Generic.Dictionary[string,string]]::new([StringComparer]::OrdinalIgnoreCase)
Flatten-Secrets -Node $secretsJson -Accumulator $flat -Prefix ""

# Normalise contact notification recipients (support legacy ToAddress or array)
$recipientValues = @()
if ($flat.ContainsKey("ContactNotifications__ToAddresses__0")) {
    $keys = $flat.Keys | Where-Object { $_ -like "ContactNotifications__ToAddresses__*" }
    foreach ($key in $keys) {
        $recipientValues += $flat[$key]
    }
} elseif ($flat.ContainsKey("ContactNotifications__ToAddresses")) {
    $recipientValues += $flat["ContactNotifications__ToAddresses"]
} elseif ($flat.ContainsKey("ContactNotifications__ToAddress")) {
    $recipientValues += $flat["ContactNotifications__ToAddress"]
}

if ($recipientValues.Count -gt 0) {
    $flat.Remove("ContactNotifications__ToAddress") | Out-Null
    $index = 0
    foreach ($recipient in ($recipientValues | Where-Object { $_ -and $_.Trim() -ne "" })) {
        $flat["ContactNotifications__ToAddresses__${index}"] = $recipient.Trim()
        $index++
    }
}

function Set-IfPresent {
    param(
        [string]$Key,
        [string]$Value
    )
    if ($Value) {
        $flat[$Key] = $Value
    }
}

if ($flat.ContainsKey("AWS__AccessKey")) {
    $ak = $flat["AWS__AccessKey"]
    Set-IfPresent -Key "AWS_ACCESS_KEY_ID" -Value $ak
    Set-IfPresent -Key "AWS_SES_ACCESS_KEY_ID" -Value $ak
}

if ($flat.ContainsKey("AWS__SecretKey")) {
    $sk = $flat["AWS__SecretKey"]
    Set-IfPresent -Key "AWS_SECRET_ACCESS_KEY" -Value $sk
    Set-IfPresent -Key "AWS_SES_SECRET_ACCESS_KEY" -Value $sk
}

if ($flat.ContainsKey("AWS__Region")) {
    $region = $flat["AWS__Region"]
    Set-IfPresent -Key "AWS_REGION" -Value $region
    Set-IfPresent -Key "AWS_SES_REGION" -Value $region
}

if ($flat.ContainsKey("ContactNotifications__Enabled")) {
    $flat["CONTACT_NOTIFICATIONS_ENABLED"] = $flat["ContactNotifications__Enabled"]
}

if ($flat.ContainsKey("ContactNotifications__FromAddress")) {
    $flat["CONTACT_NOTIFICATIONS_FROM_ADDRESS"] = $flat["ContactNotifications__FromAddress"]
}

if ($flat.ContainsKey("ContactNotifications__Subject")) {
    $flat["CONTACT_NOTIFICATIONS_SUBJECT"] = $flat["ContactNotifications__Subject"]
}

if ($recipientValues.Count -gt 0) {
    $joined = ($recipientValues | Where-Object { $_ -and $_.Trim() -ne "" } | ForEach-Object { $_.Trim() }) -join ';'
    if ($joined) {
        $flat["CONTACT_NOTIFICATIONS_RECIPIENTS"] = $joined
    }
}

function ConvertTo-EnvContent {
    param([System.Collections.Generic.Dictionary[string,string]]$Data)
    $builder = New-Object System.Text.StringBuilder
    foreach ($key in ($Data.Keys | Sort-Object)) {
        $value = $Data[$key]
        if ($null -eq $value) {
            continue
        }
        $escaped = $value.Replace('\', '\\').Replace('"', '\"')
        $line = '{0}="{1}"' -f $key, $escaped
        [void]$builder.AppendLine($line)
    }
    return $builder.ToString()
}

$envContent = ConvertTo-EnvContent -Data $flat
$tempEnv = New-TemporaryFile
Write-UnixFile -Path $tempEnv -Content $envContent

$knownHosts = Join-Path $env:USERPROFILE ".ssh\known_hosts"
if (-not (Test-Path $knownHosts)) {
    $null = New-Item -ItemType File -Path $knownHosts -Force
}
$sshOptions = @("-i", $KeyPath, "-o", "StrictHostKeyChecking=no", "-o", "UserKnownHostsFile=$knownHosts", "-o", "ForwardAgent=yes")
$scpArgs = $sshOptions
$sshArgs = $sshOptions + ("${SshUser}@${HostName}")

Write-Host "Copying environment file..."
& scp @scpArgs $tempEnv "${SshUser}@${HostName}:/tmp/app.env"

function Invoke-RemoteScript {
    param(
        [string]$Content,
        [string]$RemoteName
    )

    $localTemp = New-TemporaryFile
    Write-UnixFile -Path $localTemp -Content $Content
    & scp @scpArgs $localTemp "${SshUser}@${HostName}:/tmp/$RemoteName"
    Remove-Item -LiteralPath $localTemp
    & ssh @sshArgs "chmod +x /tmp/$RemoteName && bash /tmp/$RemoteName && rm /tmp/$RemoteName"
}

$repositoryUrlEscaped = $RepositoryUrl.Replace("'", "'\''")

$preflightTemplate = @'
#!/usr/bin/env bash
set -euo pipefail

REPO_URL='__REPO_URL__'

export DEBIAN_FRONTEND=noninteractive

if ! command -v dotnet >/dev/null 2>&1; then
  wget -q https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
  sudo dpkg -i packages-microsoft-prod.deb >/dev/null
  rm packages-microsoft-prod.deb
fi

sudo apt-get update -y
sudo apt-get install -y postgresql git rsync curl

if ! /usr/share/dotnet/dotnet --info >/dev/null 2>&1; then
  sudo bash -c 'curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 9.0 --install-dir /usr/share/dotnet --no-path'
fi

if [ ! -f /etc/profile.d/dotnet-install.sh ]; then
  sudo tee /etc/profile.d/dotnet-install.sh >/dev/null <<'PROFILE'
export DOTNET_ROOT=/usr/share/dotnet
export PATH="$DOTNET_ROOT:$DOTNET_ROOT/tools:$PATH"
PROFILE
fi

if [ ! -f /usr/bin/dotnet ]; then
  sudo ln -sf /usr/share/dotnet/dotnet /usr/bin/dotnet
fi

export DOTNET_ROOT=/usr/share/dotnet
export PATH="$DOTNET_ROOT:$DOTNET_ROOT/tools:$PATH"

dotnet tool update --global dotnet-ef >/dev/null 2>&1 || dotnet tool install --global dotnet-ef >/dev/null

if [ ! -f /etc/profile.d/dotnet-tools.sh ]; then
  echo 'export PATH="$PATH:$HOME/.dotnet/tools"' | sudo tee /etc/profile.d/dotnet-tools.sh >/dev/null
fi

sudo mkdir -p /etc/ge-band-site
sudo mkdir -p /var/backups/ge-band-site
sudo chown postgres:postgres /var/backups/ge-band-site
sudo mkdir -p /var/www/ge-band-site/wwwroot/images
sudo mkdir -p /var/www/ge-band-site/wwwroot/videos
sudo chown -R www-data:www-data /var/www/ge-band-site

sudo mkdir -p /app
if [ ! -d /app/GE.BandSite ]; then
  sudo mkdir -p /app/GE.BandSite
  sudo chown __SSH_USER__:__SSH_USER__ /app/GE.BandSite
fi

mkdir -p "$HOME/.ssh"
chmod 700 "$HOME/.ssh"
ssh-keyscan -H github.com 2>/dev/null | sort -u | tee -a "$HOME/.ssh/known_hosts" >/dev/null

if [ ! -d /app/GE.BandSite/.git ]; then
  git clone "__REPO_URL__" /app/GE.BandSite
else
  cd /app/GE.BandSite
  git remote set-url origin "__REPO_URL__"
fi

if [ ! -f /etc/systemd/system/ge-band-site.service ]; then
  sudo tee /etc/systemd/system/ge-band-site.service >/dev/null <<'UNIT'
[Unit]
Description=Swing The Boogie website
After=network.target postgresql.service

[Service]
WorkingDirectory=/var/www/ge-band-site
ExecStart=/usr/bin/dotnet /var/www/ge-band-site/GE.BandSite.Server.dll
EnvironmentFile=/etc/ge-band-site/app.env
Restart=on-failure
RestartSec=5
User=www-data
Group=www-data

[Install]
WantedBy=multi-user.target
UNIT
  sudo systemctl daemon-reload
  sudo systemctl enable ge-band-site >/dev/null
fi

sudo -u postgres psql -tc "SELECT 1 FROM pg_database WHERE datname = 'ge-band-site'" | grep -q 1 || sudo -u postgres createdb ge-band-site
sudo -u postgres psql -c "ALTER USER postgres WITH PASSWORD 'pg_2f7e8a1b3c9d4h5j6k0l2m7n8p1q3r4s';" >/dev/null
'@

$preflightScript = $preflightTemplate.Replace('__REPO_URL__', $repositoryUrlEscaped).Replace('__SSH_USER__', $SshUser)

if (-not $SkipPrerequisites) {
    Write-Host "Running remote preflight..."
    Invoke-RemoteScript -Content $preflightScript -RemoteName "ge-preflight.sh"
}

$skipBackupLiteral = if ($SkipBackup) { "true" } else { "false" }

$deployTemplate = @'
#!/usr/bin/env bash
set -euo pipefail

export DOTNET_ROOT=/usr/share/dotnet
export PATH="$DOTNET_ROOT:$DOTNET_ROOT/tools:$PATH:$HOME/.dotnet/tools"
set -a
source /tmp/app.env
set +a

REPO_URL='__REPO_URL__'
BRANCH='__BRANCH__'
SKIP_BACKUP='__SKIP_BACKUP__'

cd /app/GE.BandSite
git fetch origin
if git show-ref --verify --quiet "refs/heads/$BRANCH"; then
  git checkout $BRANCH
else
  git checkout -b $BRANCH origin/$BRANCH
fi
git reset --hard origin/$BRANCH

dotnet restore

if [ "$SKIP_BACKUP" != "true" ]; then
  ts=$(date +%Y%m%d%H%M%S)
  sudo -u postgres pg_dump ge-band-site | gzip > /tmp/ge-band-site-${ts}.sql.gz
  sudo mv /tmp/ge-band-site-${ts}.sql.gz /var/backups/ge-band-site/ge-band-site-${ts}.sql.gz
  sudo chown postgres:postgres /var/backups/ge-band-site/ge-band-site-${ts}.sql.gz
  sudo find /var/backups/ge-band-site -type f -name 'ge-band-site-*.sql.gz' -mtime +14 -delete
fi

dotnet ef database update --project GE.BandSite.Server --context GeBandSiteDbContext

dotnet publish GE.BandSite.Server -c Release -r linux-arm64 --self-contained false -o /tmp/publish

sudo rsync -a --delete --exclude 'wwwroot/images/' --exclude 'wwwroot/videos/' /tmp/publish/ /var/www/ge-band-site/

sudo mv /tmp/app.env /etc/ge-band-site/app.env
sudo chown www-data:www-data /etc/ge-band-site/app.env
sudo chmod 640 /etc/ge-band-site/app.env

sudo chown -R www-data:www-data /var/www/ge-band-site

sudo systemctl restart ge-band-site

sudo rm -rf /tmp/publish
'@

$deployScript = $deployTemplate.Replace('__REPO_URL__', $repositoryUrlEscaped).Replace('__BRANCH__', $Branch).Replace('__SKIP_BACKUP__', $skipBackupLiteral)

Write-Host "Deploying application..."
Invoke-RemoteScript -Content $deployScript -RemoteName "ge-deploy.sh"

$verifyScript = @"
#!/usr/bin/env bash
set -euo pipefail

sudo systemctl status ge-band-site --no-pager
curl -sf http://localhost/ -o /dev/null
sudo journalctl -u ge-band-site -n 100 --no-pager
"@

Write-Host "Verifying service status..."
Invoke-RemoteScript -Content $verifyScript -RemoteName "ge-verify.sh"

Remove-Item -LiteralPath $tempEnv -ErrorAction SilentlyContinue

Write-Host "Deployment complete." -ForegroundColor Green
