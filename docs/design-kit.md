Below is a **copy‑paste, agent‑ready brief** that translates the research‑backed design kit into **non‑destructive, implementation rules** for the “Swing the Boogie” site. It is written so an automation agent can apply it **without changing functionality** (no class/ID renames, no behavioral changes), while raising visual quality, cohesion, and accessibility.

---

# ✅ Agent Brief: “Swing the Boogie” Research‑Backed Design System (No‑Code Implementation Rules)

**Objective**
Apply a consistent, research‑backed design system to the existing codebase (CSS + markup) **without breaking functionality**. Improve readability, accessibility, and polish in line with the client brief (vintage‑meets‑modern swing/jazz; black base, red highlights; high‑end events).

**Scope & guardrails (must follow)**

* **Do not** change class names, HTML structure, Razor/C# logic, JS behavior, routes, or data attributes.
* **Only** adjust **design tokens**, **per‑component styles**, and **semantic attributes** where noted (e.g., `aria-labels`) if they **don’t alter behavior**.
* **Keep all content and copy intact**; you may change **link labels** only where they are generic (e.g., “Learn more”) and a **more descriptive label already exists in adjacent text**. ([Nielsen Norman Group][1])
* **Maintain the site’s dark theme**; ensure all changes pass the contrast and interaction rules below (WCAG 2.2 AA). ([W3C][2])

---

## 1) Token & Color Rules (no functional changes)

**Do this in tokens only (no selector rewrites):** Use/keep the site’s variables in `:root` and **apply the pairings below** across components.

### 1.1 Required contrast thresholds (WCAG 2.2)

* Body text & icons: **≥ 4.5:1**; large (≥ 18 pt/24 px or 14 pt/18.66 px bold): **≥ 3:1**. Non‑text UI parts (borders, states) **≥ 3:1**. ([W3C][2])

**Key pairs already in the theme (computed ratios):**

* `#f5f5f5` on `#050505` → **17.7:1** ✅ (all text)
* `#c3c3ce` on `#050505` → **7.3:1** ✅ (secondary text)
* `#d7263d` on `#050505` → **4.11:1** ➜ **OK for large text/CTAs**, **not for body text**.
* `#d7263d` on `#111117` → **3.79:1** ➜ **large text only**.
* `#f4b942` on `#050505` → **11.5:1** ✅ (accent highlights, links on dark)
* `#f4b942` on `#111117` → **10.0:1** ✅ (accent highlights on elevated surfaces)

**Agent actions**

* Where **accent red** (`--color-accent: #d7263d`) is used on dark surfaces for **normal‑sized text**, **don’t** rely on color alone. Keep text in `--color-text-primary` or `--color-text-secondary` and use the red for **borders, rules, icons, buttons, or headings ≥ 24 px**. ([W3C][2])
* Prefer `--color-accent-soft: #f4b942` for **small‑text accents** (badges, pill labels) since it passes contrast comfortably on dark. ([W3C][3])

### 1.2 Links inside paragraphs (mandatory)

* Links **embedded in running text** must not depend **on color alone** for recognition. Ensure a **non‑color cue** (e.g., underline or weight change present at rest, not just on hover). This avoids **WCAG failure F73**. ([W3C][4])
* Replace vague link labels like **“Learn more/Read more/Click here”** with descriptive labels **already present** in adjacent copy (e.g., “**Book Your Event**”). **Do not invent new copy.** ([Nielsen Norman Group][1])

### 1.3 Dark theme considerations

* Maintain layered dark surfaces (`--color-background` → `--color-surface` → `--color-surface-elevated`) to give depth and avoid flat, low‑hierarchy dark UIs. NN/g highlights frequent **dark‑mode pitfalls** (poor hierarchy, low legibility) and recommends careful layered contrast.
* Respect user settings: keep dark theme as default, but **honor** `prefers-color-scheme` and `prefers-reduced-motion` (see §4). ([MDN Web Docs][5])

---

## 2) Typography & Readability

### 2.1 Hierarchy & scale

* Keep **Limelight** for display headings and **Libre Baskerville** for body (already used). Use **bold, geometric display** only for titles, not body copy. (Genre‑consistent and legible.)
* For reading comfort, body‑text line length **≈ 50–75 characters/line** (mobile 30–50, desktop 45–75) per aggregated research; enforce via max‑widths on text containers (no code examples here). ([Baymard Institute][6])

### 2.2 Text spacing (must support overrides)

* The layout must **not break** if users increase spacing to: line height **1.5×**, paragraph spacing **2× font size**, letter‑spacing **0.12em**, word‑spacing **0.16em** (WCAG 1.4.12). Avoid clipping/overlap under these conditions. ([W3C][7])

