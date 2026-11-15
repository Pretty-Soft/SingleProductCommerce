# Customization guide

Use the admin interface for supported storefront and catalog changes. Source-level customization is intended for developers and must retain the HTTPS, role authorization, anti-forgery, audit, validation, localization, and responsive requirements.

## Admin-configurable content

- The Catalog section manages the single product, SKU variants, inventory, product attributes/options, catalog copy, and product images.
- Storefront settings manage the shop name, headline, subtitle, flash sale, social sharing, WhatsApp contact, trust/service details, brand logo, and storefront translations.
- English is the source and fallback. Bangla and Spanish content is entered by admins in the appropriate Catalog or Storefront section.
- Store currency is configured once during initial product setup. Changing it later is locked until the owner approves a conversion rule. Do not edit stored price/currency fields manually to bypass this rule.

Role assignments restrict the areas each administrator can edit. See the [administrator guide](ADMINISTRATOR_GUIDE.md) before granting a role.

## Source-level changes

- Backend contracts, validation, and business behavior belong in the corresponding backend application or persistence services; keep HTTP handlers and React components focused on input/output and presentation.
- The storefront UI is in `frontend/src`. Maintain keyboard access, readable focus indicators, small-screen layouts, and the supported storefront languages when changing a component.
- Storefront colors and limited typography sizes can be changed through Storefront settings, with a preview. Product titles are constrained to 14–16 px and details to 12–14 px. Custom CSS, arbitrary CSS values, and admin-editable stylesheets are not allowed; layout remains source-controlled in `frontend/src/styles.css`.
- Storefront language UI strings are maintained in `frontend/src/storeI18n.ts`. Admin-entered product and store text is persisted in the catalog/storefront translation fields, not in this dictionary.
- Public/admin routes and payload expectations are summarized in the [API contracts](API_CONTRACTS.md).

## Images and local storage

Use the admin media controls for product images and the brand logo. The bytes stay on local API-host storage through `IProductImageStorage`; set `ProductMedia:RootPath` in local configuration to move the storage directory. Preserve media files together with the database when backing up or restoring. Cloud image hosting is not required.

## Before shipping a customization

1. Update focused tests for changed behavior and keep provider-specific behavior covered by its migration assembly.
2. Run the backend tests, frontend tests, frontend production build, and solution build.
3. Check `git diff --check` and confirm that local configuration, generated files, customer data, and credentials are excluded from the commit.
4. Update the requirements/roadmap only when a product owner confirms a decision; keep unresolved policies marked open.
