import { useEffect } from "react";
import { useLocation } from "react-router";
import { useAnalytics } from "../context/AnalyticsContextCore";

export default function RouteAnalytics() {
  const location = useLocation();
  const { analyticsEnabled, track } = useAnalytics();

  useEffect(
    () => {
      if (!analyticsEnabled)
        return;
      track(
        "page_view",
        { path: location.pathname }
      );
    },
    [analyticsEnabled, location.pathname, track]
  );

  return null;
}