---

## 3) Spacing & Layout Rhythm

### 3.1 Grid & rhythm

* Standardize spacing on an **8‑point grid** across paddings/margins (8/16/24/32/40/48/56/64). This aligns with widely adopted design‑system conventions (Material). ([Material Design][8])

### 3.2 Touch target sizes

* Minimum **touch target**: **24×24 px** (WCAG 2.5.8 AA), preferably **44×44 pt** (Apple HIG) or **48×48 dp** (Material) for comfort. Do not shrink existing tap areas below these.

---

## 4) Interaction, Focus, and Motion

### 4.1 Focus visibility (keyboard)

* Every interactive element must have a **visible focus indicator** with **≥ 3:1 contrast** against adjacent pixels and an area **≥ 2px perimeter equivalent** (WCAG 2.4.13). If custom styling hides the default outline, add a clear custom focus ring. ([W3C][9])
* **Sticky headers/footers must not obscure focus** while tabbing. Use scroll padding or ensure partial visibility so focused elements aren’t fully hidden (WCAG 2.4.11). ([W3C][10])

### 4.2 Reduced motion

* Honor `prefers-reduced-motion`: if present, **suppress non‑essential animations/parallax** and minimize transitions to near‑instant. ([MDN Web Docs][11])

### 4.3 Bypass repeated content

* Keep the **Skip to content** link operable and visible on focus to meet **Bypass Blocks** (WCAG 2.4.1). ([W3C][12])

---

## 5) Content & Calls to Action

### 5.1 Link/CTA wording (no new copy creation)

* Replace generic labels (“**Learn more**”) with **descriptive labels** already present nearby; this improves **information scent** and accessibility. Example approach: if a card title is “**Corporate Events**”, change its link label from “Learn more” to “**Corporate Events**” or “**Explore Corporate Events**” **only if that wording already exists** in the UI text. ([Nielsen Norman Group][1])

### 5.2 Icons must have labels

* Don’t rely on icons alone—ensure text labels or accessible names are present (improves discoverability/scanability). ([Nielsen Norman Group][13])

---

## 6) Media (Imagery & Video) with Performance in mind

### 6.1 Images

* Use **responsive images** patterns so the browser can choose correctly sized assets; ensure width/density candidates are available and the browser can select based on the rendered size. (Don’t embed oversized hero images.) ([web.dev][14])
* Lazy‑load **non‑critical, below‑the‑fold** imagery; **do not** lazy‑load **initial viewport** LCP image. ([web.dev][15])

### 6.2 Video

* For embedded highlight reels:

  * Provide **captions** for prerecorded video (WCAG 1.2.2). ([W3C][16])
  * Provide **audio control** for any autoplaying audio (WCAG 1.4.2) and ensure users can **pause/stop/hide** moving content that starts automatically (WCAG 2.2.2). ([W3C][17])
  * Lazy‑load non‑critical videos and optimize codecs/containers; this helps LCP/INP. ([web.dev][18])

---

## 7) Core Web Vitals (non‑functional, perf‑minded adjustments)

Target **good** thresholds at the 75th percentile: **LCP ≤ 2.5 s**, **INP ≤ 200 ms**, **CLS ≤ 0.1**. Prioritize hero media optimization, responsive imagery, stable dimensions for media/cards, and defer non‑critical assets. ([Google Help][19])

---

## 8) Brand & Style (Jazz Age fit, evidence‑based)

* The client’s “vintage‑meets‑modern swing/jazz” brief pairs well with **Art Deco‑influenced geometry and typography accents** (as light, decorative motifs only—rules, dividers, badges). This aesthetic is historically associated with the **Jazz Age** in design and decorative arts; use **sparingly** to avoid pastiche. ([Cleveland Museum of Art][20])
* Maintain a **modern layout** with **clean, modular typography** (International/Swiss influence) and use Deco‑style **caps, rules, and geometric separators** only as subtle accents, not as functional controls (so we don’t harm usability).

---

## 9) Component‑level application (work from top to bottom)

> Apply the following **without altering structure/behavior**—only adjust tokens, states, and accessible names.

**Header / Navigation**

* Keep sticky header; ensure **focus is never obscured**; if needed, add scroll padding so focused items remain visible. ([W3C][10])
* Ensure **44×44 px** (preferred) targets for hamburger, nav links, and header CTA; never smaller than **24×24 px**.
* Active/hover/focus states must show **non‑text contrast ≥ 3:1** and a **visible focus ring**. ([W3C][3])

**Hero**

* Keep display face (Limelight) for headlines; **don’t** use it for body copy.
* Ensure hero lead text max line length ≈ 50–75 CPL; if hero features an image, ensure LCP asset is **not lazy‑loaded**. ([web.dev][21])

