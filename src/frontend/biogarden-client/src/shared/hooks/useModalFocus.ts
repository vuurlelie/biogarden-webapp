import { useEffect, useRef, type RefObject } from "react";

const focusableSelector = [
  "a[href]",
  "button:not([disabled])",
  "textarea:not([disabled])",
  "input:not([disabled])",
  "select:not([disabled])",
  "[tabindex]:not([tabindex='-1'])",
].join(",");

type UseModalFocusOptions = {
  isOpen: boolean;
  onClose: () => void;
  returnFocusRef?: RefObject<HTMLElement | null>;
};

export function useModalFocus<T extends HTMLElement>(modalRef: RefObject<T | null>, { isOpen, onClose, returnFocusRef }: UseModalFocusOptions) {
  const onCloseRef = useRef(onClose);

  useEffect(
    () => {
      onCloseRef.current = onClose;
    },
    [onClose]
  );

  useEffect(() => {
    if (!isOpen) {
      return;
    }

    const previouslyFocused = document.activeElement instanceof HTMLElement
      ? document.activeElement
      : null;
    const returnFocusElement = returnFocusRef?.current;
    const modal = modalRef.current;
    if (!modal)
      return;

    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    // Disable background interaction without hiding the dialog's ancestors.
    const background: { element: HTMLElement; inert: boolean }[] = [];
    let branch: HTMLElement = modal.closest<HTMLElement>('[role="dialog"]') ?? modal;
    while (branch.parentElement) {
      for (const sibling of branch.parentElement.children) {
        if (sibling instanceof HTMLElement && sibling !== branch) {
          background.push({ element: sibling, inert: sibling.inert });
          sibling.setAttribute("inert", "");
        }
      }
      branch = branch.parentElement;
      if (branch === document.body)
        break;
    }

    const focusTimer = window.setTimeout(
      () => {
        const firstFocusable = getFocusableElements(modal)[0];
        (firstFocusable ?? modal)?.focus();
      },
      0
    );

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") {
        event.preventDefault();
        onCloseRef.current();
        return;
      }

      if (event.key !== "Tab") {
        return;
      }

      const focusableElements = getFocusableElements(modal);
      if (focusableElements.length === 0) {
        event.preventDefault();
        modal?.focus();
        return;
      }

      const firstElement = focusableElements[0];
      const lastElement = focusableElements[focusableElements.length - 1];

      if (event.shiftKey && (document.activeElement === firstElement || !focusableElements.includes(document.activeElement as HTMLElement))) {
        event.preventDefault();
        lastElement.focus();
      }

      if (!event.shiftKey && (document.activeElement === lastElement || !focusableElements.includes(document.activeElement as HTMLElement))) {
        event.preventDefault();
        firstElement.focus();
      }
    }

    document.addEventListener("keydown", handleKeyDown);

    return () => {
      window.clearTimeout(focusTimer);
      document.removeEventListener("keydown", handleKeyDown);
      for (const { element, inert } of background) element.toggleAttribute("inert", inert);
      document.body.style.overflow = previousOverflow;
      (returnFocusElement ?? previouslyFocused)?.focus();
    };
  }, [isOpen, modalRef, returnFocusRef]);
}

function getFocusableElements(container: HTMLElement | null) {
  if (!container) {
    return [];
  }

  return Array.from(container.querySelectorAll<HTMLElement>(focusableSelector))
    .filter((element) => !element.matches(":disabled") && element.tabIndex >= 0
      && !element.closest("[inert]") && element.getClientRects().length > 0
      && getComputedStyle(element).visibility !== "hidden");
}
