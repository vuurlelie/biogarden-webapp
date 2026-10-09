let flushCheckout: (() => Promise<boolean>) | undefined;

export function registerCheckoutFlush(flush: () => Promise<boolean>): () => void {
  flushCheckout = flush;
  return () => {
    if (flushCheckout === flush)
      flushCheckout = undefined;
  };
}

export async function prepareSessionTransition(): Promise<void> {
  if (flushCheckout && !await flushCheckout())
    throw new Error("A megadott rendelési adatok mentése nem sikerült. Kérjük, próbáld újra.");
}
