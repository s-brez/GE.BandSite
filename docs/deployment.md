# Deployment Guide

This repository now ships with `scripts/deploy.ps1`, a PowerShell helper that automates publishing to the production EC2 instance, synchronises environment variables from local `secrets.json`, runs Entity Framework Core migrations, and performs a timestamped PostgreSQL backup before each deployment.

## Prerequisites (local)
- Windows 11 or PowerShell 7 with OpenSSH (`ssh`, `scp`), `git`, and the .NET 9 SDK installed.
- SSH key converted to OpenSSH format (one-off):
  ```powershell
  ssh-keygen -p -m PEM -f "$env:USERPROFILE\.ssh\ge_band_site.pem"
  icacls "$env:USERPROFILE\.ssh\ge_band_site.pem" /inheritance:r /grant:r "$($env:USERNAME):(F)"
  ```
- Load the key into the Windows OpenSSH agent so deployments are non-interactive:
  ```powershell
  Set-Service ssh-agent -StartupType Automatic
  Start-Service ssh-agent
  ssh-add $env:USERPROFILE\.ssh\ge_band_site.pem
  ```
- User secrets file present at `%APPDATA%\Microsoft\UserSecrets\524d13f4-f0de-4587-839c-835f8aa9f4fb\secrets.json`.

## Prerequisites (remote)
- Ubuntu 24.04 LTS ARM (t4g.small) instance reachable at `<host>` with SSH user `ubuntu` and inbound TCP/80 open from the Internet.
- Security group allows `ssh` from admin network.

The script is idempotent: it installs system packages (`postgresql`, `git`, `rsync`, `curl`), downloads the .NET 9 runtime into `/usr/share/dotnet`, configures `dotnet-ef`, ensures `/srv/GE.BandSite` + `/var/www/ge-band-site`, writes the `ge-band-site.service` unit, and prepares Postgres (`ge-band-site` database; `postgres` password `pg_2f7e…`).

## Running a deployment
```powershell
pwsh ./scripts/deploy.ps1 -HostName ec2-1-2-3-4.ap-southeast-2.compute.amazonaws.com
```

Optional parameters:
- `-Branch staging` – deploy a different branch.
- `-RepositoryUrl https://github.com/<user>/GE.BandSite.git` – override Git origin (inferred automatically). SSH-style URLs are converted to HTTPS automatically; for private repos supply a PAT, e.g. `https://<token>@github.com/<user>/GE.BandSite.git`.
- `-SkipPrerequisites` – skip package/unit provisioning when already configured.
- `-SkipBackup` – bypass the pre-deploy `pg_dump` (default keeps 14 days of gzip’d dumps under `/var/backups/ge-band-site`).

The script emits service logs (`journalctl -u ge-band-site -n 100`) after restarting the app. Static media under `wwwroot/images` and `wwwroot/videos` are preserved between deployments via `rsync --exclude`.

## CloudFront and DNS routing

The existing CloudFront distribution `cf-swingtheboogie` can front both the static media (current S3 origin) and the web app:

1. **Create EC2 origin**
   - In CloudFront → *Origins*, add a new origin pointing to the instance’s public DNS (e.g. `ec2-1-2-3-4.ap-southeast-2.compute.amazonaws.com`).
   - Origin protocol policy: `HTTP only` (Kestrel listens on port 80). Leave path blank.

2. **Behaviours**
   - Set the **Default behaviour (`/*`)** to the new EC2 origin.
   - Ensure Viewer protocol policy is “Redirect HTTP to HTTPS”.
   - For dynamic pages, disable caching (set minimum, default, and maximum TTL to 0).
   - Keep the existing S3 origin for `/media/*` (or whichever prefix serves static video/photo assets). Configure an additional behaviour for that path pointing to the S3 origin.

3. **Security group**
   - Allow inbound port 80 on the EC2 instance from `0.0.0.0/0`. CloudFront uses AWS ranges, so restricting narrowly is impractical without automation.

4. **DNS (GoDaddy)**
   - Create a new CNAME record: `www` → `d2r0vyil5uhr44.cloudfront.net`.
   - GoDaddy cannot alias the apex to CloudFront; configure root-domain forwarding (permanent redirect) from `http://swingtheboogie.com` to `https://www.swingtheboogie.com` until the 60-day lock expires. Once transfers unlock, move DNS to Route 53 and create an `A` alias directly to CloudFront so the apex resolves natively.

5. **Validation**
   - After CloudFront deploys, browse `https://www.swingtheboogie.com/Contact`; ensure response headers show `Via: 1.1 cloudfront.net` and the form loads.
   - Check the CloudFront access logs or real-time metrics for cache misses to confirm routing operates as expected.

6. **Post-deployment**
   - The deployment script already runs `curl http://localhost/`, but you should also hit the CloudFront URL from a browser.
   - Monitor `journalctl -u ge-band-site` for errors and CloudFront’s “Origin health” panel for HTTP 5xx spikes.

### Route 53 (optional upgrade)
When the 60-day GoDaddy lock ends, create a Route 53 public hosted zone for `swingtheboogie.com`, update GoDaddy nameservers to the four AWS NS records, then create:

- `A` Alias (`swingtheboogie.com` → `cf-swingtheboogie` distribution).
- `A` Alias (`www` → same distribution).

This removes the reliance on GoDaddy forwarding and ensures both apex and `www` stay on CloudFront with proper TLS.
