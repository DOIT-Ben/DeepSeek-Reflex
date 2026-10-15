import React from 'react';
import { MAX_TEXT, compose, capture, canApply, applyResult } from './core.js';

const modes = __REFLEX_MODES__;
const h = React.createElement;
const ns = 'reflex-window';
const dictionaries = {
  zh: {
    label: 'Reflex', title: '官网小窗 · 不调用 DSH 模型', close: '关闭', mode: '处理模式',
    help: '1. 复制请求并打开小窗，将小窗切到普通对话，再粘贴并确认发送，避免叠加模式。2. 复制官网回复。3. 在这里预览并确认填回。只处理文字，附件留在 DSH。',
    open: '只打开小窗', send: '复制请求并打开', paste: '读取复制的回复', preview: '回复预览（也可手动粘贴）', apply: '确认填回输入框', busy: '正在处理…',
    ready: '请求已复制。将小窗切到普通对话，再粘贴并检查发送。', opened: '已请求打开小窗。回复不会自动写入当前会话。',
    failed: '无法打开小窗。请先安装 Reflex，或在 DSH 启动环境中设置 REFLEX_EXECUTABLE 指向 DeepSeekFloat.exe。',
    local: '此试用版需要 Windows 本机的 DSH HTTP 页面（localhost）。远程页面和 file:// 桌面入口暂不支持。',
    clipboard: '剪贴板操作未完成。请检查浏览器权限；回复也可手动粘贴到预览框。',
    copied: '请求已复制，但小窗未能打开。可手动打开 Reflex 后粘贴。',
    conflict: '原草稿、附件或会话已变化，本次不覆盖。请复制预览内容并自行合并，或重新从当前草稿开始。',
    invalid: '请输入 1–20,000 字的文字。', applied: '已填回草稿，尚未发送。',
    references: '当前草稿包含引用标签，此试用版不替换它们。可只打开小窗，或先将文字复制出来处理。',
    unsupported: '当前 DSH 未提供受支持的草稿接口，请使用 DSH 0.1.5-rc.2 或更新的兼容版本。',
    normal: '随手问', translate: '翻译', explain: '解释', question: '理解提问', polish: '润色', summarize: '总结',
    noPending: '请先点击“复制请求并打开”或“只打开小窗”，绑定当前草稿。',
  },
  en: {
    label: 'Reflex', title: 'Website window · no DSH model calls', close: 'Close', mode: 'Text mode',
    help: '1. Copy and open. Set the window to regular chat before pasting and sending to avoid stacking modes. 2. Copy the website reply. 3. Preview and confirm fill-back here. Only text is processed; attachments stay in DSH.',
    open: 'Open window only', send: 'Copy request and open', paste: 'Read copied reply', preview: 'Reply preview (or paste manually)', apply: 'Confirm draft fill-back', busy: 'Working…',
    ready: 'Request copied. Set the window to regular chat, then paste, review and send.', opened: 'Window requested. Replies do not enter this session automatically.',
    failed: 'Could not open Reflex. Install it or set REFLEX_EXECUTABLE to DeepSeekFloat.exe in the DSH launch environment.',
    local: 'This preview requires a local Windows DSH HTTP page (localhost). Remote pages and file:// desktop entries are not supported yet.',
    clipboard: 'Clipboard operation did not complete. Check browser permissions, or paste the reply into the preview manually.',
    copied: 'Request copied, but the window did not open. Open Reflex manually and paste it.',
    conflict: 'The original draft, attachments or session changed. Copy the preview and merge manually, or start again from the current draft.',
    invalid: 'Enter 1–20,000 characters.', applied: 'Draft filled; nothing sent.',
    references: 'This preview does not replace reference chips. Open the window only, or copy plain text out first.',
    unsupported: 'Supported draft APIs are missing. Use DSH 0.1.5-rc.2 or a newer compatible version.',
    normal: 'Quick question', translate: 'Translate', explain: 'Explain', question: 'Questions', polish: 'Polish', summarize: 'Summarize',
    noPending: 'First use Copy request and open or Open window only to bind the current draft.',
  },
};

