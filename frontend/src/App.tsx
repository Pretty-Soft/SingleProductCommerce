import { useEffect, useState } from "react";
import AdminApp from "./AdminApp";
import { getMediaUrl, getPublishedProduct, StorefrontProduct } from "./storeApi";
import { addCartSelection, CartLine, changeCartSelectionQuantity, removeCartSelection } from "./cart";

export default function App() {
  if (window.location.pathname.startsWith("/admin")) {
    return <AdminApp />;
  }

  return <Storefront />;
}

function Storefront() {
  const [product, setProduct] = useState<StorefrontProduct | null>(null);
  const [selectedVariantId, setSelectedVariantId] = useState("");
  const [selectedImageId, setSelectedImageId] = useState("");
  const [selectedAttributeOptions, setSelectedAttributeOptions] = useState<Record<string, string[]>>({});
  const [quantity, setQuantity] = useState(1);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [clientTimeMs, setClientTimeMs] = useState(Date.now());
  const [productReceivedAtMs, setProductReceivedAtMs] = useState<number | null>(null);
  const [shareMessage, setShareMessage] = useState("");
  const [cartItems, setCartItems] = useState<CartLine[]>([]);
  const [cartOpen, setCartOpen] = useState(false);
  const [cartMessage, setCartMessage] = useState("");

  useEffect(() => {
    let active = true;
    void getPublishedProduct()
      .then((loaded) => {
        if (!active) return;
        setProduct(loaded);
        setSelectedVariantId(loaded?.variants[0]?.id ?? "");
        setSelectedImageId(loaded?.images[0]?.id ?? "");
        setProductReceivedAtMs(Date.now());
      })
      .catch(() => {
        if (active) setError("The secure store API could not be reached. Please try again later.");
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => { active = false; };
  }, []);

  useEffect(() => {
    if (!product?.flashSale.enabled || !product.flashSale.endsAtUtc) return;
    const interval = window.setInterval(() => setClientTimeMs(Date.now()), 1000);
    return () => window.clearInterval(interval);
  }, [product?.flashSale.enabled, product?.flashSale.endsAtUtc]);

  const selectedVariant = product?.variants.find((variant) => variant.id === selectedVariantId);
  const selectedImage = product?.images.find((image) => image.id === selectedImageId) ?? product?.images[0];
  const selectedOptions = product?.attributeGroups.flatMap((group) => {
    const selected = new Set(selectedAttributeOptions[group.id] ?? []);
    return group.options.filter((option) => selected.has(option.id));
  }) ?? [];
  const requiredAttributesSelected = product?.attributeGroups.every((group) =>
    !group.isRequired || (selectedAttributeOptions[group.id]?.length ?? 0) > 0
  ) ?? false;
  const estimatedServerNow = product && productReceivedAtMs !== null
    ? Date.parse(product.flashSale.serverTimeUtc) + Math.max(0, clientTimeMs - productReceivedAtMs)
    : clientTimeMs;
  const flashSaleStartMs = product?.flashSale.startsAtUtc ? Date.parse(product.flashSale.startsAtUtc) : Number.NaN;
  const flashSaleEndMs = product?.flashSale.endsAtUtc ? Date.parse(product.flashSale.endsAtUtc) : Number.NaN;
  const flashSaleActive = Boolean(product?.flashSale.enabled)
    && Number.isFinite(flashSaleStartMs)
    && Number.isFinite(flashSaleEndMs)
    && estimatedServerNow >= flashSaleStartMs
    && estimatedServerNow < flashSaleEndMs;
  const saleRemainingSeconds = flashSaleActive ? Math.max(0, Math.floor((flashSaleEndMs - estimatedServerNow) / 1000)) : 0;
  const selectedBasePrice = flashSaleActive && selectedVariant?.salePrice != null
    ? selectedVariant.salePrice
    : selectedVariant?.price ?? 0;
  const currentTotal = selectedBasePrice + selectedOptions.reduce((total, option) => total + option.priceAdjustment, 0);
  const cartItemCount = cartItems.reduce((total, item) => total + item.quantity, 0);
  const cartSubtotals = cartItems.reduce<Record<string, number>>((totals, item) => {
    const variant = product?.variants.find((candidate) => candidate.id === item.variantId);
    if (!product || !variant) return totals;
    const unitPrice = (flashSaleActive && variant.salePrice != null ? variant.salePrice : variant.price)
      + product.attributeGroups
        .flatMap((group) => group.options)
        .filter((option) => item.optionIds.includes(option.id))
        .reduce((sum, option) => sum + option.priceAdjustment, 0);
    totals[item.currencyCode] = (totals[item.currencyCode] ?? 0) + unitPrice * item.quantity;
    return totals;
  }, {});

  function chooseAttributeOption(groupId: string, optionId: string, allowMultiple: boolean) {
    setSelectedAttributeOptions((current) => {
      const selected = current[groupId] ?? [];
      const next = allowMultiple
        ? selected.includes(optionId) ? selected.filter((id) => id !== optionId) : [...selected, optionId]
        : [optionId];
      return { ...current, [groupId]: next };
    });
  }

  function addToCart() {
    if (!selectedVariant || !selectedVariant.isAvailable || !requiredAttributesSelected) return;
    const optionIds = selectedOptions.map((option) => option.id).sort();
    const result = addCartSelection(cartItems, selectedVariant.id, optionIds, quantity, selectedVariant.currencyCode);
    if (result.error) {
      setCartMessage(result.error);
      setCartOpen(true);
      return;
    }
    setCartItems(result.items);
    setCartMessage(`${quantity} ${quantity === 1 ? "item" : "items"} added to your cart.`);
    setCartOpen(true);
  }

  function changeCartQuantity(key: string, change: number) {
    setCartItems((current) => changeCartSelectionQuantity(current, key, change));
  }

  function removeCartItem(key: string) {
    setCartItems((current) => removeCartSelection(current, key));
  }

  async function shareProduct() {
    if (!product) return;
    const url = window.location.href;
    try {
      if (navigator.share) {
        await navigator.share({ title: product.name, text: product.description, url });
        setShareMessage("Product link shared.");
      } else if (navigator.clipboard) {
        await navigator.clipboard.writeText(url);
        setShareMessage("Product link copied.");
      } else {
        window.prompt("Copy this product link", url);
      }
    } catch (reason) {
      if (reason instanceof Error && reason.name === "AbortError") return;
      setShareMessage("Could not share the product link.");
    }
  }

  return (
    <main className="page">
      <header className="topbar">
        <a className="brand" href="/" aria-label="OneCart Commerce home">
          {product?.brandLogoUrl
            ? <img className="brand-logo" src={getMediaUrl(product.brandLogoUrl)} alt="" />
            : <span className="brand-mark">{(product?.brandName ?? "OneCart").slice(0, 1).toUpperCase()}</span>}
          <span>{product?.brandName ?? "OneCart"}</span>
        </a>
        <div className="store-header-actions">
          {product && <button className="cart-toggle" type="button" onClick={() => setCartOpen((open) => !open)} aria-expanded={cartOpen}>Cart <span>{cartItemCount}</span></button>}
          <a className="status" href="/admin">Admin</a>
        </div>
      </header>

      {loading ? (
        <section className="hero" aria-live="polite"><p className="eyebrow">OneCart Commerce</p><h1>Loading store…</h1></section>
      ) : error ? (
        <section className="hero" role="alert"><p className="eyebrow">OneCart Commerce</p><h1>Store temporarily unavailable</h1><p className="intro">{error}</p></section>
      ) : product ? (
        <section className={`store-product${product.images.length > 0 ? " has-images" : ""}`} aria-labelledby="hero-title">
          <div className="product-copy">
            <p className="eyebrow">{product.brandName}</p>
            <h1 id="hero-title">{product.storefrontHeadline || product.name}</h1>
            {product.storefrontSubtitle && <p className="product-subtitle">{product.storefrontSubtitle}</p>}
            {product.storefrontHeadline && <p className="product-name-note">{product.name}</p>}
            <p className="intro">{product.description}</p>
          </div>
          {selectedImage && (
            <div className="product-gallery" aria-label="Product images">
              <figure className="product-image-stage">
                <img src={getMediaUrl(selectedImage.url)} alt={selectedImage.altText} />
              </figure>
              {product.images.length > 1 && (
                <div className="product-thumbnails" aria-label="Choose product image">
                  {product.images.map((image) => (
                    <button
                      className={`product-thumbnail${selectedImage.id === image.id ? " selected" : ""}`}
                      type="button"
                      key={image.id}
                      onClick={() => setSelectedImageId(image.id)}
                      aria-label={`Show image: ${image.altText}`}
                      aria-pressed={selectedImage.id === image.id}
                    >
                      <img src={getMediaUrl(image.url)} alt="" />
                    </button>
                  ))}
                </div>
              )}
            </div>
          )}
          <div className="purchase-panel">
            {flashSaleActive && (
              <div className="flash-sale-banner" aria-live="polite">
                <span className="flash-sale-label">{product.flashSale.label || "Flash sale"}</span>
                <span className="flash-sale-timer">Ends in {formatCountdown(saleRemainingSeconds)}</span>
              </div>
            )}
            <p className="eyebrow">Choose your product</p>
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
                  <span className="store-variant-price">
                    {flashSaleActive && variant.salePrice != null ? (
                      <span className="price-stack">
                        <del>{formatPrice(variant.price, variant.currencyCode)}</del>
                        <strong>{formatPrice(variant.salePrice, variant.currencyCode)}</strong>
                      </span>
                    ) : formatPrice(variant.price, variant.currencyCode)}
                  </span>
                  <span className={variant.isAvailable ? "availability" : "availability unavailable"}>
                    {variant.isAvailable ? "In stock" : "Out of stock"}
                  </span>
                </label>
              ))}
            </div>
            {product.attributeGroups.map((group) => (
              <fieldset className="store-attribute" key={group.id}>
                <legend>{group.name}{group.isRequired ? " *" : " (optional)"}</legend>
                <div className="attribute-options">
                  {group.options.map((option) => {
                    const checked = (selectedAttributeOptions[group.id] ?? []).includes(option.id);
                    return (
                      <label className="store-attribute-option" key={option.id}>
                        <input
                          type={group.allowMultiple ? "checkbox" : "radio"}
                          name={`attribute-${group.id}`}
                          required={group.isRequired && !group.allowMultiple}
                          checked={checked}
                          onChange={() => chooseAttributeOption(group.id, option.id, group.allowMultiple)}
                        />
                        <span>{option.name}</span>
                        {option.priceAdjustment > 0 && <span className="store-variant-price">+{formatPrice(option.priceAdjustment, selectedVariant?.currencyCode ?? "USD")}</span>}
                      </label>
                    );
                  })}
                </div>
              </fieldset>
            ))}
            <label className="quantity-control">
              Quantity
              <input type="number" min="1" step="1" value={quantity} onChange={(event) => setQuantity(Math.max(1, Math.trunc(Number(event.target.value) || 1)))} />
            </label>
            <p className="store-total" aria-live="polite">Total: {formatPrice(currentTotal * quantity, selectedVariant?.currencyCode ?? "USD")}</p>
            {flashSaleActive && selectedVariant?.salePrice != null && (
              <p className="discount-note">
                Save {product.flashSale.discountType === "percentage"
                  ? `${product.flashSale.discountValue}%`
                  : formatPrice(selectedVariant.price - selectedVariant.salePrice, selectedVariant.currencyCode)} during this sale
              </p>
            )}
            <button type="button" disabled={!selectedVariant?.isAvailable || !requiredAttributesSelected} onClick={addToCart}>
              Add to cart
            </button>
            <p className="note">{!requiredAttributesSelected ? "Choose an option for every required attribute." : !selectedVariant?.isAvailable ? "This option is currently out of stock." : "Cash on Delivery checkout will be available after store setup is complete."}</p>
            {(product.socialSharingEnabled || product.whatsAppContactUrl) && (
              <div className="product-contact-actions">
                {product.socialSharingEnabled && <button className="button-secondary" type="button" onClick={() => void shareProduct()}>Share product</button>}
                {product.whatsAppContactUrl && <a className="button-whatsapp" href={product.whatsAppContactUrl} target="_blank" rel="noopener noreferrer">Chat with us on WhatsApp</a>}
              </div>
            )}
            {shareMessage && <p className="share-message" role="status">{shareMessage}</p>}
          </div>
          {product.trustItems.length > 0 && (
            <ul className="store-trust-items" aria-label="Store service details">
              {product.trustItems.map((item) => (
                <li key={item.title}>
                  <span className="trust-mark" aria-hidden="true">✓</span>
                  <span><strong>{item.title}</strong>{item.description && <small>{item.description}</small>}</span>
                </li>
              ))}
            </ul>
          )}
        </section>
      ) : (
        <section className="hero">
          <p className="eyebrow">Single product commerce</p>
          <h1>Your product story starts here.</h1>
          <p className="intro">This store is ready for its first product. A System Admin can configure and publish it from the admin panel.</p>
          <a className="button store-admin-link" href="/admin">Open admin</a>
        </section>
      )}

      {product && cartOpen && (
        <section className="cart-panel" aria-labelledby="cart-title">
          <div className="cart-heading">
            <div><p className="eyebrow">Your selections</p><h2 id="cart-title">Shopping cart ({cartItemCount})</h2></div>
            <button className="button-secondary" type="button" onClick={() => setCartOpen(false)}>Close cart</button>
          </div>
          {cartItems.length === 0 ? <p className="note">Your cart is empty. Choose your product options and add them here.</p> : (
            <>
              <ul className="cart-list">
                {cartItems.map((item) => {
                  const variant = product.variants.find((candidate) => candidate.id === item.variantId);
                  if (!variant) return null;
                  const chosenOptions = product.attributeGroups.flatMap((group) => group.options.filter((option) => item.optionIds.includes(option.id)));
                  const unitPrice = (flashSaleActive && variant.salePrice != null ? variant.salePrice : variant.price)
                    + chosenOptions.reduce((sum, option) => sum + option.priceAdjustment, 0);
                  return (
                    <li className="cart-item" key={item.key}>
                      <div className="cart-item-copy">
                        <strong>{product.name} · {variant.displayName}</strong>
                        {chosenOptions.length > 0 && <small>{chosenOptions.map((option) => option.name).join(", ")}</small>}
                        <span>{formatPrice(unitPrice, item.currencyCode)} each</span>
                      </div>
                      <div className="cart-item-actions" aria-label={`Quantity for ${variant.displayName}`}>
                        <button type="button" aria-label="Decrease quantity" onClick={() => changeCartQuantity(item.key, -1)}>−</button>
                        <span>{item.quantity}</span>
                        <button type="button" aria-label="Increase quantity" onClick={() => changeCartQuantity(item.key, 1)}>+</button>
                        <button className="remove-cart-item" type="button" onClick={() => removeCartItem(item.key)}>Remove</button>
                      </div>
                    </li>
                  );
                })}
              </ul>
              <div className="cart-summary">
                <div><strong>Estimated subtotal</strong>{Object.entries(cartSubtotals).map(([currency, amount]) => <strong key={currency}>{formatPrice(amount, currency)}</strong>)}</div>
                <p>Prices and availability are confirmed when COD checkout is enabled.</p>
              </div>
            </>
          )}
          {cartMessage && <p className="share-message" role="status">{cartMessage}</p>}
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

function formatCountdown(seconds: number): string {
  const days = Math.floor(seconds / 86_400);
  const hours = Math.floor((seconds % 86_400) / 3_600);
  const minutes = Math.floor((seconds % 3_600) / 60);
  const remainingSeconds = seconds % 60;
  const time = [hours, minutes, remainingSeconds].map((value) => String(value).padStart(2, "0")).join(":");
  return days > 0 ? `${days}d ${time}` : time;
}
