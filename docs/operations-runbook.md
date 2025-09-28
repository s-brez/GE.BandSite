# Operations Runbook

## Routine Deployment
- Confirm the target EC2 instance has the latest container image or published build artifact staged. Pull from the protected artifact bucket if updates were pushed by CI in the last 24 hours.
- Validate Route 53 health checks are green before touching the host. If any are failing, pause and investigate CloudWatch alarms first.
- On the EC2 instance, stop the `ge-band-site` systemd unit, deploy the new binaries to `/var/www/ge-band-site`, and restart the service. Always tail `journalctl -u ge-band-site -f` for 2–3 minutes after restart to confirm a healthy boot.
- Issue an `aws cloudfront create-invalidation --distribution-id <id> --paths "/index.html"` when public Razor Pages contain content updates. Static assets that ship with hashed filenames do not require invalidations.
- Run a smoke test: browse `/`, `/Media`, `/Admin`, and submit a contact form (directed to staging SES identity). Check the application logs for warnings.

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
- The video pipeline depends on `ffmpeg` available at `MediaProcessing: FfmpegPath` (default `ffmpeg`). Confirm the binary is present with `ffmpeg -version`; install from the distribution repository for the EC2 AMI if missing.
- When updating ffmpeg, restart the application service to ensure the hosted background worker picks up the new binary path. Run an admin video upload on staging to verify transcodes succeed end-to-end.
- Troubleshoot failures by reviewing logs emitted by `MediaProcessingHostedService`; errors surface the command line and exception details.
- On application startup the media bootstrapper scans the configured S3 prefixes and inserts missing `MediaAsset` rows. Legacy `.mov` sources are added with `Pending` state so the transcode worker can regenerate MP4 playback files automatically.
- To regenerate MP4 playbacks for existing `.mov` uploads, list raw objects under `uploads/raw/` in S3, locate the corresponding records in `MediaAssets`, set `ProcessingState` back to `Pending`, and allow the hosted service to reprocess them. Confirm new `.mp4` files appear under the playback prefix before toggling publication flags.

## Email Notifications (SES)
- Contact form notifications use Amazon SES in `us-east-1`. Verify the sending identity remains in production mode and monitor bounce/complaint metrics using the SES console (see [SES sending activity monitoring](https://docs.aws.amazon.com/ses/latest/dg/monitor-sending-activity.html)).
- Update the configured sender and reply-to addresses via `ContactNotifications` settings. When rotating, re-run SES verification for the new address or domain before deploying.
- If email sending fails, throttle contact submissions by temporarily disabling the form (set `ContactNotifications:Enabled=false`) until SES access is restored. Resume once deliverability metrics stabilize.

## CloudFront, Route 53, and Health Checks
- CloudFront should be configured with Origin Shield and the correct origin access control. Validate origins quarterly using `aws cloudfront get-distribution-config` and compare against the architecture doc.
- Route 53 health checks must target the CloudFront distribution endpoint. Confirm status via the Route 53 console; red health checks require immediate action before deployments continue.
- Document completed manual validations in the operations log after each staging rehearsal: include timestamps for backup verification, ffmpeg test transcodes, and SES smoke tests.
