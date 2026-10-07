import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";

type CarouselProps = {
  children: React.ReactNode[];
  auto?: boolean;
  interval?: number;
};

export default function Carousel({
  children,
  auto = false,
  interval = 5000,
}: CarouselProps) {
  const slides = useMemo(() => React.Children.toArray(children), [children]);
  const [current, setCurrent] = useState(0);
  const [paused, setPaused] = useState(false);
  const focusRef = useRef(false);
  const hoverRef = useRef(false);

  const previous = useCallback(() => {
    setCurrent((current) => (current === 0 ? slides.length - 1 : current - 1));
  }, [slides.length]);

  const next = useCallback(() => {
    setCurrent((current) => (current === slides.length - 1 ? 0 : current + 1));
  }, [slides.length]);

  useEffect(() => {
    if (!auto || paused || slides.length <= 1)
      return;

    const id = setInterval(() => {
      if (!hoverRef.current && !focusRef.current && !window.matchMedia("(prefers-reduced-motion: reduce)").matches) next();
    }, Math.max(2000, interval));
    return () => clearInterval(id);
  }, [auto, paused, next, interval, slides.length]);

  const playbackLabel = paused ? "Automatikus lejátszás folytatása" : "Automatikus lejátszás szüneteltetése";
  const startX = useRef<number | null>(null);

  const onTouchStart = (e: React.TouchEvent) => (startX.current = e.touches[0].clientX);

  const onTouchEnd = (e: React.TouchEvent) => {
    if (startX.current == null) return;
    const dx = e.changedTouches[0].clientX - startX.current;
    if (Math.abs(dx) > 40) {
      if (dx > 0) {
        previous();
      } else {
        next();
      }
    }
    startX.current = null;
  };

  return (
    <div
      className="relative overflow-hidden w-full" 
      onMouseEnter={() => (hoverRef.current = true)}
      onMouseLeave={() => (hoverRef.current = false)}
      onTouchStart={onTouchStart}
      onTouchEnd={onTouchEnd}
      role="region"
      aria-label="Kertészeti képek"
      onFocusCapture={() => { focusRef.current = true; }}
      onBlurCapture={event => { if (!event.currentTarget.contains(event.relatedTarget)) focusRef.current = false; }}
      onKeyDown={event => {
        if (event.key === "ArrowLeft") { event.preventDefault(); previous(); }
        if (event.key === "ArrowRight") { event.preventDefault(); next(); }
      }}
      aria-roledescription="carousel"
    >
      <div
        className="flex transition-transform ease-out duration-500"
        style={{ transform: `translateX(-${current * 100}%)` }}
      >
        {slides.map((node, i) => (
          <div key={i} className="min-w-full">
            {node}
          </div>
        ))}
      </div>

      <div className="absolute inset-0 flex items-center justify-between p-3 sm:p-4 pointer-events-none">
        <button
          onClick={previous}
          className="pointer-events-auto p-2 rounded-full shadow bg-white/85 text-site-heading hover:bg-white"
          aria-label="Előző dia"
        >
          <svg
            viewBox="0 0 24 24"
            className="w-5 h-5"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
          >
            <path strokeLinecap="round" strokeLinejoin="round" d="M15 19l-7-7 7-7" />
          </svg>
        </button>
        <button
          onClick={next}
          className="pointer-events-auto p-2 rounded-full shadow bg-white/85 text-site-heading hover:bg-white"
          aria-label="Következő dia"
        >
          <svg
            viewBox="0 0 24 24"
            className="w-5 h-5"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
          >
            <path strokeLinecap="round" strokeLinejoin="round" d="M9 5l7 7-7 7" />
          </svg>
        </button>
      </div>

      {auto && <button type="button" onClick={() => {
        if (paused) {
          // Explicit Play takes precedence over the hover/focus that activated this button.
          hoverRef.current = false;
          focusRef.current = false;
        }
        setPaused(!paused);
      }}
        aria-label={playbackLabel}
        title={playbackLabel}
        className="absolute right-3 top-3 flex h-11 w-11 items-center justify-center rounded-full bg-white/85 text-site-heading shadow hover:bg-white focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-site-heading">
        <svg viewBox="0 0 24 24" className="h-5 w-5" fill="currentColor" aria-hidden="true" focusable="false">
          {paused ? <path d="M8 5v14l11-7z" /> : <><rect x="6" y="5" width="4" height="14" rx="1" /><rect x="14" y="5" width="4" height="14" rx="1" /></>}
        </svg>
      </button>}
      <div className="absolute bottom-3 left-0 right-0">
        <div className="flex items-center justify-center gap-2">
          {slides.map((_, i) => (
            <button
              key={i}
              onClick={() => setCurrent(i)}
              aria-label={`Ugrás a(z) ${i + 1}. diára`}
              className={`transition-all w-1 h-1 rounded-full bg-white ${
                current === i ? "scale-110" : "opacity-60"
              }`}
            />
          ))}
        </div>
      </div>
    </div>
  );
}
