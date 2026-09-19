# Security Policy

## Supported versions

ERP-AI-Pro is pre-release. Only the `main` branch receives security fixes.

## Reporting a vulnerability

Report vulnerabilities privately through GitHub's private vulnerability reporting on this repository
(**Security → Report a vulnerability**). Do not open a public issue for security problems.

Include the affected area (backend, web, infrastructure), reproduction steps, impact, and any suggested fix.

Response targets: acknowledgement within 3 business days, triage within 7 days, fix or mitigation for
confirmed high/critical findings within 30 days.

## Scope

- Authentication and authorization (Microsoft Entra ID sign-in, session cookies, permissions)
- Data protection of personal and statutory data (PAN, Aadhaar, bank details, payroll)
- Configuration and secrets handling (Azure App Configuration, Azure Key Vault)
- Supply chain (dependencies, GitHub Actions, container images)

Automated scanning in place: GitHub secret scanning with push protection, Dependabot security updates,
CodeQL, dependency review on pull requests.
