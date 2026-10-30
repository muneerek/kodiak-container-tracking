export function employeeApi(request) {
  return {
    list: (page = 1, pageSize = 25) => request('/employees?page=' + page + '&pageSize=' + pageSize),
    get: id => request('/employees/' + id),
    create: body => request('/employees', { method: 'POST', body }),
    update: (id, version, body) => request('/employees/' + id, { method: 'PUT', version, body }),
    remove: (id, version) => request('/employees/' + id, { method: 'DELETE', version }),
    photo: (id, signal) => request('/employees/' + id + '/photo', { responseType: 'blob', signal }),
    uploadPhoto(id, version, blob) { const body = new FormData(); body.append('photo', blob, 'photo.jpg'); return request('/employees/' + id + '/photo', { method: 'PUT', version, body }); },
    removePhoto: (id, version) => request('/employees/' + id + '/photo', { method: 'DELETE', version }),
    async masters(kind) {
      const records = []; let page = 1; let total;
      do { const result = await request('/masters/' + kind + '?page=' + page++ + '&pageSize=100'); records.push(...result.items); total = result.totalCount; } while (records.length < total);
      return records;
    }
  };
}
