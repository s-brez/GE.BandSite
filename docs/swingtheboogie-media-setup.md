# swingtheboogie-media Setup Guide

This document records the end-to-end configuration for delivering band media assets through the `swingtheboogie-media` S3 bucket and the `EJ25PN5UJQC9B` CloudFront distribution (`d2r0vyil5uhr44.cloudfront.net`) behind the custom hostname `media.swingtheboogie.com`.

## Prerequisites
- Fresh AWS account with Billing access and root credentials.
- Domain `swingtheboogie.com` managed at GoDaddy (nameserver transfer locked for 60 days).
- Local workstation with administrator access and space for image/video staging.
- Optional but recommended: AWS CLI v2 installed locally.

## 1. Secure the AWS Account
1. Sign in as the AWS root user and enable MFA (My Security Credentials → Multi-factor authentication).
2. Create IAM group `Administrators` with AWS managed policy `AdministratorAccess`.
3. Create IAM user `sam-admin` (console + programmatic access) and add to `Administrators`.
4. Store the access key CSV in a password manager. Rotate keys every 90 days.
5. Create IAM role `BandSitePowerUser` with policy `PowerUserAccess` for future developers.
6. Enable AWS Budgets (Billing → Budgets) for cost and data transfer alerts.

## 2. Local AWS CLI Profile (Optional)
1. Install AWS CLI v2 (Linux example):
   ```bash
   curl "https://awscli.amazonaws.com/awscli-exe-linux-x86_64.zip" -o awscliv2.zip
   unzip awscliv2.zip
   sudo ./aws/install
   aws --version
   ```
2. Configure profile `swingtheboogie`:
   ```bash
   aws configure --profile swingtheboogie
   # region: ap-southeast-2
   # output: json
   # access key id / secret: from sam-admin user
   ```
3. Confirm identity:
   ```bash
   aws sts get-caller-identity --profile swingtheboogie
   ```

## 3. Create S3 Bucket `swingtheboogie-media`
1. Console → S3 → Create bucket.
2. Name: `swingtheboogie-media`; Region: `Asia Pacific (Sydney) ap-southeast-2`.
3. Object Ownership: **Bucket owner enforced** (ACLs disabled).
4. Block Public Access: leave all options enabled.
5. Bucket Versioning: enable.
6. Default encryption: SSE-S3 (AES-256).
7. Create bucket.

## 4. Prepare Folder Structure
Use the console (Create folder) or CLI to create logical prefixes:
```bash
aws s3api put-object --bucket swingtheboogie-media --key audio/ --profile swingtheboogie
aws s3api put-object --bucket swingtheboogie-media --key images/ --profile swingtheboogie
aws s3api put-object --bucket swingtheboogie-media --key staging/ --profile swingtheboogie
aws s3api put-object --bucket swingtheboogie-media --key thumbnails/ --profile swingtheboogie
aws s3api put-object --bucket swingtheboogie-media --key videos/ --profile swingtheboogie
aws s3 ls s3://swingtheboogie-media/ --profile swingtheboogie
```

## 5. Upload Initial Media
1. Organise local assets (`images/`, `videos/`, etc.). Avoid spaces and uppercase in file names.
2. Console → `swingtheboogie-media` → Upload. Drag folders and ensure:
   - `Content-Type` matches the file (e.g., `image/jpeg`, `video/mp4`).
   - `Cache-Control` for long-lived assets: `public,max-age=31536000,immutable`.
3. Optional CLI mirroring:
   ```bash
   aws s3 sync ./media/ s3://swingtheboogie-media/ --exclude ".DS_Store" --profile swingtheboogie
   ```
4. Spot-check upload:
   ```bash
   aws s3api list-objects-v2 --bucket swingtheboogie-media --prefix images/ --max-items 5 --profile swingtheboogie
   ```

## 6. Issue ACM Certificate
1. Console → Certificate Manager → switch to `us-east-1`.
2. Request public certificate for:
   - `media.swingtheboogie.com`
   - Optional wildcard `*.swingtheboogie.com`
3. Choose DNS validation. Copy the generated CNAME values.

## 7. GoDaddy DNS Validation
1. GoDaddy → DNS Management for `swingtheboogie.com`.
2. Add ACM validation CNAME records exactly as provided.
3. Wait for ACM status to change to **Issued**.

