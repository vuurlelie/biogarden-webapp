import { lazy, Suspense, useEffect, useState, type MouseEvent, type ReactNode } from "react";
import { NavLink, useLocation } from "react-router-dom";
import { ROUTES } from "../../constants/routes";

const mapSource =
  "https://www.google.com/maps?q=Bakonyszentl%C3%A1szl%C3%B3%2C%208431&z=10&output=embed";
const ContactForm = lazy(() => import("../../../features/contact/components/ContactForm"));

export default function Footer() {
  const { pathname } = useLocation();
  const [isContactModalOpen, setIsContactModalOpen] = useState(false);

  useEffect(() => {
    if (!isContactModalOpen) {
      return;
    }

    function closeOnEscape(event: KeyboardEvent) {
      if (event.key === "Escape") {
        setIsContactModalOpen(false);
      }
    }

    document.addEventListener("keydown", closeOnEscape);
    document.body.style.overflow = "hidden";

    return () => {
      document.removeEventListener("keydown", closeOnEscape);
      document.body.style.overflow = "";
    };
  }, [isContactModalOpen]);

  function scrollToTopOnCurrentPage(targetPath: string) {
    return (event: MouseEvent<HTMLAnchorElement>) => {
      if (pathname !== targetPath) return;

      event.preventDefault();
      window.scrollTo({ top: 0, behavior: "smooth" });
    };
  }

  return (
    <>
      <footer
        id="contact"
        className="relative z-10 w-full border-t border-site-footer-border bg-site-footer px-4 py-10 shadow-[0_14px_24px_-18px_rgba(31,41,27,0.75)] sm:px-6 lg:px-8"
      >
        <div className="mx-auto grid w-full max-w-6xl gap-10 lg:grid-cols-2 lg:items-start">
          <section>
            <h2 className="text-center text-sm font-bold uppercase tracking-wider text-site-heading md:text-left">
              Itt találsz minket
            </h2>

            <div className="mt-6 grid items-start gap-12 sm:grid-cols-[auto_minmax(0,22rem)] sm:justify-start">
              <div className="flex items-center justify-center gap-4 sm:flex-col sm:items-start">
                <a
                  href="https://www.facebook.com/profile.php?id=100083082863561"
                  target="_blank"
                  rel="noreferrer"
                  className="inline-flex h-11 w-11 items-center justify-center rounded-full bg-[#3b5998] text-xl font-bold text-white shadow transition hover:scale-105 hover:brightness-95 focus:outline-none focus:ring-2 focus:ring-site-green focus:ring-offset-2"
                  aria-label="Facebook oldal"
                >
                  f
                </a>
                <a
                  href="https://www.instagram.com/bakony.kincse.biokerteszet/"
                  target="_blank"
                  rel="noreferrer"
                  className="inline-flex h-11 w-11 items-center justify-center rounded-full bg-[radial-gradient(circle_at_30%_110%,#feda75_0%,#fa7e1e_28%,#d62976_52%,#962fbf_75%,#4f5bd5_100%)] text-white shadow transition hover:scale-105 hover:brightness-95 focus:outline-none focus:ring-2 focus:ring-site-green focus:ring-offset-2"
                  aria-label="Instagram oldal"
                >
                  <InstagramIcon />
                </a>
              </div>

              <iframe
                title="Biogarden location - Bakonyszentlászló"
                className="h-52 w-full rounded-sm border border-site-border shadow-sm sm:h-56"
                src={mapSource}
                loading="lazy"
                referrerPolicy="no-referrer-when-downgrade"
                allowFullScreen
              />
            </div>
          </section>

          <div className="grid gap-8 md:grid-cols-2">
            <section>
              <h2 className="mb-5 text-center text-sm font-bold uppercase tracking-wider text-site-heading md:text-left">
                Kapcsolat
              </h2>

              <div className="space-y-5 text-site-text">
                <FooterInfo icon={<UserIcon />}>Volf Gábor</FooterInfo>
                <FooterInfo icon={<HomeIcon />} alignTop>
                  8431 Bakonyszentlászló, Jókai utca 22.
                </FooterInfo>
                <FooterInfo icon={<MailIcon />}>
                  <a
                    href="mailto:info@bakonykincse.hu"
                    className="font-semibold text-site-green-dark underline underline-offset-4 transition hover:text-site-green-hover"
                  >
                    info@bakonykincse.hu
                  </a>
                </FooterInfo>
                <FooterInfo icon={<PhoneIcon />}>
                  <a
                    href="tel:+36208005606"
                    className="font-semibold text-site-green-dark underline underline-offset-4 transition hover:text-site-green-hover"
                  >
                    +36 20 800 56 06
                  </a>
                </FooterInfo>
                <div className="pt-2">
                  <FooterInfo icon={<MessageIcon />}>
                    <button
                      type="button"
                      onClick={() => setIsContactModalOpen(true)}
                      className="font-semibold text-site-green-dark underline underline-offset-4 transition hover:text-site-green-hover"
                    >
                      Írj nekünk itt
                    </button>
                  </FooterInfo>
                </div>
              </div>
            </section>

            <div className="space-y-8">
              <section>
                <h2 className="mb-5 text-center text-sm font-bold uppercase tracking-wider text-site-heading md:text-left">
                  Hasznos információk
                </h2>

                <div className="space-y-3 text-center text-site-text md:text-left">
                  <NavLink
                    to={ROUTES.privacyPolicy}
                    onClick={scrollToTopOnCurrentPage(ROUTES.privacyPolicy)}
                    className="block font-semibold text-site-green-dark underline underline-offset-4 transition hover:text-site-green-hover"
                  >
                    Adatkezelési tájékoztató
                  </NavLink>
                  <NavLink
                    to={ROUTES.terms}
                    onClick={scrollToTopOnCurrentPage(ROUTES.terms)}
                    className="block font-semibold text-site-green-dark underline underline-offset-4 transition hover:text-site-green-hover"
                  >
                    Általános Szerződési Feltételek
                  </NavLink>
                  <NavLink
                    to={ROUTES.orderWithdrawalRequest}
                    onClick={scrollToTopOnCurrentPage(ROUTES.orderWithdrawalRequest)}
                    className="block font-semibold text-site-green-dark underline underline-offset-4 transition hover:text-site-green-hover"
                  >
                    Elállási vagy fogyasztói kérés
                  </NavLink>
                </div>
              </section>

              <section>
                <h2 className="mb-5 text-center text-sm font-bold uppercase tracking-wider text-site-heading md:text-left">
                  Nyitvatartás
                </h2>

                <div className="text-center leading-relaxed text-site-text md:text-left">
                  <div>Hétfő - Péntek: 8:00 - 16:00</div>
                  <div className="mt-2 text-xs">
                    Birtoklátogatás előtt kérjük, egyeztess velünk a megadott elérhetőségeink egyikén.
                  </div>
                  <div className="mt-2 text-xl" aria-hidden="true">
                    🌱 😊
                  </div>
                </div>
              </section>
            </div>
          </div>
        </div>
      </footer>

      <div className="border-t border-site-border bg-site-green-dark p-4 text-center text-white/90 shadow-md">
        <div>©{new Date().getFullYear()} Bakony Kincse Biokertészet - Minden jog fenntartva.</div>
        <div className="text-sm">
          Készítette:{" "}
          <a
            className="font-semibold underline underline-offset-4 transition hover:text-white"
            href="https://www.facebook.com/profile.php?id=100012648488859"
            target="_blank"
            rel="noreferrer"
          >
            Rácz Krisztina
          </a>
        </div>
      </div>

      {isContactModalOpen && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/45 px-4 py-6"
          role="dialog"
          aria-modal="true"
          aria-labelledby="contact-modal-title"
          onMouseDown={(event) => {
            if (event.target === event.currentTarget) {
              setIsContactModalOpen(false);
            }
          }}
        >
          <div className="relative max-h-[90vh] w-full max-w-3xl overflow-y-auto">
            <button
              type="button"
              onClick={() => setIsContactModalOpen(false)}
              className="absolute right-3 top-3 z-10 inline-flex h-9 w-9 items-center justify-center rounded-full bg-white/80 text-2xl leading-none text-site-heading shadow-sm transition hover:bg-site-surface-soft focus:outline-none focus:ring-2 focus:ring-site-green"
              aria-label="Bezárás"
            >
              ×
            </button>
            <Suspense
              fallback={
                <div className="rounded-sm bg-white p-10 pt-12 text-center text-site-muted shadow-2xl">
                  Kapcsolati űrlap betöltése...
                </div>
              }
            >
              <ContactForm
                className="pt-12 shadow-2xl"
                surfaceClassName="bg-white"
                titleId="contact-modal-title"
              />
            </Suspense>
          </div>
        </div>
      )}
    </>
  );
}

