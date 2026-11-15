import { FormEvent, useEffect, useState } from "react";
import { adminRequest, loadAdminImagePreview } from "./adminApi";

interface TrustItem { id: string | null; title: string; description: string; isActive: boolean; displayOrder: number }
interface Settings {
  brandName: string;
  storefrontHeadline: string;
  storefrontSubtitle: string;
  flashSale: { enabled: boolean; label: string; discountType: "percentage" | "fixedAmount"; discountValue: number; startsAtUtc: string | null; endsAtUtc: string | null };
  socialSharingEnabled: boolean;
  whatsAppEnabled: boolean;
  whatsAppPhoneNumber: string;
  whatsAppPrefillMessage: string;
  brandLogo: { id: string; altText: string } | null;
  trustItems: TrustItem[];
  storefrontTranslations: Record<string, Record<string, string>>;
  theme: { primaryColor: string; accentColor: string; textColor: string; mutedTextColor: string; backgroundColor: string; surfaceColor: string; borderColor: string; productTitleFontSizePx: number; detailsFontSizePx: number };
}

const emptySettings: Settings = {
  brandName: "Pretty Shop", storefrontHeadline: "", storefrontSubtitle: "",
  flashSale: { enabled: false, label: "", discountType: "percentage", discountValue: 10, startsAtUtc: null, endsAtUtc: null },
  socialSharingEnabled: true, whatsAppEnabled: false, whatsAppPhoneNumber: "", whatsAppPrefillMessage: "Hello, I have a question about this product.", brandLogo: null, trustItems: [], storefrontTranslations: {},
  theme: { primaryColor: "#242020", accentColor: "#c17b88", textColor: "#282326", mutedTextColor: "#777078", backgroundColor: "#fbf8f8", surfaceColor: "#ffffff", borderColor: "#eee5e8", productTitleFontSizePx: 15, detailsFontSizePx: 13 }
};

const translationLanguages = [{ code: "bn", label: "Bangla" }, { code: "es", label: "Spanish" }] as const;

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
  const [logoFile, setLogoFile] = useState<File | null>(null);
  const [logoAltText, setLogoAltText] = useState("");

  useEffect(() => {
    let active = true;
    void adminRequest<Settings>("/api/v1/admin/storefront/settings")
      .then(async value => {
        if (!active) return;
        setSettings(value);
        if (value.brandLogo) setLogoPreview(await loadAdminImagePreview(value.brandLogo.id));
      })
      .catch(reason => { if (active) setError(reason instanceof Error ? reason.message : "Could not load storefront settings."); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
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
        body: JSON.stringify({ ...settings, flashSale: { ...settings.flashSale,
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
      <label className="admin-field-wide">Subtitle<input maxLength={500} value={settings.storefrontSubtitle} onChange={event => setSettings(current => ({ ...current, storefrontSubtitle: event.target.value }))} /></label>
      <fieldset className="admin-field-wide"><legend>Storefront theme</legend>
        <p className="admin-muted">Use these controls to change storefront colors and text sizes. Custom CSS is not supported.</p>
        <div className="admin-form-grid">
          {([ ["primaryColor", "Primary color"], ["accentColor", "Accent color"], ["textColor", "Text color"], ["mutedTextColor", "Muted text"], ["backgroundColor", "Page background"], ["surfaceColor", "Card background"], ["borderColor", "Borders"] ] as const).map(([key, label]) => <label key={key}>{label}<input type="color" value={settings.theme[key]} onChange={event => updateTheme(key, event.target.value)} /></label>)}
          <label>Product title size<select value={settings.theme.productTitleFontSizePx} onChange={event => updateTheme("productTitleFontSizePx", Number(event.target.value))}>{[14, 15, 16].map(size => <option key={size} value={size}>{size}px</option>)}</select></label>
          <label>Details text size<select value={settings.theme.detailsFontSizePx} onChange={event => updateTheme("detailsFontSizePx", Number(event.target.value))}>{[12, 13, 14].map(size => <option key={size} value={size}>{size}px</option>)}</select></label>
        </div>
        <div className="theme-preview" style={{ color: settings.theme.textColor, background: settings.theme.backgroundColor, borderColor: settings.theme.borderColor }}>
          <strong style={{ color: settings.theme.primaryColor, fontSize: `${settings.theme.productTitleFontSizePx}px` }}>{settings.brandName || "Store name"}</strong>
          <p style={{ color: settings.theme.mutedTextColor, fontSize: `${settings.theme.detailsFontSizePx}px` }}>{settings.storefrontHeadline || "Storefront headline preview"}</p>
          <button type="button" style={{ background: settings.theme.primaryColor }}>Preview button</button>
          <span style={{ background: settings.theme.surfaceColor, borderColor: settings.theme.borderColor, color: settings.theme.accentColor }}>Preview card</span>
        </div>
      </fieldset>
      <fieldset className="admin-field-wide"><legend>Storefront translations</legend><p className="admin-muted">English is the default and fallback. Blank translations display the English text.</p>
        {translationLanguages.map(language => <div className="admin-form-grid" key={language.code}>
          <label>{language.label} brand name<input maxLength={120} value={settings.storefrontTranslations[language.code]?.["store.brandName"] ?? ""} onChange={event => updateTranslation(language.code, "store.brandName", event.target.value)} /></label>
          <label>{language.label} headline<input maxLength={200} value={settings.storefrontTranslations[language.code]?.["store.headline"] ?? ""} onChange={event => updateTranslation(language.code, "store.headline", event.target.value)} /></label>
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
      <button className="button" type="submit" disabled={busy}>{busy ? "Saving…" : "Save storefront settings"}</button>
      {message && <p className="admin-message" role="status">{message}</p>}{error && <p className="admin-error" role="alert">{error}</p>}
    </form>
  </section>;
}
