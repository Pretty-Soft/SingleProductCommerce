import { CSSProperties, FormEvent, useEffect, useState } from "react";
import { adminRequest, loadAdminImagePreview } from "./adminApi";
import { withRosyDefaultPalette } from "./storeApi";

interface TrustItem { id: string | null; title: string; description: string; isActive: boolean; displayOrder: number }
interface Settings {
  brandName: string;
  storefrontHeadline: string;
  aboutUs: string;
  benefits: Array<{ id: string; title: string; description: string }>;
  usageSteps: Array<{ id: string; title: string; description: string }>;
  reviews: Array<{ id: string | null; displayName: string; rating: number; title: string; content: string; isPublished: boolean; displayOrder: number }>;
  storefrontSubtitle: string;
  flashSale: { enabled: boolean; label: string; discountType: "percentage" | "fixedAmount"; discountValue: number; startsAtUtc: string | null; endsAtUtc: string | null };
  socialSharingEnabled: boolean;
  whatsAppEnabled: boolean;
  whatsAppPhoneNumber: string;
  whatsAppPrefillMessage: string;
  brandLogo: { id: string; altText: string } | null;
  productImages: Array<{ id: string; url: string; mediaKind: "product"; altText: string; displayOrder: number }>;
  trustItems: TrustItem[];
  storefrontTranslations: Record<string, Record<string, string>>;
  theme: { primaryColor: string; accentColor: string; textColor: string; mutedTextColor: string; backgroundColor: string; surfaceColor: string; borderColor: string; productTitleFontSizePx: number; detailsFontSizePx: number };
}

const emptySettings: Settings = {
  brandName: "Pretty Shop", storefrontHeadline: "", aboutUs: "", benefits: [], usageSteps: [], reviews: [], storefrontSubtitle: "",
  flashSale: { enabled: false, label: "", discountType: "percentage", discountValue: 10, startsAtUtc: null, endsAtUtc: null },
  socialSharingEnabled: true, whatsAppEnabled: false, whatsAppPhoneNumber: "", whatsAppPrefillMessage: "Hello, I have a question about this product.", brandLogo: null, productImages: [], trustItems: [], storefrontTranslations: {},
  theme: { primaryColor: "#171316", accentColor: "#e8437a", textColor: "#211b1e", mutedTextColor: "#756970", backgroundColor: "#ffffff", surfaceColor: "#ffffff", borderColor: "#f1dce4", productTitleFontSizePx: 30, detailsFontSizePx: 13 }
};

const translationLanguages = [{ code: "bn", label: "Bangla" }, { code: "es", label: "Spanish" }] as const;
const emptyUsageId = "00000000-0000-4000-8000-000000000001";

function toLocal(value: string | null) {
  if (!value) return "";
  const date = new Date(value);
  return new Date(date.getTime() - date.getTimezoneOffset() * 60000).toISOString().slice(0, 16);
}

