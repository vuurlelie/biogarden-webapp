# Article and image validation

## Articles

The API sanitizes article HTML on create/update and on every article DTO read, including older database records. Legacy rows are not rewritten automatically. This protects public detail/list responses and the admin editor without a migration.

The supported subset matches the rich-text editor: headings, paragraphs, line breaks, emphasis, underline/strike, lists, quotations, code, horizontal rules and links. Inline styling is limited to text alignment, font family, font size and colour. Links may use HTTP, HTTPS, mailto or relative URLs; they open in the current tab. Scripts, embedded media, SVG, event handlers, arbitrary classes/IDs and other styles are removed by the parser-based HtmlSanitizer library.

New article HTML is limited to 100,000 characters before parsing and must contain at least 20 non-whitespace visible text characters after sanitization. Tag names, script contents and invisible formatting characters do not satisfy that minimum. Sanitization may normalize the spelling of CSS colours and HTML entities; this is expected.

After deployment, open an existing formatted article and create/edit a draft with headings, lists, links, font settings and colours. Confirm that preview and published output preserve the supported formatting. Content outside the supported subset will disappear from the editor and will be removed permanently when saved; preserve a database backup before editing legacy content.

## Images

Product and article uploads share `ManagedImageStorage`. Only administrators can upload. The API accepts JPEG, PNG and WebP with matching filename extension, MIME type and file signature, then fully decodes the first frame with Magick.NET. It rejects decoding warnings/errors, limits the input and encoded output to 5 MiB, each dimension to 4096 pixels and total pixels to 16 million. The admin UI retains its stricter 2 MiB target after browser resizing.

Images are oriented from their metadata, stripped of profiles/metadata and re-encoded. Animation is not preserved. One upload is processed per API process at a time; another overlapping upload receives 409 and can be retried. The decoder has memory, pixel, frame-buffer, profile and disk limits. Input bytes are also counted independently of the declared upload length.

The application assigns a random filename. Client filenames never form filesystem paths. A temporary `.uploading` file in the managed directory is renamed only after encoding finishes; ASP.NET static files do not serve this extension. Failed writes clean up their temporary file. A process crash can leave a `.uploading` file, which may be removed during maintenance after stopping uploads. Symbolic links/junctions at the web root or within the managed uploads tree are rejected.

### Retention and cleanup

Replacing/deleting a post or product does **not** delete the physical image. A save response can be lost after a successful commit, or another admin can assign the same image while cleanup runs. Keeping immutable files avoids breaking either case. The old image DELETE endpoints return 409 with an explanation; current forms no longer send these cleanup requests.

This intentionally trades disk space for safe concurrent saves. Monitor storage consumption. There is no automatic orphan cleanup job in this release. To reclaim space, take a backup, pause all admin content writes/uploads on every API instance, compare candidate paths against **both** post and product image references (including retained/deleted records), exclude default images and recent/in-flight uploads, then remove only reviewed unreferenced files. Resume writes after the maintenance operation. Never delete files merely because they disappeared from one edit form.

### Deployment and verification

No database migration is required. Deploy the matching API and frontend. Magick.NET includes native runtime libraries: preserve the publish output's runtime assets and verify that the assigned SmarterASP process architecture can load them. Local Windows tests do not establish hosted compatibility. Upload one small JPEG, PNG and WebP on UAT and open each returned image before considering this release verified.

Back up and preserve `wwwroot/uploads/posts` and `wwwroot/uploads/products` across publish, rollback and redeployment. Do not synchronize these directories with deletion enabled or overwrite them with an empty build output. The app identity needs create/write/rename permissions there. Multiple API instances require the same durable upload storage. Keep `.uploading` files non-public if adding IIS MIME mappings or replacing the static-file configuration. Existing images are retained as-is; only newly uploaded images are decoded/re-encoded by this release.

Automated coverage includes HTML attack vectors and editor markup, real SQL/API article writes and legacy reads, multipart uploads for both image categories, public image retrieval, authentication/authorization, corrupt and renamed files, dimensions/pixel limits, actual-byte limits, metadata stripping, temporary-file visibility, failed writes, concurrent decoding and cancellation.

## Responsive images

New uploads retain their full re-encoded image and generate WebP variants at 480, 960 and 1600 pixels wide, capped at the source width without upscaling. Duplicate widths are generated once. The original filename uses `<id>-r<source-width>.<extension>`; its variants use `<id>-<width>w.webp`. The original is published last, after every variant succeeds. A failed operation removes its generated variants; a process crash can leave unreferenced files for reviewed maintenance.

Public product/article cards and detail images use responsive sources only for this new filename format. Legacy/default images keep their original URL; zoom keeps the full image. No database migration or automatic rewriting of existing uploads is required. Keep every variant with its original during backup, deployment and cleanup. A variant is referenced implicitly by its original: never classify it as orphaned solely because its filename is absent from SQL.

The admin browser rejects source files over 20 MiB before decoding and centre-crops without enlarging small images. Its processed JPEG limit remains 2 MiB; server validation remains authoritative. The server permits at most three additional variants per upload, each bounded by the existing output limit. Account for the additional storage in hosting capacity checks.

Server-named GUID uploads and their variants receive `Cache-Control: public,max-age=31536000,immutable`. Default/replaceable assets keep the normal static-file policy. Verify these headers on the actual hosting origin because IIS may serve files independently of ASP.NET Core. Emergency removal of an already-cached public image cannot retract browser copies; replacement must use a new filename.

## Bundled image optimisation

The homepage carousel/banner and about-page photos have checked-in WebP variants (768 pixels wide and full source width). Original JPEGs remain fallbacks. Regenerate these assets with `python scripts/images/generate_variants.py` using Pillow with WebP support; normal frontend builds need neither Python nor Pillow. Review visual quality after changing originals or encoder settings. The first carousel image remains eager and receives high fetch priority; other slides retain lazy loading.

For the seven current photos, original JPEGs total 1,984,193 bytes; full-width WebP variants total 1,318,424 bytes (about 34% smaller), and 768-pixel variants total 394,372 bytes (about 80% smaller). These are file-size comparisons, not measured page-load improvements. Browser selection depends on viewport and device pixel ratio. Generated asset filenames are content-hashed by Vite during production builds.
