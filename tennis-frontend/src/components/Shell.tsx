import { NavLink, Outlet, useLocation } from "react-router-dom";

const links = [
  { to: "/", label: "Dashboard", end: true },
  { to: "/players", label: "Players", end: false },
  { to: "/matches", label: "Matches", end: false },
  { to: "/tournaments", label: "Tournaments", end: false },
  { to: "/imports", label: "Data Imports", end: false },
  { to: "/settings", label: "Settings", end: false },
];

const later = ["Pre-Match Evaluator", "Live Evaluator", "Odds Analysis", "Statistical Models"];

const titles: Record<string, string> = {
  "/": "Dashboard",
  "/players": "Players",
  "/matches": "Matches",
  "/tournaments": "Tournaments",
  "/imports": "Data Imports",
  "/settings": "Settings",
};

export function Shell() {
  const location = useLocation();
  const section = Object.keys(titles).find((path) => (path === "/" ? location.pathname === "/" : location.pathname.startsWith(path)));
  const title = section ? titles[section] : "Tennis archive";

  return (
    <div className="shell">
      <aside className="sidebar">
        <p className="brand">Tennis archive</p>
        {links.map((link) => (
          <NavLink key={link.to} to={link.to} end={link.end} className={({ isActive }) => (isActive ? "nav-link active" : "nav-link")}>
            {link.label}
          </NavLink>
        ))}
        <p className="later-label">Later</p>
        {later.map((label) => (
          <span key={label} className="nav-later" aria-disabled="true">
            {label}
          </span>
        ))}
      </aside>
      <div className="main">
        <header className="topbar">
          <h1>{title}</h1>
          <span className="muted">ATP seasons 2021–2026</span>
        </header>
        <main className="content">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
