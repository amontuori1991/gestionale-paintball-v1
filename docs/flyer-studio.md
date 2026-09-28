# Flyer studio

Admin-only `/Volantini` is linked from the dashboard. The shared back button is
enabled for both Volantini and ProfiloAzienda. GET and export POST require the
Admin role; POST also requires antiforgery validation.

The renderer reads CompanyProfileService for every preview/export and always
uses `wwwroot/img/logo.gif`. Nothing is fetched from arbitrary external URLs.
The saved website generates a QR code. All populated company contacts are printed.
Company data cannot be overridden through posted form fields.

Customizable content: headline, subtitle, top label, promotional stripe, date or
occasion, details/conditions, call to action, and three palettes. Starter buttons
only populate editable text; no actual prices or event dates are invented.

The same Skia layout draws JPEG preview, 2480x3508 JPEG and one-page vector A4 PDF.
The logo remains raster. There is no bleed/CMYK conversion; professional print
requirements should be checked with the printer. Font files are bundled with
their SIL OFL licenses, avoiding dependence on installed server fonts.

Rendering fits text within bounded rectangles; content that cannot fit returns
an explicit error instead of silently cutting it off. No flyer content, generated
PDF or JPEG is stored in Neon or R2. This version does not save drafts.

Tests in PhotoAlbums.Checks cover authorization, validation, both export types,
resolution, all palettes and back buttons. `--flyer-only` runs native rendering
without a database (also exercised on Linux). Export fixtures under
`.codex-build/flyer-fixtures` use synthetic company data only.
