import fs from 'node:fs/promises';
import path from 'node:path';
import { pathToFileURL } from 'node:url';
const runtime='C:/Users/zisla/.cache/codex-runtimes/codex-primary-runtime/dependencies';
const {Presentation,PresentationFile}=await import(pathToFileURL(runtime+'/node/node_modules/@oai/artifact-tool/dist/artifact_tool.mjs'));
const dir=path.resolve('docs/handbook');
const build=path.resolve('artifacts/handbook-build');
await fs.mkdir(build,{recursive:true});
const slides=JSON.parse(await fs.readFile(path.join(dir,'slides.json'),'utf8'));
const presentation=Presentation.create({slideSize:{width:1280,height:720}});
function text(slide,value,x,y,w,h,size,color,bold=false){const s=slide.shapes.add({geometry:'textbox',position:{left:x,top:y,width:w,height:h},fill:'none',line:{fill:'none',width:0}});s.text=value;s.text.style={fontFamily:'Arial',fontSize:size,color,bold};return s;}
for(let i=0;i<slides.length;i++){
 const [title,subtitle,points]=slides[i];
 const slide=presentation.slides.add();
 const dark=i%2===0;
 slide.background.fill=dark?'#292229':'#FFFFFF';
 text(slide,'OneCart Commerce',76,42,900,34,20,dark?'#DCA3B7':'#9E355A',true);
 text(slide,title,76,122,1120,92,50,dark?'#FFFFFF':'#292229',true);
 text(slide,subtitle,76,226,1110,82,30,dark?'#E6BBCB':'#A53861');
 points.forEach((value,j)=>{
  text(slide,String(j+1).padStart(2,'0'),80,346+j*67,54,46,21,'#C16B8B',true);
  text(slide,value,154,342+j*67,1030,58,25,dark?'#FAF4F7':'#393039');
 });
 text(slide,`${String(i+1).padStart(2,'0')} / ${slides.length}`,1100,663,100,25,14,dark?'#C7B8C0':'#76636D');
 const png=await presentation.export({slide,format:'png',scale:0.7});
 await fs.writeFile(path.join(build,`slide-${i+1}.png`),new Uint8Array(await png.arrayBuffer()));
}
const candidate=path.join(build,'candidate.pptx');
await (await PresentationFile.exportPptx(presentation)).save(candidate);
const skill='C:/Users/zisla/.codex/plugins/cache/openai-primary-runtime/presentations/26.1116.12020/skills/presentations';
const {finalizePresentation}=await import(pathToFileURL(skill+'/container_tools/artifact_tool_utils.mjs'));
const result=await finalizePresentation({workspaceDir:process.cwd(),candidatePath:candidate,finalPath:path.join(dir,'OneCart-Marketing.pptx'),pythonExecutable:runtime+'/python/python.exe',integrityValidatorPath:skill+'/container_tools/inspect_presentation_package_integrity.py',layoutValidatorPath:skill+'/container_tools/inspect_presentation_layout_geometry.py',layoutArgs:['--expected-slide-size-emu','12192000,6858000','--validate-heading-fit'],fontPolicy:{basis:'design',families:['Arial']},verifyArtifactToolImport:true,receiptPath:path.join(build,'validation.json')});
console.log(result);
