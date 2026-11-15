import { FormEvent, useEffect, useState } from "react";
import AdminApp from "./AdminApp";
import { getMediaUrl, getPublishedProduct, placeStoreOrder, quoteCartSelection, StorefrontProduct, StoreOrderConfirmation } from "./storeApi";
import { addCartSelection, CartLine, changeCartSelectionQuantity, removeCartSelection } from "./cart";
import { storeLanguages, StoreLanguage, storeText } from "./storeI18n";

export default function App() {
  if (window.location.pathname.startsWith("/admin")) {
    return <AdminApp />;
  }

  return <Storefront />;
}

function Storefront() {
  const [language, setLanguage] = useState<StoreLanguage>(() => {
    const saved = window.localStorage.getItem("onecart-language");
    return saved === "bn" || saved === "es" ? saved : "en";
  });
  const text = storeText(language);
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
  const [checkoutOpen, setCheckoutOpen] = useState(false);
  const [checkoutBusy, setCheckoutBusy] = useState(false);
  const [checkoutError, setCheckoutError] = useState("");
  const [customerName, setCustomerName] = useState("");
  const [phoneNumber, setPhoneNumber] = useState("");
  const [deliveryAddress, setDeliveryAddress] = useState("");
  const [orderConfirmation, setOrderConfirmation] = useState<StoreOrderConfirmation | null>(null);

  useEffect(() => {
    let active = true;
    void getPublishedProduct(language)
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
  }, [language]);

  useEffect(() => {
    window.localStorage.setItem("onecart-language", language);
    document.documentElement.lang = language;
  }, [language]);

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
    totals[item.currencyCode] = (totals[item.currencyCode] ?? 0) + item.unitPrice * item.quantity;
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

  async function addToCart() {
    if (!selectedVariant || !selectedVariant.isAvailable || !requiredAttributesSelected) return;
    setCheckoutError("");
    setCheckoutBusy(true);
    const optionIds = selectedOptions.map((option) => option.id).sort();
    try {
      const quote = await quoteCartSelection(selectedVariant.id, optionIds);
      const result = addCartSelection(cartItems, selectedVariant.id, optionIds, quantity, quote);
      if (result.error) {
        setCartMessage(result.error);
        setCartOpen(true);
        return;
      }
      setCartItems(result.items);
      setCartMessage(`${quantity} ${quantity === 1 ? "item" : "items"} added to your cart at the quoted price.`);
      setCartOpen(true);
    } catch (reason) {
      setCartMessage(reason instanceof Error ? reason.message : "Could not price this cart selection.");
    } finally {
      setCheckoutBusy(false);
    }
  }

  function changeCartQuantity(key: string, change: number) {
    setCartItems((current) => changeCartSelectionQuantity(current, key, change));
  }

  function removeCartItem(key: string) {
    setCartItems((current) => removeCartSelection(current, key));
  }

  async function submitOrder(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setCheckoutBusy(true);
    setCheckoutError("");
    try {
      const order = await placeStoreOrder({
        customerName,
        phoneNumber,
        deliveryAddress,
        lines: cartItems.map((item) => ({ quoteToken: item.quoteToken, quantity: item.quantity }))
      });
      setOrderConfirmation(order);
      setCartItems([]);
      setCheckoutOpen(false);
      setCustomerName("");
      setPhoneNumber("");
      setDeliveryAddress("");
      setCartMessage("");
    } catch (reason) {
      setCheckoutError(reason instanceof Error ? reason.message : "The order could not be placed. Review your cart and try again.");
    } finally {
      setCheckoutBusy(false);
    }
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
        <a className="brand" href="/" aria-label={`${product?.brandName ?? "Pretty Shop"} home`}>
          {product?.brandLogoUrl
            ? <img className="brand-logo" src={getMediaUrl(product.brandLogoUrl)} alt="" />
            : <span className="brand-mark">{(product?.brandName ?? "Pretty Shop").slice(0, 1).toUpperCase()}</span>}
          <span className="brand-copy">
            <span className="brand-name">{product?.brandName ?? "Pretty Shop"}</span>
            {(product?.storefrontHeadline ?? "Your trust, Your Love") && (
              <span className="brand-headline">{product?.storefrontHeadline ?? "Your trust, Your Love"}</span>
            )}
          </span>
        </a>
        <div className="store-header-actions">
          <label className="store-language-picker"><span className="visually-hidden">Language</span><select value={language} onChange={event => setLanguage(event.target.value as StoreLanguage)} aria-label="Language">{storeLanguages.map(item => <option key={item.code} value={item.code}>{item.label}</option>)}</select></label>
          {product && <button className="cart-toggle" type="button" onClick={() => setCartOpen((open) => !open)} aria-expanded={cartOpen}>{text.cart} <span>{cartItemCount}</span></button>}
          <a className="status" href="/admin">{text.admin}</a>
        </div>
      </header>

      {loading ? (
        <section className="hero" aria-live="polite"><h1>{text.loading}</h1></section>
      ) : error ? (
        <section className="hero" role="alert"><h1>{text.unavailable}</h1><p className="intro">{error}</p></section>
      ) : product ? (
        <section className={`store-product${product.images.length > 0 ? " has-images" : ""}`} aria-labelledby="hero-title">
          <div className="product-copy">
            <h1 id="hero-title">{product.name}</h1>
            {product.storefrontSubtitle && <p className="product-subtitle">{product.storefrontSubtitle}</p>}
            <p className="intro">{product.description}</p>
          </div>
          {selectedImage && (
            <div className="product-gallery" aria-label={text.productImages}>
              <figure className="product-image-stage">
                <img src={getMediaUrl(selectedImage.url)} alt={selectedImage.altText} />
              </figure>
              {product.images.length > 1 && (
                <div className="product-thumbnails" aria-label={text.chooseImage}>
                  {product.images.map((image) => (
                    <button
                      className={`product-thumbnail${selectedImage.id === image.id ? " selected" : ""}`}
                      type="button"
                      key={image.id}
                      onClick={() => setSelectedImageId(image.id)}
                      aria-label={`${text.chooseImage}: ${image.altText}`}
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
                <span className="flash-sale-label">{product.flashSale.label || text.flash}</span>
                <span className="flash-sale-timer">{text.ends} {formatCountdown(saleRemainingSeconds)}</span>
              </div>
            )}
            <p className="eyebrow">{text.choose}</p>
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
                    {variant.isAvailable ? text.stock : text.out}
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
            <div className="quantity-control">
              <label htmlFor="store-quantity">{text.quantity}</label>
              <div className="quantity-stepper">
                <button type="button" aria-label={text.decrease} disabled={quantity <= 1} onClick={() => setQuantity((value) => Math.max(1, value - 1))}>−</button>
                <input id="store-quantity" type="number" inputMode="numeric" min="1" step="1" value={quantity} onChange={(event) => setQuantity(Math.max(1, Math.trunc(Number(event.target.value) || 1)))} />
                <button type="button" aria-label={text.increase} onClick={() => setQuantity((value) => value + 1)}>+</button>
              </div>
            </div>
            <p className="store-total" aria-live="polite">{text.total}: {formatPrice(currentTotal * quantity, selectedVariant?.currencyCode ?? "USD")}</p>
            {flashSaleActive && selectedVariant?.salePrice != null && (
              <p className="discount-note">
                {text.save} {product.flashSale.discountType === "percentage"
                  ? `${product.flashSale.discountValue}%`
                  : formatPrice(selectedVariant.price - selectedVariant.salePrice, selectedVariant.currencyCode)} {text.duringSale}
              </p>
            )}
            <button type="button" disabled={!selectedVariant?.isAvailable || !requiredAttributesSelected || checkoutBusy} onClick={() => void addToCart()}>
              {text.add}
            </button>
            <p className="note">{!requiredAttributesSelected ? text.chooseRequired : !selectedVariant?.isAvailable ? text.soldOut : `${text.cod} ${text.availableAfter}`}</p>
            {(product.socialSharingEnabled || product.whatsAppContactUrl) && (
              <div className="product-contact-actions">
                {product.socialSharingEnabled && <button className="button-secondary" type="button" onClick={() => void shareProduct()}>{text.share}</button>}
                {product.whatsAppContactUrl && <a className="button-whatsapp" href={product.whatsAppContactUrl} target="_blank" rel="noopener noreferrer">
                  <svg className="whatsapp-icon" viewBox="0 0 32 32" aria-hidden="true" focusable="false">
                    <path fill="currentColor" d="M16.04 3A12.9 12.9 0 0 0 4.93 22.47L3.2 28.8l6.49-1.7A12.9 12.9 0 1 0 16.04 3Zm0 23.47c-1.92 0-3.8-.51-5.44-1.47l-.39-.23-3.85 1.01 1.03-3.75-.25-.4a10.57 10.57 0 1 1 8.9 4.84Zm5.8-7.92c-.32-.16-1.9-.94-2.2-1.05-.3-.11-.52-.16-.74.16-.22.32-.84 1.05-1.03 1.27-.19.21-.38.24-.7.08-.32-.16-1.36-.5-2.59-1.59-.96-.86-1.61-1.92-1.8-2.24-.19-.32-.02-.49.14-.65.14-.14.32-.38.48-.56.16-.19.21-.32.32-.54.11-.21.05-.4-.03-.56-.08-.16-.73-1.76-1-2.41-.27-.63-.54-.55-.73-.56h-.62c-.21 0-.56.08-.86.4-.3.32-1.13 1.1-1.13 2.68s1.16 3.11 1.32 3.32c.16.22 2.28 3.48 5.51 4.88.77.33 1.37.53 1.84.68.77.24 1.47.21 2.03.13.62-.09 1.9-.78 2.17-1.54.27-.75.27-1.4.19-1.54-.08-.13-.29-.21-.62-.37Z" />
                  </svg>
                  <span>{text.whatsapp}</span>
                </a>}
              </div>
            )}
            {shareMessage && <p className="share-message" role="status">{shareMessage}</p>}
          </div>
          {product.trustItems.length > 0 && (
            <ul className="store-trust-items" aria-label={text.storeDetails}>
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
          <h1>{text.story}</h1>
          <p className="intro">{text.empty}</p>
          <a className="button store-admin-link" href="/admin">{text.openAdmin}</a>
        </section>
      )}

      {product && cartOpen && (
        <section className="cart-panel" aria-labelledby="cart-title">
          <div className="cart-heading">
            <div><p className="eyebrow">{text.selections}</p><h2 id="cart-title">{text.shoppingCart} ({cartItemCount})</h2></div>
            <button className="button-secondary" type="button" onClick={() => setCartOpen(false)}>{text.closeCart}</button>
          </div>
          {cartItems.length === 0 ? <p className="note">{text.emptyCart}</p> : (
            <>
              <ul className="cart-list">
                {cartItems.map((item) => {
                  const variant = product.variants.find((candidate) => candidate.id === item.variantId);
                  if (!variant) return null;
                  const chosenOptions = product.attributeGroups.flatMap((group) => group.options.filter((option) => item.optionIds.includes(option.id)));
                  return (
                    <li className="cart-item" key={item.key}>
                      <div className="cart-item-copy">
                        <strong>{product.name} · {variant.displayName}</strong>
                        {chosenOptions.length > 0 && <small>{chosenOptions.map((option) => option.name).join(", ")}</small>}
                        <span>{formatPrice(item.unitPrice, item.currencyCode)} {text.each}</span>
                      </div>
                      <div className="cart-item-actions" aria-label={`Quantity for ${variant.displayName}`}>
                        <button type="button" aria-label={text.decrease} onClick={() => changeCartQuantity(item.key, -1)}>−</button>
                        <span>{item.quantity}</span>
                        <button type="button" aria-label={text.increase} onClick={() => changeCartQuantity(item.key, 1)}>+</button>
                        <button className="remove-cart-item" type="button" onClick={() => removeCartItem(item.key)}>{text.remove}</button>
                      </div>
                    </li>
                  );
                })}
              </ul>
              <div className="cart-summary">
                <div><strong>{text.estimatedSubtotal}</strong>{Object.entries(cartSubtotals).map(([currency, amount]) => <strong key={currency}>{formatPrice(amount, currency)}</strong>)}</div>
                <p>{text.priceLocked}</p>
                {!checkoutOpen && <button type="button" onClick={() => { setCheckoutError(""); setCheckoutOpen(true); }}>{text.cod}</button>}
              </div>
              {checkoutOpen && (
                <form className="checkout-form" onSubmit={(event) => void submitOrder(event)}>
                  <h3>{text.deliveryDetails}</h3>
                  <label>{text.customerName}<input autoComplete="name" maxLength={200} required value={customerName} onChange={(event) => setCustomerName(event.target.value)} /></label>
                  <label>{text.phoneNumber}<input type="tel" autoComplete="tel" maxLength={50} required value={phoneNumber} onChange={(event) => setPhoneNumber(event.target.value)} /></label>
                  <label>{text.deliveryAddress}<textarea autoComplete="street-address" maxLength={1000} required rows={3} value={deliveryAddress} onChange={(event) => setDeliveryAddress(event.target.value)} /></label>
                  {checkoutError && <p className="form-error" role="alert">{checkoutError}</p>}
                  <p className="note">{text.pendingPayment}</p>
                  <button type="submit" disabled={checkoutBusy || cartItems.length === 0}>{checkoutBusy ? text.placingOrder : text.placeOrder}</button>
                  <button className="button-secondary" type="button" onClick={() => setCheckoutOpen(false)} disabled={checkoutBusy}>{text.backToCart}</button>
                </form>
              )}
            </>
          )}
          {cartMessage && <p className="share-message" role="status">{cartMessage}</p>}
          {orderConfirmation && (
            <div className="order-confirmation" role="status">
              <p className="eyebrow">{text.orderPlaced}</p>
              <h3>{text.thankYou}</h3>
              <p>{text.orderNumber}: <strong>{orderConfirmation.orderNumber}</strong></p>
              <p>Total: <strong>{formatPrice(orderConfirmation.total, orderConfirmation.currencyCode)}</strong></p>
              <p>{text.keepNumber}</p>
            </div>
          )}
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
