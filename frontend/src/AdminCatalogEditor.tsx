import { FormEvent, useEffect, useState } from "react";
import { adminRequest, loadAdminImagePreview } from "./adminApi";

interface EditableVariant {
  id: string | null;
  sku: string;
  displayName: string;
  price: number;
  currencyCode: string;
  stockQuantity: number;
  isActive: boolean;
}

interface EditableProduct {
  id: string | null;
  name: string;
  slug: string;
  description: string;
  currencyCode: string;
  isPublished: boolean;
  variants: EditableVariant[];
  attributeGroups: EditableAttributeGroup[];
  flashSale: EditableFlashSale;
  socialSharingEnabled: boolean;
  whatsAppEnabled: boolean;
  whatsAppPhoneNumber: string;
  whatsAppPrefillMessage: string;
  brandName: string;
  storefrontHeadline: string;
  storefrontSubtitle: string;
  images: EditableProductImage[];
  trustItems: EditableTrustItem[];
  catalogTranslations: Record<string, Record<string, string>>;
  storefrontTranslations?: Record<string, Record<string, string>>;
}

interface EditableProductImage {
  id: string;
  url: string;
  mediaKind: "product" | "logo";
  altText: string;
  displayOrder: number;
}

interface EditableTrustItem {
  id: string | null;
  title: string;
  description: string;
  isActive: boolean;
  displayOrder: number;
}

interface EditableFlashSale {
  enabled: boolean;
  label: string;
  discountType: "percentage" | "fixedAmount";
  discountValue: number;
  startsAtUtc: string | null;
  endsAtUtc: string | null;
}

interface EditableAttributeGroup {
  id: string | null;
  name: string;
  isRequired: boolean;
  allowMultiple: boolean;
  displayOrder: number;
  options: EditableAttributeOption[];
}

interface EditableAttributeOption {
  id: string | null;
  name: string;
  priceAdjustment: number;
  isActive: boolean;
  displayOrder: number;
}

type CatalogSectionId = "basics" | "trust" | "variants" | "pricing" | "inventory" | "attributes" | "promotions" | "contact";

const variantDataSections: CatalogSectionId[] = ["variants", "pricing", "inventory"];

export function sharesVariantEditScope(section: CatalogSectionId, editingSection: CatalogSectionId | null) {
  return editingSection !== null
    && variantDataSections.includes(section)
    && variantDataSections.includes(editingSection);
}

export function canAddSectionItem(currentCount: number, maximum: number, busy = false) {
  return !busy && currentCount < maximum;
}

const emptyProduct = (): EditableProduct => ({
  id: null,
  name: "",
  slug: "",
  description: "",
  currencyCode: "",
  isPublished: false,
  variants: [emptyVariant()],
  attributeGroups: [],
  flashSale: { enabled: false, label: "", discountType: "percentage", discountValue: 0, startsAtUtc: null, endsAtUtc: null },
  socialSharingEnabled: true,
  whatsAppEnabled: false,
  whatsAppPhoneNumber: "",
  whatsAppPrefillMessage: "Hello, I have a question about this product.",
  brandName: "Pretty Shop",
  storefrontHeadline: "Your trust, Your Love",
  storefrontSubtitle: "",
  images: [],
  trustItems: [],
  catalogTranslations: {}
});

const LANGUAGES = [{ code: "bn", label: "Bangla" }, { code: "es", label: "Spanish" }] as const;

function emptyVariant(currencyCode = ""): EditableVariant {
  return {
    id: null,
    sku: "",
    displayName: "",
    price: 0,
    currencyCode,
    stockQuantity: 0,
    isActive: true
  };
}

function emptyAttributeGroup(displayOrder: number): EditableAttributeGroup {
  return {
    id: null,
    name: "",
    isRequired: true,
    allowMultiple: false,
    displayOrder,
    options: [emptyAttributeOption(0)]
  };
}

function emptyAttributeOption(displayOrder: number): EditableAttributeOption {
  return { id: null, name: "", priceAdjustment: 0, isActive: true, displayOrder };
}

