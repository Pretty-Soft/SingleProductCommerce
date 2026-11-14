import { useEffect, useState } from "react";
import AdminApp from "./AdminApp";
import { getPublishedProduct, StorefrontProduct } from "./storeApi";

export default function App() {
  if (window.location.pathname.startsWith("/admin")) {
    return <AdminApp />;
  }

  return <Storefront />;
}

function Storefront() {
  const [product, setProduct] = useState<StorefrontProduct | null>(null);
  const [selectedVariantId, setSelectedVariantId] = useState("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    let active = true;
    void getPublishedProduct()
      .then((loaded) => {
        if (!active) return;
        setProduct(loaded);
        setSelectedVariantId(loaded?.variants[0]?.id ?? "");
      })
      .catch(() => {
        if (active) setError("The secure store API could not be reached. Please try again later.");
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => { active = false; };
  }, []);

  const selectedVariant = product?.variants.find((variant) => variant.id === selectedVariantId);

  return (
    <main className="page">
      <header className="topbar">
        <a className="brand" href="/" aria-label="OneCart Commerce home">
          <span className="brand-mark">O</span>
          <span>OneCart</span>
        </a>
        <a className="status" href="/admin">Admin</a>
      </header>

      {loading ? (
        <section className="hero" aria-live="polite"><p className="eyebrow">OneCart Commerce</p><h1>Loading store…</h1></section>
      ) : error ? (
        <section className="hero" role="alert"><p className="eyebrow">OneCart Commerce</p><h1>Store temporarily unavailable</h1><p className="intro">{error}</p></section>
      ) : product ? (
        <section className="store-product" aria-labelledby="hero-title">
          <div className="product-copy">
            <p className="eyebrow">Featured product</p>
            <h1 id="hero-title">{product.name}</h1>
            <p className="intro">{product.description}</p>
          </div>
          <div className="purchase-panel">
            <p className="eyebrow">Choose an option</p>
            <div className="variant-options" role="radiogroup" aria-label="Product variants">
              {product.variants.map((variant) => (
                <label className={`store-variant${selectedVariantId === variant.id ? " selected" : ""}`} key={variant.id}>
                  <input
                    type="radio"
                    name="variant"
                    value={variant.id}
                    checked={selectedVariantId === variant.id}
                    onChange={() => setSelectedVariantId(variant.id)}
                  />
                  <span className="store-variant-name">{variant.displayName}</span>
                  <span className="store-variant-price">{formatPrice(variant.price, variant.currencyCode)}</span>
                  <span className={variant.isAvailable ? "availability" : "availability unavailable"}>
                    {variant.isAvailable ? "In stock" : "Out of stock"}
                  </span>
                </label>
              ))}
            </div>
            <button type="button" disabled={!selectedVariant?.isAvailable}>
              Cash on Delivery checkout coming next
            </button>
            <p className="note">Orders are not available yet.</p>
          </div>
        </section>
      ) : (
        <section className="hero">
          <p className="eyebrow">Single product commerce</p>
          <h1>Your product story starts here.</h1>
          <p className="intro">This store is ready for its first product. A System Admin can configure and publish it from the admin panel.</p>
          <a className="button store-admin-link" href="/admin">Open admin</a>
        </section>
      )}

      <footer>OneCart Commerce · ASP.NET Core + React</footer>
    </main>
  );
}

function formatPrice(amount: number, currencyCode: string): string {
  try {
    return new Intl.NumberFormat(undefined, { style: "currency", currency: currencyCode }).format(amount);
  } catch {
    return `${currencyCode} ${amount.toFixed(2)}`;
  }
}
