import fs from 'node:fs/promises';
import path from 'node:path';
import {pathToFileURL} from 'node:url';
const runtime='C:/Users/zisla/.cache/codex-runtimes/codex-primary-runtime/dependencies';
process.env.RUNTIME_NODE_MODULES=runtime+'/node/node_modules';
const {Presentation,PresentationFile}=await import(pathToFileURL(process.env.RUNTIME_NODE_MODULES+'/@oai/artifact-tool/dist/artifact_tool.mjs'));
const build=path.resolve('artifacts/ui-presentation');
const out=path.resolve('docs/handbook');
const items=[
 ['OneCart Commerce','A tour of the storefront and administration workspace','storefront.png','Product content, variant choices and guest shopping on your own infrastructure.'],
 ['Mobile storefront','A product page for smaller screens','mobile.png','The same product journey adapts to phone displays. Language selection and cart controls stay within reach.'],
 ['Administrator sign-in','Email, password and optional authenticator verification','signin.png','Secure API cookies and server-side authorization protect administrator access.'],
 ['Store workspace','Navigation by business responsibility','dashboard.png','Catalog, orders, storefront settings, accounts and audit are available according to assigned roles.'],
 ['Product entry','Structured sections for catalog data','catalog.png','Manage product information and related entries with explicit save and edit actions.'],
 ['Variants and pricing','Independent SKU, price and availability','variants.png','Add variants, update their inventory and maintain translated labels.'],
 ['Order management','Guest COD orders and fulfillment state','orders.png','Review saved order values and customer-provided delivery details. Cancellation restores stock.'],
 ['Storefront configuration','Branding and product content in the admin panel','settings.png','Maintain About Us, benefits, usage text, reviews and theme selections.'],
 ['Administrative access','Roles, MFA policy and email recovery','accounts.png','SystemAdmin creates team accounts, assigns responsibilities and configures the store’s SMTP relay.']
];
const p=Presentation.create({slideSize:{width:1280,height:720}});
function txt(s,v,x,y,w,h,size,color='#292229',bold=false){const t=s.shapes.add({geometry:'textbox',position:{left:x,top:y,width:w,height:h},fill:'none',line:{fill:'none',width:0}});t.text=v;t.text.style={fontFamily:'Arial',fontSize:size,color,bold};}
for(let i=0;i<items.length;i++){
 const [title,sub,img,body]=items[i];const s=p.slides.add();s.background.fill='#FCFAFB';
 txt(s,title,54,32,1160,64,40,'#292229',true);
 txt(s,sub,56,103,1150,40,23,'#A63861');
 const bytes=await fs.readFile(path.join(build,img));
 const width=bytes.readUInt32BE(16),height=bytes.readUInt32BE(20);
 const crop=img==='catalog.png'||img==='settings.png'||img==='accounts.png'
  ? {left:0,top:0,right:0,bottom:Math.max(0,1-(width*.78)/height)} : undefined;
 s.images.add({blob:new Uint8Array(bytes),contentType:'image/png',alt:sub,fit:'contain',...(crop?{crop}:{}),position:{left:50,top:170,width:860,height:480}});
 txt(s,body,950,220,270,250,25);
 txt(s,'Actual application UI. Admin views use demonstration data.',950,540,270,75,14,'#75616C');
 txt(s,`${i+1} / ${items.length}`,1150,670,100,25,14,'#75616C');
 const png=await p.export({slide:s,format:'png',scale:.65});await fs.writeFile(path.join(build,`preview-${i+1}.png`),new Uint8Array(await png.arrayBuffer()));
}
const candidate=path.join(build,'ui-candidate.pptx');await(await PresentationFile.exportPptx(p)).save(candidate);
const skill='C:/Users/zisla/.codex/plugins/cache/openai-primary-runtime/presentations/26.1116.12020/skills/presentations';
const {finalizePresentation}=await import(pathToFileURL(skill+'/container_tools/artifact_tool_utils.mjs'));
await finalizePresentation({workspaceDir:process.cwd(),candidatePath:candidate,finalPath:path.join(out,'OneCart-UI-Presentation.pptx'),pythonExecutable:runtime+'/python/python.exe',integrityValidatorPath:skill+'/container_tools/inspect_presentation_package_integrity.py',layoutValidatorPath:skill+'/container_tools/inspect_presentation_layout_geometry.py',layoutArgs:['--expected-slide-size-emu','12192000,6858000','--validate-heading-fit'],fontPolicy:{basis:'design',families:['Arial']},verifyArtifactToolImport:true,receiptPath:path.join(build,'ui-validation.json')});
const esc=s=>s.replaceAll('&','&amp;').replaceAll('<','&lt;');
await fs.mkdir(path.join(out,'ui-images'),{recursive:true});
for(const item of items)await fs.copyFile(path.join(build,item[2]),path.join(out,'ui-images',item[2]));
await fs.writeFile(path.join(out,'ui-presentation.html'),`<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>OneCart UI presentation</title><style>body{margin:0;background:#fcfafb;color:#292229;font:18px/1.5 'Segoe UI',sans-serif}section{max-width:1200px;margin:35px auto;padding:35px;border-bottom:1px solid #eadce2}h1{font-size:38px}h2{color:#a63861}img{max-width:100%;max-height:75vh;object-fit:contain;display:block;margin:auto}small{color:#75616c}@media print{section{break-after:page}}</style><a href="OneCart-UI-Presentation.pptx">Download PowerPoint</a>${items.map(([t,s,img,b])=>`<section><h1>${esc(t)}</h1><h2>${esc(s)}</h2><img src="ui-images/${img}" alt="${esc(s)}"><p>${esc(b)}</p><small>Actual application UI. Admin views use demonstration data.</small></section>`).join('')}</html>`);
console.log('Created screenshot presentation and HTML edition.');
