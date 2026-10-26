// The native PanelTheme is the only source for type, dimensions, color and motion tokens.
const theme = __REFLEX_THEME__;
const variables = Object.entries(theme.variables).map(([name, value]) => `${name}:${value}`).join(';');

export const css = `
.reflex-control{${variables};position:relative;display:inline-flex;color-scheme:light dark;font-family:"${theme.fontFamily}","Segoe UI",system-ui,sans-serif;font-size:var(--reflex-body-size);line-height:1.6;color:var(--text-primary,var(--reflex-ink))}
.reflex-control button,.reflex-control select,.reflex-control textarea{font:inherit;color:inherit;box-sizing:border-box}
.reflex-control button{display:inline-flex;align-items:center;justify-content:center;gap:4px;min-height:var(--reflex-control-height);border:1px solid transparent;background:transparent;border-radius:var(--reflex-control-radius);padding:4px 10px;line-height:1.25;white-space:nowrap;cursor:pointer;transition:background-color var(--reflex-hover-duration) ease,color var(--reflex-hover-duration) ease,transform var(--reflex-release-duration) ease}
.reflex-control button:hover:not(:disabled){background:color-mix(in srgb,currentColor 6%,transparent)}
.reflex-control button:active:not(:disabled){background:color-mix(in srgb,currentColor 12%,transparent);transform:translateY(1px);transition-duration:var(--reflex-press-duration)}
.reflex-control button:disabled{color:var(--reflex-muted);cursor:default}
.reflex-control :focus-visible{outline:2px solid var(--reflex-accent);outline-offset:2px}
.reflex-control .reflex-trigger[aria-expanded=true]{background:var(--reflex-tint);color:var(--reflex-accent-hover)}
.reflex-panel{position:fixed;inset:auto;margin:0;width:min(360px,calc(100vw - 24px));max-height:min(560px,calc(100dvh - 24px));overflow:auto;box-sizing:border-box;background:var(--bg-primary,Canvas);color:inherit;border:1px solid color-mix(in srgb,currentColor 12%,transparent);border-radius:var(--reflex-panel-radius);padding:16px;box-shadow:0 18px 40px color-mix(in srgb,currentColor 8%,transparent);z-index:40}
.reflex-head,.reflex-actions{display:flex;align-items:center;gap:8px;flex-wrap:wrap}.reflex-head{justify-content:space-between;flex-wrap:nowrap;gap:12px}.reflex-head strong{min-width:0;flex:1;overflow-wrap:anywhere;font-size:var(--reflex-title-size);font-weight:600;line-height:1.3}
.reflex-control .reflex-close{flex:none;width:var(--reflex-control-height);padding:0;font-size:var(--reflex-title-size)}
.reflex-control .reflex-close:hover:not(:disabled){background:var(--reflex-close-hover);color:var(--reflex-ink)}.reflex-control .reflex-close:active:not(:disabled){background:var(--reflex-close-pressed)}
.reflex-panel p,.reflex-panel label,.reflex-panel [role=status]{font-size:var(--reflex-caption-size);line-height:1.6;color:color-mix(in srgb,currentColor 80%,transparent)}
.reflex-panel p{margin:4px 0 8px}.reflex-panel label{display:block;margin:8px 0 4px}
.reflex-panel select,.reflex-panel textarea{display:block;width:100%;margin-top:4px;padding:8px;background:var(--bg-primary,Canvas);color:var(--text-primary,var(--reflex-ink));font-size:var(--reflex-body-size);border:1px solid var(--reflex-input-border);border-radius:var(--reflex-control-radius);line-height:1.6;transition:border-color var(--reflex-hover-duration) ease}
.reflex-panel select{min-height:var(--reflex-input-height)}.reflex-panel select:disabled{color:var(--reflex-muted)}
.reflex-panel select:focus-visible,.reflex-panel textarea:focus-visible{border-color:var(--reflex-accent);outline:none}
.reflex-actions{margin-top:8px}.reflex-actions.reflex-phase{margin-top:16px}.reflex-panel .reflex-primary{background:var(--reflex-accent);color:white;font-weight:600}
.reflex-panel .reflex-primary:hover:not(:disabled){background:var(--reflex-accent-hover)}.reflex-panel .reflex-primary:active:not(:disabled){background:var(--reflex-accent-pressed)}
.reflex-panel .reflex-primary:disabled{background:var(--reflex-surface);color:var(--reflex-muted);border-color:var(--reflex-border)}
.reflex-panel textarea{resize:vertical;min-height:96px;max-height:220px}
.reflex-panel [role=status]{margin-top:12px;overflow-wrap:anywhere}.reflex-panel [role=status]:empty{display:none}
@media(prefers-reduced-motion:reduce){.reflex-control button,.reflex-panel select,.reflex-panel textarea{transition:none}.reflex-control button:active:not(:disabled){transform:none}}
`;
