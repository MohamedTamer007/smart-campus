# Free hosting and university email setup

Checked against provider documentation on 2026-11-09. No hosting account, chargeable resource, public deployment, tenant consent, or hosted database change has been created by this implementation.

## Decision and unresolved constraint

Oracle Always Free is the compute candidate for the complete API plus local SigLIP worker. This is an engineering selection pending actual ARM image/inference tests and capacity allocation, not an availability guarantee. The existing VM packaging in `deployment/compose.yml` reserves 8 GiB for the worker, 2 GiB for the API, and 128 MiB for Caddy. Real measured requirements are still pending.

The user requires a free platform-provided HTTPS subdomain and has no domain. Render supplies such an address, but its free service cannot host the complete packaged system with persistent keys/model cache. Oracle VM provisioning alone does not complete that hostname requirement. Therefore **the complete public deployment is BLOCKED**, pending a verified no-cost hostname/HTTPS arrangement and account access. No fabricated public URL or temporary tunnel is a delivered deployment.

| Candidate | Verified constraints | Decision for this application |
|---|---|---|
| [Render Free](https://render.com/docs/free), [compute pricing](https://render.com/pricing), [web service address](https://render.com/docs/web-services) | 512 MiB; sleeps after 15 idle minutes; no persistent disk; SMTP 25/465/587 blocked; managed TLS and onrender.com address | Useful API preview candidate only after persistent key storage and HTTPS mail transport are solved. Does not satisfy the complete API plus local worker requirement. Do not describe it as the full application. |
| [Koyeb Free](https://www.koyeb.com/docs/reference/instances) | One free instance, 512 MiB, 0.1 vCPU, 2 GB SSD; no volumes or worker service type; sleeps after one idle hour | Insufficient for this worker and persistent deployment packaging. |
| [Oracle Always Free](https://docs.oracle.com/en-us/iaas/Content/FreeTier/freetier_topic-Always_Free_Resources.htm) | A1 allowance equivalent to 2 OCPU/12 GB for free tenancies in the home region; capacity may be unavailable and idle compute may be reclaimed | Best compute candidate among these three; requires user account setup, ARM verification, durable backups, and a separate resolved free HTTPS hostname. No paid upgrade for capacity. |

The 10-user, p95 under 2 seconds, RPO 24 hours, and RTO 4 hours figures are targets. They have not been demonstrated on any hosting provider.

## Oracle account setup the account owner must complete

1. Start at [Oracle Cloud Free Tier](https://www.oracle.com/cloud/free/). Register personally and complete the provider's identity/payment-method verification directly on Oracle. Do not send card data, passwords, recovery codes, or SSH private keys through chat. A verification method does not authorize paid services.
2. Select the home region deliberately: Always Free compute must be there. Keep the tenancy on Free Tier; do not upgrade to Pay As You Go or use trial-credit-only shapes. If signup or free capacity is unavailable, record the exact error and stop provisioning rather than choosing a paid alternative.
3. Enable account MFA. Create an application compartment. For an initial owner-operated setup, retain owner control; do not grant broad third-party tenancy access. Later automation should receive only the reviewed compartment permissions it actually needs.
4. Review the console's current free limits and resource labels. The prepared candidate is one Ubuntu ARM VM using `VM.Standard.A1.Flex`, at most 2 OCPU and 12 GB total, within the tenancy's free boot-volume allocation. Do not create it unless its complete configuration is Always Free eligible. Do not substitute AMD paid shapes, paid load balancers, or extra disks. Budget alerts are alerts, not a spending cap.
5. Before creating network rules, review the intended rules: public HTTP/HTTPS only for Caddy, SSH restricted to the owner's current IP, no public PostgreSQL, API 8080, or worker 8123. Applying external network/security permissions requires the separate account-owner approval already specified by the project.
6. Save an SSH private key in the owner's secure key store. Share only non-secret setup status: account ready, home region, free capacity available/unavailable. Access credentials belong in provider/host secret storage.
7. Resolve the free hostname requirement before activating public deployment. An IP address is not a supplied platform HTTPS subdomain. A third-party free DNS name would require a documented change to that preference; it is not silently assumed here.

Once account, hostname, and scoped access are ready, the remaining engineering sequence is: verify ARM images and actual model, install the reviewed container runtime, supply secure configuration, prepare backup destinations, validate isolated staging migrations, review the storage-policy correction, bootstrap the first administrator, run the controlled mailbox journeys, then test the public origin. `deployment/*.env.example` files are templates; their placeholder values cannot start a valid hosted deployment.

## Outlook university mailbox

An Outlook inbox proves access to the mailbox, not permission for this application to send as it. The current implementation supports SMTP with STARTTLS or Resend HTTPS. Neither has been verified against the university tenant.

Resend requires a verified sending domain, so the user's current lack of a controlled domain prevents that path. Never claim ownership of o6u.edu.eg or request arbitrary university DNS changes.

Ask university IT whether an authorized test sender can use an approved SMTP relay or delegated [Microsoft Graph Mail.Send](https://learn.microsoft.com/en-us/graph/permissions-reference#mail-send). Delegated Graph permissions act as the signed-in user and remain subject to tenant consent policies. Graph support is **NOT IMPLEMENTED**; an OAuth/token-refresh adapter and its tests would be required after the permitted route is established. Do not request application-wide Mail.Send or the user's mailbox password as a shortcut.

The exact authorized test address is needed only when configuring the controlled release allowlist and exercising real delivery. Share that non-secret address when ready; supply credentials/consent directly to the approved provider. Initial tests must verify delivered verification/reset messages, expired/replayed links, and links returning to the actual public HTTPS origin. An HTTP acceptance response or local mail-sink result does not prove delivery.

## Remaining external release gates

Hosted `smart_campus` migrations have not been applied. The legacy storage SELECT policy allows authenticated reads across the maintenance/found buckets; its prepared prefix restriction is in `scripts/sql/restrict-storage-prefix.sql`, with an isolated proof in `scripts/sql/test-storage-prefix.sql`. This is an external permission change and must receive approval after isolated verification and backup. Until corrected, new application objects must not be placed in those hosted prefixes for a real pilot.

Runtime/migration database roles, private storage credentials, authorized mail transport, free AI keys, reviewed campus directory/taxonomy, reviewed retrieval calibration, and off-host restore proof remain required. Keep `Release__Mode=controlled`, populate only authorized university test emails, and keep inference/image forwarding and search disabled until their respective gates pass. No university-wide release is authorized by a successful testing deployment.
