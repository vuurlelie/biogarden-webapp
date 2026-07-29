type FacebookPostActionsProps = {
  postPath: string;
  title: string;
  compact?: boolean;
};

function createPostUrl(postPath: string) {
  return new URL(postPath, window.location.origin).toString();
}

function createShareUrl(postUrl: string) {
  const shareUrl = new URL("https://www.facebook.com/sharer/sharer.php");
  shareUrl.searchParams.set("u", postUrl);

  return shareUrl.toString();
}

export default function FacebookPostActions({
  postPath,
  title,
  compact = false,
}: FacebookPostActionsProps) {
  const postUrl = createPostUrl(postPath);
  const shareUrl = createShareUrl(postUrl);
  const buttonSize = compact ? "px-3 py-1.5 text-xs" : "px-4 py-2 text-sm";
  const groupAlignment = compact ? "" : "justify-center";

  return (
    <section
      className={`flex flex-wrap items-center gap-2 ${groupAlignment}`}
      aria-label={`Facebook megosztási lehetőségek: ${title}`}
    >
      <a
        href={shareUrl}
        target="_blank"
        rel="noreferrer"
        className={`inline-flex items-center gap-2 rounded-sm border border-[#31589b] bg-[#4267B2] ${buttonSize} font-bold text-white shadow-sm transition hover:bg-[#365899] focus:outline-none focus:ring-2 focus:ring-[#4267B2]/40 focus:ring-offset-2`}
        aria-label={`Bejegyzés megosztása Facebookon: ${title}`}
        title="Megosztás Facebookon"
      >
        <span aria-hidden="true" className="text-base leading-none">f</span>
        <span>Megosztás</span>
      </a>
    </section>
  );
}