const css = `
.reflex-control{position:relative;display:inline-flex;font:inherit;color:inherit}
.reflex-control button,.reflex-control select,.reflex-control textarea{font:inherit;color:inherit;box-sizing:border-box}
.reflex-control button{border:0;background:transparent;border-radius:7px;padding:6px 8px;cursor:pointer;min-height:30px}
.reflex-control button:hover:not(:disabled){background:color-mix(in srgb,currentColor 8%,transparent)}
.reflex-control button:disabled{opacity:.48;cursor:default}
.reflex-control :focus-visible{outline:2px solid #4563f5;outline-offset:2px}
.reflex-panel{position:fixed;inset:auto;margin:0;width:min(360px,calc(100vw - 24px));max-height:min(560px,calc(100dvh - 24px));overflow:auto;box-sizing:border-box;background:var(--bg-primary,Canvas);color:var(--text-primary,CanvasText);border:1px solid color-mix(in srgb,currentColor 14%,transparent);border-radius:12px;padding:14px;box-shadow:0 8px 28px #0002;z-index:40}
.reflex-head,.reflex-actions{display:flex;align-items:center;gap:8px;flex-wrap:wrap}.reflex-head{justify-content:space-between}
.reflex-panel p{font-size:12px;line-height:1.6;opacity:.76;margin:8px 0 12px}.reflex-panel label{display:block;font-size:12px;margin:10px 0 6px}
.reflex-panel select{width:100%;padding:7px;background:inherit;border:1px solid color-mix(in srgb,currentColor 18%,transparent);border-radius:7px}
.reflex-actions{margin-top:10px}.reflex-panel .reflex-primary{background:#4563f5;color:white}.reflex-panel .reflex-primary:hover:not(:disabled){background:#354ed1}
.reflex-panel textarea{display:block;resize:vertical;width:100%;min-height:112px;max-height:260px;padding:8px;background:inherit;border:1px solid color-mix(in srgb,currentColor 18%,transparent);border-radius:7px;line-height:1.5}
.reflex-panel [role=status]{font-size:12px;line-height:1.5;margin-top:10px;overflow-wrap:anywhere}
`;

