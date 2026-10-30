import {test} from 'node:test';
import assert from 'node:assert/strict';
import {utils,write} from 'xlsx';
import {containerApi} from '../src/services/container-api.js';
import {filePreviewType,validateTrackingFiles,readWorkbookPreview} from '../src/services/container-tracking.js';
test('tracking update sends one versioned multipart request containing fields and files',async()=>{
 let call; const api=containerApi(async(path,options)=>{call={path,options};return {id:7};});
 const file=new Blob(['%PDF-1.7'],{type:'application/pdf'});Object.defineProperty(file,'name',{value:'evidence.pdf'});
 await api.track(7,'4',{status:3,eventAt:'2026-10-23T12:00:00Z',location:'Arabian Sea',isOnHold:false},[file]);
 assert.equal(call.path,'/containers/7/tracking');assert.equal(call.options.version,'4');assert.equal(call.options.method,'POST');
 assert.equal(call.options.body.get('status'),'3');assert.equal(call.options.body.get('isOnHold'),'false');assert.equal(call.options.body.getAll('files')[0].name,'evidence.pdf');
});
test('tracking files validate types, individual sizes and total limits',()=>{
 assert.equal(validateTrackingFiles([{name:'photo.webp',size:100},{name:'legacy.xls',size:100},{name:'modern.xlsx',size:100}]),'');
 assert.ok(validateTrackingFiles([{name:'script.html',size:100}]));assert.ok(validateTrackingFiles([{name:'photo.png',size:0}]));
 assert.ok(validateTrackingFiles([{name:'large.pdf',size:10485761}]));assert.ok(validateTrackingFiles(Array.from({length:11},()=>({name:'photo.png',size:1}))));
 assert.ok(validateTrackingFiles(Array.from({length:6},()=>({name:'photo.png',size:10485760}))));
 assert.equal(filePreviewType({fileName:'PROOF.PDF'}),'pdf');assert.equal(filePreviewType({fileName:'cargo.xls'}),'excel');assert.equal(filePreviewType({fileName:'cargo.xlsx'}),'excel');assert.equal(filePreviewType({fileName:'photo.png'}),'image');
});
for(const bookType of ['xls','xlsx'])test(bookType+' preview reads sheets as text and limits visible rows and columns',async()=>{
 const workbook=utils.book_new();const rows=Array.from({length:220},(_,i)=>Array.from({length:32},(_,j)=>i+':'+j));
 utils.book_append_sheet(workbook,utils.aoa_to_sheet(rows),'Cargo');utils.book_append_sheet(workbook,utils.aoa_to_sheet([['<script>text only</script>']]),'Notes');
 const buffer=write(workbook,{bookType,type:'array'});const preview=await readWorkbookPreview(buffer);
 assert.equal(preview.length,2);assert.equal(preview[0].name,'Cargo');assert.equal(preview[0].rows.length,200);assert.equal(preview[0].rows[0].length,30);assert.equal(preview[0].truncated,true);
 assert.equal(preview[1].rows[0][0],'<script>text only</script>');
});