type FooterInfoProps = {
  children: ReactNode;
  icon: ReactNode;
  alignTop?: boolean;
};

function FooterInfo({ children, icon, alignTop = false }: FooterInfoProps) {
  return (
    <p className={`flex justify-center gap-3 md:justify-start ${alignTop ? "items-start" : "items-center"}`}>
      <span className={`${alignTop ? "mt-0.5" : ""} shrink-0 text-site-text`}>{icon}</span>
      <span>{children}</span>
    </p>
  );
}

function UserIcon() {
  return (
    <svg className="h-5 w-5" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
      <path d="M12 12a4 4 0 1 0-4-4 4 4 0 0 0 4 4Zm0 2c-3.3 0-6 1.8-6 4v1h12v-1c0-2.2-2.7-4-6-4Z" />
    </svg>
  );
}

function HomeIcon() {
  return (
    <svg className="h-5 w-5" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
      <path d="M12 3 3 10.5l1.4 1.5L6 10.7V21h5v-6h2v6h5V10.7l1.6 1.3 1.4-1.5L12 3Z" />
    </svg>
  );
}

function MailIcon() {
  return (
    <svg className="h-5 w-5" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
      <path d="M4 5h16a2 2 0 0 1 2 2v.4l-10 6.1L2 7.4V7a2 2 0 0 1 2-2Zm-2 5 9 5.5a2 2 0 0 0 2 0L22 10v7a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2v-7Z" />
    </svg>
  );
}

