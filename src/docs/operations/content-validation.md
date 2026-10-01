# Article and image validation

## Articles

The API sanitizes article HTML on create/update and on every article DTO read, including older database records. Legacy rows are not rewritten automatically. This protects public detail/list responses and the admin editor without a migration.

The supported subset matches the rich-text editor: headings, paragraphs, line breaks, emphasis, underline/strike, lists, quotations, code, horizontal rules and links. Inline styling is limited to text alignment, font family, font size and colour. Links may use HTTP, HTTPS, mailto or relative URLs; they open in the current tab. Scripts, embedded media, SVG, event handlers, arbitrary classes/IDs and other styles are removed by the parser-based HtmlSanitizer library.

New article HTML is limited to 100,000 characters before parsing and must contain at least 20 non-whitespace visible text characters after sanitization. Tag names, script contents and invisible formatting characters do not satisfy that minimum. Sanitization may normalize the spelling of CSS colours and HTML entities; this is expected.

After deployment, open an existing formatted article and create/edit a draft with headings, lists, links, font settings and colours. Confirm that preview and published output preserve the supported formatting. Content outside the supported subset will disappear from the editor and will be removed permanently when saved; preserve a database backup before editing legacy content.
