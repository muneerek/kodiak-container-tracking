export function containerApi(request) {
  return {
    list: (page = 1, pageSize = 25, search = '', status = '') => request('/containers?page=' + page + '&pageSize=' + pageSize + '&search=' + encodeURIComponent(search) + (status === '' ? '' : '&status=' + encodeURIComponent(status))),
    trackingList(page = 1, pageSize = 12, filters = {}) {
      const params = new URLSearchParams({page:String(page),pageSize:String(pageSize)});
      for (const [key,value] of Object.entries(filters)) if (value !== '' && value !== null && value !== undefined) params.set(key,String(value));
      return request('/containers/tracking?' + params.toString());
    },
    get: id => request('/containers/' + id), create: body => request('/containers', { method: 'POST', body }),
    update: (id, version, body) => request('/containers/' + id, { method: 'PUT', version, body }),
    track(id, version, update, files = []) {
      const body = new FormData();
      for (const [key, value] of Object.entries(update)) if (value !== null && value !== undefined) body.append(key, String(value));
      for (const file of files) body.append('files', file, file.name);
      return request('/containers/' + id + '/tracking', { method: 'POST', version, body, signal: AbortSignal.timeout(120000) });
    },
    cancel: (id, version) => request('/containers/' + id, { method: 'DELETE', version }),
    async options(kind) { const items = []; let page = 1; let total; do { const result = await request('/containers/options/' + kind + '?page=' + page++ + '&pageSize=100'); items.push(...result.items); total = result.totalCount; } while (items.length < total); return items; },
    upload(id, version, file, kind) { const body = new FormData(); body.append('file', file, file.name); body.append('kind', String(kind)); return request('/containers/' + id + '/files', { method: 'POST', version, body }); },
    removeFile: (id, fileId, version) => request('/containers/' + id + '/files/' + fileId, { method: 'DELETE', version }),
    download: (id, fileId) => request('/containers/' + id + '/files/' + fileId, { responseType: 'blob' })
  };
}
