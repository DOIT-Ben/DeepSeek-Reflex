const fs=require('node:fs'),vm=require('node:vm'),assert=require('node:assert/strict'),path=require('node:path');
const root=process.argv[2];
const first=JSON.parse(fs.readFileSync(path.join(root,'prompt-first.json'),'utf8'));
const second=JSON.parse(fs.readFileSync(path.join(root,'prompt-second.json'),'utf8'));
let passed=0;
function fixture(value='') {
 class Editor {
  constructor(){this.tagName='TEXTAREA';this.value=value;this.handlers={};}
  getClientRects(){return [{}];}focus(){}
  addEventListener(name,callback,options){(this.handlers[name]??=[]).push({callback,once:options?.once});}
  removeEventListener(name,callback){this.handlers[name]=(this.handlers[name]??[]).filter(h=>h.callback!==callback);}
  dispatchEvent(event){for(const h of [...(this.handlers[event.type]??[])]){h.callback(event);if(h.once)this.handlers[event.type]=this.handlers[event.type].filter(x=>x!==h);}}
 }
 Object.defineProperty(Editor.prototype,'value',{get(){return this._value;},set(v){this._value=v;}});
 const editor=new Editor(),window={},location={href:'https://chat.deepseek.com/a',origin:'https://chat.deepseek.com'};
 const context=vm.createContext({window,location,document:{querySelectorAll:()=>[context.editor]},getComputedStyle:()=>({visibility:'visible'}),HTMLTextAreaElement:Editor,Event:class Event{constructor(type){this.type=type;}},editor});
 return {context,Editor,editor,run:script=>vm.runInContext(script,context)};
}
function check(name,fn){fn();passed++;console.log('PASS '+name);}
check('ordinary personal draft is preserved',()=>{const f=fixture('PERSONAL');assert.equal(f.run(first),'draft');assert.equal(f.editor.value,'PERSONAL');});
check('owned untouched request can be replaced',()=>{const f=fixture();assert.equal(f.run(first),'filled');assert.equal(f.run(second),'filled');assert.equal(f.editor.value,'NEXT REQUEST');});
check('equal text pasted into new editor is preserved',()=>{const f=fixture();f.run(first);const e=new f.Editor();e.value='PRIOR REQUEST';f.context.editor=e;assert.equal(f.run(second),'draft');assert.equal(e.value,'PRIOR REQUEST');});
check('edited then restored equal text is user owned',()=>{const f=fixture();f.run(first);f.editor.value='USER EDIT';f.editor.dispatchEvent({type:'input'});f.editor.value='PRIOR REQUEST';assert.equal(f.run(second),'draft');});
check('same editor after SPA navigation loses ownership',()=>{const f=fixture();f.run(first);f.context.location.href='https://chat.deepseek.com/b';assert.equal(f.run(second),'draft');});
check('Enter submission revokes ownership',()=>{const f=fixture();f.run(first);f.editor.dispatchEvent({type:'keydown',key:'Enter',shiftKey:false});assert.equal(f.run(second),'draft');});
check('cleared editor permits a new draft',()=>{const f=fixture();f.run(first);f.editor.value='';f.editor.dispatchEvent({type:'input'});assert.equal(f.run(second),'filled');});
check('Enter after an unrelated key still revokes ownership',()=>{const f=fixture();f.run(first);f.editor.dispatchEvent({type:'keydown',key:'ArrowLeft'});f.editor.dispatchEvent({type:'keydown',key:'Enter',shiftKey:false});assert.equal(f.run(second),'draft');});
check('replacement removes obsolete ownership listeners',()=>{const f=fixture();f.run(first);f.run(second);assert.equal(f.editor.handlers.input.length,1);assert.equal(f.editor.handlers.keydown.length,1);});
check('ambiguous editors remain untouched',()=>{const f=fixture();f.context.document.querySelectorAll=()=>[f.editor,new f.Editor()];assert.equal(f.run(first),'ambiguous');assert.equal(f.editor.value,'');});
check('contenteditable personal draft is preserved',()=>{const f=fixture();f.editor.tagName='DIV';f.editor.innerText='PRIOR REQUEST';assert.equal(f.run(second),'draft');assert.equal(f.editor.innerText,'PRIOR REQUEST');});
console.log('TOTAL '+passed+' draft checks; simulated DOM, no website or user data');
