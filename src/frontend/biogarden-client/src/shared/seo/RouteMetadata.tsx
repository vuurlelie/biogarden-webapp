import { useLayoutEffect } from "react";
import { useLocation } from "react-router";
import { applyMetadata } from "./metadata";

const titles: Record<string, string> = {
  "/kosar": "Kosár", "/penztar": "Pénztár", "/rendeles-sikeres": "Rendelés visszaigazolása",
  "/bejelentkezes": "Bejelentkezés", "/regisztracio": "Regisztráció",
  "/email-megerosites": "Email megerősítés", "/elfelejtett-jelszo": "Elfelejtett jelszó",
  "/jelszo-visszaallitas": "Új jelszó", "/termekek": "Termékek", "/tudastar": "Tudástár",
};

export default function RouteMetadata() {
  const { pathname, search } = useLocation();
  useLayoutEffect(() => {
    // Reset before page-level effects, including while lazy pages/data are loading.
    // Unknown/private routes are noindex; public pages explicitly supply their own SEO.
    applyMetadata(
      { title: titles[pathname] ?? (pathname.startsWith("/admin") ? "Adminisztráció" :
        pathname.startsWith("/fiokom") ? "Fiókom" : undefined), noIndex: true },
      pathname
    );
  }, [pathname, search]);
  return null;
}
