# Firm Scope
- Brand essence must feel energetic, stylish, and vintage-meets-modern swing; use a black base with bold red accents, confident/fun copy, and emphasize the flexible 1–10 piece lineup led by Gilbert Ernest.
- Site operates as a marketing funnel for corporate events, weddings, and premium private functions, using supplied photo/video assets and any future reference examples (still pending). Bookings rely on a simple enquiry form—no availability calendar.
- Admin portal ships in MVP so internal staff can manage media assets, events, testimonials, and band lineup visibility. Admin UI prioritizes function over polish.
- All visitor-facing content is public. Authentication only protects the admin area and uses simple email/password accounts (no MFA).

## Required Pages & Features
- Home: Hero image of full band, tagline “The world-class swing band bringing the vibe to your event,” CTA “Book Your Event”; include value snapshot bullets (corporate events, weddings, flexibility, international experience) and embedded highlight-reel video section.
- About: Sections “Our Story” and “Why Choose Us” covering global performance history, 10-member flexibility, and professionalism/energy; blend group and solo imagery.
- Band Lineup: Profiles for all ten musicians (names + roles, placeholders until final copy) and clear messaging that configurations range from solo to full band.
- Services: Detail offerings for Corporate Events, Weddings, Private Functions plus “Flexible Packages” explaining Solo/Duo, 5-piece, 10-piece options; call out “Choose your band size.”
- Media (Gallery): Display curated photos and videos with a “Watch the Highlights” showcase. Videos play via HTML5 `<video>` using MP4 derivatives and admin-provided poster images.
- Testimonials: Surface at least three supplied quotes (wedding couple, corporate manager, private client) with attribution text.
- Events: Listing for upcoming public shows/festivals—structure must exist even if initially empty and editable via admin portal.
- Contact / Bookings: Intro copy inviting enquiries; form captures event type, event date, location, preferred band size, budget range, and optional message. Submissions persist to PostgreSQL and send notification emails through Amazon SES. Display direct contact details (manager name/phone/email) plus FAQ snippets (travel worldwide, AV provision, customizable band size).
- Footer: Tagline “Swing The Boogie – The heartbeat of your event.” with quick links (Home, About, Services, Media, Contact) and social icons for Instagram, YouTube, LinkedIn.

## Media & Content Management Expectations
- Photo assets: Initial set uploaded to S3 ahead of launch. Admin UI lets staff upload new photos, choose featured images, and toggle visibility.
- Video assets: Admins upload source files (MOV or MP4) and a poster image. Backend automatically transcodes MOV uploads to MP4 (H.264/AAC) for playback, stores both original and derivative in S3, and records duration/resolution/bitrate/poster metadata in PostgreSQL.
- Events and testimonials: Stored in the database; admins create/edit entries through the portal. No external calendar or feed integrations.
- Contact submissions: Persist in PostgreSQL for reference, trigger SES email notifications, and are deletable via admin UI if required by retention policy.

