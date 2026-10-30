export const trackingStatuses = [{id:1,name:'Booked'},{id:2,name:'Loaded'},{id:3,name:'In transit'},{id:4,name:'Arrived'},{id:5,name:'Customs clearance'},{id:6,name:'Delivered'}];
export function trackingStatusName(status) { return trackingStatuses.find(item => item.id === status)?.name || 'Booked'; }
export function filePreviewType(file) {
  const extension = file.fileName?.split('.').pop()?.toLowerCase();
  if (['jpg','jpeg','png','webp'].includes(extension)) return 'image';
  if (extension === 'pdf') return 'pdf';
  if (['xls','xlsx'].includes(extension)) return 'excel';
  return 'download';
}
export function validateTrackingFiles(files) {
  const allowed = ['jpg','jpeg','png','webp','pdf','xls','xlsx'];
  if (files.length > 10) return 'Attach up to 10 files per tracking update.';
  if (files.some(file => !allowed.includes(file.name.split('.').pop().toLowerCase()) || file.size === 0 || file.size > 10485760)) return 'Choose JPEG, PNG, WebP, PDF or Excel (.xls/.xlsx) files, up to 10 MB each.';
  if (files.reduce((size,file) => size + file.size, 0) > 50 * 1024 * 1024) return 'The combined upload size must be 50 MB or less.';
  return '';
}
export async function readWorkbookPreview(buffer) {
  const {read, utils} = await import('xlsx');
  const workbook = read(buffer, {type:'array', sheetRows:201, dense:true, cellFormula:false, cellHTML:false});
  return workbook.SheetNames.map(name => {
    const sheet = workbook.Sheets[name]; const range = utils.decode_range(sheet['!ref'] || 'A1');
    const rows = utils.sheet_to_json(sheet, {header:1, raw:false, defval:'', blankrows:true, range:{s:{r:0,c:0},e:{r:Math.min(range.e.r,199),c:Math.min(range.e.c,29)}}});
    return {name, rows, truncated:range.e.r >= 200 || range.e.c >= 30 || !!sheet['!fullref']};
  });
}
