import React from 'react';
import { createRoot } from 'react-dom/client';
import { runChecks } from './browser-checks.js';

let Component, dictionaries, clipboard = '', state = { draft: '帮我把这段需求写清楚：希望增加翻译和润色模式，保留原意。', draftRev: 1, phase: 'plain', occurrences: [], attachmentIds: ['fixture-image'] };
let sessionId = 'fixture-a';
const listeners = new Set();
const publish = () => listeners.forEach(fn => fn());
const clipboardState = { fail: false, writes: [] };
Object.defineProperty(navigator, 'clipboard', { configurable: true, value: {
  writeText: async text => { if (clipboardState.fail) throw new Error('fixture denied'); clipboard = text; clipboardState.writes.push(text); },
  readText: async () => { if (clipboardState.fail) throw new Error('fixture denied'); return clipboard; },
} });
const actions = { insertText: (text, span) => {
  if (span.draftRev !== state.draftRev || state.phase !== 'plain') return false;
  state = { ...state, draft: text, draftRev: state.draftRev + 1 }; publish(); return true;
} };
if (new URLSearchParams(location.search).has('legacy')) {
  delete actions.insertText;
  actions.setDraft = text => { state = { ...state, draft: text, draftRev: state.draftRev + 1 }; publish(); };
}
const locale = { register: (_ns, language, copy) => {
  if (typeof language !== 'string' || !copy) throw new Error('Use the three-argument DSH locale API');
  dictionaries ||= {}; dictionaries[language] = copy;
  return () => { delete dictionaries[language]; };
} };
function useInput(select) { return React.useSyncExternalStore(fn => { listeners.add(fn); return () => listeners.delete(fn); }, () => select(state)); }
function App() {
  const input = useInput(s => s);
  return <div id="composer"><textarea className="draft" aria-label="DSH 草稿" value={input.draft} onChange={e => { state = { ...state, draft: e.target.value, draftRev: state.draftRev + 1 }; publish(); }}/><div className="toolbar"><span>＋</span>{Component && <Component key={sessionId} sessionId={sessionId} useInput={useInput} inputActions={actions} t={key => dictionaries.zh[key] || key}/>}<span className="attachments">附件 × {input.attachmentIds.length}</span><button className="send" type="button" disabled>发送（预览禁用）</button></div></div>;
}
const root = createRoot(document.getElementById('root'));
const render = () => root.render(<App/>);
window.__ModuleLoader__ = { load: entry => {
  const plugin = entry.factory(name => { if (name !== 'react') throw new Error('Unexpected module'); return React; });
  plugin.apply({ effect: fn => fn(), locale, slots: { inject: (_name, fn) => fn(), register: (slot, component) => { if (slot.name !== 'conversation.input.left') throw new Error('Unexpected slot'); Component = component; return () => {}; } } });
  render();
} };
window.fixture = {
  get state() { return structuredClone(state); }, get copied() { return clipboard; }, clipboardState,
  reply: text => { clipboard = text; }, edit: text => { state = { ...state, draft: text, draftRev: state.draftRev + 1 }; publish(); },
  switchSession: id => { sessionId = id; render(); }, references: items => { state = { ...state, occurrences: items }; publish(); },
  attachments: ids => { state = { ...state, attachmentIds: ids }; publish(); },
};
window.runReflexChecks = runChecks;
