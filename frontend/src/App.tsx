import { CSSProperties, FormEvent, useEffect, useState } from "react";
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
  const selectedImageIndex = product && selectedImage ? product.images.findIndex(image => image.id === selectedImage.id) : -1;
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
  const saleCountdownParts = [Math.min(99, Math.floor(saleRemainingSeconds / 3600)), Math.floor((saleRemainingSeconds % 3600) / 60), saleRemainingSeconds % 60];
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

  function moveImage(offset: number) {
    if (!product || product.images.length < 2) return;
    const next = (selectedImageIndex + offset + product.images.length) % product.images.length;
    setSelectedImageId(product.images[next].id);
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
    <main className="page" style={product ? {
      "--theme-primary": product.theme.primaryColor,
      "--theme-accent": product.theme.accentColor,
      "--theme-text": product.theme.textColor,
      "--theme-muted": product.theme.mutedTextColor,
      "--theme-background": product.theme.backgroundColor,
      "--theme-surface": product.theme.surfaceColor,
      "--theme-border": product.theme.borderColor,
      "--theme-title-size": `${product.theme.productTitleFontSizePx}px`,
      "--theme-details-size": `${product.theme.detailsFontSizePx}px`
    } as CSSProperties : undefined}>
      {product && <div className="store-assurance-strip">
        <span><i aria-hidden="true">✓</i> {product.trustItems[0]?.title ?? text.authentic}</span>
        {product.trustItems[0]?.description && <span className="topbar-trust-description">{product.trustItems[0].description}</span>}
        {product.whatsAppContactUrl && <a href={product.whatsAppContactUrl} target="_blank" rel="noopener noreferrer"><svg className="whatsapp-topbar-icon" viewBox="0 0 32 32" aria-hidden="true"><path d="M16 3a12 12 0 0 0-10.3 18.2L4 28l7-1.8A12 12 0 1 0 16 3Z" /><path d="M12 10c-.7 0-1.2.4-1.5 1-.4.8-.5 1.7-.1 2.7.6 1.8 2.7 4.5 5.4 5.8 1.2.6 2.4.7 3.4.3.7-.3 1.1-.9 1.1-1.6 0-.3-.2-.5-.5-.7l-2-.9c-.3-.1-.5-.1-.7.2l-.8 1c-.2.2-.4.2-.7.1-1.4-.6-2.5-1.6-3.3-2.9-.2-.3-.1-.5.1-.7l.6-.7c.2-.2.2-.5.1-.7l-.9-2c-.1-.4-.4-.9-.8-.9Z" /></svg> WhatsApp: {getWhatsAppNumber(product.whatsAppContactUrl) || text.whatsappMessage}</a>}
      </div>}
      <header className="topbar">
        <a className="brand-logo-link" href="/" aria-label={`${product?.brandName ?? "Pretty Shop"} home`}>
          {product?.brandLogoUrl
            ? <img className="brand-logo" src={getMediaUrl(product.brandLogoUrl)} alt="" />
            : <span className="brand-mark">{(product?.brandName ?? "Pretty Shop").slice(0, 1).toUpperCase()}</span>}
          <span className="brand-logo-copy"><strong>{product?.brandName ?? "Pretty Shop"}</strong><small>{product?.storefrontHeadline ?? "Your trust, Your Love"}</small></span>
        </a>
        <div className="store-header-actions">
          <label className="store-language-picker">
            <span className="visually-hidden">Language</span>
            <svg viewBox="0 0 24 24" aria-hidden="true" focusable="false"><circle cx="12" cy="12" r="9" /><path d="M3 12h18M12 3a15 15 0 0 1 0 18M12 3a15 15 0 0 0 0 18" /></svg>
            <select value={language} onChange={event => setLanguage(event.target.value as StoreLanguage)} aria-label="Language">
              {storeLanguages.map(item => <option key={item.code} value={item.code}>{item.label}</option>)}
            </select>
          </label>
          {product?.aboutUs.trim() && <a className="header-about-link" href="#about-us">{text.aboutUs}</a>}
          {product && <button className="cart-toggle" type="button" onClick={() => setCartOpen((open) => !open)} aria-expanded={cartOpen} aria-label={`${text.shoppingCart}, ${cartItemCount}`}><svg viewBox="0 0 32 32" aria-hidden="true"><path d="M3 4h3l3.2 16.1h15.3l3.1-11.6H8M12 26a1.5 1.5 0 1 0 0 .1M23 26a1.5 1.5 0 1 0 0 .1" /></svg><span>{cartItemCount}</span></button>}
        </div>
      </header>

      {loading ? (
        <section className="hero" aria-live="polite"><h1>{text.loading}</h1></section>
      ) : error ? (
        <section className="hero" role="alert"><h1>{text.unavailable}</h1><p className="intro">{error}</p></section>
      ) : product ? (
        <section id="product" className={`store-product${product.images.length > 0 ? " has-images" : ""}`} aria-labelledby="hero-title">
          <div className="product-copy">
            {product.storefrontSubtitle && <p className="product-kicker">{product.storefrontSubtitle}</p>}
            <h1 id="hero-title">{product.name}</h1>
            {product.reviewCount > 0 && <a className="product-rating" href="#reviews" aria-label={`${product.averageRating} ${text.rating}, ${product.reviewCount} ${product.reviewCount === 1 ? text.review : text.reviewsPlural}`}>
              <span className="rating-stars" aria-hidden="true">{renderStars(product.averageRating)}</span>
              <strong>{product.averageRating.toFixed(1)}</strong><span>({product.reviewCount} {product.reviewCount === 1 ? text.review : text.reviewsPlural})</span>
            </a>}
            <p className="intro">{product.description}</p>
            {product.benefits.length > 0 && <ul className="hero-benefits" aria-label={text.whyChoose}>{product.benefits.slice(0, 4).map(benefit => <li key={benefit.id}><span aria-hidden="true">✓</span>{benefit.title}</li>)}</ul>}
          </div>
          <div className={`product-gallery${product.images.length > 1 ? " has-thumbnails" : ""}`} aria-label={text.productImages}>
            {selectedImage ? <>
              <figure className="product-image-stage">
                <img src={getMediaUrl(selectedImage.url)} alt={selectedImage.altText} />
                {product.images.length > 1 && <>
                  <button type="button" className="gallery-arrow gallery-arrow-previous" onClick={() => moveImage(-1)} aria-label={text.previousImage}>‹</button>
                  <button type="button" className="gallery-arrow gallery-arrow-next" onClick={() => moveImage(1)} aria-label={text.nextImage}>›</button>
                  <span className="gallery-counter">{selectedImageIndex + 1} / {product.images.length}</span>
                </>}
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
              {product.images.length > 1 && <div className="gallery-dots" aria-label={text.chooseImage}>{product.images.map((image, index) => <button key={image.id} type="button" className={selectedImage.id === image.id ? "active" : ""} aria-label={`${text.chooseImage} ${index + 1}`} aria-current={selectedImage.id === image.id ? "true" : undefined} onClick={() => setSelectedImageId(image.id)} />)}</div>}
            </> : <div className="product-image-stage product-image-empty" aria-label={text.productImages}><span>{product.name}</span></div>}
          </div>
          <div className="purchase-panel">
            {flashSaleActive && (
              <div className="flash-sale-banner" aria-live="polite">
                <strong className="flash-sale-label"><span aria-hidden="true">ϟ</span>{product.flashSale.label || text.flash}</strong>
                <span className="flash-sale-timer-label">{text.ends}</span>
                <span className="flash-sale-timer">{saleCountdownParts.map((part, index) => <span className="countdown-cell" key={index}><strong>{String(part).padStart(2, "0")}</strong><small>{[text.hours, text.minutes, text.seconds][index]}</small></span>)}</span>
              </div>
            )}
            {selectedVariant && <div className="hero-price-line">
              {flashSaleActive && selectedVariant.salePrice != null && <del>{formatPrice(selectedVariant.price + selectedOptions.reduce((sum, option) => sum + option.priceAdjustment, 0), selectedVariant.currencyCode)}</del>}
              <strong>{formatPrice(currentTotal, selectedVariant.currencyCode)}</strong>
              {flashSaleActive && selectedVariant.salePrice != null && <span className="sale-badge">{product.flashSale.discountType === "percentage" ? `−${product.flashSale.discountValue}%` : text.save}</span>}
              {flashSaleActive && selectedVariant.salePrice != null && <small className="you-save">{text.save}: {formatPrice(selectedVariant.price - selectedVariant.salePrice, selectedVariant.currencyCode)}</small>}
            </div>}
            <div className="purchase-choice-heading"><h2>{text.variants}</h2><p>{text.choose}</p></div>
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
            {product.attributeGroups.length > 0 && <h3 className="store-attribute-heading">{text.attributes}</h3>}
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
              <svg className="add-to-cart-icon" viewBox="0 0 32 32" aria-hidden="true"><path d="M3 4h3l3.2 16.1h15.3l3.1-11.6H8M12 26a1.5 1.5 0 1 0 0 .1M23 26a1.5 1.5 0 1 0 0 .1" /></svg>{text.add}
            </button>
            <div className="purchase-assurances" aria-label={text.storeDetails}>
              <div><span aria-hidden="true"><AssuranceIcon kind="shield" /></span><strong>{text.authentic}</strong></div>
              <div><span aria-hidden="true"><AssuranceIcon kind="delivery" /></span><strong>{text.fastDelivery}</strong></div>
              <div><span aria-hidden="true"><AssuranceIcon kind="return" /></span><strong>{text.easyReturn}</strong></div>
              <div><span aria-hidden="true"><AssuranceIcon kind="support" /></span><strong>{text.customerSupport}</strong></div>
            </div>
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
        </section>
      ) : (
        <section className="hero">
          <h1>{text.story}</h1>
          <p className="intro">{text.empty}</p>
          <a className="button store-admin-link" href="/admin">{text.openAdmin}</a>
        </section>
      )}

      {product && <nav className="product-section-tabs" aria-label={text.productSections}>
        <a className="active" href="#product-details"><span aria-hidden="true">▤</span>{text.productDetails}</a>
        {product.usageSteps.length > 0 && <a href="#how-to-use"><span aria-hidden="true">▷</span>{text.howToUse}</a>}
        <a href="#reviews"><span aria-hidden="true">☆</span>{text.reviews} ({product.reviewCount})</a>
        <a href="#shipping-returns"><span aria-hidden="true">⇢</span>{text.shippingReturns}</a>
      </nav>}

      {product && <section className="landing-info-grid" aria-label={text.productDetails}>
        <article id="product-details" className="landing-info-card product-details-card">
          <h2>{text.productDetails}</h2>
          <dl>
            <div><dt>{text.brand}</dt><dd>{product.brandName}</dd></div>
            <div><dt>{text.productDetails}</dt><dd>{product.name}</dd></div>
            {selectedVariant && <>
              <div><dt>{text.selectedOption}</dt><dd>{selectedVariant.displayName}</dd></div>
              <div><dt>SKU</dt><dd>{selectedVariant.sku}</dd></div>
              <div><dt>{text.stock}</dt><dd>{selectedVariant.isAvailable ? text.stock : text.out}</dd></div>
            </>}
            {product.attributeGroups.map(group => {
              const selectedIds = new Set(selectedAttributeOptions[group.id] ?? []);
              const selectedNames = group.options.filter(option => selectedIds.has(option.id)).map(option => option.name);
              return selectedNames.length > 0 ? <div key={group.id}><dt>{group.name}</dt><dd>{selectedNames.join(", ")}</dd></div> : null;
            })}
            <div><dt>{text.productOptions}</dt><dd>{product.variants.map(variant => variant.displayName).join(", ")}</dd></div>
          </dl>
        </article>
        {product.benefits.length > 0 && <article className="landing-info-card product-benefits-card">
          <h2>{text.keyBenefits}</h2>
          <div className="benefit-grid">
            {product.benefits.map((benefit, index) => <article className="benefit-card" key={benefit.id}>
              <span className="benefit-icon" aria-hidden="true">{["✦", "◌", "⌁", "◇", "❋", "✓"][index % 6]}</span>
              <h3>{benefit.title}</h3>
              {benefit.description && <p>{benefit.description}</p>}
            </article>)}
          </div>
        </article>}
      </section>}

      {product && product.usageSteps.length > 0 && <section id="how-to-use" className="landing-info-card usage-card" aria-labelledby="how-to-use-title">
        <h2 id="how-to-use-title">{text.howToUse}</h2>
        <div className="usage-text">{product.usageSteps.map(step => step.description).filter(Boolean).join("\n\n")}</div>
      </section>}

      {product && <section id="reviews" className="landing-section reviews-section" aria-labelledby="reviews-title">
        <div className="section-title"><span></span><div><p>{product.reviewCount} {product.reviewCount === 1 ? text.review : text.reviewsPlural}</p><h2 id="reviews-title">{text.reviews}</h2></div><span></span></div>
        {product.reviewCount > 0 ? <><div className="review-summary"><strong>{product.averageRating.toFixed(1)}</strong><span className="rating-stars" aria-label={`${product.averageRating} ${text.rating}`}>{renderStars(product.averageRating)}</span><span>{product.reviewCount} {product.reviewCount === 1 ? text.review : text.reviewsPlural}</span></div>
        <div className="review-grid">{product.reviews.map((review, index) => <article className="review-card" key={`${review.displayName}-${index}`}>
          <div className="review-card-top"><span className="rating-stars" aria-label={`${review.rating} ${text.rating}`}>{renderStars(review.rating)}</span><strong>{review.displayName}</strong></div>
          <h3>{review.title}</h3><p>{review.content}</p>
        </article>)}</div></> : <p className="no-reviews-message">{text.noReviews}</p>}
      </section>}

      {product && <section id="shipping-returns" className="shipping-returns-section" aria-labelledby="shipping-returns-title">
        <h2 id="shipping-returns-title">{text.shippingReturns}</h2>
        {product.trustItems.length > 0
          ? <div className="shipping-return-grid">{product.trustItems.map((item, index) => <article key={`${item.title}-${index}`}><span aria-hidden="true">{["✓", "⇢", "↻", "♡"][index % 4]}</span><div><strong>{item.title}</strong>{item.description && <p>{item.description}</p>}</div></article>)}</div>
          : <div className="shipping-return-grid"><article><span aria-hidden="true">✓</span><div><strong>{text.codAvailable}</strong></div></article><article><span aria-hidden="true">↻</span><div><strong>{text.easyReturn}</strong></div></article><article><span aria-hidden="true">♡</span><div><strong>{text.customerSupport}</strong></div></article></div>}
      </section>}

      {product && product.images.length > 0 && <section className="product-promo-banner" aria-label={product.benefits[0]?.title ?? product.name}>
        <img src={getMediaUrl(product.images[product.images.length - 1].url)} alt="" />
        <div><p>{product.brandName}</p><h2>{product.benefits[0]?.title ?? product.name}</h2><span>{product.benefits[0]?.description ?? product.storefrontSubtitle}</span></div>
      </section>}

      {product?.aboutUs.trim() && (
        <section id="about-us" className="store-about" aria-labelledby="about-us-title">
          <p className="eyebrow">{text.aboutUs}</p>
          <h2 id="about-us-title">{text.aboutUs}</h2>
          <p>{product.aboutUs}</p>
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

      <footer className="store-footer">
        <p className="store-copyright">© {new Date().getFullYear()} Pretty Shop</p>
        <nav className="store-footer-links" aria-label={text.storeNavigation}>
          <a className="footer-whatsapp" href="https://wa.me/01872441501?text=Hello%2C%20I%20have%20a%20question%20about%20this%20product." target="_blank" rel="noopener noreferrer">
            <svg className="whatsapp-icon" viewBox="0 0 32 32" aria-hidden="true" focusable="false">
              <path fill="currentColor" d="M16.04 3A12.9 12.9 0 0 0 4.93 22.47L3.2 28.8l6.49-1.7A12.9 12.9 0 1 0 16.04 3Zm0 23.47c-1.92 0-3.8-.51-5.44-1.47l-.39-.23-3.85 1.01 1.03-3.75-.25-.4a10.57 10.57 0 1 1 8.9 4.84Zm5.8-7.92c-.32-.16-1.9-.94-2.2-1.05-.3-.11-.52-.16-.74.16-.22.32-.84 1.05-1.03 1.27-.19.21-.38.24-.7.08-.32-.16-1.36-.5-2.59-1.59-.96-.86-1.61-1.92-1.8-2.24-.19-.32-.02-.49.14-.65.14-.14.32-.38.48-.56.16-.19.21-.32.32-.54.11-.21.05-.4-.03-.56-.08-.16-.73-1.76-1-2.41-.27-.63-.54-.55-.73-.56h-.62c-.21 0-.56.08-.86.4-.3.32-1.13 1.1-1.13 2.68s1.16 3.11 1.32 3.32c.16.22 2.28 3.48 5.51 4.88.77.33 1.37.53 1.84.68.77.24 1.47.21 2.03.13.62-.09 1.9-.78 2.17-1.54.27-.75.27-1.4.19-1.54-.08-.13-.29-.21-.62-.37Z" />
            </svg>
            <span className="footer-contact-copy"><small>WhatsApp</small><strong>01872441501</strong></span>
          </a>
          <a className="footer-admin" href="/admin"><span aria-hidden="true">↗</span> অ্যাডমিন</a>
        </nav>
      </footer>
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

function getWhatsAppNumber(value: string): string {
  try {
    const url = new URL(value);
    const queryNumber = url.searchParams.get("phone");
    const pathNumber = url.hostname.endsWith("wa.me") ? url.pathname.split("/").filter(Boolean)[0] : "";
    return (queryNumber || pathNumber).replace(/[^\d+]/g, "");
  } catch {
    return "";
  }
}

function renderStars(rating: number): string {
  const filled = Math.max(0, Math.min(5, Math.round(rating)));
  return "★".repeat(filled) + "☆".repeat(5 - filled);
}

function AssuranceIcon({ kind }: { kind: "shield" | "delivery" | "return" | "support" }) {
  const common = { fill: "none", stroke: "currentColor", strokeLinecap: "round" as const, strokeLinejoin: "round" as const, strokeWidth: 1.8 };
  return <svg viewBox="0 0 32 32" aria-hidden="true" {...common}>
    {kind === "shield" && <><path d="M16 3 27 7v8c0 7-4.7 11.4-11 14-6.3-2.6-11-7-11-14V7l11-4Z" /><path d="m10.5 16 3.5 3.5 7-7" /></>}
    {kind === "delivery" && <><path d="M3 8h16v14H3zM19 13h5l5 5v4H19z" /><circle cx="9" cy="24" r="2.5" /><circle cx="24" cy="24" r="2.5" /><path d="M22 13v5h7" /></>}
    {kind === "return" && <><path d="M7 9h18l2 18H5L7 9Z" /><path d="M11 9a5 5 0 0 1 10 0M11 17l-3 3 3 3M8 20h9a4 4 0 0 0 4-4" /></>}
    {kind === "support" && <><path d="M4 17v-2a12 12 0 0 1 24 0v2" /><path d="M4 17h5v9H7a3 3 0 0 1-3-3v-6ZM28 17h-5v9h2a3 3 0 0 0 3-3v-6ZM23 27a8 8 0 0 1-7 3h-2" /></>}
  </svg>;
}
