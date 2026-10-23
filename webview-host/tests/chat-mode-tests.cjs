const fs=require('node:fs'),vm=require('node:vm'),assert=require('node:assert/strict'),path=require('node:path');
const root=process.argv[2],scripts={};
for(const mode of ['normal','translate','explain','question','polish','summarize'])scripts[mode]=JSON.parse(fs.readFileSync(path.join(root,`chat-mode-${mode}.json`),'utf8'));
const selection=JSON.parse(fs.readFileSync(path.join(root,'prompt-first.json'),'utf8'));
const copy=JSON.parse(fs.readFileSync(path.join(root,'chat-mode-copy.json'),'utf8'));
let passed=0;
function fixture(value='') {
 const handlers={},timers=[];
 const document={activeElement:null,querySelectorAll:()=>editors,addEventListener:(name,callback)=>(handlers[name]??=[]).push(callback)};
 const fire=(type,extra={})=>{for(const callback of handlers[type]??[])callback({type,...extra});};
 class Editor {
  constructor(text=''){this.tagName='TEXTAREA';this.value=text;this.selectionStart=text.length;this.selectionEnd=text.length;this.selectionDirection='none';this.handlers={};}
  getClientRects(){return [{}];}focus(){document.activeElement=this;}
  setSelectionRange(start,end,direction){this.selectionStart=Math.min(this.value.length,start);this.selectionEnd=Math.min(this.value.length,end);this.selectionDirection=direction;}
  addEventListener(name,callback){(this.handlers[name]??=[]).push(callback);}
  removeEventListener(name,callback){this.handlers[name]=(this.handlers[name]??[]).filter(h=>h!==callback);}
  dispatchEvent(event){fire(event.type,event);for(const h of [...(this.handlers[event.type]??[])])h(event);}
 }
 Object.defineProperty(Editor.prototype,'value',{get(){return this._value;},set(v){this._value=v;}});
 const e=new Editor(value);let editors=[e];document.activeElement=e;
 const window={};window.top=window;
 const location={origin:'https://chat.deepseek.com',href:'https://chat.deepseek.com/a'};
 const context=vm.createContext({window,document,location,HTMLTextAreaElement:Editor,Event:class{constructor(type,options){this.type=type;Object.assign(this,options);}},MutationObserver:class{constructor(callback){this.callback=callback;}observe(){}},getComputedStyle:()=>({visibility:'visible'}),setTimeout:callback=>timers.push(callback)});
 return {e,window,location,Editor,document,fire,run:mode=>vm.runInContext(scripts[mode],context),runSelection:()=>vm.runInContext(selection,context),expectCopy:()=>vm.runInContext(copy,context),type:text=>{e.value=text;e.selectionStart=e.selectionEnd=text.length;fire('input');},flush:()=>{while(timers.length)timers.shift()();},setEditors:list=>{editors=list;}};
}
function check(name,fn){fn();passed++;console.log('PASS '+name);}
check('ordinary conversation never changes raw input',()=>{const f=fixture('RAW');f.run('normal');f.type('NEXT');assert.equal(f.e.value,'NEXT');});
check('switching applies a visible translation prefix to an existing draft',()=>{const f=fixture('RAW');assert.equal(f.run('translate'),'ready');assert.ok(f.e.value.startsWith('请翻译'));assert.ok(f.e.value.endsWith('RAW'));});
check('empty input stays empty until material is entered',()=>{const f=fixture();f.run('translate');assert.equal(f.e.value,'');f.type('hello');assert.ok(f.e.value.endsWith('hello'));assert.ok(f.e.value.startsWith('请翻译'));});
check('consecutive messages each receive exactly one prefix',()=>{const f=fixture();f.run('translate');f.type('one');f.type('');assert.equal(f.e.value,'');f.type('two');const text=f.e.value;f.fire('click');f.fire('keydown',{key:'Enter'});f.run('translate');assert.equal(f.e.value,text);assert.equal(text.split('待处理内容：').length,2);assert.ok(text.endsWith('two'));});
check('switching modes replaces only the owned prefix',()=>{const f=fixture('RAW');f.run('translate');f.run('explain');assert.ok(f.e.value.startsWith('请面向学生'));assert.ok(!f.e.value.includes('请翻译'));f.run('normal');assert.equal(f.e.value,'RAW');});
check('user edits to material survive repeated switches',()=>{const f=fixture('RAW');f.run('translate');f.type(f.e.value+' USER EDIT');f.run('summarize');f.run('normal');assert.equal(f.e.value,'RAW USER EDIT');});
check('edited prompt headers remain intact instead of accumulating prefixes',()=>{const f=fixture('RAW');f.run('translate');const edited=f.e.value.replace('请翻译','请仔细翻译');f.type(edited);f.run('explain');f.run('normal');assert.equal(f.e.value,edited);});
check('caret and selected range follow the visible prefix',()=>{const f=fixture('HELLO');f.e.selectionStart=1;f.e.selectionEnd=3;f.run('translate');const length=f.e.value.length-5;assert.equal(f.e.selectionStart,1+length);assert.equal(f.e.selectionEnd,3+length);f.run('normal');assert.equal(f.e.selectionStart,1);assert.equal(f.e.selectionEnd,3);});
check('Chinese IME composition is left alone until completion',()=>{const f=fixture();f.run('translate');f.fire('compositionstart');f.e.value='你好';f.fire('input',{isComposing:true});f.fire('keydown',{key:'Enter',isComposing:true,keyCode:229});assert.equal(f.e.value,'你好');f.fire('compositionend');f.flush();assert.ok(f.e.value.startsWith('请翻译'));assert.ok(f.e.value.endsWith('你好'));});
check('Ctrl+A replacement and paste receive the current mode',()=>{const f=fixture('OLD');f.run('polish');f.type('NEW PASTED TEXT');assert.ok(f.e.value.startsWith('请润色'));assert.ok(f.e.value.endsWith('NEW PASTED TEXT'));assert.ok(!f.e.value.includes('OLD'));});
check('SPA navigation with the same draft does not duplicate its prefix',()=>{const f=fixture('RAW');f.run('translate');const prior=f.e.value;f.location.href='https://chat.deepseek.com/b';f.fire('click');assert.equal(f.e.value,prior);f.run('normal');assert.equal(f.e.value,'RAW');});
check('replaced composer uses the current mode',()=>{const f=fixture('OLD');f.run('translate');const next=new f.Editor('NEW');f.setEditors([next]);f.fire('input');assert.ok(next.value.startsWith('请翻译'));assert.ok(next.value.endsWith('NEW'));});
check('ambiguous composers are left untouched',()=>{const f=fixture('RAW');f.setEditors([f.e,new f.Editor('OTHER')]);assert.equal(f.run('translate'),'no-editor');assert.equal(f.e.value,'RAW');});
check('rich editors remain intact rather than being flattened',()=>{const f=fixture();f.e.tagName='DIV';f.e.innerText='RICH';assert.equal(f.run('translate'),'unsupported-editor');assert.equal(f.e.innerText,'RICH');});
check('other origins and child frames get no listeners or mutation',()=>{const f=fixture('RAW');f.location.origin='https://example.com';assert.equal(f.run('translate'),'wrong-host');assert.equal(f.window.__deepSeekFloatChatMode,undefined);f.location.origin='https://chat.deepseek.com';f.window.top={};assert.equal(f.run('translate'),'wrong-host');});
check('one-shot selection bypasses persistent mode without double wrapping',()=>{const f=fixture();f.run('translate');assert.equal(f.runSelection(),'filled');assert.equal(f.e.value,'PRIOR REQUEST');f.run('explain');assert.equal(f.e.value,'PRIOR REQUEST');f.type('');f.type('NEXT');assert.ok(f.e.value.startsWith('请面向学生'));assert.ok(f.e.value.endsWith('NEXT'));});
check('pasting a tool-copied one-shot request skips one mode prefix',()=>{const f=fixture();f.run('translate');assert.equal(f.expectCopy(),true);f.type('UNRELATED');assert.ok(f.e.value.startsWith('请翻译'));f.type('');f.type('COPIED ONE SHOT');assert.equal(f.e.value,'COPIED ONE SHOT');f.type('');f.type('NEW MATERIAL');assert.ok(f.e.value.startsWith('请翻译'));});
check('emptying material also removes the owned prefix',()=>{const f=fixture('RAW');f.run('translate');f.type(f.e.value.slice(0,-3));assert.equal(f.e.value,'');});
check('all catalog modes can be activated and switched back',()=>{for(const mode of Object.keys(scripts)){const f=fixture('中文\nEnglish "quote" \\ path');f.run(mode);f.run('normal');assert.equal(f.e.value,'中文\nEnglish "quote" \\ path');}});
check('normal one-shot with leading blank line relinquishes ownership after clearing',()=>{const f=fixture();f.run('translate');f.window.__deepSeekFloatChatMode.adopt(f.e,'\nRAW QUESTION');f.type('\nRAW QUESTION');assert.equal(f.e.value,'\nRAW QUESTION');f.type('');f.type('NEXT');assert.ok(f.e.value.startsWith('请翻译'));assert.ok(f.e.value.endsWith('NEXT'));});
console.log(`TOTAL ${passed} chat-mode checks; simulated DOM, no website or user data`);
