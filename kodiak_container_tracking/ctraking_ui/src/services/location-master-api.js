export function locationMasterApi(kind, request) {
  if (!['Countries', 'States', 'ShippingPorts'].includes(kind)) throw new Error('Unsupported location master.');
  const path = '/masters/' + kind;
  return {
    list: (page = 1, pageSize = 25) => request(path + '?page=' + page + '&pageSize=' + pageSize),
    get: id => request(path + '/' + id), create: body => request(path, { method: 'POST', body }),
    update: (id, version, body) => request(path + '/' + id, { method: 'PUT', version, body }),
    remove: (id, version) => request(path + '/' + id, { method: 'DELETE', version }),
    nextPortCode: () => request('/masters/ShippingPorts/next-code'),
    timeZones: () => request('/masters/timezones'),
    async lookup(lookupKind) {
      const items = []; let page = 1; let total;
      do { const result = await request('/masters/' + lookupKind + '?page=' + page++ + '&pageSize=100'); items.push(...result.items); total = result.totalCount; } while (items.length < total);
      return items;
    }
  };
}