export default function AdminCatalogEditor({ canManageStoreSettings = true }: { canManageStoreSettings?: boolean }) {
  const [product, setProduct] = useState<EditableProduct>(emptyProduct);
  const [savedProduct, setSavedProduct] = useState<EditableProduct | null>(null);
  const [editingSection, setEditingSection] = useState<CatalogSectionId | null>(null);
  const [expandedSections, setExpandedSections] = useState<Record<string, boolean>>({
    "product-basics": true
  });
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const [imagePreviews, setImagePreviews] = useState<Record<string, string>>({});
  const [productImageFile, setProductImageFile] = useState<File | null>(null);
  const [productImageAltText, setProductImageAltText] = useState("");
  const [productImageResetKey, setProductImageResetKey] = useState(0);
  const [logoFile, setLogoFile] = useState<File | null>(null);
  const [logoAltText, setLogoAltText] = useState("");
  const [logoResetKey, setLogoResetKey] = useState(0);

  useEffect(() => {
    let active = true;
    void adminRequest<EditableProduct>("/api/v1/admin/catalog/product")
      .then((loaded) => {
        if (active) {
          setProduct(loaded);
          setSavedProduct(loaded);
        }
      })
      .catch((reason: unknown) => {
        if (!active) return;
        if (reason instanceof Error && reason.message.includes("404")) {
          const blankProduct = emptyProduct();
          setProduct(blankProduct);
          setSavedProduct(blankProduct);
        } else {
          setError(reason instanceof Error ? reason.message : "Could not load product settings.");
        }
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => { active = false; };
  }, []);

  const hasUnsavedChanges = savedProduct !== null
    && JSON.stringify({ ...product, images: [] }) !== JSON.stringify({ ...savedProduct, images: [] });

  useEffect(() => {
    if (!hasUnsavedChanges) return;
    const warnBeforeLeaving = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = "";
    };
    window.addEventListener("beforeunload", warnBeforeLeaving);
    return () => window.removeEventListener("beforeunload", warnBeforeLeaving);
  }, [hasUnsavedChanges]);

  useEffect(() => {
    let active = true;
    let previewUrls: string[] = [];
    void Promise.all(product.images
      .filter(image => image.mediaKind === "product" || canManageStoreSettings)
      .map(async (image) => [image.id, await loadAdminImagePreview(image.id)] as const))
      .then((entries) => {
        previewUrls = entries.map(([, url]) => url);
        if (active) setImagePreviews(Object.fromEntries(entries));
        else previewUrls.forEach(URL.revokeObjectURL);
      })
      .catch(() => {
        if (active) setImagePreviews({});
      });
    return () => {
      active = false;
      previewUrls.forEach(URL.revokeObjectURL);
    };
  }, [product.images, canManageStoreSettings]);

  function updateProduct<K extends keyof EditableProduct>(key: K, value: EditableProduct[K]) {
    setProduct((current) => ({ ...current, [key]: value }));
  }

  function updateVariant(index: number, changes: Partial<EditableVariant>) {
    setProduct((current) => ({
      ...current,
      variants: current.variants.map((variant, itemIndex) => itemIndex === index
        ? { ...variant, ...changes }
        : variant)
    }));
  }

  function updateAttributeGroup(index: number, changes: Partial<EditableAttributeGroup>) {
    setProduct((current) => ({
      ...current,
      attributeGroups: current.attributeGroups.map((group, itemIndex) => itemIndex === index
        ? { ...group, ...changes }
        : group)
    }));
  }

  function updateAttributeOption(groupIndex: number, optionIndex: number, changes: Partial<EditableAttributeOption>) {
    setProduct((current) => ({
      ...current,
      attributeGroups: current.attributeGroups.map((group, itemIndex) => itemIndex === groupIndex
        ? { ...group, options: group.options.map((option, choiceIndex) => choiceIndex === optionIndex
          ? { ...option, ...changes }
          : option) }
        : group)
    }));
  }

  function confirmChildRemoval(message: string) {
    return window.confirm(`${message}\n\nThis change is staged until you save this section.`);
  }

  async function handleSave(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setError("");
    setMessage("");
    try {
      const saved = await adminRequest<EditableProduct>("/api/v1/admin/catalog/product", {
        method: "PUT",
        body: JSON.stringify(product)
      });
      setProduct(saved);
      setSavedProduct(saved);
      setEditingSection(null);
      setMessage("Product, storefront content, variants, and attributes saved.");
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Product settings could not be saved.");
    } finally {
      setBusy(false);
    }
  }

  async function handleDeleteProduct() {
    if (!product.id || !window.confirm(`Permanently delete “${product.name || "this product"}” and its variants, images, attributes, reviews, and storefront content? Products referenced by past orders cannot be deleted.`)) return;
    setBusy(true);
    setError("");
    setMessage("");
    try {
      await adminRequest<void>("/api/v1/admin/catalog/product", { method: "DELETE" });
      const blankProduct = emptyProduct();
      setProduct(blankProduct);
      setSavedProduct(blankProduct);
      setEditingSection(null);
      setMessage("Product deleted. You can now create a new product.");
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "The product could not be deleted.");
    } finally {
      setBusy(false);
    }
  }

  function isSectionEditable(section: CatalogSectionId) {
    return !product.id || editingSection === section || sharesVariantEditScope(section, editingSection);
  }

  function beginSectionEdit(section: CatalogSectionId) {
    if (editingSection && hasUnsavedChanges) {
      setError("Save or cancel the current section before editing another one.");
      return;
    }
    setError("");
    setMessage("");
    setEditingSection(section);
  }

  function prepareSectionAdd(section: CatalogSectionId) {
    if (busy) return false;
    if (editingSection && !isSectionEditable(section)) {
      setError("Save or cancel the current section before adding here.");
      return false;
    }
    setError("");
    setMessage("");
    if (!editingSection) setEditingSection(section);
    return true;
  }

  function cancelSectionEdit(section: CatalogSectionId) {
    if (!savedProduct) return;
    const sectionFields: Record<CatalogSectionId, (keyof EditableProduct)[]> = {
      basics: ["currencyCode", "brandName", "storefrontHeadline", "storefrontSubtitle", "name", "slug", "description", "isPublished"],
      trust: ["trustItems"],
      variants: ["variants"],
      pricing: ["variants"],
      inventory: ["variants"],
      attributes: ["attributeGroups"],
      promotions: ["flashSale"],
      contact: ["socialSharingEnabled", "whatsAppEnabled", "whatsAppPhoneNumber", "whatsAppPrefillMessage"]
    };
    const fields = sectionFields[section];
    setProduct((current) => Object.assign({}, current, ...fields.map((field) => ({ [field]: savedProduct[field] }))));
    setEditingSection(null);
    setError("");
    setMessage("Changes discarded.");
  }

  async function saveSection(section: CatalogSectionId) {
    if (editingSection !== section || !hasUnsavedChanges) return;
    setBusy(true);
    setError("");
    setMessage("");
    try {
      const saved = await adminRequest<EditableProduct>("/api/v1/admin/catalog/product", {
        method: "PUT",
        body: JSON.stringify(product)
      });
      setProduct(saved);
      setSavedProduct(saved);
      setEditingSection(null);
      setMessage("Section changes saved.");
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Section changes could not be saved.");
    } finally {
      setBusy(false);
    }
  }

  function sectionActions(section: CatalogSectionId) {
    if (!product.id) return null;
    if (editingSection === section) {
      return (
        <div className="section-edit-actions">
          <button className="button button-quiet" type="button" onClick={() => cancelSectionEdit(section)} disabled={busy}>Cancel</button>
          <button className="button" type="button" onClick={() => void saveSection(section)} disabled={busy || !hasUnsavedChanges}>{busy ? "Saving…" : variantDataSections.includes(section) ? "Save variant data" : "Save section"}</button>
        </div>
      );
    }
    return (
      <button className="button button-quiet" type="button" onClick={() => beginSectionEdit(section)} disabled={busy} title={editingSection && hasUnsavedChanges ? "Save or cancel the current section before editing this section." : undefined}>
        {section === "variants" ? "Edit variants" : section === "pricing" ? "Edit prices" : section === "inventory" ? "Edit inventory" : "Edit"}
      </button>
    );
  }

  function updateFlashSale(changes: Partial<EditableFlashSale>) {
    setProduct((current) => ({ ...current, flashSale: { ...current.flashSale, ...changes } }));
  }

  function updateFlashSaleDate(key: "startsAtUtc" | "endsAtUtc", localValue: string) {
    updateFlashSale({ [key]: localValue ? new Date(localValue).toISOString() : null });
  }

  function updateTrustItem(index: number, changes: Partial<EditableTrustItem>) {
    setProduct((current) => ({
      ...current,
      trustItems: current.trustItems.map((item, itemIndex) => itemIndex === index ? { ...item, ...changes } : item)
    }));
  }

  async function uploadCatalogImage(mediaKind: "product" | "logo") {
    const file = mediaKind === "logo" ? logoFile : productImageFile;
    const altText = mediaKind === "logo" ? logoAltText : productImageAltText;
    if (!file) {
      setError(mediaKind === "logo" ? "Choose a logo image first." : "Choose a product image first.");
      return;
    }
    if (!altText.trim()) {
      setError("Add alternative text before uploading the image.");
      return;
    }
    setBusy(true);
    setError("");
    setMessage("");
    try {
      const form = new FormData();
      form.set("file", file);
      form.set("mediaKind", mediaKind);
      form.set("altText", altText);
      const image = await adminRequest<EditableProductImage>("/api/v1/admin/catalog/product/images", {
        method: "POST",
        body: form
      });
      setProduct((current) => ({
        ...current,
        images: mediaKind === "logo"
          ? [...current.images.filter((item) => item.mediaKind !== "logo"), image]
          : [...current.images, image]
      }));
      if (mediaKind === "logo") {
        setLogoFile(null);
        setLogoAltText("");
        setLogoResetKey((current) => current + 1);
      } else {
        setProductImageFile(null);
        setProductImageAltText("");
        setProductImageResetKey((current) => current + 1);
      }
      setMessage(mediaKind === "logo" ? "Brand logo saved." : "Product image saved.");
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Image upload failed.");
    } finally {
      setBusy(false);
    }
  }

  async function saveImageDetails(image: EditableProductImage) {
    setBusy(true);
    setError("");
    try {
      await adminRequest<EditableProductImage>(`/api/v1/admin/catalog/product/images/${image.id}`, {
        method: "PUT",
        body: JSON.stringify({ altText: image.altText, displayOrder: image.displayOrder })
      });
      setMessage("Image details saved.");
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Image details could not be saved.");
    } finally {
      setBusy(false);
    }
  }

  async function deleteImage(image: EditableProductImage) {
    setBusy(true);
    setError("");
    try {
      await adminRequest<void>(`/api/v1/admin/catalog/product/images/${image.id}`, { method: "DELETE" });
      setProduct((current) => ({ ...current, images: current.images.filter((item) => item.id !== image.id) }));
      setMessage(image.mediaKind === "logo" ? "Brand logo removed." : "Product image removed.");
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Image could not be removed.");
    } finally {
      setBusy(false);
    }
  }

  function toLocalDateTime(value: string | null): string {
    if (!value) return "";
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? "" : new Date(date.getTime() - date.getTimezoneOffset() * 60_000).toISOString().slice(0, 16);
  }

  function goToSection(sectionId: string) {
    setExpandedSections(current => ({ ...current, [sectionId]: true }));
    window.requestAnimationFrame(() => document.getElementById(sectionId)?.scrollIntoView({ behavior: "smooth", block: "start" }));
  }

  function sectionToggle(sectionId: string, label: string) {
    const expanded = expandedSections[sectionId] ?? false;
    return <button className="catalog-section-toggle" type="button" aria-expanded={expanded} aria-controls={sectionId} onClick={() => toggleSection(sectionId)}>
      <svg viewBox="0 0 24 24" aria-hidden="true" focusable="false"><path d={expanded ? "m6 14 6-6 6 6" : "m6 10 6 6 6-6"} /></svg>
      <span className="visually-hidden">{expanded ? "Collapse" : "Expand"} {label}</span>
    </button>;
  }

  function sectionTitleToggle(sectionId: string, title: string) {
    return <button className="catalog-section-title-toggle" type="button" aria-expanded={expandedSections[sectionId] ?? false} aria-controls={sectionId} onClick={() => toggleSection(sectionId)}>{title}</button>;
  }

  function toggleSection(sectionId: string) {
    setExpandedSections(current => ({ ...current, [sectionId]: !(current[sectionId] ?? false) }));
  }

  function expandedSectionClass(sectionId: string) {
    return expandedSections[sectionId] ? "catalog-section-expanded" : "";
  }

  return (
    <section className="admin-card">
      <div className="section-heading">
        <div>
          <h2>Product catalog</h2>
          <p className="admin-muted">Configure the single storefront product and its selectable variants.</p>
        </div>
        <span className="user-count">{product.isPublished ? "Published" : "Draft"}</span>
      </div>
      {loading ? <p className="admin-muted">Loading catalog…</p> : (
        <form className="catalog-form" onSubmit={handleSave}>
          <div className="catalog-toolbar">
            <div className="catalog-save-status" role="status" aria-live="polite">
              <strong>{hasUnsavedChanges ? "Unsaved changes" : !product.id ? "New product" : product.isPublished ? "Product published" : "Draft saved"}</strong>
              <span>{hasUnsavedChanges ? product.id ? "Save or cancel the section you’re editing." : "Complete the product setup and save when ready." : !product.id ? "Complete the product setup and save when ready." : "Edit a section to make changes."}</span>
            </div>
            {!product.id && <button className="button" type="submit" disabled={busy || loading || !hasUnsavedChanges}>
              {busy ? "Saving…" : "Save product"}
            </button>}
          </div>
          {message && <p className="admin-message catalog-feedback" role="status">{message}</p>}
          {error && <p className="admin-error catalog-feedback" role="alert">{error}</p>}
          <nav className="catalog-section-nav" aria-label="Product editor sections">
            {[
              ["product-basics", "Product info"],
              ["product-prices", "Prices"],
              ["product-inventory", "Inventory"],
              ["product-variants", "Variants"],
              ["product-media", "Product images"],
              ["product-logo", "Brand logo"],
              ["product-trust", "Store details"],
              ["product-attributes", "Attributes"],
              ["product-promotions", "Flash sale"],
              ["product-contact", "Sharing & contact"]
            ].filter(([sectionId]) => canManageStoreSettings || !["product-logo", "product-trust", "product-promotions", "product-contact"].includes(sectionId)).map(([sectionId, label]) => (
              <button type="button" key={sectionId} onClick={() => goToSection(sectionId)}>{label}</button>
            ))}
          </nav>
          <div className={`catalog-basics-section catalog-section ${expandedSectionClass("product-basics")}`} id="product-basics">
          <div className="section-heading catalog-accordion-heading">
            <div><h3>{sectionTitleToggle("product-basics", "Product information")}</h3><p className="admin-muted">Name, description, currency and publication status.</p></div>
            <div className="section-action-stack">{sectionActions("basics")}{product.id && <button className="button button-quiet" type="button" onClick={() => void handleDeleteProduct()} disabled={busy}>Delete product</button>}{sectionToggle("product-basics", "Product information")}</div>
          </div>
          <fieldset id="product-basics-content" className="catalog-form-fields catalog-accordion-content" hidden={!expandedSections["product-basics"]} disabled={busy || !isSectionEditable("basics")}>
          <div className="store-currency-setting">
            <label>
              Store currency
              <input
                maxLength={3}
                minLength={3}
                pattern="[A-Za-z]{3}"
                required
                value={product.currencyCode}
                disabled={Boolean(product.id)}
                placeholder="e.g. BDT"
                onChange={(event) => {
                  const currencyCode = event.target.value.toUpperCase();
                  setProduct((current) => ({
                    ...current,
                    currencyCode,
                    variants: current.variants.map((variant) => ({ ...variant, currencyCode }))
                  }));
                }}
              />
            </label>
            <p className="admin-muted">Applies to all variant prices and orders. Changing it after setup is locked until the price conversion rule is configured.</p>
          </div>
          <div className="catalog-fields">
            {canManageStoreSettings && <>
            <label>
              Shop name
              <input maxLength={120} required value={product.brandName} onChange={(event) => updateProduct("brandName", event.target.value)} />
              <small className="field-help">Shown in the store header and brand area.</small>
            </label>
            <label>
              Storefront slogan
              <input maxLength={200} value={product.storefrontHeadline} onChange={(event) => updateProduct("storefrontHeadline", event.target.value)} placeholder="Your trust, Your Love" />
              <small className="field-help">A short promise shown above the product description.</small>
            </label>
            <label>
              Supporting line (optional)
              <input maxLength={500} value={product.storefrontSubtitle} onChange={(event) => updateProduct("storefrontSubtitle", event.target.value)} placeholder="Add one short supporting sentence" />
              <small className="field-help">Appears below the storefront slogan.</small>
            </label>
            </>}
            <label>
              Product name
              <input maxLength={200} required value={product.name} onChange={(event) => updateProduct("name", event.target.value)} />
            </label>
            <label>
              URL slug
              <input maxLength={180} pattern="[a-z0-9]+(-[a-z0-9]+)*" required value={product.slug} onChange={(event) => updateProduct("slug", event.target.value)} />
            </label>
          </div>
          <label>
            Product description
            <textarea maxLength={8000} rows={5} value={product.description} onChange={(event) => updateProduct("description", event.target.value)} />
          </label>
          <fieldset className="admin-field-wide"><legend>Product translations</legend>
            <p className="admin-muted">English is the source and fallback. Add Bangla and Spanish content below; blank fields fall back to English.</p>
            {LANGUAGES.map(language => <div className="admin-form-grid" key={language.code}>
              <label>{language.label} product name<input maxLength={200} value={product.catalogTranslations[language.code]?.["product.name"] ?? ""} onChange={event => setProduct(current => ({ ...current, catalogTranslations: { ...current.catalogTranslations, [language.code]: { ...current.catalogTranslations[language.code], "product.name": event.target.value } } }))} /></label>
              <label>{language.label} description<textarea maxLength={8000} rows={3} value={product.catalogTranslations[language.code]?.["product.description"] ?? ""} onChange={event => setProduct(current => ({ ...current, catalogTranslations: { ...current.catalogTranslations, [language.code]: { ...current.catalogTranslations[language.code], "product.description": event.target.value } } }))} /></label>
            </div>)}
          </fieldset>
          <label className="role-option publish-toggle">
            <input type="checkbox" checked={product.isPublished} onChange={(event) => updateProduct("isPublished", event.target.checked)} />
            <span>Publish product to the storefront</span>
          </label>
          </fieldset>
          </div>

          <section className={`catalog-settings catalog-section ${expandedSectionClass("product-media")}`} id="product-media">
            <div className="section-heading catalog-accordion-heading">
              <div><h3>{sectionTitleToggle("product-media", "Product images")}</h3><p className="admin-muted">Upload and arrange product photos and gallery thumbnails.</p></div>
              {sectionToggle("product-media", "Product images")}
            </div>
            <p className="admin-muted">{product.images.filter(image => image.mediaKind === "product").length} of 8 product images · JPEG, PNG, and WebP are accepted, up to 10 MB each.</p>
            <div className="image-upload-form">
              <label>
                Image file
                <input key={productImageResetKey} type="file" accept="image/jpeg,image/png,image/webp" onChange={(event) => setProductImageFile(event.target.files?.[0] ?? null)} />
              </label>
              <label>
                Alternative text
                <input maxLength={200} value={productImageAltText} onChange={(event) => setProductImageAltText(event.target.value)} placeholder="Describe the image" />
              </label>
              <button className="button button-quiet" type="button" onClick={() => void uploadCatalogImage("product")} disabled={busy || !product.id || product.images.filter(image => image.mediaKind === "product").length >= 8} title={product.images.filter(image => image.mediaKind === "product").length >= 8 ? "A product can have up to eight images." : undefined}>{busy ? "Saving…" : "Save product image"}</button>
              {!product.id && <p className="admin-muted">Save the product draft before uploading media.</p>}
            </div>
            <div className="admin-media-list">
              {product.images.filter((image) => image.mediaKind === "product").map((image) => (
                <article className="admin-media-row" key={image.id}>
                  {imagePreviews[image.id] ? <img src={imagePreviews[image.id]} alt={image.altText} /> : <div className="admin-media-preview" aria-label="Loading image preview" />}
                  <div className="catalog-fields">
                    <label>
                      Image alternative text
                      <input maxLength={200} value={image.altText} onChange={(event) => setProduct((current) => ({ ...current, images: current.images.map((item) => item.id === image.id ? { ...item, altText: event.target.value } : item) }))} />
                    </label>
                    <label>
                      Display order
                      <input type="number" min="0" step="1" value={image.displayOrder} onChange={(event) => setProduct((current) => ({ ...current, images: current.images.map((item) => item.id === image.id ? { ...item, displayOrder: Number(event.target.value) } : item) }))} />
                    </label>
                  </div>
                  <div className="variant-actions">
                    <button className="button button-quiet" type="button" onClick={() => void saveImageDetails(image)} disabled={busy}>Save details</button>
                    <button className="button button-quiet" type="button" onClick={() => void deleteImage(image)} disabled={busy}>Delete image</button>
                  </div>
                </article>
              ))}
              {product.images.filter((image) => image.mediaKind === "product").length === 0 && <p className="admin-muted">No product images uploaded.</p>}
            </div>
          </section>

          {canManageStoreSettings && <section className={`catalog-settings catalog-section ${expandedSectionClass("product-logo")}`} id="product-logo">
            <div className="section-heading catalog-accordion-heading">
              <div><h3>{sectionTitleToggle("product-logo", "Brand logo")}</h3><p className="admin-muted">Manage the shop identity image separately from product photos.</p></div>
              {sectionToggle("product-logo", "Brand logo")}
            </div>
            <p className="admin-muted">Upload or replace the logo shown in the storefront header. This is managed separately from product images.</p>
            <div className="image-upload-form">
              <label>
                Logo file
                <input key={logoResetKey} type="file" accept="image/jpeg,image/png,image/webp" onChange={(event) => setLogoFile(event.target.files?.[0] ?? null)} />
              </label>
              <label>
                Alternative text
                <input maxLength={200} value={logoAltText} onChange={(event) => setLogoAltText(event.target.value)} placeholder="Describe the shop logo" />
              </label>
              <button className="button button-quiet" type="button" onClick={() => void uploadCatalogImage("logo")} disabled={busy || !product.id}>{busy ? "Saving…" : "Save brand logo"}</button>
              {!product.id && <p className="admin-muted">Save the product draft before uploading a logo.</p>}
            </div>
            <div className="admin-media-list">
              {product.images.filter((image) => image.mediaKind === "logo").map((image) => (
                <article className="admin-media-row" key={image.id}>
                  {imagePreviews[image.id] ? <img src={imagePreviews[image.id]} alt={image.altText} /> : <div className="admin-media-preview" aria-label="Loading logo preview" />}
                  <div className="catalog-fields">
                    <label>
                      Logo alternative text
                      <input maxLength={200} value={image.altText} onChange={(event) => setProduct((current) => ({ ...current, images: current.images.map((item) => item.id === image.id ? { ...item, altText: event.target.value } : item) }))} />
                    </label>
                  </div>
                  <div className="variant-actions">
                    <button className="button button-quiet" type="button" onClick={() => void saveImageDetails(image)} disabled={busy}>Save logo details</button>
                    <button className="button button-quiet" type="button" onClick={() => void deleteImage(image)} disabled={busy}>Delete logo</button>
                  </div>
                </article>
              ))}
              {product.images.every((image) => image.mediaKind !== "logo") && <p className="admin-muted">No brand logo uploaded.</p>}
            </div>
          </section>}

          {canManageStoreSettings && <section className={`catalog-settings catalog-section ${expandedSectionClass("product-trust")}`} id="product-trust">
            <div className="section-heading catalog-accordion-heading">
              <div>
                <h3>{sectionTitleToggle("product-trust", "Trust and service details")}</h3>
              <p className="admin-muted">Add, edit, or delete product level promises shown below the product. Changes apply when you save this section.</p>
              </div>
              <div className="section-action-stack">
                {sectionActions("trust")}
                <button className="button button-quiet" type="button" onClick={() => {
                  if (prepareSectionAdd("trust")) updateProduct("trustItems", [...product.trustItems, { id: null, title: "", description: "", isActive: true, displayOrder: product.trustItems.length }]);
                }} disabled={busy || product.trustItems.length >= 8}>
                  Add detail
                </button>
                {sectionToggle("product-trust", "Store details")}
              </div>
            </div>
            <fieldset className="catalog-form-fields">
            {product.trustItems.map((item, index) => (
              <fieldset className="variant-card" key={item.id ?? `trust-${index}`}>
                <legend>Store detail {index + 1}</legend>
                <fieldset className="catalog-fields" disabled={busy || !isSectionEditable("trust")}>
                  <label>
                    Title
                    <input maxLength={80} required value={item.title} onChange={(event) => updateTrustItem(index, { title: event.target.value })} placeholder="e.g. Free shipping" />
                  </label>
                  <label>
                    Description
                    <input maxLength={300} value={item.description} onChange={(event) => updateTrustItem(index, { description: event.target.value })} />
                  </label>
                  <label className="role-option">
                    <input type="checkbox" checked={item.isActive} onChange={(event) => updateTrustItem(index, { isActive: event.target.checked })} />
                    <span>Show on storefront</span>
                  </label>
                  <label>
                    Display order
                    <input type="number" min="0" step="1" value={item.displayOrder} onChange={(event) => updateTrustItem(index, { displayOrder: Number(event.target.value) })} />
                  </label>
                </fieldset>
                <button className="button button-quiet" type="button" onClick={() => {
                  if (confirmChildRemoval(`Delete store detail “${item.title || `Store detail ${index + 1}`}”?`)) {
                    if (prepareSectionAdd("trust")) updateProduct("trustItems", product.trustItems.filter((_, itemIndex) => itemIndex !== index).map((entry, order) => ({ ...entry, displayOrder: order })));
                  }
                }} disabled={busy}>Delete detail</button>
              </fieldset>
            ))}
            </fieldset>
          </section>}

          <section className={`catalog-settings catalog-section ${expandedSectionClass("product-variants")}`} id="product-variants">
          <div className="section-heading variant-heading catalog-accordion-heading">
            <div>
              <h3>{sectionTitleToggle("product-variants", "Variants")}</h3>
              <p className="admin-muted">{product.variants.length} of 100 variants · {product.variants.filter(variant => variant.isActive).length} active on the storefront · {product.variants.filter(variant => !variant.isActive).length} inactive and hidden. Edit variant details, prices, and inventory together, then save once. Variants used by existing orders cannot be deleted, so deactivate them instead.</p>
            </div>
            <div className="section-action-stack">
              {sectionActions("variants")}
              <button className="button button-quiet" type="button" onClick={() => {
                if (prepareSectionAdd("variants")) updateProduct("variants", [...product.variants, emptyVariant(product.currencyCode)]);
              }} disabled={!canAddSectionItem(product.variants.length, 100, busy)}>
                Add another variant
              </button>
              {sectionToggle("product-variants", "Variants")}
            </div>
          </div>

          <fieldset className="catalog-form-fields">
          {product.variants.map((variant, index) => (
            <fieldset className="variant-card" key={variant.id ?? `new-${index}`}>
              <legend>Variant {index + 1}</legend>
              <fieldset className="catalog-fields" disabled={busy || !isSectionEditable("variants")}>
                <label>
                  SKU
                  <input maxLength={64} required value={variant.sku} onChange={(event) => updateVariant(index, { sku: event.target.value })} />
                </label>
                <label>
                  Display name / options
                  <input maxLength={200} required value={variant.displayName} onChange={(event) => updateVariant(index, { displayName: event.target.value })} />
                </label>
                <label>
                  Regular price ({product.currencyCode || "currency"})
                  <input type="number" min="0" step="0.0001" required value={variant.price} onChange={(event) => updateVariant(index, { price: Number(event.target.value) })} />
                </label>
              </fieldset>
              {variant.id && <fieldset className="admin-form-grid" disabled={busy || !isSectionEditable("variants")}>{LANGUAGES.map(language => <label key={language.code}>{language.label} display name<input maxLength={200} value={product.catalogTranslations[language.code]?.[`variant.${variant.id}.name`] ?? ""} onChange={event => setProduct(current => ({ ...current, catalogTranslations: { ...current.catalogTranslations, [language.code]: { ...current.catalogTranslations[language.code], [`variant.${variant.id}.name`]: event.target.value } } }))} /></label>)}</fieldset>}
              <div className="variant-actions">
                <label className="role-option">
                  <input type="checkbox" disabled={busy || !isSectionEditable("variants")} checked={variant.isActive} onChange={(event) => updateVariant(index, { isActive: event.target.checked })} />
                  <span>Available on storefront</span>
                </label>
                <button className="button button-quiet" type="button" onClick={() => {
                  if (confirmChildRemoval(`Delete variant “${variant.displayName || variant.sku || `Variant ${index + 1}`}”?`)) {
                    if (prepareSectionAdd("variants")) updateProduct("variants", product.variants.filter((_, itemIndex) => itemIndex !== index));
                  }
                }} disabled={busy || product.variants.length === 1}>
                  Delete variant
                </button>
              </div>
            </fieldset>
          ))}
          </fieldset>
          </section>

          <section className={`catalog-settings catalog-section ${expandedSectionClass("product-prices")}`} id="product-prices">
            <div className="section-heading catalog-accordion-heading">
              <div><h3>{sectionTitleToggle("product-prices", "Prices")}</h3><p className="admin-muted">Set prices for each variant in {product.currencyCode || "the store currency"}. Prices save together with variant and inventory edits.</p></div>
              <div className="section-action-stack">{sectionActions("pricing")}{sectionToggle("product-prices", "Prices")}</div>
            </div>
            <fieldset className="catalog-form-fields" disabled={busy || !isSectionEditable("pricing")}>
              {product.variants.map((variant, index) => <fieldset className="variant-card catalog-price-row" key={variant.id ?? `price-${index}`}>
                <legend>{variant.displayName || `Variant ${index + 1}`}</legend>
                <label>Regular price ({product.currencyCode || "currency"})
                  <input type="number" min="0" step="0.0001" required value={variant.price} onChange={event => updateVariant(index, { price: Number(event.target.value) })} />
                </label>
              </fieldset>)}
            </fieldset>
          </section>

          <section className={`catalog-settings catalog-section ${expandedSectionClass("product-inventory")}`} id="product-inventory">
            <div className="section-heading catalog-accordion-heading">
              <div><h3>{sectionTitleToggle("product-inventory", "Inventory")}</h3><p className="admin-muted">Set stock separately for each variant. Newly added variants appear here immediately; save once to store their details, prices, and inventory together.</p></div>
              <div className="section-action-stack">{sectionActions("inventory")}{sectionToggle("product-inventory", "Inventory")}</div>
            </div>
            <fieldset className="catalog-form-fields" disabled={busy || !isSectionEditable("inventory")}>
              {product.variants.map((variant, index) => <fieldset className="variant-card catalog-inventory-row" key={variant.id ?? `inventory-${index}`}>
                <legend>{variant.displayName || `Variant ${index + 1}`}{variant.sku ? ` · ${variant.sku}` : ""}</legend>
                <label>Stock quantity
                  <input type="number" min="0" step="1" required value={variant.stockQuantity} onChange={event => updateVariant(index, { stockQuantity: Number(event.target.value) })} />
                </label>
              </fieldset>)}
            </fieldset>
          </section>

          <section className={`catalog-settings catalog-section ${expandedSectionClass("product-attributes")}`} id="product-attributes">
          <div className="section-heading variant-heading catalog-accordion-heading">
            <div>
              <h3>{sectionTitleToggle("product-attributes", "Attribute groups")}</h3>
              <p className="admin-muted">{product.attributeGroups.length} of 30 groups. Attribute groups belong to this product, and choices belong to their group. Add, edit, or delete them here, then save this section to apply.</p>
            </div>
            <div className="section-action-stack">
              {sectionActions("attributes")}
              <button className="button button-quiet" type="button" onClick={() => {
                if (prepareSectionAdd("attributes")) updateProduct("attributeGroups", [...product.attributeGroups, emptyAttributeGroup(product.attributeGroups.length)]);
              }} disabled={!canAddSectionItem(product.attributeGroups.length, 30, busy)}>
                Add attribute group
              </button>
              {sectionToggle("product-attributes", "Attribute groups")}
            </div>
          </div>

          <fieldset className="catalog-form-fields">
          {product.attributeGroups.map((group, groupIndex) => (
            <fieldset className="variant-card" key={group.id ?? `attribute-${groupIndex}`}>
              <legend>Attribute {groupIndex + 1}</legend>
              <fieldset className="catalog-fields" disabled={busy || !isSectionEditable("attributes")}>
                <label>
                  Name
                  <input maxLength={100} required value={group.name} onChange={(event) => updateAttributeGroup(groupIndex, { name: event.target.value })} placeholder="e.g. Processor" />
                </label>
                {group.id && LANGUAGES.map(language => <label key={language.code}>{language.label} name<input maxLength={100} value={product.catalogTranslations[language.code]?.[`attributeGroup.${group.id}.name`] ?? ""} onChange={event => setProduct(current => ({ ...current, catalogTranslations: { ...current.catalogTranslations, [language.code]: { ...current.catalogTranslations[language.code], [`attributeGroup.${group.id}.name`]: event.target.value } } }))} /></label>)}
                <label className="role-option">
                  <input type="checkbox" checked={group.isRequired} onChange={(event) => updateAttributeGroup(groupIndex, { isRequired: event.target.checked })} />
                  <span>Shopper must choose an option</span>
                </label>
                <label className="role-option">
                  <input type="checkbox" checked={group.allowMultiple} onChange={(event) => updateAttributeGroup(groupIndex, { allowMultiple: event.target.checked })} />
                  <span>Allow multiple choices (add-ons)</span>
                </label>
              </fieldset>
              <div className="section-heading variant-heading">
                <h4>Choices</h4>
                <button className="button button-quiet" type="button" onClick={() => {
                  if (prepareSectionAdd("attributes")) updateAttributeGroup(groupIndex, { options: [...group.options, emptyAttributeOption(group.options.length)] });
                }} disabled={busy || group.options.length >= 100}>
                  Add choice
                </button>
              </div>
              {group.options.map((option, optionIndex) => (
                <div className="attribute-option-row" key={option.id ?? `choice-${groupIndex}-${optionIndex}`}>
                  <fieldset className="catalog-fields attribute-option" disabled={busy || !isSectionEditable("attributes")}>
                  <label>
                    Choice
                    <input maxLength={150} required value={option.name} onChange={(event) => updateAttributeOption(groupIndex, optionIndex, { name: event.target.value })} placeholder="e.g. 16 GB RAM" />
                  </label>
                  {option.id && LANGUAGES.map(language => <label key={language.code}>{language.label} choice<input maxLength={150} value={product.catalogTranslations[language.code]?.[`attributeOption.${option.id}.name`] ?? ""} onChange={event => setProduct(current => ({ ...current, catalogTranslations: { ...current.catalogTranslations, [language.code]: { ...current.catalogTranslations[language.code], [`attributeOption.${option.id}.name`]: event.target.value } } }))} /></label>)}
                  <label>
                    Added price
                    <input type="number" min="0" step="0.0001" required value={option.priceAdjustment} onChange={(event) => updateAttributeOption(groupIndex, optionIndex, { priceAdjustment: Number(event.target.value) })} />
                  </label>
                  <label className="role-option">
                    <input type="checkbox" checked={option.isActive} onChange={(event) => updateAttributeOption(groupIndex, optionIndex, { isActive: event.target.checked })} />
                    <span>Available</span>
                  </label>
                  </fieldset>
                  <button className="button button-quiet" type="button" onClick={() => {
                    if (confirmChildRemoval(`Delete choice “${option.name || `Choice ${optionIndex + 1}`}” from “${group.name || `Attribute ${groupIndex + 1}`}”?`)) {
                      if (prepareSectionAdd("attributes")) updateAttributeGroup(groupIndex, { options: group.options.filter((_, itemIndex) => itemIndex !== optionIndex).map((item, order) => ({ ...item, displayOrder: order })) });
                    }
                  }} disabled={busy || group.options.length === 1}>
                    Delete choice
                  </button>
                </div>
              ))}
              <button className="button button-quiet" type="button" onClick={() => {
                if (confirmChildRemoval(`Delete attribute group “${group.name || `Attribute ${groupIndex + 1}`}” and all ${group.options.length} of its choices?`)) {
                  if (prepareSectionAdd("attributes")) updateProduct("attributeGroups", product.attributeGroups.filter((_, itemIndex) => itemIndex !== groupIndex).map((item, order) => ({ ...item, displayOrder: order })));
                }
              }} disabled={busy}>
                Delete attribute group
              </button>
            </fieldset>
          ))}
          </fieldset>
          </section>

          {canManageStoreSettings && <section className={`catalog-settings catalog-section ${expandedSectionClass("product-promotions")}`} id="product-promotions">
            <div className="section-heading catalog-accordion-heading">
              <div><h3>{sectionTitleToggle("product-promotions", "Flash sale")}</h3><p className="admin-muted">Schedule a discount for a chosen time window.</p></div>
              <div className="section-action-stack">{sectionActions("promotions")}{sectionToggle("product-promotions", "Flash sale")}</div>
            </div>
            <fieldset className="catalog-form-fields" disabled={busy || !isSectionEditable("promotions")}>
            <label className="role-option">
              <input type="checkbox" checked={product.flashSale.enabled} onChange={(event) => updateFlashSale({ enabled: event.target.checked })} />
              <span>Enable flash sale</span>
            </label>
            {product.flashSale.enabled && (
              <div className="catalog-fields">
                <label>
                  Sale label
                  <input maxLength={100} required value={product.flashSale.label} onChange={(event) => updateFlashSale({ label: event.target.value })} placeholder="e.g. Weekend flash sale" />
                </label>
                <label>
                  Discount type
                  <select value={product.flashSale.discountType} onChange={(event) => updateFlashSale({ discountType: event.target.value as EditableFlashSale["discountType"] })}>
                    <option value="percentage">Percentage</option>
                    <option value="fixedAmount">Fixed amount</option>
                  </select>
                </label>
                <label>
                  {product.flashSale.discountType === "percentage" ? "Discount percentage" : "Discount amount"}
                  <input type="number" min="0.0001" max={product.flashSale.discountType === "percentage" ? 100 : undefined} step={product.flashSale.discountType === "percentage" ? 0.01 : 0.0001} required value={product.flashSale.discountValue} onChange={(event) => updateFlashSale({ discountValue: Number(event.target.value) })} />
                </label>
                <label>
                  Starts (your local time)
                  <input type="datetime-local" required value={toLocalDateTime(product.flashSale.startsAtUtc)} onChange={(event) => updateFlashSaleDate("startsAtUtc", event.target.value)} />
                </label>
                <label>
                  Ends (your local time)
                  <input type="datetime-local" required value={toLocalDateTime(product.flashSale.endsAtUtc)} onChange={(event) => updateFlashSaleDate("endsAtUtc", event.target.value)} />
                </label>
              </div>
            )}
            </fieldset>
          </section>}

          {canManageStoreSettings && <section className={`catalog-settings catalog-section ${expandedSectionClass("product-contact")}`} id="product-contact">
            <div className="section-heading catalog-accordion-heading">
              <div><h3>{sectionTitleToggle("product-contact", "Sharing and contact")}</h3><p className="admin-muted">Configure product sharing and WhatsApp contact.</p></div>
              <div className="section-action-stack">{sectionActions("contact")}{sectionToggle("product-contact", "Sharing and contact")}</div>
            </div>
            <fieldset className="catalog-form-fields" disabled={busy || !isSectionEditable("contact")}>
            <label className="role-option">
              <input type="checkbox" checked={product.socialSharingEnabled} onChange={(event) => updateProduct("socialSharingEnabled", event.target.checked)} />
              <span>Show product share action</span>
            </label>
            <label className="role-option">
              <input type="checkbox" checked={product.whatsAppEnabled} onChange={(event) => updateProduct("whatsAppEnabled", event.target.checked)} />
              <span>Show WhatsApp contact action</span>
            </label>
            {product.whatsAppEnabled && (
              <div className="catalog-fields">
                <label>
                  WhatsApp number with country code
                  <input type="tel" maxLength={20} required pattern="\+?[0-9\s()\-]{7,20}" value={product.whatsAppPhoneNumber} onChange={(event) => updateProduct("whatsAppPhoneNumber", event.target.value)} placeholder="e.g. +880 1XXXXXXXXX" />
                </label>
                <label>
                  Prefilled message
                  <textarea maxLength={500} rows={3} value={product.whatsAppPrefillMessage} onChange={(event) => updateProduct("whatsAppPrefillMessage", event.target.value)} />
                </label>
              </div>
            )}
            </fieldset>
          </section>}
        </form>
      )}
    </section>
  );
}