**Value cards / service cards / profile cards**

* Preserve existing large radii (24–32px) as modern luxe; ensure card borders or shadows provide **≥ 3:1** contrast vs surrounding surfaces (non‑text contrast). ([W3C][3])
* Buttons inside cards: maintain **descriptive labels** (see §5.1). ([Nielsen Norman Group][1])

**Links in running text**

* Add **non‑color cue** (underline or weight) so links are evident at rest. Avoid only‑color distinctions → prevents **F73**. ([W3C][4])

**Forms (Contact)**

* Keep explicit labels (don’t replace with placeholders). Ensure focus ring ≥ 2px, **≥ 3:1** against adjacent pixels. Inputs/controls meet size rules. ([design-system.service.gov.uk][22])

**Media (Gallery/Video)**

* Provide captions for prerecorded video; ensure user can pause/stop moving content and control audio. ([W3C][16])
* Use responsive images guidance; lazy‑load below‑the‑fold. ([web.dev][14])

**Footer**

* Link text meets contrast and information‑scent rules (no generic “Learn more”). ([Nielsen Norman Group][1])

---

## 10) Accessibility & UX acceptance criteria (AA)

**Contrast & text**

* All text/interactive elements meet **1.4.3**; all UI parts meet **1.4.11**. **Text spacing overrides** do not break layout (1.4.12). ([W3C][2])

**Navigation & focus**

* **Skip link** works and is visible on focus (2.4.1). **Focus** is always visible (2.4.13) and not obscured by sticky layers (2.4.11). ([W3C][12])

**Links**

* In‑text links are identifiable **without relying on color alone**; no F73 failures. ([W3C][4])

**Targets & motion**

* Touch targets **≥ 24×24** (AA), ideally **44×44**/ **48×48** per platform guidance. Motion is reduced when the user requests it. ([MDN Web Docs][11])

**Media**

* Captions (1.2.2), audio controls (1.4.2), pause/stop/hide (2.2.2) are available. ([W3C][16])

---

## 11) Performance acceptance criteria (Core Web Vitals)

* 75th percentile: **LCP ≤ 2.5 s**, **INP ≤ 200 ms**, **CLS ≤ 0.1**. Optimize hero media, reserve space for images/video to avoid layout shifts, lazy‑load below‑the‑fold. ([Google Help][19])

---

## 12) Step‑by‑step execution plan (for the agent)

1. **Pre‑flight**

   * Snapshot Lighthouse + CWV metrics and an a11y scan (axe or equivalent) on **Home, About, Services, Media, Contact** for before/after comparison. *(Tool choice up to you.)* ([Google Help][19])

2. **Token pass (centralized)**

   * Keep existing color tokens; **only** adjust usage per pairings in **§1.1** and link rules **§1.2**. Ensure **accent red** appears on large text/controls; use **gold** for small‑text accents when necessary to meet contrast. ([W3C][2])

3. **Typography pass**

   * Enforce heading/display vs body roles; tune max text widths to achieve **50–75 CPL** across content blocks. ([Baymard Institute][6])

4. **Spacing/targets pass**

   * Normalize paddings/margins to 8‑pt increments; verify all tappable elements meet **≥ 24×24 px** minimum, aiming for **44–48** where feasible.

5. **Link/CTA pass**

   * Ensure in‑paragraph links have a **non‑color cue** at rest; change generic labels to **descriptive** labels found **in existing text** only. ([W3C][4])

6. **Focus & sticky layers pass**

   * Apply visible focus indicators to all interactives; confirm sticky header/footer **does not obscure focus** while tabbing. ([W3C][9])

7. **Media pass**

   * Ensure responsive image patterns for heavy images; **don’t** lazy‑load the LCP image; lazy‑load below‑fold media; add captions to prerecorded video and audio controls. ([web.dev][14])

8. **Reduced‑motion pass**

   * Respect `prefers-reduced-motion` by suppressing decorative animations. ([MDN Web Docs][11])

9. **Verification**

   * Re‑run a11y checks (contrast, F73, focus, text spacing), and Lighthouse/CWV on the same pages. Confirm thresholds in **§10–11** are met or improved. ([Google Help][19])

10. **Deliverables**

* A short “change log” listing: token usage adjustments, link labeling fixes (before→after), focus style confirmation, media optimizations performed, and before/after scan summaries.

---

## 13) Rationale highlights (why these rules)

