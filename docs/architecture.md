# GE Band Site Architecture

## Guiding Goals
- Low operating cost: Operate a single ASP.NET application server and keep the footprint small enough to fit comfortably on one EC2 instance.
- Always-on experience: Keep the public site hot around the globe via CloudFront so fans never hit a cold start.
- Simple presentation stack: Deliver Razor Pages with vanilla HTML, CSS, and JavaScript to avoid the overhead of frontend frameworks or third-party CDNs.
- Media-first storytelling: Offer rich photo and video galleries with an admin workflow that safely stores original media in Amazon S3 and serves optimized renditions through CloudFront.
- Operational clarity: Define lightweight but explicit procedures for deployment, observability, and content upkeep so maintenance remains predictable.

## High-Level Topology
```
Users (global)
    ↓ Route 53 hosted zone with CloudFront alias ([docs.aws.amazon.com](https://docs.aws.amazon.com/Route53/latest/DeveloperGuide/routing-to-cloudfront-distribution.html))
CloudFront distribution (cached, always hot)
    ↓ HTTPS only over AWS-managed edge ranges ([docs.aws.amazon.com](https://docs.aws.amazon.com/AmazonCloudFront/latest/DeveloperGuide/LocationsOfEdgeServers.html))
Single EC2 origin running Kestrel + ASP.NET 9.0
    ↓ Local PostgreSQL 17
    ↘ Amazon S3 (media storage, CloudFront Origin Access Control)
```