function PhoneIcon() {
  return (
    <svg className="h-5 w-5" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
      <path d="M6.6 10.8a15.2 15.2 0 0 0 6.6 6.6l2.2-2.2a1 1 0 0 1 1-.25 11.6 11.6 0 0 0 3.6.55 1 1 0 0 1 1 1V20a1 1 0 0 1-1 1A17 17 0 0 1 3 4a1 1 0 0 1 1-1h3.5a1 1 0 0 1 1 1 11.6 11.6 0 0 0 .55 3.6 1 1 0 0 1-.25 1l-2.2 2.2Z" />
    </svg>
  );
}

function MessageIcon() {
  return (
    <svg className="h-5 w-5" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
      <path d="M4.804 21.644A6.707 6.707 0 0 0 6 21.75a6.721 6.721 0 0 0 3.583-1.029A10.27 10.27 0 0 0 12 21c5.322 0 9.75-3.97 9.75-9S17.322 3 12 3s-9.75 3.97-9.75 9c0 2.409 1.025 4.587 2.674 6.16.167.159.243.389.196.615a5.63 5.63 0 0 1-.316.869ZM8.25 12.75a.75.75 0 1 0 0-1.5.75.75 0 0 0 0 1.5Zm3.75 0a.75.75 0 1 0 0-1.5.75.75 0 0 0 0 1.5Zm3.75 0a.75.75 0 1 0 0-1.5.75.75 0 0 0 0 1.5Z" />
    </svg>
  );
}

function InstagramIcon() {
  return (
    <svg className="h-6 w-6" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <rect x="5" y="5" width="14" height="14" rx="4" stroke="currentColor" strokeWidth="2" />
      <circle cx="12" cy="12" r="3" stroke="currentColor" strokeWidth="2" />
      <circle cx="16.5" cy="7.5" r="1" fill="currentColor" />
    </svg>
  );
}
