import { useEffect, useState } from "react";
import { adminRequest } from "./adminApi";

interface AdminOrderSummary {
  id: string;
  orderNumber: string;
  status: string;
  customerName: string;
  phoneNumber: string;
  currencyCode: string;
  total: number;
  createdAtUtc: string;
  itemCount: number;
}

interface AdminOrderDetails extends AdminOrderSummary {
  deliveryAddress: string;
  updatedAtUtc: string;
  lines: Array<{
    productName: string;
    sku: string;
    variantName: string;
    selectedOptions: Array<{ id: string; name: string; priceAdjustment: number }>;
    quantity: number;
    unitPrice: number;
  }>;
}

export default function AdminOrders() {
  const [orders, setOrders] = useState<AdminOrderSummary[]>([]);
  const [selected, setSelected] = useState<AdminOrderDetails | null>(null);
  const [status, setStatus] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");

  async function loadOrders(selectedId?: string) {
    const loaded = await adminRequest<AdminOrderSummary[]>("/api/v1/admin/orders");
    setOrders(loaded);
    const orderId = selectedId ?? selected?.id ?? loaded[0]?.id;
    if (!orderId) {
      setSelected(null);
      return;
    }
    const details = await adminRequest<AdminOrderDetails>(`/api/v1/admin/orders/${orderId}`);
    setSelected(details);
    setStatus(details.status);
  }

  useEffect(() => {
    void loadOrders().catch((reason: unknown) => setError(reason instanceof Error ? reason.message : "Orders could not be loaded."));
  }, []);

  async function selectOrder(orderId: string) {
    setError("");
    try {
      const details = await adminRequest<AdminOrderDetails>(`/api/v1/admin/orders/${orderId}`);
      setSelected(details);
      setStatus(details.status);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Order details could not be loaded.");
    }
  }

  async function saveStatus() {
    if (!selected) return;
    setBusy(true);
    setError("");
    setMessage("");
    try {
      const updated = await adminRequest<AdminOrderDetails>(`/api/v1/admin/orders/${selected.id}/status`, {
        method: "PUT",
        body: JSON.stringify({ status })
      });
      setSelected(updated);
      setStatus(updated.status);
      await loadOrders(updated.id);
      setMessage("Order status saved.");
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Order status could not be saved.");
    } finally {
      setBusy(false);
    }
  }

  const nextStatuses = selected?.status === "Pending"
    ? ["Pending", "Processing", "Cancelled"]
    : selected?.status === "Processing"
      ? ["Processing", "Completed", "Cancelled"]
      : selected ? [selected.status] : [];

  return (
    <section className="admin-card">
      <div className="section-heading">
        <div><h2>Orders</h2><p className="admin-muted">Recent Cash on Delivery orders. Cancelling a pending or processing order restores its stock.</p></div>
        <span className="user-count">Latest {orders.length}</span>
      </div>
      <div className="orders-layout">
        <div className="orders-list">
          {orders.map((order) => (
            <button className={`order-list-item${selected?.id === order.id ? " selected" : ""}`} type="button" key={order.id} onClick={() => void selectOrder(order.id)}>
              <strong>{order.orderNumber}</strong>
              <span>{order.customerName} · {order.status}</span>
              <span>{formatPrice(order.total, order.currencyCode)} · {order.itemCount} items</span>
            </button>
          ))}
          {orders.length === 0 && <p className="admin-muted">No orders have been placed.</p>}
        </div>
        {selected && (
          <div className="order-details">
            <div className="section-heading"><div><p className="eyebrow">{selected.status}</p><h3>{selected.orderNumber}</h3></div><time dateTime={selected.createdAtUtc}>{new Date(selected.createdAtUtc).toLocaleString()}</time></div>
            <dl className="order-customer-details">
              <dt>Customer</dt><dd>{selected.customerName}</dd>
              <dt>Phone</dt><dd><a href={`tel:${encodeURIComponent(selected.phoneNumber)}`}>{selected.phoneNumber}</a></dd>
              <dt>Delivery address</dt><dd>{selected.deliveryAddress}</dd>
            </dl>
            <ul className="order-lines">
              {selected.lines.map((line, index) => (
                <li key={`${line.sku}-${index}`}>
                  <strong>{line.productName} · {line.variantName}</strong>
                  <small>SKU {line.sku}{line.selectedOptions.length > 0 ? ` · ${line.selectedOptions.map((option) => option.name).join(", ")}` : ""}</small>
                  <span>{line.quantity} × {formatPrice(line.unitPrice, selected.currencyCode)}</span>
                </li>
              ))}
            </ul>
            <p className="order-total">Total <strong>{formatPrice(selected.total, selected.currencyCode)}</strong></p>
            <div className="order-status-editor">
              <label>Status<select value={status} onChange={(event) => setStatus(event.target.value)} disabled={nextStatuses.length <= 1 || busy}>
                {nextStatuses.map((value) => <option key={value} value={value}>{value}</option>)}
              </select></label>
              <button type="button" onClick={() => void saveStatus()} disabled={busy || status === selected.status || nextStatuses.length <= 1}>Save status</button>
            </div>
          </div>
        )}
      </div>
      {message && <p className="admin-message" role="status">{message}</p>}
      {error && <p className="admin-error" role="alert">{error}</p>}
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