## Hosting Stack
### Edge, DNS, and Certificates
- Use Amazon Route 53 to publish an alias record that points the apex/root domain to the CloudFront distribution for minimal latency routing ([Routing traffic to CloudFront distribution](https://docs.aws.amazon.com/Route53/latest/DeveloperGuide/routing-to-cloudfront-distribution.html)).
- Terminate TLS at CloudFront with an ACM-issued public certificate so we benefit from AWS-managed renewals at no cost ([CloudFront TLS requirements](https://docs.aws.amazon.com/AmazonCloudFront/latest/DeveloperGuide/cnames-and-https-requirements.html)).
- Configure CloudFront standard Shield protections for baseline DDoS mitigation with no incremental cost ([AWS Shield Standard overview](https://docs.aws.amazon.com/waf/latest/developerguide/ddos-standard-summary.html)).

### Origin Host
- Run the entire application on a single EC2 instance sized for steady load. Favor Graviton-backed instances to reduce cost; .NET 9 officially supports Linux Arm64 deployments ([.NET 9 supported operating systems](https://raw.githubusercontent.com/dotnet/core/main/release-notes/9.0/supported-os.md)).
- Host the application with the built-in Kestrel web server behind CloudFront. Avoid IIS/NGINX.
- Co-locate PostgreSQL 17 on the same instance for cost savings and straightforward administration. Size storage and IOPS with headroom for the gallery catalog and logs.
- Harden the security group or firewall to only allow inbound HTTPS from the AWS-managed CloudFront origin-facing prefix list so the origin cannot be bypassed ([CloudFront managed prefix list](https://aws.amazon.com/blogs/networking-and-content-delivery/limit-access-to-your-origins-using-the-aws-managed-prefix-list-for-amazon-cloudfront/)).
- Permit SSH for trusted developer IPs so emergency access stays possible; rely on key-based auth.

### Secrets and Configuration
- Treat environment variables as the single source of truth for secrets (database credentials, S3 keys, SES credentials, JWT keys). The application reads everything through ASP.NET `IConfiguration`, which automatically pulls from user secrets during local development.
- Use AWS Systems Manager Parameter Store or Secrets Manager to distribute production secrets, exposed to the process as environment variables at startup.

### Network and Security Posture
- Enforce TLS end-to-end. CloudFront communicates with the origin using HTTPS, and the origin redirects any accidental HTTP traffic to HTTPS.
- Keep S3 media buckets private and fronted by CloudFront Origin Access Control (OAC). Only CloudFront can fetch and cache media objects.
- Limit IAM roles to the minimum needed: application execution (S3 read/write, SES send, optional Parameter Store access) and backup automation.

## Application Layer
- Target .NET 9.0 with nullable reference types enabled and rely on idiomatic ASP.NET Razor Pages for all UI concerns.
- No client-side frameworks, jQuery, Bootstrap, or external CDN bundles. Author accessibility-first, responsive markup and styling with static assets served by the application.
- Maintain clear separation of concerns: Razor Pages orchestrate user experience, services encapsulate domain logic, and data access flows through EF Core against PostgreSQL.
- Prefer async I/O to keep the single-node origin responsive even under concurrent media requests.
- Bake CloudFront-aware caching headers into responses: immutable hashes for static assets, short-lived caching for HTML, and conditional GET handling for JSON endpoints.
- Ship a minimal but functional Razor-based admin portal as part of MVP. Admin pages manage media uploads, events, testimonials, and band lineup visibility; they prioritize correctness over visual polish.

## Data and Storage
- Store all relational data (events, bios, gallery metadata, contact submissions) in PostgreSQL. Maintain schema changes via EF Core migrations kept under source control; do not auto-run migrations in production.
- Keep database backups automated via nightly `pg_dump` executed by an internal hosted service. The job writes encrypted archives to an S3 backup bucket and prunes backups beyond the retention window (minimum 30 days).
- Media assets live in a dedicated S3 bucket. Key naming should encode media type and logical grouping (for example, `media/photos/2025/05-tour-opening.jpg`).
- Cache media metadata (dimensions, duration, mime type, poster paths, encoding status) in PostgreSQL to avoid repeated S3 HEAD calls during page rendering.

## Media Workflow
- Admin workflow:
  1. Admin authenticates to the portal and selects photos or videos to upload.
  2. Application issues time-limited pre-signed S3 URLs. Uploads land in a staging prefix (for example, `uploads/raw/`).
  3. After upload completion, the portal submits metadata (title, caption, tags, staging key, uploaded poster image) to the application, which records a pending-media entry.
  4. A background job downloads the source file (supports `.mov` and `.mp4` today), stores the original, and uses `ffmpeg` to transcode to the standard MP4 derivative (H.264 video, AAC audio, constant 8 Mbps @1080p or 5 Mbps @720p). Both original and derivative versions are pushed to S3 under durable prefixes (for example, `media/videos/` with `/source` and `/playback` subfolders).
  5. The job updates PostgreSQL with metadata (duration, resolution, bitrate, poster path, derivative key) and marks the asset ready for publication.
- Public requests retrieve media directly from S3 via CloudFront OAC. HTML pages render pre-signed or public CloudFront URLs while keeping the S3 bucket private.
- Videos play through HTML5 `<video>` elements using the MP4 derivative and the admin-supplied poster image for the `<video poster>` attribute. Adaptive streaming (HLS/DASH) is out of scope for MVP but the transcode service should be isolated so we can extend it later.
- Photos are served in their uploaded resolution. If optimization becomes necessary, extend the pipeline with optional resizing jobs.

## Availability and Performance
- CloudFront keeps the site hot worldwide and absorbs traffic bursts. Configure multiple cache behaviors if we later split static and dynamic paths.
- Implement basic health checks from Route 53 to the origin to detect failures quickly. CloudFront will serve stale content briefly if the origin is unreachable.
- Keep the EC2 instance within an Auto Recovery-capable instance family so AWS can automatically recover from hardware issues without multi-node complexity.

## Observability and Operations
- Emit structured Serilog logs to rolling on-host files. Retention follows the `LoggingConfiguration.RetainedFileCount` setting; no centralized log aggregation in MVP.
- Do not collect metrics or health dashboards. Operational checks rely on logs, Route 53 health monitoring, and CloudFront/EC2 status pages.
- Centralize configuration (connection strings, S3 bucket names, SES settings) in environment variables populated from AWS secrets services; never bake secrets into configuration files.
- Deployments should restart the application service to pick up refreshed configuration and secrets.

## Compliance and Data Protection
- Enforce least-privilege access to admin features with the provided email/password authentication stack. No MFA or external identity provider in MVP; all public pages remain anonymous.
- Encrypt all sensitive data at rest (PostgreSQL disk volume encryption + S3 default encryption) and in transit (TLS).
- Contact submissions persist to PostgreSQL and trigger transactional email notifications via Amazon SES ([aws.amazon.com/ses](https://aws.amazon.com/ses/)). Limit stored personal data to required fields and provide a purge mechanism if retention policies demand it.