function Control({ sessionId, useInput, inputActions, t }) {
  const input = useInput?.(s => s);
  const [open, setOpen] = React.useState(false);
  const [modeId, setMode] = React.useState('polish');
  const [busy, setBusy] = React.useState(false);
  const [message, setMessage] = React.useState('');
  const [preview, setPreview] = React.useState('');
  const [pending, setPending] = React.useState(null);
  const alive = React.useRef(true);
  const inFlight = React.useRef(false);
  const container = React.useRef(null);
  const trigger = React.useRef(null);
  const panel = React.useRef(null);
  const [placement, setPlacement] = React.useState({});
  const current = React.useRef();
  current.current = { input, sessionId, inputActions };
  React.useEffect(() => { alive.current = true; return () => { alive.current = false; }; }, []);
  React.useLayoutEffect(() => {
    if (!open || !panel.current) return;
    const element = panel.current;
    // A top-layer popover escapes transformed/clipped composer ancestors without creating another app root.
    if (typeof element.showPopover === 'function') element.showPopover();
    const position = () => {
      const anchor = trigger.current.getBoundingClientRect();
      const width = element.offsetWidth, height = element.offsetHeight;
      const left = Math.max(12, Math.min(anchor.left, innerWidth - width - 12));
      const desired = anchor.top - height - 8 >= 12 ? anchor.top - height - 8 : anchor.bottom + 8;
      const top = Math.max(12, Math.min(desired, innerHeight - height - 12));
      setPlacement(previous => previous.left === left && previous.top === top ? previous : { left, top });
    };
    position();
    const observer = new ResizeObserver(position); observer.observe(element);
    window.addEventListener('resize', position);
    window.addEventListener('scroll', position, true);
    return () => {
      observer.disconnect(); window.removeEventListener('resize', position); window.removeEventListener('scroll', position, true);
      if (typeof element.hidePopover === 'function' && element.matches(':popover-open')) element.hidePopover();
    };
  }, [open]);
  React.useEffect(() => {
    if (!open) return;
    container.current?.querySelector('select')?.focus();
    const outside = event => { if (!container.current?.contains(event.target)) setOpen(false); };
    document.addEventListener('pointerdown', outside);
    return () => document.removeEventListener('pointerdown', outside);
  }, [open]);
  function dismiss() { setOpen(false); trigger.current?.focus(); }
  const supported = input && typeof input.draft === 'string' && Number.isSafeInteger(input.draftRev) && (typeof inputActions?.insertText === 'function' || typeof inputActions?.setDraft === 'function');
  const idle = supported && input.phase === 'plain';
  const references = (input?.occurrences || []).length > 0;

  async function request(action) {
    if (location.protocol !== 'http:' || !['127.0.0.1', 'localhost', '[::1]'].includes(location.hostname)) throw new Error('local');
    const response = await fetch('/reflex-window/' + action, { method: 'POST', headers: { 'x-reflex-request': '1' }, signal: AbortSignal.timeout(5000) });
    const result = await response.json();
    if (!response.ok || result.available === false) throw new Error(result.reason === 'windows-only' ? 'local' : 'failed');
    return result;
  }

  async function openWindow(withDraft) {
    if (inFlight.current || !idle) return;
    if (withDraft && references) { setMessage(t('references')); return; }
    const origin = capture(sessionId, input);
    let text;
    try { if (withDraft) text = compose(modes.find(m => m.id === modeId), origin.text); }
    catch { setMessage(t('invalid')); return; }
    inFlight.current = true; setBusy(true); setMessage('');
    let copied = false;
    try {
      await request('status');
      if (!alive.current || current.current.sessionId !== origin.sessionId) return;
      if (withDraft) {
        try { await navigator.clipboard.writeText(text); copied = true; }
        catch { setMessage(t('clipboard')); return; }
      }
      if (!alive.current) return;
      await request('open');
      if (!alive.current) return;
      setPending(origin); setPreview(''); setMessage(t(withDraft ? 'ready' : 'opened'));
    } catch (error) { if (alive.current) setMessage(t(copied ? 'copied' : error.message === 'local' ? 'local' : 'failed')); }
    finally { inFlight.current = false; if (alive.current) setBusy(false); }
  }

  async function readReply() {
    if (inFlight.current || !pending) return;
    const targetSession = sessionId;
    inFlight.current = true; setBusy(true);
    try {
      const value = await navigator.clipboard.readText();
      if (!alive.current || current.current.sessionId !== targetSession) return;
      if (!value.trim() || value.length > MAX_TEXT) { setMessage(t('invalid')); return; }
      setPreview(value); setMessage('');
    } catch { if (alive.current && current.current.sessionId === targetSession) setMessage(t('clipboard')); }
    finally { inFlight.current = false; if (alive.current) setBusy(false); }
  }

  function fillBack() {
    if (!pending) { setMessage(t('noPending')); return; }
    const live = current.current;
    if (!applyResult(pending, live.sessionId, live.input, live.inputActions, preview)) { setMessage(t('conflict')); return; }
    setPending(null); setPreview(''); setMessage(t('applied'));
  }

  const canFill = supported && canApply(pending, sessionId, input) && !!preview.trim() && preview.length <= MAX_TEXT;
  return h('div', { className: 'reflex-control', ref: container },
    h('style', null, css),
    h('button', { type: 'button', ref: trigger, title: t('title'), 'aria-expanded': open, 'aria-label': t('title'), onClick: () => setOpen(!open) }, t('label'), ' ▾'),
    open && h('section', { className: 'reflex-panel', popover: 'manual', ref: panel, style: placement, 'aria-label': t('title'), onKeyDown: event => { if (event.key === 'Escape') { event.stopPropagation(); dismiss(); } } },
      h('div', { className: 'reflex-head' }, h('strong', null, t('title')), h('button', { type: 'button', 'aria-label': t('close'), onClick: dismiss }, '×')),
      h('p', null, t('help')),
      h('label', null, t('mode'), h('select', { value: modeId, disabled: busy, onChange: event => setMode(event.target.value) }, modes.map(m => h('option', { key: m.id, value: m.id }, t(m.id))))),
      h('div', { className: 'reflex-actions' },
        h('button', { type: 'button', className: 'reflex-primary', disabled: busy || !idle || references || !input.draft.trim() || input.draft.length > MAX_TEXT, onClick: () => openWindow(true) }, busy ? t('busy') : t('send')),
        h('button', { type: 'button', disabled: busy || !idle, onClick: () => openWindow(false) }, t('open'))),
      !supported && h('p', { role: 'alert' }, t('unsupported')),
      references && h('p', { role: 'alert' }, t('references')),
      h('div', { className: 'reflex-actions' }, h('button', { type: 'button', disabled: busy || !pending, onClick: readReply }, t('paste'))),
      h('label', null, t('preview'), h('textarea', { value: preview, maxLength: MAX_TEXT, onChange: event => setPreview(event.target.value) })),
      pending && !canApply(pending, sessionId, input) && h('p', { role: 'alert' }, t('conflict')),
      h('div', { className: 'reflex-actions' }, h('button', { type: 'button', className: 'reflex-primary', disabled: busy || !canFill, onClick: fillBack }, t('apply'))),
      h('div', { role: 'status', 'aria-live': 'polite' }, message)));
}

export const plugin = {
  inject: ['slots', 'locale'],
  apply(ctx) {
    for (const [language, dictionary] of Object.entries(dictionaries)) {
      ctx.effect(() => ctx.locale.register(ns, language, dictionary), 'reflex-window: copy ' + language);
    }
    ctx.slots.inject('conversation.input.left', () => ctx.slots.register({ name: 'conversation.input.left', id: 'reflex-window', order: 55, locale: ns }, Control));
  },
};
