# Operations Runbook

## Routine Deployment
- Run `pwsh ./scripts/deploy.ps1 -HostName <ec2-dns>` from the repo root. The script synchronises secrets, runs migrations, publishes, and restarts the systemd unit; it also captures a timestamped `pg_dump` (keeps 14 days by default).
- Watch the script output: it streams the last 100 lines from `journalctl -u ge-band-site` after restart. Cancel if errors appear and roll back via `git checkout <prev commit>` followed by another deployment.
- Issue an `aws cloudfront create-invalidation --distribution-id <id> --paths "/index.html"` when public Razor Pages contain content updates. Static assets that ship with hashed filenames do not require invalidations.
- Smoke test CloudFront (`https://www.swingtheboogie.com`) for `/`, `/Media`, `/Admin`, and the contact form. Check application logs for warnings and watch the CloudFront dashboard for origin errors.

## Rollback Procedure
- Keep the previously deployed build in `/var/www/ge-band-site/releases/<timestamp>` with a matching systemd unit. To roll back, switch the active symlink to the prior release, restart the service, and invalidate `/index.html` on CloudFront.
- Restore the PostgreSQL data volume snapshot taken automatically by nightly EBS snapshots if the database schema regresses. Mount the previous snapshot to a recovery instance to export tables if a point-in-time restore is required.
- Disable admin access by rotating JWT signing keys via Parameter Store if a regression affects authentication. Re-enable after verifying the reverted release.

## Nightly Database Backups
- Backups run through `DatabaseBackupHostedService` at `DatabaseBackup:RunAtUtc` (default 03:00 UTC). The service executes `pg_dump` using the configured connection string and uploads the archive to S3 at `<BucketName>/<KeyPrefix>/<yyyy>/<MM>/ge-band-site-<timestamp>.dump`.
- Configure S3 destination by setting the following environment variables or `appsettings` values before deployment:
  - `DatabaseBackup__Enabled=true`
  - `DatabaseBackup__BucketName=<production-backup-bucket>`
  - `DatabaseBackup__KeyPrefix=backups/database`
  - `DatabaseBackup__RetentionDays=30`
- To force a manual backup, temporarily set `DatabaseBackup__RunAtUtc` to the current UTC time rounded to the next minute, restart the service, and monitor `Logs/ge_band_site_*.log` for the "pg_dump backup completed successfully" entry. Revert the scheduled time immediately after the manual run completes.
- Verify backup retention weekly: list objects with `aws s3 ls s3://<bucket>/backups/database/ --recursive`. Files older than 30 days should be missing; investigate the hosted service logs if older artifacts remain.
- Ensure the S3 bucket enforces default encryption and lifecycle rules mirror the 30-day retention target to guard against manual uploads that bypass the hosted service.

## Log Retention
- Serilog writes structured logs to `Logs/ge_band_site_<date>.log` with a retention window controlled by `Logging:RetainedFileCount`. Production defaults to 30 files (≈ one month). Adjust via `Logging__RetainedFileCount` when disk pressure occurs.
- Forward logs to CloudWatch Logs if centralized retention is required. Until that integration is in place, archive the log directory weekly to S3 before pruning local copies beyond the configured count.

## Media Processing (ffmpeg)
- The video pipeline depends on `ffmpeg` available via the platform-specific settings (`MediaProcessing:FfmpegPathWindows` / `MediaProcessing:FfmpegPathUnix`, falling back to `MediaProcessing:FfmpegPath`). Confirm the binary is present with `ffmpeg -version`; install from the distribution repository for the EC2 AMI if missing.
- When updating ffmpeg, restart the application service to ensure the hosted background worker picks up the new binary path. Run an admin video upload on staging to verify transcodes succeed end-to-end.
- Troubleshoot failures by reviewing logs emitted by `MediaProcessingHostedService`; errors surface the command line and exception details.
- On application startup the media bootstrapper scans the configured S3 prefixes and inserts missing `MediaAsset` rows. Legacy `.mov` sources are added with `Pending` state so the transcode worker can regenerate MP4 playback files automatically.
- To regenerate MP4 playbacks for existing `.mov` uploads, list raw objects under `uploads/raw/` in S3, locate the corresponding records in `MediaAssets`, set `ProcessingState` back to `Pending`, and allow the hosted service to reprocess them. Confirm new `.mp4` files appear under the playback prefix before toggling publication flags.

## Email Notifications (SES)
- Contact form notifications use Amazon SES in `us-east-1`. Verify the sending identity remains in production mode and monitor bounce/complaint metrics using the SES console (see [SES sending activity monitoring](https://docs.aws.amazon.com/ses/latest/dg/monitor-sending-activity.html)).
- Application instances require `AWS_SES_ACCESS_KEY_ID`, `AWS_SES_SECRET_ACCESS_KEY`, and `AWS_SES_REGION` (or the standard `AWS_ACCESS_KEY_ID`/`AWS_SECRET_ACCESS_KEY`/`AWS_REGION`) to authenticate. For local development, store them with `dotnet user-secrets`; for production, inject via the process manager/environment.
- Configure `CONTACT_NOTIFICATIONS_FROM_ADDRESS`, `CONTACT_NOTIFICATIONS_RECIPIENTS`, and optionally `CONTACT_NOTIFICATIONS_SUBJECT`/`CONTACT_NOTIFICATIONS_ENABLED` before deployment. Recipients should be semicolon- or newline-delimited and include every admin forwarding target.
- Update the configured sender and reply-to addresses via `ContactNotifications` settings. When rotating, re-run SES verification for the new address or domain before deploying.
- After rotating credentials or sender addresses, execute the explicit integration test `ContactSubmissionSesLiveTests.SubmitContactForm_SendsLiveEmail` to validate end-to-end delivery. The test will skip automatically when credentials or contact notification environment variables are absent.
- If email sending fails, throttle contact submissions by temporarily disabling the form (set `ContactNotifications:Enabled=false`) until SES access is restored. Resume once deliverability metrics stabilize.

## CloudFront & DNS
- CloudFront distribution `cf-swingtheboogie` fronts both static media (S3 origin) and the web app (EC2 origin). Default behaviour `/*` should target the EC2 origin with caching disabled; `/media/*` stays mapped to the S3 origin.
- GoDaddy DNS: `www` CNAME → `d2r0vyil5uhr44.cloudfront.net`; apex uses GoDaddy forwarding to `https://www.swingtheboogie.com` until Route 53 takes over after the domain transfer lock.
- Security groups must keep TCP/80 open so CloudFront can reach the Kestrel origin.

## CloudFront, Route 53, and Health Checks
- CloudFront should be configured with Origin Shield and the correct origin access control. Validate origins quarterly using `aws cloudfront get-distribution-config` and compare against the architecture doc.
- Route 53 health checks must target the CloudFront distribution endpoint. Confirm status via the Route 53 console; red health checks require immediate action before deployments continue.
- Document completed manual validations in the operations log after each staging rehearsal: include timestamps for backup verification, ffmpeg test transcodes, and SES smoke tests.
