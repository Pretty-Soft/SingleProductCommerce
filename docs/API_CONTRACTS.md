# API contracts

The API base is HTTPS-only. The default local IIS Express API URL is `https://localhost:44300`; production uses the deployment's trusted HTTPS URL. The frontend must set `VITE_API_BASE_URL` to an HTTPS URL. Do not send credentials or admin cookies to an untrusted origin.

All routes below are prefixed with `/api/v1`. JSON field names use camel case. The service returns standard HTTP status codes; validation failures use a `ValidationProblemDetails` response with an `errors` object. Signed cart quote tokens are opaque and must be returned unchanged during checkout.

## Public storefront and checkout

| Method and route | Access | Purpose |
| --- | --- | --- |
| `GET /store/product?language=en\|bn\|es` | Anonymous | Read the published product, active variants/options, images, sale, WhatsApp link, and trust details. Language defaults to English; an unpublished product returns `404`. Unsupported languages return `400`. |
| `GET /store/product/images/{imageId}` | Anonymous | Read an image only when it belongs to the published product. |
| `POST /store/cart/quote` | Anonymous | Request a signed add-time price quote for a variant and selected option IDs. |
| `POST /store/orders` | Anonymous | Place a guest COD order from signed quote tokens. Orders begin `Pending`; placement atomically reduces stock and returns the order number. |

Quote request:

```json
{
  "variantId": "00000000-0000-0000-0000-000000000000",
  "attributeOptionIds": ["00000000-0000-0000-0000-000000000000"]
}
```

Order request:

```json
{
  "customerName": "Customer name",
  "phoneNumber": "+8801000000000",
  "deliveryAddress": "Delivery address",
  "lines": [
    { "quoteToken": "opaque-server-issued-token", "quantity": 1 }
  ]
}
```

The API does not accept client-entered prices. Mixed-currency orders are rejected. The quote response contains `token`, `currencyCode`, `unitPrice`, and `quotedAtUtc`; checkout returns order number, status, total, currency, timestamp, and saved line summaries.

## Admin authentication and request protection

Admin authentication uses a secure, HTTP-only cookie. It is not a bearer-token API. Obtain an anti-forgery request token from `GET /admin/csrf` and send it in the `X-CSRF-TOKEN` header for cookie-authenticated requests that require anti-forgery validation. All cookie-authenticated state-changing routes require it; the MFA setup read route also validates it. Keep the anti-forgery cookie and request token associated with the same browser session. The forgot-password endpoint is rate-limited and responds generically.

| Method and route | Access | Purpose |
| --- | --- | --- |
| `GET /admin/csrf` | Anonymous | Establish anti-forgery cookie and return request token. |
| `POST /admin/login` | Anonymous | Email/password sign-in; may indicate that authenticator verification is required. |
| `POST /admin/two-factor/login` | Anonymous | Complete sign-in with an authenticator code. |
| `POST /admin/password/forgot` | Anonymous, rate-limited | Request a password-reset email. |
| `POST /admin/password/reset` | Anonymous | Complete reset with email, token, and new password. |
| `GET /admin/me` | Signed-in admin | Return current roles and password/MFA state. |
| `POST /admin/logout` | Signed-in admin | End the cookie session. |
| `POST /admin/password` | Signed-in admin | Change password. |
| `GET|POST /admin/mfa/setup` | Signed-in admin | Begin and confirm authenticator enrollment. |
| `POST /admin/mfa/disable` | Signed-in admin | Disable personal MFA after code verification when policy allows. |

Any first-sign-in temporary password change is required before using protected admin operations.

## Admin work APIs

| Method and route | Role | Purpose |
| --- | --- | --- |
| `GET /admin/catalog/product` | SystemAdmin, CatalogManager | Read product setup and catalog translations. |
| `PUT /admin/catalog/product` | SystemAdmin, CatalogManager | Save product, variants, attributes, and catalog translations. |
| `POST /admin/catalog/product/images` | SystemAdmin, CatalogManager, StoreManager | Upload product image or brand logo, subject to media-kind authorization. |
| `GET|PUT|DELETE /admin/catalog/product/images/{imageId}` | SystemAdmin, CatalogManager, StoreManager | Preview, update metadata, or delete authorized media. |
| `GET|PUT /admin/storefront/settings` | SystemAdmin, StoreManager | Read/save storefront copy, branding, promotion, contact, trust items, and storefront translations. |
| `GET|PUT /admin/security/mfa-policy` | SystemAdmin | Read or set global optional/required MFA policy. |
| `GET /admin/orders/summary` | SystemAdmin, OrderManager | Read order totals by status and saved currency. |
| `GET /admin/orders` | SystemAdmin, OrderManager | List recent orders. |
| `GET /admin/orders/{orderId}` | SystemAdmin, OrderManager | Read an order and its saved line/contact details. |
| `PUT /admin/orders/{orderId}/status` | SystemAdmin, OrderManager | Update status; cancelling restores stock exactly once. |
| `GET /admin/customers?search=&page=1&pageSize=25` | SystemAdmin, OrderManager | Search/paginate guest order contact records; these are not customer profiles. |
| `GET /admin/roles`, `GET /admin/users` | SystemAdmin | List built-in roles or admin accounts. |
| `POST /admin/users` | SystemAdmin | Create an admin account and assign roles. |
| `PUT /admin/users/{userId}/roles` | SystemAdmin | Replace an account's assigned roles. |
| `POST /admin/users/{userId}/mfa/reset` | SystemAdmin | Reset another account's authenticator enrollment. |
| `GET /admin/audit` | SystemAdmin | Read recent audit events. |

Authenticated state-changing admin routes require the anti-forgery header. Authorization is enforced by the API; hiding a UI section does not grant or revoke access. See `ADMINISTRATOR_GUIDE.md` for role responsibilities and current order transitions.

## Operational routes and errors

`GET /health` reports process health and does not prove that all database operations are healthy. Use an application operation or a database-specific monitor to verify database availability. API exceptions must not be treated as successful responses; clients should handle `400`, `401`, `403`, `404`, and `5xx` responses explicitly and avoid displaying internal exception details to customers.
