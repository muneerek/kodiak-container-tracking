export const statuses = ['Booked', 'At origin', 'In transit', 'At destination', 'Delivered'];
export const demoAccounts = [
  { id: 'u1', name: 'Alex Morgan', email: 'admin@kodiak.demo', password: 'Demo123!', role: 'admin', org: 'kodiak', active: true },
  { id: 'u2', name: 'Omar Khalid', email: 'distributor@kodiak.demo', password: 'Demo123!', role: 'distributor', org: 'd1', active: true },
  { id: 'u3', name: 'Priya Sharma', email: 'supplier@kodiak.demo', password: 'Demo123!', role: 'supplier', org: 's1', active: true },
];
const event = (status, date, location, note) => ({ status, date, location, note, by: 'Alex Morgan', recordedAt: date });
export function initialData() {
  return {
    users: structuredClone(demoAccounts),
    distributors: [
      { id: 'd1', name: 'GulfBridge Distribution', contact: 'Omar Khalid', email: 'omar@gulfbridge.example', phone: '+971 50 123 4567', country: 'United Arab Emirates', address: 'Jebel Ali Free Zone, Dubai', active: true },
      { id: 'd2', name: 'Meridian Trade Partners', contact: 'Sarah Lim', email: 'sarah@meridian.example', phone: '+65 6123 4567', country: 'Singapore', address: 'HarbourFront, Singapore', active: true },
    ],
    suppliers: [
      { id: 's1', name: 'Atlas Exports', contact: 'Priya Sharma', email: 'priya@atlas.example', phone: '+91 98200 12345', country: 'India', distributorId: 'd1', state: 'Active', category: 'Textiles & apparel', address: 'Andheri East, Mumbai', reason: '' },
      { id: 's2', name: 'Coastal Foods', contact: 'Rahul Menon', email: 'rahul@coastal.example', phone: '+91 98400 12345', country: 'India', distributorId: 'd1', state: 'Active', category: 'Food & produce', address: 'Kochi, Kerala', reason: '' },
      { id: 's3', name: 'Pacific Components', contact: 'Wei Chen', email: 'wei@pacific.example', phone: '+86 138 0013 8000', country: 'China', distributorId: 'd2', state: 'Active', category: 'Electronics', address: 'Shenzhen, Guangdong', reason: '' },
      { id: 's4', name: 'Northstar Trading', contact: 'James Lee', email: 'james@northstar.example', phone: '+65 8123 4567', country: 'Singapore', distributorId: 'd2', state: 'Blacklisted', category: 'General cargo', address: 'Singapore', reason: 'Repeated documentation discrepancies. New bookings restricted pending review.', restrictedAt: '2026-09-26T10:00', restrictedBy: 'Alex Morgan' },
      { id: 's5', name: 'Evergreen Ceramics', contact: 'Meera Shah', email: 'meera@evergreen.example', phone: '+91 99200 33344', country: 'India', distributorId: 'd1', state: 'Pending', category: 'Building materials', address: 'Morbi, Gujarat', reason: '' },
    ],
    countries: [{ id: 'IN', name: 'India', code: 'IN' }, { id: 'AE', name: 'United Arab Emirates', code: 'AE' }, { id: 'SG', name: 'Singapore', code: 'SG' }, { id: 'CN', name: 'China', code: 'CN' }],
    states: [{ id: 'MH', name: 'Maharashtra', countryId: 'IN' }, { id: 'KL', name: 'Kerala', countryId: 'IN' }, { id: 'DU', name: 'Dubai', countryId: 'AE' }, { id: 'SGS', name: 'Singapore', countryId: 'SG' }, { id: 'GD', name: 'Guangdong', countryId: 'CN' }],
    ports: [
      { id: 'INNSA', name: 'Nhava Sheva', city: 'Mumbai', countryId: 'IN', stateId: 'MH', code: 'INNSA' },
      { id: 'AEJEA', name: 'Jebel Ali', city: 'Dubai', countryId: 'AE', stateId: 'DU', code: 'AEJEA' },
      { id: 'INCOK', name: 'Cochin', city: 'Kochi', countryId: 'IN', stateId: 'KL', code: 'INCOK' },
      { id: 'SGSIN', name: 'Singapore', city: 'Singapore', countryId: 'SG', stateId: 'SGS', code: 'SGSIN' },
      { id: 'CNSZX', name: 'Shenzhen', city: 'Shenzhen', countryId: 'CN', stateId: 'GD', code: 'CNSZX' },
    ],
    containers: [
      { id: 'KD-2048', number: 'MSCU 782341-6', supplierId: 's1', distributorId: 'd1', type: '40′ High Cube', origin: 'INNSA', destination: 'AEJEA', cargo: 'Cotton textiles & finished apparel', weight: 18400, packages: 320, departure: '2026-09-28', eta: '2026-10-06', status: 'In transit', hold: false, holdReason: '', blacklisted: false, vessel: 'MSC Aurora', reference: 'ATL-2026-089', created: '2026-09-24', events: [event('Booked', '2026-09-24T09:30', 'Mumbai', 'Booking reviewed and confirmed by Kodiak operations.'), event('At origin', '2026-09-26T14:20', 'Nhava Sheva · INNSA', 'Container received at terminal. Seal and documents verified.'), event('In transit', '2026-09-28T18:45', 'Arabian Sea', 'Loaded on MSC Aurora. Vessel departed Nhava Sheva for Jebel Ali.')] },
      { id: 'KD-2047', number: 'TCLU 459812-3', supplierId: 's2', distributorId: 'd1', type: '20′ Reefer', origin: 'INCOK', destination: 'AEJEA', cargo: 'Packaged frozen produce', weight: 12600, packages: 480, departure: '2026-09-29', eta: '2026-10-05', status: 'In transit', hold: false, blacklisted: false, vessel: 'Ocean Pearl', reference: 'CF-102', created: '2026-09-25', events: [event('Booked','2026-09-25T10:00','Kochi','Booking confirmed.'),event('At origin','2026-09-27T11:00','Cochin','Temperature and seal checked.'),event('In transit','2026-09-29T16:00','Arabian Sea','Vessel departure confirmed manually.')] },
      { id: 'KD-2046', number: 'CMAU 621903-4', supplierId: 's3', distributorId: 'd2', type: '40′ Standard', origin: 'CNSZX', destination: 'SGSIN', cargo: 'Electronic components', weight: 21800, packages: 240, departure: '2026-09-25', eta: '2026-10-02', status: 'At destination', hold: true, holdReason: 'Awaiting corrected commercial invoice for destination release.', blacklisted: false, vessel: 'Pacific Horizon', reference: 'PC-442', created: '2026-09-22', events: [event('Booked','2026-09-22T09:00','Shenzhen','Booking confirmed.'),event('At origin','2026-09-24T10:00','Shenzhen','Received at port.'),event('In transit','2026-09-25T15:00','South China Sea','Vessel departed.'),event('At destination','2026-10-02T08:30','Singapore','Arrived at destination. Documentation hold applied.')] },
      { id: 'KD-2045', number: 'TEMU 308754-1', supplierId: 's1', distributorId: 'd1', type: '20′ Standard', origin: 'INNSA', destination: 'AEJEA', cargo: 'Woven home furnishings', weight: 9200, packages: 180, departure: '2026-10-05', eta: '2026-10-12', status: 'At origin', hold: false, blacklisted: false, vessel: 'Gulf Voyager', reference: 'ATL-2026-090', created: '2026-09-29', events: [event('Booked','2026-09-29T09:00','Mumbai','Supplier booking received.'),event('At origin','2026-10-01T12:00','Nhava Sheva','Container received and ready for loading.')] },
      { id: 'KD-2044', number: 'FCIU 912047-8', supplierId: 's2', distributorId: 'd1', type: '20′ Reefer', origin: 'INCOK', destination: 'SGSIN', cargo: 'Processed food products', weight: 11600, packages: 360, departure: '2026-09-18', eta: '2026-09-26', status: 'Delivered', hold: false, blacklisted: false, vessel: 'Eastern Star', reference: 'CF-098', created: '2026-09-15', events: [event('Booked','2026-09-15T09:00','Kochi','Booking confirmed.'),event('At origin','2026-09-17T10:00','Cochin','Cargo received.'),event('In transit','2026-09-18T15:00','Indian Ocean','Departed.'),event('At destination','2026-09-25T07:00','Singapore','Discharged at terminal.'),event('Delivered','2026-09-26T14:30','Singapore','Delivery confirmed with recipient.')] },
      { id: 'KD-2043', number: 'Pending allocation', supplierId: 's1', distributorId: 'd1', type: '40′ High Cube', origin: 'INNSA', destination: 'SGSIN', cargo: 'Readymade garments', weight: 16800, packages: 260, departure: '2026-10-10', eta: '2026-10-20', status: 'Booked', hold: false, blacklisted: false, vessel: '', reference: 'ATL-2026-091', created: '2026-10-02', events: [event('Booked','2026-10-02T10:00','Mumbai','Supplier submitted container registration. Awaiting allocation.')] },
      { id: 'KD-2042', number: 'OOLU 567234-9', supplierId: 's3', distributorId: 'd2', type: '40′ Standard', origin: 'CNSZX', destination: 'AEJEA', cargo: 'Industrial control units', weight: 19200, packages: 120, departure: '2026-09-15', eta: '2026-10-01', status: 'In transit', hold: false, blacklisted: false, vessel: 'Orient Express', reference: 'PC-439', created: '2026-09-12', events: [event('Booked','2026-09-12T10:00','Shenzhen','Booking confirmed.'),event('At origin','2026-09-14T12:00','Shenzhen','Received at origin.'),event('In transit','2026-09-15T17:00','Indian Ocean','Departed; revised arrival confirmation requested.')] },
      { id: 'KD-2041', number: 'HLXU 829105-2', supplierId: 's4', distributorId: 'd2', type: '20′ Standard', origin: 'SGSIN', destination: 'AEJEA', cargo: 'General merchandise', weight: 8400, packages: 90, departure: '2026-09-20', eta: '2026-10-04', status: 'In transit', hold: true, holdReason: 'Kodiak review required following supplier restriction.', blacklisted: false, vessel: 'Gulf Navigator', reference: 'NT-021', created: '2026-09-16', events: [event('Booked','2026-09-16T09:00','Singapore','Booking confirmed.'),event('At origin','2026-09-19T10:00','Singapore','Container received.'),event('In transit','2026-09-20T14:00','Indian Ocean','Departed. Existing shipment remains visible during review.')] },
    ],
    notifications: [
      { id: 'n1', title: 'Documentation needs attention', text: 'KD-2046 is on hold pending a corrected invoice.', containerId: 'KD-2046', date: '2026-10-02T08:30', readBy: [] },
      { id: 'n2', title: 'New container registration', text: 'Atlas Exports submitted KD-2043 for review.', containerId: 'KD-2043', date: '2026-10-02T10:00', readBy: [] },
      { id: 'n3', title: 'Container received at origin', text: 'KD-2045 is ready for loading at Nhava Sheva.', containerId: 'KD-2045', date: '2026-10-01T12:00', readBy: [] },
    ],
    audit: [],
  };
}
export function visibleContainers(data, user) {
  if (!user) return [];
  return data.containers.filter(c => user.role === 'admin' || (user.role === 'supplier' ? c.supplierId === user.org : c.distributorId === user.org));
}
export function visibleSuppliers(data, user) {
  if (!user) return [];
  return data.suppliers.filter(s => user.role === 'admin' || (user.role === 'supplier' ? s.id === user.org : s.distributorId === user.org));
}
export function canBook(data, supplierId) { return data.suppliers.some(s => s.id === supplierId && s.state === 'Active' && s.distributorId); }
export function containerNumberIssue(data, number, excludeId = '') {
  const normalized = String(number || '').replace(/[^a-z0-9]/gi, '').toUpperCase();
  if (!normalized || normalized === 'PENDINGALLOCATION') return '';
  const matches = data.containers.filter(c => c.id !== excludeId && c.number.replace(/[^a-z0-9]/gi, '').toUpperCase() === normalized);
  if (matches.some(c => c.blacklisted)) return 'This physical container is blacklisted and cannot be allocated to a new journey.';
  if (matches.some(c => c.status !== 'Delivered')) return 'This container already has an active journey.';
  return '';
}
export function isOverdue(c, today = new Date().toISOString().slice(0,10)) { return !!c.eta && c.eta < today && !['At destination','Delivered'].includes(c.status); }
