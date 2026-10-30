import { trackingStatusName } from './container-tracking.js';
const statusNames = { 1: 'Booked', 2: 'Cancelled' };
function cell(value) {
  let text = String(value ?? '');
  // Prevent spreadsheet formulas from executing when opening an exported CSV.
  if (/^[\s]*[=+@-]/.test(text)) text = "'" + text;
  return '"' + text.replaceAll('"', '""') + '"';
}
export function containerCsv(items) {
  const columns = ['Booking', 'Container number', 'Shipper', 'Consignee', 'Container type', 'Cargo', 'Weight (kg)', 'Packages', 'Origin', 'Destination', 'Departure', 'Expected arrival', 'Status', 'Reference', 'Tracking status', 'On hold'];
  const rows = items.map(x => [x.code, x.containerNumber, x.shipperName, x.consigneeName, x.containerType, x.cargo, x.weightKg, x.packages, x.originPortName, x.destinationPortName, x.departure, x.expectedArrival, statusNames[x.status] || x.status, x.reference, trackingStatusName(x.trackingStatus), x.isOnHold ? 'Yes' : 'No']);
  return '\uFEFF' + [columns, ...rows].map(row => row.map(cell).join(',')).join('\r\n');
}
export async function fetchContainerExport(api, search, status) {
  const items = []; let page = 1;
  while (true) {
    const result = await api.list(page++, 100, search, status);
    items.push(...result.items);
    if (!result.items.length || items.length >= result.totalCount) return items;
  }
}
