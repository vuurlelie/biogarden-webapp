import { useEffect, useRef, useState } from "react";

export default function BackToTopBtn() {
  const [isVisible, setIsVisible] = useState(false);
  const [overDarkFooter, setOverDarkFooter] = useState(false);
  const buttonRef = useRef<HTMLButtonElement>(null);

  useEffect(
    () => {
      const onScroll = () => {
        const scrollHeight = window.scrollY || document.documentElement.scrollTop;
        setIsVisible(scrollHeight > 30);
        const button = buttonRef.current?.getBoundingClientRect();
        const footer = document.getElementById("footer-credits")?.getBoundingClientRect();
        setOverDarkFooter(Boolean(button && footer && button.bottom > footer.top &&
          button.top < footer.bottom && button.right > footer.left && button.left < footer.right));
      };
      window.addEventListener(
        "scroll",
        onScroll,
        { passive: true }
      );
      window.addEventListener("resize", onScroll);
      const observer = new ResizeObserver(onScroll);
      observer.observe(document.body);
      onScroll();
      return () => {
        window.removeEventListener("scroll", onScroll);
        window.removeEventListener("resize", onScroll);
        observer.disconnect();
      };
    },
    [isVisible]
  );

  const backToTop = () => window.scrollTo({ top: 0, behavior: "smooth" });

  if (!isVisible)
    return null;

  return (
    <button
      ref={buttonRef}
      type="button"
      onClick={backToTop}
      aria-label="Vissza az oldal tetejére"
      title="Vissza az oldal tetejére"
      className={`fixed bottom-5 right-5 z-20 flex h-11 w-11 items-center justify-center rounded-full shadow-md transition-colors
                 duration-150 ease-in-out hover:shadow-lg focus-visible:outline-2 focus-visible:outline-offset-2 ${
                   overDarkFooter
                     ? "bg-white text-site-green-hover hover:bg-site-surface-soft active:bg-site-surface-soft focus-visible:outline-white"
                     : "bg-site-green text-white hover:bg-site-green-hover active:bg-site-green-hover focus-visible:outline-site-heading"
                 }`}
    >
      <svg
        aria-hidden="true"
        xmlns="http://www.w3.org/2000/svg"
        viewBox="0 0 24 24"
        strokeWidth={3}
        stroke="currentColor"
        fill="none"
        className="w-4 h-4"
      >
        <path strokeLinecap="round" strokeLinejoin="round" d="M4.5 10.5 12 3l7.5 7.5M12 3v18" />
      </svg>
    </button>
  );
}