export default function AdminStoreSettings() {
  const [settings, setSettings] = useState<Settings>(emptySettings);
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const [logoPreview, setLogoPreview] = useState("");
  const [productImagePreviews, setProductImagePreviews] = useState<Record<string, string>>({});
  const [logoFile, setLogoFile] = useState<File | null>(null);
  const [logoAltText, setLogoAltText] = useState("");

  useEffect(() => {
    let active = true;
    const imageUrls: string[] = [];
    void adminRequest<Settings>("/api/v1/admin/storefront/settings")
      .then(async value => {
        if (!active) return;
        setSettings({ ...value, theme: withRosyDefaultPalette(value.theme) });
        if (value.brandLogo) {
          const preview = await loadAdminImagePreview(value.brandLogo.id).catch(() => "");
          if (preview) imageUrls.push(preview);
          if (active) setLogoPreview(preview);
        }
        const previews = await Promise.all((value.productImages ?? []).map(async image => {
          const preview = await loadAdminImagePreview(image.id).catch(() => "");
          if (preview) imageUrls.push(preview);
          return [image.id, preview] as const;
        }));
        if (active) setProductImagePreviews(Object.fromEntries(previews.filter(([, preview]) => preview)));
        else imageUrls.forEach(URL.revokeObjectURL);
      })
      .catch(reason => { if (active) setError(reason instanceof Error ? reason.message : "Could not load storefront settings."); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; imageUrls.forEach(URL.revokeObjectURL); };
  }, []);

  function updateSale(changes: Partial<Settings["flashSale"]>) {
    setSettings(current => ({ ...current, flashSale: { ...current.flashSale, ...changes } }));
  }

  function updateTranslation(language: string, key: string, value: string) {
    setSettings(current => ({ ...current, storefrontTranslations: {
      ...current.storefrontTranslations,
      [language]: { ...current.storefrontTranslations[language], [key]: value }
    } }));
  }

  function updateTheme<K extends keyof Settings["theme"]>(key: K, value: Settings["theme"][K]) {
    setSettings(current => ({ ...current, theme: { ...current.theme, [key]: value } }));
  }

  function updateContent(section: "benefits" | "usageSteps", id: string, changes: Partial<Settings["benefits"][number]>) {
    setSettings(current => ({ ...current, [section]: current[section].map(item => item.id === id ? { ...item, ...changes } : item) }));
  }

  function addContent(section: "benefits" | "usageSteps") {
    setSettings(current => ({ ...current, [section]: [...current[section], { id: crypto.randomUUID(), title: "", description: "" }] }));
  }

  function updateReview(id: string, changes: Partial<NonNullable<Settings["reviews"][number]>>) {
    setSettings(current => ({ ...current, reviews: current.reviews.map(item => item.id === id ? { ...item, ...changes } : item) }));
  }

  async function uploadLogo() {
    if (!logoFile) return;
    setBusy(true); setError(""); setMessage("");
    try {
      const form = new FormData();
      form.set("file", logoFile);
      form.set("mediaKind", "logo");
      form.set("altText", logoAltText);
      const uploaded = await adminRequest<{ id: string; altText: string }>("/api/v1/admin/catalog/product/images", { method: "POST", body: form });
      setSettings(current => ({ ...current, brandLogo: uploaded }));
      setLogoPreview(await loadAdminImagePreview(uploaded.id));
      setLogoFile(null); setLogoAltText(""); setMessage("Brand logo saved.");
    } catch (reason) { setError(reason instanceof Error ? reason.message : "Brand logo could not be uploaded."); }
    finally { setBusy(false); }
  }

  async function removeLogo() {
    if (!settings.brandLogo) return;
    setBusy(true); setError(""); setMessage("");
    try {
      await adminRequest<void>(`/api/v1/admin/catalog/product/images/${settings.brandLogo.id}`, { method: "DELETE" });
      if (logoPreview) URL.revokeObjectURL(logoPreview);
      setLogoPreview(""); setSettings(current => ({ ...current, brandLogo: null })); setMessage("Brand logo removed.");
    } catch (reason) { setError(reason instanceof Error ? reason.message : "Brand logo could not be removed."); }
    finally { setBusy(false); }
  }

  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setBusy(true); setError(""); setMessage("");
    try {
      await adminRequest("/api/v1/admin/storefront/settings", {
        method: "PUT",
        body: JSON.stringify({ ...settings, usageSteps: settings.usageSteps.filter(item => item.description.trim()), flashSale: { ...settings.flashSale,
          startsAtUtc: settings.flashSale.startsAtUtc ? new Date(settings.flashSale.startsAtUtc).toISOString() : null,
          endsAtUtc: settings.flashSale.endsAtUtc ? new Date(settings.flashSale.endsAtUtc).toISOString() : null } })
      });
      setMessage("Storefront settings saved.");
    } catch (reason) { setError(reason instanceof Error ? reason.message : "Settings could not be saved."); }
    finally { setBusy(false); }
  }

  if (loading) return <section className="admin-card"><p className="admin-muted">Loading storefront settings…</p></section>;
  return <section className="admin-card">
    <div className="section-heading"><div><h2>Storefront settings</h2><p className="admin-muted">Configure customer-facing brand, promotions, contact and trust details.</p></div></div>
    <form className="admin-form-grid" onSubmit={save}>
      <label>Brand name<input maxLength={120} required value={settings.brandName} onChange={event => setSettings(current => ({ ...current, brandName: event.target.value }))} /></label>
      <label>Headline<input maxLength={200} value={settings.storefrontHeadline} onChange={event => setSettings(current => ({ ...current, storefrontHeadline: event.target.value }))} /></label>
      <label className="admin-field-wide">About Us<textarea maxLength={5000} rows={5} value={settings.aboutUs} onChange={event => setSettings(current => ({ ...current, aboutUs: event.target.value }))} placeholder="Tell customers about your shop." /></label>
      <fieldset className="admin-field-wide"><legend>Product benefits</legend><p className="admin-muted">Add short, accurate benefits for the “Why choose this product?” section. These appear on the storefront.</p>
        {settings.benefits.map((item, index) => <div className="admin-form-grid" key={item.id}>
          <label>Benefit {index + 1}<input maxLength={120} required value={item.title} onChange={event => updateContent("benefits", item.id, { title: event.target.value })} /></label>
          <label>Description<input maxLength={1000} value={item.description} onChange={event => updateContent("benefits", item.id, { description: event.target.value })} /></label>
          {translationLanguages.flatMap(language => [
            <label key={`${language.code}-title`}>{language.label} benefit<input maxLength={120} value={settings.storefrontTranslations[language.code]?.[`benefit.${item.id}.title`] ?? ""} onChange={event => updateTranslation(language.code, `benefit.${item.id}.title`, event.target.value)} /></label>,
            <label key={`${language.code}-description`}>{language.label} description<input maxLength={1000} value={settings.storefrontTranslations[language.code]?.[`benefit.${item.id}.description`] ?? ""} onChange={event => updateTranslation(language.code, `benefit.${item.id}.description`, event.target.value)} /></label>
          ])}
          <button className="button button-quiet" type="button" onClick={() => setSettings(current => ({ ...current, benefits: current.benefits.filter(value => value.id !== item.id) }))}>Remove benefit</button>
        </div>)}
        <button className="button button-quiet" type="button" disabled={settings.benefits.length >= 6} onClick={() => addContent("benefits")}>Add benefit</button>
      </fieldset>
      <fieldset className="admin-field-wide"><legend>How to use</legend><p className="admin-muted">Enter product-specific instructions as plain text. Use line breaks to separate steps or notes; this content appears as a text section on the storefront.</p>
        <label className="admin-field-wide">Instructions<textarea maxLength={1000} rows={7} value={settings.usageSteps.map(item => item.description).filter(Boolean).join("\n\n")} onChange={event => {
          const current = settings.usageSteps;
          const first = current[0] ?? { id: emptyUsageId, title: "Instructions", description: "" };
          setSettings(value => ({ ...value, usageSteps: [{ ...first, title: "Instructions", description: event.target.value }] }));
        }} placeholder="Enter clear instructions for using this product…" /></label>
        {translationLanguages.map(language => {
          const itemId = settings.usageSteps[0]?.id ?? emptyUsageId;
          const key = `usage.${itemId}.description`;
          return <label className="admin-field-wide" key={language.code}>{language.label} instructions<textarea maxLength={1000} rows={5} value={settings.storefrontTranslations[language.code]?.[key] ?? ""} onChange={event => updateTranslation(language.code, key, event.target.value)} /></label>;
        })}
      </fieldset>
      <label className="admin-field-wide">Subtitle<input maxLength={500} value={settings.storefrontSubtitle} onChange={event => setSettings(current => ({ ...current, storefrontSubtitle: event.target.value }))} /></label>
      <fieldset className="admin-field-wide"><legend>Storefront theme</legend>
        <p className="admin-muted">Use these controls to change storefront colors and text sizes. Custom CSS is not supported.</p>
        <button className="button button-quiet" type="button" onClick={() => setSettings(current => ({ ...current, theme: { ...current.theme, primaryColor: "#171316", accentColor: "#e8437a", textColor: "#211b1e", mutedTextColor: "#756970", backgroundColor: "#ffffff", surfaceColor: "#ffffff", borderColor: "#f1dce4" } }))}>Apply white, black & rosy palette</button>
        <div className="admin-form-grid">
          {([ ["primaryColor", "Primary color"], ["accentColor", "Accent color"], ["textColor", "Text color"], ["mutedTextColor", "Muted text"], ["backgroundColor", "Page background"], ["surfaceColor", "Card background"], ["borderColor", "Borders"] ] as const).map(([key, label]) => <label key={key}>{label}<input type="color" value={settings.theme[key]} onChange={event => updateTheme(key, event.target.value)} /></label>)}
          <label>Product heading size<select value={settings.theme.productTitleFontSizePx} onChange={event => updateTheme("productTitleFontSizePx", Number(event.target.value))}>{[26, 28, 30, 32, 36, 40, 42].map(size => <option key={size} value={size}>{size}px</option>)}</select></label>
          <label>Details text size<select value={settings.theme.detailsFontSizePx} onChange={event => updateTheme("detailsFontSizePx", Number(event.target.value))}>{[12, 13, 14].map(size => <option key={size} value={size}>{size}px</option>)}</select></label>
        </div>
        <div className="theme-preview" style={{
          color: settings.theme.textColor,
          background: settings.theme.backgroundColor,
          borderColor: settings.theme.borderColor,
          "--theme-primary": settings.theme.primaryColor,
          "--theme-accent": settings.theme.accentColor,
          "--theme-text": settings.theme.textColor,
          "--theme-muted": settings.theme.mutedTextColor,
          "--theme-surface": settings.theme.surfaceColor,
          "--theme-border": settings.theme.borderColor
        } as CSSProperties}>
          <div className="theme-preview-trustbar">
            <span>{settings.trustItems.find(item => item.isActive)?.title || "Your trusted store"}</span>
            {settings.trustItems.find(item => item.isActive)?.description && <span>{settings.trustItems.find(item => item.isActive)?.description}</span>}
            {settings.whatsAppEnabled && <span>WhatsApp · {settings.whatsAppPhoneNumber || "Add a contact number"}</span>}
          </div>
          <div className="theme-preview-header">
            {logoPreview
              ? <img className="theme-preview-logo" src={logoPreview} alt="" />
              : <span className="theme-preview-logo-fallback">{(settings.brandName || "S").slice(0, 1).toUpperCase()}</span>}
            <div className="theme-preview-brand">
              <strong style={{ color: settings.theme.accentColor, fontSize: `${Math.min(settings.theme.productTitleFontSizePx, 24)}px` }}>{settings.brandName || "Store name"}</strong>
              <span style={{ color: settings.theme.textColor, fontSize: `${Math.min(settings.theme.detailsFontSizePx, 14)}px` }}>{settings.storefrontHeadline || "Storefront headline preview"}</span>
            </div>
            <div className="theme-preview-header-actions"><span>About Us</span><button className="theme-preview-cart" type="button" aria-label="Cart preview"><b>0</b></button></div>
          </div>
          <div className="theme-preview-hero">
            <div className="theme-preview-gallery">
              <div className="theme-preview-image-zone" style={{ background: settings.theme.surfaceColor, borderColor: settings.theme.borderColor }}>
                {settings.productImages[0] && productImagePreviews[settings.productImages[0].id]
                  ? <img src={productImagePreviews[settings.productImages[0].id]} alt="" />
                  : <span>Uploaded product image preview</span>}
              </div>
              {settings.productImages.length > 1 && <div className="theme-preview-thumbnails">{settings.productImages.slice(0, 4).map(image => productImagePreviews[image.id] ? <img key={image.id} src={productImagePreviews[image.id]} alt="" /> : <span key={image.id} />)}</div>}
            </div>
            <div className="theme-preview-product-copy">
              {settings.storefrontSubtitle && <span className="theme-preview-kicker">{settings.storefrontSubtitle}</span>}
              <strong className="theme-preview-product-title" style={{ color: settings.theme.primaryColor, fontSize: `${settings.theme.productTitleFontSizePx}px` }}>Product name preview</strong>
              <p className="theme-preview-description" style={{ color: settings.theme.mutedTextColor, fontSize: `${settings.theme.detailsFontSizePx}px` }}>The product name and description come from Product Catalog.</p>
              <div className="theme-preview-rating"><span>★★★★★</span><small>{settings.reviews.filter(review => review.isPublished).length} published reviews</small></div>
              <ul className="theme-preview-hero-benefits">{(settings.benefits.length ? settings.benefits.slice(0, 4) : [{ id: "benefit-placeholder", title: "Configured product benefits preview here." }]).map(item => <li key={item.id}><b>✓</b>{item.title}</li>)}</ul>
              {settings.flashSale.enabled && <div className="theme-preview-sale"><strong>ϟ {settings.flashSale.label || "Flash sale"}</strong><span>Sale countdown preview · 00:00:00</span></div>}
              <div className="theme-preview-price"><strong>Price and discount preview</strong><small>Actual prices are shown from the saved catalog variants.</small></div>
              <div className="theme-preview-variants"><span>Variant preview</span><span>Option pricing comes from Product Catalog</span></div>
              <button className="theme-preview-button" type="button" style={{ background: `linear-gradient(100deg, ${settings.theme.accentColor}, ${settings.theme.primaryColor})` }}>Add to cart</button>
              <div className="theme-preview-assurances"><span>100% Authentic</span><span>Fast Delivery</span><span>Easy Return</span><span>Customer Support</span></div>
            </div>
          </div>
          <nav className="theme-preview-tabs" aria-label="Preview product section navigation"><span>Product Details</span><span>How to Use</span><span>Reviews</span><span>Shipping &amp; Returns</span></nav>
          <div className="theme-preview-sections">
            <div className="theme-preview-section-grid">
              <section className="theme-preview-section"><strong>Product details</strong><small>Product name, SKU, attributes, and variant details appear from Product Catalog.</small></section>
              <section className="theme-preview-section"><strong>Key benefits</strong><div className="theme-preview-benefits">{(settings.benefits.length ? settings.benefits.slice(0, 4) : [{ id: "benefit-preview", title: "Product benefits preview here.", description: "Add benefits above to preview your content." }]).map(item => <div key={item.id}><b>{item.title}</b><small>{item.description || "Product benefit"}</small></div>)}</div></section>
            </div>
            <div className="theme-preview-section-grid">
              <section className="theme-preview-section"><strong>How to use</strong><small className="theme-preview-usage">{settings.usageSteps.map(item => item.description).filter(Boolean).join("\n\n") || "Add multiline usage instructions above to preview them here."}</small></section>
              <section className="theme-preview-section"><strong>Shipping &amp; returns</strong><small>{settings.trustItems.filter(item => item.isActive).map(item => item.title).join(" · ") || "Configured shipping and service details appear here."}</small></section>
            </div>
            <section className="theme-preview-section"><strong>Customer reviews</strong><small>{settings.reviews.filter(review => review.isPublished).length ? `${settings.reviews.filter(review => review.isPublished).length} published admin-managed review(s)` : "Admin-managed reviews will appear here when published."}</small></section>
          </div>
          {settings.productImages.length > 0 && <div className="theme-preview-promo"><span>{settings.productImages.length > 1 && productImagePreviews[settings.productImages[settings.productImages.length - 1].id] && <img src={productImagePreviews[settings.productImages[settings.productImages.length - 1].id]} alt="" />}</span><div><small>{settings.brandName}</small><strong>{settings.benefits[0]?.title || "Product promotional message"}</strong><p>{settings.benefits[0]?.description || "The promotional strip uses your uploaded product imagery and configured benefits."}</p></div></div>}
          <footer className="theme-preview-footer"><span>© {new Date().getFullYear()} {settings.brandName || "Store name"}</span><span>{settings.whatsAppEnabled ? settings.whatsAppPhoneNumber || "WhatsApp contact number" : "WhatsApp contact"} · Admin</span></footer>
        </div>
      </fieldset>
      <fieldset className="admin-field-wide"><legend>Storefront translations</legend><p className="admin-muted">English is the default and fallback. Blank translations display the English text.</p>
        {translationLanguages.map(language => <div className="admin-form-grid" key={language.code}>
          <label>{language.label} brand name<input maxLength={120} value={settings.storefrontTranslations[language.code]?.["store.brandName"] ?? ""} onChange={event => updateTranslation(language.code, "store.brandName", event.target.value)} /></label>
          <label>{language.label} headline<input maxLength={200} value={settings.storefrontTranslations[language.code]?.["store.headline"] ?? ""} onChange={event => updateTranslation(language.code, "store.headline", event.target.value)} /></label>
          <label className="admin-field-wide">{language.label} About Us<textarea maxLength={5000} rows={4} value={settings.storefrontTranslations[language.code]?.["store.aboutUs"] ?? ""} onChange={event => updateTranslation(language.code, "store.aboutUs", event.target.value)} /></label>
          <label>{language.label} subtitle<input maxLength={500} value={settings.storefrontTranslations[language.code]?.["store.subtitle"] ?? ""} onChange={event => updateTranslation(language.code, "store.subtitle", event.target.value)} /></label>
        </div>)}
      </fieldset>
      <fieldset className="admin-field-wide"><legend>Brand logo</legend>
        {logoPreview && <div className="admin-logo-preview"><img src={logoPreview} alt={settings.brandLogo?.altText ?? "Current store logo"} /><button className="button button-quiet" type="button" onClick={() => void removeLogo()} disabled={busy}>Remove logo</button></div>}
        <label>Upload logo<input type="file" accept="image/jpeg,image/png,image/webp" onChange={event => setLogoFile(event.target.files?.[0] ?? null)} /></label>
        {logoFile && <><label>Alternative text<input required maxLength={200} value={logoAltText} onChange={event => setLogoAltText(event.target.value)} /></label><button className="button button-quiet" type="button" disabled={busy || !logoAltText.trim()} onClick={() => void uploadLogo()}>Upload logo</button></>}
      </fieldset>
      <fieldset className="admin-field-wide"><legend>Promotions</legend>
        <label className="role-option"><input type="checkbox" checked={settings.flashSale.enabled} onChange={event => updateSale({ enabled: event.target.checked })} />Enable scheduled flash sale</label>
        {settings.flashSale.enabled && <div className="admin-form-grid">
          <label>Sale label<input maxLength={100} required value={settings.flashSale.label} onChange={event => updateSale({ label: event.target.value })} /></label>
          <label>Discount type<select value={settings.flashSale.discountType} onChange={event => updateSale({ discountType: event.target.value as "percentage" | "fixedAmount" })}><option value="percentage">Percentage</option><option value="fixedAmount">Fixed amount</option></select></label>
          <label>Discount value<input type="number" min="0.0001" max={settings.flashSale.discountType === "percentage" ? 100 : undefined} step="0.0001" required value={settings.flashSale.discountValue} onChange={event => updateSale({ discountValue: Number(event.target.value) })} /></label>
          <label>Starts<input type="datetime-local" required value={toLocal(settings.flashSale.startsAtUtc)} onChange={event => updateSale({ startsAtUtc: event.target.value || null })} /></label>
          <label>Ends<input type="datetime-local" required value={toLocal(settings.flashSale.endsAtUtc)} onChange={event => updateSale({ endsAtUtc: event.target.value || null })} /></label>
          {translationLanguages.map(language => <label key={language.code}>{language.label} sale label<input maxLength={100} value={settings.storefrontTranslations[language.code]?.["promotion.flashSaleLabel"] ?? ""} onChange={event => updateTranslation(language.code, "promotion.flashSaleLabel", event.target.value)} /></label>)}
        </div>}
      </fieldset>
      <fieldset className="admin-field-wide"><legend>Customer actions</legend>
        <label className="role-option"><input type="checkbox" checked={settings.socialSharingEnabled} onChange={event => setSettings(current => ({ ...current, socialSharingEnabled: event.target.checked }))} />Enable product sharing</label>
        <label className="role-option"><input type="checkbox" checked={settings.whatsAppEnabled} onChange={event => setSettings(current => ({ ...current, whatsAppEnabled: event.target.checked }))} />Enable WhatsApp contact</label>
        {settings.whatsAppEnabled && <div className="admin-form-grid">
          <label>WhatsApp number<input maxLength={20} required value={settings.whatsAppPhoneNumber} onChange={event => setSettings(current => ({ ...current, whatsAppPhoneNumber: event.target.value }))} /></label>
          <label>Prefilled message<input maxLength={500} value={settings.whatsAppPrefillMessage} onChange={event => setSettings(current => ({ ...current, whatsAppPrefillMessage: event.target.value }))} /></label>
          {translationLanguages.map(language => <label key={language.code}>{language.label} prefilled message<input maxLength={500} value={settings.storefrontTranslations[language.code]?.["contact.whatsAppPrefillMessage"] ?? ""} onChange={event => updateTranslation(language.code, "contact.whatsAppPrefillMessage", event.target.value)} /></label>)}
        </div>}
      </fieldset>
      <fieldset className="admin-field-wide"><legend>Trust and service details</legend>
        {settings.trustItems.map((item, index) => <div className="admin-form-grid" key={item.id ?? `new-${index}`}>
          <label>Title<input required maxLength={80} value={item.title} onChange={event => setSettings(current => ({ ...current, trustItems: current.trustItems.map((row, rowIndex) => rowIndex === index ? { ...row, title: event.target.value } : row) }))} /></label>
          <label>Description<input maxLength={300} value={item.description} onChange={event => setSettings(current => ({ ...current, trustItems: current.trustItems.map((row, rowIndex) => rowIndex === index ? { ...row, description: event.target.value } : row) }))} /></label>
          {item.id && translationLanguages.map(language => <label key={`${language.code}-title`}>{language.label} title<input maxLength={80} value={settings.storefrontTranslations[language.code]?.[`trust.${item.id}.title`] ?? ""} onChange={event => updateTranslation(language.code, `trust.${item.id}.title`, event.target.value)} /></label>)}
          {item.id && translationLanguages.map(language => <label key={`${language.code}-description`}>{language.label} description<input maxLength={300} value={settings.storefrontTranslations[language.code]?.[`trust.${item.id}.description`] ?? ""} onChange={event => updateTranslation(language.code, `trust.${item.id}.description`, event.target.value)} /></label>)}
          <button className="button button-quiet" type="button" onClick={() => setSettings(current => ({ ...current, trustItems: current.trustItems.filter((_, rowIndex) => rowIndex !== index) }))}>Remove</button>
        </div>)}
        <button className="button button-quiet" type="button" disabled={settings.trustItems.length >= 8} onClick={() => setSettings(current => ({ ...current, trustItems: [...current.trustItems, { id: null, title: "", description: "", isActive: true, displayOrder: current.trustItems.length }] }))}>Add detail</button>
      </fieldset>
      <fieldset className="admin-field-wide"><legend>Product reviews</legend><p className="admin-muted">Only reviews you add here can appear publicly. Review submissions are disabled; uncheck Published to keep a review hidden.</p>
        {settings.reviews.map((review, index) => <div className="admin-form-grid" key={review.id ?? `review-${index}`}>
          <label>Display name<input maxLength={120} required value={review.displayName} onChange={event => updateReview(review.id!, { displayName: event.target.value })} /></label>
          <label>Rating<select value={review.rating} onChange={event => updateReview(review.id!, { rating: Number(event.target.value) })}>{[5, 4, 3, 2, 1].map(value => <option key={value} value={value}>{value} stars</option>)}</select></label>
          <label className="admin-field-wide">Review title<input maxLength={160} required value={review.title} onChange={event => updateReview(review.id!, { title: event.target.value })} /></label>
          <label className="admin-field-wide">Review<textarea maxLength={2000} rows={3} required value={review.content} onChange={event => updateReview(review.id!, { content: event.target.value })} /></label>
          {translationLanguages.flatMap(language => [
            <label key={`${language.code}-name`}>{language.label} display name<input maxLength={120} value={settings.storefrontTranslations[language.code]?.[`review.${review.id}.displayName`] ?? ""} onChange={event => updateTranslation(language.code, `review.${review.id}.displayName`, event.target.value)} /></label>,
            <label key={`${language.code}-title`}>{language.label} title<input maxLength={160} value={settings.storefrontTranslations[language.code]?.[`review.${review.id}.title`] ?? ""} onChange={event => updateTranslation(language.code, `review.${review.id}.title`, event.target.value)} /></label>,
            <label className="admin-field-wide" key={`${language.code}-content`}>{language.label} review<textarea maxLength={2000} rows={2} value={settings.storefrontTranslations[language.code]?.[`review.${review.id}.content`] ?? ""} onChange={event => updateTranslation(language.code, `review.${review.id}.content`, event.target.value)} /></label>
          ])}
          <label className="role-option"><input type="checkbox" checked={review.isPublished} onChange={event => updateReview(review.id!, { isPublished: event.target.checked })} />Published</label>
          <button className="button button-quiet" type="button" onClick={() => setSettings(current => ({ ...current, reviews: current.reviews.filter(value => value.id !== review.id) }))}>Delete review</button>
        </div>)}
        <button className="button button-quiet" type="button" disabled={settings.reviews.length >= 30} onClick={() => setSettings(current => ({ ...current, reviews: [...current.reviews, { id: crypto.randomUUID(), displayName: "", rating: 5, title: "", content: "", isPublished: false, displayOrder: current.reviews.length }] }))}>Add review</button>
      </fieldset>
      <button className="button" type="submit" disabled={busy}>{busy ? "Saving…" : "Save storefront settings"}</button>
      {message && <p className="admin-message" role="status">{message}</p>}{error && <p className="admin-error" role="alert">{error}</p>}
    </form>
  </section>;
}