* **Contrast & link affordance** are top predictors of legibility and wayfinding; WCAG 2.2 sets explicit thresholds, and **links in text must not rely on color alone** (avoid F73). ([W3C][2])
* Users **scan** and follow strong **information scent**; replacing generic CTAs improves clarity and conversion. ([Nielsen Norman Group][23])
* **Dark‑mode pitfalls** (poor tonal hierarchy) are common; layered surfaces and careful contrast mitigate them.
* **Text spacing overrides** and **touch target sizes** are essential AA requirements that prevent breakage for many users. ([W3C][7])
* **Core Web Vitals** thresholds correlate with perceived quality; responsive media and prudent lazy‑loading improve LCP/CLS/INP. ([web.dev][24])
* The **Jazz Age ↔ Art Deco** connection is well‑documented in museum scholarship; using Deco‑influenced accents (sparingly) aligns the brand with the brief without harming usability. ([Cleveland Museum of Art][20])

---

This brief gives you everything needed to **systematically** apply a **research‑backed** design system to the existing site, **without changing functionality**—and with clear, checkable acceptance criteria.

[1]: https://www.nngroup.com/articles/learn-more-links/?utm_source=chatgpt.com "“Learn More” Links: You Can Do Better"
[2]: https://www.w3.org/TR/WCAG22/?utm_source=chatgpt.com "Web Content Accessibility Guidelines (WCAG) 2.2 - W3C"
[3]: https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html?utm_source=chatgpt.com "Understanding Success Criterion 1.4.11: Non-text Contrast"
[4]: https://www.w3.org/WAI/WCAG21/Techniques/failures/F73?utm_source=chatgpt.com "F73: Failure of Success Criterion 1.4.1 due to creating links ..."
[5]: https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Sec-CH-Prefers-Color-Scheme?utm_source=chatgpt.com "Sec-CH-Prefers-Color-Scheme header - HTTP - MDN Web Docs"
[6]: https://baymard.com/blog/line-length-readability?utm_source=chatgpt.com "Readability: The Optimal Line Length"
[7]: https://www.w3.org/WAI/WCAG22/Understanding/text-spacing.html "Understanding Success Criterion 1.4.12: Text Spacing | WAI | W3C"
[8]: https://m2.material.io/design/layout/understanding-layout.html?utm_source=chatgpt.com "Understanding layout - Material Design"
[9]: https://www.w3.org/WAI/WCAG22/Understanding/focus-appearance "Understanding Success Criterion 2.4.13: Focus Appearance | WAI | W3C"
[10]: https://www.w3.org/WAI/WCAG22/Understanding/focus-not-obscured-minimum.html "Understanding Success Criterion 2.4.11: Focus Not Obscured (Minimum) | WAI | W3C"
[11]: https://developer.mozilla.org/en-US/docs/Web/CSS/%40media/prefers-color-scheme?utm_source=chatgpt.com "prefers-color-scheme - MDN - Mozilla"
[12]: https://www.w3.org/WAI/WCAG21/Understanding/bypass-blocks.html "Understanding Success Criterion 2.4.1: Bypass Blocks | WAI | W3C"
[13]: https://www.nngroup.com/articles/how-to-test-digital-icons/?utm_source=chatgpt.com "Icon Usability: When and How to Evaluate Digital Icons"
[14]: https://web.dev/learn/images/responsive-images?utm_source=chatgpt.com "Responsive images"
[15]: https://web.dev/learn/performance/lazy-load-images-and-iframe-elements?utm_source=chatgpt.com "Lazy load images and <iframe> elements"
[16]: https://www.w3.org/WAI/WCAG21/Understanding/captions-prerecorded.html?utm_source=chatgpt.com "Understanding SC 1.2.2: Captions (Prerecorded) (Level A)"
[17]: https://www.w3.org/WAI/WCAG21/Understanding/audio-control.html?utm_source=chatgpt.com "Understanding Success Criterion 1.4.2: Audio Control | WAI"
[18]: https://web.dev/articles/lazy-loading-video?utm_source=chatgpt.com "Lazy loading video | Articles"
[19]: https://support.google.com/webmasters/answer/9205520?hl=en "Core Web Vitals report - Search Console Help"
[20]: https://www.clevelandart.org/about/press/cleveland-museum-art-presents-jazz-age-american-style-1920s?utm_source=chatgpt.com "The Jazz Age: American Style in the 1920s"
[21]: https://web.dev/articles/lcp-lazy-loading?utm_source=chatgpt.com "The performance effects of too much lazy loading | Articles"
[22]: https://design-system.service.gov.uk/components/text-input/?utm_source=chatgpt.com "Text input"
[23]: https://www.nngroup.com/articles/information-scent/?utm_source=chatgpt.com "Information Scent: How Users Decide Where to Go Next"
[24]: https://web.dev/articles/defining-core-web-vitals-thresholds "How the Core Web Vitals metrics thresholds were defined  |  Articles  |  web.dev"
