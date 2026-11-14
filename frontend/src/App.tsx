import AdminApp from "./AdminApp";

export default function App() {
  if (window.location.pathname.startsWith("/admin")) {
    return <AdminApp />;
  }

  return (
    <main className="page">
      <header className="topbar">
        <a className="brand" href="/" aria-label="OneCart Commerce home">
          <span className="brand-mark">O</span>
          <span>OneCart</span>
        </a>
        <span className="status">Storefront starter</span>
      </header>

      <section className="hero" aria-labelledby="hero-title">
        <p className="eyebrow">Single product commerce</p>
        <h1 id="hero-title">Your product story starts here.</h1>
        <p className="intro">
          This clean starter is ready for the approved product details,
          customer experience, and Cash on Delivery checkout flow.
        </p>
        <button type="button" disabled>
          Product setup coming next
        </button>
        <p className="note">
          Checkout is not available in this starter yet.
        </p>
      </section>

      <footer>OneCart Commerce · ASP.NET Core + React</footer>
    </main>
  );
}
