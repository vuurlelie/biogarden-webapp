import { useEffect } from "react";
import { useLocation } from "react-router";
import { applyMetadata, type SeoProps } from "./metadata";

export default function Seo(props: SeoProps) {
  const location = useLocation();
  useEffect(() => {
    applyMetadata(props, `${location.pathname}${location.search}`);
  }, [props, location.pathname, location.search]);
  return null;
}
