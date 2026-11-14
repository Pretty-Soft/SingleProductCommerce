import { FormEvent, useEffect, useState } from "react";
import { adminRequest } from "./adminApi";

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
  attributeGroups: []
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
      setMessage("Product, variants, and attributes saved.");
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Product settings could not be saved.");
    } finally {
      setBusy(false);
    }
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
