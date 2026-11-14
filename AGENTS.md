# Codex project instructions

## Product context

This repository builds OneCart Commerce, a maintainable commercial single-product commerce platform. The authoritative product scope is `docs/PRODUCT_REQUIREMENTS.md`; update it when the owner confirms a decision.

## Engineering rules

- Use ASP.NET Core 10 and C# for backend work; use React and TypeScript for the storefront.
- Keep business rules out of HTTP endpoints and UI components. Prefer small, cohesive services and explicit contracts.
- Keep database-specific behavior behind persistence adapters. Do not claim a database provider is supported until migrations, transactions, installation, and integration coverage exist for it.
- COD is the only confirmed payment method. Do not add online payment providers without an approved requirement.
- Treat security, input validation, authorization, auditability, accessibility, localization, and responsive behavior as product requirements.
- Never commit secrets, production credentials, personal customer data, or generated environment files.
- Avoid placeholder implementations in user-facing or security-critical flows. If a requirement is not ready, document it and keep the behavior explicit.
- Add or update focused automated tests for behavior when implementing features; run the relevant checks before reporting completion.
- Keep setup and operator documentation current with code changes.

## Decision handling

The planning conversation established several goals but did not finalize every implementation choice. Preserve all entries marked **Open decision** in the requirements document until the product owner decides them. Recommendations are not approvals.
