# Free hosting and university email setup

## Provider check: 10 November 2026

The updated provider documentation does not change the no-cost production decision. Render Free has a useful platform-provided `onrender.com` HTTPS URL, but sleeps after 15 idle minutes, has only 512 MiB, and loses local files on restart/redeploy. Its free PostgreSQL expires after 30 days and lacks backups. Render also blocks outbound SMTP ports 25/465/587. It can preview a stateless API connected to separately provisioned services; it does not meet the required always-available full app/private worker/persistent backup target. Verify current outbound-bandwidth billing limits before any launch; never assume “free tier” caps all usage charges. [Render free deployment limits](https://render.com/docs/free), [Render subdomain and TLS](https://render.com/docs/web-services).

Oracle's current Always Free documentation says A1 allowance is up to 2 OCPU and 12 GB/month equivalent in the home region, within its free limits. Oracle may reclaim A1 instances whose CPU, network and (A1) memory utilization each stay below its threshold for seven days. Capacity can be unavailable. This remains the strongest documented compute candidate, but it does not by itself supply a stable free hostname or prove the ARM worker, disk, backup or uptime requirements. No account or VM has been provisioned. [Oracle Always Free resources and idle reclamation](https://docs.oracle.com/en-us/iaas/Content/FreeTier/freetier_topic-Always_Free_Resources.htm).

Cloudflare Tunnel requires a domain on Cloudflare for stable published application hostnames. Its Quick Tunnel (`trycloudflare.com`) needs no domain/account, but is temporary, changes on restart, and is explicitly for testing/development; anyone holding the URL can reach the exposed service unless an access restriction is configured. It is suitable only for a short test, not the stable public application. [Tunnel prerequisites](https://developers.cloudflare.com/tunnel/get-started/), [Quick Tunnel limits](https://developers.cloudflare.com/tunnel/get-started/quick-tunnels/).

The user-authorized free budget means a temporary URL, sleeping API, expiring database, or unavailable worker cannot be represented as a complete production-equivalent deployment. Current hosted credentials, an Oracle account/capacity and any stable platform-provided hostname remain unresolved. The secure local Resend verification test can still proceed independently on the owner's PC.

Checked against provider documentation on 2026-11-09. No hosting account, paid hosting plan, public deployment, tenant consent, or hosted database change has been created by this implementation. CI uses standard GitHub runners against the account's included allocation; no billing setting or paid plan was enabled.

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

## Additional HTTPS ingress candidate for a small controlled test

[ngrok Free](https://ngrok.com/docs/pricing-limits/free-plan-limits) supplies an account-assigned stable development hostname and HTTPS without buying a domain. Its free endpoint can remain online, but limits are 20,000 HTTP requests and 1 GB outbound traffic per month, and browser HTML initially shows ngrok's notice. Oracle would still host the actual API/worker; ngrok would be an additional public ingress provider, not replacement compute.

This is a possible controlled testing arrangement, not the selected university release architecture or a capacity guarantee. Notification polling, image downloads and normal application requests all consume its quota. Before using it, obtain an owner-controlled free account and verify the assigned hostname, privacy/traffic-inspection settings, free quota and proxy-header behavior. Its agent token belongs in host secret storage; never paste it here. Do not activate a paid plan, silently bypass its browser notice, or describe a live developer tunnel as a verified production deployment. No account, agent, public endpoint or new third-party access has been created.

## Outlook university mailbox

An Outlook inbox proves access to the mailbox, not permission for this application to send as it. The current implementation supports SMTP with STARTTLS or Resend HTTPS. Neither has been verified against the university tenant.

For delivery to general recipients, Resend requires a verified sending domain. The user does not own a domain, so that general delivery path remains blocked. Never claim ownership of o6u.edu.eg or request arbitrary university DNS changes.

There is a narrower first-test option: [Resend's shared test sender](https://resend.com/docs/knowledge-base/403-error-resend-dev-domain) can send from `onboarding@resend.dev` only to the email associated with that Resend account. If the owner can register a free Resend account using the authorized university test mailbox, the existing HTTPS adapter can exercise real verification/recovery delivery to that one inbox without purchasing a domain. Configure `Email:Provider=resend`, `Email:LocalSink=false`, that test sender, the key through secure configuration, and a controlled release allowlist containing only the authorized address. Check the actual inbox and usable link; provider acceptance alone is insufficient. No real message has been sent yet.

This single-inbox test does not establish multi-user university delivery or solve a full recovery/custody pilot needing independent accounts. Resend simulation recipients such as `delivered@resend.dev` do not prove university-mail delivery and must not be substituted for the authorized inbox.

Ask university IT whether an authorized test sender can use an approved SMTP relay or delegated [Microsoft Graph Mail.Send](https://learn.microsoft.com/en-us/graph/permissions-reference#mail-send). Delegated Graph permissions act as the signed-in user and remain subject to tenant consent policies. Graph support is **NOT IMPLEMENTED**; an OAuth/token-refresh adapter and its tests would be required after the permitted route is established. Do not request application-wide Mail.Send or the user's mailbox password as a shortcut.

The exact authorized test address is needed only when configuring the controlled release allowlist and exercising real delivery. Share that non-secret address when ready; supply credentials/consent directly to the approved provider. Initial tests must verify delivered verification/reset messages, expired/replayed links, and links returning to the actual public HTTPS origin. An HTTP acceptance response or local mail-sink result does not prove delivery.

## Remaining external release gates

Hosted `smart_campus` migrations have not been applied. The legacy storage SELECT policy allows authenticated reads across the maintenance/found buckets; its prepared prefix restriction is in `scripts/sql/restrict-storage-prefix.sql`, with an isolated proof in `scripts/sql/test-storage-prefix.sql`. This is an external permission change and must receive approval after isolated verification and backup. Until corrected, new application objects must not be placed in those hosted prefixes for a real pilot.

Runtime/migration database roles, private storage credentials, authorized mail transport, free AI keys, reviewed campus directory/taxonomy, reviewed retrieval calibration, and off-host restore proof remain required. Keep `Release__Mode=controlled`, populate only authorized university test emails, and keep inference/image forwarding and search disabled until their respective gates pass. No university-wide release is authorized by a successful testing deployment.
