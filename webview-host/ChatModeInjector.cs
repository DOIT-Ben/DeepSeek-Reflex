using System.Linq;
namespace DeepSeekFloat {
    internal static class ChatModeInjector {
        internal static string ExpectOneShotScript(string prompt){return "(()=>{if(location.origin!=='https://chat.deepseek.com'||window.top!==window)return false;const state=window.__deepSeekFloatChatMode;if(!state)return false;state.oneShotPending="+PromptInserter.Json(prompt)+";return true;})()";}
        internal static string Script(string id) {
            string catalog="{"+string.Join(",",ChatModes.All.Select(m=>PromptInserter.Json(m.Id)+":"+PromptInserter.Json(ChatModes.Prefix(m))))+"}";
            return "(()=>{if(location.origin!=='https://chat.deepseek.com'||window.top!==window)return 'wrong-host';const prefixes="+catalog+",mode="+PromptInserter.Json(ChatModes.Find(id).Id)+";"+@"
let state=window.__deepSeekFloatChatMode;
if(!state){
 state={mode:'normal',marker:null,oneShot:null,suspended:false,writing:false,composing:false};
 window.__deepSeekFloatChatMode=state;
 const read=e=>e.tagName==='TEXTAREA'?e.value:e.innerText;
 const editor=()=>{const all=Array.from(document.querySelectorAll('textarea#chat-input,textarea[placeholder*=""发送消息""],#chat-input[contenteditable=true]')).filter(e=>!e.disabled&&!e.readOnly&&e.getClientRects().length&&getComputedStyle(e).visibility!=='hidden');return all.length===1?all[0]:null;};
 const write=(e,text,offset)=>{
  state.writing=true;
  try{
   if(e.tagName==='TEXTAREA'){
    const start=e.selectionStart,end=e.selectionEnd,direction=e.selectionDirection;
    Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype,'value').set.call(e,text);
    if(document.activeElement===e&&start!==null)e.setSelectionRange(Math.max(0,start+offset),Math.max(0,end+offset),direction);
    e.dispatchEvent(new Event('input',{bubbles:true}));
   }else{
    // Preserve rich editors rather than flattening markup or moving their caret.
    return false;
   }
   return read(e)===text;
  }finally{state.writing=false;}
 };
 state.adopt=(e,text)=>{const end=text.indexOf('\n',Math.max(0,text.search(/\S/)));state.marker=null;state.oneShot={element:e,url:location.href,header:end<0?text:text.slice(0,end)};};
 state.apply=()=>{
  if(state.suspended||state.writing||state.composing)return 'busy';
  if(state.mode==='normal'&&!state.marker)return 'ready';
  const e=editor();if(!e)return 'no-editor';
  if(e.tagName!=='TEXTAREA')return 'unsupported-editor';
  const text=read(e),mark=state.marker;
  if(state.oneShotPending&&text===state.oneShotPending){state.adopt(e,text);state.oneShotPending=null;return 'one-shot';}
  if(state.oneShot){
   const once=state.oneShot;
   if(text.trim()&&once.header&&once.element===e&&once.url===location.href&&text.startsWith(once.header))return 'one-shot';
   state.oneShot=null;
  }
  let raw=text,oldPrefix='';
  if(mark&&mark.element===e){
   if(text.startsWith(mark.prefix)){oldPrefix=mark.prefix;raw=text.slice(oldPrefix.length);}
   else if(text.trim()&&(text.startsWith(mark.prefix.split('\n')[0])||text.includes('\n\n待处理内容：\n')))return 'edited-prefix';
  }
  const prefix=prefixes[state.mode]||'';
  // Empty composers stay empty: there must always be real material to send.
  const next=raw.trim()?prefix+raw:raw;
  if(next===text){state.marker=prefix&&raw.trim()?{element:e,url:location.href,prefix}:null;return 'ready';}
  state.marker=prefix&&raw.trim()?{element:e,url:location.href,prefix}:null;
  if(!write(e,next,prefix.length-oldPrefix.length)){state.marker=null;return 'failed';}
  return 'ready';
 };
 document.addEventListener('compositionstart',()=>{state.composing=true;},true);
 document.addEventListener('compositionend',()=>{state.composing=false;setTimeout(()=>state.apply(),0);},true);
 document.addEventListener('input',event=>{if(!event.isComposing)state.apply();},true);
 document.addEventListener('keydown',event=>{if(event.key==='Enter'&&!event.shiftKey&&!event.isComposing&&event.keyCode!==229)state.apply();},true);
 // Pointer sends and SPA editor replacements need no site-specific CSS classes.
 document.addEventListener('click',()=>state.apply(),true);
 new MutationObserver(()=>state.apply()).observe(document,{childList:true,subtree:true});
}
state.mode=mode;return state.apply();})()";
        }
    }
}
