const fs=require('node:fs'),vm=require('node:vm'),assert=require('node:assert/strict');
const path=require('node:path'),root=process.argv[2]||__dirname,script=JSON.parse(fs.readFileSync(path.join(root,'focus-script.json'),'utf8'));
let passed=0;
function run(options={}) {
 let focusCalls=0;
 const document={hasFocus:()=>options.active!==false,activeElement:null,querySelectorAll:()=>editors};
 function editor(overrides={}) {
  const e={disabled:false,readOnly:false,getClientRects:()=>[{}],style:{visibility:'visible'},focus(arg){assert.equal(arg.preventScroll,true);document.activeElement=this;focusCalls++;},...overrides};
  for(const prop of ['value','innerText','textContent'])Object.defineProperty(e,prop,{get(){throw new Error('must not read draft');},set(){throw new Error('must not alter draft');}});
  return e;
 }
 const editors=options.editors?options.editors(editor):[editor()];
 const result=vm.runInNewContext(script,{document,location:{origin:options.origin||'https://chat.deepseek.com'},getComputedStyle:e=>e.style});
 return {result,focusCalls};
}
function check(name,options,expected,calls=0){const actual=run(options);assert.equal(actual.result,expected,name);assert.equal(actual.focusCalls,calls,name);passed++;console.log('PASS '+name);}
check('focus one visible writable editor without reading or writing draft',{},'focused',1);
check('do not focus when document is inactive',{active:false},'not-active');
check('do not focus other origin',{origin:'https://example.com'},'wrong-host');
check('login page without editor',{editors:()=>[]},'no-editor');
check('ambiguous multiple editors',{editors:e=>[e(),e()]},'no-editor');
check('ignore readonly and disabled fields',{editors:e=>[e({readOnly:true}),e({disabled:true})]},'no-editor');
check('ignore hidden editor',{editors:e=>[e({getClientRects:()=>[]})]},'no-editor');
check('ignore visibility hidden editor',{editors:e=>[e({style:{visibility:'hidden'}})]},'no-editor');
check('select only eligible visible editor',{editors:e=>[e({disabled:true}),e()]},'focused',1);
check('detect unsuccessful focus',{editors:e=>[e({focus(){}})]},'not-focused');
fs.writeFileSync(path.join(root,'focus-tests.txt'),`passed=${passed}\nSimulated DOM only; no actual website focus acceptance.\n`);
console.log('TOTAL '+passed);
