import React from 'react';
import { MAX_TEXT, compose, capture, canApply, applyResult } from './core.js';

const modes = __REFLEX_MODES__;
const h = React.createElement;
const ns = 'reflex-window';
const dictionaries = {
  zh: {
    label: 'Reflex', title: '官网小窗 · 不调用 DSH 模型', close: '关闭', mode: '处理模式',
    help: '选择处理模式后，「带入小窗」把当前草稿交给独立小窗，检查后发送。发送后点「读取小窗回复」自动读回；只处理文字，附件留在 DSH。',
    helpReturn: '发送后点「读取小窗回复」预览；自动读取不可用时，把官网回复复制到剪贴板再点「读取复制的回复」。确认后点「确认填回输入框」，不自动发送。',
    open: '只打开小窗', stage: '带入小窗', staged: '已填入小窗，请检查后发送。发送后点「读取小窗回复」读回。',
    stageDraft: '小窗已有草稿，已保留。请先处理小窗中的文字，再重试。',
    stageUnavailable: '小窗尚未就绪、页面不支持或设置面板仍打开。请检查小窗；也可选择“复制请求并打开”。',
    stageUnknown: '未能确认是否已填入。请先检查小窗，避免重复带入；旧版小窗可选“复制请求并打开”。',
    send: '复制请求并打开', paste: '读取复制的回复', fetch: '读取小窗回复', preview: '回复预览（也可手动粘贴）', apply: '确认填回输入框', busy: '正在处理…',
    fetchEmpty: '小窗里还没有回复。先在小窗发送请求，等回复完成再读取。',
    fetchDone: '已读取小窗回复，请核对后点「确认填回输入框」。',
    fetchOverflow: '回复超过 20,000 字。请在小窗手动复制需要的部分，粘贴到预览框。',
    fetchUnsupported: '小窗页面暂不支持自动读取。请手动复制官网回复，再点「读取复制的回复」。',
    fetchBusy: '小窗正在处理上一条请求，请稍后再读取。',
    fetchUnknown: '未能确认是否读取成功。请重试，或手动复制官网回复。',
    ready: '请求已复制。将小窗切到普通对话，再粘贴并检查发送。', opened: '已请求打开小窗。回复不会自动写入当前会话。',
    failed: '无法打开小窗。请先安装 Reflex，或在 DSH 启动环境中设置 REFLEX_EXECUTABLE 指向 DeepSeek-Reflex.exe。',
    local: '此试用版需要 Windows 本机的 DSH HTTP 页面（localhost）。远程页面和 file:// 桌面入口暂不支持。',
    clipboard: '剪贴板操作未完成。请检查浏览器权限；回复也可手动粘贴到预览框。',
    emptyClipboard: '剪贴板里没有文字。先复制官网回复，再读取。',
    copied: '请求已复制，但小窗未能打开。可手动打开 Reflex 后粘贴。',
    conflict: '原草稿、附件或会话已变化，本次不覆盖。请复制预览内容并自行合并，或重新从当前草稿开始。',
    invalid: '请输入 1–20,000 字的文字。', applied: '已填回草稿，尚未发送。',
    tooLong: '草稿超过 20,000 字。超长草稿只能用「只打开小窗」，或先缩短后再带入。',
    references: '当前草稿包含引用标签，此试用版不替换它们。可只打开小窗，或先将文字复制出来处理。',
    unsupported: '当前 DSH 未提供受支持的草稿接口，请使用 DSH 0.1.5-rc.2 或更新的兼容版本。',
    normal: '随手问', translate: '翻译', explain: '解释', question: '理解提问', polish: '润色', summarize: '总结',
    noPending: '请先点击“复制请求并打开”或“只打开小窗”，绑定当前草稿。',
  },
  en: {
    label: 'Reflex', title: 'Website window · no DSH model calls', close: 'Close', mode: 'Text mode',
    help: 'Pick a mode, then Bring into window hands the current draft to the separate window; review and send there. After it replies, Read window reply fetches it back automatically; only text is handled and attachments stay in DSH.',
    helpReturn: 'After sending, click Read window reply to preview it. If automatic read is unavailable, copy the website reply and click Read copied reply. Confirm with Confirm draft fill-back; nothing is sent automatically.',
    open: 'Open window only', stage: 'Bring into window', staged: 'Filled into the window. Review before sending, then use Read window reply to fetch it back.',
    stageDraft: 'The window already has a draft. It was preserved; handle it before trying again.',
    stageUnavailable: 'The window is not ready, the page is unsupported, or a settings panel is open. Check the window, or choose Copy request and open.',
    stageUnknown: 'Fill status could not be confirmed. Check the window before retrying. Older windows can use Copy request and open.',
    send: 'Copy request and open', paste: 'Read copied reply', fetch: 'Read window reply', preview: 'Reply preview (or paste manually)', apply: 'Confirm draft fill-back', busy: 'Working…',
    fetchEmpty: 'No reply in the window yet. Send the request there first, then read once it finishes.',
    fetchDone: 'Reply fetched. Review it, then confirm the draft fill-back.',
    fetchOverflow: 'The reply exceeds 20,000 characters. Copy the parts you need in the window and paste them into the preview.',
    fetchUnsupported: 'The window page does not support automatic reading yet. Copy the website reply manually, then use Read copied reply.',
    fetchBusy: 'The window is still handling the previous request. Try reading again shortly.',
    fetchUnknown: 'Read status could not be confirmed. Retry, or copy the website reply manually.',
    ready: 'Request copied. Set the window to regular chat, then paste, review and send.', opened: 'Window requested. Replies do not enter this session automatically.',
    failed: 'Could not open Reflex. Install it or set REFLEX_EXECUTABLE to DeepSeek-Reflex.exe in the DSH launch environment.',
    local: 'This preview requires a local Windows DSH HTTP page (localhost). Remote pages and file:// desktop entries are not supported yet.',
    clipboard: 'Clipboard operation did not complete. Check browser permissions, or paste the reply into the preview manually.',
    emptyClipboard: 'No text on the clipboard. Copy the website reply first, then read it.',
    copied: 'Request copied, but the window did not open. Open Reflex manually and paste it.',
    conflict: 'The original draft, attachments or session changed. Copy the preview and merge manually, or start again from the current draft.',
    invalid: 'Enter 1–20,000 characters.', applied: 'Draft filled; nothing sent.',
    tooLong: 'The draft exceeds 20,000 characters. Long drafts can only use Open window, or shorten the draft first.',
    references: 'This preview does not replace reference chips. Open the window only, or copy plain text out first.',
    unsupported: 'Supported draft APIs are missing. Use DSH 0.1.5-rc.2 or a newer compatible version.',
    normal: 'Quick question', translate: 'Translate', explain: 'Explain', question: 'Questions', polish: 'Polish', summarize: 'Summarize',
    noPending: 'First use Copy request and open or Open window only to bind the current draft.',
  },
};

