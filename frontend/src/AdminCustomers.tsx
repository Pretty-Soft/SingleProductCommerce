import { FormEvent, useEffect, useState } from "react";
import { adminRequest } from "./adminApi";

interface CustomerOrderContact {
  id: string;
  orderNumber: string;
  status: string;
  customerName: string;
  phoneNumber: string;
  deliveryAddress: string;
  currencyCode: string;
  orderTotal: number;
  createdAtUtc: string;
}

interface CustomerContactPage {
  items: CustomerOrderContact[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export default function AdminCustomers() {
  const [query, setQuery] = useState("");
  const [appliedQuery, setAppliedQuery] = useState("");
  const [page, setPage] = useState(1);
  const [result, setResult] = useState<CustomerContactPage>({ items: [], totalCount: 0, page: 1, pageSize: 25 });
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    let active = true;
    const params = new URLSearchParams({ page: String(page), pageSize: "25" });
    if (appliedQuery) params.set("search", appliedQuery);
    setLoading(true);
    setError("");
    void adminRequest<CustomerContactPage>(`/api/v1/admin/customers?${params.toString()}`)
      .then((loaded) => {
        if (!active) return;
        setResult(loaded);
        if (loaded.page !== page) setPage(loaded.page);
      })
      .catch((reason: unknown) => {
        if (active) setError(reason instanceof Error ? reason.message : "Customer contacts could not be loaded.");
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => { active = false; };
  }, [appliedQuery, page]);

  function submitSearch(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setPage(1);
    setAppliedQuery(query.trim());
  }

  function clearSearch() {
    setQuery("");
    setPage(1);
    setAppliedQuery("");
  }

  const pageCount = Math.max(1, Math.ceil(result.totalCount / result.pageSize));
  const firstItem = result.totalCount === 0 ? 0 : (result.page - 1) * result.pageSize + 1;
  const lastItem = Math.min(result.page * result.pageSize, result.totalCount);

  return (
    <section className="admin-card">
      <div className="section-heading">
        <div>
          <h2>Customers</h2>
          <p className="admin-muted">Search the contact details customers provided with their orders.</p>
        </div>
        <span className="user-count">{result.totalCount} order contacts</span>
      </div>

      <form className="customer-search" onSubmit={submitSearch} role="search">
        <label>
          Search name, phone, or order number
          <input type="search" maxLength={120} value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Enter a name, phone number, or order number" />
        </label>
        <button className="button" type="submit" disabled={loading}>Search</button>
        {appliedQuery && <button className="button button-quiet" type="button" onClick={clearSearch}>Clear search</button>}
      </form>

      <p className="customer-directory-note">Each record represents one order contact. Orders are shown separately; customer records are not merged.</p>
      {error && <p className="admin-error" role="alert">{error}</p>}
      {loading ? <p className="admin-muted" role="status">Loading customer contacts…</p> : (
        <>
          <div className="customer-contact-list">
            {result.items.map((contact) => (
              <article className="customer-contact-card" key={contact.id}>
                <div className="customer-contact-heading">
                  <div><h3>{contact.customerName}</h3><a href={`tel:${encodeURIComponent(contact.phoneNumber)}`}>{contact.phoneNumber}</a></div>
                  <time dateTime={contact.createdAtUtc}>{new Date(contact.createdAtUtc).toLocaleString()}</time>
                </div>
                <div className="customer-contact-order">
                  <strong>{contact.orderNumber}</strong>
                  <span>{contact.status}</span>
                  <span>{formatPrice(contact.orderTotal, contact.currencyCode)}</span>
                </div>
                <p className="customer-contact-address">{contact.deliveryAddress}</p>
              </article>
            ))}
            {result.items.length === 0 && <p className="admin-muted">{appliedQuery ? "No order contacts match this search." : "No customer orders have been placed yet."}</p>}
          </div>

          {result.totalCount > 0 && (
            <div className="customer-pagination">
              <span>Showing {firstItem}–{lastItem} of {result.totalCount}</span>
              <div>
                <button className="button button-quiet" type="button" onClick={() => setPage((current) => Math.max(1, current - 1))} disabled={loading || result.page <= 1}>Previous</button>
                <span>Page {result.page} of {pageCount}</span>
                <button className="button button-quiet" type="button" onClick={() => setPage((current) => Math.min(pageCount, current + 1))} disabled={loading || result.page >= pageCount}>Next</button>
              </div>
            </div>
          )}
        </>
      )}
    </section>
  );
}

function formatPrice(amount: number, currencyCode: string): string {
  try {
    return new Intl.NumberFormat(undefined, { style: "currency", currency: currencyCode }).format(amount);
  } catch {
    return `${currencyCode} ${amount.toFixed(2)}`;
  }
}
