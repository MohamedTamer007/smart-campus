# Smart Campus requirements

## Confirmed product direction

Build a working graduation-project website for October 6 University, with a delivery target of approximately two months. Academic reports and presentation materials are outside the current delivery scope.

- Provide a responsive website with Arabic and English interfaces, including right-to-left Arabic layouts. A separate mobile application is outside scope.
- Use React/TypeScript, a managed backend, and a separate Python AI service. Use pretrained models for real inference and evaluate their results.
- Support maintenance reports with text and images, classification, priority/risk recommendations, assignment, and status tracking.
- Support lost-and-found reports, visual similarity recommendations, and staff verification of ownership claims.
- Provide decision-support dashboards for recurring faults, service performance, and issue distribution across campus locations.
- Restrict reporting to verified university accounts. Separate reporter, technician, maintenance manager, lost-and-found staff, and administrator permissions.
- Provide in-app operational notifications. Email supports account verification and password recovery.
- Use clearly labeled demonstration data until authorized campus data is available.
- Use free service tiers. Required owner account: `mohamed23013292@gmail.com`. Use Vercel for website hosting; do not use Netlify.

This records confirmed direction, not a complete application implementation plan. Model selection, detailed application interfaces, campus data, and AI hosting still require investigation before their implementation.

## Environment setup acceptance criteria

- Create a private `smart-campus` repository under the verified GitHub account `mohamed23013292`, and configure repository-local Git identity with the required owner email.
- Configure the Vercel `smart-campus` project on the existing Hobby team and connect the repository. Disable Git-triggered deployment while no application build exists.
- Reuse the existing Supabase project. Configure its auth site URL and the local Vite development redirect, preserve email confirmation, and leave the application schema unchanged.
- Configure only the public Supabase URL and publishable key for frontend use. Privileged service tokens and the database password must not enter frontend configuration or Git history.
- Exclude local environment files and service-link metadata from Git. Keep a setup record containing project identifiers, verified checks, limitations, and remaining access requirements without credential values.
- Identify SMTP and AI-hosting prerequisites without claiming untested services work. Do not create paid resources.

Application CI, typechecking, tests, builds, migrations, and public deployment belong to the application setup phase. Environment-only checks must not be presented as application test results.
