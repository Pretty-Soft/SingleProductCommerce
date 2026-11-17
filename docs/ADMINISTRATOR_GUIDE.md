# Administrator guide

## SMTP and password recovery

Sign in as SystemAdmin and open **Accounts → SMTP and password recovery**. Enter the relay host, STARTTLS port, sender address, and relay login. Save, then select **Test saved settings** to send a message to your administrator inbox. A saved password is never displayed. Leave its field blank to keep it when the relay host, port, and username stay the same.

For Gmail, use `smtp.gmail.com` on port `587`, your Gmail address as the username and sender, and a Google app password. Configure the storefront reset URL on the API host as described in the [deployment guide](DEPLOYMENT_GUIDE.md).

This guide describes the current admin application. It documents implemented behavior and does not resolve the remaining policy decisions in `PRODUCT_REQUIREMENTS.md`.

## Sign in and account recovery

Administrators sign in at `/admin` with an account created by a System Admin. There is no public account registration or customer sign-in. A first-time admin must replace the temporary password. Passwords must contain at least 12 characters, including uppercase, lowercase, a digit, and a symbol.

To enroll an authenticator app, add an account in the app, scan the QR code shown by OneCart, and enter the current six-digit code to confirm enrollment. MFA is optional by default. A System Admin can make it mandatory for all admins and can reset another admin's authenticator enrollment. If required MFA is enabled, the admin must enroll before using the workspace. Users can disable their own optional MFA after verifying a current code.

Password recovery sends an email link. Configure SMTP and the HTTPS reset URL as described in the [local setup guide](../README.md#run-locally). Requests use a generic response whether or not an account exists.

## Roles and access

| Role | Access |
| --- | --- |
| `SystemAdmin` | All admin areas, user and role administration, MFA policy, audit history, product and store settings, orders, and customer order contacts. |
| `CatalogManager` | Product catalog, variants, attributes, product translations, and product images. Cannot change storefront-only settings or manage the brand logo. |
| `OrderManager` | Order review and status updates, order totals, and customer contact records captured with orders. |
| `StoreManager` | Storefront settings and translations, trust/service details, promotions, contact settings, brand logo and storefront media. |

Only a System Admin can create admin accounts, assign roles, view the audit list, set the global MFA policy, or reset another user's authenticator enrollment. The API enforces these permissions in addition to hiding unavailable sections in the interface.

## Catalog and product media

The catalog contains one product with multiple variants and configurable attribute groups/options. Save the product before uploading images or adding translations that depend on saved variant and attribute IDs. Set each variant's SKU, display name, price, stock, and availability. Attribute groups can be required or optional; options may add to the displayed price. Add, edit, or remove variants, groups, choices, trust details, and image records in their product sections, then save that section to apply the changes. Product image files are uploaded and deleted separately from the product save.

Use **Delete product** in Product information to remove the product and its child catalog data. The operation is permanent and removes its stored image files. A product referenced by an order cannot be deleted; unpublish it and deactivate its variants to preserve order history.

English is the source language and fallback. Catalog Managers can enter Bangla and Spanish text for product names/descriptions, saved variant names, saved attribute group names, and saved option names. Blank fields fall back to English.

Product images are stored under the API host's `App_Data/product-images` folder by default; `ProductMedia:RootPath` can change this location. The current limits and accepted image types are described in the [project setup documentation](../README.md#run-locally). Keep uploaded content and backups on protected local storage.

## Storefront presentation

System Admins and Store Managers can update the shop name, headline, subtitle, flash-sale schedule and discount, sharing setting, WhatsApp contact, and trust/service details. They can also manage the brand logo. Storefront text can be entered in Bangla and Spanish; missing content falls back to English. The visitor's language selection is remembered in that browser.

Choose the store currency from the product editor's common-currency list during initial setup. Only a SystemAdmin can change it later, and only when all orders are Completed or Cancelled. Enter new prices for every variant and priced option, then confirm the repricing in the product editor; the previous flash sale is disabled. The choice also remains available after product deletion when no order is open. Existing orders retain their original price and currency snapshots. Customers with old-currency cart quotes must add those items again before checkout.

You can enter variant prices and option adjustments with up to four decimal places. At purchase time, OneCart adds the active sale/base price and selected adjustments, then rounds the final unit price once using the active currency’s standard precision: BDT, USD, EUR, and GBP use two decimal places; currencies such as JPY use none; currencies such as KWD use three. The stored cart quote and resulting order use that rounded unit price.

## Orders and customer contacts

Customers use guest checkout and pay by Cash on Delivery. Each order stores the customer's name, phone, and delivery address as an order contact; separate orders are not merged into customer profiles. An order starts as `Pending` and receives an order number.

Current admin status transitions are `Pending` to `Processing` or `Cancelled`, and `Processing` to `Completed` or `Cancelled`. Cancelling an order restores its reserved stock exactly once. These are current implementation rules; broader status policy remains an open product decision.

Order summaries group counts and totals by status and saved currency. Different currencies are not combined. Customer search and pagination show order contact records to System Admins and Order Managers.

## Audit and backups

Important catalog, storefront, user/role, security-policy, media, and order-status changes are audited. Only System Admins can review the audit list.

Back up PostgreSQL together with the local product images and Data Protection keys. See the [PostgreSQL backup and restore runbook](OPERATIONS_POSTGRES.md). A database dump alone is not a complete store backup.
