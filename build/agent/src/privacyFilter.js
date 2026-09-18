// Privacy Filter & Masking Layer (spec section 2).
// Decides, BEFORE any screenshot or OCR happens, whether the current window is
// allowed to be captured at all. When excluded, the caller must not take a
// screenshot or run OCR — only the fact that something was filtered is logged.

function normalize(s) {
  return (s || '').toLowerCase();
}

export function createPrivacyFilter(config) {
  const excludedApps = (config.excluded_apps || []).map(normalize);
  const excludedTitleKeywords = (config.excluded_title_keywords || []).map(normalize);

  // כמו isExcluded, אבל מחזיר גם *למה* — איזו כניסה ב-excluded_apps/
  // excluded_title_keywords גרמה להחרגה. משמש את ה-briefing כדי להראות
  // בפועל אילו כללי פרטיות עשו עבודה היום, לא רק מספר סתמי.
  function checkExclusion(appName, windowTitle) {
    const app = normalize(appName);
    const title = normalize(windowTitle);
    const appMatch = excludedApps.find((needle) => app.includes(needle));
    if (appMatch) return { excluded: true, reason: `app:${appMatch}` };
    const titleMatch = excludedTitleKeywords.find((needle) => title.includes(needle));
    if (titleMatch) return { excluded: true, reason: `keyword:${titleMatch}` };
    return { excluded: false, reason: null };
  }

  function isExcluded(appName, windowTitle) {
    return checkExclusion(appName, windowTitle).excluded;
  }

  // Defense-in-depth redaction for any OCR text that does get through, in case
  // a sensitive field appears in an otherwise-allowed app (e.g. a credit card
  // number pasted into a chat window). Best-effort, not a guarantee.
  function redact(text) {
    if (!text) return text;
    return text
      // Credit/debit card numbers (13-19 digits, may have spaces or dashes)
      .replace(/\b(?:\d[ -]*?){13,19}\b/g, '[REDACTED-CARD]')
      // Israeli ID / 9-digit national IDs
      .replace(/\b\d{9}\b/g, '[REDACTED-ID]')
      // Email addresses
      .replace(/\b[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}\b/g, '[REDACTED-EMAIL]')
      // International phone numbers (+XX prefix or local 05X/03X/07X patterns)
      .replace(/\+\d[\d\s\-().]{7,16}\d/g, '[REDACTED-PHONE]')
      .replace(/\b0(?:5[0-9]|[23489])[0-9\-\s]{6,10}\b/g, '[REDACTED-PHONE]')
      // Common secret/password patterns in OCR text (e.g. "password: abc123")
      .replace(/(?:password|סיסמ[הא]|passwd|secret|token|api[_\s-]*key)\s*[:=]\s*\S+/gi, '[REDACTED-SECRET]')
      // Bearer tokens / JWTs (three base64url segments separated by dots)
      .replace(/ey[A-Za-z0-9_\-]{10,}\.[A-Za-z0-9_\-]{10,}\.[A-Za-z0-9_\-]{10,}/g, '[REDACTED-TOKEN]');
  }

  return { isExcluded, checkExclusion, redact };
}