## 8. Create CloudFront Distribution `EJ25PN5UJQC9B`
1. Console → CloudFront → Create distribution.
2. Origin domain: `swingtheboogie-media.s3.ap-southeast-2.amazonaws.com`.
3. Origin access: create Origin Access Control; accept policy reminder.
4. Viewer: Redirect HTTP to HTTPS, Allowed methods `GET, HEAD`.
5. Cache key: default (no query strings or headers unless needed).
6. Default TTL 3600 seconds; Min TTL 0; Max TTL 86400.
7. Alternate domain name: `media.swingtheboogie.com` (add wildcard later if required).
8. Custom SSL certificate: select the ACM certificate covering `media.swingtheboogie.com`.
9. Enable HTTP/3 and IPv6; turn on compression.
10. Logging: enable (target bucket `swingtheboogie-media` or dedicated logs bucket if available).
11. Create distribution and note assigned domain `d2r0vyil5uhr44.cloudfront.net`.

## 9. Update S3 Bucket Policy for CloudFront
1. CloudFront → Distribution `EJ25PN5UJQC9B` → Origins → View policy snippet.
2. Apply policy in S3 console → `swingtheboogie-media` → Permissions → Bucket policy:
   ```json
   {
       "Version": "2008-10-17",
       "Id": "PolicyForCloudFrontPrivateContent",
       "Statement": [
           {
               "Sid": "AllowCloudFrontServicePrincipal",
               "Effect": "Allow",
               "Principal": {
                   "Service": "cloudfront.amazonaws.com"
               },
               "Action": "s3:GetObject",
               "Resource": "arn:aws:s3:::swingtheboogie-media/*",
               "Condition": {
                   "ArnLike": {
                       "AWS:SourceArn": "arn:aws:cloudfront::830424059876:distribution/EJ25PN5UJQC9B"
                   }
               }
           }
       ]
   }
   ```
3. Save and verify AWS Access Analyzer reports no public access.

## 10. GoDaddy CNAME for CloudFront
1. Add DNS record: type `CNAME`, name `media`, value `d2r0vyil5uhr44.cloudfront.net`.
2. TTL can remain default (1 hour).
3. Confirm propagation using `nslookup media.swingtheboogie.com` or https://dnschecker.org.

## 11. Browser Spot Checks
1. Visit `https://media.swingtheboogie.com/images/1.jpeg`.
   - Expect a 200 response, valid certificate, and header `server: CloudFront`.
   - DevTools → Network: first load `x-cache: Miss from cloudfront`, refresh to see `Hit from cloudfront`.
2. Visit S3 direct URL `https://swingtheboogie-media.s3.ap-southeast-2.amazonaws.com/images/1.jpeg` → should return `AccessDenied`.
3. Request non-existent object `https://media.swingtheboogie.com/images/not-found.jpg` → expect CloudFront `404`.
4. For video playback, open `https://media.swingtheboogie.com/videos/<file>.mp4` and confirm streaming/range requests.

## 12. Lifecycle Configuration
1. Console → `swingtheboogie-media` → Management → Create lifecycle rule.
2. Rule `ArchiveVideosAfter180Days`: scope prefix `videos/`; transition to **Glacier Deep Archive** after 180 days.
3. Rule `ExpireStagingAfter30Days`: scope prefix `staging/`; expiration 30 days.
4. Rule `AbortIncompleteUploads`: apply to entire bucket; abort multipart uploads after 7 days.
5. Validate via CLI:
   ```bash
   aws s3api get-bucket-lifecycle-configuration --bucket swingtheboogie-media --profile swingtheboogie
   ```

## 13. Ongoing Operations
- **Cache Busting**: prefer versioned file names; use CloudFront invalidation (`/*` or targeted paths) for emergency updates.
- **Uploads**: continue via console or `aws s3 sync`; keep bucket private.
- **Monitoring**: set CloudWatch alarms on CloudFront 4xx/5xx and S3 storage size if budgets require.
- **Security**: rotate IAM keys, review bucket policies quarterly, and retain `Block Public Access` settings.
- **Runbook Checks**: document validations in `docs/operations-runbook.md` after significant changes.

## 14. Verification Commands
```bash
aws s3 ls --profile swingtheboogie
aws s3 ls s3://swingtheboogie-media/ --profile swingtheboogie
aws s3api list-objects-v2 --bucket swingtheboogie-media --prefix images/ --max-items 5 --profile swingtheboogie
aws s3api get-bucket-versioning --bucket swingtheboogie-media --profile swingtheboogie
aws s3api get-bucket-encryption --bucket swingtheboogie-media --profile swingtheboogie
aws s3api get-bucket-policy --bucket swingtheboogie-media --profile swingtheboogie
aws cloudfront get-distribution --id EJ25PN5UJQC9B --profile swingtheboogie
```

Following these steps keeps the `swingtheboogie-media` assets securely stored in S3 while delivering through CloudFront at `media.swingtheboogie.com` with private origin control and cached performance.
