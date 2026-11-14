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

const emptyProduct = (): EditableProduct => ({
  id: null,
  name: "",
  slug: "",
  description: "",
  isPublished: false,
  variants: [emptyVariant()],
  attributeGroups: [],
  flashSale: { enabled: false, label: "", discountType: "percentage", discountValue: 0, startsAtUtc: null, endsAtUtc: null },
  socialSharingEnabled: true,
  whatsAppEnabled: false,
  whatsAppPhoneNumber: "",
  whatsAppPrefillMessage: "Hello, I have a question about this product.",
  brandName: "OneCart",
  storefrontHeadline: "",
  storefrontSubtitle: "",
  images: [],
  trustItems: []
});

function emptyVariant(): EditableVariant {
  return {
    id: null,
    sku: "",
    displayName: "",
    price: 0,
    currencyCode: "",
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

export default function AdminCatalogEditor() {
  const [product, setProduct] = useState<EditableProduct>(emptyProduct);
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const [imagePreviews, setImagePreviews] = useState<Record<string, string>>({});
  const [uploadFile, setUploadFile] = useState<File | null>(null);
  const [uploadKind, setUploadKind] = useState<"product" | "logo">("product");
  const [uploadAltText, setUploadAltText] = useState("");
  const [uploadResetKey, setUploadResetKey] = useState(0);

  useEffect(() => {
    let active = true;
    void adminRequest<EditableProduct>("/api/v1/admin/catalog/product")
      .then((loaded) => {
        if (active) setProduct(loaded);
      })
      .catch((reason: unknown) => {
        if (!active) return;
        if (reason instanceof Error && reason.message.includes("404")) {
          setProduct(emptyProduct());
        } else {
          setError(reason instanceof Error ? reason.message : "Could not load product settings.");
        }
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => { active = false; };
  }, []);

  useEffect(() => {
    let active = true;
    let previewUrls: string[] = [];
    void Promise.all(product.images.map(async (image) => [image.id, await loadAdminImagePreview(image.id)] as const))
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
  }, [product.images]);

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
      setMessage("Product, storefront content, variants, and attributes saved.");
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Product settings could not be saved.");
    } finally {
      setBusy(false);
    }
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

  async function uploadProductImage() {
    if (!uploadFile) {
      setError("Choose an image first.");
      return;
    }
    setBusy(true);
    setError("");
    setMessage("");
    try {
      const form = new FormData();
      form.set("file", uploadFile);
      form.set("mediaKind", uploadKind);
      form.set("altText", uploadAltText);
      const image = await adminRequest<EditableProductImage>("/api/v1/admin/catalog/product/images", {
        method: "POST",
        body: form
      });
      setProduct((current) => ({
        ...current,
        images: uploadKind === "logo"
          ? [...current.images.filter((item) => item.mediaKind !== "logo"), image]
          : [...current.images, image]
      }));
      setUploadFile(null);
      setUploadAltText("");
      setUploadResetKey((current) => current + 1);
      setMessage(uploadKind === "logo" ? "Brand logo uploaded." : "Product image uploaded.");
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
          <div className="catalog-fields">
            <label>
              Store / brand name
              <input maxLength={120} required value={product.brandName} onChange={(event) => updateProduct("brandName", event.target.value)} />
            </label>
            <label>
              Storefront headline
              <input maxLength={200} value={product.storefrontHeadline} onChange={(event) => updateProduct("storefrontHeadline", event.target.value)} placeholder="Uses the product name when blank" />
            </label>
            <label>
              Storefront subtitle
              <input maxLength={500} value={product.storefrontSubtitle} onChange={(event) => updateProduct("storefrontSubtitle", event.target.value)} placeholder="Short product value statement" />
            </label>
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

          <section className="catalog-settings">
            <h3>Store branding and product images</h3>
            <p className="admin-muted">Upload a brand logo or up to eight product images. JPEG, PNG, and WebP are accepted, up to 10 MB each.</p>
            <div className="image-upload-form">
              <label>
                Image type
                <select value={uploadKind} onChange={(event) => setUploadKind(event.target.value as "product" | "logo")}>
                  <option value="product">Product image</option>
                  <option value="logo">Brand logo (replaces current logo)</option>
                </select>
              </label>
              <label>
                Image file
                <input key={uploadResetKey} type="file" accept="image/jpeg,image/png,image/webp" required onChange={(event) => setUploadFile(event.target.files?.[0] ?? null)} />
              </label>
              <label>
                Alternative text
                <input maxLength={200} required value={uploadAltText} onChange={(event) => setUploadAltText(event.target.value)} placeholder="Describe the image" />
              </label>
              <button className="button button-quiet" type="button" onClick={() => void uploadProductImage()} disabled={busy || !product.id}>{busy ? "Uploading…" : "Upload image"}</button>
              {!product.id && <p className="admin-muted">Save the product draft before uploading media.</p>}
            </div>
            <div className="admin-media-list">
              {product.images.map((image) => (
                <article className="admin-media-row" key={image.id}>
                  {imagePreviews[image.id] ? <img src={imagePreviews[image.id]} alt={image.altText} /> : <div className="admin-media-preview" aria-label="Loading image preview" />}
                  <div className="catalog-fields">
                    <label>
                      {image.mediaKind === "logo" ? "Logo alternative text" : "Image alternative text"}
                      <input maxLength={200} value={image.altText} onChange={(event) => setProduct((current) => ({ ...current, images: current.images.map((item) => item.id === image.id ? { ...item, altText: event.target.value } : item) }))} />
                    </label>
                    {image.mediaKind === "product" && (
                      <label>
                        Display order
                        <input type="number" min="0" step="1" value={image.displayOrder} onChange={(event) => setProduct((current) => ({ ...current, images: current.images.map((item) => item.id === image.id ? { ...item, displayOrder: Number(event.target.value) } : item) }))} />
                      </label>
                    )}
                  </div>
                  <div className="variant-actions">
                    <button className="button button-quiet" type="button" onClick={() => void saveImageDetails(image)} disabled={busy}>Save details</button>
                    <button className="button button-quiet" type="button" onClick={() => void deleteImage(image)} disabled={busy}>Remove image</button>
                  </div>
                </article>
              ))}
              {product.images.length === 0 && <p className="admin-muted">No product images or logo uploaded.</p>}
            </div>
          </section>

          <section className="catalog-settings">
            <div className="section-heading">
              <div>
                <h3>Trust and service details</h3>
                <p className="admin-muted">Add short store promises shown below the product.</p>
              </div>
              <button className="button button-quiet" type="button" onClick={() => updateProduct("trustItems", [...product.trustItems, { id: null, title: "", description: "", isActive: true, displayOrder: product.trustItems.length }])} disabled={busy || product.trustItems.length >= 8}>
                Add detail
              </button>
            </div>
            {product.trustItems.map((item, index) => (
              <fieldset className="variant-card" key={item.id ?? `trust-${index}`}>
                <legend>Store detail {index + 1}</legend>
                <div className="catalog-fields">
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
                </div>
                <button className="button button-quiet" type="button" onClick={() => updateProduct("trustItems", product.trustItems.filter((_, itemIndex) => itemIndex !== index).map((entry, order) => ({ ...entry, displayOrder: order })))} disabled={busy}>Remove detail</button>
              </fieldset>
            ))}
          </section>

          <div className="section-heading variant-heading">
            <div>
              <h3>Variants</h3>
              <p className="admin-muted">Each variant has its own SKU, display name, price, currency and stock count.</p>
            </div>
            <button className="button button-quiet" type="button" onClick={() => updateProduct("variants", [...product.variants, emptyVariant()])} disabled={busy || product.variants.length >= 100}>
              Add variant
            </button>
          </div>

          {product.variants.map((variant, index) => (
            <fieldset className="variant-card" key={variant.id ?? `new-${index}`}>
              <legend>Variant {index + 1}</legend>
              <div className="catalog-fields">
                <label>
                  SKU
                  <input maxLength={64} required value={variant.sku} onChange={(event) => updateVariant(index, { sku: event.target.value })} />
                </label>
                <label>
                  Display name / options
                  <input maxLength={200} required value={variant.displayName} onChange={(event) => updateVariant(index, { displayName: event.target.value })} />
                </label>
                <label>
                  Price
                  <input type="number" min="0" step="0.0001" required value={variant.price} onChange={(event) => updateVariant(index, { price: Number(event.target.value) })} />
                </label>
                <label>
                  Currency code
                  <input maxLength={3} minLength={3} pattern="[A-Za-z]{3}" required placeholder="e.g. USD" value={variant.currencyCode} onChange={(event) => updateVariant(index, { currencyCode: event.target.value.toUpperCase() })} />
                </label>
                <label>
                  Stock quantity
                  <input type="number" min="0" step="1" required value={variant.stockQuantity} onChange={(event) => updateVariant(index, { stockQuantity: Number(event.target.value) })} />
                </label>
              </div>
              <div className="variant-actions">
                <label className="role-option">
                  <input type="checkbox" checked={variant.isActive} onChange={(event) => updateVariant(index, { isActive: event.target.checked })} />
                  <span>Available on storefront</span>
                </label>
                <button className="button button-quiet" type="button" onClick={() => updateProduct("variants", product.variants.filter((_, itemIndex) => itemIndex !== index))} disabled={busy || product.variants.length === 1}>
                  Remove
                </button>
              </div>
            </fieldset>
          ))}

          <div className="section-heading variant-heading">
            <div>
              <h3>Configurable attributes</h3>
              <p className="admin-muted">Add required selections or optional add-ons. Choices can increase the selected variant price.</p>
            </div>
            <button className="button button-quiet" type="button" onClick={() => updateProduct("attributeGroups", [...product.attributeGroups, emptyAttributeGroup(product.attributeGroups.length)])} disabled={busy || product.attributeGroups.length >= 30}>
              Add attribute
            </button>
          </div>

          {product.attributeGroups.map((group, groupIndex) => (
            <fieldset className="variant-card" key={group.id ?? `attribute-${groupIndex}`}>
              <legend>Attribute {groupIndex + 1}</legend>
              <div className="catalog-fields">
                <label>
                  Name
                  <input maxLength={100} required value={group.name} onChange={(event) => updateAttributeGroup(groupIndex, { name: event.target.value })} placeholder="e.g. Processor" />
                </label>
                <label className="role-option">
                  <input type="checkbox" checked={group.isRequired} onChange={(event) => updateAttributeGroup(groupIndex, { isRequired: event.target.checked })} />
                  <span>Shopper must choose an option</span>
                </label>
                <label className="role-option">
                  <input type="checkbox" checked={group.allowMultiple} onChange={(event) => updateAttributeGroup(groupIndex, { allowMultiple: event.target.checked })} />
                  <span>Allow multiple choices (add-ons)</span>
                </label>
              </div>
              <div className="section-heading variant-heading">
                <h4>Choices</h4>
                <button className="button button-quiet" type="button" onClick={() => updateAttributeGroup(groupIndex, { options: [...group.options, emptyAttributeOption(group.options.length)] })} disabled={busy || group.options.length >= 100}>
                  Add choice
                </button>
              </div>
              {group.options.map((option, optionIndex) => (
                <div className="catalog-fields attribute-option" key={option.id ?? `choice-${groupIndex}-${optionIndex}`}>
                  <label>
                    Choice
                    <input maxLength={150} required value={option.name} onChange={(event) => updateAttributeOption(groupIndex, optionIndex, { name: event.target.value })} placeholder="e.g. 16 GB RAM" />
                  </label>
                  <label>
                    Added price
                    <input type="number" min="0" step="0.0001" required value={option.priceAdjustment} onChange={(event) => updateAttributeOption(groupIndex, optionIndex, { priceAdjustment: Number(event.target.value) })} />
                  </label>
                  <label className="role-option">
                    <input type="checkbox" checked={option.isActive} onChange={(event) => updateAttributeOption(groupIndex, optionIndex, { isActive: event.target.checked })} />
                    <span>Available</span>
                  </label>
                  <button className="button button-quiet" type="button" onClick={() => updateAttributeGroup(groupIndex, { options: group.options.filter((_, itemIndex) => itemIndex !== optionIndex).map((item, order) => ({ ...item, displayOrder: order })) })} disabled={busy || group.options.length === 1}>
                    Remove choice
                  </button>
                </div>
              ))}
              <button className="button button-quiet" type="button" onClick={() => updateProduct("attributeGroups", product.attributeGroups.filter((_, itemIndex) => itemIndex !== groupIndex).map((item, order) => ({ ...item, displayOrder: order })))} disabled={busy}>
                Remove attribute
              </button>
            </fieldset>
          ))}

          <section className="catalog-settings">
            <h3>Flash sale</h3>
            <p className="admin-muted">Set a scheduled discount. The storefront shows the regular and sale prices only during the configured window.</p>
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
          </section>

          <section className="catalog-settings">
            <h3>Storefront contact and sharing</h3>
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
          </section>

          <label className="role-option publish-toggle">
            <input type="checkbox" checked={product.isPublished} onChange={(event) => updateProduct("isPublished", event.target.checked)} />
            <span>Publish product to the storefront</span>
          </label>
          <button className="button" type="submit" disabled={busy || loading}>{busy ? "Saving…" : "Save catalog"}</button>
        </form>
      )}
      {message && <p className="admin-message" role="status">{message}</p>}
      {error && <p className="admin-error" role="alert">{error}</p>}
    </section>
  );
}