import { css } from './styles.js';

function inputCluster(trigger) {
  let rect = trigger.getBoundingClientRect();
  let node = trigger.parentElement;
  for (let i = 0; node && node !== document.body && i < 5; i += 1, node = node.parentElement) {
    const box = node.getBoundingClientRect();
    if (rect.top - box.top > 240 || box.height > 320 || box.bottom < rect.bottom - 1) break;
    if (box.top <= rect.top && box.bottom >= rect.bottom) rect = box;
  }
  return rect;
}

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
      const anchor = inputCluster(trigger.current);
      const margin = 12, gap = 10;
      const spaceAbove = anchor.top - gap - margin;
      const spaceBelow = innerHeight - anchor.bottom - gap - margin;
      const placeAbove = spaceAbove >= 180 || spaceAbove >= spaceBelow;
      const maxHeight = Math.max(160, Math.floor(placeAbove ? spaceAbove : spaceBelow));
      const width = element.offsetWidth;
      const left = Math.max(margin, Math.min(anchor.left, innerWidth - width - margin));
      const height = Math.min(element.scrollHeight, maxHeight);
      const top = placeAbove ? Math.max(margin, anchor.top - gap - height) : anchor.bottom + gap;
      const next = { left, top, maxHeight };
      setPlacement(previous => previous.left === next.left && previous.top === next.top && previous.maxHeight === next.maxHeight ? previous : next);
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
  const tooLong = supported && input.draft.length > MAX_TEXT;

  async function request(action, payload, timeout) {
    if (location.protocol !== 'http:' || !['127.0.0.1', 'localhost', '[::1]'].includes(location.hostname)) throw new Error('local');
    const limit = timeout ?? (payload ? 16000 : 5000);
    const response = await fetch('/reflex-window/' + action, { method: 'POST', headers: { 'x-reflex-request': '1', ...(payload ? { 'content-type': 'application/json' } : {}) }, ...(payload ? { body: JSON.stringify(payload) } : {}), signal: AbortSignal.timeout(limit) });
    const result = await response.json();
    if (!response.ok || result.available === false) throw new Error(result.reason === 'windows-only' ? 'local' : 'failed');
    return result;
  }

  async function stageWindow() {
    if (inFlight.current || !idle || references || !input.draft.trim() || input.draft.length > MAX_TEXT) return;
    const origin = capture(sessionId, input), id = crypto.randomUUID();
    inFlight.current = true; setBusy(true); setMessage('');
    try {
      const result = await request('stage', { id, mode: modeId, text: origin.text });
      if (!alive.current || current.current.sessionId !== origin.sessionId) return;
      if (result.id !== id || result.status === 'outcome-unknown') { setMessage(t('stageUnknown')); return; }
      if (result.status !== 'filled') { setMessage(t(result.status === 'draft' ? 'stageDraft' : 'stageUnavailable')); return; }
      setPending(origin); setPreview(''); setMessage(t('staged'));
    } catch (error) { if (alive.current && current.current.sessionId === origin.sessionId) setMessage(t(error.message === 'local' ? 'local' : 'stageUnknown')); }
    finally { inFlight.current = false; if (alive.current) setBusy(false); }
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
      if (!alive.current || current.current.sessionId !== origin.sessionId) return;
      await request('open');
      if (!alive.current || current.current.sessionId !== origin.sessionId) return;
      setPending(origin); setPreview(''); setMessage(t(withDraft ? 'ready' : 'opened'));
    } catch (error) { if (alive.current) setMessage(t(copied ? 'copied' : error.message === 'local' ? 'local' : 'failed')); }
    finally { inFlight.current = false; if (alive.current) setBusy(false); }
  }

  async function fetchReply() {
    if (inFlight.current || !pending) return;
    const targetSession = sessionId;
    inFlight.current = true; setBusy(true); setMessage('');
    try {
      const result = await request('reply', null, 16000);
      if (!alive.current || current.current.sessionId !== targetSession) return;
      if (result.status === 'ok' && typeof result.text === 'string' && result.text.trim()) { setPreview(result.text); setMessage(t('fetchDone')); return; }
      if (result.status === 'empty') { setMessage(t('fetchEmpty')); return; }
      if (result.status === 'overflow') { setMessage(t('fetchOverflow')); return; }
      if (result.status === 'unsupported' || result.status === 'wrong-host') { setMessage(t('fetchUnsupported')); return; }
      if (result.status === 'loading' || result.status === 'busy') { setMessage(t('fetchBusy')); return; }
      setMessage(t('fetchUnknown'));
    } catch (error) { if (alive.current && current.current.sessionId === targetSession) setMessage(t(error.message === 'local' ? 'local' : 'fetchUnknown')); }
    finally { inFlight.current = false; if (alive.current) setBusy(false); }
  }

  async function readReply() {
    if (inFlight.current || !pending) return;
    const targetSession = sessionId;
    inFlight.current = true; setBusy(true);
    try {
      const value = await navigator.clipboard.readText();
      if (!alive.current || current.current.sessionId !== targetSession) return;
      if (!value.trim()) { setMessage(t('emptyClipboard')); return; }
      if (value.length > MAX_TEXT) { setMessage(t('invalid')); return; }
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
    h('button', { type: 'button', className: 'reflex-trigger', ref: trigger, title: t('title'), 'aria-expanded': open, 'aria-label': t('title'), onClick: () => setOpen(!open) }, t('label'), ' ▾'),
    open && h('section', { className: 'reflex-panel', popover: 'manual', ref: panel, style: placement, 'aria-label': t('title'), onKeyDown: event => { if (event.key === 'Escape') { event.stopPropagation(); dismiss(); } } },
      h('div', { className: 'reflex-head' }, h('strong', null, t('title')), h('button', { type: 'button', className: 'reflex-close', 'aria-label': t('close'), title: t('close'), onClick: dismiss }, '×')),
      h('p', null, t(pending ? 'helpReturn' : 'help')),
      h('label', null, t('mode'), h('select', { value: modeId, disabled: busy, onChange: event => setMode(event.target.value) }, modes.map(m => h('option', { key: m.id, value: m.id }, t(m.id))))),
      h('div', { className: 'reflex-actions' },
        h('button', { type: 'button', className: canFill ? '' : 'reflex-primary', disabled: busy || !idle || references || !input.draft.trim() || input.draft.length > MAX_TEXT, onClick: stageWindow }, busy ? t('busy') : t('stage')),
        h('button', { type: 'button', disabled: busy || !idle || references || !input.draft.trim() || input.draft.length > MAX_TEXT, onClick: () => openWindow(true) }, t('send')),
        h('button', { type: 'button', disabled: busy || !idle, onClick: () => openWindow(false) }, t('open'))),
      !supported && h('p', { role: 'alert' }, t('unsupported')),
      references && h('p', { role: 'alert' }, t('references')),
      tooLong && h('p', { role: 'alert' }, t('tooLong')),
      h('div', { className: 'reflex-actions reflex-phase' },
        h('button', { type: 'button', disabled: busy || !pending, onClick: fetchReply }, t('fetch')),
        h('button', { type: 'button', disabled: busy || !pending, onClick: readReply }, t('paste'))),
      h('label', null, t('preview'), h('textarea', { value: preview, maxLength: MAX_TEXT, onChange: event => setPreview(event.target.value) })),
      pending && !canApply(pending, sessionId, input) && h('p', { role: 'alert' }, t('conflict')),
      h('div', { className: 'reflex-actions' }, h('button', { type: 'button', className: canFill ? 'reflex-primary' : '', disabled: busy || !canFill, onClick: fillBack }, t('apply'))),
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
