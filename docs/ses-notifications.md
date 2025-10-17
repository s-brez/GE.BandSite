# SES Deliverability & Feedback Handling

This application now enforces an Amazon SES bounce/complaint feedback workflow. Refer to the official SES documentation for background and message schema details ([docs.aws.amazon.com](https://docs.aws.amazon.com/ses/latest/dg/monitor-sending-activity-using-notifications.html)).

## AWS Configuration Checklist

1. **Create SNS topics**
   - Create three *Standard* Amazon SNS topics in the production region: `ses-bounce`, `ses-complaint`, and (optional) `ses-delivery`.
   - Attach the SES publish policy from the SES guide to each topic and replace the region/account placeholders with production values ([docs.aws.amazon.com](https://docs.aws.amazon.com/ses/latest/dg/configure-sns-notifications.html)).
   - Subscribe an HTTPS endpoint pointing at `https://<public-domain>/api/ses/notifications` and confirm the subscription. The webhook automatically confirms future subscription requests when `AutoConfirmSubscriptions` remains enabled.

2. **Update SES identity settings**
   - For each verified domain or address used to send mail, open *Configuration → Verified identities → Notifications* and assign the bounce and complaint topics. Assign the delivery topic if you want success telemetry ([docs.aws.amazon.com](https://docs.aws.amazon.com/ses/latest/dg/monitor-sending-activity-using-notifications-sns.html)).
   - Enable “Include original email headers” for bounce and complaint topics so diagnostic codes appear in the admin dashboard.
   - Disable Email Feedback Forwarding once topics are assigned so SES does not double-deliver events ([docs.aws.amazon.com](https://docs.aws.amazon.com/ses/latest/dg/monitor-sending-activity-using-notifications-email.html)).

3. **Server configuration**
   - Populate the following environment variables or `appsettings` keys on the server:

     | Setting | Purpose |
     | --- | --- |
     | `SesNotifications:BounceTopicArn` or `SES_NOTIFICATIONS_BOUNCE_TOPIC_ARN` | Required bounce topic ARN |
     | `SesNotifications:ComplaintTopicArn` or `SES_NOTIFICATIONS_COMPLAINT_TOPIC_ARN` | Required complaint topic ARN |
     | `SesNotifications:DeliveryTopicArn` (optional) | Delivery topic ARN |
     | `SesNotifications:AllowedTopicArns` or `SES_NOTIFICATIONS_ALLOWED_TOPIC_ARNS` | Permit additional topic ARNs if multiple identities share a topic |
     | `SesNotifications:Enabled` or `SES_NOTIFICATIONS_ENABLED` | Kill-switch for the webhook |
     | `SES_NOTIFICATIONS_AUTO_CONFIRM` / `SES_NOTIFICATIONS_REQUIRE_TOPIC_VALIDATION` | Override auto-confirm / strict topic enforcement |

   - Ensure outbound HTTPS access so the webhook can download SNS signing certificates (`https://sns.<region>.amazonaws.com/…`).

4. **Smoke test**
   - Use SES’s simulation addresses (`bounce@simulator.amazonses.com`, `complaint@simulator.amazonses.com`) to trigger events and verify that:
     - The `/api/ses/notifications` endpoint responds with HTTP 200.
     - `SesFeedbackEvents`, `SesFeedbackRecipients`, and `EmailSuppressions` receive new records.
     - The admin Deliverability dashboard shows the suppression and event.

## Operations

- **Suppression lifecycle** – Permanent and undetermined bounces plus all complaints add entries to `EmailSuppressions`. Admins can clear a suppression from *Admin → Deliverability*. Clearing sets `ReleasedAt` but keeps history for audit.
- **Password reset & contact notifications** – Before sending outbound email the system checks the suppression list and logs a warning if a recipient is blocked. Requests remain accepted to avoid leaking address validity to end users.
- **Log monitoring** – Look for `Bounce notification`, `Complaint notification`, and `Supression released` entries in the structured logs to audit handling. Failures to validate SNS signatures log at warning level with actionable reasons.

Document updates to this process alongside SES account changes so the webhook topic list stays in sync with the AWS configuration.
