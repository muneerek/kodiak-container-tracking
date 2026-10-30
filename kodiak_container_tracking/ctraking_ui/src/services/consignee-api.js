export function consigneeApi(request) {
  return {
    list: (page = 1, pageSize = 25, search = '') => request('/consignees?page=' + page + '&pageSize=' + pageSize + (search ? '&search=' + encodeURIComponent(search) : '')),
    get: id => request('/consignees/' + id),
    create: body => request('/consignees', { method: 'POST', body }),
    update: (id, version, body) => request('/consignees/' + id, { method: 'PUT', version, body }),
    remove: (id, version) => request('/consignees/' + id, { method: 'DELETE', version }),
    async countries() {
      const records = []; let page = 1; let total;
      do { const result = await request('/masters/Countries?page=' + page++ + '&pageSize=100'); records.push(...result.items); total = result.totalCount; } while (records.length < total);
      return records;
    }
  };
}
